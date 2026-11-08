using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SeaLegs
{
    // Crew song. While someone else steers, you sing on your own when you sit on the boat or stand still
    // on the deck for a few seconds: your character plays the Vibe, Dance and Roar emotes in turns.
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

            string reason;
            bool want = ShouldSing(p, out reason);
            if (reason != _lastReason)
            {
                _lastReason = reason;
                Plugin.Debug("[song] " + (want ? "singing: " : "not singing: ") + reason);
            }

            string current = zdo.GetString(ZDOVars.s_emote, "");
            if (_singing && current != _emote) _singing = false;   // the game or the player changed it

            if (!want) { if (_singing) Stop(); Release(p); return; }
            if (!p.IsAttached() && !Hold(p)) return;
            if (!_singing && current != "") return;                // the player is doing their own emote
            if (!_singing || Time.time >= _nextSwitch) Play(zdo, Pick());
        }

        private static bool ShouldSing(Player p, out string reason)
        {
            if (!Plugin.AutoSing.Value) { reason = "AutoSing is off"; return false; }
            if (p.IsDead()) { reason = "dead"; return false; }
            if (p.GetControlledShip() != null) { reason = "you are steering"; _stillSince = -1f; return false; }
            Ship ship = Ship.GetLocalShip();
            if (ship == null || !OnBoard(ship, p)) { reason = "not on a boat"; _stillSince = -1f; return false; }
            if (Plugin.OnlyWhileSteered.Value && (ship.m_shipControlls == null || !ship.m_shipControlls.HaveValidUser())) { reason = "nobody is steering"; return false; }

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
                    if (p == null || p.GetPlayerID() == helmsman || p.IsDead()) continue;
                    ZDO z = Zdo(p);
                    if (z == null) continue;
                    if (System.Array.IndexOf(SongEmotes, z.GetString(ZDOVars.s_emote, "")) >= 0) n++;
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
                    if (p == null || p.GetPlayerID() == helmsman || p.IsDead()) continue;
                    ZDO z = Zdo(p);
                    if (z != null && System.Array.IndexOf(SongEmotes, z.GetString(ZDOVars.s_emote, "")) >= 0) SingerBuf.Add(p);
                }
            return SingerBuf;
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
