// Прохождение Backrooms: вход всем отрядом, смена уровней, поимка, сеть, HUD.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PeakBR
{
    internal static class Play
    {
        public static bool On, Busy, Catching, CamFxOn = true; public static int RunId, Seed, Level, SizeIdx = 2, Diff = 2, Deaths; public static Vector3 BaseP, HomePos; public static Built Cur; public static LevelDef L;
        public static float EnterT, LevelT0, Prox, Glitch, TapeEnd, Fade, FadeTarget, RunStart, TitleT = -99f, ResultT = -99f; public static string Result = "", CatchText = "";
        public static bool CatchCam; public static Vector3 CatchPos; public static Quaternion CatchRot;
        public static readonly float[] SizeMul = { 0.7f, 1f, 1.5f, 2f }; public static readonly string[] SizeName = { "Малая", "Обычная", "Огромная", "Безумная" };
        public static readonly string[] DiffName = { "Лёгкая", "Средняя", "Жёсткая" }; static readonly float[] DiffVs = { 0.86f, 1f, 1.1f };
        public static bool AcceptRun = true, OptOut, AutoJoin = true; public static float FogMul = 1f; public static string RunArg = ""; static float joinT, reportAt, ambNext, moteT; public static KeyCode PanicKey = KeyCode.End;
        static readonly Dictionary<int, float> hb = new Dictionary<int, float>(); static readonly Dictionary<int, float> immune = new Dictionary<int, float>();
        static float deadT, hbT, snapT, snT, nextHowl, doorT, ignNext, lastRescue, hazT, mothCd, fadeSpeed = 2f, blackout; static AudioSource ambient; static string exitInfo = ""; static int lastDoorNear;
        public static string StatusLine = "";
        static MonoBehaviour Mono { get { return Plugin.Inst; } }

        // ================= хост =================
        public static int HostActor()
        {
            int me = Game.LocalActor(); if (!Game.InRoom) return me; int best = me; float now = Time.time;
            foreach (var kv in hb) if (now - kv.Value < 5f && kv.Key < best) best = kv.Key; return best;
        }
        public static bool IsHost { get { return HostActor() == Game.LocalActor(); } }

        // ================= старт =================
        /// <summary>Нажата кнопка «Войти»: хост рассылает команду всему отряду и входит сам.</summary>
        public static void StartRun(int startLevel)
        {
            if (Dormant) return;
            if (On || Busy) return; var me = Util.MeAlive(); if (me == null) { Ui.Toast("Нужно быть живым и в игре", Ui.Bad); return; }
            Island.Hide(); int seed = UnityEngine.Random.Range(1000, 900000); Vector3 pc = Util.CenterOf(me); int maxN = 32;
            foreach (var d in Levels.All) maxN = Mathf.Max(maxN, LevelN(d, SizeIdx));
            float W = maxN * 4f; Vector3 b0 = new Vector3(pc.x - W * 0.5f, pc.y + 230f, pc.z - W * 0.5f); Vector3 bp = Foreign.PickBase(b0, maxN, 6f);
            string arg = string.Join(",", new[] { seed.ToString(), SizeIdx.ToString(), Diff.ToString(), CamFxOn ? "1" : "0", startLevel.ToString(), bp.x.ToString("0.0", CultureInfo.InvariantCulture), bp.y.ToString("0.0", CultureInfo.InvariantCulture), bp.z.ToString("0.0", CultureInfo.InvariantCulture) });
            OptOut = false; Net.Send("go", arg); Begin(arg);
        }
        static void Begin(string arg)
        {
            try
            {
                RunArg = arg; var f = arg.Split(','); Seed = int.Parse(f[0]); SizeIdx = Mathf.Clamp(int.Parse(f[1]), 0, 3); Diff = Mathf.Clamp(int.Parse(f[2]), 0, 2); CamFxOn = f[3] == "1"; int st = Mathf.Clamp(int.Parse(f[4]), 0, Levels.All.Length - 1);
                BaseP = new Vector3(float.Parse(f[5], CultureInfo.InvariantCulture), float.Parse(f[6], CultureInfo.InvariantCulture), float.Parse(f[7], CultureInfo.InvariantCulture));
                var me = Game.Me(); if (me == null) return; Island.Hide(); Loot.Reset(); HomePos = Util.CenterOf(me); On = true; Deaths = 0; RunStart = Time.time; EnterT = Time.time; Catching = false; hb[Game.LocalActor()] = Time.time; Net.Send("hb", "1"); hbT = Time.time + 1f;
                CamFx.Install(); CamFx.ExtraActive = () => Catching && CatchCam; CamFx.ExtraApply = ApplyCatchCam; Mono.StartCoroutine(CoEnter(st, true));
                Ui.Toast("Backrooms: вход через несколько секунд…", Ui.Accent);
            }
            catch (Exception e) { Plugin.Warn("begin", e); On = false; Island.Restore(); }
        }

        public static int LevelN(LevelDef d, int sizeIdx)
        {
            int n = Mathf.RoundToInt(d.baseN * SizeMul[Mathf.Clamp(sizeIdx, 0, 3)]);
            return d.style == Style.Snake ? Mathf.Clamp(n, 16, 32) : Mathf.Clamp(n, 24, 150);
        }

        // ================= смена уровня =================
        static IEnumerator CoEnter(int idx, bool first)
        {
            Busy = true; fadeSpeed = 2.2f; FadeTarget = 1f; NetGuard.On(); yield return new WaitForSeconds(0.55f);
            try { CleanLevel(); Tex.Clear(); } catch (Exception e) { Plugin.Warn("clean", e); }
            L = Levels.All[idx]; Level = idx; int n = LevelN(L, SizeIdx); int lseed = Seed * 131 + idx * 7919;
            try
            {
                var swb = System.Diagnostics.Stopwatch.StartNew(); var g = Grid.Make(L, n, lseed); Cur = LevelBuilder.Build(g, L, BaseP, lseed, L.dark); Cur.rig.Disturb = Ents.LampDisturb; Cur.rig.Mul = 0f; Env.Apply(L); Refl.Start(L); Atmos.Start(Cur, L); ApplyQuality();
                Ents.DiffV = DiffVs[Diff]; Ents.Spawn(Cur, lseed, SizeMul[SizeIdx]); Loot.Build(Cur, lseed); ambient = Snd2.Ambient(L.humKind, 0.3f); Plugin.Log("Уровень " + L.id + ": построен за " + swb.ElapsedMilliseconds + " мс (одним кадром — игра в это время стоит)");
            }
            catch (Exception e) { Plugin.Warn("build " + idx, e); }
            yield return null;
            if (Cur != null) { Cur.Cull(Cur.spawn, CullR()); Util.Warp(Cur.spawn + Vector3.up * 1.3f); }
            yield return new WaitForSeconds(0.3f);
            var me = Game.Me();
            if (me != null && Cur != null)
            {
                Foreign.Scan(me, Cur.baseP + new Vector3(Cur.n * 2f, Cur.H * 0.5f, Cur.n * 2f), new Vector3(Cur.n * 2f + 12f, Cur.H * 0.5f + 40f, Cur.n * 2f + 12f));
                if (Game.IsDead(me)) Game.Rpc(me, "RPCA_ReviveAtPosition", Cur.spawn + Vector3.up * 1.3f, false, -1);
                if (Cur != null) Util.Warp(Cur.spawn + Vector3.up * 1.3f);
            }
            reportAt = Time.time + 6f; LevelT0 = Time.time; Arrow.Reset(); doorT = 0f; ignNext = Time.time + 1f; nextHowl = Time.time + UnityEngine.Random.Range(60f, 110f); TitleT = Time.time; Glitch = 0f; Prox = 0f; blackout = 0f; Ents.LevelT = Time.time;
            if (first) { EnterT = Time.time; if (CamFxOn) { try { Shade.Set(ShadeKind.Camera, 0f); } catch { } } }
            if (Cur != null) Snd2.Flat(idx == 0 && first ? "boom" : "ding", 0.6f);
            fadeSpeed = 0.9f; FadeTarget = 0f; Busy = false;
        }

        static float cullT;
        /// <summary>Дальность прорисовки кусков уровня: где туман уже почти непрозрачен, куски выключаются.</summary>
        public static int Qual; static float fpsAvg = 60f, lowT; static readonly float[] QualMul = { 1f, 1f, 0.75f, 0.55f };
        /// <summary>Авто-оптимизация: если FPS долго низкий, поэтапно снижаем качество (отражения → тени → дальность прорисовки).</summary>
        static void AutoQ(float dt)
        {
            fpsAvg = Mathf.Lerp(fpsAvg, 1f / Mathf.Max(0.001f, Time.unscaledDeltaTime), 0.04f); if (Busy || Fade > 0.4f || Qual >= 3) return;
            if (fpsAvg < 42f) lowT += dt; else lowT = Mathf.Max(0f, lowT - dt * 2f);
            if (lowT > 6f) { lowT = 0f; Qual++; ApplyQuality(); Ui.Toast("Авто-оптимизация: качество снижено (ступень " + Qual + "/3, FPS ~" + Mathf.RoundToInt(fpsAvg) + ")", Ui.Accent); Plugin.Log("Авто-оптимизация: ступень " + Qual + ", FPS " + fpsAvg.ToString("0")); }
        }
        static void ApplyQuality()
        {
            if (Qual >= 1) { Refl.On = false; try { Refl.Stop(); } catch { } }
            if (Cur != null) Cur.rig.SetShadows(Qual >= 3 ? 0 : (Qual == 2 ? 1 : 2));
        }
        public static float CullR() { if (L == null) return 130f; float rho = Mathf.Max(0.004f, L.fogDen * FogMul * (BrGfx.LitOk ? 1f : 1.4f)); float r = Mathf.Clamp(3.2f / rho, 50f, 130f); return Dev.Bright ? 130f : r * QualMul[Mathf.Clamp(Qual, 0, 3)]; }
        static void CleanLevel()
        {
            Dread.Reset(); Water.Reset(); Ents.Clear(); Loot.Clear(); if (Cur != null) Cur.Destroy(); Cur = null; if (ambient != null) UnityEngine.Object.Destroy(ambient.gameObject); ambient = null; BrSnd.Stop(); Atmos.Stop(); Refl.Stop();
        }

        // ================= выход / финиш =================
        public static void Leave(bool win, string why)
        {
            if (!On || Busy) return; if (!win) OptOut = true; Mono.StartCoroutine(CoFinish(win, why));
        }
        static IEnumerator CoFinish(bool win, string why)
        {
            Busy = true; fadeSpeed = 2f; FadeTarget = 1f; yield return new WaitForSeconds(0.6f);
            int reached = Level; float secs = Time.time - RunStart;
            try { CleanLevel(); Torch.Stop(); NetGuard.Off(); Env.Restore(); Dev.Reset(); Foreign.Restore(); Island.Restore(); try { if (Shade.Cur == ShadeKind.Camera) Shade.Off(); } catch { } CamFx.Boost = 0f; Tex.Clear(); Snd2.Clear(); }
            catch (Exception e) { Plugin.Warn("finish", e); }
            var me = Game.Me();
            if (me != null) { Util.Warp(HomePos + Vector3.up * 1.0f); if (Game.IsDead(me)) Game.Rpc(me, "RPCA_ReviveAtPosition", HomePos + Vector3.up * 1.5f, false, -1); }
            On = false; Catching = false; CatchCam = false; InputBlock.Want = false;
            Result = (win ? "ПОБЕДА! Ты прошёл все " + Levels.All.Length + " уровней Backrooms." : "Ты покинул Backrooms (" + why + ").") + "\nДошёл до: " + Levels.All[Mathf.Clamp(reached, 0, Levels.All.Length - 1)].name + "\nВремя: " + FmtTime(secs) + " · поймали раз: " + Deaths + " · карта: " + SizeName[SizeIdx] + " · сложность: " + DiffName[Diff] + "\nЯщиков открыто: " + Loot.Opened + " · предметов найдено: " + Loot.Found;
            ResultT = Time.time; try { File.WriteAllText(Path.Combine(BepInEx.Paths.BepInExRootPath, "PeakBackrooms_run_last.txt"), Result + "\n"); } catch { }
            Snd2.Flat(win ? "ding" : "boom", 0.7f); fadeSpeed = 0.8f; FadeTarget = 0f; Busy = false;
        }
        static string FmtTime(float s) { var t = TimeSpan.FromSeconds(s); return string.Format("{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds); }

        // ================= сеть =================
        public static bool Dormant;   // копия мода отключена (рядом стоит другая сборка)
        public static void OnNet(string cmd, string arg, int sender)
        {
            if (Dormant) return;
            try
            {
                switch (cmd)
                {
                    case "go": if (AcceptRun && !On && !Busy) { hb[sender] = Time.time; OptOut = false; Begin(arg); } break;
                    case "crate": case "glow": case "lure": Loot.OnNet(cmd, arg); break;
                    case "join": if (AcceptRun && AutoJoin && !OptOut && !On && !Busy) { hb[sender] = Time.time; Begin(arg); } break;
                    case "hb": hb[sender] = Time.time; break;
                    case "ent": if (On && !Busy && Cur != null && sender <= Game.LocalActor()) { hb[sender] = Time.time; if (sender < Game.LocalActor()) Ents.Apply(arg); } break;
                    case "sn": if (On && Cur != null && !string.IsNullOrEmpty(arg)) foreach (var s in arg.Split(',')) { int id; if (int.TryParse(s, out id)) Ents.MarkSeen(id); } break;
                    case "ct": if (On && Cur != null) { int id; int.TryParse(arg, out id); if (id >= 0 && id < Ents.All.Count) OnCaught(Ents.All[id].kind); } break;
                    case "lv": if (On && !Busy) { int idx; if (int.TryParse(arg, out idx) && idx == Level + 1 && idx < Levels.All.Length) Mono.StartCoroutine(CoEnter(idx, false)); } break;
#if DEV
                    case "lg": if (On && !Busy) { int idx; if (int.TryParse(arg, out idx) && idx >= 0 && idx < Levels.All.Length && idx != Level) Mono.StartCoroutine(CoEnter(idx, false)); } break;
#endif
                    case "fin": if (On && !Busy) Leave(true, "финал"); break;
                    case "ev": if (On && Cur != null && arg == "howl") DoHowl(); break;
                }
            }
            catch (Exception e) { Plugin.Warn("net " + cmd, e); }
        }

        // ================= игроки =================
        static readonly List<PL> pls = new List<PL>();
        static readonly Dictionary<int, Vector3> spPos = new Dictionary<int, Vector3>(); static readonly Dictionary<int, float> spVal = new Dictionary<int, float>(); static readonly Dictionary<int, float> spT = new Dictionary<int, float>();
        static float PlSpeed(int a, Vector3 p, float now)
        {
            Vector3 pp; float pt, v; if (!spPos.TryGetValue(a, out pp) || !spT.TryGetValue(a, out pt)) { spPos[a] = p; spT[a] = now; spVal[a] = 0f; return 0f; }
            float dt = now - pt; spVal.TryGetValue(a, out v); if (dt < 0.0001f) return v; float inst = Util.Flat(p - pp).magnitude / dt; if (inst > 25f) inst = 0f; v = Mathf.Lerp(v, inst, 1f - Mathf.Exp(-dt * 5f));
            spPos[a] = p; spT[a] = now; spVal[a] = v; if (a == Game.LocalActor()) { if (v > Ents.SpeedRef) Ents.SpeedRef = Mathf.Lerp(Ents.SpeedRef, v, 0.1f); Ents.LocalLoud = v > Ents.LoudSpd; } return v;
        }
        static List<PL> Pls()
        {
            pls.Clear(); if (Cur == null) return pls; int me = Game.LocalActor(); float now = Time.time;
            foreach (var c in Game.Everyone(true))
            {
                int a = Game.ActorOf(c); float t; bool part = a == me || (hb.TryGetValue(a, out t) && now - t < 6f); if (!part) continue; Vector3 p = Util.CenterOf(c);
                if (!Cur.Inside(p)) continue; pls.Add(new PL { c = c, actor = a, pos = p, crouch = Util.Crouching(c), spd = PlSpeed(a, p, now) });
            }
            return pls;
        }

        // ================= кадр =================
        public static void Tick(float dt)
        {
            Fade = Mathf.MoveTowards(Fade, FadeTarget, dt * fadeSpeed); TapeEnd = Mathf.MoveTowards(TapeEnd, 0f, dt * 0.8f); Glitch = Mathf.MoveTowards(Glitch, 0f, dt * 0.5f); CamFx.Boost = Mathf.MoveTowards(CamFx.Boost, 0f, dt * 1.2f);
            if (!On || Busy || Cur == null) { if (!On) { if (Fade > 0f && FadeTarget == 0f) Fade = Mathf.MoveTowards(Fade, 0f, dt * 2f); } return; }
            var me = Game.Me(); if (me == null) return; float now = Time.time; Vector3 pc = Util.CenterOf(me);
            if (now > hbT) { hbT = now + 1f; Net.Send("hb", "1"); hb[Game.LocalActor()] = now; }
            if (Keys.Down(PanicKey) && !Catching) { Leave(false, "по кнопке " + PanicKey); return; }
            if (Game.IsDead(me)) { deadT += dt; if (deadT > 4f && !Catching) { deadT = 0f; Deaths++; Game.Rpc(me, "RPCA_ReviveAtPosition", Cur.spawn + Vector3.up * 1.3f, false, -1); Util.Warp(Cur.spawn + Vector3.up * 1.3f); Ui.Toast("Ты погиб(ла) — возврат в начало уровня", Ui.Bad); } } else deadT = 0f;
            if (!Cur.Inside(pc) && !Catching && !Game.IsDead(me) && now > lastRescue) { lastRescue = now + 3f; Util.Warp(Cur.spawn + Vector3.up * 1.3f); Ui.Toast("Ты вышел за пределы уровня — возврат на старт", Ui.Bad); }
            if (now > ignNext) { ignNext = now + 1.5f; Foreign.Scan(me, pc, new Vector3(16f, 9f, 16f)); }
            if (Catching) InputBlock.Apply();
            Island.Tick(); if (reportAt > 0f && Time.time > reportAt) { reportAt = 0f; Island.Report("уровень " + Level); }
            var list = Pls(); bool host = IsHost;
            if (host)
            {
                if (!Dev.Freeze) Ents.HostTick(dt, list); HostCatch(list, now); HostEvents(now);
                if (now > joinT && !Busy && RunArg != "") { joinT = now + 5f; var jf = RunArg.Split(','); if (jf.Length > 7) { jf[4] = Level.ToString(); Net.Send("join", string.Join(",", jf)); } }
                if (now > snapT && Game.InRoom) { snapT = now + 0.125f; Net.Send("ent", Ents.Snapshot(pc)); }
            }
            ExitLogic(list, host, now, dt); Loot.Tick(dt);
            if (now > moteT && Cur != null) { moteT = now + 0.35f; Vfx.Motes(pc, Color.Lerp(Color.white, L.lamp, 0.4f), 2); }
            if (now > ambNext && !Busy && !Catching && Cur != null && !L.rest)
            {
                ambNext = now + UnityEngine.Random.Range(28f, 65f); string[] amb = Atmos.AmbientSet(L.id); float ang = UnityEngine.Random.value * Mathf.PI * 2f; float dd = UnityEngine.Random.Range(13f, 26f);
                Snd2.At(amb[UnityEngine.Random.Range(0, amb.Length)], pc + new Vector3(Mathf.Cos(ang) * dd, 0f, Mathf.Sin(ang) * dd), UnityEngine.Random.Range(0.25f, 0.5f));
            }
            Ents.Visual(dt, host);
            if (now > snT) { snT = now + 0.25f; var ids = Ents.LocalSees(); if (host) foreach (var i in ids) Ents.MarkSeen(i); else if (ids.Count > 0) Net.Send("sn", string.Join(",", ids.ConvertAll(x => x.ToString()).ToArray())); }
            // свет
            if (blackout > 0f) { blackout -= dt; Cur.rig.Mul = Mathf.Min(Cur.rig.Mul, 0.03f); }
            Cur.rig.Tick(pc);
            int fi = 0; foreach (var r in Cur.flick) { if (r != null) { float dd = Ents.LampDisturb(r.transform.position); r.enabled = blackout <= 0f && Mathf.PerlinNoise(now * 3f + fi * 1.7f, 0.3f) > (0.28f + dd * 0.5f); } fi++; }
            // страх: близость угрозы
            float thr = Ents.NearestThreat(pc); float pr = Mathf.Clamp01(1f - thr / 17f); Prox = Mathf.MoveTowards(Prox, pr, dt * 1.5f);
            BrSnd.Heart(Catching ? 0f : Mathf.Clamp01((Prox - 0.2f) * 1.4f), Mathf.Lerp(0.85f, 1.5f, Prox)); Shade.Boost = Mathf.Max(Mathf.Max(Prox * 0.7f, Glitch), Dread.Level * 0.22f);
            if (!Catching) CamFx.Boost = Mathf.Max(CamFx.Boost, Prox > 0.7f ? (Prox - 0.7f) * 1.2f : 0f);
            Hazards(me, pc, dt, now);
            if (ambient != null) ambient.volume = Mathf.Lerp(0.3f, 0.12f, Prox) * Dread.SilenceMul;
            Dread.Tick(dt, pc, Camera.main, Prox); Refl.Tick(pc, dt); Atmos.Tick(dt, pc); if (Cur.L.id == "37") Water.Tick(Cur, pc, dt); Torch.Tick(dt, pc); AutoQ(dt);
            if (now > cullT) { cullT = now + 0.25f; Cur.Cull(pc, CullR()); }
        }

        // ---------- ловушки, вода, мотыльки (локально для себя) ----------
        static void Hazards(Component me, Vector3 pc, float dt, float now)
        {
            if (Catching) return; var b = Cur;
            foreach (var h in b.hazards)
            {
                float d = Util.Flat(pc - h.pos).magnitude; if (d > h.r + 0.2f || Mathf.Abs(pc.y - (b.baseP.y + 1f)) > 2.5f) continue;
                if (h.kind == 0)
                {
                    if (now < h.next) continue; h.next = now + 2.4f; Snd2.At("zap", pc, 0.8f); CamFx.Boost = Mathf.Max(CamFx.Boost, 1.1f); Glitch = Mathf.Max(Glitch, 0.4f); Util.Status("Poison", 0.07f); Game.Rpc(me, "RPCA_Fall", 1.3f); Ui.Toast("Удар током!", Ui.Bad);
                    Vfx.Sparks(pc, Vector3.up, 16, new Color(0.5f, 0.9f, 1f));
                }
                else { Util.Drip("Poison", 0.1f, dt); if (now > h.next) { h.next = now + 3f; Snd2.At("splash", pc, 0.5f); Ui.Toast("Тёмная вода жжёт — выйди!", Ui.Bad); } }
            }
            if (b.L.rest && now > hazT) { hazT = now + 4f; bool inDark = false; foreach (var h in b.hazards) if (h.kind == 1 && Util.Flat(pc - h.pos).magnitude < h.r + 1f) inDark = true; if (!inDark) Util.ClearStatuses(); }
            foreach (var e in Ents.All)
            {
                if (e.kind != "moth" || e.root == null) continue; if ((e.root.transform.position + Vector3.up * Mathf.Max(0.5f, e.height) - pc).magnitude > 1.7f || now < mothCd) continue;
                mothCd = now + 3.5f; Util.Status("Spores", 0.12f); Util.Status("Drowsy", 0.08f); Glitch = Mathf.Max(Glitch, 0.25f); Ui.Toast("Мотылёк-Смерть оставил споры", Ui.Bad); Snd2.At("hiss", pc, 0.5f);
            }
        }

        // ---------- поимка ----------
        static void HostCatch(List<PL> list, float now)
        {
            if (Time.time < Ents.Grace) return;
            foreach (var p in list)
            {
                float t; if (immune.TryGetValue(p.actor, out t) && now < t) continue; var e = Ents.CaughtBy(p, now); if (e == null) continue;
                immune[p.actor] = now + 6f; int id = e.id; string kind = e.kind; Ents.AfterCatch(e, list);
                if (p.actor == Game.LocalActor()) OnCaught(kind); else Net.Send("ct", id.ToString(), p.actor);
            }
        }
        static string KindName(string k) { string nn = Ents.KindName(k); if (nn != null) return nn; switch (k) { case "stealer": return "Вор Кожи"; case "smiler": return "Смайлер"; case "hound": return "Гончая"; case "party": return "Гость вечеринки"; default: return "Что-то"; } }
        public static void OnCaught(string kind) { if (Dev.God) return; if (Catching || Busy || !On || Cur == null) return; if (Loot.TryCharm(KindName(kind))) return; Mono.StartCoroutine(CoCatch(kind)); }

        static float catchT0;
        static void ApplyCatchCam(Transform tr)
        {
            float p = Mathf.Clamp01((Time.time - catchT0) / 1.1f); float e = 1f - Mathf.Pow(1f - p, 3f);
            tr.position = tr.position - Vector3.up * (0.95f * e); tr.rotation = Quaternion.Euler(0f, 0f, 68f * e) * tr.rotation * Quaternion.Euler(-24f * e, 0f, 0f);
        }
        static IEnumerator CoCatch(string kind)
        {
            Catching = true; CatchCam = true; Deaths++; catchT0 = Time.time; CatchText = "Тебя поймал: " + KindName(kind); var me = Game.Me();
            Dread.CatchSting(); CamFx.Boost = 1.1f; Glitch = 0.55f; TapeEnd = 1f; try { if (me != null) Game.Rpc(me, "RPCA_Fall", 2.4f); } catch { }
            float t0 = Time.time; while (Time.time - t0 < 0.75f) { InputBlock.Want = true; CamFx.Boost = Mathf.Max(CamFx.Boost, 1.6f); yield return null; }
            blackout = 3f; if (Cur != null) Cur.rig.Mul = 0f; fadeSpeed = 1.6f; FadeTarget = 1f;
            while (Time.time - t0 < 1.9f) { InputBlock.Want = true; yield return null; }
            BrSnd.StopScreams(); BrSnd.Heart(0f, 1f);
            if (Cur != null) { Util.Warp(Cur.spawn + Vector3.up * 1.3f); me = Game.Me(); if (me != null && Game.IsDead(me)) Game.Rpc(me, "RPCA_ReviveAtPosition", Cur.spawn + Vector3.up * 1.3f, false, -1); }
            try { if (Diff >= 1) Util.Status("Poison", Diff == 2 ? 0.14f : 0.07f); if (Diff == 2) Util.Status("Drowsy", 0.25f); } catch { }
            CatchCam = false; InputBlock.Want = false; Prox = 0f; yield return new WaitForSeconds(0.9f);
            Catching = false; fadeSpeed = 0.8f; FadeTarget = 0f; CatchText = ""; Ents.Grace = Time.time + 2.5f;
        }

        // ---------- вой (событие уровней) ----------
        static void HostEvents(float now)
        {
            if (now < nextHowl || L == null || L.rest || Ents.All.Count == 0 || Time.time < Ents.Grace + 5f) return; nextHowl = now + UnityEngine.Random.Range(70f, 130f) / Mathf.Max(0.6f, DiffVs[Diff]);
            Net.Send("ev", "howl"); DoHowl(); Ents.Provoke(Vector3.zero);
        }
        static void DoHowl()
        {
            Snd2.Flat("howl", 0.42f); CamFx.Boost = Mathf.Max(CamFx.Boost, 0.45f); Glitch = Mathf.Max(Glitch, 0.35f); Dread.Bump(0.12f); blackout = Mathf.Max(blackout, 3.5f); Ui.Toast("Где-то воет Howler… свет гаснет", Ui.Bad);
        }

        // ---------- выход уровня ----------
        static void ExitLogic(List<PL> list, bool host, float now, float dt)
        {
            if (Cur == null) return; int total = list.Count, nearD = 0; bool anyAt = false; float bestMe = 1e9f; var me = Game.Me(); Vector3 mp = me != null ? Util.CenterOf(me) : Vector3.zero;
            foreach (var p in list)
            {
                float bd = 1e9f; foreach (var ex in Cur.exitPos) { float d = Util.Flat(p.pos - ex).magnitude; if (Mathf.Abs(p.pos.y - ex.y) < 4f && d < bd) bd = d; }
                if (bd < 9f) nearD++; if (bd < 2.4f) anyAt = true;
            }
            foreach (var ex in Cur.exitPos) bestMe = Mathf.Min(bestMe, Util.Flat(mp - ex).magnitude);
            exitInfo = anyAt ? "Дверь открыта: у двери " + nearD + "/" + total + (nearD < total ? " — подожди отряд" : "") : "";
            if (anyAt) doorT += dt; else doorT = Mathf.Max(0f, doorT - dt * 2f); lastDoorNear = nearD;
            if (!host || Busy || Catching) return;
            if (anyAt && ((nearD >= total && doorT > 1.5f) || doorT > 20f)) { doorT = 0f; Advance(); }
        }
        static void Advance()
        {
            int next = Level + 1;
            if (next >= Levels.All.Length) { Net.Send("fin", ""); Leave(true, "финал"); }
            else { Net.Send("lv", next.ToString()); Mono.StartCoroutine(CoEnter(next, false)); }
        }
        /// <summary>DEV: перейти на любой уровень (отряд следует за хостом).</summary>
        public static void DevGo(int idx)
        {
            if (Busy) return; if (!On) { StartRun(idx); return; }
            if (idx == Level) return; Net.Send("lg", idx.ToString()); Mono.StartCoroutine(CoEnter(idx, false));
        }
        public static void Skip() { if (On && !Busy && IsHost) Advance(); }

        // ================= HUD =================
        public static void DrawOverlay(float W, float H, float s)
        {
            var cam = Camera.main;
            if (On && Cur != null && cam != null) { CamFx.Draw(W, H, s, cam); DrawHud(W, H, s, cam); Loot.Hud(W, H, s, cam); Torch.Hud(W, H, s); Arrow.Hud(W, H, s, cam); }
            if (Fade > 0.003f) { GUI.color = new Color(0, 0, 0, Mathf.Clamp01(Fade)); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }
            if (CatchText != "" && Fade > 0.4f) Ui.Text(new Rect(0, H * 0.42f, W, 80f * s), CatchText, (int)(40f * s), new Color(0.9f, 0.1f, 0.1f, Mathf.Clamp01((Fade - 0.4f) * 2f)), TextAnchor.MiddleCenter, true);
            float tt = Time.time - TitleT; if (On && tt < 8.5f && L != null && Fade < 0.9f)
            {
                float a = Mathf.Clamp01(Mathf.Min(tt / 1.2f, (8.5f - tt) / 1.6f)) * (1f - Fade); float bar = H * 0.1f * Mathf.Clamp01(Mathf.Min(tt / 0.8f, (8.5f - tt) / 0.8f));
                GUI.color = new Color(0, 0, 0, 0.92f); GUI.DrawTexture(new Rect(0, 0, W, bar), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0, H - bar, W, bar), Texture2D.whiteTexture); GUI.color = Color.white;
                float cy = H * 0.5f - 90f * s; Color ac = new Color(1f, 0.9f, 0.5f);
                Ui.Text(new Rect(0, cy, W, 26f * s), Sp(LvTag()), (int)(18f * s), new Color(ac.r, ac.g, ac.b, a * 0.9f), TextAnchor.MiddleCenter, true);
                Ui.Box(new Rect(W * 0.5f - 90f * s * a, cy + 32f * s, 180f * s * a, 1.5f * s), new Color(ac.r, ac.g, ac.b, a * 0.8f), 0f);
                Ui.Text(new Rect(0, cy + 40f * s, W, 70f * s), LvName(), (int)(52f * s), new Color(1f, 0.97f, 0.88f, a), TextAnchor.MiddleCenter, true);
                Ui.Text(new Rect(W * 0.2f, cy + 108f * s, W * 0.6f, 28f * s), L.eng + "   ·   " + (Level + 1) + " / " + Levels.All.Length, (int)(15f * s), new Color(0.8f, 0.78f, 0.7f, a * 0.8f), TextAnchor.MiddleCenter, false);
                Ui.Text(new Rect(W * 0.25f, cy + 142f * s, W * 0.5f, 70f * s), L.blurb, (int)(17f * s), new Color(1f, 1f, 1f, a * 0.85f), TextAnchor.UpperCenter, false);
                Ui.Text(new Rect(W * 0.25f, cy + 214f * s, W * 0.5f, 40f * s), L.tip, (int)(14f * s), new Color(0.6f, 1f, 0.7f, a * 0.7f), TextAnchor.UpperCenter, false);
            }
            float rt = Time.time - ResultT; if (!On && rt < 14f && Result != "")
            {
                float a = Mathf.Clamp01(Mathf.Min(rt / 0.6f, (14f - rt) / 2f)); var rr0 = new Rect(W * 0.5f - 380f * s, H * 0.3f, 760f * s, 220f * s); Ui.Box(rr0, new Color(0.02f, 0.017f, 0.01f, 0.86f * a), 0f); Ui.Border(rr0, new Color(1f, 0.9f, 0.5f, 0.4f * a), 1f); Ui.Brackets(rr0, new Color(1f, 0.9f, 0.5f, a), 16f * s, 2f);
                Ui.Text(new Rect(W * 0.5f - 360f * s, H * 0.3f + 12f * s, 720f * s, 200f * s), Result, (int)(20f * s), new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter, true);
            }
            GUI.color = Color.white;
        }

        static string LvTag() { string id = L != null ? L.id : null; return string.IsNullOrEmpty(id) ? "ЗОНА " + (Level + 1) : "УРОВЕНЬ " + id.ToUpperInvariant(); }
        static string LvName() { string n = L != null ? L.name : ""; int k = n.IndexOf(" — "); return k >= 0 ? n.Substring(k + 3) : n; }
        static string Sp(string t) { var sb = new System.Text.StringBuilder(); for (int i = 0; i < t.Length; i++) { sb.Append(t[i]); if (i < t.Length - 1 && t[i] != ' ') sb.Append(' '); } return sb.ToString(); }

        static void DrawHud(float W, float H, float s, Camera cam)
        {
            Color acc = new Color(1f, 0.9f, 0.5f), dim = new Color(1f, 0.95f, 0.8f, 0.5f); int N = Levels.All.Length;
            // --- табличка уровня сверху ---
            float pw = 380f * s, ph = 50f * s, px = W * 0.5f - pw * 0.5f, py = 14f * s; var plate = new Rect(px, py, pw, ph);
            Ui.Box(plate, new Color(0.02f, 0.017f, 0.01f, 0.55f), 0f); Ui.Border(plate, new Color(acc.r, acc.g, acc.b, 0.28f), 1f); Ui.Brackets(plate, new Color(acc.r, acc.g, acc.b, 0.9f), 10f * s, 2f);
            Ui.Text(new Rect(px, py + 3f * s, pw, 16f * s), Sp(LvTag()), (int)(11f * s), new Color(acc.r, acc.g, acc.b, 0.85f), TextAnchor.MiddleCenter, true);
            Ui.Text(new Rect(px, py + 17f * s, pw, 24f * s), LvName(), (int)(18f * s), new Color(1f, 0.97f, 0.88f, 0.95f), TextAnchor.MiddleCenter, true);
            float tw = pw - 40f * s, tk = tw / N; for (int i = 0; i < N; i++) Ui.Box(new Rect(px + 20f * s + i * tk + 1f, py + ph - 6f * s, tk - 2f, 2f * s), i <= Level ? new Color(acc.r, acc.g, acc.b, 0.9f) : new Color(1f, 1f, 1f, 0.15f), 0f);
            var me = Game.Me(); if (me == null) return; Vector3 pc = Util.CenterOf(me); Vector3 near = Cur.exitPos.Count > 0 ? Cur.exitPos[0] : pc; float bd = 1e9f; foreach (var ex in Cur.exitPos) { float d = (ex - pc).magnitude; if (d < bd) { bd = d; near = ex; } }
            float inT = Time.time - LevelT0;
            // --- указатель на выход: вращающийся шеврон и расстояние ---
            if (inT > 40f)
            {
                Vector3 to = near - pc; to.y = 0f; Vector3 fw = cam.transform.forward; fw.y = 0f; float ang = Vector3.SignedAngle(fw, to, Vector3.up); float cx = W * 0.5f, cy = py + ph + 34f * s; float a0 = Mathf.Clamp01((inT - 40f) / 3f);
                var m0 = GUI.matrix; GUIUtility.RotateAroundPivot(ang, new Vector2(cx, cy)); Ui.Text(new Rect(cx - 20f * s, cy - 20f * s, 40f * s, 40f * s), "▲", (int)(26f * s), new Color(0.45f, 1f, 0.6f, 0.8f * a0), TextAnchor.MiddleCenter, true); GUI.matrix = m0;
                Ui.Text(new Rect(cx - 100f * s, cy + 16f * s, 200f * s, 20f * s), "ВЫХОД · " + Mathf.RoundToInt(bd) + " м", (int)(13f * s), new Color(0.55f, 1f, 0.7f, 0.75f * a0), TextAnchor.MiddleCenter, true);
            }
            else if (inT > 15f) Ui.Text(new Rect(W * 0.5f - 200f * s, py + ph + 8f * s, 400f * s, 20f * s), "дверь EXIT ≈ " + Mathf.RoundToInt(bd) + " м", (int)(13f * s), new Color(0.55f, 1f, 0.7f, 0.65f), TextAnchor.MiddleCenter, true);
            foreach (var ex in Cur.exitPos)
            {
                Vector3 sp = cam.WorldToScreenPoint(ex + Vector3.up * (Cur.H - 0.2f));
                if (sp.z > 0.5f && (ex - pc).magnitude < 30f) Ui.Text(new Rect(sp.x - 60f * s, H - sp.y - 14f * s, 120f * s, 28f * s), "EXIT", (int)(22f * s), Cur.L.exitCol, TextAnchor.MiddleCenter, true);
            }
            if (exitInfo != "") { var er = new Rect(W * 0.5f - 230f * s, H * 0.62f, 460f * s, 34f * s); Ui.Box(er, new Color(0f, 0.04f, 0.01f, 0.55f), 0f); Ui.Border(er, new Color(0.4f, 1f, 0.55f, 0.5f), 1f); Ui.Text(er, exitInfo, (int)(16f * s), new Color(0.6f, 1f, 0.7f, 0.95f), TextAnchor.MiddleCenter, true); }
            // --- преследование: красная кайма и тихая надпись вместо крика «БЕГИ!» ---
            if (Ents.AnyChasing()) { Dread.Vignette(W, H, new Color(0.9f, 0.04f, 0.02f), 0.28f + 0.2f * Mathf.Sin(Time.time * 5f)); Ui.Text(new Rect(W * 0.5f - 300f * s, H - 92f * s, 600f * s, 24f * s), Sp("ОНО БЕЖИТ ЗА ТОБОЙ"), (int)(14f * s), new Color(1f, 0.2f, 0.15f, 0.55f + 0.3f * Mathf.Sin(Time.time * 5f)), TextAnchor.MiddleCenter, true); }
            Ui.Text(new Rect(W * 0.5f - 420f * s, H - 54f * s, 840f * s, 22f * s), "[" + PanicKey + "] выйти из Backrooms", (int)(12f * s), dim, TextAnchor.MiddleCenter, false);
            if (Cur.L.dark) Ui.Text(new Rect(10f * s, H - 80f * s, 400f * s, 24f * s), "фонарь камеры включён", (int)(12f * s), new Color(1f, 1f, 1f, 0.4f), TextAnchor.MiddleLeft, false);
        }
    }
}
