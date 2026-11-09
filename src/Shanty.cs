using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SeaLegs
{
    // The sound of the crew song, heard by everyone on the boat who has the mod.
    //
    // One song plays for the whole boat. The playlist is shuffled when the crew starts singing, and it
    // always goes two sung songs, then one instrumental as a breather, and again. The songs fit the crew
    // on board: only women -> women's songs, only men -> men's songs, both -> all of them.
    //
    // Keeping everyone in time: each player writes the playlist they follow on their own character
    // (the only data a player may change), with the start time of the current song in the server's clock.
    // On a boat, everyone follows the oldest playlist found among the players on board, so someone who
    // comes aboard joins the song that is already playing instead of starting a new one. Whoever is first
    // in line moves the playlist on to the next song; the others copy it, so anyone can take over if that
    // player leaves. When nobody has sung on the boat for a minute, the playlist ends and the next song
    // starts a new shuffle.
    //
    // Each singer adds a quieter copy of the song at their own voice pitch, coming from their character,
    // so a bigger crew sounds like a bigger choir.
    [HarmonyPatch]
    internal static class Shanty
    {
        private const string KBorn = "SeaLegs_born", KShip = "SeaLegs_ship", KQueue = "SeaLegs_queue", KIndex = "SeaLegs_index",
            KStart = "SeaLegs_start", KLen = "SeaLegs_len", KLive = "SeaLegs_live", KGen = "SeaLegs_gen", KPool = "SeaLegs_pool",
            KVoice = "SeaLegs_voicelevel";
        private const float NoVoice = -999f;

        private class State
        {
            public long Born, Start, Live;
            public string Ship = "", Queue = "";
            public int Index, Gen, Pool;
            public float Len;

            public bool Same(State o)
            {
                return o != null && Born == o.Born && Start == o.Start && Live == o.Live && Ship == o.Ship && Queue == o.Queue &&
                       Index == o.Index && Gen == o.Gen && Pool == o.Pool && Len == o.Len;
            }

            public State Copy() { return (State)MemberwiseClone(); }

            public string[] Names { get { return Queue.Length == 0 ? new string[0] : Queue.Split('|'); } }

            public string Track
            {
                get
                {
                    string[] n = Names;
                    return Index >= 0 && Index < n.Length ? n[Index] : null;
                }
            }

            public string Next
            {
                get
                {
                    string[] n = Names;
                    return Index + 1 >= 0 && Index + 1 < n.Length ? n[Index + 1] : null;
                }
            }
        }

        private static State _me;          // the playlist we follow (or lead); null = none
        private static State _written;     // what is on our character right now
        private static float _writtenVoice = NoVoice;
        private static float _nextThink;
        private static Ship _ship;         // boat we are on (null = not on one)
        private static string _shipId;
        private static int _singers;
        private static bool _leading;

        private static readonly FieldInfo MusicSource = AccessTools.Field(typeof(MusicMan), "m_musicSource");
        private static readonly FieldInfo SfxMixer = AccessTools.Field(typeof(AudioMan), "m_ambientMixer");

        // ------------------------------------------------------------ playlist

        private static long Now()
        {
            return ZNet.instance != null ? (long)(ZNet.instance.GetTimeSeconds() * 1000.0) : 0L;
        }

        private static long Timeout { get { return (long)(Mathf.Max(5f, Plugin.ReshuffleSeconds.Value) * 1000f); } }

        internal static void Update()
        {
            if (!Plugin.SongSound.Value) { StopAudio(); return; }
            if (Time.time >= _nextThink)
            {
                _nextThink = Time.time + 0.25f;
                Think();
            }
            Audio();
        }

        private static void Think()
        {
            Player p = Player.m_localPlayer;
            _ship = null;
            _singers = 0;
            _leading = false;
            if (p == null || ZNet.instance == null) return;
            long now = Now();
            if (_me != null && now - _me.Live > Timeout)
            {
                Plugin.Debug("[shanty] nobody sang for " + (Timeout / 1000) + " s: the playlist ends, the next song starts a new shuffle");
                _me = null;
            }
            WriteVoice(p);

            Ship ship = Ship.GetLocalShip();
            if (p.IsDead() || ship == null || !Song.OnBoard(ship, p)) { Write(p); return; }
            string id = ShipId(ship);
            if (id == null) { Write(p); return; }
            _ship = ship;
            _shipId = id;
            _singers = Song.Singers(ship);

            // The oldest playlist on board wins; among copies of it, the newest copy, and the player
            // with the lowest id leads.
            State best = null;
            long bestId = 0;
            List<Player> list = Song.Players(ship);
            if (list != null)
                foreach (Player o in list)
                {
                    if (o == null) continue;
                    State s = o == p ? _me : Read(Song.Zdo(o));
                    if (s == null || s.Ship != id || s.Born <= 0 || s.Queue.Length == 0 || now - s.Live > Timeout) continue;
                    long pid = o.GetPlayerID();
                    if (best == null || s.Born < best.Born || s.Born == best.Born && (s.Gen > best.Gen || s.Gen == best.Gen && pid < bestId))
                    {
                        best = s;
                        bestId = pid;
                    }
                }

            if (best == null)
            {
                if (_singers > 0 && SongLib.HasAny)
                {
                    _me = new State { Born = now, Live = now, Ship = id, Gen = 1 };
                    Shuffle(ship, null);
                    Plugin.Debug("[shanty] new playlist for " + ship.name + ": " + _me.Queue.Replace("|", ", "));
                    if (_me.Queue.Length == 0) _me = null;
                }
                else if (_me != null && _me.Ship != id) _me = null;
            }
            else if (best != _me)
            {
                if (_me == null || _me.Born != best.Born) Plugin.Debug("[shanty] joined the playlist already playing on " + ship.name);
                _me = best.Copy();
            }

            _leading = _me != null && (best == null || best == _me || bestId == p.GetPlayerID());
            if (_leading && _me != null) Lead(ship, now);
            Write(p);
        }

        private static void Lead(Ship ship, long now)
        {

            if (_singers > 0 && now - _me.Live > 1000) _me.Live = now;
            string name = _me.Track;
            if (name == null) { Shuffle(ship, null); return; }
            if (_me.Start == 0)
            {
                SongLib.Track t = SongLib.Find(name);
                if (t == null || SongLib.HasFailed(t)) { Advance(ship, name); return; }
                AudioClip clip = SongLib.Get(t);
                if (clip == null || !SongLib.Ready(t)) return;   // still loading
                _me.Start = now + 400;
                _me.Len = clip.length;
                _me.Gen++;
                Plugin.Debug("[shanty] leading: song " + (_me.Index + 1) + "/" + _me.Names.Length + " " + name);
                return;
            }
            if (now >= _me.Start + (long)(_me.Len * 1000f) + (long)(Plugin.SongGapSeconds.Value * 1000f)) Advance(ship, name);
        }

        private static void Advance(Ship ship, string last)
        {
            _me.Index++;
            _me.Start = 0;
            _me.Len = 0f;
            _me.Gen++;
            if (_me.Index >= _me.Names.Length || Pool(ship) != _me.Pool)
            {
                if (_me.Index < _me.Names.Length) Plugin.Debug("[shanty] the crew changed: new shuffle for it");
                Shuffle(ship, last);
            }
        }

        // 1 = men only, 2 = women only, 3 = both (or nobody known)
        private static int Pool(Ship ship)
        {
            bool men = false, women = false;
            List<Player> list = Song.Players(ship);
            if (list != null)
                foreach (Player o in list)
                {
                    ZDO z = Song.Zdo(o);
                    if (z == null) continue;
                    if (z.GetInt(ZDOVars.s_modelIndex, 0) == 1) women = true; else men = true;
                }
            return men && !women ? 1 : women && !men ? 2 : 3;
        }

        private static readonly List<SongLib.Track> Sung = new List<SongLib.Track>(), Breaks = new List<SongLib.Track>();

        // Two sung songs, one instrumental, again and again, each list in random order.
        private static void Shuffle(Ship ship, string last)
        {
            int pool = Pool(ship);
            SongLib.ForCrew(pool != 2, pool != 1, Sung, Breaks);
            Mix(Sung);
            Mix(Breaks);
            if (last != null && Sung.Count > 1 && Sung[0].Name == last) { SongLib.Track t = Sung[0]; Sung.RemoveAt(0); Sung.Add(t); }
            List<string> q = new List<string>();
            int b = 0;
            for (int k = 0; k < Sung.Count; k++)
            {
                q.Add(Sung[k].Name);
                if (k % 2 == 1 && Breaks.Count > 0) q.Add(Breaks[b++ % Breaks.Count].Name);
            }
            if (Sung.Count == 0) foreach (SongLib.Track t in Breaks) q.Add(t.Name);
            _me.Queue = string.Join("|", q.ToArray());
            _me.Index = 0;
            _me.Start = 0;
            _me.Len = 0f;
            _me.Pool = pool;
            _me.Gen++;
        }

        private static void Mix(List<SongLib.Track> l)
        {
            for (int i = l.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                SongLib.Track t = l[i]; l[i] = l[j]; l[j] = t;
            }
        }

        private static string ShipId(Ship ship)
        {
            ZNetView nv = ship.GetComponent<ZNetView>();
            ZDO z = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            return z != null ? z.m_uid.ToString() : null;
        }

        private static State Read(ZDO z)
        {
            if (z == null) return null;
            long born = z.GetLong(KBorn, 0L);
            if (born <= 0) return null;
            return new State
            {
                Born = born,
                Ship = z.GetString(KShip, ""),
                Queue = z.GetString(KQueue, ""),
                Index = z.GetInt(KIndex, 0),
                Start = z.GetLong(KStart, 0L),
                Len = z.GetFloat(KLen, 0f),
                Live = z.GetLong(KLive, 0L),
                Gen = z.GetInt(KGen, 0),
                Pool = z.GetInt(KPool, 3),
            };
        }

        private static void Write(Player p)
        {
            ZDO z = Song.Zdo(p);
            if (z == null || !z.IsOwner()) return;
            if (_me == null)
            {
                if (_written == null) return;
                z.Set(KBorn, 0L);
                _written = null;
                return;
            }
            if (_me.Same(_written)) return;
            z.Set(KBorn, _me.Born);
            z.Set(KShip, _me.Ship);
            z.Set(KQueue, _me.Queue);
            z.Set(KIndex, _me.Index);
            z.Set(KStart, _me.Start);
            z.Set(KLen, _me.Len);
            z.Set(KLive, _me.Live);
            z.Set(KGen, _me.Gen);
            z.Set(KPool, _me.Pool);
            _written = _me.Copy();
        }

        // ------------------------------------------------------------ voices

        internal enum Voice { Auto, Bass, Baritone, Tenor, Alto, MezzoSoprano }

        // How high each voice sits, in semitones from the singer of a men's song. A women's song is sung
        // about WomenSong higher, so the same voice is moved down by that much on it.
        // The shift is kept within MaxShift: a voice moved further than that starts to sound artificial.
        private const float WomenSong = 4.5f, MaxShift = 3f;

        private static float Level(Voice v, bool female)
        {
            switch (v)
            {
                case Voice.Bass: return -3f;
                case Voice.Baritone: return -1.5f;
                case Voice.Tenor: return 1.5f;
                case Voice.Alto: return 3f;
                case Voice.MezzoSoprano: return 4.5f;
                default: return female ? 4.5f : 0f;
            }
        }

        private static void WriteVoice(Player p)
        {
            ZDO z = Song.Zdo(p);
            if (z == null || !z.IsOwner()) return;
            float v = Level(Plugin.VoiceType.Value, z.GetInt(ZDOVars.s_modelIndex, 0) == 1) + Plugin.VoiceFineTune.Value;
            if (Mathf.Approximately(v, _writtenVoice) && Mathf.Approximately(z.GetFloat(KVoice, NoVoice), v)) return;
            z.Set(KVoice, v);
            _writtenVoice = v;
            Plugin.Debug("[shanty] your voice: " + Plugin.VoiceType.Value + " (" + v.ToString("+0.0;-0.0") + ")");
        }

        // How far to move the song's voice for this player. Players without the mod get a voice from
        // their character, with a small difference picked from their id so two of them don't overlap.
        private static float ShiftFor(Player o, SongLib.Track t)
        {
            ZDO z = Song.Zdo(o);
            float level = z != null ? z.GetFloat(KVoice, NoVoice) : NoVoice;
            if (level <= NoVoice + 1f)
            {
                long id = o.GetPlayerID();
                level = (z != null && z.GetInt(ZDOVars.s_modelIndex, 0) == 1 ? WomenSong : 0f) + ((int)((id % 7 + 7) % 7) - 3) * 0.5f;
            }
            float shift = t.Kind == 'f' ? level - WomenSong : level;
            return Mathf.Clamp(shift, -MaxShift, MaxShift);
        }

        // ------------------------------------------------------------ sound

        private static AudioSource _main;
        private static readonly Dictionary<Player, AudioSource> Layers = new Dictionary<Player, AudioSource>();
        private static readonly List<Player> Gone = new List<Player>();
        private static float _fade;
        private static string _shown;

        internal static bool Uses(AudioClip c)
        {
            if (_main != null && _main.clip == c) return true;
            foreach (AudioSource s in Layers.Values) if (s != null && s.clip == c) return true;
            return false;
        }

        private static void Audio()
        {
            SongLib.Track track = null;
            AudioClip clip = null;
            double pos = -1.0;
            if (_ship != null && _me != null && _me.Ship == _shipId && _me.Start > 0)
            {
                track = SongLib.Find(_me.Track);
                clip = SongLib.Get(track);
                if (clip != null) pos = (Now() - _me.Start) / 1000.0;
                SongLib.Track next = SongLib.Find(_me.Next);   // have the next song ready
                SongLib.Get(next);
                SongLib.Voice(next);
            }
            bool inside = clip != null && pos >= 0.0 && pos < clip.length - 0.05;
            float vol = Mathf.Clamp01(Plugin.SongVolume.Value);
            float target = inside && _singers > 0 ? vol : 0f;
            _fade = Mathf.MoveTowards(_fade, target, Time.deltaTime * Mathf.Max(0.05f, vol) / 1.5f);

            if (inside)
            {
                if (_main == null || _main.transform.parent != _ship.transform) MakeMain(_ship);
                if (_main.clip != clip)
                {
                    _main.Stop();
                    _main.clip = clip;
                    _main.time = (float)pos;
                    _main.Play();
                    if (_shown != _me.Born + ":" + _me.Index) { _shown = _me.Born + ":" + _me.Index; Show(track); }
                }
                else if (!_main.isPlaying) { _main.time = (float)pos; _main.Play(); }
                else if (Mathf.Abs(_main.time - (float)pos) > 0.3f) _main.time = (float)pos;
            }
            else if (_main != null && _fade <= 0.001f && _main.isPlaying) _main.Stop();

            // the instruments under the choir (split songs) and the instrumental breaks have their own volume
            float inst = track != null && (track.Split || !track.Sung) ? Mathf.Clamp01(Plugin.InstrumentVolume.Value) : 1f;
            if (_main != null) _main.volume = _fade * inst;
            // Split songs: every singer sings the voice. One-file songs: quieter copies of the whole song.
            AudioClip voice = null;
            if (inside && track.Sung && _fade > 0.001f) voice = track.Split ? SongLib.Voice(track) : clip;
            UpdateLayers(voice, track, (float)pos);
        }

        private static AudioSource NewSource(string name, Transform parent, Vector3 local)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 10f;
            s.maxDistance = 70f;
            s.dopplerLevel = 0f;
            AudioMan am = AudioMan.instance;
            if (am != null && SfxMixer != null) s.outputAudioMixerGroup = SfxMixer.GetValue(am) as UnityEngine.Audio.AudioMixerGroup;
            return s;
        }

        private static void MakeMain(Ship ship)
        {
            if (_main != null) Object.Destroy(_main.gameObject);
            Vector3 at = Vector3.up * 2f;
            if (ship.m_mastObject != null) at = ship.transform.InverseTransformPoint(ship.m_mastObject.transform.position) + Vector3.up * 1.5f;
            _main = NewSource("SeaLegs_song", ship.transform, at);
            _main.spatialBlend = 0.5f;
            _main.priority = 40;
        }

        // Where each voice sits around the beat, as a share of half of ChoirSpreadMs: the first singer
        // is on time, the next ones late or early, so the earliest and latest are at most ChoirSpreadMs apart.
        private static readonly float[] Lag = { 0f, 1f, -1f, 0.5f, -0.5f, 0.75f, -0.25f, 0.25f, -0.75f, 0.1f };

        private static void UpdateLayers(AudioClip clip, SongLib.Track track, float pos)
        {
            List<Player> singing = clip != null ? Song.SingerList(_ship) : null;
            bool split = track != null && track.Split;
            int max = Mathf.Max(split ? 1 : 0, Plugin.MaxVoiceLayers.Value);   // a split song always needs a voice
            Gone.Clear();
            foreach (KeyValuePair<Player, AudioSource> kv in Layers)
                if (kv.Key == null || kv.Value == null || singing == null || !singing.Contains(kv.Key) || singing.IndexOf(kv.Key) >= max)
                    Gone.Add(kv.Key);
            foreach (Player g in Gone)
            {
                AudioSource s;
                if (Layers.TryGetValue(g, out s) && s != null) Object.Destroy(s.gameObject);
                Layers.Remove(g);
            }
            if (singing == null) return;

            // More singers sound like a bigger choir, but not that much louder.
            int voices = Mathf.Min(singing.Count, max);
            float vol = _fade * Mathf.Clamp01(Plugin.VoiceLayerVolume.Value) * (split ? 1f : 0.35f) / Mathf.Pow(Mathf.Max(1, voices), 0.35f);
            for (int i = 0; i < singing.Count && i < max; i++)
            {
                Player o = singing[i];
                AudioSource s;
                if (!Layers.TryGetValue(o, out s) || s == null)
                {
                    s = NewSource("SeaLegs_voice", o.transform, Vector3.up * 1.6f);
                    s.spatialBlend = 0.8f;
                    s.priority = 60;
                    s.gameObject.AddComponent<PitchShift>();
                    Layers[o] = s;
                }
                PitchShift ps = s.GetComponent<PitchShift>();
                if (ps != null) ps.Ratio = Mathf.Pow(2f, ShiftFor(o, track) / 12f);
                // Each voice comes in a tiny bit after the first one, like people singing together.
                float at = pos - Mathf.Max(0f, Plugin.ChoirSpreadMs.Value) / 2000f * Lag[i % Lag.Length];
                if (at < 0f) { if (s.isPlaying) s.Stop(); continue; }
                if (s.clip != clip) { s.Stop(); s.clip = clip; s.time = at; s.Play(); }
                else if (!s.isPlaying) { if (at < clip.length - 0.05f) { s.time = at; s.Play(); } }
                else if (Mathf.Abs(s.time - at) > 0.15f) s.time = at;
                s.volume = vol;
            }
        }

        private static void Show(SongLib.Track t)
        {
            Plugin.Debug("[shanty] playing " + t.Name + (_leading ? " (you lead the playlist)" : ""));
            if (!Plugin.ShowSongName.Value || MessageHud.instance == null || t == null) return;
            MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, (t.Sung ? "Crew song: " : "Crew song (instrumental): ") + t.Title);
        }

        internal static void StopAudio()
        {
            if (_main != null) { Object.Destroy(_main.gameObject); _main = null; }
            foreach (AudioSource s in Layers.Values) if (s != null) Object.Destroy(s.gameObject);
            Layers.Clear();
            _fade = 0f;
        }

        // Turn the game's own music down while the crew sings.
        [HarmonyPatch(typeof(MusicMan), "UpdateMusic")]
        [HarmonyPostfix]
        private static void Duck(MusicMan __instance)
        {
            if (_fade <= 0.001f || MusicSource == null) return;
            AudioSource src = MusicSource.GetValue(__instance) as AudioSource;
            if (src == null) return;
            float vol = Mathf.Max(0.01f, Plugin.SongVolume.Value);
            src.volume *= Mathf.Lerp(1f, Mathf.Clamp01(Plugin.GameMusicWhileSinging.Value), Mathf.Clamp01(_fade / vol));
        }
    }
}
