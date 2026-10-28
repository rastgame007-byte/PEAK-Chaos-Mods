// Всё, что мод делает с игроком PEAK: позиция, урон/лечение через статусы, движение, толчки, телепорт, оглушение.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeakJJK
{
    internal static class Pl
    {
        public static Component Me() { return Game.Me(); }
        public static bool Alive { get { var m = Game.Me(); return m != null && !Game.IsDead(m); } }
        public static Vector3 Pos
        {
            get
            {
                var m = Game.Me(); if (m == null) return lastPos;
                var c = R.Get(m, "Center"); if (c is Vector3) { lastPos = (Vector3)c; return lastPos; }
                lastPos = m.transform.position; return lastPos;
            }
        }
        static Vector3 lastPos;
        public static Vector3 Fwd { get { var c = Cam; return c != null ? Vector3.ProjectOnPlane(c.transform.forward, Vector3.up).normalized : Vector3.forward; } }
        public static Camera Cam { get { return Camera.main; } }
        public static int Mask
        {
            get
            {
                if (mask != 0) return mask;
                try
                {
                    var hf = R.FindType("HelperFunctions"); var lt = R.FindType("HelperFunctions+LayerType");
                    if (hf != null && lt != null && R.TryInvokeStatic(hf, "GetMask", out var m, Enum.ToObject(lt, 1))) { if (m is LayerMask lm) mask = lm.value; else if (m is int i) mask = i; }
                }
                catch { }
                if (mask == 0) mask = ~0; return mask;
            }
        }
        static int mask;

        // ---------- урон и лечение ----------
        static float iframesUntil; public static float Invuln; // Invuln — пока >0, урон не проходит (бесконечность, домены)
        public static float TotalTaken, LastHurt;
        public static bool Hurt(float injury, string status = "Injury", float iframes = 0.5f)
        {
            if (!Alive) return false;
            if (Time.time < iframesUntil || Time.time < Invuln) return false;
            injury *= Plugin.CfgDamage;
            float mod = Skills.IncomingMul(); injury *= mod; if (injury <= 0.0005f) return false;
            iframesUntil = Time.time + iframes; TotalTaken += injury; LastHurt = Time.time;
            Status(status, injury);
            Sfx.Hurt(0.5f); Fx.Flash(new Color(0.8f, 0.05f, 0.05f), 0.35f, 4f); Fx.Shake(0.25f, 0.25f);
            return true;
        }
        public static void Status(string status, float amount)
        {
            var me = Me(); if (me == null || Game.IsDead(me)) return; var affs = Game.Afflictions(me); if (affs == null) return;
            try { R.TryInvoke(affs, "AddStatus", out var _, status, amount); } catch { }
        }
        static Type healT;
        /// <summary>ОПТ: короткий импульс лечения (Affliction_HealAll).</summary>
        public static void Heal(float maxHealing = 0.12f, float dur = 1f)
        {
            var me = Me(); if (me == null || Game.IsDead(me)) return; var affs = Game.Afflictions(me); if (affs == null) return;
            try
            {
                if (healT == null) healT = R.FindType("Peak.Afflictions.Affliction_HealAll") ?? R.FindType("Affliction_HealAll"); if (healT == null) return;
                var a = R.New(healT); if (a == null) return; R.Set(a, "maxHealing", maxHealing); R.Set(a, "totalTime", dur);
                if (!R.TryInvoke(affs, "AddAffliction", out var _, a, false)) R.TryInvoke(affs, "AddAffliction", out var _2, a);
            }
            catch { }
        }

        // ---------- движение ----------
        static readonly Dictionary<string, float[]> muls = new Dictionary<string, float[]>(); static float baseForce = -1f, baseJump = -1f; static bool touched; static float nextApply;
        /// <summary>Множители скорости и прыжка по ключу (0 убирает).</summary>
        public static void Mul(string key, float speed, float jump) { if (speed == 1f && jump == 1f) muls.Remove(key); else muls[key] = new[] { speed, jump }; nextApply = 0; }
        public static void Tick()
        {
            if (Time.time < nextApply) return; nextApply = Time.time + 0.2f; var me = Me(); if (me == null) return;
            float s = 1f, j = 1f; foreach (var v in muls.Values) { s *= v[0]; j *= v[1]; }
            if (s == 1f && j == 1f && !touched) return; touched = s != 1f || j != 1f;
            var mv = Game.Movement(me); if (mv == null) return;
            if (baseForce < 0f) { baseForce = R.Get<float>(mv, "movementForce"); baseJump = R.Get<float>(mv, "jumpImpulse"); }
            if (baseForce > 0f) R.Set(mv, "movementForce", baseForce * s); if (baseJump > 0f) R.Set(mv, "jumpImpulse", baseJump * j);
        }
        public static void Reset() { muls.Clear(); nextApply = 0; touched = true; Tick(); Invuln = 0; }
        static float stunUntil; public static bool Stunned { get { return Time.time < stunUntil; } }
        public static void Stun(float dur) { stunUntil = Mathf.Max(stunUntil, Time.time + dur); Mul("stun", 0.0f, 0.0f); stunEnd = stunUntil; }
        static float stunEnd;
        public static void StunTick() { if (stunEnd > 0 && Time.time >= stunEnd) { stunEnd = 0; Mul("stun", 1f, 1f); } }

        public static void Push(Vector3 vel)
        {
            var me = Me(); if (me == null) return; foreach (var rb in Game.Rigs(me)) rb.AddForce(vel, ForceMode.VelocityChange);
        }
        public static void Fall(float t = 2f) { Game.Rpc(Me(), "RPCA_Fall", t); }
        public static void Warp(Vector3 p)
        {
            var me = Me(); if (me == null) return; Game.Rpc(me, "WarpPlayerRPC", p, false); Game.HaltBody(me);
        }
        public static float Speed { get { var me = Me(); if (me == null) return 0; float m = 0; foreach (var rb in Game.Rigs(me)) { m = Mathf.Max(m, rb.velocity.magnitude); break; } return m; } }
    }
}
