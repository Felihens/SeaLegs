using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;
using UnityEngine.Networking;

namespace SeaLegs
{
    // The song files. They come from the mod's own "songs" folder and from BepInEx/config/SeaLegs/songs,
    // where players can drop their own. The start of each file name says what it is:
    //   m_  sung by men      f_  sung by women      i_  instrumental (a breather between songs)
    // Files without one of these count as sung, for any crew.
    // A song can come as one file, or split in two: name_inst (the instruments) and name_vocals (the
    // voice alone). Split songs sound better: the instruments play once for the boat and each singer
    // sings the voice at their own pitch.
    // Songs are loaded only when needed, a few at a time, so they don't fill the memory.
    internal static class SongLib
    {
        internal class Track
        {
            public string Name;    // file name without extension, lower case: what the crew shares
            public string Full;    // the whole song in one file (may be null when split)
            public string Inst;    // instruments only (split songs)
            public string Vocals;  // voice only (split songs)
            public string Title;
            public char Kind;      // 'm', 'f', 'i' or 'x' (sung, any crew)
            public bool Sung { get { return Kind != 'i'; } }
            public bool Split { get { return Inst != null && Vocals != null; } }
            public string MainFile { get { return Split ? Inst : Full ?? Inst ?? Vocals; } }
            public bool Playable { get { return Full != null || Split; } }
        }

        private static readonly List<Track> All = new List<Track>();
        private static readonly Dictionary<string, Track> ByName = new Dictionary<string, Track>();
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();   // by file path
        private static readonly Dictionary<string, float> LastUse = new Dictionary<string, float>();
        private static readonly HashSet<string> Loading = new HashSet<string>();
        private static readonly HashSet<string> Failed = new HashSet<string>();
        private const int KeepClips = 6;

        internal static string UserFolder { get { return System.IO.Path.Combine(Paths.ConfigPath, "SeaLegs" + System.IO.Path.DirectorySeparatorChar + "songs"); } }

