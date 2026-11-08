using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SeaLegs
{
    // While you steer: the camera moves further back and a little higher, and the sail turns
    // see-through, so the sail no longer blocks the view ahead. Only your own screen changes.
    [HarmonyPatch]
    internal static class HelmView
    {
        private static readonly FieldInfo CamDistance = AccessTools.Field(typeof(GameCamera), "m_distance");

        private static Ship _ship;            // ship you are steering
        private static float _origMaxBoat = -1f;

        private class SailSwap
        {
            public Renderer R;
            public Material[] Original;
            public Material[] SeeThrough;
        }
        private static readonly List<SailSwap> Swaps = new List<SailSwap>();
        private static Shader _shader;
        private static bool _shaderChecked;
        private static float _appliedOpacity = -1f;

        internal static void Update()
        {
            Ship ship = Plugin.Steering();
            if (ship != _ship)
            {
                Leave();
                if (ship != null) Enter(ship);
            }
            if (_ship == null) return;

            Song.UpdateHelm(_ship);
            ArrowKeys();
            if (Plugin.SeeThroughSail.Value && Swaps.Count == 0) SailOn(_ship);
            if (!Plugin.SeeThroughSail.Value && Swaps.Count > 0) SailOff();
            if (Swaps.Count > 0 && !Mathf.Approximately(_appliedOpacity, Plugin.SailOpacity.Value)) SetOpacity();
        }

        private static void Enter(Ship ship)
        {
            _ship = ship;
            Plugin.Debug("[helm] took the helm of " + ship.name);
            GameCamera gc = GameCamera.instance;
            float want = Plugin.CameraDistance.Value;
            if (gc != null && want > 0f)
            {
                if (_origMaxBoat < 0f) _origMaxBoat = gc.m_maxDistanceBoat;
                gc.m_maxDistanceBoat = Mathf.Max(_origMaxBoat, want);
                if (CamDistance != null) CamDistance.SetValue(gc, want);
                Plugin.Debug("[helm] camera distance " + want + " (boat max was " + _origMaxBoat + ")");
            }
        }

        internal static void Leave()
        {
            if (_ship == null) return;
            Plugin.Debug("[helm] left the helm");
            SailOff();
            GameCamera gc = GameCamera.instance;
            if (gc != null && _origMaxBoat >= 0f) gc.m_maxDistanceBoat = _origMaxBoat;
            _ship = null;
            Song.UpdateHelm(null);
        }

        // Arrow keys: left/right slide the camera sideways, up/down raise or lower it. The values are kept
        // while you hold a key and saved to the config when you let go.
        private static float _side = float.NaN, _height = float.NaN;
        private static bool _moving;

        private static void ArrowKeys()
        {
            if (float.IsNaN(_side)) { _side = Plugin.CameraSide.Value; _height = Plugin.CameraHeight.Value; }
            if (!Plugin.CameraKeys.Value || Busy()) return;
            float dx = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float dy = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            if (dx != 0f || dy != 0f)
            {
                float step = Plugin.CameraKeySpeed.Value * Time.deltaTime;
                _side = Mathf.Clamp(_side + dx * step, -8f, 8f);
                _height = Mathf.Clamp(_height + dy * step, -2f, 10f);
                _moving = true;
            }
            else if (_moving)
            {
                _moving = false;
                Plugin.CameraSide.Value = (float)System.Math.Round(_side, 2);
                Plugin.CameraHeight.Value = (float)System.Math.Round(_height, 2);
                Plugin.Debug("[helm] camera moved: side " + Plugin.CameraSide.Value + " m, height " + Plugin.CameraHeight.Value + " m");
            }
        }

        private static bool Busy()
        {
            return (Chat.instance != null && Chat.instance.HasFocus()) || Console.IsVisible() || Menu.IsVisible() ||
                   InventoryGui.IsVisible() || TextInput.IsVisible() || Minimap.IsOpen();
        }

        // Move the camera while steering (up by default), so you look over the deck and the crew.
        [HarmonyPatch(typeof(GameCamera), "GetCameraPosition")]
        [HarmonyPostfix]
        private static void Raise(ref Vector3 pos, ref Quaternion rot)
        {
            if (_ship == null || !Plugin.Enabled.Value) return;
            if (!_moving) { _side = Plugin.CameraSide.Value; _height = Plugin.CameraHeight.Value; }
            Vector3 right = rot * Vector3.right;
            right.y = 0f;
            if (right.sqrMagnitude > 0.0001f) pos += right.normalized * _side;
            pos += Vector3.up * _height;
        }

        // ------------------------------------------------------------ sail

        private static void SailOn(Ship ship)
        {
            if (ship.m_sailObject == null) return;
            Shader sh = FindShader();
            if (sh == null) return;
            foreach (Renderer r in ship.m_sailObject.GetComponentsInChildren<Renderer>(true))
            {
                Material[] orig = r.sharedMaterials;
                if (orig == null || orig.Length == 0) continue;
                SailSwap s = new SailSwap { R = r, Original = orig, SeeThrough = new Material[orig.Length] };
                for (int i = 0; i < orig.Length; i++)
                {
                    Material m = new Material(sh);
                    m.hideFlags = HideFlags.HideAndDontSave;
                    if (orig[i] != null && orig[i].HasProperty("_MainTex")) { m.mainTexture = orig[i].mainTexture; m.mainTextureScale = orig[i].mainTextureScale; m.mainTextureOffset = orig[i].mainTextureOffset; }
                    s.SeeThrough[i] = m;
                }
                r.sharedMaterials = s.SeeThrough;
                Swaps.Add(s);
            }
            _appliedOpacity = -1f;
            SetOpacity();
            Plugin.Debug("[helm] sail see-through: " + Swaps.Count + " renderer(s), shader " + sh.name);
        }

        private static void SetOpacity()
        {
            float a = Mathf.Clamp01(Plugin.SailOpacity.Value);
            foreach (SailSwap s in Swaps)
                foreach (Material m in s.SeeThrough) if (m != null) m.color = new Color(0.85f, 0.85f, 0.85f, a);
            foreach (SailSwap s in Swaps) if (s.R != null) s.R.enabled = a > 0.001f;
            _appliedOpacity = Plugin.SailOpacity.Value;
        }

        private static void SailOff()
        {
            foreach (SailSwap s in Swaps)
            {
                if (s.R != null) { s.R.sharedMaterials = s.Original; s.R.enabled = true; }
                foreach (Material m in s.SeeThrough) if (m != null) Object.Destroy(m);
            }
            if (Swaps.Count > 0) Plugin.Debug("[helm] sail back to normal");
            Swaps.Clear();
        }

        private static Shader FindShader()
        {
            if (_shaderChecked) return _shader;
            _shaderChecked = true;
            foreach (string n in new[] { "Sprites/Default", "UI/Default", "Legacy Shaders/Transparent/Diffuse" })
            {
                Shader s = Shader.Find(n);
                if (s != null && s.isSupported) { _shader = s; break; }
            }
            if (_shader == null) Plugin.Log.LogWarning("No see-through shader found; the sail stays normal.");
            return _shader;
        }
    }
}
