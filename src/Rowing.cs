using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SeaLegs
{
    // Side rowing. Every boat gets two spots on the deck, at the left and right edge near the middle,
    // marked with two pulsing rings on the floor. Use one and your character holds on there and works the
    // ropes, pushing the boat straight sideways toward that side: handy to come alongside a dock or slip
    // past a rock. Any movement key lets go.
    // Each rower writes the side on their own character; the game that moves the boat (usually the
    // helmsman's) adds the push. The spots sit at the foot of the boat's own ropes when it has some.
    [HarmonyPatch]
    internal static class Rowing
    {
        internal const string RowKey = "SeaLegs_row";   // -1 left, 1 right, 0 none

        private static readonly FieldInfo ShipBody = AccessTools.Field(typeof(Ship), "m_body");
        private static readonly FieldInfo ShipView = AccessTools.Field(typeof(Ship), "m_nview");

        private class Rig
        {
            public Ship Ship;
            public Spot[] Spots = new Spot[2];
        }
        private static readonly Dictionary<Ship, Rig> Rigs = new Dictionary<Ship, Rig>();
        private static Material _mat;

        private static Spot _rowingAt;      // the spot you are holding, or null
        private static float _nextEmote;

        internal static bool IsRowing { get { return _rowingAt != null; } }

        // ------------------------------------------------------------ the spots

        internal class Spot : MonoBehaviour, Hoverable, Interactable
        {
            public Ship Ship;
            public int Side;                // -1 left, 1 right
            public Transform Hold;          // where you stand
            public Transform Inner, Outer;  // the rings
            public LineRenderer InnerLine, OuterLine;

            public string GetHoverText()
            {
                string side = Side < 0 ? "left" : "right";
                string act = _rowingAt == this ? "Stop rowing" : "Row the boat to the " + side;
                return Localization.instance.Localize("Side rowing\n[<color=yellow><b>$KEY_Use</b></color>] " + act);
            }

            public string GetHoverName() { return "Side rowing"; }

            public float GetHoverOffset() { return 0f; }

            public bool Interact(Humanoid user, bool hold, bool alt)
            {
                if (hold || user != Player.m_localPlayer) return false;
                if (_rowingAt == this) { StopRowing(); return false; }
                return StartRowing(this);
            }

            public bool UseItem(Humanoid user, ItemDrop.ItemData item) { return false; }

            private void Update()
            {
                if (Ship == null) return;
                bool show = Plugin.SideRowing.Value && ShowFor(Ship);
                if (InnerLine.enabled != show) { InnerLine.enabled = show; OuterLine.enabled = show; }
                if (!show) return;
                float t = Time.time * 2.2f;
                float a = Plugin.RowSpotColor.Value.a * (_rowingAt == this ? 0.45f : 1f);
                Outer.localScale = Vector3.one * (0.55f + 0.08f * Mathf.Sin(t));
                Inner.localScale = Vector3.one * (0.30f + 0.07f * Mathf.Sin(t + 1.6f));
                Color c = Plugin.RowSpotColor.Value;
                Color co = new Color(c.r, c.g, c.b, a * (0.55f + 0.25f * Mathf.Sin(t)));
                Color ci = new Color(c.r, c.g, c.b, a * (0.75f + 0.25f * Mathf.Sin(t + 1.6f)));
                OuterLine.startColor = OuterLine.endColor = co;
                InnerLine.startColor = InnerLine.endColor = ci;
            }
        }

        // Rings show to you when you are on this boat and not steering it.
        private static bool ShowFor(Ship ship)
        {
            Player p = Player.m_localPlayer;
            return p != null && !p.IsDead() && p.GetControlledShip() == null && Ship.GetLocalShip() == ship && Song.OnBoard(ship, p);
        }

        internal static void Update()
        {
            Player p = Player.m_localPlayer;
            if (!Plugin.SideRowing.Value) { StopRowing(); Clear(); return; }

            // build the spots for the boat you are on, drop the ones far away
            Ship local = p != null ? Ship.GetLocalShip() : null;
            if (local != null && !Rigs.ContainsKey(local) && Song.OnBoard(local, p)) Build(local);
            if (Rigs.Count > 0)
            {
                List<Ship> gone = null;
                foreach (KeyValuePair<Ship, Rig> kv in Rigs)
                    if (kv.Key == null || p == null || Vector3.Distance(kv.Key.transform.position, p.transform.position) > 80f)
                        (gone ?? (gone = new List<Ship>())).Add(kv.Key);
                if (gone != null) foreach (Ship s in gone) Drop(s);
            }

            if (_rowingAt == null) return;
            if (p == null || p.IsDead() || _rowingAt.Ship == null || !p.IsAttached() || p.GetAttachPoint() != _rowingAt.Hold || p.GetControlledShip() != null)
            {
                StopRowing();
                return;
            }
            ZDO zdo = Song.Zdo(p);
            if (zdo == null) return;
            if (zdo.GetInt(RowKey, 0) != _rowingAt.Side) zdo.Set(RowKey, _rowingAt.Side);
            if (Time.time >= _nextEmote && !string.IsNullOrEmpty(Plugin.RowEmote.Value))
            {
                zdo.Set(ZDOVars.s_emoteID, zdo.GetInt(ZDOVars.s_emoteID, 0) + 1);
                zdo.Set(ZDOVars.s_emote, Plugin.RowEmote.Value);
                zdo.Set(ZDOVars.s_emoteOneshot, true);
                _nextEmote = Time.time + Mathf.Max(0.5f, Plugin.RowEmoteSeconds.Value);
            }
        }

        private static bool StartRowing(Spot spot)
        {
            Player p = Player.m_localPlayer;
            if (p == null || p.IsDead() || p.GetControlledShip() != null || !Song.OnBoard(spot.Ship, p)) return false;
            if (p.IsAttached()) p.AttachStop();
            // hold you where you stand, facing out over that side
            if (spot.Hold != null && spot.Hold != spot.transform) Object.Destroy(spot.Hold.gameObject);
            GameObject hold = new GameObject("SeaLegs_row_hold");
            hold.transform.SetParent(spot.Ship.transform, false);
            hold.transform.position = p.transform.position;
            hold.transform.localRotation = Quaternion.LookRotation(new Vector3(spot.Side, 0f, 0f), Vector3.up);
            spot.Hold = hold.transform;
            p.AttachStart(spot.Hold, null, false, false, true, Plugin.RowAnimation.Value, new Vector3(0f, 0.3f, 0f), null);
            if (!p.IsAttached()) { Plugin.Debug("[row] the game did not hold you at the " + (spot.Side < 0 ? "left" : "right") + " spot"); return false; }
            _rowingAt = spot;
            _nextEmote = 0f;
            Plugin.Debug("[row] rowing to the " + (spot.Side < 0 ? "left" : "right") + " on " + spot.Ship.name);
            return true;
        }

        internal static void StopRowing()
        {
            if (_rowingAt == null) return;
            Spot was = _rowingAt;
            _rowingAt = null;
            Player p = Player.m_localPlayer;
            if (p != null && p.IsAttached() && was != null && p.GetAttachPoint() == was.Hold) p.AttachStop();
            if (was != null && was.Hold != null && was.Hold != was.transform) { Object.Destroy(was.Hold.gameObject); was.Hold = was.transform; }
            ZDO zdo = Song.Zdo(p);
            if (zdo != null)
            {
                zdo.Set(RowKey, 0);
                if (zdo.GetString(ZDOVars.s_emote, "") == Plugin.RowEmote.Value)
                {
                    zdo.Set(ZDOVars.s_emoteID, zdo.GetInt(ZDOVars.s_emoteID, 0) + 1);
                    zdo.Set(ZDOVars.s_emote, "");
                }
            }
            Plugin.Debug("[row] stopped rowing");
        }

        internal static void Clear()
        {
            foreach (Ship s in new List<Ship>(Rigs.Keys)) Drop(s);
        }

        private static void Drop(Ship ship)
        {
            Rig r;
            if (!Rigs.TryGetValue(ship, out r)) return;
            Rigs.Remove(ship);
            if (_rowingAt != null && (r.Spots[0] == _rowingAt || r.Spots[1] == _rowingAt)) StopRowing();
            foreach (Spot s in r.Spots) if (s != null) Object.Destroy(s.gameObject);
        }

        // ------------------------------------------------------------ placing the spots

        private static void Build(Ship ship)
        {
            Rig rig = new Rig { Ship = ship };
            Rigs[ship] = rig;
            Transform st = ship.transform;

            // width and middle of the hull, in the boat's own space
            Vector3 center = Vector3.zero;
            float half = 1.5f;
            BoxCollider fc = ship.m_floatCollider;
            if (fc != null)
            {
                center = st.InverseTransformPoint(fc.transform.TransformPoint(fc.center));
                half = Mathf.Abs(fc.size.x * fc.transform.lossyScale.x) * 0.5f;
            }
            Rigidbody body = ShipBody != null ? ShipBody.GetValue(ship) as Rigidbody : null;
            Vector3[] feet = RopeFeet(ship, center);
            Player me = Player.m_localPlayer;
            float refY = me != null ? st.InverseTransformPoint(me.transform.position).y : center.y;   // you stand on the deck
            float mastZ = ship.m_mastObject != null ? st.InverseTransformPoint(ship.m_mastObject.transform.position).z : center.z;

            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                // level with the mast, where the side ropes come down (or with the rope foot when we found it)
                bool atRope = feet[i] != Vector3.zero;
                float z = atRope ? feet[i].z : mastZ;
                float edge = DeckEdge(ship, body, center.x, z, side, half, refY);
                float x = center.x + side * Mathf.Max(0.2f, edge - 0.15f - Plugin.RowSpotInset.Value);   // right by the rail
                Vector3 local = new Vector3(x, center.y, z);
                local.y = Deck(ship, body, local);

                GameObject go = new GameObject("SeaLegs_row_" + (side < 0 ? "left" : "right"));
                go.transform.SetParent(st, false);
                go.transform.localPosition = local;
                go.transform.localRotation = Quaternion.LookRotation(new Vector3(side, 0f, 0f), Vector3.up);
                Spot spot = go.AddComponent<Spot>();
                spot.Ship = ship;
                spot.Side = side;
                spot.Hold = go.transform;
                spot.Outer = Ring(go.transform, "outer", out spot.OuterLine);
                spot.Inner = Ring(go.transform, "inner", out spot.InnerLine);
                rig.Spots[i] = spot;
                Plugin.Debug("[row] " + ship.name + " " + (side < 0 ? "left" : "right") + " spot at " + local.ToString("0.00") + ", deck edge " + edge.ToString("0.00") + " m from the middle" + (atRope ? ", rope foot " + feet[i].ToString("0.00") : ", level with the mast"));
            }
        }

        // Walks from the middle of the deck toward one side and returns how far the flat deck goes before
        // the rail (the floor rises) or the hull ends (nothing under the ray).
        private static float DeckEdge(Ship ship, Rigidbody body, float cx, float z, int side, float half, float deckY)
        {
            float edge = 0f;
            bool any = false;
            for (float d = 0.3f; d < half + 1f; d += 0.1f)
            {
                float y = DeckHit(ship, body, new Vector3(cx + side * d, deckY, z));
                if (float.IsNaN(y)) { if (any) break; continue; }       // no floor yet (mast foot) or past the hull
                if (y > deckY + 0.45f) break;                            // the rail
                if (y < deckY - 0.8f) { if (any) break; continue; }
                any = true;
                edge = d;
            }
            return any ? edge : Mathf.Max(0.6f, half * 0.6f);
        }

        // Height of the highest up-facing part of the boat under the point, in the boat's space (NaN = none).
        private static float DeckHit(Ship ship, Rigidbody body, Vector3 local)
        {
            Transform st = ship.transform;
            Vector3 from = st.TransformPoint(new Vector3(local.x, local.y + 1.6f, local.z));   // from a bit above the deck, under the mast top
            float best = float.NaN;
            foreach (RaycastHit h in Physics.RaycastAll(from, -st.up, 3.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider == null || h.normal.y < 0.6f) continue;
                if (h.collider.attachedRigidbody != body && h.collider.GetComponentInParent<Ship>() != ship) continue;
                if (h.collider.GetComponentInParent<Character>() != null) continue;
                float y = st.InverseTransformPoint(h.point).y;
                if (float.IsNaN(best) || y > best) best = y;
            }
            return best;
        }

        // Highest floor under the point (the deck), in the boat's space; falls back to your own height.
        private static float Deck(Ship ship, Rigidbody body, Vector3 local)
        {
            Transform st = ship.transform;
            Vector3 from = st.TransformPoint(local + Vector3.up * 4f);
            float best = float.NegativeInfinity;
            foreach (RaycastHit h in Physics.RaycastAll(from, -st.up, 8f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider == null || h.normal.y < 0.6f) continue;
                if (h.collider.attachedRigidbody != body && h.collider.GetComponentInParent<Ship>() != ship) continue;
                float y = st.InverseTransformPoint(h.point).y;
                if (y > best) best = y;
            }
            if (!float.IsNegativeInfinity(best)) return best;
            Player p = Player.m_localPlayer;
            return p != null ? st.InverseTransformPoint(p.transform.position).y : local.y;
        }

        // Where the boat's own ropes come down on each side (left, right), in the boat's space; zero = none.
        // Rope meshes are found by name. On each side the lowest points closest to the mast are the foot.
        private static readonly HashSet<string> Listed = new HashSet<string>();
        private static Vector3[] RopeFeet(Ship ship, Vector3 center)
        {
            Vector3[] feet = new Vector3[2];
            Transform st = ship.transform;
            float mastZ = ship.m_mastObject != null ? st.InverseTransformPoint(ship.m_mastObject.transform.position).z : center.z;
            List<Vector3>[] pts = { new List<Vector3>(), new List<Vector3>() };
            List<string> names = new List<string>();
            foreach (Renderer r in ship.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("SeaLegs")) continue;
                string n = r.name;
                Mesh mesh = null;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
                SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
                if (smr != null) mesh = smr.sharedMesh;
                if (mesh != null) n += "/" + mesh.name;
                if (n.ToLowerInvariant().IndexOf("rope") < 0) continue;
                LineRenderer line = r as LineRenderer;
                names.Add(n + " [" + r.GetType().Name + (r.enabled ? "" : ", off") + (line != null ? ", " + line.positionCount + " points" : mesh != null ? ", " + (mesh.isReadable ? mesh.vertexCount + " vertices" : "not readable") : "") + ", size " + r.bounds.size.ToString("0.0") + "]");
                List<Vector3> world = new List<Vector3>();
                LineRenderer lr = r as LineRenderer;
                if (lr != null)
                {
                    for (int i = 0; i < lr.positionCount; i++) world.Add(lr.useWorldSpace ? lr.GetPosition(i) : lr.transform.TransformPoint(lr.GetPosition(i)));
                }
                else if (mesh != null && mesh.isReadable)
                {
                    Vector3[] v = mesh.vertices;
                    for (int i = 0; i < v.Length; i += Mathf.Max(1, v.Length / 400)) world.Add(r.transform.TransformPoint(v[i]));
                }
                else
                {
                    Bounds b = r.bounds;
                    for (int i = 0; i < 8; i++)
                        world.Add(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                }
                foreach (Vector3 w in world)
                {
                    Vector3 l = st.InverseTransformPoint(w);
                    float dx = l.x - center.x;
                    if (Mathf.Abs(dx) < 0.4f) continue;
                    pts[dx < 0f ? 0 : 1].Add(l);
                }
            }
            if (Listed.Add(ship.name))
                Plugin.Debug("[row] " + ship.name + " ropes: " + (names.Count == 0 ? "none" : string.Join(", ", names.ToArray())));
            for (int s = 0; s < 2; s++)
            {
                List<Vector3> p = pts[s];
                if (p.Count == 0) continue;
                float minY = float.MaxValue;
                foreach (Vector3 v in p) minY = Mathf.Min(minY, v.y);
                // low points only; of those, the bunch nearest the mast
                Vector3 pick = Vector3.zero;
                float bestZ = float.MaxValue;
                foreach (Vector3 v in p)
                    if (v.y < minY + 0.5f && Mathf.Abs(v.z - mastZ) < bestZ) { bestZ = Mathf.Abs(v.z - mastZ); pick = v; }
                Vector3 sum = Vector3.zero;
                int count = 0;
                foreach (Vector3 v in p)
                    if (v.y < minY + 0.5f && Vector3.Distance(v, pick) < 0.8f) { sum += v; count++; }
                if (count > 0) feet[s] = sum / count;
            }
            return feet;
        }

        private static Material Mat()
        {
            if (_mat != null) return _mat;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("UI/Default");
            _mat = new Material(sh);
            _mat.hideFlags = HideFlags.HideAndDontSave;
            return _mat;
        }

        private static Transform Ring(Transform parent, string name, out LineRenderer lr)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            lr = g.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.sharedMaterial = Mat();
            lr.widthMultiplier = 0.035f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            const int n = 40;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            }
            return g.transform;
        }

        // ------------------------------------------------------------ hover: the spot under your feet

        [HarmonyPatch(typeof(Player), "FindHoverObject")]
        [HarmonyPostfix]
        private static void Hover(Player __instance, ref GameObject hover)
        {
            if (!Plugin.Enabled.Value || !Plugin.SideRowing.Value || __instance != Player.m_localPlayer || Rigs.Count == 0) return;
            if (hover != null && hover.GetComponentInParent<Interactable>() != null && hover.GetComponentInParent<Spot>() == null) return;
            Ship ship = Ship.GetLocalShip();
            Rig rig;
            if (ship == null || !Rigs.TryGetValue(ship, out rig) || !ShowFor(ship)) return;
            Vector3 me = __instance.transform.position;
            Spot best = null;
            float bestD = Plugin.RowSpotReach.Value;
            foreach (Spot s in rig.Spots)
            {
                if (s == null) continue;
                Vector3 d = s.transform.position - me;
                if (Mathf.Abs(d.y) > 1.5f) continue;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < bestD) { bestD = dist; best = s; }
            }
            if (best != null) hover = best.gameObject;
        }

        // ------------------------------------------------------------ the boat: push it sideways

        private static float _lastLog;

        [HarmonyPatch(typeof(Ship), "CustomFixedUpdate")]
        [HarmonyPostfix]
        private static void Push(Ship __instance, float fixedDeltaTime)
        {
            if (!Plugin.Enabled.Value || !Plugin.SideRowing.Value) return;
            ZNetView nv = ShipView != null ? ShipView.GetValue(__instance) as ZNetView : null;
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
            Rigidbody body = ShipBody != null ? ShipBody.GetValue(__instance) as Rigidbody : null;
            if (body == null || body.isKinematic) return;
            List<Player> list = Song.Players(__instance);
            if (list == null || list.Count == 0) return;

            long helmsman = __instance.m_shipControlls != null ? __instance.m_shipControlls.GetUser() : 0L;
            int left = 0, right = 0;
            foreach (Player p in list)
            {
                if (p == null || p.IsDead() || p.GetPlayerID() == helmsman) continue;
                ZDO z = Song.Zdo(p);
                if (z == null) continue;
                int s = z.GetInt(RowKey, 0);
                if (s < 0) left++; else if (s > 0) right++;
            }
            int max = Mathf.Max(1, Plugin.MaxRowers.Value);
            int net = Mathf.Min(right, max) - Mathf.Min(left, max);
            if (net == 0) return;

            Vector3 side = __instance.transform.right;
            side.y = 0f;
            if (side.sqrMagnitude < 0.01f) return;
            side.Normalize();
            float dir = Mathf.Sign(net);
            float want = Mathf.Min(Plugin.MaxRowSpeed.Value, Mathf.Abs(net) * Plugin.RowSpeed.Value);
            float now = Vector3.Dot(body.linearVelocity, side) * dir;          // speed already going our way
            if (now >= want) return;
            float accel = Mathf.Min(Plugin.RowAcceleration.Value, (want - now) * 3f);
            body.AddForce(side * (dir * accel), ForceMode.Acceleration);  // at the centre: straight, no turning

            if (Plugin.DetailedLog.Value && Time.time - _lastLog > 5f)
            {
                _lastLog = Time.time;
                Plugin.Log.LogInfo("[row] " + __instance.name + ": " + left + " rowing left, " + right + " right; sideways " + (now * dir).ToString("0.00") + " m/s, target " + (want * dir).ToString("0.00"));
            }
        }
    }
}
