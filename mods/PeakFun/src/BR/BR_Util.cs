// Мелкие помощники: игрок, статусы, телепорт, ложные гейты.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static class Util
    {
        public static Component Me() { return Game.Me(); }
        public static Component MeAlive() { var m = Game.Me(); return (m != null && !Game.IsDead(m)) ? m : null; }
        public static bool Alive(Component c) { return c != null && !Game.IsDead(c); }
        public static Vector3 CenterOf(Component c) { var v = R.Get(c, "Center"); return v is Vector3 p ? p : c.transform.position; }
        public static bool Crouching(Component c) { var d = R.Get(c, "data"); return R.Get<bool>(d, "isCrouching"); }

        public static int TerrainMask()
        {
            try
            {
                var hf = R.FindType("HelperFunctions"); var lt = R.FindType("HelperFunctions+LayerType");
                if (hf != null && lt != null && R.TryInvokeStatic(hf, "GetMask", out var m, Enum.ToObject(lt, 1))) { if (m is LayerMask lm) return lm.value; if (m is int i) return i; }
            }
            catch { }
            return ~0;
        }

        public static void Status(string status, float amount)
        {
            var me = MeAlive(); if (me == null) return; var affs = Game.Afflictions(me); if (affs == null) return;
            try { R.TryInvoke(affs, "AddStatus", out var _, status, amount); } catch (Exception e) { Plugin.Warn("status " + status, e); }
        }
        static readonly Dictionary<string, float> drips = new Dictionary<string, float>();
        public static void Drip(string status, float perSec, float dt)
        {
            float b; drips.TryGetValue(status, out b); b += perSec * dt; if (b >= 0.03f) { Status(status, b); b = 0f; } drips[status] = b;
        }
        /// <summary>Наложить аффликцию игры по имени класса (как в Actions): лечение, бонусная выносливость и т.п.</summary>
        public static bool AddAff(string cls, Action<object> setup, params object[] args)
        {
            try
            {
                var me = MeAlive(); if (me == null) return false; var affs = Game.Afflictions(me); if (affs == null) return false;
                var t = R.FindType("Peak.Afflictions." + cls) ?? R.FindType(cls); if (t == null) return false;
                object a = R.New(t, args); if (a == null) return false; if (setup != null) setup(a);
                if (R.TryInvoke(affs, "AddAffliction", out var _, a, false)) return true;
                return R.TryInvoke(affs, "AddAffliction", out var _2, a);
            }
            catch (Exception e) { Plugin.Warn("аффликция " + cls, e); return false; }
        }
        /// <summary>Миндальная (минеральная) вода: снимает статусы, лечит, даёт запас сил и успокаивает.</summary>
        public static void Refresh()
        {
            ClearStatuses();
            AddAff("Affliction_HealAll", a => R.Set(a, "maxHealing", 1f));
            AddAff("Affliction_AddBonusStamina", null, 1f, 0f);
            Dread.Level = Mathf.Max(0f, Dread.Level - 0.4f);
        }
        public static void ClearStatuses() { var me = MeAlive(); if (me == null) return; var affs = Game.Afflictions(me); if (affs != null && !R.TryInvoke(affs, "ClearAllStatus", out var _)) R.TryInvoke(affs, "ClearAll", out var _2); }

        /// <summary>Телепорт своего персонажа через RPC игры (как делает сама игра).</summary>
        public static bool Warp(Vector3 pos)
        {
            var me = Me(); if (me == null) return false;
            string err = Game.Rpc(me, "WarpPlayerRPC", pos, false);
            if (err != null) { Plugin.Log("Warp: " + err); try { me.transform.position = pos; } catch { } return false; }
            try { Game.HaltBody(me); } catch { }
            return true;
        }

        public static float Rand(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }
        public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>Есть ли прямая видимость между двумя точками (стены уровня на нашем слое).</summary>
        public static bool Los(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a; float len = d.magnitude; if (len < 0.1f) return true;
            return !Physics.Raycast(a, d / len, len - 0.2f, 1 << LevelBuilder.Layer(), QueryTriggerInteraction.Ignore);
        }
    }
}
