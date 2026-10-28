// Каталог модификаторов прохождения: 9 категорий, у каждого — очки сложности (минус = легче), параметр, исключения и логика (start / tick / biome / stop).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PeakMods
{
    internal sealed class Mod
    {
        public string id, cat, name, desc, excl = "", fmt = "0.##", unit = ""; public int pts, c;
        public float def, min, max; public string[] choices;
        public Action<Mod> start, stop; public Action<Mod, float> tick; public Action<Mod, int> biome; public Func<Mod, string> hud;
        // состояние
        public float cur, t, t2; public int n; public object o; public Vector3 v; public List<GameObject> objs = new List<GameObject>(); public List<Vector3> pts3 = new List<Vector3>(); public int errors; public bool broken; public List<int> kinds = new List<int>();
        public bool HasParam { get { return choices != null || max > min; } }
        public string ParamText() { return choices != null ? choices[Mathf.Clamp(Mathf.RoundToInt(cur), 0, choices.Length - 1)] : cur.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture) + unit; }
    }

    public partial class PeakModsPlugin
    {
        internal static readonly string[] CatNames = { "Облегчения", "Усложнения", "Условия", "Предметы", "Визуал", "Потребности", "Ловушки", "Бонусы", "Команда" };
        internal static readonly Color[] CatCols =
        {
            new Color(0.3f, 0.87f, 0.5f), new Color(1f, 0.38f, 0.34f), new Color(1f, 0.78f, 0.25f), new Color(0.35f, 0.7f, 1f), new Color(0.75f, 0.5f, 1f),
            new Color(1f, 0.6f, 0.3f), new Color(1f, 0.3f, 0.6f), new Color(0.4f, 0.95f, 0.85f), new Color(0.6f, 0.85f, 0.4f),
        };
        internal static readonly List<Mod> All = new List<Mod>(); internal static readonly Dictionary<string, Mod> ById = new Dictionary<string, Mod>();
        public static string RunWarn = ""; public static float RunWarnUntil;
        public static void Warn(string s, float dur = 3.5f) { RunWarn = s; RunWarnUntil = Time.time + dur; }

        static Mod M(int cat, string id, string name, string desc, int pts, string excl = "", float def = 0f, float min = 0f, float max = 0f, string fmt = "0.##", string unit = "")
        {
            var m = new Mod { c = cat, cat = CatNames[cat], id = id, name = name, desc = desc, pts = pts, excl = excl, def = def, min = min, max = max, fmt = fmt, unit = unit, cur = def }; All.Add(m); ById[id] = m; return m;
        }
        static bool Every(Mod m, float dt, float period) { m.t += dt; if (m.t >= period) { m.t = 0f; return true; } return false; }
        static System.Random RngS(long salt) { return new System.Random((int)((runSeed * 7919L + salt * 104729L) & 0x7fffffff)); }
        static void BuffMod(Mod m, string id) { m.start = x => Buff(id, true); m.stop = x => Buff(id, false); }
        static void Clean(Mod m) { foreach (var g in m.objs) if (g != null) UnityEngine.Object.Destroy(g); m.objs.Clear(); m.pts3.Clear(); m.kinds.Clear(); }
        static GameObject Marker(Vector3 p, Color c, float r)
        {
            var g = new GameObject("PM_marker"); g.transform.position = p + Vector3.up * 0.06f; g.transform.rotation = Quaternion.Euler(90f, 0f, 0f); g.transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
            g.AddComponent<MeshFilter>().sharedMesh = Vfx.Quad(); var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = Vfx.Mat(Vfx.Blob(), c); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return g;
        }
        static float NearestOther(Component me, out Component who)
        {
            who = null; float best = 1e9f; foreach (var o in OthersAlive()) { float d = Vector3.Distance(o.transform.position, me.transform.position); if (d < best) { best = d; who = o; } } return best;
        }
        static string Mmss(float s) { s = Mathf.Max(0f, s); return ((int)s / 60) + ":" + ((int)s % 60).ToString("00"); }

        static readonly ShadeKind[] FilterKinds = { ShadeKind.Film, ShadeKind.Noir, ShadeKind.Sunset, ShadeKind.Cyber, ShadeKind.Dream, ShadeKind.Night, ShadeKind.Thermal, ShadeKind.GameBoy, ShadeKind.Poster, ShadeKind.Vhs, ShadeKind.Horror };
        static readonly string[] FilterNames = { "Киноплёнка", "Чёрно-белое", "Закат", "Киберпанк", "Сон", "Ночное зрение", "Тепловизор", "GameBoy", "Постер", "Старая запись", "Хоррор" };
        static readonly string[] RoleNames = { "Скаут", "Силач", "Санитар", "Везунчик", "Выносливый" };
        public static int MyRole = -1;
        static readonly HashSet<string> StatusUsers = new HashSet<string> { "h_hunger", "h_poison", "h_cold", "h_heat", "c_timer", "c_tide", "n_sleep", "n_night", "n_meal", "n_warm", "t_rocks", "t_gust", "t_zones", "c_together" };
        internal static bool Clash(Mod a, Mod b)
        {
            if (a == b) return false; if (a.excl != "" && a.excl == b.excl) return true;
            return (a.excl == "immune" && StatusUsers.Contains(b.id)) || (b.excl == "immune" && StatusUsers.Contains(a.id));
        }

        static void BuildCatalog()
        {
            if (All.Count > 0) return; Mod m;
            // ===================== 0. ОБЛЕГЧЕНИЯ =====================
            m = M(0, "e_stam", "Вечная выносливость", "Выносливость не кончается", -4, "stam"); BuffMod(m, "stamina");
            m = M(0, "e_chalk", "Магнезия на руках", "Лазание тратит меньше сил", -2); BuffMod(m, "chalk");
            m = M(0, "e_djump", "Двойной прыжок", "Второй прыжок в воздухе", -2); BuffMod(m, "djump");
            m = M(0, "e_sun", "Крем от солнца", "Не обгораешь", -1); BuffMod(m, "sunscreen");
            m = M(0, "e_food", "Сытые всегда", "Голод не растёт", -2, "food"); BuffMod(m, "nohunger");
            m = M(0, "e_lowg", "Мягкие прыжки", "Низкая гравитация (как в игре)", -2, "grav"); BuffMod(m, "lowgrav");
            m = M(0, "e_climb", "Проворные пальцы", "Лазание быстрее", -1, "", 1.5f, 1.1f, 3f, "0.0", "×"); m.start = x => MulSet(mClm, "e_climb", x.cur); m.stop = x => mClm.Remove("e_climb");
            m = M(0, "e_run", "Лёгкие ноги", "Бег быстрее", -1, "", 1.25f, 1.05f, 2f, "0.00", "×"); m.start = x => MulSet(mSpd, "e_run", x.cur); m.stop = x => mSpd.Remove("e_run");
            m = M(0, "e_regen", "Регенерация", "Лечение раз в N секунд", -2, "heal", 45f, 15f, 120f, "0", " с"); m.tick = (x, dt) => { if (Every(x, dt, x.cur)) Pulse("heal", 1f); };
            m = M(0, "e_immune", "Железный иммунитет", "Статусы (яд, холод, голод…) не набираются", -4, "immune"); m.tick = (x, dt) => { if (Every(x, dt, 1f)) EnsureLock(true); }; m.stop = x => EnsureLock(false);
            m = M(0, "e_invinc", "Режим новичка", "Неуязвимость", -6, "immune"); BuffMod(m, "invinc");
            // ===================== 1. УСЛОЖНЕНИЯ =====================
            m = M(1, "h_heavy", "Тяжёлый рюкзак", "Бег и прыжок слабее", 2, "", 0.85f, 0.5f, 0.95f, "0.00", "×"); m.start = x => { MulSet(mSpd, "h_heavy", x.cur); MulSet(mJmp, "h_heavy", x.cur); }; m.stop = x => { mSpd.Remove("h_heavy"); mJmp.Remove("h_heavy"); };
            m = M(1, "h_grav", "Тяжёлая гравитация", "Падаешь быстрее, прыгаешь ниже", 3, "grav", 1.35f, 1.1f, 2.5f, "0.00", "×"); m.start = x => MulSet(mGrv, "h_grav", x.cur); m.stop = x => mGrv.Remove("h_grav");
            m = M(1, "h_hunger", "Вечный голод", "Голод растёт быстрее", 2, "food", 0.006f, 0.002f, 0.03f, "0.000", "/с"); m.tick = (x, dt) => Drip("Hunger", x.cur, dt);
            m = M(1, "h_weak", "Слабые руки", "Лазание медленнее", 2, "", 0.7f, 0.4f, 0.95f, "0.00", "×"); m.start = x => MulSet(mClm, "h_weak", x.cur); m.stop = x => mClm.Remove("h_weak");
            m = M(1, "h_tired", "Вечная усталость", "Периодическое истощение", 3, "stam", 25f, 10f, 90f, "0", " с"); m.tick = (x, dt) => { if (Every(x, dt, x.cur)) { Pulse("exhaust", 4f); Warn("Накатила усталость…"); } };
            m = M(1, "h_confuse", "Путаница", "Периодический «хаос» в организме", 3, "", 45f, 20f, 120f, "0", " с"); m.tick = (x, dt) => { if (Every(x, dt, x.cur)) { Pulse("chaosaff", 8f); Warn("Голова кругом…"); } };
            m = M(1, "h_poison", "Тлеющий яд", "Яд набирается сам", 2, "", 0.003f, 0.001f, 0.01f, "0.000", "/с"); m.tick = (x, dt) => Drip("Poison", x.cur, dt);
            m = M(1, "h_cold", "Стужа", "Холод растёт, чем выше ты в биоме", 3, "temp", 0.004f, 0.0015f, 0.012f, "0.000", "/с");
            m.tick = (x, dt) => { var me = MeAlive(); if (me == null) return; float rise = Mathf.Clamp((me.transform.position.y - SegBaseY) / 150f, 0f, 2f); Drip("Cold", x.cur * (0.4f + rise), dt); };
            m = M(1, "h_heat", "Пекло", "Жара копится сама", 3, "temp", 0.004f, 0.0015f, 0.012f, "0.000", "/с"); m.tick = (x, dt) => Drip("Hot", x.cur, dt);
            m = M(1, "h_blind", "Слепые пятна", "Периодически темнеет в глазах", 2, "", 70f, 30f, 180f, "0", " с"); m.tick = (x, dt) => { if (Every(x, dt, x.cur)) { Pulse("blind", 4f); Warn("Темнеет в глазах!"); } };
            m = M(1, "h_numb", "Онемение", "Периодическое онемение", 2, "", 80f, 30f, 200f, "0", " с"); m.tick = (x, dt) => { if (Every(x, dt, x.cur)) { Pulse("numb", 5f); Warn("Руки немеют!"); } };
            // ===================== 2. УСЛОВИЯ =====================
            m = M(2, "c_timer", "Таймер биома", "На биом отведено время; после — яд", 3, "", 9f, 3f, 25f, "0", " мин");
            m.tick = (x, dt) => { if (SegT > x.cur * 60f) { x.t += dt; if (x.t > 8f) { x.t = 0f; Status("Poison", 0.08f); Warn("Время биома вышло — яд!"); } } else x.t = 0f; };
            m.hud = x => { float left = x.cur * 60f - SegT; return left > 0 ? "осталось " + Mmss(left) : "ВРЕМЯ ВЫШЛО"; };
            m = M(2, "c_nojump", "Прыгать нельзя", "Каждый прыжок — порция яда", 2, "");
            m.tick = (x, dt) => { var me = MeAlive(); if (me == null) return; var ic = InputOf(me); if (ic == null) return; bool j = R.Get<bool>(ic, "jumpIsPressed"), was = x.o is bool b && b; x.o = j; if (j && !was) { Status("Poison", 0.05f); Warn("Прыгать нельзя!", 1.5f); } };
            m = M(2, "c_tide", "Ядовитый туман снизу", "Туман поднимается по горе — не отставай", 4, "", 4f, 1f, 15f, "0.0", " м/мин");
            m.tick = (x, dt) =>
            {
                var me = MeAlive(); if (me == null) return; float line = SegBaseY - 12f + x.cur * SegT / 60f, d = me.transform.position.y - line; x.t2 = d;
                if (d < 0f) { Drip("Poison", 0.05f, dt); if (Every(x, dt, 4f)) Warn("Туман настиг тебя — лезь выше!"); } else if (d < 10f && Every(x, dt, 6f)) Warn("Туман близко: " + Mathf.RoundToInt(d) + " м");
            };
            m.hud = x => x.t2 < 0 ? "ТЫ В ТУМАНЕ" : "туман в " + Mathf.RoundToInt(x.t2) + " м ниже";
            // ===================== 3. ПРЕДМЕТЫ =====================
            m = M(3, "i_kit", "Стартовый набор", "В начале забега — случайные припасы", -2, "", 3f, 1f, 8f, "0", " шт"); m.start = x => Give(RngS(11), Mathf.RoundToInt(x.cur), "heal", "food", "tool");
            m = M(3, "i_rain", "Дождь из припасов", "Раз в N секунд — предмет с неба", -1, "", 150f, 40f, 400f, "0", " с"); m.tick = (x, dt) => { if (Every(x, dt, x.cur)) Give(RngS((long)Utc()), 1, "food", "any", "heal", "tool"); };
            m = M(3, "i_biome", "Припасы биома", "На входе в биом — еда, лекарство, инструмент", -2); m.biome = (x, s) => Give(RngS(s * 3 + 5), 3, "food", "heal", "tool");
            m = M(3, "i_junk", "Лотерея хлама", "На входе в биом — случайные предметы", 0, "", 2f, 1f, 5f, "0", " шт"); m.biome = (x, s) => Give(RngS(s * 5 + 9), Mathf.RoundToInt(x.cur), "any");
            // ===================== 4. ВИЗУАЛ =====================
            m = M(4, "v_fog", "Густой туман", "Видимость падает", 1, "fog", 0.012f, 0.003f, 0.05f, "0.000"); m.start = x => SetFog(x.cur); m.stop = x => SetFog(-1f);
            m = M(4, "v_dusk", "Сумерки", "Солнце тускнеет", 1, "dusk", 0.45f, 0.15f, 0.9f, "0.00", "×"); m.start = x => MulSet(mLgt, "v_dusk", x.cur); m.stop = x => mLgt.Remove("v_dusk");
            m = M(4, "v_filter", "Фильтр картинки", "Весь мир через шейдер", 0, "shade", 0f, 0f, 10f); m.choices = FilterNames;
            m.start = x => { if (!Dry) Shade.Set(FilterKinds[Mathf.Clamp(Mathf.RoundToInt(x.cur), 0, FilterKinds.Length - 1)], 0f); }; m.stop = x => { if (!Dry) Shade.Off(); };
            m.tick = (x, dt) => { if (Dry) return; var k = FilterKinds[Mathf.Clamp(Mathf.RoundToInt(x.cur), 0, FilterKinds.Length - 1)]; if (Shade.Cur != k && Every(x, dt, 3f)) Shade.Set(k, 0f); };
            m = M(4, "v_tunnel", "Туннельное зрение", "Края экрана размыты", 2, "shade"); m.start = x => { if (!Dry) Shade.Set(ShadeKind.Tunnel, 0f); }; m.stop = x => { if (!Dry) Shade.Off(); };
            m.tick = (x, dt) => { if (Dry) return; if (Shade.Cur != ShadeKind.Tunnel && Every(x, dt, 3f)) Shade.Set(ShadeKind.Tunnel, 0f); };
            m = M(4, "v_fov", "Угол обзора", "Широкий или узкий FOV", 0, "", 95f, 55f, 120f, "0", "°"); m.start = x => SetFov(x.cur); m.stop = x => SetFov(-1f);
            // ===================== 5. ПОТРЕБНОСТИ =====================
            m = M(5, "n_sleep", "Усталость от пути", "Сонливость растёт, пока не дойдёшь до костра (нового биома)", 3, "", 0.006f, 0.002f, 0.02f, "0.000", "/с"); m.tick = (x, dt) => Drip("Drowsy", x.cur * Mathf.Clamp01(SegT / 600f), dt);
            m = M(5, "n_night", "Ночь на горе", "Каждые 4 минуты — ночь: темно и холодно", 3, "", 0.012f, 0.004f, 0.03f, "0.000", "/с");
            m.stop = x => mLgt.Remove("n_night");
            m.tick = (x, dt) => { float ph = SegT % 240f; bool night = ph > 170f; MulSet(mLgt, "n_night", night ? 0.22f : 1f); if (night) Drip("Cold", x.cur, dt); if (night && x.n == 0) { x.n = 1; Warn("Наступает ночь — холодает!"); } if (!night) x.n = 0; };
            m.hud = x => (SegT % 240f) > 170f ? "НОЧЬ, ещё " + Mmss(240f - SegT % 240f) : "до ночи " + Mmss(170f - SegT % 240f);
            m = M(5, "n_meal", "Паёк", "Голод скачет раз в N минут — надо есть", 2, "food", 4f, 2f, 10f, "0", " мин"); m.tick = (x, dt) => { if (Every(x, dt, x.cur * 60f)) { Status("Hunger", 0.2f); Warn("Пора перекусить!"); } };
            m = M(5, "n_warm", "Тепло друг друга", "Один — мёрзнешь; рядом друг (до 5 м) — тепло", 3, "", 0.007f, 0.003f, 0.02f, "0.000", "/с");
            m.tick = (x, dt) => { var me = MeAlive(); if (me == null) return; Component w; float d = NearestOther(me, out w); if (d > 5f) { Drip("Cold", x.cur, dt); if (Every(x, dt, 25f)) Warn("Холодно одному — держись ближе к друзьям"); } };
            // ===================== 6. ЛОВУШКИ =====================
            m = M(6, "t_wind", "Порывы ветра", "Толкает всех в одну сторону (у всех одновременно)", 2, "", 40f, 15f, 120f, "0", " с");
            m.tick = (x, dt) =>
            {
                long slot = (long)(Utc() / x.cur); if (x.o == null) x.o = slot;
                if (slot != (long)x.o) { x.o = slot; var r = RngS(slot * 7 + 3); float a = (float)(r.NextDouble() * Math.PI * 2.0); x.v = new Vector3(Mathf.Cos(a), 0.15f, Mathf.Sin(a)); x.t = 2.2f; Warn("Порыв ветра! Держись!"); }
                if (x.t > 0f) { x.t -= dt; if (x.t <= 0f) { var me = MeAlive(); if (me != null) Push(me, x.v * 4.5f); } }
            };
            m = M(6, "t_rocks", "Камнепад", "Раз в N секунд камень валит одного из игроков", 3, "", 75f, 30f, 200f, "0", " с");
            m.tick = (x, dt) =>
            {
                long slot = (long)(Utc() / x.cur); if (x.o == null) x.o = slot;
                if (slot != (long)x.o)
                {
                    x.o = slot; var acts = ActorsSorted(); if (acts.Count == 0) return; int victim = acts[RngS(slot * 13 + 1).Next(acts.Count)]; x.n = victim; x.t = 2.5f; x.v = new Vector3((float)RngS(slot).NextDouble() - 0.5f, 0f, (float)RngS(slot + 9).NextDouble() - 0.5f);
                    Warn("Камнепад над " + (victim == Game.LocalActor() ? "ТОБОЙ" : Game.PlayerName(victim)) + "!");
                }
                if (x.t > 0f) { x.t -= dt; if (x.t <= 0f && x.n == Game.LocalActor()) { var me = MeAlive(); if (me != null) { Trip(me); Push(me, x.v.normalized * 3f + Vector3.down * 2f); Status("Poison", 0.05f); } } }
            };
            m = M(6, "t_gust", "Ледяные порывы", "Раз в N секунд все получают холод", 2, "", 55f, 20f, 150f, "0", " с");
            m.tick = (x, dt) => { long slot = (long)(Utc() / x.cur); if (x.o == null) x.o = slot; if (slot != (long)x.o) { x.o = slot; Warn("Ледяной порыв!"); Status("Cold", 0.2f); } };
            m = M(6, "t_mines", "Мины-сюрпризы", "В каждом биоме — мины (слабо светятся); наступишь — подбросит", 3, "", 6f, 2f, 14f, "0", " шт");
            m.biome = (x, s) => { Clean(x); if (Dry) return; var pts = FieldPoints(s, Mathf.RoundToInt(x.cur), 101, 8f, 70f, 6f); foreach (var p in pts) { x.pts3.Add(p); x.objs.Add(Marker(p, new Color(1f, 0.2f, 0.15f, 0.55f), 0.7f)); } };
            m.tick = (x, dt) =>
            {
                var me = MeAlive(); if (me == null) return; Vector3 pp = me.transform.position;
                for (int i = x.pts3.Count - 1; i >= 0; i--)
                {
                    Vector3 q = x.pts3[i]; float dx = pp.x - q.x, dz = pp.z - q.z; if (dx * dx + dz * dz > 2.9f || Mathf.Abs(pp.y - q.y) > 2.5f) continue;
                    Vfx.Sparks(q + Vector3.up * 0.3f, Vector3.up, 40, new Color(1f, 0.55f, 0.2f)); Launch(me, 0.8f); Warn("БАХ! Мина!"); if (x.objs[i] != null) UnityEngine.Object.Destroy(x.objs[i]); x.objs.RemoveAt(i); x.pts3.RemoveAt(i);
                }
            };
            m.stop = x => Clean(x);
            m = M(6, "t_zones", "Ядовитые зоны", "В каждом биоме — пятна: споры, паутина, яд, холод", 2, "", 4f, 1f, 10f, "0", " шт");
            m.biome = (x, s) =>
            {
                Clean(x); if (Dry) return; var pts = FieldPoints(s, Mathf.RoundToInt(x.cur), 202, 10f, 75f, 14f); var r = RngS(s * 31 + 7); Color[] cols = { new Color(0.6f, 0.9f, 0.3f, 0.5f), new Color(0.9f, 0.9f, 0.9f, 0.45f), new Color(0.7f, 0.2f, 0.8f, 0.5f), new Color(0.4f, 0.7f, 1f, 0.5f) };
                foreach (var p in pts) { int k = r.Next(4); x.pts3.Add(p); x.kinds.Add(k); x.objs.Add(Marker(p, cols[k], 5.5f)); }
            };
            m.tick = (x, dt) =>
            {
                var me = MeAlive(); if (me == null) return; Vector3 pp = me.transform.position; string[] st = { "Spores", "Web", "Poison", "Cold" };
                for (int i = 0; i < x.pts3.Count; i++) { var z = x.pts3[i]; float dx = pp.x - z.x, dz = pp.z - z.z; if (dx * dx + dz * dz < 30f && Mathf.Abs(pp.y - z.y) < 4f) { Drip(st[x.kinds[i]], 0.05f, dt); if (Every(x, dt, 4f)) Warn("Опасная зона — выходи!"); } }
            };
            m.stop = x => Clean(x);
            // ===================== 7. БОНУСЫ =====================
            m = M(7, "b_camp", "Благословение костра", "В новом биоме — лечение, запас сил и бодрость", -2); m.biome = (x, s) => { Pulse("heal", 1f); Pulse("bonus", 1f); Pulse("energy", 20f); Warn("Благословение костра!"); };
            m = M(7, "b_lucky", "Счастливчик", "Раз в N минут — бесплатный предмет", -1, "", 3f, 1f, 10f, "0", " мин"); m.tick = (x, dt) => { if (Every(x, dt, x.cur * 60f)) Give(RngS((long)Utc()), 1, "food", "tool", "heal", "any"); };
            m = M(7, "b_tail", "Попутный ветер", "Раз в N секунд — 15 секунд бодрости", -1, "", 180f, 60f, 400f, "0", " с"); m.tick = (x, dt) => { if (Every(x, dt, x.cur)) { Pulse("energy", 15f); Warn("Попутный ветер!"); } };
            m = M(7, "b_breath", "Второе дыхание", "Раз в N секунд — запас выносливости", -1, "", 90f, 30f, 240f, "0", " с"); m.tick = (x, dt) => { if (Every(x, dt, x.cur)) { Pulse("bonus", 1f); Warn("Второе дыхание!"); } };
            m = M(7, "b_fast", "Награда за скорость", "Пришёл в новый биом быстрее N минут — лечение и предмет", -1, "", 7f, 3f, 20f, "0", " мин");
            m.biome = (x, s) => { if (x.t2 > 0f && Time.time - x.t2 < x.cur * 60f) { Pulse("heal", 1f); Give(RngS(s + 77), 1, "food", "tool"); Warn("Скоростной бонус!"); } x.t2 = Time.time; };
            // ===================== 8. КОМАНДА =====================
            m = M(8, "c_together", "Держимся вместе", "Отстал от ближайшего друга дальше N м — холод и споры", 2, "", 45f, 20f, 150f, "0", " м");
            m.tick = (x, dt) => { var me = MeAlive(); if (me == null) return; Component w; float d = NearestOther(me, out w); if (d < 1e8f && d > x.cur) { Drip("Cold", 0.015f, dt); Drip("Spores", 0.01f, dt); if (Every(x, dt, 5f)) Warn("Ты отстал от группы: " + Mathf.RoundToInt(d) + " м"); } };
            m = M(8, "c_rope", "Верёвка", "Друзья связаны: слишком далеко — тебя тянет к ним", 2, "", 30f, 12f, 80f, "0", " м");
            m.tick = (x, dt) =>
            {
                var me = MeAlive(); if (me == null) return; Component w; float d = NearestOther(me, out w); if (w == null || d <= x.cur) return;
                Vector3 dir = (w.transform.position - me.transform.position).normalized; float k = Mathf.Clamp((d - x.cur) * 0.5f, 0f, 6f);
                if (Dry) { DryCalls++; return; } foreach (var rb in Game.Rigs(me)) rb.AddForce(dir * k * dt * 2f, ForceMode.VelocityChange); if (Every(x, dt, 4f)) Warn("Верёвка натянулась!");
            };
            m = M(8, "c_roles", "Роли в отряде", "Каждому — своя роль (скаут, силач, санитар…)", 0);
            m.start = x => { x.t = 99f; }; m.stop = x => { mSpd.Remove("role"); mJmp.Remove("role"); mClm.Remove("role"); if (MyRole == 4) Buff("chalk", false); MyRole = -1; };
            m.tick = (x, dt) =>
            {
                x.t += dt; if (x.t >= 5f) { x.t = 0f; var acts = ActorsSorted(); int me = Game.LocalActor(); int idx = acts.IndexOf(me); int role = idx < 0 ? (me + runSeed) % 5 : (idx + runSeed) % 5; if (role < 0) role += 5;
                    if (role != MyRole) { bool hadChalk = MyRole == 4; MyRole = role; mSpd.Remove("role"); mJmp.Remove("role"); mClm.Remove("role"); if (hadChalk) Buff("chalk", false);
                        if (role == 0) { MulSet(mSpd, "role", 1.2f); MulSet(mClm, "role", 1.1f); } else if (role == 1) { MulSet(mJmp, "role", 1.3f); MulSet(mSpd, "role", 0.93f); } else if (role == 4) Buff("chalk", true); Warn("Твоя роль: " + RoleNames[role], 4f); } }
                if (MyRole == 2) { x.t2 += dt; if (x.t2 > 75f) { x.t2 = 0f; Pulse("heal", 1f); } }
            };
            m.biome = (x, s) => { if (MyRole == 3) Give(RngS(s + 5), 1, "food", "tool", "heal"); };
            m.hud = x => MyRole >= 0 ? "твоя роль: " + RoleNames[MyRole] : "роль определяется…";
        }

        static void EnsureLock(bool want)
        {
            if (Dry) { DryCalls++; return; } var me = MeAlive(); if (me == null) return; if (Game.StatusesLocked(me) != want) Game.ToggleLockStatuses();
        }
        static List<int> ActorsSorted() { var l = new List<int>(); foreach (var c in Game.Everyone()) { int a = Game.ActorOf(c); if (a >= 0 && !l.Contains(a)) l.Add(a); } l.Sort(); if (l.Count == 0) l.Add(Game.LocalActor()); return l; }
    }
}
