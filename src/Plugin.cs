using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SeaLegs
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "felih.SeaLegs";
        public const string NAME = "Sea Legs";
        public const string VERSION = "0.9.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;

        internal static ConfigEntry<bool> AutoSing, SingWhenSeated, SingStanding, CrewMessage;
        internal static ConfigEntry<string> HoldAnimation;
        internal static ConfigEntry<float> StandStillSeconds, BonusPerSinger, MaxBonus, EmoteMinSeconds, EmoteMaxSeconds;
        internal static ConfigEntry<int> VibeWeight, DanceWeight, RoarWeight;

        internal static ConfigEntry<bool> OnlyWhileSteered;

        internal enum RockLook { Outline, Fill, Both }
        internal static ConfigEntry<bool> RockSilhouettes;
        internal static ConfigEntry<RockLook> RockStyle;
        internal static ConfigEntry<float> RockRange, RockDepth, RockOpacity, RockOutlineWidth, RockAboveHeight;
        internal static ConfigEntry<Color> RockColor, RockAboveColor;

        internal static ConfigEntry<float> CameraDistance, CameraHeight, CameraSide, CameraKeySpeed, SailOpacity;
        internal static ConfigEntry<bool> SeeThroughSail, CameraKeys;

        internal static ConfigEntry<bool> SongSound, ShowSongName;
        internal static ConfigEntry<float> SongVolume, VoiceLayerVolume, ChoirSpreadMs, GameMusicWhileSinging, ReshuffleSeconds, SongGapSeconds, VoiceFineTune;
        internal static ConfigEntry<int> MaxVoiceLayers;
        internal static ConfigEntry<Shanty.Voice> VoiceType;
        internal static ConfigEntry<string> DisabledSongs;

        internal static ConfigEntry<bool> DetailedLog;
        internal static Plugin Instance;

        private void Awake()
        {
            Log = Logger;
            Instance = this;
            Enabled = Config.Bind("1 - General", "Enabled", true, "Turns the whole mod on or off.");

            AutoSing = Config.Bind("2 - Crew song", "AutoSing", true,
                "Sing automatically while you are on a boat (any boat: raft, karve, longship, drakkar) and not steering it.");
            OnlyWhileSteered = Config.Bind("2 - Crew song", "OnlyWhileSomeoneSteers", false,
                "Sing only while someone else is at the helm. Off = you also sing alone, or on an anchored boat.");
            SingWhenSeated = Config.Bind("2 - Crew song", "SingWhenSeated", true, "Sing while sitting on the boat.");
            SingStanding = Config.Bind("2 - Crew song", "SingStanding", true,
                "Sing while standing still on the deck. You are held in place like on a bench, so bumps can't throw you off; any movement key lets go.");
            StandStillSeconds = Config.Bind("2 - Crew song", "StandStillSeconds", 3f,
                new ConfigDescription("How long you must stand still on the deck before you start singing.", new AcceptableValueRange<float>(0.5f, 30f)));
            HoldAnimation = Config.Bind("2 - Crew song", "HoldAnimation", "",
                "Animation used while you are held on the deck. Empty = stand normally, so the dance shows.");
            BonusPerSinger = Config.Bind("2 - Crew song", "BonusPerSinger", 0.15f,
                new ConfigDescription("Extra sail force per crew member singing (0.15 = +15%). Used by the helmsman's game.", new AcceptableValueRange<float>(0f, 1f)));
            MaxBonus = Config.Bind("2 - Crew song", "MaxBonus", 0.45f,
                new ConfigDescription("Most extra sail force the song can give (0.45 = +45%).", new AcceptableValueRange<float>(0f, 3f)));
            CrewMessage = Config.Bind("2 - Crew song", "CrewMessage", true,
                "At the helm, show a message when the number of singers changes.");
            EmoteMinSeconds = Config.Bind("2 - Crew song", "EmoteMinSeconds", 8f, "Shortest time before switching to another dance move.");
            EmoteMaxSeconds = Config.Bind("2 - Crew song", "EmoteMaxSeconds", 16f, "Longest time before switching to another dance move.");
            VibeWeight = Config.Bind("2 - Crew song", "VibeWeight", 75, "How often the Vibe emote is picked (relative weight).");
            DanceWeight = Config.Bind("2 - Crew song", "DanceWeight", 20, "How often the Dance emote is picked (relative weight).");
            RoarWeight = Config.Bind("2 - Crew song", "RoarWeight", 5, "How often the Roar emote is picked (relative weight).");

            SongSound = Config.Bind("2b - Song sound", "SongSound", true,
                "Hear the crew song while people sing on your boat. Everyone on board with the mod hears the same song at the same time.");
            SongVolume = Config.Bind("2b - Song sound", "Volume", 0.6f,
                new ConfigDescription("Volume of the crew song (also follows the game's sound effects volume).", new AcceptableValueRange<float>(0f, 1f)));
            VoiceType = Config.Bind("2b - Song sound", "VoiceType", Shanty.Voice.Auto,
                "Your singing voice. Each singer adds a voice to the choir at their own pitch; others hear yours. Auto = picked from your character.");
            VoiceFineTune = Config.Bind("2b - Song sound", "VoiceFineTune", 0f,
                new ConfigDescription("Move your voice up or down a little more (semitones).", new AcceptableValueRange<float>(-3f, 3f)));
            VoiceLayerVolume = Config.Bind("2b - Song sound", "VoiceVolume", 1f,
                new ConfigDescription("Volume of the singers' voices compared with the instruments. Songs that come as a single file get quieter extra voices on top.", new AcceptableValueRange<float>(0f, 1f)));
            ChoirSpreadMs = Config.Bind("2b - Song sound", "ChoirSpreadMs", 100f,
                new ConfigDescription("Singers are not exactly together, like a real choir: the first one sings on the beat, the others a little late or early. This is the most time between the earliest and the latest voice (milliseconds, 0 = all exactly together).", new AcceptableValueRange<float>(0f, 300f)));
            MaxVoiceLayers = Config.Bind("2b - Song sound", "MaxChoirVoices", 4,
                new ConfigDescription("Most singers heard with their own voice at once.", new AcceptableValueRange<int>(0, 10)));
            GameMusicWhileSinging = Config.Bind("2b - Song sound", "GameMusicWhileSinging", 0.15f,
                new ConfigDescription("Game music volume while the crew sings (1 = unchanged, 0 = silent).", new AcceptableValueRange<float>(0f, 1f)));
            ShowSongName = Config.Bind("2b - Song sound", "ShowSongName", true, "Show the name of each song when it starts.");
            ReshuffleSeconds = Config.Bind("2b - Song sound", "ReshuffleSeconds", 60f,
                new ConfigDescription("When nobody has sung on the boat for this long, the playlist ends and the next song starts a new shuffle.", new AcceptableValueRange<float>(10f, 600f)));
            SongGapSeconds = Config.Bind("2b - Song sound", "GapBetweenSongs", 2f,
                new ConfigDescription("Silence between two songs (seconds). Set by whoever leads the playlist.", new AcceptableValueRange<float>(0f, 15f)));
            DisabledSongs = Config.Bind("2b - Song sound", "DisabledSongs", "",
                "Songs you never want in the playlist when you lead it: file names without extension, separated by commas (example: m_cursed_compass, i_wellerman_epic). Your own songs go in BepInEx/config/SeaLegs/songs.");

            RockSilhouettes = Config.Bind("3 - Rocks", "RockSilhouettes", true,
                "At the helm, show rocks hidden just under the water.");
            RockStyle = Config.Bind("3 - Rocks", "Style", RockLook.Outline,
                "Outline = only a line around the rocks. Fill = paint the whole rock. Both = filled with a line around.");
            RockOutlineWidth = Config.Bind("3 - Rocks", "OutlineWidth", 2f,
                new ConfigDescription("Width of the outline in pixels.", new AcceptableValueRange<float>(1f, 6f)));
            RockRange = Config.Bind("3 - Rocks", "Range", 30f,
                new ConfigDescription("How far around the boat to look for rocks (metres).", new AcceptableValueRange<float>(10f, 150f)));
            RockDepth = Config.Bind("3 - Rocks", "Depth", 3f,
                new ConfigDescription("Show the parts of rocks from the water surface down to this depth (metres).", new AcceptableValueRange<float>(0.5f, 10f)));
            RockColor = Config.Bind("3 - Rocks", "Color", new Color(1f, 0.45f, 0.1f), "Color of rocks under the water.");
            RockOpacity = Config.Bind("3 - Rocks", "Opacity", 0.15f,
                new ConfigDescription("Opacity of the rock silhouettes.", new AcceptableValueRange<float>(0.05f, 1f)));
            RockAboveHeight = Config.Bind("3 - Rocks", "AboveWaterHeight", 1.5f,
                new ConfigDescription("Also show rocks from the water surface up to this height, in their own color (0 = off).", new AcceptableValueRange<float>(0f, 10f)));
            RockAboveColor = Config.Bind("3 - Rocks", "AboveWaterColor", new Color(1f, 0.9f, 0.2f), "Color of rocks just above the water.");

            CameraDistance = Config.Bind("4 - Helm view", "CameraDistance", 12f,
                new ConfigDescription("Camera distance when you take the helm (0 = leave the camera alone). You can still zoom with the mouse wheel.",
                    new AcceptableValueRange<float>(0f, 30f)));
            CameraHeight = Config.Bind("4 - Helm view", "CameraHeight", 1.5f,
                new ConfigDescription("Raise the camera this much while steering (metres).", new AcceptableValueRange<float>(-2f, 10f)));
            CameraKeys = Config.Bind("4 - Helm view", "ArrowKeysMoveCamera", true,
                "While steering, the arrow keys move the camera: left/right slides it sideways, up/down raises or lowers it. The position is saved.");
            CameraSide = Config.Bind("4 - Helm view", "CameraSide", 0f,
                new ConfigDescription("Slide the camera sideways while steering (metres, negative = left).", new AcceptableValueRange<float>(-8f, 8f)));
            CameraKeySpeed = Config.Bind("4 - Helm view", "ArrowKeySpeed", 3f,
                new ConfigDescription("How fast the arrow keys move the camera (metres per second).", new AcceptableValueRange<float>(0.5f, 15f)));
            SeeThroughSail = Config.Bind("4 - Helm view", "SeeThroughSail", true, "Make the sail see-through while you steer.");
            SailOpacity = Config.Bind("4 - Helm view", "SailOpacity", 0.25f,
                new ConfigDescription("How visible the sail stays while you steer (0 = invisible, 1 = normal).", new AcceptableValueRange<float>(0f, 1f)));

            DetailedLog = Config.Bind("9 - Debug", "DetailedLog", false, "Writes details to the BepInEx log.");

            SongLib.Scan(System.IO.Path.GetDirectoryName(Info.Location));
            new Harmony(GUID).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo(NAME + " " + VERSION + " loaded");
        }

        private void Update()
        {
            if (!Enabled.Value) { HelmView.Leave(); Song.Stop(); Shanty.StopAudio(); return; }
            Song.Update();
            Shanty.Update();
            HelmView.Update();
        }

        private void LateUpdate()
        {
            Rocks.LateUpdate();
        }

        internal static void Debug(string msg)
        {
            if (DetailedLog.Value) Log.LogInfo(msg);
        }

        // The ship the local player is steering, or null.
        internal static Ship Steering()
        {
            Player p = Player.m_localPlayer;
            return p != null && !p.IsDead() ? p.GetControlledShip() : null;
        }
    }
}
