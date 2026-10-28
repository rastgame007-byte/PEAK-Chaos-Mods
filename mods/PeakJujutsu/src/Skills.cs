// Персонажи, техники, проклятая энергия (ЦЭ), кулдауны, ульт.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakJJK
{
    internal sealed class Skill
    {
        public string name, desc; public float cost, cd, ready; public Action cast; public bool toggle;
        public float Remaining { get { return Mathf.Max(0f, ready - Time.time); } }
        public float Frac { get { return cd <= 0.01f ? 0f : Mathf.Clamp01(Remaining / cd); } }
    }
    internal sealed class Chr
    {
        public string id, name, jp, desc; public Color col, col2; public int unlock; public Skill[] s = new Skill[5];
    }

    internal static class Skills
    {
        public static readonly List<Chr> Chars = new List<Chr>(); public static Chr Cur;
        public static float CE = 100f, Ult; public static float MaxCE { get { return Prog.MaxCE; } }
        public static bool Infinity, Boost, Overtime, ZoneOn, BfWindow, Training;
        static float boostUntil, overtimeUntil, zoneUntil, castLock, nextHeal, bfA, bfB; static bool bfArmed; public static float HealGlow;
        static int ratio; static GameObject aura; static readonly KeyCode[] slotKeys = { KeyCode.Z, KeyCode.X, KeyCode.V, KeyCode.G, KeyCode.H };
        static Color Cc(float r, float g, float b) { return new Color(r, g, b); }
        static readonly Color Blue = new Color(0.25f, 0.55f, 1f), Red = new Color(1f, 0.2f, 0.2f), Purple = new Color(0.75f, 0.35f, 1f), Fire = new Color(1f, 0.5f, 0.1f), Cyan = new Color(0.4f, 0.85f, 1f);

        public static bool Unlocked(Chr c) { return Plugin.CfgUnlockAll.Value || Prog.Level >= c.unlock; }
        public static float DmgMul() { return Prog.DmgMul * Plugin.CfgPower * Combo.Mul * (Boost ? 1.35f : 1f) * (Overtime ? 1.5f : 1f) * (ZoneOn ? 1.25f : 1f); }
        public static float IncomingMul()
        {
            if (Infinity && (Training || CE >= 6f)) { if (!Training) CE -= 6f; Fx.Ring(Pl.Pos, Vector3.up, new Color(0.5f, 0.8f, 1f), 2.6f, 0.35f, 0.15f); Sfx.Ding(0.2f, 0.7f); return 0f; }
            float m = 1f; if (Overtime) m *= 0.7f; if (Boost) m *= 0.85f; return m;
        }
        public static void OnHit(Enemy e, float dmg) { Ult = Mathf.Min(100f, Ult + dmg * 0.045f); CE = Mathf.Min(MaxCE, CE + 0.25f); }
        public static void OnKill(Enemy e) { }

        // ================= определение персонажей =================
        static Skill S(string name, string desc, float cost, float cd, Action a, bool toggle = false) { return new Skill { name = name, desc = desc, cost = cost, cd = cd, cast = a, toggle = toggle }; }
        static Chr Add(string id, string name, string jp, string desc, Color c1, Color c2, int unlock) { var c = new Chr { id = id, name = name, jp = jp, desc = desc, col = c1, col2 = c2, unlock = unlock }; Chars.Add(c); return c; }

        static Skills()
        {
            var g = Add("gojo", "Сатору Годзё", "五条 悟", "Сильнейший. Безлимит и Шесть глаз: управляй притяжением, отталкиванием и их слиянием.", Blue, Cc(0.7f, 0.85f, 1f), 8);
            g.s[0] = S("Синий", "Притягивает и крошит врагов в точке прицела", 20, 4.5f, GojoBlue);
            g.s[1] = S("Красный", "Взрывной снаряд, отбрасывает", 25, 5f, GojoRed);
            g.s[2] = S("Фиолетовый", "Заряд 1,2 с и луч пустоты", 60, 14f, GojoPurple);
            g.s[3] = S("Бесконечность", "Переключатель: поглощает урон за ЦЭ", 0, 0.6f, GojoInfinity, true);
            g.s[4] = S("Безграничная пустота", "Домен: оглушает всех в куполе и давит информацией", 50, 0f, GojoDomain);

            var s = Add("sukuna", "Рёмен Сукуна", "両面宿儺", "Король проклятий. Рассечение, огонь и Храм Злобы.", Red, Cc(1f, 0.6f, 0.6f), 12);
            s.s[0] = S("Рассечение", "Три режущих лезвия веером", 12, 1.3f, SukDismantle);
            s.s[1] = S("Разруб", "Рубит врагов в конусе, урон растёт с их ХП", 30, 6f, SukCleave);
            s.s[2] = S("Огненная стрела", "Зарядка 0,8 с, взрыв и поджог", 50, 10f, SukFuga);
            s.s[3] = S("Рывок Хэйан", "Молниеносный рывок с ударом", 10, 3f, SukDash);
            s.s[4] = S("Храм Злобы", "Домен: 12 с непрерывных разрезов", 50, 0f, SukDomain);

            var y = Add("yuji", "Юдзи Итадори", "虎杖 悠仁", "Кулаки, скорость и Чёрная вспышка.", Fire, Cc(1f, 0.8f, 0.5f), 1);
            y.s[0] = S("Расходящийся кулак", "Удар с задержанным вторым ударом", 10, 1.1f, YujiFist);
            y.s[1] = S("Чёрная вспышка", "Нажми ещё раз в окно удара — ×4 урон и «Зона»", 25, 5f, YujiBlack);
            y.s[2] = S("Манджи-кик", "Прыжок-вертушка по площади", 15, 4f, YujiKick);
            y.s[3] = S("Усиление ЦЭ", "8 с: урон, скорость и прыжок выше", 20, 14f, YujiBoost);
            y.s[4] = S("Серия вспышек", "6 автоматических Чёрных вспышек по врагам", 50, 0f, YujiUlt);

            var m = Add("megumi", "Мегуми Фусигуро", "伏黑 恵", "Десять теней: псы, Нуэ, Махорага.", Cc(0.3f, 0.35f, 0.7f), Cc(0.6f, 0.7f, 1f), 3);
            m.s[0] = S("Божественные псы", "Белый и чёрный псы на 12 с", 20, 8f, MegDogs);
            m.s[1] = S("Нуэ", "Громовая птица бьёт молниями 4 с", 30, 9f, MegNue);
            m.s[2] = S("Макс-слон", "Водяная волна: урон и оглушение", 35, 10f, MegElephant);
            m.s[3] = S("Шаг в тени", "Телепорт к точке прицела (до 16 м)", 15, 3.5f, MegStep);
            m.s[4] = S("Сад теней: Махорага", "Домен и Восьмирукий Махорага на 20 с", 50, 0f, MegDomain);

            var t = Add("todo", "Аой Тодо", "東堂 葵", "Буги-Вуги: обмен, хлопки, кулаки лучшего друга.", Cc(0.9f, 0.7f, 0.2f), Cc(1f, 0.9f, 0.5f), 5);
            t.s[0] = S("Буги-Вуги", "Меняется местами с врагом или блинкует", 15, 3f, TodoBoogie);
            t.s[1] = S("Хлопок", "Оглушает всех вокруг", 10, 2.8f, TodoClap);
            t.s[2] = S("Серия ударов", "5 быстрых ударов", 25, 6f, TodoBarrage);
            t.s[3] = S("Лучший друг", "Призрак Юдзи сражается 12 с", 30, 15f, TodoFriend);
            t.s[4] = S("Вихрь хлопков", "6 с: стягивает и оглушает всех", 50, 0f, TodoUlt);

            var n = Add("nanami", "Кенто Нанами", "七海 建人", "Ратио 7:3. Дисциплина и работа сверхурочно.", Cc(0.95f, 0.85f, 0.4f), Cc(1f, 0.95f, 0.7f), 1);
            n.s[0] = S("Соотношение 7:3", "Удар тесаком; в точке 7:3 — крит ×3", 15, 1.8f, NanRatio);
            n.s[1] = S("Обрушение", "Линия разрушения впереди, оглушает", 30, 7f, NanCollapse);
            n.s[2] = S("Сверхурочная", "10 с: урон ×1,5, получаемый ×0,7", 20, 20f, NanOvertime);
            n.s[3] = S("Рывок клинка", "Короткий рывок с ударом", 10, 3f, NanDash);
            n.s[4] = S("Жёсткий предел", "Крит-серия по всем врагам рядом", 50, 0f, NanUlt);
            Cur = Chars[2];
        }

        public static void Select(Chr c)
        {
            if (c == null || c == Cur) return; if (!Unlocked(c)) { Ui.Toast(c.name + " откроется на уровне " + c.unlock); return; }
            ClearBuffs(); Cur = c; Plugin.CfgChar.Value = c.id; Ui.Toast("Персонаж: " + c.name); Fx.Ring(Pl.Pos, Vector3.up, c.col, 4f, 0.5f, 0.3f); Sfx.Ding(0.3f, 1.2f);
        }
        public static void SelectById(string id) { foreach (var c in Chars) if (c.id == id && Unlocked(c)) { Cur = c; return; } }
        public static void Next() { int i = Chars.IndexOf(Cur); for (int k = 1; k <= Chars.Count; k++) { var c = Chars[(i + k) % Chars.Count]; if (Unlocked(c)) { Select(c); return; } } }
        public static void ClearBuffs()
        {
            Infinity = false; Boost = false; Overtime = false; ZoneOn = false; bfArmed = false; BfWindow = false; boostUntil = overtimeUntil = zoneUntil = 0f; Pl.Mul("boost", 1f, 1f); Pl.Mul("cast", 1f, 1f); if (aura != null) { Fx.Kill(aura); aura = null; }
        }

        // ================= тик =================
        public static void Tick(float dt)
        {
            Training = Hunt.Mode == 2 || Plugin.CfgInfiniteCE.Value; if (Training) { CE = MaxCE; Ult = 100f; }
            CE = Mathf.Min(MaxCE, CE + Prog.Regen * dt * (Keys.Held(KeyCode.J) ? 0f : 1f)); Ult = Mathf.Min(100f, Ult + 1.0f * dt);
            if (Boost && Time.time > boostUntil) { Boost = false; Pl.Mul("boost", 1f, 1f); Ui.Toast("Усиление закончилось"); }
            if (Overtime && Time.time > overtimeUntil) { Overtime = false; Ui.Toast("Рабочий день окончен"); }
            if (ZoneOn && Time.time > zoneUntil) { ZoneOn = false; }
            if (Infinity) { if (!Training) CE -= 3f * dt; if (CE <= 0.5f) { CE = 0f; Infinity = false; Ui.Toast("Бесконечность спала: нет ЦЭ"); } }
            UpdateAura();
            if (bfArmed) BfWindow = Time.time >= bfA && Time.time <= bfB; else BfWindow = false;
            if (!Pl.Alive) { if (Infinity || Boost) ClearBuffs(); return; }
            if (Plugin.MenuOpen || Plugin.TypingNow) return;

            if (Keys.Down(KeyCode.B)) Next();
            // лечение обратной проклятой техникой
            if (Keys.Held(KeyCode.J)) { if (Time.time >= nextHeal && (CE >= 15f || Training)) { nextHeal = Time.time + 1.2f; if (!Training) CE -= 12f; Pl.Heal(0.12f, 1f); Sfx.Heal(0.3f); Fx.Burst(Pl.Pos, 8, 2f, 0.25f, new Color(0.5f, 1f, 0.6f), 0.7f, true, 2f); HealGlow = 1f; } }
            HealGlow = Mathf.Max(0f, HealGlow - dt * 2f);
            if (Cur == null || castLock > Time.time) { return; }
            if (bfArmed && Keys.Down(KeyCode.X)) { BfPress(); return; }
            for (int i = 0; i < 5; i++)
            {
                if (!Keys.Down(slotKeys[i])) continue; Cast(i); break;
            }
        }

        public static bool Cast(int i)
        {
            var sk = Cur.s[i]; if (sk == null || Pl.Stunned) return false;
            if (sk.Remaining > 0f) { if (!sk.toggle) { Sfx.Ding(0.1f, 0.5f); } return false; }
            if (i == 4 && Ult < 100f && !Training) { Ui.Toast("Ульт заряжен на " + Mathf.FloorToInt(Ult) + "%"); return false; }
            bool toggleOff = sk.toggle && Infinity;
            if (!toggleOff && !Training && CE < sk.cost) { Ui.Toast("Не хватает ЦЭ (" + Mathf.CeilToInt(sk.cost) + ")"); Sfx.Ding(0.15f, 0.4f); return false; }
            if (!toggleOff && !Training) CE -= sk.cost; sk.ready = Time.time + sk.cd * Plugin.CfgCooldown; if (i == 4) { Ult = 0f; sk.ready = Time.time + 1f; }
            castLock = Time.time + 0.18f; try { sk.cast(); } catch (Exception e) { Plugin.Log("техника " + sk.name, e); }
            return true;
        }

        static void UpdateAura()
        {
            bool want = Infinity && Pl.Alive;
            if (want && aura == null) { aura = new GameObject("JJK_Infinity"); aura.AddComponent<MeshFilter>().sharedMesh = Fx.Sphere(); var mr = aura.AddComponent<MeshRenderer>(); mr.sharedMaterial = Gfx.Fade(null, new Color(0.4f, 0.7f, 1f, 0.12f), true); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            else if (!want && aura != null) { Fx.Kill(aura); aura = null; }
            if (aura != null) { aura.transform.position = Pl.Pos; aura.transform.localScale = Vector3.one * (3.2f + Mathf.Sin(Time.time * 4f) * 0.15f); }
        }

        // ================= помощники =================
        static Vector3 Flat() { return Pl.Fwd; }
        static Vector3 AimDir() { var t = Aim.Target(25f, 10f); if (t != null) return (t.Center - Pl.Pos).normalized; return Aim.Dir; }
        static Vector3 Ground(Vector3 p) { return World.OnGround(p); }
        static void Lunge(float speed) { Vector3 d = AimDir(); d.y = Mathf.Clamp(d.y, -0.2f, 0.3f); Pl.Push(d.normalized * speed + Vector3.up * 1.2f); }
        static void Arc(Vector3 pos, Vector3 dir, Color col, float size, float roll = 0f)
        {
            var go = new GameObject("JJK_Arc"); go.transform.position = pos; go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, roll); go.AddComponent<MeshFilter>().sharedMesh = World.CrescentMesh();
            var mr = go.AddComponent<MeshRenderer>(); var mat = Gfx.Fade(null, col, true); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Fx.Add(go, 0.25f, (k, g) => { g.transform.localScale = Vector3.one * size * (0.7f + k * 0.7f); var c = col; c.a = col.a * (1f - k); mat.color = c; });
        }
        static GameObject Glowball(Vector3 pos, float r, Color col)
        {
            var go = new GameObject("JJK_Ball"); go.transform.position = pos; go.AddComponent<MeshFilter>().sharedMesh = Fx.Sphere(); var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Gfx.Fade(null, col, true); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; go.transform.localScale = Vector3.one * r * 2f; return go;
        }
        static int Melee(float range, float angle, float dmg, float knock, Color col, bool crit = false, Func<Enemy, float> dmgOf = null)
        {
            Vector3 d = AimDir(); Vector3 o = Pl.Pos; int n = World.Cone(o, d, range, angle, dmg * DmgMul(), knock, col, crit, dmgOf == null ? (Func<Enemy, float>)null : (e => dmgOf(e) * DmgMul()));
            Arc(o + d * 1.6f, d, new Color(col.r, col.g, col.b, 0.8f), range * 0.35f, UnityEngine.Random.Range(-40f, 40f)); Sfx.Whoosh(o, 0.4f); if (n > 0) { Fx.Shake(0.12f, 0.12f); Fx.HitStop(0.04f); }
            return n;
        }
        static void Blink(Vector3 target)
        {
            Vector3 p = target + Vector3.up * 0.8f; Fx.Burst(Pl.Pos, 14, 4f, 0.4f, new Color(0.1f, 0.1f, 0.2f, 0.8f), 0.7f); Pl.Warp(p); Pl.Invuln = Time.time + 0.4f; Fx.Burst(p, 14, 4f, 0.4f, new Color(0.1f, 0.1f, 0.2f, 0.8f), 0.7f); Sfx.Whoosh(p, 0.5f);
        }
        static void Lock(float t) { castLock = Time.time + t; }

        // ================= ГОДЗЁ =================
        static void GojoBlue()
        {
            Vector3 p = Aim.Target(40f, 12f) != null ? Aim.Target(40f, 12f).Center : Aim.Point(32f); float dur = 2.6f, rad = 15f; Sfx.Charge(p, 0.6f);
            var go = Glowball(p, 1f, new Color(0.2f, 0.45f, 1f, 0.55f)); var core = Glowball(p, 0.5f, new Color(0.8f, 0.95f, 1f, 0.9f)); core.transform.SetParent(go.transform, true); float next = 0f, popT = 0f; Fx.Light(p, Blue, 5f, 22f, dur);
            Fx.Add(go, dur, (k, gg) =>
            {
                float sz = 2.2f * (1f + Mathf.Sin(Time.time * 18f) * 0.07f) * Mathf.Min(1f, k * 6f); gg.transform.localScale = Vector3.one * sz; Fx.Swirl(p, rad * 0.5f, 3, new Color(0.4f, 0.65f, 1f, 0.7f), 0.3f, 0f);
                float dt = Time.deltaTime; bool dmg = Time.time >= next; if (dmg) next = Time.time + 0.2f; bool pop = Time.time >= popT; if (pop) popT = Time.time + 0.5f;
                foreach (var e in World.E.ToArray())
                {
                    if (e.dead) continue; Vector3 d = p - e.Center; float dist = d.magnitude; if (dist > rad) continue; e.knock += d.normalized * (e.d.boss ? 9f : 34f) * dt * Mathf.Lerp(1f, 0.4f, dist / rad); e.stun = Mathf.Max(e.stun, 0.1f);
                    if (dmg && dist < 6f) World.Damage(e, 7f * DmgMul(), d, 0f, false, Blue, !pop);
                }
                Fx.Shake(0.05f, 0.1f);
            });
            World.After(dur, () => { Fx.Explosion(p, 5f, Blue); World.Burst(p, 7f, 35f * DmgMul(), 8f, Blue); });
        }
        static void GojoRed()
        {
            Lock(0.3f); Vector3 o = Aim.Hand; Vector3 d = AimDir(); Fx.Orb(o, 1.1f, new Color(1f, 0.2f, 0.2f, 0.7f), 0.28f); Sfx.Charge(o, 0.5f);
            World.After(0.28f, () => { var p = World.Shoot(o, Aim.Dir, 48f, 0.9f, 48f * DmgMul(), Red, true, 2f); p.expl = 5.5f; p.knock = 22f; p.pierce = false; Fx.Shake(0.2f, 0.2f); Pl.Push(-Aim.Dir * 3f); Sfx.Boom(o, 0.5f); });
        }
        static void GojoPurple()
        {
            Lock(1.3f); Pl.Mul("cast", 0.25f, 0.2f); Vector3 o = Aim.Hand; var b = Glowball(o + Vector3.left * 0.9f, 0.35f, new Color(0.3f, 0.55f, 1f, 0.9f)); var r = Glowball(o + Vector3.right * 0.9f, 0.35f, new Color(1f, 0.2f, 0.2f, 0.9f)); Sfx.Charge(o, 0.8f); Fx.Flash(new Color(0.5f, 0.2f, 0.9f), 0.15f, 1.5f);
            Fx.Add(b, 1.15f, (k, g) => { g.transform.position = Aim.Hand + Aim.Dir * 0.8f + Pl.Cam.transform.right * Mathf.Lerp(-0.9f, 0f, k); g.transform.localScale = Vector3.one * (0.7f + k); });
            Fx.Add(r, 1.15f, (k, g) => { g.transform.position = Aim.Hand + Aim.Dir * 0.8f + Pl.Cam.transform.right * Mathf.Lerp(0.9f, 0f, k); g.transform.localScale = Vector3.one * (0.7f + k); });
            World.After(1.2f, () =>
            {
                Pl.Mul("cast", 1f, 1f); Vector3 a = Aim.Hand, d = Aim.Dir, e2 = a + d * 70f; RaycastHit h; if (Physics.Raycast(Aim.Origin + d * 1.5f, d, out h, 70f, Pl.Mask, QueryTriggerInteraction.Ignore)) e2 = h.point;
                Fx.Bolt(a, e2, new Color(0.7f, 0.3f, 1f, 0.9f), 4.5f, 0.6f, 0.15f, 24); Fx.Bolt(a, e2, new Color(1f, 1f, 1f, 1f), 1.8f, 0.45f, 0.1f, 24); Fx.Explosion(e2, 11f, Purple, true); Fx.Orb(a, 3f, new Color(0.8f, 0.5f, 1f, 0.6f), 0.4f);
                Fx.Flash(Color.white, 0.55f, 2.2f); Fx.Shake(0.8f, 0.8f); Fx.HitStop(0.14f); Pl.Push(-d * 7f); Sfx.Flash(a, 1f);
                World.Line(a, e2, 3.8f, 210f * DmgMul(), 30f, Purple); World.Burst(e2, 11f, 120f * DmgMul(), 22f, Purple);
            });
        }
        static void GojoInfinity()
        {
            Infinity = !Infinity; Ui.Toast(Infinity ? "Бесконечность: ВКЛ" : "Бесконечность: ВЫКЛ"); Fx.Ring(Pl.Pos, Vector3.up, Blue, 4f, 0.5f, 0.3f); Sfx.Ding(0.3f, Infinity ? 1.4f : 0.7f);
        }
        static void GojoDomain()
        {
            Lock(0.8f); Pl.Invuln = Time.time + 1f; Domains.Open(Pl.Pos + Vector3.up * 0.5f, 30f, 1, 9f, Color.white); Hud.Say("Сатору Годзё", "Расширение домена: Безграничная пустота."); Vector3 c = Pl.Pos; float t0 = Time.time;
            for (float t = 0.4f; t < 9f; t += 0.5f) { float tt = t; World.After(tt, () => { foreach (var e in World.E.ToArray()) { if (e.dead) continue; if ((e.pos - c).magnitude > 30f) continue; e.stun = Mathf.Max(e.stun, e.d.boss ? 0.8f : 1.2f); World.Damage(e, (e.d.boss ? 22f : 12f) * DmgMul(), Vector3.zero, 0f, false, new Color(0.8f, 0.9f, 1f), tt % 1f > 0.1f); } Fx.Spark(c + UnityEngine.Random.insideUnitSphere * 12f, Vector3.zero, 0.3f, Color.white, 0.8f); }); }
        }

        // ================= СУКУНА =================
        static void SukDismantle()
        {
            Vector3 o = Pl.Pos + Vector3.up * 0.3f; Vector3 d = AimDir(); Sfx.Slash(o, 0.7f); Fx.Shake(0.1f, 0.15f);
            for (int i = -1; i <= 1; i++) { Vector3 dir = Quaternion.AngleAxis(i * 11f, Vector3.up) * d; dir = Quaternion.AngleAxis(UnityEngine.Random.Range(-14f, 14f), Vector3.Cross(dir, Vector3.up)) * dir; var p = World.Shoot(o + dir * 0.8f, dir, 56f, 1.1f, 22f * DmgMul(), new Color(1f, 0.85f, 0.85f), true, 0.7f, true); p.pierce = true; p.knock = 5f; }
        }
        static void SukCleave()
        {
            Lock(0.25f); Vector3 d = AimDir(); float dm = DmgMul(); Sfx.Slash(Pl.Pos, 0.9f); Fx.HitStop(0.06f);
            int n = World.Cone(Pl.Pos, d, 9f, 110f, 0f, 10f, Red, false, e => (e.d.boss ? 30f + e.maxHp * 0.035f : 30f + e.hp * 0.45f) * dm);
            Arc(Pl.Pos + d * 2.5f, d, new Color(1f, 0.15f, 0.15f, 0.9f), 4f, 70f); Arc(Pl.Pos + d * 3f, d, new Color(1f, 1f, 1f, 0.8f), 3.2f, -50f); Fx.Burst(Pl.Pos + d * 3f, 14, 7f, 0.3f, Red, 0.6f); if (n > 0) Fx.Shake(0.3f, 0.25f);
        }
        static void SukFuga()
        {
            Lock(0.9f); Pl.Mul("cast", 0.4f, 0.4f); Vector3 o = Aim.Hand; Fx.Orb(o, 1.6f, new Color(1f, 0.55f, 0.1f, 0.7f), 0.85f, true); Sfx.Charge(o, 0.8f); Fx.Light(o, Fire, 5f, 14f, 0.9f); Hud.Say("Рёмен Сукуна", "Пылай. Фуга.");
            World.After(0.85f, () =>
            {
                Pl.Mul("cast", 1f, 1f); Vector3 a = Aim.Hand, d = Aim.Dir; var p = World.Shoot(a, d, 62f, 1.3f, 150f * DmgMul(), new Color(1f, 0.5f, 0.1f), true, 2.5f); p.expl = 8f; p.knock = 16f;
                p.onHit = (pp, at, e) => { foreach (var en in World.E) if (!en.dead && Vector3.Distance(en.Center, at) < 9f) en.burn = Mathf.Max(en.burn, 5f); };
                Fx.Shake(0.4f, 0.4f); Fx.Flash(Fire, 0.3f, 3f); Pl.Push(-d * 4f);
            });
        }
        static void SukDash()
        {
            Pl.Invuln = Time.time + 0.35f; Vector3 d = AimDir(); Lunge(20f); Fx.Burst(Pl.Pos, 14, 5f, 0.3f, Red, 0.5f); World.After(0.12f, () => { Melee(7f, 70f, 28f, 8f, Red); });
        }
        static void SukDomain()
        {
            Lock(0.8f); Pl.Invuln = Time.time + 1f; Domains.Open(Pl.Pos, 34f, 0, 12f, Red); Hud.Say("Рёмен Сукуна", "Расширение домена: Храм Злобы."); Vector3 c = Pl.Pos;
            for (float t = 0.6f; t < 12f; t += 0.16f) { float tt = t; World.After(tt, () => { int shown = 0; foreach (var e in World.E.ToArray()) { if (e.dead || (e.pos - c).magnitude > 34f) continue; World.Damage(e, (e.d.boss ? 7f : 9f) * DmgMul(), Vector3.zero, 0f, false, Red, shown++ > 1); if (shown <= 3) { Vector3 b = e.Center + UnityEngine.Random.insideUnitSphere * 1.2f; Fx.Bolt(b + new Vector3(-1.6f, 1.2f, 0), b + new Vector3(1.6f, -1.2f, 0), new Color(1f, 0.95f, 0.9f, 1f), 0.25f, 0.18f, 0.2f, 4); Fx.Burst(b, 4, 5f, 0.2f, Red, 0.4f); } } if (UnityEngine.Random.value < 0.5f) Sfx.Slash(c, 0.3f); }); }
        }

        // ================= ЮДЗИ =================
        static void YujiFist()
        {
            Lunge(9f); World.After(0.1f, () => { Melee(5f, 80f, 22f, 8f, Fire); Fx.Ring(Pl.Pos + AimDir() * 2f, AimDir(), Fire, 2.5f, 0.3f, 0.2f); });
            World.After(0.38f, () => { Melee(5.5f, 80f, 16f, 12f, new Color(1f, 0.8f, 0.5f)); Sfx.Thud(Pl.Pos, 0.5f); });
        }
        static void YujiBlack()
        {
            Lunge(14f); bfArmed = true; bfA = Time.time + 0.28f; bfB = Time.time + 0.6f; Fx.Ring(Pl.Pos, Aim.Dir, new Color(1f, 0.2f, 0.2f), 3f, 0.3f, 0.2f); Sfx.Charge(Pl.Pos, 0.3f);
            World.After(0.3f, () => { if (bfArmed) Fx.Ring(Pl.Pos + AimDir() * 1.5f, AimDir(), new Color(1f, 0.1f, 0.1f), 2.5f, 0.3f, 0.3f); });
            World.After(0.66f, () => { if (bfArmed) { bfArmed = false; Melee(5f, 70f, 28f, 8f, Fire); Ui.Toast("Мимо тайминга…"); } });
        }
        static void BfPress()
        {
            if (Time.time < bfA) { bfArmed = false; Melee(5f, 70f, 28f, 8f, Fire); Ui.Toast("Слишком рано"); return; }
            bfArmed = false; BfWindow = false; BlackFlashHit();
        }
        static void BlackFlashHit()
        {
            float mul = ZoneOn ? 1.5f : 1f; Vector3 d = AimDir(); Vector3 o = Pl.Pos + d * 1.5f;
            int n = World.Cone(Pl.Pos, d, 6.5f, 90f, 115f * DmgMul() * mul, 18f, Red, true);
            for (int i = 0; i < 4; i++) { Vector3 e1 = o + UnityEngine.Random.onUnitSphere * 3f; Fx.Bolt(o, e1, new Color(1f, 0.1f, 0.1f, 1f), 0.35f, 0.4f, 0.8f); Fx.Bolt(o, e1, new Color(0.02f, 0f, 0.02f, 1f), 0.15f, 0.4f, 0.8f); }
            Fx.Orb(o, 3.5f, new Color(0.02f, 0f, 0.03f, 0.8f), 0.3f); Fx.Orb(o, 4.5f, new Color(1f, 0.1f, 0.1f, 0.4f), 0.35f); Fx.Flash(new Color(0.1f, 0f, 0.05f), 0.55f, 4f); Fx.Shake(0.55f, 0.4f); Fx.HitStop(0.13f, 0.03f); Sfx.Flash(o, 1f);
            Fx.Popup_(Pl.Pos + Vector3.up * 2.2f, "ЧЁРНАЯ ВСПЫШКА!", new Color(1f, 0.15f, 0.15f), 2f);
            if (n > 0 && !ZoneOn) { ZoneOn = true; zoneUntil = Time.time + 20f; Ui.Toast("В ЗОНЕ! Урон +25% на 20 с"); }
            else if (n > 0) zoneUntil = Time.time + 20f;
        }
        static void YujiKick()
        {
            Pl.Push(Aim.Dir * 8f + Vector3.up * 6f); Sfx.Whoosh(Pl.Pos, 0.6f);
            World.After(0.3f, () => { Vector3 c = Pl.Pos; Fx.Ring(c, Vector3.up, Fire, 6f, 0.4f, 0.4f); Fx.Burst(c, 18, 7f, 0.3f, Fire, 0.5f); Fx.Shake(0.25f, 0.2f); World.Burst(c, 5.5f, 30f * DmgMul(), 14f, Fire); Sfx.Thud(c, 0.6f); });
        }
        static void YujiBoost()
        {
            Boost = true; boostUntil = Time.time + 8f; Pl.Mul("boost", 1.3f, 1.25f); Fx.Burst(Pl.Pos, 30, 5f, 0.3f, Fire, 0.8f, true, 3f); Fx.Ring(Pl.Pos, Vector3.up, Fire, 5f, 0.6f, 0.4f); Sfx.Roar(Pl.Pos, 0.4f); Ui.Toast("Усиление ЦЭ: 8 с");
        }
        static void YujiUlt()
        {
            if (World.E.Count == 0) { Ui.Toast("Нет целей — ульт не нужен"); Ult = 100f; return; }
            Hud.Banner("СЕРИЯ ЧЁРНЫХ ВСПЫШЕК", "Юдзи Итадори"); Pl.Invuln = Time.time + 3.8f;
            for (int i = 0; i < 6; i++) World.After(0.2f + i * 0.55f, () =>
            {
                if (!Pl.Alive) return; var e = Aim.Nearest(Pl.Pos, 45f); if (e == null) return; Vector3 side = (Pl.Pos - e.pos); side.y = 0f; side = side.sqrMagnitude < 0.1f ? Vector3.back : side.normalized;
                Blink(Ground(e.pos + side * (2.2f + e.Radius)) + Vector3.up * 0.4f);
                World.After(0.16f, () => { if (e == null || e.dead) return; Vector3 o = e.Center; World.Damage(e, 100f * DmgMul(), o - Pl.Pos, 14f, true, new Color(1f, 0.1f, 0.1f)); Fx.Bolt(Pl.Pos + Vector3.up, o, new Color(1f, 0.1f, 0.1f, 1f), 0.4f, 0.4f, 0.9f); Fx.Bolt(Pl.Pos + Vector3.up, o, new Color(0.02f, 0f, 0.02f, 1f), 0.18f, 0.4f, 0.9f); Fx.Orb(o, 3.5f, new Color(0.02f, 0f, 0.03f, 0.8f), 0.3f); Fx.Orb(o, 4.5f, new Color(1f, 0.1f, 0.1f, 0.4f), 0.35f); Fx.Flash(new Color(0.1f, 0f, 0.05f), 0.4f, 4f); Fx.Shake(0.5f, 0.3f); Fx.HitStop(0.1f, 0.03f); Sfx.Flash(o, 0.9f); });
            });
        }

        // ================= МЕГУМИ =================
        static void MegDogs()
        {
            Allies.KillKind("dog_w"); Allies.KillKind("dog_b"); Vector3 f = Flat(); Vector3 r = Vector3.Cross(Vector3.up, f);
            Allies.Spawn("dog_w", Ground(Pl.Pos + f * 3f + r * 1.5f), 12f); Allies.Spawn("dog_b", Ground(Pl.Pos + f * 3f - r * 1.5f), 12f); Sfx.Howl(Pl.Pos, 0.6f); Fx.Burst(Pl.Pos, 24, 5f, 0.4f, new Color(0.05f, 0.05f, 0.12f, 0.8f), 0.9f, false, 1f);
        }
        static void MegNue()
        {
            Allies.KillKind("nue"); Vector3 p = Aim.Target(40f, 12f) != null ? Aim.Target(40f, 12f).pos : Aim.Point(30f); var a = Allies.Spawn("nue", Pl.Pos + Vector3.up * 8f, 4.6f); a.anchor = p; Sfx.Screech(Pl.Pos, 0.6f); Fx.Flash(new Color(0.6f, 0.8f, 1f), 0.15f, 3f);
        }
        static void MegElephant()
        {
            Lock(0.3f); Vector3 d = AimDir(); Vector3 o = Pl.Pos + d * 1.2f; float dm = DmgMul(); Sfx.Boom(o, 0.5f); Fx.Shake(0.3f, 0.4f);
            for (int i = 0; i < 40; i++) { Vector3 dir = Quaternion.AngleAxis(UnityEngine.Random.Range(-28f, 28f), Vector3.up) * d; Fx.Puff(o + dir * UnityEngine.Random.value * 2f, dir * UnityEngine.Random.Range(14f, 24f) + Vector3.up * UnityEngine.Random.Range(0f, 4f), UnityEngine.Random.Range(0.8f, 1.8f), new Color(0.4f, 0.75f, 1f, 0.55f), 0.9f, true); }
            var w = World.Shoot(o, d, 30f, 4.5f, 0f, new Color(0.4f, 0.8f, 1f), true, 0.6f, true); w.pierce = true;
            World.Cone(Pl.Pos, d, 20f, 60f, 0f, 24f, Cyan, false, e => { e.stun = Mathf.Max(e.stun, e.d.boss ? 0.4f : 1.3f); return 58f * dm; });
        }
        static void MegStep()
        {
            var t = Aim.Target(30f, 12f); Vector3 p;
            if (t != null) { Vector3 behind = t.pos - (t.pos - Pl.Pos).normalized * -2.2f; p = Ground(behind); }
            else { bool hit; Vector3 pt = Aim.Point(16f, out hit); Vector3 dir = (pt - Pl.Pos).normalized; p = hit ? pt - dir * 1.2f : pt; p = Ground(p); }
            Blink(p + Vector3.up * 0.3f); Fx.Burst(p, 20, 4f, 0.5f, new Color(0.02f, 0.02f, 0.06f, 0.9f), 1f, false, 1f);
        }
        static void MegDomain()
        {
            Lock(0.8f); Pl.Invuln = Time.time + 1f; Domains.Open(Pl.Pos, 26f, 2, 20f, new Color(0.2f, 0.3f, 0.8f)); Hud.Say("Мегуми Фусигуро", "Расширение домена: Сад теней — химера.");
            Allies.KillKind("mahoraga"); Allies.Spawn("mahoraga", Ground(Pl.Pos + Flat() * 8f), 20f); Sfx.Roar(Pl.Pos, 1f);
            Vector3 c = Pl.Pos; for (float t = 0.5f; t < 20f; t += 0.5f) World.After(t, () => { foreach (var e in World.E) if (!e.dead && (e.pos - c).magnitude < 26f) e.slow = Mathf.Max(e.slow, 0.8f); });
        }

        // ================= ТОДО =================
        static void TodoBoogie()
        {
            var t = Aim.Target(45f, 18f);
            if (t != null)
            {
                Vector3 my = Pl.Pos; Vector3 eo = t.pos; Vector3 dest = Ground(eo + (my - eo).normalized * 1.5f);
                Blink(dest + Vector3.up * 0.3f); if (!t.d.boss) { t.pos = Ground(my) + Vector3.up * t.d.hover; t.knock = Vector3.zero; } t.stun = Mathf.Max(t.stun, 0.8f);
                Fx.Ring(my, Vector3.up, Cc(1f, 0.9f, 0.4f), 4f, 0.4f, 0.3f); Fx.Ring(eo, Vector3.up, Cc(1f, 0.9f, 0.4f), 4f, 0.4f, 0.3f); World.Damage(t, 12f * DmgMul(), Vector3.zero, 0f, false, Cc(1f, 0.9f, 0.4f));
            }
            else { bool hit; Vector3 pt = Aim.Point(24f, out hit); Vector3 dir = (pt - Pl.Pos).normalized; Blink(Ground(hit ? pt - dir * 1.2f : pt)); }
            Sfx.Clap(Pl.Pos, 0.9f);
        }
        static void TodoClap()
        {
            Vector3 c = Pl.Pos; Sfx.Clap(c, 1f); Fx.Ring(c, Vector3.up, Color.white, 14f, 0.5f, 0.6f); Fx.Ring(c + Vector3.up, Vector3.up, Cc(1f, 0.9f, 0.5f), 10f, 0.4f, 0.4f); Fx.Shake(0.4f, 0.3f); Fx.Burst(c, 24, 9f, 0.3f, Color.white, 0.5f); Fx.HitStop(0.05f);
            foreach (var e in World.E.ToArray()) { if (e.dead || (e.Center - c).magnitude > 15f) continue; e.stun = Mathf.Max(e.stun, e.d.boss ? 0.8f : 2.1f); World.Damage(e, 12f * DmgMul(), e.Center - c, 5f, false, Cc(1f, 0.9f, 0.5f)); }
        }
        static void TodoBarrage()
        {
            Lock(1f); for (int i = 0; i < 5; i++) { int j = i; World.After(j * 0.18f, () => { if (!Pl.Alive) return; if (j % 2 == 0) Lunge(5f); Melee(5.5f, 80f, 14f, 5f, Cc(1f, 0.85f, 0.4f)); Sfx.Hit(Pl.Pos, 0.5f); }); }
        }
        static void TodoFriend()
        {
            Allies.KillKind("clone"); Allies.Spawn("clone", Ground(Pl.Pos + Flat() * 3f + Vector3.Cross(Vector3.up, Flat()) * 1.5f), 12f); Hud.Say("Аой Тодо", "Ты же мой лучший друг, правда?"); Sfx.Clap(Pl.Pos, 0.7f);
        }
        static void TodoUlt()
        {
            Hud.Banner("ВИХРЬ ХЛОПКОВ", "Аой Тодо"); Pl.Invuln = Time.time + 2f; Vector3 c = Pl.Pos;
            for (float t = 0f; t < 6f; t += 0.2f) { float tt = t; World.After(tt, () => { if (!Pl.Alive) return; Vector3 p = Pl.Pos; Fx.Swirl(p, 9f, 8, new Color(1f, 0.9f, 0.5f, 0.7f), 0.4f, 1f); if (tt % 1f < 0.2f) { Sfx.Clap(p, 0.5f); Fx.Ring(p, Vector3.up, Color.white, 16f, 0.4f, 0.4f); Fx.Shake(0.15f, 0.2f); } foreach (var e in World.E.ToArray()) { if (e.dead) continue; Vector3 d = p - e.Center; if (d.magnitude > 18f) continue; e.knock += d.normalized * (e.d.boss ? 4f : 14f); e.stun = Mathf.Max(e.stun, 0.35f); World.Damage(e, 9f * DmgMul(), d, 0f, false, Cc(1f, 0.9f, 0.5f), tt % 0.6f > 0.1f); } }); }
        }

        // ================= НАНАМИ =================
        static void NanRatio()
        {
            Lunge(7f); ratio++; bool crit = ratio % 3 == 0 || UnityEngine.Random.value < (Overtime ? 0.5f : 0.25f);
            World.After(0.1f, () =>
            {
                int n = Melee(5.2f, 70f, 26f * (crit ? 3f : 1f), 9f, crit ? Red : Cc(0.95f, 0.85f, 0.4f), crit);
                if (crit && n > 0) { Fx.Popup_(Pl.Pos + Vector3.up * 2.2f, "7 : 3 !", Cc(1f, 0.9f, 0.3f), 1.8f); Fx.Flash(Cc(1f, 0.9f, 0.5f), 0.15f, 5f); Sfx.Slash(Pl.Pos, 0.9f); Fx.HitStop(0.08f); }
            });
        }
        static void NanCollapse()
        {
            Lock(0.4f); Vector3 a = Pl.Pos, d = Flat(); Vector3 b = a + d * 24f; float dm = DmgMul(); Sfx.Charge(a, 0.5f);
            for (int i = 0; i < 8; i++) { int j = i; World.After(0.1f + i * 0.05f, () => { Vector3 p = a + d * (3f + j * 3f); Fx.Burst(Ground(p) + Vector3.up * 0.5f, 10, 6f, 0.4f, Cc(0.6f, 0.55f, 0.5f), 0.8f, false, 1.5f); Fx.Burst(Ground(p) + Vector3.up * 0.5f, 6, 5f, 0.25f, Cc(1f, 0.9f, 0.5f), 0.5f); Sfx.Thud(p, 0.4f); }); }
            World.After(0.3f, () => { World.Line(a, b, 3f, 0f, 10f, Cc(1f, 0.9f, 0.5f), e => { e.stun = Mathf.Max(e.stun, e.d.boss ? 0.5f : 1.3f); return 72f * dm; }); Fx.Shake(0.45f, 0.5f); Fx.Bolt(a + Vector3.up, b + Vector3.up, new Color(1f, 0.9f, 0.5f, 0.7f), 1.4f, 0.3f, 0.3f, 16); });
        }
        static void NanOvertime()
        {
            Overtime = true; overtimeUntil = Time.time + 10f; Hud.Say("Кенто Нанами", "Начинается сверхурочная работа."); Fx.Ring(Pl.Pos, Vector3.up, Cc(1f, 0.9f, 0.3f), 5f, 0.6f, 0.4f); Fx.Burst(Pl.Pos, 24, 4f, 0.3f, Cc(1f, 0.9f, 0.3f), 0.8f, true, 2f); Sfx.Roar(Pl.Pos, 0.3f); Ui.Toast("Сверхурочная: 10 с");
        }
        static void NanDash()
        {
            Pl.Invuln = Time.time + 0.3f; Lunge(18f); World.After(0.1f, () => Melee(5f, 60f, 18f, 7f, Cc(1f, 0.9f, 0.5f)));
        }
        static void NanUlt()
        {
            Hud.Banner("ЖЁСТКИЙ ПРЕДЕЛ", "Кенто Нанами"); Pl.Invuln = Time.time + 2f; var list = new List<Enemy>(World.E); list.RemoveAll(e => e.dead || (e.pos - Pl.Pos).magnitude > 24f); if (list.Count == 0) { Ult = 100f; return; }
            for (int i = 0; i < 9; i++) { int j = i; World.After(0.2f + i * 0.28f, () => { var e = list[j % list.Count]; if (e == null || e.dead) return; Vector3 side = (Pl.Pos - e.pos); side.y = 0f; side = side.normalized; Blink(Ground(e.pos + side * (2f + e.Radius)) + Vector3.up * 0.3f); World.After(0.1f, () => { if (e.dead) return; World.Damage(e, 40f * 3f * DmgMul(), e.Center - Pl.Pos, 9f, true, new Color(1f, 0.85f, 0.2f)); Fx.Bolt(e.Center + new Vector3(-1.5f, 1.2f, 0), e.Center + new Vector3(1.5f, -1.2f, 0), new Color(1f, 0.9f, 0.4f, 1f), 0.3f, 0.25f, 0.2f, 5); Sfx.Slash(e.Center, 0.8f); Fx.HitStop(0.06f); }); }); }
        }
    }
}
