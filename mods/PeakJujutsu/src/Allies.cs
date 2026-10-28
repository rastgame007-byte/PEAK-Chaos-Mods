// Призванные существа: божественные псы, Нуэ, Махорага, «Лучший друг» Тодо.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakJJK
{
    internal sealed class Ally { public string kind; public ModelB m; public GameObject go; public Vector3 pos; public float life, cd, t, adapt = 1f, seed, yaw, born; public int hits; public bool moving; public Vector3 anchor; }

    internal static class Allies
    {
        public static readonly List<Ally> L = new List<Ally>();
        public static int Count(string kind) { int n = 0; foreach (var a in L) if (a.kind == kind) n++; return n; }

        public static Ally Spawn(string kind, Vector3 pos, float life)
        {
            var a = new Ally { kind = kind, life = life, seed = UnityEngine.Random.value * 50f, born = Time.time };
            switch (kind)
            {
                case "dog_w": a.m = Models.Hound(new Color(0.92f, 0.92f, 0.95f), new Color(0.2f, 0.2f, 0.25f), new Color(0.5f, 0.8f, 1f)); break;
                case "dog_b": a.m = Models.Hound(new Color(0.04f, 0.04f, 0.06f), new Color(0.8f, 0.8f, 0.9f), new Color(0.9f, 0.3f, 0.3f)); break;
                case "nue": a.m = Models.Nue(); break;
                case "mahoraga": a.m = Models.Mahoraga(); break;
                default: a.m = Models.Clone(); break;
            }
            a.go = a.m.Finish(); a.go.name = "JJK_ally_" + kind; a.pos = pos; a.go.transform.position = pos; a.go.transform.localScale = Vector3.zero; L.Add(a);
            Fx.Burst(pos + Vector3.up * 0.8f, 20, 4f, 0.4f, kind == "dog_b" ? new Color(0.1f, 0.1f, 0.2f, 0.8f) : new Color(0.7f, 0.85f, 1f), 0.8f); Fx.Ring(pos + Vector3.up * 0.2f, Vector3.up, new Color(0.6f, 0.8f, 1f), 3f, 0.5f, 0.3f);
            return a;
        }
        public static void Clear() { foreach (var a in L) if (a.go != null) UnityEngine.Object.Destroy(a.go); L.Clear(); }
        public static void KillKind(string kind) { for (int i = L.Count - 1; i >= 0; i--) if (L[i].kind == kind) L[i].life = 0f; }

        static void Rot(ModelB m, string part, Vector3 e) { Transform t; if (m.parts.TryGetValue(part, out t)) t.localRotation = Quaternion.Euler(e); }
        static float Gy(Vector3 p, float fallback) { bool f; float y = World.Ground(p, out f); return f ? y : fallback; }

        public static void Tick(float dt)
        {
            Vector3 pl = Pl.Pos;
            for (int i = L.Count - 1; i >= 0; i--)
            {
                var a = L[i]; a.t += dt; a.life -= dt; float age = Time.time - a.born;
                if (a.life <= 0f || a.go == null) { if (a.go != null) { Fx.Burst(a.pos + Vector3.up, 14, 3f, 0.3f, new Color(0.6f, 0.7f, 1f), 0.6f); UnityEngine.Object.Destroy(a.go); } L.RemoveAt(i); continue; }
                a.moving = false; a.cd -= dt;
                try { switch (a.kind) { case "dog_w": case "dog_b": Dog(a, dt, pl); break; case "nue": Nue(a, dt, pl); break; case "mahoraga": Mahoraga(a, dt, pl); break; default: Clone(a, dt, pl); break; } } catch (Exception e) { Plugin.Log("союзник " + a.kind, e); a.life = 0; }
                float grow = Mathf.Clamp01(age / 0.5f); var tr = a.go.transform; tr.position = a.pos; tr.rotation = Quaternion.Euler(0, a.yaw, 0); tr.localScale = Vector3.one * grow;
            }
        }
        static void Step(Ally a, Vector3 target, float speed, float dt, float hover = 0f)
        {
            Vector3 d = target - a.pos; d.y = 0f; float l = d.magnitude; if (l > 0.05f) { a.pos += d / l * Mathf.Min(l, speed * dt); a.yaw = Mathf.LerpAngle(a.yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, dt * 10f); a.moving = l > 0.3f; }
            float gy = Gy(a.pos, a.pos.y - hover) + hover; a.pos.y += Mathf.Clamp(gy - a.pos.y, -25f * dt, 10f * dt);
        }
        static void Face(Ally a, Vector3 p, float dt) { Vector3 d = p - a.pos; d.y = 0f; if (d.sqrMagnitude > 0.01f) a.yaw = Mathf.LerpAngle(a.yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, dt * 12f); }

        static void Dog(Ally a, float dt, Vector3 pl)
        {
            var e = Aim.Nearest(a.pos, 55f); float sp = 15f;
            if (e != null)
            {
                float d = Vector3.Distance(new Vector3(e.pos.x, 0, e.pos.z), new Vector3(a.pos.x, 0, a.pos.z));
                if (d > 1.7f + e.Radius) Step(a, e.pos, sp, dt); else { Face(a, e.pos, dt); if (a.cd <= 0f) { a.cd = 0.55f; Vector3 dir = e.pos - a.pos; World.Damage(e, 13f * Skills.DmgMul(), dir, 3f, false, new Color(0.9f, 0.95f, 1f)); Sfx.Slash(a.pos, 0.3f); } }
            }
            else { float d = Vector3.Distance(a.pos, pl); if (d > 4f) Step(a, pl, sp, dt); }
            float sw = Mathf.Sin(a.t * 14f + a.seed) * 38f * (a.moving ? 1f : 0.1f); var m = a.m; Rot(m, "legFL", new Vector3(sw, 0, 0)); Rot(m, "legBR", new Vector3(sw, 0, 0)); Rot(m, "legFR", new Vector3(-sw, 0, 0)); Rot(m, "legBL", new Vector3(-sw, 0, 0)); Rot(m, "jaw", new Vector3(a.cd > 0.35f ? 30f : 5f, 0, 0));
        }
        static void Nue(Ally a, float dt, Vector3 pl)
        {
            Vector3 c = a.anchor == Vector3.zero ? pl : a.anchor; float ang = a.t * 1.8f; Vector3 tgt = c + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 8f; float gy = Gy(c, c.y);
            Vector3 want = new Vector3(tgt.x, gy + 14f, tgt.z); a.pos = Vector3.MoveTowards(a.pos, want, 28f * dt); Face(a, tgt + new Vector3(-Mathf.Sin(ang), 0, Mathf.Cos(ang)) * 5f, dt);
            float fl = Mathf.Sin(a.t * 12f) * 35f; Rot(a.m, "wingL", new Vector3(0, 0, fl)); Rot(a.m, "wingR", new Vector3(0, 0, -fl));
            if (UnityEngine.Random.value < 0.4f) Fx.Spark(a.pos + UnityEngine.Random.insideUnitSphere * 2f, Vector3.zero, 0.15f, new Color(0.7f, 0.85f, 1f), 0.4f);
            if (a.cd <= 0f && a.t > 0.5f)
            {
                var e = Aim.Nearest(c, 35f); if (e != null) { a.cd = 0.38f; Vector3 p = e.pos + UnityEngine.Random.insideUnitSphere * 1.5f; p.y = e.pos.y; Fx.Bolt(p + Vector3.up * 45f, p + Vector3.up * 0.5f, new Color(0.8f, 0.9f, 1f, 1f), 0.7f, 0.25f, 1.6f, 14); Fx.Bolt(p + Vector3.up * 45f, p + Vector3.up * 0.5f, new Color(0.3f, 0.5f, 1f, 0.8f), 1.6f, 0.3f, 1.6f, 14); Fx.Explosion(p + Vector3.up * 0.5f, 3f, new Color(0.7f, 0.85f, 1f)); World.Burst(p, 3.5f, 30f * Skills.DmgMul(), 4f, new Color(0.8f, 0.9f, 1f)); Sfx.Zap(p, 0.7f); }
            }
        }
        static void Mahoraga(Ally a, float dt, Vector3 pl)
        {
            var e = Aim.Nearest(a.pos, 60f);
            if (e != null)
            {
                e.slow = Mathf.Max(e.slow, 0.3f); float d = Vector3.Distance(new Vector3(e.pos.x, 0, e.pos.z), new Vector3(a.pos.x, 0, a.pos.z));
                if (d > 4.2f + e.Radius) Step(a, e.pos, 11f, dt); else { Face(a, e.pos, dt); if (a.cd <= 0f) { a.cd = 1.15f; a.hits++; a.adapt = Mathf.Min(2.6f, 1f + a.hits * 0.12f); Vector3 c = a.pos + Quaternion.Euler(0, a.yaw, 0) * Vector3.forward * 3.5f; Fx.Explosion(c, 4.5f, new Color(1f, 0.9f, 0.5f)); World.Burst(c, 6f, 55f * a.adapt * Skills.DmgMul(), 12f, new Color(1f, 0.9f, 0.5f)); Fx.Popup_(a.pos + Vector3.up * 8f, "Адаптация ×" + a.adapt.ToString("0.0"), new Color(1f, 0.9f, 0.5f), 1.1f); Fx.HitStop(0.06f); } }
            }
            else { float d = Vector3.Distance(a.pos, pl); if (d > 6f) Step(a, pl, 11f, dt); }
            float sa = Mathf.Sin(a.t * 6f) * 25f * (a.moving ? 1f : 0.1f); Rot(a.m, "legL", new Vector3(sa, 0, 0)); Rot(a.m, "legR", new Vector3(-sa, 0, 0));
            float sl = a.cd > 0.6f ? -110f + (1.15f - a.cd) * 300f : -20f; Rot(a.m, "armR", new Vector3(sl, 0, -6f)); Rot(a.m, "foreR", new Vector3(-20f, 0, 0)); Rot(a.m, "armL", new Vector3(-sa * 0.7f, 0, 6f)); Rot(a.m, "wheel", new Vector3(0, a.t * 140f, 0));
            if (UnityEngine.Random.value < 0.3f) Fx.Spark(a.pos + Vector3.up * (5.5f + UnityEngine.Random.value), UnityEngine.Random.insideUnitSphere * 2f, 0.2f, new Color(1f, 0.85f, 0.4f), 0.6f);
        }
        static void Clone(Ally a, float dt, Vector3 pl)
        {
            var e = Aim.Nearest(a.pos, 45f);
            if (e != null)
            {
                float d = Vector3.Distance(new Vector3(e.pos.x, 0, e.pos.z), new Vector3(a.pos.x, 0, a.pos.z));
                if (d > 2.2f + e.Radius) Step(a, e.pos, 11f, dt); else { Face(a, e.pos, dt); if (a.cd <= 0f) { a.cd = 0.65f; a.hits++; Vector3 dir = e.pos - a.pos; if (a.hits % 4 == 0) { World.Damage(e, 85f * Skills.DmgMul(), dir, 10f, true, new Color(1f, 0.2f, 0.15f)); Fx.Bolt(a.pos + Vector3.up * 1.4f, e.Center, new Color(1f, 0.1f, 0.1f), 0.35f, 0.3f, 0.8f); Fx.Bolt(a.pos + Vector3.up * 1.4f, e.Center, new Color(0.02f, 0f, 0.02f, 1f), 0.15f, 0.3f, 0.8f); Sfx.Flash(a.pos, 0.6f); Fx.Shake(0.2f, 0.2f); } else { World.Damage(e, 18f * Skills.DmgMul(), dir, 4f); Sfx.Hit(a.pos, 0.4f); } } }
            }
            else { float d = Vector3.Distance(a.pos, pl); if (d > 4f) Step(a, pl, 12f, dt); }
            float sa = Mathf.Sin(a.t * 10f) * 32f * (a.moving ? 1f : 0.05f); Rot(a.m, "legL", new Vector3(sa, 0, 0)); Rot(a.m, "legR", new Vector3(-sa, 0, 0));
            float p = a.cd > 0.3f ? -80f : -10f; Rot(a.m, "armR", new Vector3(a.moving ? -sa : p, 0, -5f)); Rot(a.m, "armL", new Vector3(a.moving ? sa : -10f, 0, 5f)); Rot(a.m, "torso", new Vector3(a.moving ? 10f : 0f, 0, 0));
        }
    }
}
