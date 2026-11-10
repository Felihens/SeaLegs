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
        public const string VERSION = "0.11.1";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;

        internal static ConfigEntry<bool> AutoSing, SingWhenSeated, SingStanding, CrewMessage, HelmGlow, HelmSings, SingHint;
        internal static ConfigEntry<KeyboardShortcut> SingKey;
        internal static ConfigEntry<string> HoldAnimation;
        internal static ConfigEntry<float> StandStillSeconds, BonusPerSinger, MaxBonus, EmoteMinSeconds, EmoteMaxSeconds;
        internal static ConfigEntry<int> VibeWeight, DanceWeight, RoarWeight;

        internal static ConfigEntry<bool> OnlyWhileSteered;

        internal enum RockLook { Outline, Fill, Both }
        internal static ConfigEntry<bool> RockSilhouettes;
        internal static ConfigEntry<RockLook> RockStyle;
        internal static ConfigEntry<float> RockRange, RockSecondsAhead, RockMaxRange, RockDepth, RockOpacity, RockOutlineWidth, RockAboveHeight;
        internal static ConfigEntry<Color> RockColor, RockAboveColor;

        internal static ConfigEntry<float> CameraDistance, CameraHeight, CameraSide, CameraKeySpeed, SailOpacity;
        internal static ConfigEntry<bool> SeeThroughSail, CameraKeys;

        internal static ConfigEntry<bool> SongSound, ShowSongName;
        internal static ConfigEntry<float> SongVolume, InstrumentVolume, VoiceLayerVolume, ChoirSpreadMs, GameMusicWhileSinging, ReshuffleSeconds, SongGapSeconds, VoiceFineTune;
        internal static ConfigEntry<int> MaxVoiceLayers;
        internal static ConfigEntry<Shanty.Voice> VoiceType;
        internal static ConfigEntry<string> DisabledSongs;

        internal static ConfigEntry<bool> SideRowing;
        internal static ConfigEntry<float> RowSpeed, MaxRowSpeed, RowAcceleration, RowSpotReach, RowSpotInset, RowEmoteSeconds;
        internal static ConfigEntry<int> MaxRowers;
        internal static ConfigEntry<string> RowEmote, RowAnimation;
        internal static ConfigEntry<Color> RowSpotColor;

        internal static ConfigEntry<bool> DetailedLog;
        internal static Plugin Instance;

        private void Awake()
        {
            Log = Logger;
            Instance = this;
            Enabled = Config.Bind("1 - General", "Enabled", true, "Turns the whole mod on or off.");

            SingKey = Config.Bind("2 - Crew song", "SingKey", new KeyboardShortcut(KeyCode.K),
                "Starts a song on the boat (typing /vibe, /dance or /roar on a boat does the same). Press it again to stop singing; you stay quiet until that song is over.");
            AutoSing = Config.Bind("2 - Crew song", "AutoSing", true,
                "Join in automatically when someone on your boat starts a song, while you sit or stand still on the deck. Nobody starts singing on their own.");
            HelmSings = Config.Bind("2 - Crew song", "HelmsmanSingsAlong", true,
                "At the helm you sing along while the crew sings (you count as a singer too), but you never start the song.");
            SingHint = Config.Bind("2 - Crew song", "SingHint", true,
                "When you get on a boat where nobody sings, show which key starts a song.");
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
            HelmGlow = Config.Bind("2 - Crew song", "HelmGlow", true,
                "At the helm, show the song working: golden wind ribbons through the sail, a golden sail, and a golden wind icon with clinking mugs and the number of singers under it.");
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
            InstrumentVolume = Config.Bind("2b - Song sound", "InstrumentVolume", 0.5f,
                new ConfigDescription("Volume of the instruments under the choir, and of the instrumental songs, compared with the voices (1 = as recorded, 0.5 = half, so the band stays in the background). Songs that come as a single file are not changed.", new AcceptableValueRange<float>(0f, 1f)));
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
            RockSecondsAhead = Config.Bind("3 - Rocks", "SecondsAhead", 6f,
                new ConfigDescription("The faster the boat, the further ahead rocks show: Range plus this many seconds of sailing in front of the bow (0 = always Range).", new AcceptableValueRange<float>(0f, 20f)));
            RockMaxRange = Config.Bind("3 - Rocks", "MaxRange", 100f,
                new ConfigDescription("How far ahead rocks can show at full speed (metres).", new AcceptableValueRange<float>(10f, 200f)));
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

            SideRowing = Config.Bind("5 - Side rowing", "SideRowing", true,
                "Two spots on every boat, at the left and right edge near the middle, marked with rings on the deck. Use one to work the ropes and push the boat straight sideways toward that side. Any movement key lets go.");
            RowSpeed = Config.Bind("5 - Side rowing", "SpeedPerRower", 1f,
                new ConfigDescription("Sideways speed each rower adds (metres per second). Rowers on opposite sides cancel out. Used by the game that moves the boat (usually the helmsman's).", new AcceptableValueRange<float>(0.1f, 5f)));
            MaxRowSpeed = Config.Bind("5 - Side rowing", "MaxSpeed", 2.5f,
                new ConfigDescription("Fastest the crew can push the boat sideways (metres per second).", new AcceptableValueRange<float>(0.2f, 10f)));
            RowAcceleration = Config.Bind("5 - Side rowing", "Acceleration", 2f,
                new ConfigDescription("How quickly the boat picks up sideways speed (metres per second, each second).", new AcceptableValueRange<float>(0.1f, 10f)));
            MaxRowers = Config.Bind("5 - Side rowing", "MaxRowersPerSide", 4,
                new ConfigDescription("Most rowers counted on each side.", new AcceptableValueRange<int>(1, 10)));
            RowSpotReach = Config.Bind("5 - Side rowing", "SpotReach", 1.4f,
                new ConfigDescription("How close you must stand to a spot to use it (metres).", new AcceptableValueRange<float>(0.5f, 4f)));
            RowSpotInset = Config.Bind("5 - Side rowing", "SpotInset", 0f,
                new ConfigDescription("Move the spots toward the middle of the boat (positive) or toward the edge and its ropes (negative), in metres.", new AcceptableValueRange<float>(-1.5f, 2f)));
            RowSpotColor = Config.Bind("5 - Side rowing", "SpotColor", new Color(0.55f, 0.9f, 1f, 0.8f), "Color of the rings on the deck.");
            RowEmote = Config.Bind("5 - Side rowing", "RowEmote", "comehere",
                "Emote played over and over while you row, so the hands pull the rope. Empty = none. Others: point, wave, cheer, challenge, flex.");
            RowEmoteSeconds = Config.Bind("5 - Side rowing", "RowEmoteSeconds", 1.8f,
                new ConfigDescription("Time between two pulls of the rope.", new AcceptableValueRange<float>(0.5f, 10f)));
            RowAnimation = Config.Bind("5 - Side rowing", "RowAnimation", "",
                "Pose while you hold the spot. Empty = stand, so the emote shows.");

            DetailedLog = Config.Bind("9 - Debug", "DetailedLog", false, "Writes details to the BepInEx log.");

            SongLib.Scan(System.IO.Path.GetDirectoryName(Info.Location));
            new Harmony(GUID).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo(NAME + " " + VERSION + " loaded");
        }

        private void Update()
        {
            if (!Enabled.Value) { SongGlow.Clear(); HelmView.Leave(); Rowing.StopRowing(); Rowing.Clear(); Song.Stop(); Shanty.StopAudio(); return; }
            Rowing.Update();
            Song.Update();
            Shanty.Update();
            HelmView.Update();
            SongGlow.Update(Steering());
        }

        private void OnGUI()
        {
            if (Enabled.Value) SongGlow.OnGUI();
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
            if (p == null || p.IsDead()) return null;
            Ship s = p.GetControlledShip();
            if (s != null) return s;
            // fallback: the boat under you whose helm says you hold it
            Ship local = Ship.GetLocalShip();
            if (local != null && local.m_shipControlls != null && local.m_shipControlls.HaveValidUser()
                && local.m_shipControlls.GetUser() == p.GetPlayerID() && p.IsAttached())
                return local;
            return null;
        }
    }
}
