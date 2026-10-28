// Ввод, устойчивый к тому, что игра может использовать только новую Input System.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PeakJJK
{
    internal static class Keys
    {
        static bool legacyBroken;
        static Type kbType, keyEnumType;
        static PropertyInfo kbCurrent, kbItem;
        static bool newInited;
        static readonly Dictionary<KeyCode, int> fired = new Dictionary<KeyCode, int>();
        static readonly Dictionary<KeyCode, object> keyObj = new Dictionary<KeyCode, object>();

        [MethodImpl(MethodImplOptions.NoInlining)] static bool LegacyDown(KeyCode k) { return Input.GetKeyDown(k); }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LegacyHeld(KeyCode k) { return Input.GetKey(k); }

        static void InitNew()
        {
            if (newInited) return;
            newInited = true;
            kbType = R.FindType("UnityEngine.InputSystem.Keyboard");
            keyEnumType = R.FindType("UnityEngine.InputSystem.Key");
            if (kbType == null || keyEnumType == null) return;
            kbCurrent = kbType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            foreach (var p in kbType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var ip = p.GetIndexParameters();
                if (ip.Length == 1 && ip[0].ParameterType == keyEnumType) { kbItem = p; break; }
            }
        }

        static object Control(KeyCode k)
        {
            InitNew();
            if (kbCurrent == null || kbItem == null) return null;
            var kb = kbCurrent.GetValue(null, null);
            if (kb == null) return null;
            if (!keyObj.TryGetValue(k, out var key))
            {
                string n = k.ToString();
                if (n == "LeftControl") n = "LeftCtrl"; else if (n == "RightControl") n = "RightCtrl";
                else if (n.StartsWith("Alpha")) n = "Digit" + n.Substring(5);
                try { key = Enum.Parse(keyEnumType, n, true); } catch { key = null; }
                keyObj[k] = key;
            }
            if (key == null) return null;
            try { return kbItem.GetValue(kb, new[] { key }); } catch { return null; }
        }

        /// <summary>true один раз за кадр, когда клавишу нажали (из любого источника).</summary>
        public static bool Fire(KeyCode k)
        {
            if (fired.TryGetValue(k, out var f) && f == Time.frameCount) return false;
            fired[k] = Time.frameCount;
            return true;
        }

        public static bool Down(KeyCode k)
        {
            bool d = false;
            if (!legacyBroken)
            {
                try { d = LegacyDown(k); } catch { legacyBroken = true; }
            }
            if (!d)
            {
                var c = Control(k);
                if (c != null && R.Get(c, "wasPressedThisFrame") is bool b && b) d = true;
            }
            return d && Fire(k);
        }

        // ---- мышь (0 ЛКМ, 1 ПКМ, 2 СКМ) ----
        static bool mouseBroken, mouseInit; static PropertyInfo mouseCur;
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LMDown(int b) { return Input.GetMouseButtonDown(b); }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool LMHeld(int b) { return Input.GetMouseButton(b); }
        static object MouseCtl(int b)
        {
            if (!mouseInit)
            {
                mouseInit = true; var t = R.FindType("UnityEngine.InputSystem.Mouse");
                if (t != null) mouseCur = t.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            }
            if (mouseCur == null) return null;
            var m = mouseCur.GetValue(null, null); if (m == null) return null;
            return R.Get(m, b == 0 ? "leftButton" : b == 1 ? "rightButton" : "middleButton");
        }
        public static bool MouseDown(int b)
        {
            if (!mouseBroken) { try { if (LMDown(b)) return true; } catch { mouseBroken = true; } }
            var c = MouseCtl(b); return c != null && R.Get(c, "wasPressedThisFrame") is bool x && x;
        }
        public static bool MouseHeld(int b)
        {
            if (!mouseBroken) { try { if (LMHeld(b)) return true; } catch { mouseBroken = true; } }
            var c = MouseCtl(b); return c != null && R.Get(c, "isPressed") is bool x && x;
        }

        public static bool Held(KeyCode k)
        {
            if (!legacyBroken)
            {
                try { if (LegacyHeld(k)) return true; } catch { legacyBroken = true; }
            }
            var c = Control(k);
            return c != null && R.Get(c, "isPressed") is bool b && b;
        }
    }
}
