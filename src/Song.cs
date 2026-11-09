using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SeaLegs
{
    // Crew song. Someone starts it (the sing key, K by default, or /vibe, /dance, /roar on a boat); then the
    // rest of the crew joins in when they sit on the boat or stand still on the deck for a few seconds:
    // their characters play the Vibe, Dance and Roar emotes in turns. Nobody starts singing on their own.
    // The helmsman sings along (no emote, his hands are on the helm) while the crew sings, but never starts.
    // Pressing the key again stops you, and you stay quiet until this song is over.
    // Standing singers are held in place on the deck the same way the game holds you on a bench or at the
    // mast, so a bump of the boat can't throw them overboard. Any movement key lets go.
    // Emotes are saved on each character and seen by everyone, so the helmsman's game (which moves the
    // boat) counts the crew members playing one of those emotes and adds sail force for each.
    // Players without the mod count too when they use /vibe, /dance or /roar.
    [HarmonyPatch]
    internal static class Song
    {
        private static readonly string[] SongEmotes = { "vibe", "dance", "roar" };

        private static readonly FieldInfo ShipPlayers = AccessTools.Field(typeof(Ship), "m_players");
        private static readonly FieldInfo CharView = AccessTools.Field(typeof(Character), "m_nview");
        private static readonly FieldInfo CharMove = AccessTools.Field(typeof(Character), "m_moveDir");

        private static bool _singing;         // we set the current emote
        private static string _emote = "";
        private static float _nextSwitch, _stillSince = -1f;
        private static string _lastReason = "";
        private static GameObject _spot;      // where we hold a standing singer, child of the ship
        private static bool _started;         // we started the song (key or /vibe)
        private static bool _optOut;          // we stopped on purpose; quiet until the song is over
        private static float _ledUntil;       // someone else is leading the song until then
        private static Ship _boarded;         // for the hint when you get on a boat
        private static bool _helmSinging;

        // Saved on each character: 1 = sings only because others do (joined). A singer without it leads.
        internal static readonly int JoinedKey = "SeaLegs_joined".GetStableHashCode();
        // Saved on the helmsman: 1 = sings along at the helm.
        internal static readonly int HelmKey = "SeaLegs_helmsing".GetStableHashCode();

        // helmsman side
        private static readonly Dictionary<Ship, KeyValuePair<float, int>> Counted = new Dictionary<Ship, KeyValuePair<float, int>>();
        private static int _shownCount = -1;
        private static float _lastBonusLog;

        // ------------------------------------------------------------ your own character

        internal static void Update()
        {
            Player p = Player.m_localPlayer;
            ZDO zdo = Zdo(p);
            if (p == null || zdo == null) { _singing = false; DropSpot(); return; }
            if (_spot != null && !Held(p)) DropSpot();               // the player moved away (game let go)
            Keys(p);
            Helm(p, zdo);
            Hint(p);

            string reason;
            bool want = ShouldSing(p, out reason);
            if (reason != _lastReason)
            {
                _lastReason = reason;
                Plugin.Debug("[song] " + (want ? "singing: " : "not singing: ") + reason);
            }

            string current = zdo.GetString(ZDOVars.s_emote, "");
            if (_singing && current != _emote) _singing = false;   // the game or the player changed it

            if (!_singing && zdo.GetInt(JoinedKey, 0) != 0) zdo.Set(JoinedKey, 0);   // own emotes lead
            if (!want) { if (_singing) Stop(); Release(p); return; }
            if (!p.IsAttached() && !Hold(p)) return;
            if (!_singing && current != "") return;                // the player is doing their own emote
            if (!_singing || Time.time >= _nextSwitch) Play(zdo, Pick());
        }

        private static bool ShouldSing(Player p, out string reason)
        {
            if (p.IsDead()) { reason = "dead"; _started = false; return false; }
            if (Rowing.IsRowing) { reason = "rowing"; _stillSince = -1f; return false; }
            if (Plugin.Steering() != null) { reason = "you are steering"; _started = false; _stillSince = -1f; return false; }
            Ship ship = Ship.GetLocalShip();
            if (ship == null || !OnBoard(ship, p)) { reason = "not on a boat"; _started = false; _optOut = false; _stillSince = -1f; return false; }
            if (Plugin.OnlyWhileSteered.Value && (ship.m_shipControlls == null || !ship.m_shipControlls.HaveValidUser())) { reason = "nobody is steering"; return false; }
            if (!_started)
            {
                if (!Led(ship, p)) { _optOut = false; reason = "nobody is singing (the sing key starts a song)"; return false; }
                if (_optOut) { reason = "you stopped singing; waiting for this song to end"; return false; }
                if (!Plugin.AutoSing.Value) { reason = "AutoSing is off"; return false; }
            }

            if (p.IsAttached())
            {
                _stillSince = -1f;
                if (Held(p)) { reason = "held on the deck"; return Plugin.SingStanding.Value; }
                if (!Plugin.SingWhenSeated.Value) { reason = "seated, SingWhenSeated is off"; return false; }
                if (!p.IsAttachedToShip()) { reason = "seated, but not on the boat"; return false; }
                reason = "seated on the boat";
                return true;
            }

            if (!Plugin.SingStanding.Value) { reason = "SingStanding is off"; return false; }
            if (p.GetStandingOnShip() != ship || !p.IsOnGround()) { reason = "not standing on the deck"; _stillSince = -1f; return false; }
            Vector3 move = CharMove != null ? (Vector3)CharMove.GetValue(p) : Vector3.zero;
            if (move.sqrMagnitude > 0.01f || p.InAttack() || p.IsBlocking() || p.IsEncumbered()) { reason = "moving on the deck"; _stillSince = -1f; return false; }
            if (_stillSince < 0f) _stillSince = Time.time;
            float wait = Plugin.StandStillSeconds.Value;
            if (Time.time - _stillSince < wait) { reason = "standing still, waiting " + wait.ToString("0.#") + " s"; return false; }
            reason = "standing still on the deck";
            return true;
        }

        // Holds you where you stand, like a bench or the mast does: you move with the boat and bumps
        // can't throw you off. The game lets go as soon as you press a movement key.
        private static bool Hold(Player p)
        {
            Ship ship = p.GetStandingOnShip();
            if (ship == null) return false;
            DropSpot();
            _spot = new GameObject("SeaLegs_spot");
            _spot.transform.SetParent(ship.transform, false);
            _spot.transform.position = p.transform.position;
            _spot.transform.rotation = Quaternion.Euler(0f, p.transform.rotation.eulerAngles.y, 0f);
            p.AttachStart(_spot.transform, null, false, false, true, Plugin.HoldAnimation.Value, Vector3.zero, null);
            foreach (Chair c in ship.GetComponentsInChildren<Chair>()) Plugin.Debug("[song] this boat has a seat '" + c.name + "' with animation '" + c.m_attachAnimation + "'");
            Plugin.Debug("[song] holding you on the deck of " + ship.name + " (animation '" + Plugin.HoldAnimation.Value + "')");
            if (!p.IsAttached()) { Plugin.Debug("[song] the game did not hold you; trying again later"); DropSpot(); _stillSince = Time.time; return false; }
            return true;
        }

        private static bool Held(Player p)
        {
            return _spot != null && p.IsAttached() && p.GetAttachPoint() == _spot.transform;
        }

        private static void Release(Player p)
        {
            if (_spot == null) return;
            if (p != null && Held(p)) { p.AttachStop(); Plugin.Debug("[song] let go of you"); }
            DropSpot();
        }

        private static void DropSpot()
        {
            if (_spot != null) Object.Destroy(_spot);
            _spot = null;
        }

        private static string Pick()
        {
            int v = Mathf.Max(0, Plugin.VibeWeight.Value), d = Mathf.Max(0, Plugin.DanceWeight.Value), r = Mathf.Max(0, Plugin.RoarWeight.Value);
            int total = v + d + r;
            if (total <= 0) return "vibe";
            int n = Random.Range(0, total);
            string pick = n < v ? "vibe" : n < v + d ? "dance" : "roar";
            if (pick == "roar" && _emote == "roar") pick = "vibe";   // never two roars in a row
            return pick;
        }

        // Same as the game's StartEmote, which refuses while seated: a new emote id makes every client
        // (re)start the animation.
        private static void Play(ZDO zdo, string emote)
        {
            zdo.Set(ZDOVars.s_emoteID, zdo.GetInt(ZDOVars.s_emoteID, 0) + 1);
            zdo.Set(ZDOVars.s_emote, emote);
            zdo.Set(ZDOVars.s_emoteOneshot, true);
            zdo.Set(JoinedKey, _started ? 0 : 1);
            _emote = emote;
            _singing = true;
            float min = Mathf.Max(1f, Plugin.EmoteMinSeconds.Value), max = Mathf.Max(min, Plugin.EmoteMaxSeconds.Value);
            _nextSwitch = Time.time + (emote == "roar" ? 3.5f : Random.Range(min, max));
            Plugin.Debug("[song] emote " + emote);
        }

        internal static void Stop()
        {
            Release(Player.m_localPlayer);
            if (!_singing) return;
            _singing = false;
            ZDO zdo = Zdo(Player.m_localPlayer);
            if (zdo == null || zdo.GetString(ZDOVars.s_emote, "") != _emote) return;
            zdo.Set(ZDOVars.s_emoteID, zdo.GetInt(ZDOVars.s_emoteID, 0) + 1);
            zdo.Set(ZDOVars.s_emote, "");
            Plugin.Debug("[song] stopped");
        }

        // ------------------------------------------------------------ starting and stopping the song

        // Someone else on the boat leads a song: sings without having joined (started it, used /vibe
        // themselves, or plays without the mod). Kept a few seconds so a singer moving around doesn't end it.
        private static bool Led(Ship ship, Player me)
        {
            List<Player> list = Players(ship);
            long helmsman = ship.m_shipControlls != null ? ship.m_shipControlls.GetUser() : 0L;
            if (list != null)
                foreach (Player o in list)
                {
                    if (o == null || o == me || o.GetPlayerID() == helmsman || o.IsDead()) continue;
                    ZDO z = Zdo(o);
                    if (z == null || z.GetInt(JoinedKey, 0) != 0) continue;
                    if (System.Array.IndexOf(SongEmotes, z.GetString(ZDOVars.s_emote, "")) >= 0) { _ledUntil = Time.time + 4f; break; }
                }
            return Time.time < _ledUntil;
        }

        private static void Keys(Player p)
        {
            if (!Plugin.SingKey.Value.IsDown() || HelmView.Busy()) return;
            Toggle(p, false);
        }

        // The sing key or /vibe: start a song, or stop singing (and stay quiet until this song is over).
        private static void Toggle(Player p, bool fromEmote)
        {
            if (Plugin.Steering() != null)
            {
                Say("At the helm you sing along when the crew sings.");
                return;
            }
            Ship ship = Ship.GetLocalShip();
            if (ship == null || !OnBoard(ship, p)) { if (!fromEmote) Say("Get on a boat to start a song."); return; }
            if (!fromEmote && (_started || _singing))
            {
                _started = false;
                _optOut = true;
                Stop();
                Say("You stop singing.");
                Plugin.Debug("[song] you stopped the song (quiet until it ends)");
                return;
            }
            if (_started) return;
            _started = true;
            _optOut = false;
            _stillSince = Time.time - 100f;   // no waiting: the song starts now
            Say(Led(ship, p) ? "You join the song!" : "You start a song!");
            Plugin.Debug("[song] you started the song");
        }

        // /vibe, /dance and /roar on a boat start the crew song (the game would refuse them while seated).
        [HarmonyPatch(typeof(Player), "StartEmote"), HarmonyPrefix]
        private static bool StartEmote(Player __instance, string emote, ref bool __result)
        {
            if (!Plugin.Enabled.Value || __instance != Player.m_localPlayer || System.Array.IndexOf(SongEmotes, emote) < 0) return true;
            Ship ship = Ship.GetLocalShip();
            if (ship == null || !OnBoard(ship, __instance) || Plugin.Steering() != null) return true;
            Toggle(__instance, true);
            __result = true;
            return false;
        }

        // A hint once each time you get on a boat where nobody sings.
        private static void Hint(Player p)
        {
            Ship ship = Ship.GetLocalShip();
            bool on = ship != null && OnBoard(ship, p);
            if (!on) { _boarded = null; return; }
            if (ship == _boarded) return;
            _boarded = ship;
            if (!Plugin.SingHint.Value || Plugin.Steering() != null || Led(ship, p)) return;
            Say("Press " + Plugin.SingKey.Value + " (or type /vibe) to start a sea shanty.");
        }

        // The helmsman sings along while the crew sings (saved on his character, so everyone's game counts him).
        private static void Helm(Player p, ZDO zdo)
        {
            Ship ship = Plugin.Steering();
            bool sing = ship != null && Plugin.HelmSings.Value && Led(ship, p);
            if (sing == _helmSinging && zdo.GetInt(HelmKey, 0) == (sing ? 1 : 0)) return;
            _helmSinging = sing;
            zdo.Set(HelmKey, sing ? 1 : 0);
            Plugin.Debug("[song] " + (sing ? "you sing along at the helm" : "you stop singing at the helm"));
        }

        private static void Say(string msg)
        {
            if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, msg);
        }

        internal static ZDO Zdo(Character c)
        {
            if (c == null || CharView == null) return null;
            ZNetView nv = CharView.GetValue(c) as ZNetView;
            return nv != null && nv.IsValid() ? nv.GetZDO() : null;
        }

        internal static List<Player> Players(Ship ship)
        {
            return ShipPlayers != null ? ShipPlayers.GetValue(ship) as List<Player> : null;
        }

        internal static bool OnBoard(Ship ship, Player p)
        {
            List<Player> list = Players(ship);
            return list != null && list.Contains(p);
        }

        // ------------------------------------------------------------ helmsman: count singers, add force

        internal static int Singers(Ship ship)
        {
            KeyValuePair<float, int> c;
            if (Counted.TryGetValue(ship, out c) && Time.time - c.Key < 0.5f) return c.Value;
            int n = 0;
            List<Player> list = Players(ship);
            long helmsman = ship.m_shipControlls != null ? ship.m_shipControlls.GetUser() : 0L;
            if (list != null)
                foreach (Player p in list)
                {
                    if (p == null || p.IsDead()) continue;
                    ZDO z = Zdo(p);
                    if (z != null && Sings(p, z, helmsman)) n++;
                }
            if (Counted.Count > 20) Counted.Clear();
            Counted[ship] = new KeyValuePair<float, int>(Time.time, n);
            return n;
        }

        // Who on the boat is singing right now (same rule as the count, oldest on board first).
        private static readonly List<Player> SingerBuf = new List<Player>();
        internal static List<Player> SingerList(Ship ship)
        {
            SingerBuf.Clear();
            List<Player> list = ship != null ? Players(ship) : null;
            long helmsman = ship != null && ship.m_shipControlls != null ? ship.m_shipControlls.GetUser() : 0L;
            if (list != null)
                foreach (Player p in list)
                {
                    if (p == null || p.IsDead()) continue;
                    ZDO z = Zdo(p);
                    if (z != null && Sings(p, z, helmsman)) SingerBuf.Add(p);
                }
            return SingerBuf;
        }

        // The crew sings with an emote; the helmsman sings along without one.
        private static bool Sings(Player p, ZDO z, long helmsman)
        {
            if (p.GetPlayerID() == helmsman) return z.GetInt(HelmKey, 0) == 1;
            return System.Array.IndexOf(SongEmotes, z.GetString(ZDOVars.s_emote, "")) >= 0;
        }

        internal static float Bonus(int singers)
        {
            return Mathf.Min(Mathf.Max(0f, Plugin.MaxBonus.Value), singers * Mathf.Max(0f, Plugin.BonusPerSinger.Value));
        }

        [HarmonyPatch(typeof(Ship), "GetSailForce")]
        [HarmonyPostfix]
        private static void SailForce(Ship __instance, ref Vector3 __result)
        {
            if (!Plugin.Enabled.Value) return;
            int n = Singers(__instance);
            if (n <= 0) return;
            float bonus = Bonus(n);
            __result *= 1f + bonus;
            if (Plugin.DetailedLog.Value && Time.time - _lastBonusLog > 10f)
            {
                _lastBonusLog = Time.time;
                Plugin.Log.LogInfo("[song] " + n + " singing, sail force +" + Mathf.RoundToInt(bonus * 100f) + "%");
            }
        }

        // At speed 1 (paddling, sail down) the boat is pushed by m_backwardForce instead of the sail:
        // the song makes that push stronger too, for the length of the ship's physics step.
        internal const int SlowSpeed = 2;   // Ship.Speed: Stop 0, Back 1, Slow 2, Half 3, Full 4

        [HarmonyPatch(typeof(Ship), "CustomFixedUpdate"), HarmonyPrefix]
        private static void SlowPre(Ship __instance, out float __state)
        {
            __state = -1f;
            if (!Plugin.Enabled.Value || (int)__instance.GetSpeedSetting() != SlowSpeed) return;
            int n = Singers(__instance);
            if (n <= 0) return;
            __state = __instance.m_backwardForce;
            __instance.m_backwardForce *= 1f + Bonus(n);
        }

        // a finalizer runs even if the game's method throws, so the force is always put back
        [HarmonyPatch(typeof(Ship), "CustomFixedUpdate"), HarmonyFinalizer]
        private static void SlowFinal(Ship __instance, float __state)
        {
            if (__state >= 0f) __instance.m_backwardForce = __state;
        }

        // The helmsman sees how many are singing.
        internal static void UpdateHelm(Ship ship)
        {
            if (ship == null) { _shownCount = -1; return; }
            int n = Singers(ship);
            if (n == _shownCount) return;
            bool first = _shownCount < 0;
            _shownCount = n;
            if (first && n == 0) return;
            if (!Plugin.CrewMessage.Value || MessageHud.instance == null) return;
            string msg = n == 0 ? "The crew stopped singing"
                : (n == 1 ? "1 crew member singing" : n + " crew members singing") + ": sail +" + Mathf.RoundToInt(Bonus(n) * 100f) + "%";
            MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, msg);
        }
    }
}
