// Боевой мир мода: враги, снаряды, зоны-ловушки, прицеливание, урон. ИИ врагов — в Enemies.cs.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakJJK
{
    internal sealed class EDef
    {
        public string id, name, grade; public float hp, speed, dmg, scale = 1f, hover, radius = 0.8f, cy = 1f, xp; public bool boss, flying; public Func<ModelB> make; public Color col = Color.white;
    }

    internal sealed class Enemy
    {
        public EDef d; public GameObject go; public ModelB m; public Vector3 pos, knock; public float hp, maxHp, scale = 1f, flash, stun, slow, burn, t, cd, busy, yaw, groundY, spawned, dmgMul = 1f, seed, phaseT; public int st, phase, atk; public bool dead, dummy, spawnedMinion, air, moving, intro; public Vector3 aux, aux2; public float dashT; public Vector3 dashDir;
        public Vector3 Center { get { return pos + Vector3.up * d.cy * scale; } }
        public float Radius { get { return d.radius * scale; } }
    }

    internal sealed class Proj
    {
        public Vector3 pos, vel; public float r = 0.5f, dmg, life = 4f, grav, homing, knock = 6f, expl; public bool friendly, pierce, slash, dead; public Color col = Color.white; public GameObject go; public string status; public float statusAmt; public Action<Proj, Vector3, Enemy> onHit; public HashSet<Enemy> hit = new HashSet<Enemy>(); public Enemy target; public float born; public bool noTerrain;
    }

    internal sealed class Zone
    {
        public int shape; public Vector3 pos, a, b; public float r = 3f, delay = 1f, dmg = 0.1f, knock, linger, tick = 0.5f, born, nextTick, widthHalf; public Color col = new Color(1f, 0.2f, 0.1f); public string status = "Injury"; public float statusMul = 1f; public bool meteor, fired, dead, slowIn; public GameObject tele, fill, met; public Action<Zone> onFire, onTick; public Material fillMat; public float slowMul = 1f; public string key;
    }

    internal static class Aim
    {
        public static Vector3 Dir { get { var c = Pl.Cam; return c != null ? c.transform.forward : Vector3.forward; } }
        public static Vector3 Origin { get { var c = Pl.Cam; return c != null ? c.transform.position : Pl.Pos; } }
        public static Vector3 Hand { get { var c = Pl.Cam; if (c == null) return Pl.Pos; return Pl.Pos + c.transform.right * 0.35f + Vector3.up * 0.15f + c.transform.forward * 0.7f; } }
        public static Vector3 Point(float range, out bool hitSomething)
        {
            hitSomething = false; var c = Pl.Cam; if (c == null) return Pl.Pos + Pl.Fwd * range; RaycastHit h;
            // луч от камеры, начиная чуть дальше игрока, чтобы не попасть в самого себя
            Vector3 o = c.transform.position + c.transform.forward * 1.5f;
            if (Physics.Raycast(o, c.transform.forward, out h, range, Pl.Mask, QueryTriggerInteraction.Ignore)) { hitSomething = true; return h.point; }
            return o + c.transform.forward * range;
        }
        public static Vector3 Point(float range) { bool b; return Point(range, out b); }
        /// <summary>Враг ближе всего к центру экрана (в конусе deg) — мягкая помощь при прицеливании.</summary>
        public static Enemy Target(float range, float deg = 14f)
        {
            Enemy best = null; float bs = 1e9f; Vector3 o = Origin, f = Dir;
            foreach (var e in World.E)
            {
                if (e.dead) continue; Vector3 v = e.Center - o; float dist = v.magnitude; if (dist > range) continue; float ang = Vector3.Angle(f, v);
                if (ang > deg + e.Radius * 6f / Mathf.Max(3f, dist) * 10f) continue; float sc = ang * 2f + dist * 0.15f; if (sc < bs) { bs = sc; best = e; }
            }
            return best;
        }
        public static Enemy Nearest(Vector3 from, float range)
        {
            Enemy best = null; float bd = range * range; foreach (var e in World.E) { if (e.dead) continue; float d = (e.Center - from).sqrMagnitude; if (d < bd) { bd = d; best = e; } }
            return best;
        }
    }

    internal static partial class World
    {
        public static readonly List<Enemy> E = new List<Enemy>();
        public static readonly List<Proj> P = new List<Proj>();
        public static readonly List<Zone> Z = new List<Zone>();
        static readonly List<KeyValuePair<float, Action>> timers = new List<KeyValuePair<float, Action>>();
        public static int Kills;

        public static void After(float delay, Action a) { timers.Add(new KeyValuePair<float, Action>(Time.time + delay, a)); }
        public static float Ground(Vector3 p, out bool found)
        {
            RaycastHit h; found = false; if (Physics.Raycast(p + Vector3.up * 8f, Vector3.down, out h, 80f, Pl.Mask, QueryTriggerInteraction.Ignore)) { found = true; return h.point.y; }
            return p.y;
        }
        public static Vector3 OnGround(Vector3 p) { bool f; float y = Ground(p, out f); return new Vector3(p.x, y, p.z); }

        // ---------- урон по врагам ----------
        public static void Damage(Enemy e, float dmg, Vector3 dir, float knock = 5f, bool crit = false, Color? col = null, bool silent = false)
        {
            if (e == null || e.dead || dmg <= 0f) return;
            if (e.d.boss) knock *= 0.3f; if (e.st < 0) return;
            e.hp -= dmg; e.flash = 1f; if (!e.d.boss || dmg > 20f) { Vector3 k = dir; k.y = 0f; e.knock += k.normalized * knock; }
            if (!silent)
            {
                Color pc = col ?? (crit ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.95f, 0.8f)); Fx.Popup_(e.Center + Vector3.up * e.Radius, Mathf.RoundToInt(dmg).ToString(), pc, crit ? 1.6f : 1f);
                Sfx.Hit(e.Center, 0.4f); Fx.Burst(e.Center, 4, 3f, 0.14f, pc, 0.4f);
            }
            Combo.Hit(); Skills.OnHit(e, dmg);
            if (e.hp <= 0f) Kill(e);
        }
        public static void DamageRaw(Enemy e, float dmg) { Damage(e, dmg, Vector3.zero, 0f, false, new Color(1f, 0.5f, 0.2f), true); }

        public static int Burst(Vector3 c, float r, float dmg, float knock, Color? col = null, bool crit = false)
        {
            int n = 0; for (int i = E.Count - 1; i >= 0; i--) { var e = E[i]; if (e.dead) continue; float d = Vector3.Distance(e.Center, c) - e.Radius * 0.6f; if (d > r) continue; n++; Damage(e, dmg * Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(d / r)), (e.Center - c), knock, crit, col); }
            return n;
        }
        public static int Cone(Vector3 o, Vector3 fwd, float range, float angle, float dmg, float knock, Color? col = null, bool crit = false, Func<Enemy, float> dmgOf = null)
        {
            int n = 0; fwd.Normalize(); for (int i = E.Count - 1; i >= 0; i--) { var e = E[i]; if (e.dead) continue; Vector3 v = e.Center - o; float d = v.magnitude - e.Radius; if (d > range) continue; if (Vector3.Angle(fwd, v) > angle * 0.5f + Mathf.Atan2(e.Radius, Mathf.Max(0.5f, v.magnitude)) * Mathf.Rad2Deg) continue; n++; Damage(e, dmgOf != null ? dmgOf(e) : dmg, v, knock, crit, col); }
            return n;
        }
        public static int Line(Vector3 a, Vector3 b, float halfW, float dmg, float knock, Color? col = null, Func<Enemy, float> dmgOf = null)
        {
            int n = 0; for (int i = E.Count - 1; i >= 0; i--) { var e = E[i]; if (e.dead) continue; if (DistSeg(e.Center, a, b) > halfW + e.Radius) continue; n++; Damage(e, dmgOf != null ? dmgOf(e) : dmg, e.Center - a, knock, false, col); }
            return n;
        }
        public static float DistSeg(Vector3 p, Vector3 a, Vector3 b) { Vector3 ab = b - a; float t = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude); return Vector3.Distance(p, a + ab * t); }

        public static void Kill(Enemy e)
        {
            if (e.dead) return; e.dead = true; Kills++;
            Vector3 c = e.Center; var col = e.d.boss ? new Color(1f, 0.8f, 0.4f) : new Color(0.55f, 0.2f, 0.7f);
            Fx.Burst(c, e.d.boss ? 70 : 18, e.d.boss ? 9f : 4f, e.d.boss ? 0.5f : 0.3f, col, 0.9f); Fx.Burst(c, e.d.boss ? 20 : 6, 2f, e.d.boss ? 2.6f : 1.2f, new Color(0.05f, 0.03f, 0.08f, 0.6f), 1.4f, false, 0.6f);
            if (e.d.boss) { Fx.Explosion(c, 9f, col, true); Fx.HitStop(0.35f, 0.05f); Fx.Flash(Color.white, 0.5f, 2f); Sfx.Roar(c, 0.9f); Ui.Toast("★ " + e.d.name + " повержен!"); Hunt.BossKills++; }
            else { Sfx.Thud(c, 0.4f); Fx.Ring(c, Vector3.up, col, 3f, 0.4f, 0.2f); }
            Prog.AddXp(e.d.xp * (e.dummy ? 0f : 1f), e.d); Skills.OnKill(e); Hunt.OnKill(e);
            var go = e.go; var s0 = go != null ? go.transform.localScale : Vector3.one; float dur = e.d.boss ? 1.2f : 0.45f; Vector3 p0 = e.pos;
            if (go != null) Fx.Add(go, dur, (k, g) => { g.transform.localScale = s0 * Mathf.Max(0.01f, 1f - k * k); g.transform.Rotate(0f, 400f * Time.deltaTime, 0f); g.transform.position = p0 + Vector3.up * k * 1.5f; });
            E.Remove(e);
        }

        // ---------- снаряды ----------
        static Mesh crescent;
        public static Mesh CrescentMesh() { return Crescent(); }
        static Mesh Crescent() { if (crescent == null) { var mb = new MB(); mb.Crescent(Vector3.zero, Vector3.forward, Vector3.up, 1f, 0.75f, 130f, 0.04f, Color.white, 16); crescent = mb.ToMesh("jjk_cres"); } return crescent; }
        public static Proj Shoot(Vector3 pos, Vector3 dir, float speed, float r, float dmg, Color col, bool friendly, float life = 4f, bool slash = false)
        {
            var p = new Proj { pos = pos, vel = dir.normalized * speed, r = r, dmg = dmg, col = col, friendly = friendly, life = life, slash = slash, born = Time.time };
            var go = new GameObject(slash ? "JJK_Slash" : "JJK_Proj"); go.transform.position = pos; var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = slash ? Crescent() : Fx.Sphere();
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Gfx.Fade(null, new Color(col.r, col.g, col.b, 0.9f), true); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            go.transform.localScale = slash ? Vector3.one * r * 1.6f : Vector3.one * r * 2f; if (slash) go.transform.rotation = Quaternion.LookRotation(dir.normalized);
            if (!slash) { var core = new GameObject("core"); core.transform.SetParent(go.transform, false); core.transform.localScale = Vector3.one * 0.55f; core.AddComponent<MeshFilter>().sharedMesh = Fx.Sphere(); var cr = core.AddComponent<MeshRenderer>(); cr.sharedMaterial = Gfx.Fade(null, new Color(1f, 1f, 1f, 0.95f), true); cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            p.go = go; P.Add(p); return p;
        }

        static void TickProjs(float dt)
        {
            Vector3 pp = Pl.Pos; bool alive = Pl.Alive;
            for (int i = P.Count - 1; i >= 0; i--)
            {
                var p = P[i]; if (p.dead) { if (p.go != null) Fx.Kill(p.go); P.RemoveAt(i); continue; }
                p.life -= dt; if (p.life <= 0f) { Detonate(p, p.pos, null); continue; }
                if (p.homing > 0f)
                {
                    Vector3 tp = Vector3.zero; bool has = false;
                    if (p.friendly) { if (p.target == null || p.target.dead) p.target = Aim.Nearest(p.pos, 40f); if (p.target != null) { tp = p.target.Center; has = true; } } else if (alive) { tp = pp; has = true; }
                    if (has) { float sp = p.vel.magnitude; Vector3 want = (tp - p.pos).normalized * sp; p.vel = Vector3.RotateTowards(p.vel, want, p.homing * dt, 0f); }
                }
                p.vel += Vector3.down * p.grav * dt; Vector3 np = p.pos + p.vel * dt;
                RaycastHit h; if (!p.noTerrain && Physics.Linecast(p.pos, np, out h, Pl.Mask, QueryTriggerInteraction.Ignore)) { Detonate(p, h.point - p.vel.normalized * 0.1f, null); continue; }
                p.pos = np; if (p.go != null) { p.go.transform.position = np; if (p.slash) p.go.transform.rotation = Quaternion.LookRotation(p.vel); else { p.go.transform.localScale = Vector3.one * p.r * 2f * (1f + 0.1f * Mathf.Sin(Time.time * 30f)); } }
                if (!p.slash && UnityEngine.Random.value < 0.8f) Fx.Puff(np, UnityEngine.Random.insideUnitSphere * 0.5f, p.r * 1.3f, new Color(p.col.r, p.col.g, p.col.b, 0.5f), 0.4f);
                if (p.slash) Fx.Spark(np, UnityEngine.Random.insideUnitSphere * 1.5f, 0.2f, p.col, 0.3f);
                if (p.friendly)
                {
                    foreach (var e in E.ToArray()) { if (e.dead || p.hit.Contains(e)) continue; if ((e.Center - np).sqrMagnitude > (p.r + e.Radius) * (p.r + e.Radius)) continue; p.hit.Add(e); Damage(e, p.dmg, p.vel, p.knock, false, p.col); if (p.status == "burn") e.burn = 3f; if (!p.pierce) { Detonate(p, np, e); break; } else if (p.onHit != null) p.onHit(p, np, e); }
                }
                else if (alive && (pp - np).sqrMagnitude < (p.r + 0.75f) * (p.r + 0.75f))
                {
                    if (Pl.Hurt(p.dmg, p.status ?? "Injury", 0.45f)) { if (p.statusAmt > 0f) Pl.Status("Poison", p.statusAmt); if (p.knock > 0f) Pl.Push(p.vel.normalized * p.knock * 0.2f + Vector3.up * 1f); }
                    Detonate(p, np, null);
                }
            }
        }
        static void Detonate(Proj p, Vector3 at, Enemy hitE)
        {
            if (p.dead) return; if (p.onHit != null && p.expl > 0f == false && hitE == null) { }
            if (p.expl > 0f) { Fx.Explosion(at, p.expl, p.col, !p.friendly); if (p.friendly) Burst(at, p.expl, p.dmg * 0.7f, p.knock, p.col); else if (Pl.Alive && Vector3.Distance(Pl.Pos, at) < p.expl) { if (Pl.Hurt(p.dmg * 0.8f, p.status ?? "Injury", 0.4f)) Pl.Push((Pl.Pos - at).normalized * 4f + Vector3.up * 2f); } }
            else { Fx.Burst(at, 8, 3f, 0.2f, p.col, 0.4f); Fx.Light(at, p.col, 2f, 5f, 0.25f); }
            if (p.onHit != null && p.expl > 0f) p.onHit(p, at, hitE);
            p.dead = true; if (p.go != null) { Fx.Kill(p.go); p.go = null; }
        }

        // ---------- зоны (телеграфируемые ловушки врагов) ----------
        static Mesh flat; static Texture2D discTex, rectTex;
        static Mesh Flat() { if (flat == null) { var mb = new MB(); mb.QuadBoth(new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f), Vector3.up, Color.white); flat = mb.ToMesh("jjk_flat"); var uv = new Vector2[flat.vertexCount]; var vs = flat.vertices; for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(vs[i].x + 0.5f, vs[i].z + 0.5f); flat.uv = uv; } return flat; }
        static Texture2D Disc() { if (discTex == null) discTex = Gfx.Tex(64, 64, (u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2f; return new Color(1, 1, 1, Mathf.SmoothStep(1f, 0.7f, d) * 0.6f); }); return discTex; }
        static Texture2D Rect() { if (rectTex == null) rectTex = Gfx.Tex(32, 32, (u, v) => { float e = Mathf.Min(u, 1f - u) * 2f; return new Color(1, 1, 1, Mathf.SmoothStep(0f, 0.35f, e) * 0.6f); }); return rectTex; }
        static GameObject FlatObj(Texture tex, Color col, out Material mat)
        {
            var go = new GameObject("JJK_Zone"); go.AddComponent<MeshFilter>().sharedMesh = Flat(); var mr = go.AddComponent<MeshRenderer>(); mat = Gfx.Fade(tex, col, true); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; return go;
        }
        public static Zone Circle(Vector3 pos, float r, float delay, float dmg, Color col, string status = "Injury", float knock = 4f, bool meteor = false, float linger = 0f, Action<Zone> onFire = null)
        {
            bool f; pos.y = Ground(pos, out f); var z = new Zone { shape = 0, pos = pos, r = r, delay = delay, dmg = dmg, col = col, status = status, knock = knock, meteor = meteor, linger = linger, born = Time.time, onFire = onFire };
            Material m; z.tele = FlatObj(RingTexOf(), new Color(col.r, col.g, col.b, 0.9f), out m); z.tele.transform.position = pos + Vector3.up * 0.25f; z.tele.transform.localScale = new Vector3(r * 2f, 1f, r * 2f);
            z.fill = FlatObj(Disc(), new Color(col.r, col.g, col.b, 0.5f), out z.fillMat); z.fill.transform.position = pos + Vector3.up * 0.28f; z.fill.transform.localScale = Vector3.zero;
            if (meteor) { z.met = new GameObject("JJK_Meteor"); z.met.AddComponent<MeshFilter>().sharedMesh = Fx.Sphere(); var mr = z.met.AddComponent<MeshRenderer>(); mr.sharedMaterial = Gfx.Fade(null, new Color(1f, 0.45f, 0.1f, 0.9f), true); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; z.met.transform.localScale = Vector3.one * r * 0.9f; z.met.transform.position = pos + Vector3.up * 70f; }
            Z.Add(z); return z;
        }
        static Texture2D ringT; static Texture2D RingTexOf() { if (ringT == null) ringT = Gfx.RingTex(); return ringT; }
        public static Zone LineZone(Vector3 a, Vector3 b, float width, float delay, float dmg, Color col, string status = "Injury", float knock = 4f, Action<Zone> onFire = null)
        {
            bool f; a.y = Ground(a, out f) ; b.y = Ground(b, out f); var z = new Zone { shape = 1, a = a, b = b, widthHalf = width * 0.5f, r = width * 0.5f, delay = delay, dmg = dmg, col = col, status = status, knock = knock, born = Time.time, onFire = onFire, pos = (a + b) * 0.5f };
            Vector3 d = b - a; float len = d.magnitude; Quaternion q = Quaternion.LookRotation(new Vector3(d.x, 0f, d.z).sqrMagnitude < 0.01f ? Vector3.forward : new Vector3(d.x, 0f, d.z));
            Material m; z.tele = FlatObj(Rect(), new Color(col.r, col.g, col.b, 0.9f), out m); z.tele.transform.SetPositionAndRotation(z.pos + Vector3.up * 0.3f, q); z.tele.transform.localScale = new Vector3(width, 1f, len);
            z.fill = FlatObj(Rect(), new Color(col.r, col.g, col.b, 0.6f), out z.fillMat); z.fill.transform.SetPositionAndRotation(z.pos + Vector3.up * 0.33f, q); z.fill.transform.localScale = new Vector3(0.01f, 1f, len);
            Z.Add(z); return z;
        }
        static bool InZone(Zone z, Vector3 p)
        {
            if (z.shape == 0) { Vector3 d = p - z.pos; d.y = 0f; if (d.magnitude > z.r + 0.4f) return false; return Mathf.Abs(p.y - z.pos.y) < Mathf.Max(z.r, 3f) + 2.5f; }
            return DistSeg(p, z.a + Vector3.up * 0.5f, z.b + Vector3.up * 0.5f) < z.widthHalf + 0.5f;
        }
        static void TickZones(float dt)
        {
            bool alive = Pl.Alive; Vector3 pp = Pl.Pos; bool slowed = false;
            for (int i = Z.Count - 1; i >= 0; i--)
            {
                var z = Z[i]; float age = Time.time - z.born;
                if (!z.fired)
                {
                    float k = Mathf.Clamp01(age / z.delay);
                    if (z.shape == 0) z.fill.transform.localScale = new Vector3(z.r * 2f * k, 1f, z.r * 2f * k); else z.fill.transform.localScale = new Vector3(Mathf.Max(0.01f, z.widthHalf * 2f * k), 1f, z.fill.transform.localScale.z);
                    float pulse = 0.5f + 0.5f * Mathf.Sin(age * 18f * (0.4f + k)); var c = z.col; c.a = 0.35f + 0.4f * k * pulse; if (z.fillMat != null) z.fillMat.color = c;
                    if (z.met != null) z.met.transform.position = Vector3.Lerp(z.pos + Vector3.up * 70f, z.pos + Vector3.up * z.r * 0.4f, k * k);
                    if (age >= z.delay) Fire(z);
                }
                else
                {
                    if (z.linger > 0f)
                    {
                        float la = Time.time - z.born - z.delay; if (la >= z.linger) { Kill(z); Z.RemoveAt(i); continue; }
                        if (alive && InZone(z, pp)) { slowed |= z.slowIn; if (Time.time >= z.nextTick) { z.nextTick = Time.time + z.tick; Pl.Hurt(z.dmg * 0.35f, z.status, 0.1f); } }
                        if (z.onTick != null && UnityEngine.Random.value < dt * 6f) z.onTick(z);
                    }
                    else { Kill(z); Z.RemoveAt(i); continue; }
                }
            }
            if (slowed) Pl.Mul("zoneslow", 0.55f, 0.8f); else Pl.Mul("zoneslow", 1f, 1f);
        }
        static void Kill(Zone z) { if (z.tele != null) Fx.Kill(z.tele); if (z.fill != null) Fx.Kill(z.fill); if (z.met != null) Fx.Kill(z.met); z.dead = true; }
        static void Fire(Zone z)
        {
            z.fired = true; z.nextTick = Time.time + z.tick; if (z.met != null) { Fx.Kill(z.met); z.met = null; }
            if (z.linger <= 0f) { if (z.tele != null) z.tele.SetActive(false); if (z.fill != null) z.fill.SetActive(false); } else if (z.fill != null) z.fill.transform.localScale = z.shape == 0 ? new Vector3(z.r * 2f, 1f, z.r * 2f) : new Vector3(z.widthHalf * 2f, 1f, z.fill.transform.localScale.z);
            if (z.shape == 0)
            {
                if (z.r >= 3f) Fx.Explosion(z.pos + Vector3.up * 0.5f, z.r * 0.7f, z.col, true); else { Fx.Burst(z.pos + Vector3.up * 0.5f, 18, 6f, 0.25f, z.col, 0.5f); Fx.Ring(z.pos + Vector3.up * 0.3f, Vector3.up, z.col, z.r * 1.3f, 0.4f, 0.4f); Sfx.Slash(z.pos, 0.5f); }
                Fx.Shake(Mathf.Clamp(0.05f * z.r * (1f - Mathf.Clamp01(Vector3.Distance(Pl.Pos, z.pos) / 40f)), 0f, 0.5f), 0.3f);
            }
            else
            {
                Vector3 d = z.b - z.a; int n = Mathf.Clamp((int)(d.magnitude / 3f), 3, 24); for (int j = 0; j <= n; j++) { Vector3 p = z.a + d * (j / (float)n) + Vector3.up * 0.5f; Fx.Burst(p, 6, 5f, 0.3f, z.col, 0.5f); }
                Fx.Bolt(z.a + Vector3.up, z.b + Vector3.up, new Color(z.col.r, z.col.g, z.col.b, 0.9f), z.widthHalf * 1.4f, 0.35f, 0.6f, 24); Fx.Light(z.pos + Vector3.up * 2f, z.col, 5f, d.magnitude * 0.7f + 6f, 0.4f); Sfx.Boom(z.pos, 0.6f); Fx.Shake(0.25f, 0.3f);
            }
            if (Pl.Alive && InZone(z, Pl.Pos))
            {
                if (Pl.Hurt(z.dmg, z.status, 0.5f) && z.knock > 0f) { Vector3 away = z.shape == 0 ? (Pl.Pos - z.pos) : (Pl.Pos - Vector3.Project(Pl.Pos - z.a, z.b - z.a) - z.a); away.y = 0f; Pl.Push(away.normalized * z.knock + Vector3.up * z.knock * 0.5f); }
            }
            if (z.onFire != null) z.onFire(z);
        }

        // ---------- главный тик ----------
        public static void Tick()
        {
            float dt = Time.deltaTime; if (dt <= 0f) return;
            for (int i = timers.Count - 1; i >= 0; i--) if (Time.time >= timers[i].Key) { var a = timers[i].Value; timers.RemoveAt(i); try { a(); } catch (Exception ex) { Plugin.Log("timer", ex); } }
            TickProjs(dt); TickZones(dt); Enemies.Tick(dt);
        }

        public static void ClearAll()
        {
            foreach (var e in E) if (e.go != null) UnityEngine.Object.Destroy(e.go); E.Clear();
            foreach (var p in P) if (p.go != null) Fx.Kill(p.go); P.Clear();
            foreach (var z in Z) Kill(z); Z.Clear(); timers.Clear(); Pl.Mul("zoneslow", 1f, 1f);
        }
    }
}
