// Проклятия и боссы: определения, спавн, ИИ, анимации.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakJJK
{
    internal static class Enemies
    {
        public static readonly Dictionary<string, EDef> Defs = new Dictionary<string, EDef>();
        public static readonly string[] BossOrder = { "jogo", "hanami", "mahito", "sukuna" };
        static Color C(float r, float g, float b) { return new Color(r, g, b); }

        static Enemies()
        {
            Add(new EDef { id = "wisp", name = "Проклятие 4 ранга «Глаз»", grade = "4", hp = 30, speed = 6.5f, dmg = 0.04f, hover = 1.5f, radius = 0.6f, cy = 0f, xp = 10, flying = true, make = Models.Wisp, col = C(0.5f, 0.2f, 0.7f) });
            Add(new EDef { id = "hound", name = "Проклятие 3 ранга «Пёс»", grade = "3", hp = 70, speed = 8.5f, dmg = 0.06f, hover = 0f, radius = 0.8f, cy = 0.7f, xp = 25, make = () => Models.Hound(C(0.1f, 0.09f, 0.12f), C(0.5f, 0.1f, 0.1f), C(1f, 0.5f, 0.1f)), col = C(0.9f, 0.4f, 0.1f) });
            Add(new EDef { id = "flyhead", name = "Проклятие 2 ранга «Мухоголовое»", grade = "2", hp = 120, speed = 5f, dmg = 0.05f, hover = 2.3f, radius = 0.8f, cy = 0f, xp = 45, flying = true, make = Models.Flyhead, col = C(0.9f, 0.3f, 0.1f) });
            Add(new EDef { id = "brute", name = "Проклятие 1 ранга «Громила»", grade = "1", hp = 400, speed = 4.8f, dmg = 0.12f, hover = 0f, radius = 1.3f, cy = 1.6f, xp = 90, make = Models.Brute, col = C(1f, 0.45f, 0.1f) });
            Add(new EDef { id = "jogo", name = "Дзёго", grade = "Особый", hp = 1800, speed = 6.5f, dmg = 0.1f, hover = 2.0f, radius = 1.2f, cy = 2.0f, xp = 700, boss = true, flying = true, make = Models.Jogo, col = C(1f, 0.4f, 0.05f) });
            Add(new EDef { id = "hanami", name = "Ханами", grade = "Особый", hp = 2200, speed = 3.6f, dmg = 0.1f, hover = 0f, radius = 1.3f, cy = 2.2f, xp = 850, boss = true, make = Models.Hanami, col = C(0.4f, 0.8f, 0.3f) });
            Add(new EDef { id = "mahito", name = "Махито", grade = "Особый", hp = 2000, speed = 11f, dmg = 0.1f, hover = 0f, radius = 0.8f, cy = 1.3f, xp = 800, boss = true, make = Models.Mahito, col = C(0.6f, 0.5f, 0.9f) });
            Add(new EDef { id = "sukuna", name = "Рёмен Сукуна", grade = "Особый", hp = 4000, speed = 9.5f, dmg = 0.12f, hover = 0f, radius = 0.9f, cy = 1.4f, xp = 1500, boss = true, make = Models.Sukuna, col = C(1f, 0.1f, 0.1f) });
            Add(new EDef { id = "gojo", name = "Сатору Годзё", grade = "Особый", hp = 4500, speed = 7.5f, dmg = 0.12f, hover = 1f, radius = 0.9f, cy = 1.4f, xp = 1500, boss = true, make = Models.Gojo, col = C(0.4f, 0.7f, 1f) });
        }
        static void Add(EDef d) { Defs[d.id] = d; }

        public static Enemy Boss { get { foreach (var e in World.E) if (e.d.boss && !e.dead) return e; return null; } }
        public static int Alive { get { int n = 0; foreach (var e in World.E) if (!e.dead && !e.dummy) n++; return n; } }

        public static Enemy Spawn(string id, Vector3 pos, float hpMul = 1f, bool dummy = false)
        {
            EDef d; if (!Defs.TryGetValue(id, out d)) return null;
            var e = new Enemy { d = d, spawned = Time.time, seed = UnityEngine.Random.value * 100f, dummy = dummy };
            e.m = d.make(); e.go = e.m.Finish(); e.go.name = "JJK_" + id; e.scale = d.scale;
            e.maxHp = d.hp * hpMul * Plugin.CfgEnemyHp * (dummy ? 50f : 1f); e.hp = e.maxHp;
            bool f; float gy = World.Ground(pos, out f); e.pos = new Vector3(pos.x, gy + d.hover, pos.z); e.groundY = gy; e.cd = 1.2f + UnityEngine.Random.value;
            if (d.boss) { e.cd = 3f; e.busy = 2.6f; e.intro = true; }
            e.go.transform.position = e.pos; e.go.transform.localScale = Vector3.zero; World.E.Add(e);
            Fx.Burst(e.pos + Vector3.up * 0.5f, d.boss ? 40 : 12, d.boss ? 6f : 3f, 0.4f, new Color(0.15f, 0.05f, 0.2f, 0.7f), 1.2f, false, 0.7f); Fx.Ring(e.pos + Vector3.up * 0.3f, Vector3.up, d.col, d.boss ? 10f : 3f, 0.7f, 0.5f); Sfx.Thud(e.pos, 0.5f);
            if (d.boss) Introduce(e);
            return e;
        }

        static void Introduce(Enemy e)
        {
            Hud.Banner(e.d.name, "Проклятый дух особого ранга"); Sfx.Roar(e.pos, 0.9f); Fx.Shake(0.4f, 1.2f); Fx.Flash(new Color(0.1f, 0f, 0.15f), 0.5f, 1.5f);
            string line; switch (e.d.id) { case "jogo": line = "Погляди, как горит человеческий страх!"; break; case "hanami": line = "Земля помнит всё. Она не простит."; break; case "mahito": line = "Душа и тело — просто глина. Давай поиграем!"; break; case "sukuna": line = "Знай своё место, червь."; break; default: line = "Во всём небе и на земле я один — достойный."; break; }
            Hud.Say(e.d.name, line);
        }

        public static void Clear() { World.ClearAll(); }

        // ---------------- тик ----------------
        public static void Tick(float dt)
        {
            Vector3 pl = Pl.Pos; bool alive = Pl.Alive;
            for (int i = World.E.Count - 1; i >= 0; i--)
            {
                var e = World.E[i]; if (e.dead || e.go == null) continue;
                e.t += dt; e.cd -= dt; if (e.flash > 0f) e.flash = Mathf.Max(0f, e.flash - dt * 6f); e.slow = Mathf.Max(0f, e.slow - dt);
                if (e.burn > 0f) { e.burn -= dt; if (UnityEngine.Random.value < dt * 5f) { World.DamageRaw(e, 3f * Skills.DmgMul()); Fx.Puff(e.Center, Vector3.up * 1.5f, 0.5f, new Color(1f, 0.5f, 0.1f, 0.6f), 0.5f); if (e.dead) continue; } }
                float age = Time.time - e.spawned; bool stunned = e.stun > 0f; if (stunned) e.stun -= dt;
                Vector3 to = pl - e.pos; to.y = 0f; float dist = to.magnitude; e.moving = false;
                if (age > 0.7f && !e.dummy && alive && !stunned) { try { AI(e, dt, pl, dist); } catch (Exception ex) { Plugin.Log("ИИ " + e.d.id, ex); e.cd = 1f; } }
                if (e.dead) continue;
                // рывок
                if (e.dashT > 0f) { e.dashT -= dt; e.pos += e.dashDir * (e.d.boss ? 28f : 16f) * dt; }
                // отбрасывание
                if (e.knock.sqrMagnitude > 0.01f) { e.pos += e.knock * dt; e.knock = Vector3.Lerp(e.knock, Vector3.zero, dt * 5f); }
                // высота
                if (!e.air)
                {
                    bool f; float gy = World.Ground(e.pos, out f); float want = (f ? gy : e.pos.y - e.d.hover) + e.d.hover; if (e.d.flying) want += Mathf.Sin(e.t * 2f + e.seed) * 0.25f;
                    float dy = want - e.pos.y; float maxUp = 9f * dt, maxDn = 22f * dt; e.pos.y += Mathf.Clamp(dy, -maxDn, maxUp * (e.d.boss ? 1.5f : 1f)); e.groundY = f ? gy : e.groundY;
                }
                // расталкивание
                foreach (var o in World.E) { if (o == e || o.dead) continue; Vector3 dd = e.pos - o.pos; dd.y = 0f; float m = (e.Radius + o.Radius) * 0.9f; float dl = dd.magnitude; if (dl < m && dl > 0.01f) e.pos += dd / dl * (m - dl) * 0.5f * Mathf.Min(1f, dt * 8f); }
                // далеко ушёл — вернуть
                if (!e.d.boss && dist > 130f) { float a = UnityEngine.Random.value * 6.283f; e.pos = pl + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 40f; bool f2; e.pos.y = World.Ground(e.pos, out f2) + e.d.hover; }
                // трансформ и анимация
                float yawT = dist > 0.5f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : e.yaw; if (!stunned && e.dashT <= 0f) e.yaw = Mathf.LerpAngle(e.yaw, yawT, dt * (e.d.boss ? 6f : 9f));
                float grow = Mathf.Clamp01(age / 0.7f); grow = 1f - Mathf.Pow(1f - grow, 3f); float sq = 1f + e.flash * 0.1f;
                var tr = e.go.transform; tr.position = e.pos + (age < 0.7f ? Vector3.down * (1f - grow) * 1.2f : Vector3.zero); tr.rotation = Quaternion.Euler(0f, e.yaw, 0f); tr.localScale = new Vector3(sq, 1f / sq, sq) * (e.scale * grow);
                if (dist < 90f) Anim(e, dt, dist);
            }
        }

        // ---------------- движение ----------------
        static void Move(Enemy e, Vector3 target, float speed, float dt)
        {
            Vector3 d = target - e.pos; d.y = 0f; float l = d.magnitude; if (l < 0.05f) return; float sp = speed * (e.slow > 0f ? 0.5f : 1f); e.pos += d / l * Mathf.Min(l, sp * dt); e.moving = true;
        }
        static void Keep(Enemy e, Vector3 pl, float min, float max, float speed, float dt, float strafe = 0f)
        {
            Vector3 to = pl - e.pos; to.y = 0f; float dist = to.magnitude; Vector3 dir = to / Mathf.Max(0.01f, dist);
            if (dist > max) Move(e, pl, speed, dt); else if (dist < min) Move(e, e.pos - dir * 5f, speed * 0.8f, dt);
            if (strafe != 0f) { Vector3 side = Vector3.Cross(Vector3.up, dir) * Mathf.Sign(Mathf.Sin(e.t * 0.5f + e.seed)); e.pos += side * strafe * dt; e.moving = true; }
        }

        // ---------------- ИИ ----------------
        static void AI(Enemy e, float dt, Vector3 pl, float dist)
        {
            switch (e.d.id)
            {
                case "wisp": AiWisp(e, dt, pl, dist); break;
                case "hound": AiHound(e, dt, pl, dist); break;
                case "flyhead": AiFly(e, dt, pl, dist); break;
                case "brute": AiBrute(e, dt, pl, dist); break;
                default: AiBoss(e, dt, pl, dist); break;
            }
        }
        static bool HitsPlayer(Vector3 c, float r) { return Pl.Alive && Vector3.Distance(Pl.Pos, c) < r; }

        static void AiWisp(Enemy e, float dt, Vector3 pl, float dist)
        {
            if (e.st == 0) { Keep(e, pl, 2.2f, 4f, e.d.speed, dt, 1.5f); if (dist < 8f && e.cd <= 0f) { e.st = 1; e.phaseT = 0.5f; Sfx.Screech(e.pos, 0.3f); } }
            else if (e.st == 1) { e.phaseT -= dt; if (e.phaseT <= 0f) { e.st = 2; e.phaseT = 0.45f; e.dashDir = (pl + Vector3.up * 0.3f - e.pos).normalized; e.dashDir.y *= 0.2f; e.dashT = 0.45f; } }
            else
            {
                e.phaseT -= dt; if (HitsPlayer(e.Center, 1.6f)) { Pl.Hurt(e.d.dmg * e.dmgMul); e.st = 0; e.cd = 1.8f; e.dashT = 0f; }
                else if (e.phaseT <= 0f) { e.st = 0; e.cd = 1.6f; }
            }
        }
        static void AiHound(Enemy e, float dt, Vector3 pl, float dist)
        {
            if (e.st == 0) { Move(e, pl, e.d.speed, dt); if (dist < 11f && dist > 3f && e.cd <= 0f) { e.st = 1; e.phaseT = 0.55f; Sfx.Howl(e.pos, 0.4f); } else if (dist <= 3f && e.cd <= 0f) { if (HitsPlayer(e.Center + e.go.transform.forward, 2.2f)) { Pl.Hurt(e.d.dmg * 0.7f * e.dmgMul); Sfx.Slash(e.pos, 0.4f); } e.cd = 1.1f; } }
            else if (e.st == 1) { e.phaseT -= dt; if (e.phaseT <= 0f) { e.st = 2; e.phaseT = 0.6f; e.aux = e.pos; e.aux2 = pl; e.air = true; Sfx.Whoosh(e.pos, 0.4f); } }
            else
            {
                e.phaseT -= dt; float k = 1f - Mathf.Clamp01(e.phaseT / 0.6f); Vector3 p = Vector3.Lerp(e.aux, e.aux2, k); bool f; float gy = World.Ground(p, out f); p.y = Mathf.Lerp(e.aux.y, f ? gy : e.aux2.y, k) + Mathf.Sin(k * Mathf.PI) * 2.6f; e.pos = p;
                if (HitsPlayer(e.Center, 1.8f)) { Pl.Hurt(e.d.dmg * e.dmgMul); Pl.Push((pl - e.pos).normalized * 3f + Vector3.up); e.phaseT = 0f; }
                if (e.phaseT <= 0f) { e.st = 0; e.cd = 2f; e.air = false; Fx.Burst(e.pos, 6, 3f, 0.4f, new Color(0.3f, 0.25f, 0.2f, 0.5f), 0.7f, false, 0.4f); }
            }
        }
        static void AiFly(Enemy e, float dt, Vector3 pl, float dist)
        {
            Keep(e, pl, 11f, 17f, e.d.speed, dt, 2.5f);
            if (e.st == 0 && e.cd <= 0f && dist < 40f) { e.st = 1; e.phaseT = 0.7f; Sfx.Screech(e.pos, 0.3f); }
            else if (e.st == 1)
            {
                e.phaseT -= dt; if (UnityEngine.Random.value < 0.5f) Fx.Spark(e.Center + e.go.transform.forward * 0.5f, UnityEngine.Random.insideUnitSphere, 0.2f, new Color(1f, 0.3f, 0.1f), 0.3f);
                if (e.phaseT <= 0f) { e.st = 0; e.cd = 2.4f; Vector3 from = e.Center + e.go.transform.forward * 0.6f; var p = World.Shoot(from, (pl - from).normalized, 17f, 0.5f, e.d.dmg * e.dmgMul, new Color(1f, 0.35f, 0.1f), false, 5f); p.expl = 1.8f; p.status = "Poison"; Sfx.Zap(from, 0.4f); }
            }
        }
        static void AiBrute(Enemy e, float dt, Vector3 pl, float dist)
        {
            if (e.st == 0) { Move(e, pl, e.d.speed, dt); if (dist < 6f && e.cd <= 0f) { e.st = 1; e.phaseT = 1f; Vector3 fwd = (pl - e.pos); fwd.y = 0f; fwd.Normalize(); e.aux = e.pos + fwd * 3.2f; World.Circle(e.aux, 4.6f, 1f, e.d.dmg * e.dmgMul, new Color(1f, 0.4f, 0.1f), "Injury", 7f); Sfx.Roar(e.pos, 0.4f); } }
            else { e.phaseT -= dt; if (e.phaseT <= 0f) { e.st = 0; e.cd = 2.4f; Fx.Shake(0.3f, 0.3f); } }
        }

        // ---------------- боссы ----------------
        static void AiBoss(Enemy e, float dt, Vector3 pl, float dist)
        {
            if (e.phase == 0 && e.hp < e.maxHp * 0.5f) Phase2(e);
            if (e.busy > 0f) { e.busy -= dt; if (e.intro && e.busy <= 0f) e.intro = false; BossBusyMove(e, dt, pl); return; }
            switch (e.d.id)
            {
                case "jogo": Keep(e, pl, 13f, 22f, e.d.speed, dt, 3f); break;
                case "hanami": Keep(e, pl, 8f, 13f, e.d.speed, dt, 1f); break;
                case "mahito": Keep(e, pl, 3f, 8f, e.d.speed, dt, 3f); break;
                case "sukuna": Keep(e, pl, 5f, 9f, e.d.speed, dt, 2f); break;
                default: Keep(e, pl, 10f, 18f, e.d.speed, dt, 3f); break;
            }
            if (e.cd <= 0f) { Attack(e, pl, dist); }
        }
        static void BossBusyMove(Enemy e, float dt, Vector3 pl) { }

        static void Phase2(Enemy e)
        {
            e.phase = 1; e.dmgMul *= 1.2f; e.busy = 1.8f; e.cd = 0.5f; Fx.Explosion(e.Center, 8f, e.d.col, true); Fx.Flash(e.d.col, 0.45f, 2f); Sfx.Roar(e.pos, 1f); Fx.Shake(0.4f, 0.8f);
            string l; switch (e.d.id) { case "jogo": l = "Хватит игр! Сгори до пепла!"; break; case "hanami": l = "Лес проснулся!"; break; case "mahito": l = "А теперь — по-настоящему!"; break; case "sukuna": l = "Забавно. Я начну всерьёз."; break; default: l = "Ладно. Покажу тебе кое-что."; break; }
            Hud.Say(e.d.name, l); Hud.Banner("ФАЗА 2", e.d.name);
            if (e.d.id == "hanami" || e.d.id == "mahito") { SpawnMinions(e, 2); }
        }
        static void SpawnMinions(Enemy e, int n)
        {
            for (int i = 0; i < n; i++) { float a = UnityEngine.Random.value * 6.283f; var m = Spawn("wisp", e.pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 5f, 0.8f); if (m != null) { m.spawnedMinion = true; } }
        }
        static Vector3 Lead(Vector3 from, Vector3 pl, float speed) { float t = Mathf.Clamp(Vector3.Distance(from, pl) / speed, 0f, 1.2f); Vector3 v = Pl.Pos - lastPl; return pl + new Vector3(v.x, 0f, v.z) / Mathf.Max(0.016f, Time.deltaTime) * t * 0.6f; }
        static Vector3 lastPl;
        static void Fireball(Enemy e, Vector3 from, Vector3 pl, float speed, float dmg, Color col, float expl, float r = 0.6f, float spread = 0f, string status = null, float homing = 0f)
        {
            if (e.dead) return; Vector3 tgt = pl + UnityEngine.Random.insideUnitSphere * spread; Vector3 dir = (tgt - from).normalized; var p = World.Shoot(from, dir, speed, r, dmg * e.dmgMul, col, false, 6f); p.expl = expl; p.status = status; p.homing = homing; Sfx.Zap(from, 0.5f);
        }
        static void Say(Enemy e, string t) { Hud.Say(e.d.name, t); }
        static void Wind(Enemy e, Color c, float r, float t) { Fx.Orb(e.Center + e.go.transform.forward * 1.2f, r, new Color(c.r, c.g, c.b, 0.6f), t, true); Sfx.Charge(e.pos, 0.5f); }
        static bool PAlive(Enemy e) { return !e.dead && Pl.Alive; }

        static void Attack(Enemy e, Vector3 pl, float dist)
        {
            lastPl = pl; int n = e.atk++; int pick = UnityEngine.Random.Range(0, 100);
            switch (e.d.id)
            {
                case "jogo": AtkJogo(e, pl, n); break;
                case "hanami": AtkHanami(e, pl, n); break;
                case "mahito": AtkMahito(e, pl, dist, n); break;
                case "sukuna": AtkSukuna(e, pl, dist, n); break;
                default: AtkGojo(e, pl, dist, n); break;
            }
            e.cd = Mathf.Max(0.4f, e.busy + (e.phase == 0 ? 0.8f : 0.3f));
        }

        static void AtkJogo(Enemy e, Vector3 pl, int n)
        {
            int k = n % 3; if (e.phase == 1 && n % 4 == 3) k = 3; Color fire = new Color(1f, 0.45f, 0.08f);
            if (k == 0)
            {
                e.busy = 2.4f; Wind(e, fire, 1.2f, 0.6f); Say(e, "Пеплом станешь!");
                for (int i = 0; i < (e.phase == 1 ? 7 : 5); i++) { int j = i; World.After(0.6f + i * 0.22f, () => { if (PAlive(e)) Fireball(e, e.Center + Vector3.up * 0.5f, Pl.Pos, 24f, 0.07f, fire, 2.6f, 0.6f, 1.2f); }); }
            }
            else if (k == 1)
            {
                e.busy = 3.2f; Say(e, "Падайте, метеоры!"); int cnt = e.phase == 1 ? 10 : 6; Sfx.Roar(e.pos, 0.5f);
                for (int i = 0; i < cnt; i++) World.After(0.5f + i * 0.3f, () => { if (PAlive(e)) { Vector3 p = Pl.Pos + (UnityEngine.Random.value < 0.4f ? Vector3.zero : new Vector3(UnityEngine.Random.Range(-7f, 7f), 0f, UnityEngine.Random.Range(-7f, 7f))); World.Circle(p, 4.2f, 1.2f, 0.1f * e.dmgMul, fire, "Injury", 8f, true); } });
            }
            else if (k == 2)
            {
                e.busy = 2.8f; Say(e, "Вулкан!"); Fx.Orb(e.pos + Vector3.up * 2f, 4f, new Color(1f, 0.4f, 0.1f, 0.5f), 1.7f, true); Sfx.Charge(e.pos, 0.7f);
                World.Circle(e.pos, 13f, 1.7f, 0.16f * e.dmgMul, new Color(1f, 0.3f, 0.05f), "Injury", 11f, false, 0f, z => { for (int i = 0; i < 12; i++) { float a = i / 12f * 6.283f; Vector3 p = z.pos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * UnityEngine.Random.Range(3f, 12f); Fx.Burst(p, 10, 9f, 0.5f, fire, 1f, true, 1f); } });
            }
            else
            {
                e.busy = 4.2f; Say(e, "МАКСИМУМ: МЕТЕОР!"); Wind(e, fire, 3f, 3f); Hud.Banner("МАКСИМУМ: МЕТЕОР", "Дзёго");
                World.Circle(pl, 11f, 3.2f, 0.32f * e.dmgMul, new Color(1f, 0.25f, 0.02f), "Injury", 14f, true, 0f, z => { Fx.Flash(new Color(1f, 0.5f, 0.1f), 0.6f, 2f); Fx.Shake(0.6f, 0.8f); });
            }
        }

        static void AtkHanami(Enemy e, Vector3 pl, int n)
        {
            int k = n % 4; if (e.phase == 0 && k == 3) k = 0; Color gr = new Color(0.45f, 0.85f, 0.25f);
            if (k == 0)
            {
                e.busy = 2.5f; Say(e, "Корни!"); Wind(e, gr, 1f, 0.7f); Vector3 dir = pl - e.pos; dir.y = 0f; dir.Normalize(); int c = e.phase == 1 ? 2 : 1;
                for (int i = -c; i <= c; i++) { Vector3 d = Quaternion.Euler(0, i * 17f, 0) * dir; World.LineZone(e.pos, e.pos + d * 45f, 2.8f, 1.1f, 0.1f * e.dmgMul, new Color(0.5f, 0.7f, 0.2f), "Injury", 7f); }
            }
            else if (k == 1)
            {
                e.busy = 2.4f; Say(e, "Семена проклятия!"); Wind(e, new Color(0.9f, 0.5f, 0.7f), 1f, 0.6f);
                int c = e.phase == 1 ? 6 : 4; for (int i = 0; i < c; i++) World.After(0.6f + i * 0.25f, () => { if (PAlive(e)) Fireball(e, e.Center + Vector3.up, Pl.Pos, 13f, 0.06f, new Color(0.9f, 0.45f, 0.7f), 2f, 0.5f, 3f, "Poison", 1.5f); });
            }
            else if (k == 2)
            {
                e.busy = 2.2f; Say(e, "Цветочное поле."); Sfx.Charge(e.pos, 0.5f);
                var z = World.Circle(pl, 9.5f, 1.2f, 0.08f * e.dmgMul, new Color(0.95f, 0.45f, 0.65f), "Poison", 0f, false, 8f, zz => { Fx.Burst(zz.pos + Vector3.up * 0.3f, 24, 5f, 0.5f, new Color(1f, 0.6f, 0.75f), 1.2f, false, 1.5f); });
                z.slowIn = true; z.onTick = zz => { Vector3 p = zz.pos + UnityEngine.Random.insideUnitSphere * zz.r * 0.9f; p.y = zz.pos.y + 0.3f; Fx.Puff(p, Vector3.up * 1.4f, 0.4f, new Color(1f, 0.6f, 0.75f, 0.7f), 1.5f); };
            }
            else { e.busy = 2.2f; Say(e, "Ко мне, дети леса!"); SpawnMinions(e, 3); }
        }

        static void Dash(Enemy e, Vector3 to, float time) { Vector3 d = to - e.pos; d.y = 0f; e.dashDir = d.normalized; e.dashT = time; Sfx.Whoosh(e.pos, 0.5f); }

        static void AtkMahito(Enemy e, Vector3 pl, float dist, int n)
        {
            int k = n % 4; if (e.phase == 1 && n % 5 == 4) k = 4; Color col = new Color(0.65f, 0.55f, 1f);
            if (k == 0)
            {
                e.busy = 1.9f; Say(e, "Перекройка души!"); World.Circle(pl, 3.6f, 0.6f, 0.13f * e.dmgMul, col, "Injury", 6f); World.After(0.25f, () => { if (!e.dead) Dash(e, Pl.Pos, 0.3f); });
            }
            else if (k == 1)
            {
                e.busy = 2.3f; Say(e, "Хи-хи-хи!"); for (int i = 0; i < 3; i++) World.After(0.2f + i * 0.5f, () => { if (!PAlive(e)) return; Vector3 f = Pl.Pos - e.pos; f.y = 0f; f.Normalize(); Dash(e, e.pos + f * 3f, 0.12f); World.Circle(e.pos + f * 3.2f, 3.3f, 0.35f, 0.07f * e.dmgMul, col, "Injury", 4f); Sfx.Slash(e.pos, 0.5f); });
            }
            else if (k == 2) { e.busy = 1.9f; Say(e, "Отторжение тела!"); World.Circle(e.pos, 6.8f, 0.9f, 0.12f * e.dmgMul, col, "Injury", 11f, false, 0f, z => { for (int i = 0; i < 10; i++) { float a = i / 10f * 6.283f; Fx.Bolt(z.pos + Vector3.up * 1f, z.pos + new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)) * 6.5f, new Color(0.7f, 0.7f, 1f, 0.9f), 0.12f, 0.3f); } }); }
            else if (k == 3)
            {
                e.busy = 2f; Say(e, "Слишком медленно."); Fx.Burst(e.Center, 20, 4f, 0.5f, col, 0.5f);
                World.After(0.45f, () => { if (!PAlive(e)) return; Vector3 b = Pl.Pos - Pl.Fwd * 3.2f; bool f; b.y = World.Ground(b, out f) + e.d.hover; e.pos = b; Fx.Burst(e.Center, 24, 6f, 0.5f, col, 0.6f); Sfx.Whoosh(e.pos, 0.6f); World.Circle(Pl.Pos, 3.4f, 0.5f, 0.12f * e.dmgMul, col, "Injury", 6f); });
            }
            else { e.busy = 2.4f; Say(e, "Множественность душ!"); SpawnMinions(e, 3); }
        }

        static void AtkSukuna(Enemy e, Vector3 pl, float dist, int n)
        {
            int k = n % 4; Color red = new Color(1f, 0.15f, 0.12f);
            if (e.phase == 1 && e.aux2.x < 1f && e.hp < e.maxHp * 0.55f) { e.aux2 = new Vector3(1f, 0f, 0f); k = 9; }
            else if (e.phase == 1 && e.aux2.x < 2f && e.aux2.x >= 1f && e.hp < e.maxHp * 0.2f) { e.aux2 = new Vector3(2f, 0f, 0f); k = 9; }
            if (k == 3 && e.phase == 0) k = 0;
            if (k == 0)
            {
                e.busy = 2f; Say(e, "Рассечение."); int c = e.phase == 1 ? 5 : 3; Wind(e, red, 0.8f, 0.4f);
                for (int i = 0; i < c; i++) { int j = i; World.After(0.4f + i * 0.14f, () => { if (!PAlive(e)) return; Vector3 from = e.Center; Vector3 dir = Pl.Pos - from; dir = Quaternion.Euler(0, (j - (c - 1) / 2f) * 9f, UnityEngine.Random.Range(-8f, 8f)) * dir; var p = World.Shoot(from, dir, 44f, 1.3f, 0.07f * e.dmgMul, new Color(1f, 0.85f, 0.85f), false, 2.5f, true); p.knock = 3f; Sfx.Slash(from, 0.6f); }); }
            }
            else if (k == 1 || k == 2)
            {
                e.busy = 2f; Say(e, k == 1 ? "Разруб." : "Умри."); World.After(0.3f, () => { if (!PAlive(e)) return; Dash(e, Pl.Pos, 0.28f); });
                World.After(0.65f, () => { if (!PAlive(e)) return; Vector3 f = Pl.Pos - e.pos; f.y = 0f; f.Normalize(); World.Circle(e.pos + f * 2.2f, 4.5f, 0.35f, 0.14f * e.dmgMul, red, "Injury", 9f); World.Circle(e.pos + f * 6f, 3.5f, 0.45f, 0.1f * e.dmgMul, red, "Injury", 6f); });
            }
            else if (k == 3)
            {
                e.busy = 3.4f; Say(e, "Пылай. Фуга."); Wind(e, new Color(1f, 0.5f, 0.1f), 2f, 2f); Hud.Banner("ФУГА", "Огненная стрела");
                Vector3 dir = pl - e.Center; dir.y = 0f; dir.Normalize(); Vector3 a = e.pos, b = e.pos + dir * 70f;
                World.After(0.2f, () => { if (!e.dead) World.LineZone(a, b, 5f, 2f, 0.26f * e.dmgMul, new Color(1f, 0.45f, 0.08f), "Injury", 12f, z => { Fx.Explosion(z.b + Vector3.up, 10f, new Color(1f, 0.5f, 0.1f), true); Fx.Flash(new Color(1f, 0.5f, 0.1f), 0.5f, 2f); }); });
            }
            else
            {
                e.busy = 11f; e.cd = 0f; Say(e, "Расширение домена: Храм Злобы."); Domains.Open(e.pos, 36f, 0, 10f, e.d.col);
                float end = Time.time + 10f; Vector3 c0 = e.pos; var rnd = new System.Random();
                for (float t = 0.8f; t < 10f; t += 0.33f) { float tt = t; World.After(tt, () => { if (e.dead || !Pl.Alive) return; Vector3 p = Pl.Pos + new Vector3(UnityEngine.Random.Range(-11f, 11f), 0f, UnityEngine.Random.Range(-11f, 11f)); World.Circle(p, 2.4f, 0.55f, 0.055f * e.dmgMul, red, "Injury", 3f); }); }
                for (float t = 1.5f; t < 10f; t += 1.3f) World.After(t, () => { if (e.dead || !Pl.Alive) return; World.Circle(Pl.Pos, 3.4f, 0.6f, 0.09f * e.dmgMul, new Color(1f, 0.3f, 0.2f), "Injury", 5f); });
            }
        }

        static void AtkGojo(Enemy e, Vector3 pl, float dist, int n)
        {
            int k = n % 4; if (e.phase == 1 && n % 5 == 4) k = 9; Color blue = new Color(0.3f, 0.6f, 1f), red = new Color(1f, 0.2f, 0.2f), pur = new Color(0.7f, 0.3f, 1f);
            if (k == 0)
            {
                e.busy = 2.8f; Say(e, "Приказ: Синий."); Wind(e, blue, 1.4f, 0.8f);
                for (int i = 0; i < 14; i++) World.After(0.7f + i * 0.1f, () => { if (!PAlive(e)) return; Vector3 d = e.Center - Pl.Pos; if (d.magnitude < 40f) Pl.Push(d.normalized * 0.9f + Vector3.up * 0.1f); if (UnityEngine.Random.value < 0.4f) Fx.Swirl(e.Center, 3f, 6, blue, 0.35f, 0f); });
                World.After(2.2f, () => { if (e.dead) return; Fx.Explosion(e.Center, 6f, blue); if (Pl.Alive && Vector3.Distance(Pl.Pos, e.Center) < 6f && Pl.Hurt(0.12f * e.dmgMul, "Injury", 0.5f)) Pl.Push((Pl.Pos - e.Center).normalized * 6f); });
            }
            else if (k == 1)
            {
                e.busy = 2f; Say(e, "Реверс: Красный."); Wind(e, red, 1.2f, 0.7f); World.After(0.8f, () => { if (!PAlive(e)) return; var p = World.Shoot(e.Center + Vector3.up * 0.3f, (Pl.Pos - e.Center), 32f, 1.1f, 0.13f * e.dmgMul, red, false, 4f); p.expl = 4f; p.knock = 14f; Sfx.Boom(e.pos, 0.4f); });
            }
            else if (k == 2)
            {
                e.busy = 3.6f; Say(e, "Тайное искусство: Фиолетовый."); Hud.Banner("ПУСТОТА: ФИОЛЕТОВЫЙ", "Сатору Годзё"); Wind(e, pur, 2.2f, 2.2f); Fx.Orb(e.Center + Vector3.left * 1.2f, 0.7f, new Color(0.3f, 0.6f, 1f, 0.7f), 1.6f, true); Fx.Orb(e.Center + Vector3.right * 1.2f, 0.7f, new Color(1f, 0.2f, 0.2f, 0.7f), 1.6f, true);
                Vector3 dir = pl - e.Center; dir.y = 0f; dir.Normalize(); Vector3 a = e.pos, b = e.pos + dir * 75f;
                World.After(0.2f, () => { if (!e.dead) World.LineZone(a, b, 7f, 2.2f, 0.3f * e.dmgMul, pur, "Injury", 14f, z => { Fx.Explosion(z.b + Vector3.up, 12f, pur, true); Fx.Flash(Color.white, 0.6f, 2f); }); });
            }
            else if (k == 3) { e.busy = 1.8f; Say(e, "Слишком медленно."); Fx.Burst(e.Center, 20, 4f, 0.4f, blue, 0.5f); World.After(0.4f, () => { if (e.dead) return; float a = UnityEngine.Random.value * 6.283f; Vector3 b = Pl.Pos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 9f; bool f; b.y = World.Ground(b, out f) + e.d.hover; e.pos = b; Fx.Burst(e.Center, 20, 5f, 0.4f, blue, 0.5f); Sfx.Whoosh(e.pos, 0.5f); Fireball(e, e.Center, Pl.Pos, 22f, 0.08f, blue, 3f); }); }
            else
            {
                e.busy = 6f; Say(e, "Расширение домена: Безграничная пустота."); Domains.Open(Pl.Pos + Vector3.up * 1f, 16f, 1, 4.5f, Color.white);
                Pl.Stun(4f); for (int i = 0; i < 5; i++) World.After(0.8f + i * 0.7f, () => { if (!e.dead && Pl.Alive) { Pl.Hurt(0.05f * e.dmgMul, "Injury", 0.1f); Fx.Shake(0.2f, 0.2f); } });
            }
        }

        // ---------------- анимации ----------------
        static void Rot(ModelB m, string part, Vector3 euler) { Transform t; if (m.parts.TryGetValue(part, out t)) t.localRotation = Quaternion.Euler(euler); }
        static void Anim(Enemy e, float dt, float dist)
        {
            var m = e.m; float t = e.t + e.seed; float mv = e.moving ? 1f : 0.15f; float wind = (e.st == 1 || (e.d.boss && e.busy > 0f)) ? 1f : 0f;
            switch (e.d.id)
            {
                case "wisp":
                    Rot(m, "body", new Vector3(Mathf.Sin(t * 2f) * 6f, 0f, Mathf.Sin(t * 1.7f) * 6f)); for (int i = 0; i < 6; i++) Rot(m, "tent" + i, new Vector3(Mathf.Sin(t * 5f + i) * 24f, 0f, Mathf.Cos(t * 4.3f + i * 1.3f) * 24f));
                    if (e.st == 1) m.T("body").localScale = Vector3.one * (1f - 0.25f * Mathf.Sin(Mathf.Clamp01((0.5f - e.phaseT) / 0.5f) * 1.5f)); else m.T("body").localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t * 6f));
                    break;
                case "hound":
                    float sw = Mathf.Sin(t * 12f) * 35f * mv; Rot(m, "legFL", new Vector3(sw, 0, 0)); Rot(m, "legBR", new Vector3(sw, 0, 0)); Rot(m, "legFR", new Vector3(-sw, 0, 0)); Rot(m, "legBL", new Vector3(-sw, 0, 0));
                    Rot(m, "jaw", new Vector3((e.st == 1 ? 40f : 6f + Mathf.Sin(t * 8f) * 5f * mv), 0, 0)); Rot(m, "body", new Vector3(e.st == 1 ? 18f : (e.st == 2 ? -14f : Mathf.Sin(t * 12f) * 3f * mv), 0, 0)); break;
                case "flyhead":
                    for (int w = 0; w < 2; w++) { float fl = Mathf.Sin(t * 70f + w) * 38f; Rot(m, "wingL" + w, new Vector3(0, 0, fl)); Rot(m, "wingR" + w, new Vector3(0, 0, -fl)); }
                    Rot(m, "head", new Vector3(Mathf.Sin(t * 3f) * 8f + (e.st == 1 ? -25f : 0f), Mathf.Sin(t * 1.5f) * 12f, 0)); Rot(m, "body", new Vector3(e.moving ? 12f : 0f, 0, 0)); break;
                case "brute":
                    float sa = Mathf.Sin(t * 5f) * 18f * mv; Rot(m, "legL", new Vector3(sa, 0, 0)); Rot(m, "legR", new Vector3(-sa, 0, 0));
                    if (e.st == 1) { float k = Mathf.Clamp01((1f - e.phaseT) / 0.85f); Rot(m, "armL", new Vector3(-170f * k, 0, 0)); Rot(m, "armR", new Vector3(-170f * k, 0, 0)); }
                    else { Rot(m, "armL", new Vector3(-sa * 1.2f - 5f, 0, 4f)); Rot(m, "armR", new Vector3(sa * 1.2f - 5f, 0, -4f)); }
                    Rot(m, "body", new Vector3(Mathf.Sin(t * 2f) * 2f + (e.st == 1 ? -8f : 0f), 0, 0)); break;
                default: AnimHumanoid(e, t, mv, wind); break;
            }
        }
        static void AnimHumanoid(Enemy e, float t, float mv, float wind)
        {
            var m = e.m; float sa = Mathf.Sin(t * 8f) * 30f * (e.d.flying ? 0.2f : mv); Rot(m, "legL", new Vector3(sa, 0, 0)); Rot(m, "legR", new Vector3(-sa, 0, 0));
            float br = Mathf.Sin(t * 2f) * 3f;
            float up = wind > 0f ? -100f + Mathf.Sin(t * 14f) * 10f : -sa * 0.8f - 4f; float upL = wind > 0f ? -70f + Mathf.Sin(t * 14f + 1f) * 10f : sa * 0.8f - 4f;
            Rot(m, "armR", new Vector3(up, 0, -6f + br)); Rot(m, "armL", new Vector3(upL, 0, 6f - br)); Rot(m, "foreR", new Vector3(wind > 0f ? -30f : -15f, 0, 0)); Rot(m, "foreL", new Vector3(-15f, 0, 0));
            for (int i = 2; i < 6; i++) { Rot(m, "arm" + i, new Vector3(wind > 0f ? -50f : sa * 0.4f - 4f, 0, (i % 2 == 0 ? -1 : 1) * (10f + br))); }
            Rot(m, "torso", new Vector3(e.moving ? 8f : 2f + br * 0.3f, Mathf.Sin(t * 1.4f) * 3f, 0f)); Rot(m, "head", new Vector3(Mathf.Sin(t * 1.1f) * 3f, Mathf.Sin(t * 0.8f) * 6f, 0));
            if (e.d.id == "mahito") { var fr = m.T("foreR"); fr.localScale = new Vector3(1f, 1f, 1f); }
            if (e.d.id == "jogo" && UnityEngine.Random.value < 0.5f) Fx.Puff(e.pos + Vector3.up * 3.7f * e.scale + UnityEngine.Random.insideUnitSphere * 0.4f, Vector3.up * 2f, 0.5f, new Color(1f, 0.45f, 0.1f, 0.6f), 0.7f);
            if (e.d.id == "sukuna" && wind > 0f && UnityEngine.Random.value < 0.4f) Fx.Spark(e.Center + UnityEngine.Random.insideUnitSphere, Vector3.up, 0.15f, new Color(1f, 0.2f, 0.15f), 0.5f);
            if (e.d.id == "gojo" && UnityEngine.Random.value < 0.25f) Fx.Spark(e.Center + UnityEngine.Random.insideUnitSphere * 1.3f, Vector3.zero, 0.12f, new Color(0.5f, 0.8f, 1f), 0.6f);
        }
    }
}
