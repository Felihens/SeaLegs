using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SeaLegs
{
    // At the helm, rocks hidden just under the water are painted on top of the water, so you can steer
    // around them. Only the part of each rock between the water surface and Depth metres below it is
    // painted: that is the part a boat can hit.
    //
    // Each rock's collision shape (what the boat actually bumps into) is copied once into world
    // coordinates. The UI shader can cut a mesh to a rectangle of its own x/y coordinates; with world
    // coordinates, y is the height, so the cut keeps exactly the band under the water.
    // The band is drawn in front of everything into a few stencil bits (a per-pixel scratch pad), then
    // one full-screen pass paints those pixels, so overlapping rocks are painted only once.
    internal static class Rocks
    {
        // Low stencil bits, cleared before and after: the rock under water, the rock just above it,
        // and the same two shifted a few pixels (for the outline).
        private const int RockBit = 1, NearBit = 2, AboveBit = 4, AboveNearBit = 8, AllBits = 15;

        private class Rock
        {
            public Mesh Baked;        // world-space copy, cut to the band by the shader
            public Mesh Shape;        // shape we can't copy: drawn whole, only if it is mostly under water
            public Matrix4x4 Matrix;
            public float Top;
        }

        private static Camera _cam;
        private static CommandBuffer _cb;
        private static bool _hooked, _made;
        private static Material _clear, _markBand, _markWhole, _markNear, _markNearWhole, _markAbove, _markAboveNear;
        private static Material _fillBelow, _fillAbove, _ringBelow, _ringAbove;
        private static Mesh _quad;

        private static readonly Dictionary<int, Rock> Cache = new Dictionary<int, Rock>();   // by collider id; null = not a rock
        private static readonly List<Rock> Near = new List<Rock>();
        private static readonly Collider[] Hits = new Collider[2048];
        private static readonly System.Reflection.FieldInfo ShipBody = HarmonyLib.AccessTools.Field(typeof(Ship), "m_body");
        private static float _lastRange = -1f;
        private static float _nextScan;
        private static int _mask = -1;
        private static Ship _scannedFor;

        internal static void LateUpdate()
        {
            Camera cam = GameCamera.instance != null ? Utils.GetMainCamera() : null;
            if (cam != _cam)
            {
                if (_cam != null && _cb != null) _cam.RemoveCommandBuffer(CameraEvent.AfterHaloAndLensFlares, _cb);
                _cam = cam;
                if (_cam != null)
                {
                    if (_cb == null) { _cb = new CommandBuffer(); _cb.name = "Sea Legs rocks"; }
                    _cam.AddCommandBuffer(CameraEvent.AfterHaloAndLensFlares, _cb);
                }
            }
            if (!_hooked) { Camera.onPreRender += PreRender; _hooked = true; }

            Ship ship = Plugin.Enabled.Value && Plugin.RockSilhouettes.Value ? Plugin.Steering() : null;
            if (ship == null) { if (Near.Count > 0) Near.Clear(); _scannedFor = null; return; }
            if (ship != _scannedFor || Time.time >= _nextScan) Scan(ship);
        }

        // ------------------------------------------------------------ find rocks

        private static void Scan(Ship ship)
        {
            _scannedFor = ship;
            _nextScan = Time.time + 1f;
            if (_mask < 0) _mask = LayerMask.GetMask("static_solid", "Default");
            float water = Water();
            float depth = Plugin.RockDepth.Value;
            float above = Mathf.Max(0f, Plugin.RockAboveHeight.Value);
            // faster boat = see further ahead: the extra range goes in front of the bow
            Vector3 center = ship.transform.position;
            float radius = Plugin.RockRange.Value;
            Rigidbody body = ShipBody != null ? ShipBody.GetValue(ship) as Rigidbody : null;
            if (body != null && Plugin.RockSecondsAhead.Value > 0f)
            {
                Vector3 v = body.linearVelocity;
                v.y = 0f;
                float extra = Mathf.Min(v.magnitude * Plugin.RockSecondsAhead.Value, Mathf.Max(0f, Plugin.RockMaxRange.Value - radius));
                if (extra > 1f)
                {
                    center += v.normalized * (extra * 0.5f);
                    radius += extra * 0.5f;
                }
                if (Plugin.DetailedLog.Value && Mathf.Abs(radius - _lastRange) > 10f)
                {
                    _lastRange = radius;
                    Plugin.Log.LogInfo("[rocks] speed " + v.magnitude.ToString("0.0") + " m/s: looking " + (Plugin.RockRange.Value + extra).ToString("0") + " m ahead");
                }
            }
            int n = Physics.OverlapSphereNonAlloc(center, radius, Hits, _mask, QueryTriggerInteraction.Ignore);
            Near.Clear();
            int baked = 0, whole = 0, made = 0;
            for (int i = 0; i < n; i++)
            {
                Collider c = Hits[i];
                if (c == null) continue;
                Bounds b = c.bounds;
                if (b.max.y < water - depth || b.min.y > water + above) continue;   // not in the band
                int id = c.GetInstanceID();
                Rock r;
                if (!Cache.TryGetValue(id, out r)) { r = Make(c); Cache[id] = r; made++; }
                if (r == null) continue;
                if (r.Baked != null) { Near.Add(r); baked++; }
                else if (r.Shape != null && r.Top < water + 1f) { Near.Add(r); whole++; }
            }
            if (Cache.Count > 4000) Cache.Clear();
            if (made > 0)
                Plugin.Debug("[rocks] " + n + " colliders around the boat, showing " + baked + " (cut to the band) + " + whole +
                    " (whole, shape not copyable); " + made + " new; water " + water.ToString("0.0"));
        }

        private static Rock Make(Collider c)
        {
            if (c.attachedRigidbody != null) return null;                       // boats, carts, loose things
            if (c.GetComponentInParent<Piece>() != null) return null;           // buildings
            if (c.GetComponentInParent<Ship>() != null) return null;
            if (c.GetComponentInParent<Character>() != null) return null;
            Bounds b = c.bounds;
            if (b.size.x > 300f || b.size.z > 300f) return null;

            Rock r = new Rock { Top = b.max.y };
            Mesh shape = null;
            MeshCollider mc = c as MeshCollider;
            if (mc != null) shape = mc.sharedMesh;
            BoxCollider bc = c as BoxCollider;
            Matrix4x4 m = c.transform.localToWorldMatrix;
            if (bc != null)
            {
                shape = Box();
                m = m * Matrix4x4.TRS(bc.center, Quaternion.identity, bc.size);
            }
            if (shape == null) return null;
            r.Matrix = m;

            if (shape.isReadable)
            {
                Vector3[] v = shape.vertices;
                int[] t = shape.triangles;
                if (v.Length > 0 && t.Length > 0)
                {
                    for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(v[i]);
                    Mesh baked = new Mesh();
                    baked.name = "Sea Legs rock " + c.name;
                    if (v.Length > 65000) baked.indexFormat = IndexFormat.UInt32;
                    baked.vertices = v;
                    Color32[] col = new Color32[v.Length];
                    for (int i = 0; i < col.Length; i++) col[i] = new Color32(255, 255, 255, 255);
                    baked.colors32 = col;
                    baked.triangles = t;
                    baked.RecalculateBounds();
                    r.Baked = baked;
                    return r;
                }
            }
            r.Shape = shape;
            return r;
        }

        private static float Water()
        {
            return ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
        }

        // ------------------------------------------------------------ draw

        private static void PreRender(Camera cam)
        {
            if (cam != _cam || _cb == null) return;
            _cb.Clear();
            if (Near.Count == 0 || _cam == null) return;
            Make();
            if (_quad == null) return;

            float water = Water();
            float above = Mathf.Max(0f, Plugin.RockAboveHeight.Value);
            Vector4 below = new Vector4(-1e7f, water - Plugin.RockDepth.Value, 1e7f, water);
            Vector4 over = new Vector4(-1e7f, water, 1e7f, water + above);
            _markBand.SetVector("_ClipRect", below);
            _markNear.SetVector("_ClipRect", below);
            _markAbove.SetVector("_ClipRect", over);
            _markAboveNear.SetVector("_ClipRect", over);
            float a = Plugin.RockOpacity.Value;
            Color cb = Plugin.RockColor.Value; cb.a = a;
            Color ca = Plugin.RockAboveColor.Value; ca.a = a;
            Plugin.RockLook look = Plugin.RockStyle.Value;
            bool fill = look != Plugin.RockLook.Outline, ring = look != Plugin.RockLook.Fill;
            bool showAbove = above > 0.01f;

            Matrix4x4 view = _cam.worldToCameraMatrix, proj = _cam.projectionMatrix, front = AtNear(proj);
            _cb.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            _cb.DrawMesh(_quad, Matrix4x4.identity, _clear);

            _cb.SetViewProjectionMatrices(view, front);
            Mark(_markBand, _markWhole, showAbove ? _markAbove : null);
            if (ring)
            {
                float px = Plugin.RockOutlineWidth.Value;
                float w = Mathf.Max(1, _cam.pixelWidth), h = Mathf.Max(1, _cam.pixelHeight);
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * Mathf.PI / 4f;
                    _cb.SetViewProjectionMatrices(view, Shift(front, Mathf.Cos(ang) * px * 2f / w, Mathf.Sin(ang) * px * 2f / h));
                    Mark(_markNear, _markNearWhole, showAbove ? _markAboveNear : null);
                }
            }

            _cb.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            if (fill)
            {
                _fillBelow.SetColor("_Color", cb);
                _cb.DrawMesh(_quad, Matrix4x4.identity, _fillBelow);
                if (showAbove) { _fillAbove.SetColor("_Color", ca); _cb.DrawMesh(_quad, Matrix4x4.identity, _fillAbove); }
            }
            if (ring)
            {
                cb.a = Mathf.Min(1f, a * 2.5f); ca.a = cb.a;   // a thin line needs more strength than a fill to show
                if (showAbove) { _ringAbove.SetColor("_Color", ca); _cb.DrawMesh(_quad, Matrix4x4.identity, _ringAbove); }
                _ringBelow.SetColor("_Color", cb);
                _cb.DrawMesh(_quad, Matrix4x4.identity, _ringBelow);
            }
            _cb.DrawMesh(_quad, Matrix4x4.identity, _clear);
            _cb.SetViewProjectionMatrices(view, proj);
        }

        private static void Mark(Material band, Material whole, Material above)
        {
            foreach (Rock r in Near)
            {
                if (r.Baked != null)
                {
                    _cb.DrawMesh(r.Baked, Matrix4x4.identity, band);
                    if (above != null) _cb.DrawMesh(r.Baked, Matrix4x4.identity, above);
                }
                else if (r.Shape != null) _cb.DrawMesh(r.Shape, r.Matrix, whole);
            }
        }

        // Moves the picture by (ox, oy) in screen units (-1..1).
        private static Matrix4x4 Shift(Matrix4x4 p, float ox, float oy)
        {
            p.m00 += ox * p.m30; p.m01 += ox * p.m31; p.m02 += ox * p.m32; p.m03 += ox * p.m33;
            p.m10 += oy * p.m30; p.m11 += oy * p.m31; p.m12 += oy * p.m32; p.m13 += oy * p.m33;
            return p;
        }

        // Everything lands just in front of the near plane, so it is drawn in front of everything.
        private static Matrix4x4 AtNear(Matrix4x4 p)
        {
            p.m20 = -0.999f * p.m30; p.m21 = -0.999f * p.m31; p.m22 = -0.999f * p.m32; p.m23 = -0.999f * p.m33;
            return p;
        }

        private static Material StencilMat(Shader sh, int reference, int readMask, int writeMask, CompareFunction comp, StencilOp op, int colorMask)
        {
            Material m = new Material(sh);
            m.hideFlags = HideFlags.HideAndDontSave;
            m.SetFloat("_Stencil", reference);
            m.SetFloat("_StencilReadMask", readMask);
            m.SetFloat("_StencilWriteMask", writeMask);
            m.SetFloat("_StencilComp", (float)comp);
            m.SetFloat("_StencilOp", (float)op);
            m.SetFloat("_ColorMask", colorMask);
            return m;
        }

        private static void Make()
        {
            if (_made) return;
            _made = true;
            Shader ui = Shader.Find("UI/Default");
            if (ui == null || !ui.isSupported) { Plugin.Log.LogWarning("Rock silhouettes are off: no usable shader was found"); return; }
            _clear = StencilMat(ui, 0, 0, AllBits, CompareFunction.Always, StencilOp.Replace, 0);
            _markWhole = StencilMat(ui, RockBit, 0, RockBit, CompareFunction.Always, StencilOp.Replace, 0);
            _markNearWhole = StencilMat(ui, NearBit, 0, NearBit, CompareFunction.Always, StencilOp.Replace, 0);
            _markBand = Clipped(StencilMat(ui, RockBit, 0, RockBit, CompareFunction.Always, StencilOp.Replace, 0));
            _markNear = Clipped(StencilMat(ui, NearBit, 0, NearBit, CompareFunction.Always, StencilOp.Replace, 0));
            _markAbove = Clipped(StencilMat(ui, AboveBit, 0, AboveBit, CompareFunction.Always, StencilOp.Replace, 0));
            _markAboveNear = Clipped(StencilMat(ui, AboveNearBit, 0, AboveNearBit, CompareFunction.Always, StencilOp.Replace, 0));
            // fill: the rock itself (under water wins where both meet)
            _fillBelow = StencilMat(ui, RockBit, RockBit, 0, CompareFunction.Equal, StencilOp.Keep, 15);
            _fillAbove = StencilMat(ui, AboveBit, AboveBit | RockBit, 0, CompareFunction.Equal, StencilOp.Keep, 15);
            // line: near a rock but outside every rock; near both -> under-water color
            _ringBelow = StencilMat(ui, NearBit, NearBit | RockBit | AboveBit, 0, CompareFunction.Equal, StencilOp.Keep, 15);
            _ringAbove = StencilMat(ui, AboveNearBit, AboveNearBit | NearBit | RockBit | AboveBit, 0, CompareFunction.Equal, StencilOp.Keep, 15);
            _quad = new Mesh();
            _quad.name = "Sea Legs screen quad";
            _quad.vertices = new Vector3[]
            {
                new Vector3(-1f, -1f, -0.999f), new Vector3(1f, -1f, -0.999f), new Vector3(1f, 1f, -0.999f), new Vector3(-1f, 1f, -0.999f),
            };
            _quad.uv = new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            _quad.colors = new Color[] { Color.white, Color.white, Color.white, Color.white };
            _quad.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            _quad.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            Plugin.Debug("[rocks] silhouettes ready (shader " + ui.name + ")");
        }

        // Only the part inside _ClipRect (world x/height) is drawn.
        private static Material Clipped(Material m)
        {
            m.EnableKeyword("UNITY_UI_CLIP_RECT");     // fade outside the band...
            m.EnableKeyword("UNITY_UI_ALPHACLIP");     // ...and skip those pixels entirely
            return m;
        }

        private static Mesh _box;
        private static Mesh Box()
        {
            if (_box != null) return _box;
            _box = new Mesh();
            _box.name = "Sea Legs box";
            Vector3[] v = new Vector3[8];
            for (int i = 0; i < 8; i++) v[i] = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
            _box.vertices = v;
            _box.triangles = new int[]
            {
                0, 2, 1, 1, 2, 3,  4, 5, 6, 5, 7, 6,  0, 1, 4, 1, 5, 4,
                2, 6, 3, 3, 6, 7,  0, 4, 2, 2, 4, 6,  1, 3, 5, 3, 7, 5,
            };
            _box.RecalculateBounds();
            return _box;
        }
    }
}
