// Забег: набор модификаторов, синхронизация с друзьями, пресеты, случайный набор, драфт карт, статистика, HUD.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PeakMods
{
    public partial class PeakModsPlugin
    {
        // ---------------- состояние ----------------
        public static bool runOn, runShared; public static int runSeed = 12345, runId, runHost = -1; public static bool runDraft;
        public static int Seg = -99; public static float SegT, SegBaseY; static float segStart, runStart, nextBeat;
        internal static readonly List<Mod> active = new List<Mod>();
        static readonly Dictionary<string, float> sel = new Dictionary<string, float>();      // выбор в меню (до старта)
        static readonly List<string> runLog = new List<string>(); static readonly List<float> segTimes = new List<float>(); static int falls; static float maxAlt, minVy, lastFall; static bool fallArmed;
        public static string LastSummary = "";

        // ---------------- очки и сложность ----------------
        public static int Points(IEnumerable<string> ids) { int s = 0; foreach (var i in ids) { Mod m; if (ById.TryGetValue(i, out m)) s += m.pts; } return s; }
        public static string DiffLabel(int p) { return p <= -6 ? "для отдыха" : p <= -1 ? "лёгкий" : p <= 5 ? "обычный" : p <= 12 ? "сложный" : p <= 20 ? "жёсткий" : "кошмар"; }
        public static Color DiffColor(int p) { return p <= -1 ? Ui.Good : p <= 5 ? Ui.Txt : p <= 12 ? new Color(1f, 0.8f, 0.3f) : p <= 20 ? new Color(1f, 0.55f, 0.25f) : Ui.Bad; }

        public static void SelSet(string id, bool on)
        {
            Mod m; if (!ById.TryGetValue(id, out m)) return;
            if (!on) { sel.Remove(id); return; }
            foreach (var o in All) if (Clash(o, m)) sel.Remove(o.id);
            if (!sel.ContainsKey(id)) sel[id] = m.def;
        }
        public static bool Conflicts(string a, string b) { Mod x, y; return ById.TryGetValue(a, out x) && ById.TryGetValue(b, out y) && Clash(x, y); }

        // ---------------- пресеты ----------------
        internal static readonly string[][] Presets =
        {
            new[] { "Прогулка", "Для спокойного подъёма с друзьями", "e_stam", "e_chalk", "e_food", "e_sun", "b_camp", "i_kit" },
            new[] { "Классика+", "Чуть сложнее обычного, но с наградой", "e_chalk", "b_camp", "i_biome", "h_heavy", "n_meal" },
            new[] { "Выживание", "Холод, ночь, голод и сонливость", "n_sleep", "n_night", "n_warm", "n_meal", "i_biome", "b_camp", "c_together", "v_dusk" },
            new[] { "Командная работа", "Роли, верёвка и награды", "c_roles", "c_rope", "c_together", "n_warm", "b_camp", "i_kit", "b_fast" },
            new[] { "Ночной подъём", "Сумерки, ночное зрение и холод", "v_dusk", "n_night", "v_filter", "i_kit", "n_warm", "t_gust" },
            new[] { "Хаос-лотерея", "Случайные предметы, ветер, путаница", "i_junk", "i_rain", "t_wind", "t_gust", "b_lucky", "h_confuse", "v_filter", "c_roles" },
            new[] { "Кошмар", "Для тех, кто хочет страдать", "h_cold", "h_hunger", "h_tired", "h_confuse", "c_timer", "c_tide", "t_mines", "t_zones", "t_rocks", "t_wind", "v_fog" },
        };
        public static void ApplyPreset(int i)
        {
            var p = Presets[i]; sel.Clear(); for (int k = 2; k < p.Length; k++) SelSet(p[k], true);
            if (sel.ContainsKey("v_filter")) sel["v_filter"] = 5f;      // «ночное зрение»
        }

        // ---------------- случайный набор ----------------
        public static List<string> RollSet(int seed, int target, int count)
        {
            var r = new System.Random(seed); var pool = All.OrderBy(x => r.Next()).ToList(); var res = new List<Mod>(); int sum = 0;
            for (int n = 0; n < count; n++)
            {
                int remain = count - n; float ideal = (target - sum) / (float)remain;
                var cand = pool.Where(x => !res.Contains(x) && !res.Any(y => Clash(y, x))).OrderBy(x => Mathf.Abs(x.pts - ideal) + (float)r.NextDouble() * 1.5f).Take(3).ToList();
                if (cand.Count == 0) break; var pick = cand[r.Next(cand.Count)]; res.Add(pick); sum += pick.pts;
            }
            return res.Select(x => x.id).ToList();
        }

        // ---------------- сериализация ----------------
        static string Ser(IEnumerable<Mod> list) { var sb = new StringBuilder(); foreach (var m in list) sb.Append(';').Append(m.id).Append(':').Append(m.cur.ToString("0.####", CultureInfo.InvariantCulture)); return sb.ToString(); }
        static string SerSel() { var sb = new StringBuilder(); foreach (var kv in sel) { if (sb.Length > 0) sb.Append(';'); sb.Append(kv.Key).Append(':').Append(kv.Value.ToString("0.####", CultureInfo.InvariantCulture)); } return sb.ToString(); }
        static void ParseSel(string s) { sel.Clear(); if (string.IsNullOrEmpty(s)) return; foreach (var part in s.Split(';')) { var kv = part.Split(':'); Mod m; if (kv.Length == 2 && ById.TryGetValue(kv[0], out m)) { float v; if (!float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) v = m.def; sel[kv[0]] = m.max > m.min ? Mathf.Clamp(v, m.min, m.max) : v; } } }

        // ---------------- запуск и остановка ----------------
        public static void StartRun(Dictionary<string, float> set, int seed, bool draft, bool broadcast, int id = 0, int host = -2)
        {
            if (runOn) StopRun(false, false);
            runSeed = seed; runDraft = draft; runId = id != 0 ? id : UnityEngine.Random.Range(1, int.MaxValue); runHost = host != -2 ? host : Game.LocalActor(); runOn = true; runShared = broadcast || host != -2; runStart = Time.time; nextBeat = Time.time + 15f;
            active.Clear(); runLog.Clear(); segTimes.Clear(); falls = 0; maxAlt = -1e9f; lastFall = -99f; drips.Clear(); draftCount = 0; draftOpen = false; MyRole = -1;
            Seg = SegCur(); segStart = Time.time; SetBaseY(Seg);
            LoadItemsSafe();
            foreach (var kv in set) ActivateMod(kv.Key, kv.Value);
            runLog.Add("старт: " + string.Join(", ", active.Select(m => m.name).ToArray()));
            Ui.Toast("ЗАБЕГ НАЧАЛСЯ: " + active.Count + " мод., сложность " + DiffLabel(Points(active.Select(m => m.id))), Ui.Accent);
            if (broadcast && Game.InRoom) Net.Send("run", RunMsg());
            if (draft && runHost == Game.LocalActor()) OfferDraft();
        }
        static void LoadItemsSafe() { try { if (!itemsLoaded) LoadItems(); } catch { } }
        static string RunMsg() { return runId + "," + runSeed + "," + runHost + "," + (runDraft ? 1 : 0) + Ser(active); }

        static void ActivateMod(string id, float p)
        {
            Mod m; if (!ById.TryGetValue(id, out m) || active.Contains(m)) return;
            m.cur = m.max > m.min ? Mathf.Clamp(p, m.min, m.max) : p; m.t = m.t2 = 0f; m.n = 0; m.o = null; m.errors = 0; m.broken = false; active.Add(m);
            try { if (m.start != null) m.start(m); } catch (Exception e) { Log("start " + id, e); m.errors++; }
            if ((id == "t_mines" || id == "t_zones") && Seg >= 0) { try { m.biome(m, Seg); } catch (Exception e) { Log("biome " + id, e); } }
        }

        public static void StopRun(bool broadcast, bool summary = true)
        {
            if (!runOn) return; if (broadcast && runShared && Game.InRoom) Net.Send("stop", "");
            if (summary) WriteSummary();
            foreach (var m in active) { try { if (m.stop != null) m.stop(m); } catch (Exception e) { Log("stop " + m.id, e); } m.objs.Clear(); }
            active.Clear(); runOn = false; draftOpen = false; mSpd.Clear(); mJmp.Clear(); mClm.Clear(); mGrv.Clear(); mLgt.Clear(); wantBuff.Clear(); SetFog(-1f); SetFov(-1f); MyRole = -1;
            var me = Me(); if (me != null) { try { ApplyMovement(me, true); } catch { } }
            if (gravKnown) Physics.gravity = baseGravity;
        }

        static void SetBaseY(int seg) { Vector3 a; var me = Me(); if (SegAnchor(seg, out a)) SegBaseY = a.y; else SegBaseY = me != null ? me.transform.position.y : 0f; }

        // ---------------- главный тик ----------------
        public void RunTick(float dt)
        {
            if (!runOn) return; var me = MeAlive(); if (me == null) return;
            int s = SegCur(); if (s >= 0 && s != Seg) OnBiome(s);
            SegT = Time.time - segStart; maxAlt = Mathf.Max(maxAlt, me.transform.position.y);
            TickBuffs(me); try { ApplyMovement(me, false); } catch (Exception e) { Log("движение", e); }
            foreach (var m in active.ToArray())
            {
                if (m.broken || m.tick == null) continue;
                try { m.tick(m, dt); } catch (Exception e) { Log("tick " + m.id, e); if (++m.errors >= 8) { m.broken = true; runLog.Add("модификатор «" + m.name + "» отключён после ошибок: " + e.Message); } }
            }
            TrackFalls(me);
            if (runShared && runHost == Game.LocalActor() && Game.InRoom && Time.time >= nextBeat) { nextBeat = Time.time + 15f; Net.Send("run", RunMsg()); }
            TickDraft();
        }

        void OnBiome(int s)
        {
            if (Seg >= 0 || Seg == -99) { if (Seg != -99) segTimes.Add(Time.time - segStart); }
            runLog.Add("биом " + s + " (" + Mmss(Time.time - runStart) + ")"); Seg = s; segStart = Time.time; SegT = 0f; SetBaseY(s);
            foreach (var m in active.ToArray()) { if (m.biome == null || m.broken) continue; try { m.biome(m, s); } catch (Exception e) { Log("biome " + m.id, e); } }
            Ui.Toast("Новый биом: " + (s + 1), Ui.Accent2);
            if (runDraft && runHost == Game.LocalActor() && s <= 5) OfferDraft();
        }

        static void TrackFalls(Component me)
        {
            float vy = 0f; foreach (var rb in Game.Rigs(me)) { vy = rb.GetPointVelocity(rb.position).y; break; }
            if (vy < -16f) fallArmed = true; if (fallArmed && vy > -3f && Time.time - lastFall > 3f) { falls++; lastFall = Time.time; fallArmed = false; } if (vy > -3f && Time.time - lastFall > 3f && vy > 0f) fallArmed = false;
        }

        static void WriteSummary()
        {
            var sb = new StringBuilder(); float total = Time.time - runStart;
            sb.AppendLine("PeakMods — итог забега (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
            sb.AppendLine("Время: " + Mmss(total) + "; сложность: " + DiffLabel(Points(active.Select(m => m.id))) + " (" + Points(active.Select(m => m.id)) + "); seed " + runSeed);
            sb.AppendLine("Падений (у меня): " + falls + "; максимальная высота: " + Mathf.RoundToInt(maxAlt) + " м");
            for (int i = 0; i < segTimes.Count; i++) sb.AppendLine("Биом " + (i + 1) + ": " + Mmss(segTimes[i]));
            sb.AppendLine("Модификаторы:"); foreach (var m in active) sb.AppendLine("  • " + m.name + (m.HasParam ? " [" + m.ParamText() + "]" : "") + (m.broken ? "  (отключён из-за ошибок)" : ""));
            sb.AppendLine("Журнал:"); foreach (var l in runLog) sb.AppendLine("  " + l);
            LastSummary = sb.ToString();
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "PeakMods_run_last.txt"), LastSummary); } catch { }
            Ui.Toast("Забег окончен: " + Mmss(total) + ", падений " + falls + " (итог в BepInEx\\PeakMods_run_last.txt)", Ui.Accent);
        }

        // ---------------- сеть ----------------
        void OnNet(string cmd, string arg, int sender)
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                if (cmd == "run")
                {
                    if (!cfgAccept.Value) return; var parts = arg.Split(';'); var h = parts[0].Split(','); int id = int.Parse(h[0]), seed = int.Parse(h[1]), host = int.Parse(h[2]); bool dr = h[3] == "1";
                    var set = new Dictionary<string, float>(); var order = new List<string>();
                    for (int i = 1; i < parts.Length; i++) { var kv = parts[i].Split(':'); Mod m; if (kv.Length == 2 && ById.TryGetValue(kv[0], out m)) { set[kv[0]] = float.Parse(kv[1], inv); order.Add(kv[0]); } }
                    if (runOn && id == runId) { foreach (var kv in set) if (!active.Any(a => a.id == kv.Key)) { ActivateMod(kv.Key, kv.Value); Ui.Toast("К набору добавлено: " + ById[kv.Key].name, Ui.Accent2); } return; }
                    StartRun(set, seed, dr, false, id, host); Ui.Toast(Game.PlayerName(sender) + " запустил забег — набор у всех одинаковый", Ui.Accent); return;
                }
                if (cmd == "stop") { if (runOn) StopRun(false); return; }
                if (cmd == "offer") { var p = arg.Split(','); ShowDraft(int.Parse(p[0]), new[] { p[1], p[2], p[3] }); return; }
                if (cmd == "vote") { var p = arg.Split(','); if (draftOpen && int.Parse(p[0]) == draftId) votes[sender] = int.Parse(p[1]); return; }
                if (cmd == "pick") { var p = arg.Split(','); if (draftOpen && int.Parse(p[0]) == draftId) ResolveDraftLocal(int.Parse(p[1])); return; }
            }
            catch (Exception e) { Log("сеть " + cmd, e); }
        }

        // ---------------- драфт карт ----------------
        static bool draftOpen; static int draftId, draftCount, myVote = -1; static string[] draftIds = new string[3]; static float draftT0; static readonly Dictionary<int, int> votes = new Dictionary<int, int>();
        static readonly int[] BoonCats = { 0, 3, 7 }, CurseCats = { 1, 2, 5, 6 };

        static string[] BuildOffer(int salt)
        {
            var r = RngS(salt * 97L + 11); var res = new List<Mod>();
            Func<int[], Mod> take = cats =>
            {
                var c = All.Where(m => cats.Contains(m.c) && !active.Contains(m) && !res.Contains(m) && !res.Any(y => Clash(y, m)) && !active.Any(y => Clash(y, m))).ToList();
                return c.Count == 0 ? null : c[r.Next(c.Count)];
            };
            var a = take(BoonCats); if (a != null) res.Add(a); var b = take(CurseCats); if (b != null) res.Add(b);
            var w = take(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }); if (w != null) res.Add(w);
            while (res.Count < 3) { var x = take(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }); if (x == null) break; res.Add(x); }
            return res.Select(m => m.id).ToArray();
        }

        static void OfferDraft()
        {
            var o = BuildOffer(draftCount + runSeed); if (o.Length < 3) return; draftCount++; int id = draftCount * 1000 + UnityEngine.Random.Range(1, 999);
            ShowDraft(id, o); if (runShared && Game.InRoom) Net.Send("offer", id + "," + o[0] + "," + o[1] + "," + o[2]);
        }
        static void ShowDraft(int id, string[] ids) { draftId = id; draftIds = ids; draftOpen = true; draftT0 = Time.time; myVote = -1; votes.Clear(); Ui.Toast("Выбор карты! Кликни по одной из трёх", Ui.Accent2); }
        static void ClickDraft(int idx)
        {
            if (!draftOpen || myVote >= 0) return; myVote = idx; votes[Game.LocalActor()] = idx;
            if (runShared && runHost != Game.LocalActor() && Game.InRoom) Net.Send("vote", draftId + "," + idx);
        }
        static void TickDraft()
        {
            if (!draftOpen) return;
            if (runHost == Game.LocalActor() || !Game.InRoom)
            {
                int alive = runShared ? Math.Max(1, Game.Everyone(true).Count) : 1; if (votes.Count >= alive || Time.time - draftT0 > 30f)
                {
                    var tally = new int[3]; foreach (var v in votes.Values) if (v >= 0 && v < 3) tally[v]++; int best = tally.Max(); var tied = Enumerable.Range(0, 3).Where(i => tally[i] == best).ToList(); int pick = tied[RngS(draftId).Next(tied.Count)];
                    if (runShared && Game.InRoom) Net.Send("pick", draftId + "," + pick); ResolveDraftLocal(pick);
                }
            }
            else if (Time.time - draftT0 > 60f) draftOpen = false;
        }
        static void ResolveDraftLocal(int idx)
        {
            if (!draftOpen) return; draftOpen = false; idx = Mathf.Clamp(idx, 0, 2); string id = draftIds[idx]; Mod m; if (!ById.TryGetValue(id, out m)) return;
            ActivateMod(id, m.def); runLog.Add("карта: " + m.name); Ui.Toast("Выбрана карта: " + m.name, CatCols[m.c]); Warn("Новая карта: " + m.name, 4f);
        }

        // ---------------- HUD ----------------
        int hudMode = 1;         // 0 скрыт, 1 кратко, 2 подробно
        void DrawRunHud(float s)
        {
            if (runOn && hudMode > 0 && Event.current.type == EventType.Repaint)
            {
                int pts = Points(active.Select(m => m.id)); var lines = new List<KeyValuePair<string, Color>>();
                foreach (var m in active) { string t = m.name; if (hudMode == 2 && m.HasParam) t += " [" + m.ParamText() + "]"; string extra = null; try { if (m.hud != null) extra = m.hud(m); } catch { } if (extra != null) t += " — " + extra; if (m.broken) t += " (сбой)"; lines.Add(new KeyValuePair<string, Color>(t, m.broken ? Ui.Muted : CatCols[m.c])); }
                if (hudMode == 1 && lines.Count > 9) { int extra = lines.Count - 8; lines = lines.Take(8).ToList(); lines.Add(new KeyValuePair<string, Color>("… и ещё " + extra + " (F2 — подробнее)", Ui.Muted)); }
                float w = 340f * s, h = (62f + lines.Count * 20f) * s, x = 18f * s, y = 18f * s;
                Ui.Box(new Rect(x, y, w, h), new Color(0.04f, 0.05f, 0.1f, 0.7f), 12f * s);
                Ui.Text(new Rect(x + 12f * s, y + 5f * s, w - 24f * s, 24f * s), "ЗАБЕГ · " + DiffLabel(pts) + " (" + (pts > 0 ? "+" : "") + pts + ")", (int)(16f * s), DiffColor(pts), TextAnchor.MiddleLeft, true);
                Ui.Text(new Rect(x + 12f * s, y + 5f * s, w - 24f * s, 24f * s), Mmss(Time.time - runStart), (int)(15f * s), Ui.Txt, TextAnchor.MiddleRight, true);
                Ui.Text(new Rect(x + 12f * s, y + 29f * s, w - 24f * s, 20f * s), (Seg >= 0 ? "биом " + (Seg + 1) + ": " + Mmss(SegT) : "биом не определён") + (runDraft ? "  ·  драфт" : ""), (int)(13f * s), Ui.Muted, TextAnchor.MiddleLeft, false);
                for (int i = 0; i < lines.Count; i++) Ui.Text(new Rect(x + 12f * s, y + 54f * s + i * 20f * s, w - 24f * s, 20f * s), "● " + lines[i].Key, (int)(13f * s), lines[i].Value, TextAnchor.MiddleLeft, false);
            }
            if (Time.time < RunWarnUntil && RunWarn != "" && Event.current.type == EventType.Repaint)
            {
                float a = Mathf.Clamp01((RunWarnUntil - Time.time) / 0.6f); Ui.Text(new Rect(0, Screen.height * 0.2f, Screen.width, 50f * s), RunWarn, (int)(30f * s), new Color(1f, 0.9f, 0.5f, a), TextAnchor.MiddleCenter, true);
            }
            if (draftOpen) DrawDraft(s);
        }

        void DrawDraft(float s)
        {
            float cw = 300f * s, ch = 210f * s, gap = 22f * s, tot = cw * 3 + gap * 2, x0 = Screen.width * 0.5f - tot * 0.5f, y0 = Screen.height * 0.5f - ch * 0.5f; var e = Event.current;
            if (e.type == EventType.Repaint) { Ui.Box(new Rect(x0 - 24f * s, y0 - 70f * s, tot + 48f * s, ch + 120f * s), new Color(0.03f, 0.04f, 0.09f, 0.88f), 20f * s); Ui.Text(new Rect(x0, y0 - 62f * s, tot, 40f * s), "ВЫБЕРИ КАРТУ ДЛЯ ВСЕГО ОТРЯДА", (int)(26f * s), Ui.Accent, TextAnchor.MiddleCenter, true); }
            for (int i = 0; i < 3; i++)
            {
                Mod m; if (!ById.TryGetValue(draftIds[i] ?? "", out m)) continue; var r = new Rect(x0 + i * (cw + gap), y0, cw, ch); bool mine = myVote == i; var col = CatCols[m.c];
                if (e.type == EventType.Repaint)
                {
                    Ui.Box(r, new Color(col.r * 0.25f, col.g * 0.25f, col.b * 0.25f, 0.97f), 16f * s); if (mine || r.Contains(e.mousePosition)) Ui.Box(new Rect(r.x - 3f * s, r.y - 3f * s, r.width + 6f * s, r.height + 6f * s), new Color(col.r, col.g, col.b, mine ? 0.35f : 0.18f), 18f * s);
                    Ui.Text(new Rect(r.x + 14f * s, r.y + 10f * s, r.width - 28f * s, 22f * s), m.cat.ToUpper() + (m.pts != 0 ? "   " + (m.pts > 0 ? "+" : "") + m.pts : ""), (int)(13f * s), col, TextAnchor.MiddleLeft, true);
                    Ui.Text(new Rect(r.x + 14f * s, r.y + 38f * s, r.width - 28f * s, 56f * s), m.name, (int)(24f * s), Color.white, TextAnchor.MiddleLeft, true);
                    Ui.TextWrap(new Rect(r.x + 14f * s, r.y + 100f * s, r.width - 28f * s, 80f * s), m.desc, (int)(15f * s), Ui.Txt);
                    int v = 0; foreach (var kv in votes) if (kv.Value == i) v++; Ui.Text(new Rect(r.x + 14f * s, r.y + ch - 32f * s, r.width - 28f * s, 24f * s), mine ? "✔ твой голос" : (v > 0 ? "голосов: " + v : "кликни, чтобы выбрать"), (int)(14f * s), mine ? Ui.Good : Ui.Muted, TextAnchor.MiddleLeft, false);
                }
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { ClickDraft(i); e.Use(); }
            }
            if (e.type == EventType.Repaint) Ui.Text(new Rect(x0, y0 + ch + 14f * s, tot, 30f * s), "голосов " + votes.Count + " · решение через " + Mathf.Max(0, 30 - Mathf.RoundToInt(Time.time - draftT0)) + " с", (int)(16f * s), Ui.Muted, TextAnchor.MiddleCenter, false);
        }
    }
}
