using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SeaLegs
{
    // At the helm you can see the song working, stronger the more crew sing:
    //  - golden "song wind" ribbons stream from behind the sail, through it and on past the bow
    //    (while the sail is up or the boat paddles at speed 1, the speeds the song pushes);
    //  - the see-through sail warms to gold, pulsing to the beat;
    //  - the game's own wind icon on the ship HUD turns gold and beats, and under it two mugs of mead
    //    clink to the beat, with the number of singers under them.
    // Only the helmsman's screen shows it.
    [HarmonyPatch]
    internal static class SongGlow
    {
        private const int MaxRibbons = 8, RibbonPoints = 14;
        private static readonly Color Gold = new Color(1f, 0.78f, 0.32f);

        private static Ship _ship;
        private static int _singers;
        private static float _bonus, _level;          // _level: 0..1, eased
        private static GameObject _root;
        private static readonly List<LineRenderer> _ribbons = new List<LineRenderer>();
        private static Vector3 _sailCenter, _sailSize;   // in the ship's local space
        private static Material _mat;
        private static Texture2D _mug;
        private static bool _anchorLogged;
        private static Font _font;
        private static GUIStyle _style;

        internal static float Level { get { return _level; } }
        internal static float Beat { get { return 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * 0.9f); } }

        internal static void Update(Ship ship)
        {
            if (ship != _ship)
            {
                Clear();
                _ship = ship;
                if (ship != null) { Plugin.Debug("[glow] steering " + ship.name); MeasureSail(ship); }
            }
            float target = 0f;
            if (_ship != null && Plugin.HelmGlow.Value)
            {
                _singers = Song.Singers(_ship);
                _bonus = Song.Bonus(_singers);
                target = Plugin.MaxBonus.Value > 0.001f ? Mathf.Clamp01(_bonus / Plugin.MaxBonus.Value) : 0f;
                if (_singers > 0) target = Mathf.Max(target, 0.25f);
            }
            _level = Mathf.MoveTowards(_level, target, Time.deltaTime * 0.8f);
            if (_ship == null) { if (_level <= 0f) Clear(); return; }

            HelmView.Glow(_level * (0.55f + 0.45f * Beat));
            Ribbons();
        }

        internal static void Clear()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _ribbons.Clear();
            _ship = null;
            _level = 0f;
            _singers = 0;
        }

        private static void MeasureSail(Ship ship)
        {
            _sailCenter = new Vector3(0f, 5f, 0f);
            _sailSize = new Vector3(4f, 4f, 0.5f);
            if (ship.m_sailObject == null) return;
            bool any = false;
            Bounds b = new Bounds();
            foreach (Renderer r in ship.m_sailObject.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return;
            Transform t = ship.transform;
            _sailCenter = t.InverseTransformPoint(b.center);
            Vector3 ext = b.extents;
            // world-space box: width across the ship, height up
            _sailSize = new Vector3(
                Mathf.Abs(Vector3.Dot(t.right, Vector3.right)) * ext.x * 2f + Mathf.Abs(Vector3.Dot(t.right, Vector3.forward)) * ext.z * 2f,
                ext.y * 2f, 1f);
            _sailSize.x = Mathf.Clamp(_sailSize.x, 1.5f, 12f);
            _sailSize.y = Mathf.Clamp(_sailSize.y, 1.5f, 12f);
            Plugin.Debug("[glow] sail of " + ship.name + " at " + _sailCenter.ToString("0.0") + ", about " + _sailSize.x.ToString("0.0") + " x " + _sailSize.y.ToString("0.0") + " m");
        }

        // ------------------------------------------------------------ the song wind

        private static void Ribbons()
        {
            bool show = _level > 0.01f && (_ship.IsSailUp() || (int)_ship.GetSpeedSetting() == Song.SlowSpeed);
            int want = show ? Mathf.Clamp(2 + _singers * 2, 2, MaxRibbons) : 0;
            if (want > 0 && _root == null)
            {
                _root = new GameObject("SeaLegs_SongWind");
                _root.transform.SetParent(_ship.transform, false);
            }
            if (_root == null) return;
            while (_ribbons.Count < want) _ribbons.Add(NewRibbon(_ribbons.Count));
            for (int i = 0; i < _ribbons.Count; i++)
            {
                LineRenderer lr = _ribbons[i];
                bool on = i < want;
                if (lr.enabled != on) lr.enabled = on;
                if (on) Lay(lr, i);
            }
        }

        private static LineRenderer NewRibbon(int i)
        {
            GameObject go = new GameObject("ribbon" + i);
            go.transform.SetParent(_root.transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = RibbonPoints;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            lr.sharedMaterial = Material();
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.02f), new Keyframe(0.6f, 0.16f), new Keyframe(1f, 0.04f));
            return lr;
        }

        private static void Lay(LineRenderer lr, int i)
        {
            // each ribbon has its own lane across the sail and its own timing
            float lane = ((i * 0.618f) % 1f) * 2f - 1f;
            float laneY = (((i * 0.382f) + 0.17f) % 1f) * 2f - 1f;
            float period = 2.4f + (i % 3) * 0.35f;
            float phase = i * 1.7f;
            float run = ((Time.time + phase) % period) / period;          // 0..1 along the path
            float start = _sailCenter.z - 6f, end = _sailCenter.z + 9f;
            float head = Mathf.Lerp(start, end, run);
            float tail = head - 5f;
            for (int k = 0; k < RibbonPoints; k++)
            {
                float t = k / (float)(RibbonPoints - 1);
                float z = Mathf.Lerp(tail, head, t);
                float x = _sailCenter.x + lane * _sailSize.x * 0.38f + Mathf.Sin(z * 0.8f + phase) * 0.35f;
                float y = _sailCenter.y + laneY * _sailSize.y * 0.32f + Mathf.Sin(z * 0.55f + phase * 1.3f) * 0.3f;
                lr.SetPosition(k, new Vector3(x, y, z));
            }
            // fade in at the start of the path and out at the end, and with the song level
            float life = Mathf.Clamp01(run * 4f) * Mathf.Clamp01((1f - run) * 4f);
            float a = _level * life * (0.55f + 0.45f * Beat);
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Gold, 0f), new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0.75f), new GradientColorKey(Gold, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(a * 0.9f, 0.65f), new GradientAlphaKey(0f, 1f) });
            lr.colorGradient = g;
        }

        private static Material Material()
        {
            if (_mat != null) return _mat;
            Shader sh = null;
            foreach (string n in new[] { "Legacy Shaders/Particles/Additive", "Particles/Additive", "Sprites/Default" })
            {
                sh = Shader.Find(n);
                if (sh != null && sh.isSupported) break;
                sh = null;
            }
            _mat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
            _mat.hideFlags = HideFlags.HideAndDontSave;
            Plugin.Debug("[glow] ribbon shader " + _mat.shader.name);
            return _mat;
        }

        // ------------------------------------------------------------ the ship HUD: golden wind icon + badge

        [HarmonyPatch(typeof(Hud), "UpdateShipHud"), HarmonyPostfix]
        private static void ShipHud(Hud __instance)
        {
            if (__instance.m_shipWindIcon == null) return;
            float k = Plugin.Enabled.Value && _ship != null ? _level : 0f;
            if (k > 0.001f)
                __instance.m_shipWindIcon.color = Color.Lerp(__instance.m_shipWindIcon.color, Gold, k * (0.6f + 0.4f * Beat));
            if (__instance.m_shipWindIconRoot != null)
                __instance.m_shipWindIconRoot.localScale = Vector3.one * (1f + 0.12f * k * Beat);
        }

        internal static void OnGUI()
        {
            if (_ship == null || _level <= 0.01f || _singers <= 0 || !Plugin.HelmGlow.Value) return;
            Hud hud = Hud.instance;
            if (hud == null || hud.m_shipHudRoot == null || !hud.m_shipHudRoot.activeInHierarchy) return;
            if (_font == null)
                foreach (Font f in Resources.FindObjectsOfTypeAll<Font>())
                    if (f.name == "AveriaSerifLibre-Bold") { _font = f; break; }
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
                if (_font != null) _style.font = _font;
            }

            // right under the game's wind indicator. Both the indicator and the wind icon turn with the
            // camera, so we use the indicator's pivot (its middle, which stays put) and its size, never its corners.
            Rect anchor;
            RectTransform rt = hud.m_shipWindIndicatorRoot;
            if (rt != null)
            {
                Vector3 mid = rt.position;
                float half = Mathf.Max(Mathf.Abs(rt.rect.width * rt.lossyScale.x), Mathf.Abs(rt.rect.height * rt.lossyScale.y)) / 2f;
                if (half < 10f) half = 40f;
                if (!_anchorLogged) { _anchorLogged = true; Plugin.Debug("[glow] wind indicator at " + mid.ToString("0") + ", half size " + half.ToString("0") + ", pivot " + rt.pivot); }
                anchor = new Rect(mid.x - half, Screen.height - mid.y - half, half * 2f, half * 2f);
            }
            else anchor = new Rect(Screen.width / 2f - 40f, Screen.height - 260f, 80f, 80f);

            float a = Mathf.Clamp01(_level * 1.5f);
            float size = 30f;
            float cx = anchor.center.x, top = anchor.yMax + 2f;

            // clink on the beat: the mugs swing together, tilt their tops in, touch, and swing apart
            float s = Mathf.Sin(Time.time * Mathf.PI * 2f * 0.9f);
            float close = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s - 0.1f) / 0.9f));
            float gap = (1f - close) * 7f;
            Rect left = new Rect(cx - size + 3f - gap, top, size, size);
            Rect right = new Rect(cx - 3f + gap, top, size, size);
            Mug(left, close * 18f, false, a);
            Mug(right, -close * 18f, true, a);

            // a splash of foam where they meet
            if (close > 0.85f)
            {
                float k = (close - 0.85f) / 0.15f;
                for (int i = 0; i < 5; i++)
                {
                    float ang = (-70f + i * 35f) * Mathf.Deg2Rad;
                    float r = 4f + k * 9f;
                    Vector2 d = new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang)) * r;
                    float dot = 4f - k * 1.5f;
                    GUI.color = new Color(1f, 0.96f, 0.82f, a * (1f - k * 0.5f));
                    GUI.DrawTexture(new Rect(cx + d.x - dot / 2f, top + 3f + d.y - dot / 2f, dot, dot), Texture2D.whiteTexture);
                }
            }

            // the number of singers under the mugs, from two on
            if (_singers >= 2)
            {
                string text = _singers.ToString();
                Rect tr = new Rect(cx - 30f, top + size - 1f, 60f, 26f);
                GUI.color = new Color(0f, 0f, 0f, 0.85f * a);
                GUI.Label(new Rect(tr.x + 1.5f, tr.y + 1.5f, tr.width, tr.height), text, _style);
                GUI.color = new Color(1f, 0.85f, 0.45f, a);
                GUI.Label(tr, text, _style);
            }
            GUI.color = Color.white;
        }

        // one mug, tilted around the middle of its bottom; the right one is mirrored so the handles face out
        private static void Mug(Rect r, float angle, bool mirror, float a)
        {
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, new Vector2(r.center.x, r.yMax));
            Rect uv = mirror ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f * a);
            GUI.DrawTextureWithTexCoords(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), MugTex(), uv);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.DrawTextureWithTexCoords(r, MugTex(), uv);
            GUI.matrix = m;
        }

        // a wooden mug with iron bands and a foamy top, handle on the left, drawn in code
        private static Texture2D MugTex()
        {
            if (_mug != null) return _mug;
            const int n = 32;
            Color wood = new Color(0.62f, 0.4f, 0.2f), woodDark = new Color(0.38f, 0.23f, 0.11f);
            Color iron = new Color(0.55f, 0.55f, 0.58f), foam = new Color(1f, 0.96f, 0.84f), edge = new Color(0.16f, 0.1f, 0.05f);
            _mug = new Texture2D(n, n, TextureFormat.RGBA32, false);
            _mug.hideFlags = HideFlags.HideAndDontSave;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Color c = Color.clear;
                    bool body = x >= 10 && x <= 27 && y >= 2 && y <= 21;
                    float hx = x - 9.5f, hy = y - 12f;
                    float hr = Mathf.Sqrt(hx * hx * 1.3f + hy * hy);
                    bool handle = x < 10 && hr >= 3.6f && hr <= 6.8f;
                    float foamTop = 25f + 1.6f * Mathf.Sin(x * 0.95f) + 0.8f * Mathf.Sin(x * 2.3f + 1f);
                    bool froth = x >= 9 && x <= 28 && y >= 20 && y <= foamTop;
                    bool drip = x >= 22 && x <= 24 && y >= 15 && y <= 21;
                    if (handle) c = (hr < 4.4f || hr > 6.0f) ? edge : woodDark;
                    if (body)
                    {
                        bool stave = (x - 10) % 5 == 0;
                        bool band = (y >= 5 && y <= 6) || (y >= 16 && y <= 17);
                        c = band ? iron : (stave ? woodDark : Color.Lerp(wood, woodDark, (x - 10) / 30f));
                        if (x == 10 || x == 27 || y == 2) c = edge;
                    }
                    if (froth || drip) c = (froth && foamTop - y < 1.2f) ? foam : Color.Lerp(foam, new Color(0.9f, 0.82f, 0.62f), 0.15f);
                    _mug.SetPixel(x, y, c);
                }
            _mug.Apply();
            return _mug;
        }
    }
}