        internal static void Scan(string pluginDir)
        {
            All.Clear();
            ByName.Clear();
            try
            {
                if (!Directory.Exists(UserFolder)) Directory.CreateDirectory(UserFolder);
                string readme = System.IO.Path.Combine(UserFolder, "README.txt");
                File.WriteAllText(readme,
                    "Put your own crew songs here (.mp3, .ogg or .wav).\r\n" +
                    "Start the file name with:\r\n" +
                    "  m_  sung by men       (example: m_my_shanty.mp3)\r\n" +
                    "  f_  sung by women     (example: f_my_song.ogg)\r\n" +
                    "  i_  instrumental, played as a breather after every two sung songs\r\n" +
                    "Files without one of these count as sung, for any crew.\r\n" +
                    "\r\n" +
                    "Best: split a sung song in two files, the instruments and the voice alone:\r\n" +
                    "  m_my_shanty_inst.mp3    and    m_my_shanty_vocals.mp3\r\n" +
                    "Then each singer on the boat sings the voice at their own pitch (VoiceType in the config).\r\n" +
                    "Free tools can split a song for you, such as Ultimate Vocal Remover or Demucs.\r\n" +
                    "\r\n" +
                    "The whole crew hears a song only if everyone has the same files.\r\n");
            }
            catch (Exception e) { Plugin.Log.LogWarning("Could not prepare " + UserFolder + ": " + e.Message); }

            foreach (string dir in new[] { System.IO.Path.Combine(pluginDir, "songs"), UserFolder })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (string f in Directory.GetFiles(dir))
                {
                    string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".mp3" && ext != ".ogg" && ext != ".wav") continue;
                    string name = System.IO.Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Replace('|', '_');
                    int part = 0;   // 0 whole song, 1 instruments, 2 voice
                    foreach (string end in new[] { "_inst", ".inst", "_instrumental", "_no_vocals" })
                        if (name.EndsWith(end) && name.Length > end.Length) { name = name.Substring(0, name.Length - end.Length); part = 1; break; }
                    if (part == 0)
                        foreach (string end in new[] { "_vocals", ".vocals", "_vocal", "_voice" })
                            if (name.EndsWith(end) && name.Length > end.Length) { name = name.Substring(0, name.Length - end.Length); part = 2; break; }

                    Track t;
                    if (!ByName.TryGetValue(name, out t))
                    {
                        char kind = name.Length > 2 && name[1] == '_' && "mfi".IndexOf(name[0]) >= 0 ? name[0] : 'x';
                        t = new Track { Name = name, Kind = kind, Title = MakeTitle(kind == 'x' ? name : name.Substring(2)) };
                        ByName[name] = t;
                        All.Add(t);
                    }
                    if (part == 0 && t.Full == null) t.Full = f;
                    else if (part == 1 && t.Inst == null) t.Inst = f;
                    else if (part == 2 && t.Vocals == null) t.Vocals = f;
                }
            }
            for (int k = All.Count - 1; k >= 0; k--)
                if (!All[k].Playable)
                {
                    Plugin.Log.LogWarning("Song " + All[k].Name + " is missing its " + (All[k].Inst == null ? "_inst" : "_vocals") + " file; skipped");
                    ByName.Remove(All[k].Name);
                    All.RemoveAt(k);
                }
            int m = 0, w = 0, i = 0, x = 0, split = 0;
            foreach (Track t in All)
            {
                if (t.Kind == 'm') m++; else if (t.Kind == 'f') w++; else if (t.Kind == 'i') i++; else x++;
                if (t.Split) split++;
            }
            Plugin.Log.LogInfo("Crew songs: " + m + " men, " + w + " women, " + x + " any crew, " + i + " instrumental (" + split + " split in voice + instruments)");
        }

        private static string MakeTitle(string s)
        {
            string[] words = s.Replace('_', ' ').Replace('-', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int k = 0; k < words.Length; k++)
                words[k] = char.ToUpperInvariant(words[k][0]) + words[k].Substring(1);
            return string.Join(" ", words);
        }

        private static HashSet<string> Disabled()
        {
            HashSet<string> set = new HashSet<string>();
            foreach (string s in (Plugin.DisabledSongs.Value ?? "").Split(','))
            {
                string n = s.Trim().ToLowerInvariant();
                if (n.Length > 0) set.Add(System.IO.Path.GetFileNameWithoutExtension(n));
            }
            return set;
        }

        private static bool Broken(Track t) { return Failed.Contains(t.MainFile) || t.Split && Failed.Contains(t.Vocals); }

        // Tracks that may play for this crew: women-only crews get women's songs, men-only crews get
        // men's songs, mixed crews get everything. Instrumentals and "any crew" songs always fit.
        internal static void ForCrew(bool men, bool women, List<Track> sung, List<Track> instrumental)
        {
            sung.Clear();
            instrumental.Clear();
            HashSet<string> off = Disabled();
            foreach (Track t in All)
            {
                if (off.Contains(t.Name) || Broken(t)) continue;
                if (t.Kind == 'i') { instrumental.Add(t); continue; }
                if (t.Kind == 'm' && !men && women) continue;
                if (t.Kind == 'f' && !women && men) continue;
                sung.Add(t);
            }
            if (sung.Count == 0)   // nothing for this crew: allow every sung track
                foreach (Track t in All) if (t.Sung && !off.Contains(t.Name) && !Broken(t)) sung.Add(t);
        }

        internal static bool HasAny { get { return All.Count > 0; } }

        internal static Track Find(string name)
        {
            Track t;
            return name != null && ByName.TryGetValue(name, out t) ? t : null;
        }

        internal static bool HasFailed(Track t) { return t != null && Broken(t); }

        // What plays once for the boat: the instruments of a split song, or the whole song.
        internal static AudioClip Get(Track t) { return t != null ? Clip(t.MainFile) : null; }

        // The voice alone, for split songs (null otherwise or while loading).
        internal static AudioClip Voice(Track t) { return t != null && t.Split ? Clip(t.Vocals) : null; }

        // Main part and voice both ready (or no voice needed).
        internal static bool Ready(Track t) { return Get(t) != null && (!t.Split || Voice(t) != null); }

        private static AudioClip Clip(string path)
        {
            if (path == null || Failed.Contains(path)) return null;
            LastUse[path] = Time.time;
            AudioClip c;
            if (Clips.TryGetValue(path, out c) && c != null) return c;
            if (!Loading.Contains(path) && Plugin.Instance != null)
            {
                Loading.Add(path);
                Plugin.Instance.StartCoroutine(Load(path));
            }
            return null;
        }

        private static IEnumerator Load(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            AudioType type = ext == ".ogg" ? AudioType.OGGVORBIS : ext == ".wav" ? AudioType.WAV : AudioType.MPEG;
            float started = Time.realtimeSinceStartup;
            string file = System.IO.Path.GetFileName(path);
            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type))
            {
                yield return req.SendWebRequest();
                Loading.Remove(path);
                AudioClip clip = null;
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try { clip = DownloadHandlerAudioClip.GetContent(req); } catch (Exception e) { Plugin.Log.LogWarning("Song " + file + ": " + e.Message); }
                }
                if (clip == null || clip.length <= 0f)
                {
                    Failed.Add(path);
                    Plugin.Log.LogWarning("Could not load the song " + path + (req.error != null ? ": " + req.error : ""));
                    yield break;
                }
                clip.name = "Sea Legs " + file;
                Clips[path] = clip;
                Plugin.Debug("[songs] loaded " + file + " (" + clip.length.ToString("0") + " s) in " + (Time.realtimeSinceStartup - started).ToString("0.00") + " s");
                Trim();
            }
        }

        // Keep only the few clips used most recently (the song playing and the next one).
        private static void Trim()
        {
            while (Clips.Count > KeepClips)
            {
                string oldest = null;
                float t = float.MaxValue;
                foreach (string n in Clips.Keys)
                {
                    float u;
                    if (!LastUse.TryGetValue(n, out u)) u = 0f;
                    if (u < t) { t = u; oldest = n; }
                }
                if (oldest == null) return;
                AudioClip c = Clips[oldest];
                Clips.Remove(oldest);
                if (c != null && !Shanty.Uses(c)) UnityEngine.Object.Destroy(c);
                Plugin.Debug("[songs] unloaded " + System.IO.Path.GetFileName(oldest));
            }
        }
    }
}
