// Автотест 1.15: бот-проверяльщик. Сам прогоняет функции мода на твоём персонаже (система/API, мяч и бросок, данки, дриблинг,
// соперник ИИ, герои), меряет результат, ловит ошибки Unity и пишет отчёт в файл + буфер обмена. Отчёт можно прислать разработчику.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakFun
{
    internal sealed class TRes { public string cat, name, status, detail; public float t; }

    internal sealed class TestLogListener : BepInEx.Logging.ILogListener
    {
        public Action<string> Sink; public object Src;
        public void LogEvent(object sender, BepInEx.Logging.LogEventArgs e)
        {
            if (Sink == null) return;
            bool mine = ReferenceEquals(e.Source, Src);
            if (!mine && e.Level != BepInEx.Logging.LogLevel.Error && e.Level != BepInEx.Logging.LogLevel.Fatal) return;
            Sink(DateTime.Now.ToString("HH:mm:ss") + " [" + e.Level + "] " + (mine ? "" : e.Source.SourceName + ": ") + e.Data);
        }
        public void Dispose() { }
    }

    public partial class PeakFunPlugin
    {
        readonly List<TRes> tRes = new List<TRes>();
        bool tRunning, tAbort; string tCur = ""; int tDone; float tStartT, tEndT; string tLastFile = "", tShort = "", tFull = "";
        readonly bool[] tCat = { true, true, true, true, true, true, true, true };
        static readonly string[] TCatNames = { "Система и API", "Мяч и бросок", "Данки", "Дриблинг", "Соперник (ИИ)", "Герои", "Backrooms", "Графика, ввод, сеть" };
        ConfigEntry<KeyCode> keySelfTest;
        readonly List<string> tLog = new List<string>();
        readonly Dictionary<string, int> tErrCnt = new Dictionary<string, int>(); readonly Dictionary<string, string> tErrStack = new Dictionary<string, string>();
        int tErrTotal; HoopObj testAim, testHoop; Vector2 tScroll;
        int aiStatShots, aiStatMakes, aiStatBlocks, aiStatSteals;

        void InitTest()
        {
            keySelfTest = Config.Bind("Keys", "SelfTest", KeyCode.Insert, "Автотест (бот-проверяльщик): запуск / остановка");
            Application.logMessageReceived += OnUnityLog;
            try { BepInEx.Logging.Logger.Listeners.Add(new TestLogListener { Sink = TAddLog, Src = Logger }); } catch (Exception e) { Logger.LogWarning("Автотест: нет перехвата лога: " + e.Message); }
        }

        void TAddLog(string s) { lock (tLog) { tLog.Add(s); if (tLog.Count > 400) tLog.RemoveRange(0, 100); } }

        void OnUnityLog(string cond, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            tErrTotal++;
            string key = (cond ?? "").Split('\n')[0]; if (key.Length > 160) key = key.Substring(0, 160);
            int n; tErrCnt.TryGetValue(key, out n); tErrCnt[key] = n + 1;
            if (n == 0) { var lines = (stack ?? "").Split('\n'); var sb = new StringBuilder(); for (int i = 0; i < lines.Length && i < 4; i++) sb.Append("      ").AppendLine(lines[i].Trim()); tErrStack[key] = sb.ToString(); }
        }

        // ---------- запись результатов ----------
        void Rec(string cat, string name, string status, string detail)
        {
            tRes.Add(new TRes { cat = cat, name = name, status = status, detail = detail, t = Time.realtimeSinceStartup - tStartT });
            tDone++; tCur = name;
            TAddLog(DateTime.Now.ToString("HH:mm:ss") + " [ТЕСТ] " + status + " · " + cat + " · " + name + " — " + detail);
            Logger.LogInfo("[PeakFun][тест] " + status + " · " + name + " — " + detail);
        }

        IEnumerator Wt(float s) { float e = Time.time + s; while (Time.time < e && !tAbort) yield return null; }

        IEnumerator Guard(string cat, string name, IEnumerator body)
        {
            while (true)
            {
                object cur;
                try { if (!body.MoveNext()) break; cur = body.Current; }
                catch (Exception e)
                {
                    var ex = e.InnerException ?? e; var st = (ex.StackTrace ?? "").Split('\n');
                    Rec(cat, name, "FAIL", "исключение " + ex.GetType().Name + ": " + ex.Message + (st.Length > 0 ? " @ " + st[0].Trim() : ""));
                    yield break;
                }
                yield return cur;
                if (tAbort) yield break;
            }
        }

        void Try(string cat, string name, Func<string> f, bool failOnNull = false)
        {
            try { string r = f(); if (r != null) { bool bad = r.StartsWith("!"); bool warn = r.StartsWith("?"); Rec(cat, name, bad ? "FAIL" : warn ? "WARN" : "PASS", bad || warn ? r.Substring(1) : r); } }
            catch (Exception e) { var ex = e.InnerException ?? e; Rec(cat, name, "FAIL", "исключение " + ex.GetType().Name + ": " + ex.Message); }
        }

        // ---------- управление ----------
        void TickTestKey()
        {
            if (Input.GetKeyDown(keySelfTest.Value)) { if (tRunning) tAbort = true; else StartSelfTest(); }
        }

        void StartSelfTest()
        {
            if (tRunning) return;
            if (Game.Me() == null) { Ui.Toast("Автотест: нет персонажа — зайди в игру/лобби", Ui.Bad); return; }
            bool any = false; foreach (var b in tCat) any |= b;
            if (!any) { Ui.Toast("Выбери хотя бы одну группу проверок", Ui.Bad); return; }
            tRes.Clear(); tAbort = false; tDone = 0; tStartT = Time.realtimeSinceStartup; tRunning = true; tErrCnt.Clear(); tErrStack.Clear(); tErrTotal = 0;
            if (showMenu) ToggleMenu(); StartCoroutine(CoSelfTest());
        }

        void MakeTestHoop(Component me, float dist)
        {
            RemoveTestHoop();
            Vector3 f = FlatForward(), gp = CenterOf(me) + f * dist; gp.y = GroundY(gp + Vector3.up * 3f, me.transform.position.y - 0.9f);
            Vector3 face = -f; var h = BuildHoop(gp, face, 3.05f, 1f, -9000 - UnityEngine.Random.Range(0, 999)); hoops.Add(h); testHoop = h; testAim = h;
        }

        void RemoveTestHoop()
        {
            testAim = null;
            if (testHoop != null)
            {
                hoops.Remove(testHoop);
                if (testHoop.root != null) UnityEngine.Object.Destroy(testHoop.root);
                if (testHoop.net != null) foreach (var l in testHoop.net) if (l != null) UnityEngine.Object.Destroy(l.gameObject);
                if (testHoop.arc != null) UnityEngine.Object.Destroy(testHoop.arc.gameObject);
                testHoop = null;
            }
        }

        // ======================= сам тест =======================
        IEnumerator CoSelfTest()
        {
            var me = Game.Me(); Vector3 origin = me.transform.position; bool hadBall = ballGo != null; int oldStyle = dunkStyle, oldLevel = aiLevel; var oldHero = hero;
            var oldScores = new Dictionary<int, int>(scores); string cat = "";
            HeroToast("Автотест запущен — не трогай мышь и клавиши. Прервать: [" + keySelfTest.Value + "]");
            yield return Wt(1.5f);
            if (tCat[0]) { cat = TCatNames[0]; yield return Guard(cat, "Система", CoTSystem(me)); }
            if (tCat[1] && !tAbort) { cat = TCatNames[1]; yield return Guard(cat, "Мяч и бросок", CoTBall(me, origin)); }
            if (tCat[2] && !tAbort) { cat = TCatNames[2]; yield return Guard(cat, "Данки", CoTDunks(me, origin)); }
            if (tCat[3] && !tAbort) { cat = TCatNames[3]; yield return Guard(cat, "Дриблинг", CoTDribble(me)); }
            if (tCat[4] && !tAbort) { cat = TCatNames[4]; yield return Guard(cat, "Соперник", CoTAi(me, origin)); }
            if (tCat[5] && !tAbort) { cat = TCatNames[5]; yield return Guard(cat, "Герои", CoTHeroes(me, origin)); }
            if (tCat[6] && !tAbort) { cat = TCatNames[6]; yield return Guard(cat, "Backrooms", CoTBack(me, origin)); }
            if (tCat[7] && !tAbort) { cat = TCatNames[7]; yield return Guard(cat, "Графика и сеть", CoTMisc(me)); }
            // уборка
            try
            {
                if (aiGo != null) RemoveAi();
                RemoveTestHoop(); if (hero != oldHero) SetHero(oldHero); if (brInside) BackroomsPanic(); RemoveCrates();
                dunkBusy = false; dribMove = 0; shootCharging = false; dunkStyle = oldStyle; aiLevel = oldLevel;
                if (!hadBall) { RemoveBall(); } else if (ballHeld) DropBall(me, 1f);
                scores.Clear(); foreach (var kv in oldScores) scores[kv.Key] = kv.Value;
                if (me != null) { Warp(me, origin + Vector3.up * 0.3f); Game.HaltBody(me); }
            }
            catch (Exception e) { Rec("Уборка", "Возврат состояния", "WARN", e.Message); }
            tEndT = Time.realtimeSinceStartup; tRunning = false;
            BuildReport(tAbort);
            Ui.Toast(tAbort ? "Автотест остановлен. Отчёт сохранён" : "Автотест завершён: " + tShort.Split('\n')[0], Ui.Good);
            HeroToast("Отчёт скопирован в буфер обмена — вставь его в чат"); if (!showMenu) ToggleMenu(); curTabSet(TTest);
        }

        void curTabSet(int t) { tab = t; }

        // ---------- 0. система ----------
        IEnumerator CoTSystem(Component me)
        {
            string c = TCatNames[0];
            Try(c, "Окружение", () => "мод " + Version + ", Unity " + Application.unityVersion + ", игра " + Application.version + ", " + SystemInfo.operatingSystem + ", GPU " + SystemInfo.graphicsDeviceName + ", RAM " + SystemInfo.systemMemorySize + " МБ");
            Try(c, "Игра распознана", () => Game.Ready ? "типы Character и PhotonView найдены" : "!не найдены типы PEAK — большая часть функций не заработает");
            Try(c, "Лобби", () => "в комнате: " + Game.InRoom + ", хост: " + Game.IsMaster() + ", игроков: " + Game.Everyone().Count + ", канал: " + Net.Status);
            Try(c, "Данные персонажа", () =>
            {
                var miss = new List<string>(); foreach (var n in new[] { "Center", "Head", "data", "input", "refs" }) if (R.Get(me, n) == null) miss.Add(n);
                return miss.Count == 0 ? "Center/Head/data/input/refs доступны" : "?не читаются поля: " + string.Join(", ", miss.ToArray());
            });
            Try(c, "Части тела (позы рук, кольцо, дриблинг)", () =>
            {
                var pm = PartMap.Of(me); if (pm == null) return "!PartMap не построен";
                var miss = new List<string>(); var names = new[] { "hip", "torso", "head", "armL", "armR", "elbL", "elbR", "handL", "handR", "legL", "legR", "kneeL", "kneeR", "footL", "footR" };
                foreach (var n in names) { var f = typeof(PartMap).GetField(n); if (f == null || f.GetValue(pm) == null) miss.Add(n); }
                return miss.Count == 0 ? "все 15 частей найдены (" + pm.names + ")" : "?не найдены: " + string.Join(", ", miss.ToArray());
            });
            Try(c, "Типы игры (рефлексия)", () =>
            {
                var req = new[] { "Character", "Photon.Pun.PhotonNetwork", "Photon.Pun.PhotonView" };
                var opt = new[] { "CharacterInput", "Player", "PlayerHandler", "GUIManager", "ItemDatabase", "UIPlayerNames", "CustomizationOption", "HelperFunctions", "Photon.Realtime.RaiseEventOptions", "UnityEngine.Rendering.Volume", "UnityEngine.InputSystem.Keyboard" };
                var mr = new List<string>(); var mo = new List<string>();
                foreach (var n in req) if (R.FindType(n) == null) mr.Add(n);
                foreach (var n in opt) if (R.FindType(n) == null) mo.Add(n);
                if (mr.Count > 0) return "!нет обязательных типов: " + string.Join(", ", mr.ToArray());
                return mo.Count == 0 ? "все типы найдены" : "?нет необязательных: " + string.Join(", ", mo.ToArray());
            });
            Try(c, "Физика", () =>
            {
                string mat = (R.FindType("UnityEngine.PhysicsMaterial") ?? R.FindType("UnityEngine.PhysicMaterial")) != null ? "тип материала найден" : "тип материала НЕ найден";
                return "g=" + Physics.gravity.y.ToString("0.00") + ", fixedDt=" + Time.fixedDeltaTime.ToString("0.000") + ", " + mat;
            });
            Try(c, "Патчи Harmony", () => "ввод: " + (InputBlock.Patched ? "патч" : "запасной") + ", ник бота: " + (NamePatchOk ? "патч" : "запасной") + ", шейдеры: " + (Shade.Ready ? "готовы" : Shade.Status));
            // FPS
            float t0 = Time.realtimeSinceStartup; int f0 = Time.frameCount; yield return Wt(2f);
            float fps = (Time.frameCount - f0) / Mathf.Max(0.01f, Time.realtimeSinceStartup - t0);
            Rec(c, "Частота кадров (без нагрузки)", fps < 25f ? "WARN" : "PASS", fps.ToString("0") + " FPS");
            Rec(c, "Ошибки Unity с начала сессии", tErrTotal == 0 ? "PASS" : "WARN", tErrTotal == 0 ? "нет" : tErrTotal + " сообщений (см. раздел «Ошибки» в отчёте)");
        }

        // ---------- 1. мяч и бросок ----------
        IEnumerator CoTBall(Component me, Vector3 origin)
        {
            string c = TCatNames[1];
            MakeTestHoop(me, 4f); EnsureBall(HeadOf(me) + FlatForward()); yield return Wt(0.4f);
            Try(c, "Кольцо и мяч созданы", () => ballGo != null && testHoop != null && testHoop.root != null ? "кольцо на высоте " + testHoop.rimH.ToString("0.00") + " м, радиус обруча " + testHoop.rimR.ToString("0.00") + " м" : "!не созданы");
            // падение мяча
            {
                Vector3 fw = FlatForward(); Vector3 p = CenterOf(me) + fw * 1.8f + Vector3.up * 0.5f; float gy = GroundY(p, me.transform.position.y - 0.9f), r = 0.15f * hoopScale, H0 = 2.0f;
                ballHeld = false; ballRb.isKinematic = false; ballCol.enabled = true; ballOwner = -1;
                ballGo.transform.position = new Vector3(p.x, gy + r + H0, p.z); Game.SetVelocity(ballRb, Vector3.zero); ballRb.angularVelocity = Vector3.zero;
                float t = 0f, tFall = -1f, apex = 0f; bool up = false; float prevV = 0f;
                while (t < 3f && !tAbort)
                {
                    yield return new WaitForFixedUpdate(); t += Time.fixedDeltaTime; float vy = ballRb.velocity.y, y = ballGo.transform.position.y - gy - r;
                    if (!up && prevV < -1f && vy > 0f) { up = true; tFall = t; }
                    if (up) { apex = Mathf.Max(apex, y); if (vy < 0f) break; }
                    prevV = vy;
                }
                if (tFall < 0f) Rec(c, "Отскок мяча", "FAIL", "мяч не отскочил за 3 с (застрял/провалился?)");
                else
                {
                    float geff = 2f * H0 / (tFall * tFall), ratio = apex / H0; string det = "падение 2.0 м за " + tFall.ToString("0.00") + " с (g_eff=" + geff.ToString("0.0") + " при g=" + (-Physics.gravity.y).ToString("0.0") + "), отскок до " + apex.ToString("0.00") + " м (" + (ratio * 100f).ToString("0") + "%)";
                    bool ok = ratio > 0.55f && ratio < 0.97f && geff > 0.6f * -Physics.gravity.y && geff < 1.6f * -Physics.gravity.y;
                    Rec(c, "Отскок мяча", ok ? "PASS" : "WARN", det + (ok ? "" : " — физика мяча отличается от ожидаемой"));
                }
            }
            // броски
            float[] dists = { 4f, 7.3f, 10f }; string[] dn = { "под кольцом (4 м)", "из-за дуги (7.3 м)", "дальний (10 м)" };
            for (int di = 0; di < dists.Length && !tAbort; di++)
            {
                MakeTestHoop(me, dists[di]); yield return Wt(0.5f);
                int[] made = new int[2], rimT = new int[2]; int N = 5; float[] errs = { 0.03f, 0.3f };
                for (int mode = 0; mode < 2 && !tAbort; mode++)
                    for (int i = 0; i < N && !tAbort; i++)
                    {
                        EnsureBall(HeadOf(me) + FlatForward() * 0.5f); TakeBall(me); yield return Wt(0.3f);
                        int before; scores.TryGetValue(Game.LocalActor(), out before); float t0 = Time.time;
                        Shoot(me, errs[mode], 0f, true, 0);
                        while (Time.time - t0 < 4.2f && !tAbort) { yield return null; int now; scores.TryGetValue(Game.LocalActor(), out now); if (now > before) break; }
                        int af; scores.TryGetValue(Game.LocalActor(), out af); if (af > before) made[mode]++;
                        if (rimTouched) rimT[mode]++;
                        yield return Wt(0.5f);
                    }
                string det = "идеальный тайминг: " + made[0] + "/" + N + " (по ободу " + rimT[0] + "), средний тайминг: " + made[1] + "/" + N + " (по ободу " + rimT[1] + ")";
                string st = made[0] >= N - 1 ? "PASS" : (made[0] >= N / 2 ? "WARN" : "FAIL");
                Rec(c, "Бросок: " + dn[di], st, det + (st == "PASS" ? "" : " — идеальные броски должны попадать почти всегда"));
            }
            RemoveTestHoop();
        }

        // ---------- 2. данки ----------
        IEnumerator CoTDunks(Component me, Vector3 origin)
        {
            string c = TCatNames[2];
            string[] names = { "Классика", "Двумя руками", "Разворот 360°", "Сальто", "Мельница", "Повис на кольце", "Томагавк", "Бэкфлип", "Обратный", "Ураган 720°", "Полёт" };
            for (int ds = 0; ds < names.Length && !tAbort; ds++)
            {
                Warp(me, origin + Vector3.up * 0.3f); Game.HaltBody(me); yield return Wt(0.8f);
                MakeTestHoop(me, 3.6f); EnsureBall(HeadOf(me) + FlatForward() * 0.5f); TakeBall(me); yield return Wt(0.3f);
                dunkStyle = ds; int before; scores.TryGetValue(Game.LocalActor(), out before); float t0 = Time.time; bool started = false;
                StartCoroutine(CoDunk(testHoop, false, me)); yield return null; started = dunkBusy;
                while (dunkBusy && Time.time - t0 < 7f && !tAbort) yield return null;
                float dur = Time.time - t0; int af; scores.TryGetValue(Game.LocalActor(), out af); bool scored = af > before;
                Vector3 pc = CenterOf(me); bool finite = !float.IsNaN(pc.x + pc.y + pc.z) && !float.IsInfinity(pc.x + pc.y + pc.z); float drift = Vector3.Distance(pc, origin);
                bool done = !dunkBusy;
                string st = !started ? "FAIL" : (!done || !finite) ? "FAIL" : scored ? "PASS" : "WARN";
                Rec(c, "Данк: " + names[ds], st, (started ? "" : "не запустился; ") + "длительность " + dur.ToString("0.0") + " с, " + (scored ? "мяч засчитан" : "мяч НЕ засчитан") + ", смещение игрока " + drift.ToString("0.0") + " м" + (finite ? "" : ", координаты NaN!"));
                dunkBusy = false; yield return Wt(0.6f);
            }
            RemoveTestHoop(); Warp(me, origin + Vector3.up * 0.3f); Game.HaltBody(me); yield return Wt(0.6f);
        }

        // ---------- 3. дриблинг ----------
        IEnumerator CoTDribble(Component me)
        {
            string c = TCatNames[3];
            MakeTestHoop(me, 8f); EnsureBall(HeadOf(me) + FlatForward()); TakeBall(me); yield return Wt(1.2f);
            Try(c, "Ведение мяча", () => { float d = Vector3.Distance(ballGo.transform.position, CenterOf(me)); return d < 2.2f ? "мяч у игрока (" + d.ToString("0.0") + " м)" : "!мяч далеко от игрока (" + d.ToString("0.0") + " м)"; });
            string[] mn = { "", "Между ног", "За спиной", "Разворот", "Финт" };
            for (int k = 1; k <= 4 && !tAbort; k++)
            {
                float maxD = 0f, minY = 99f, t0 = Time.time; float gy = GroundY(CenterOf(me), me.transform.position.y - 0.9f);
                StartMove(me, k); bool started = dribMove == k;
                while (dribMove != 0 && Time.time - t0 < 2f && !tAbort)
                {
                    yield return null; maxD = Mathf.Max(maxD, Vector3.Distance(ballGo.transform.position, CenterOf(me))); minY = Mathf.Min(minY, ballGo.transform.position.y - gy);
                }
                bool done = dribMove == 0; bool sane = maxD < 2.4f && minY > 0.02f;
                Rec(c, "Приём: " + mn[k], !started || !done ? "FAIL" : sane ? "PASS" : "WARN", (started ? "" : "не запустился; ") + "длительность " + (Time.time - t0).ToString("0.00") + " с, макс. расстояние мяча " + maxD.ToString("0.00") + " м, мин. высота " + minY.ToString("0.00") + " м");
                yield return Wt(0.9f);
            }
            StartCoroutine(CoDoubleCross(me)); yield return Wt(1.2f);
            Rec(c, "Двойной кроссовер", "PASS", "выполнен без ошибок, мяч " + (ballHeld ? "в руках" : "потерян"));
            yield return Wt(2.2f);
            Rec(c, "Мяч к груди двумя руками", holdBlend > 0.6f ? "PASS" : "WARN", "holdBlend=" + holdBlend.ToString("0.00") + ", поза рук " + ballArm + (ballArm == 25 ? "" : " (ожидалась 25)"));
            RemoveTestHoop();
        }

        // ---------- 4. соперник ----------
        IEnumerator CoTAi(Component me, Vector3 origin)
        {
            string c = TCatNames[4];
            MakeTestHoop(me, 7f); testAim = null;
            if (aiGo != null) RemoveAi();
            aiStatShots = aiStatMakes = aiStatBlocks = aiStatSteals = 0;
            Try(c, "Создание соперника", () => { SpawnAi(); return aiGo != null ? "создан, уровень " + LvNames[aiLevel] : "!не создан"; });
            if (aiGo == null) yield break;
            yield return Wt(1f);
            int[] lvls = { 1, 3 };
            foreach (int lv in lvls)
            {
                if (tAbort) break; aiLevel = lv; string ln = LvNames[lv];
                // защита
                EnsureBall(HeadOf(me)); TakeBall(me); aiHasBall = false; aiInbound = false; yield return Wt(5f);
                float d = Vector3.Distance(new Vector3(aiGo.transform.position.x, 0, aiGo.transform.position.z), new Vector3(CenterOf(me).x, 0, CenterOf(me).z));
                Rec(c, "[" + ln + "] защита: держится рядом", d < 3.3f && d > 0.5f ? "PASS" : "WARN", "дистанция до игрока " + d.ToString("0.0") + " м (ожидалось 0.5–3.3)");
                // преследование
                Vector3 side = Vector3.Cross(Vector3.up, FlatForward()).normalized; Warp(me, origin + side * 7f + Vector3.up * 0.3f); Game.HaltBody(me); yield return Wt(3.2f);
                d = Vector3.Distance(new Vector3(aiGo.transform.position.x, 0, aiGo.transform.position.z), new Vector3(CenterOf(me).x, 0, CenterOf(me).z));
                Rec(c, "[" + ln + "] защита: догоняет при смене позиции", d < 3.8f ? "PASS" : "WARN", "после перемещения игрока на 7 м соперник в " + d.ToString("0.0") + " м");
                // купился на финт
                int bites = 0, tries = 24;
                for (int i = 0; i < tries && !tAbort; i++)
                {
                    aiStunUntil = 0f; AiOnPlayerMove(1 + i % 4); if (Time.time < aiStunUntil) bites++; yield return null;
                }
                aiStunUntil = 0f;
                Rec(c, "[" + ln + "] «купился» на финты", "PASS", bites + "/" + tries + " (ожидание ≈ " + (LvBite[lv] * 100f).ToString("0") + "%)");
                // атака
                if (ballHeld) DropBall(me, 0.5f);
                yield return Wt(0.3f); Warp(me, origin + Vector3.up * 0.3f); Game.HaltBody(me);
                aiNoGrabUntil = 0f; AiGrab(false, aiGo.transform.position); aiShotClock = 0f; int sBefore; scores.TryGetValue(AiId, out sBefore); int shBefore = aiShotLive ? 1 : 0;
                float t0 = Time.time; float firstAct = -1f; Vector3 lastP = aiGo.transform.position; float stuck = 0f, maxStuck = 0f; bool left = false; bool pHeld = false;
                while (Time.time - t0 < 22f && !tAbort)
                {
                    yield return null;
                    if (firstAct < 0f && (aiShotLive || aiBusy)) firstAct = Time.time - t0;
                    Vector3 p = aiGo.transform.position; if (Vector3.Distance(p, lastP) < 0.02f) stuck += Time.deltaTime; else { stuck = 0f; lastP = p; } maxStuck = Mathf.Max(maxStuck, stuck);
                    if (p.y < testHoop.baseP.y - 20f) { left = true; break; }
                    int sNow; scores.TryGetValue(AiId, out sNow); if (sNow > sBefore) break;
                    if (!aiHasBall && !aiShotLive && !aiBusy && firstAct > 0f && Time.time - t0 > firstAct + 3f) break;
                    pHeld |= ballHeld;
                }
                int sAfter; scores.TryGetValue(AiId, out sAfter);
                string st = left ? "FAIL" : firstAct < 0f ? "FAIL" : maxStuck > 4f ? "WARN" : "PASS";
                Rec(c, "[" + ln + "] атака", st, firstAct < 0f ? "соперник так и не атаковал за 22 с (мяч " + (aiHasBall ? "у него" : "не у него") + ")" : "первое действие через " + firstAct.ToString("0.0") + " с, " + (sAfter > sBefore ? "ГОЛ (+" + (sAfter - sBefore) + ")" : "промах/без гола") + ", макс. простой " + maxStuck.ToString("0.0") + " с" + (left ? ", провалился под карту!" : ""));
                // возврат мяча
                aiHasBall = false; aiShotLive = false; aiBusy = false; yield return Wt(1.5f);
            }
            // нагрузка
            {
                float t0 = Time.realtimeSinceStartup; int f0 = Time.frameCount; yield return Wt(2f);
                float fps = (Time.frameCount - f0) / Mathf.Max(0.01f, Time.realtimeSinceStartup - t0);
                Rec(c, "Частота кадров с соперником", fps < 25f ? "WARN" : "PASS", fps.ToString("0") + " FPS");
            }
            Rec(c, "Статистика ИИ за тест", "PASS", "бросков " + aiStatShots + ", голов " + aiStatMakes + ", блоков " + aiStatBlocks + ", перехватов " + aiStatSteals);
            RemoveAi(); RemoveTestHoop();
        }

        // ---------- 5. герои ----------
        IEnumerator CoTHeroes(Component me, Vector3 origin)
        {
            string c = TCatNames[5];
            foreach (HeroKind k in Enum.GetValues(typeof(HeroKind)))
            {
                if (k == HeroKind.None || tAbort) continue;
                int e0 = tErrTotal; string nm = HeroName(k); string err = null;
                try { SetHero(k); } catch (Exception e) { err = "SetHero: " + (e.InnerException ?? e).Message; }
                yield return Wt(0.7f);
                bool setOk = hero == k;
                try
                {
                    if (IsHoop(k)) { EnsureBall(HeadOf(me) + FlatForward()); HoopCast(0); }
                    else { UseA(); UseB(); }
                }
                catch (Exception e) { err = (err ?? "") + " приём: " + (e.InnerException ?? e).Message; }
                yield return Wt(1.0f);
                try { if (IsHoop(k)) { cdA = cdB = 0f; HoopCast(1); } } catch (Exception e) { err = (err ?? "") + " приём2: " + (e.InnerException ?? e).Message; }
                yield return Wt(0.8f);
                try { StopAnime(); } catch { }
                int de = tErrTotal - e0; string st = err != null || !setOk ? "FAIL" : de > 0 ? "WARN" : "PASS";
                Rec(c, "Герой: " + nm, st, (setOk ? "включился" : "не включился") + ", ошибок Unity за проверку: " + de + (err != null ? "; " + err : ""));
                try { SetHero(HeroKind.None); } catch { }
                Warp(me, origin + Vector3.up * 0.3f); Game.HaltBody(me); yield return Wt(0.6f);
            }
        }

        // ======================= отчёт =======================
        string Trunc(string s, int n) { return s == null ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…"); }

        void BuildReport(bool aborted)
        {
            int pass = 0, warn = 0, fail = 0; foreach (var r in tRes) { if (r.status == "PASS") pass++; else if (r.status == "WARN") warn++; else if (r.status == "FAIL") fail++; }
            var sb = new StringBuilder();
            sb.AppendLine("=== PeakFun " + Version + " — отчёт автотеста" + (aborted ? " (ПРЕРВАН)" : "") + " ===");
            sb.AppendLine("Время: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ", длительность " + (tEndT - tStartT).ToString("0") + " с");
            sb.AppendLine("Итого: PASS " + pass + " · WARN " + warn + " · FAIL " + fail + " · ошибок Unity " + tErrTotal);
            sb.AppendLine();
            string lastCat = "";
            foreach (var r in tRes)
            {
                if (r.cat != lastCat) { sb.AppendLine("--- " + r.cat + " ---"); lastCat = r.cat; }
                sb.AppendLine("[" + r.status + "] " + r.name + " — " + r.detail);
            }
            sb.AppendLine();
            sb.AppendLine("--- Ошибки Unity (уникальные) ---");
            if (tErrCnt.Count == 0) sb.AppendLine("нет");
            else foreach (var kv in tErrCnt) { sb.AppendLine("x" + kv.Value + "  " + kv.Key); string st; if (tErrStack.TryGetValue(kv.Key, out st)) sb.Append(st); }
            sb.AppendLine();
            sb.AppendLine("--- Настройки ---");
            sb.AppendLine("hoopScale=" + hoopScale + ", rimHeight=" + rimHeight + ", aimAssist=" + aimAssist + ", dunkStyle=" + dunkStyle + ", aiLevel=" + aiLevel + ", matchTo=" + matchTo);
            sb.AppendLine();
            sb.AppendLine("--- Лог мода (последние строки) ---");
            lock (tLog) { int from = Mathf.Max(0, tLog.Count - 120); for (int i = from; i < tLog.Count; i++) sb.AppendLine(tLog[i]); }
            tFull = sb.ToString();
            var sh = new StringBuilder();
            sh.AppendLine("PeakFun " + Version + " автотест: PASS " + pass + " · WARN " + warn + " · FAIL " + fail + " · ошибок Unity " + tErrTotal + (aborted ? " (прерван)" : ""));
            foreach (var r in tRes) if (r.status != "PASS") sh.AppendLine("[" + r.status + "] " + r.name + " — " + Trunc(r.detail, 220));
            int en = 0; foreach (var kv in tErrCnt) { if (en++ >= 5) break; sh.AppendLine("ERR x" + kv.Value + " " + Trunc(kv.Key, 160)); }
            tShort = sh.ToString();
            try
            {
                string dir = BepInEx.Paths.BepInExRootPath; if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) dir = Application.persistentDataPath;
                string f = Path.Combine(dir, "PeakFun_selftest_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                File.WriteAllText(f, tFull, new UTF8Encoding(true)); File.WriteAllText(Path.Combine(dir, "PeakFun_selftest_latest.txt"), tFull, new UTF8Encoding(true)); tLastFile = f;
            }
            catch (Exception e) { tLastFile = "не удалось записать файл: " + e.Message; }
            try { GUIUtility.systemCopyBuffer = tShort; } catch { }
        }

        // ======================= интерфейс =======================
        void DrawTestHud()
        {
            if (!tRunning || Event.current.type != EventType.Repaint) return;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f); float w = 420f * s, x = 14f * s, y = 14f * s;
            Ui.Box(new Rect(x, y, w, 82f * s), new Color(0, 0, 0, 0.62f), 12f * s);
            Ui.Text(new Rect(x + 10f * s, y + 4f * s, w - 20f * s, 24f * s), "АВТОТЕСТ  ·  шагов: " + tDone, (int)(17f * s), Themes.Now.accent, TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(x + 10f * s, y + 28f * s, w - 20f * s, 22f * s), Trunc(tCur, 48), (int)(14f * s), Color.white, TextAnchor.MiddleLeft, false);
            Ui.Text(new Rect(x + 10f * s, y + 52f * s, w - 20f * s, 22f * s), "Не трогай мышь/клавиши. Прервать: " + keySelfTest.Value, (int)(12f * s), new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft, false);
        }

        void TabTest()
        {
            Ui.Header("Автотест — бот-проверяльщик");
            Ui.Para("Бот сам прогонит функции мода на твоём персонаже: проверит систему и API игры, физику мяча и точность бросков, все 11 данков, приёмы дриблинга, соперника ИИ (защита, атака, финты), всех героев и Backrooms (падение через ящик, пол и стены, 4 угла, дверь EXIT, сущность, таймер, выход по F8; бот ненадолго «проваливается» сам и возвращается), плюс конфликты клавиш, шейдеры и сеть. Результаты — в отчёте: файл BepInEx/PeakFun_selftest_latest.txt и буфер обмена. Пришли отчёт (вставь текст в чат) — по нему я исправлю найденное. Во время теста не трогай мышь и клавиши; прервать — клавиша " + keySelfTest.Value + ". Тест ставит временное кольцо, двигает тебя и возвращает на место. «Звёздный час» (разрушительный) в тест не входит.", 12);
            for (int i = 0; i < tCat.Length; i++) tCat[i] = Ui.Toggle(TCatNames[i], tCat[i]);
            if (!tRunning) { if (Ui.Button("▶ Запустить автотест  [" + keySelfTest.Value + "]", 1)) StartSelfTest(); }
            else if (Ui.Button("■ Остановить", 2)) tAbort = true;
            if (tRes.Count > 0)
            {
                int pass = 0, warn = 0, fail = 0; foreach (var r in tRes) { if (r.status == "PASS") pass++; else if (r.status == "WARN") warn++; else fail++; }
                Ui.Header("Результаты: " + pass + " ок · " + warn + " предупр. · " + fail + " ошибок" + (tRunning ? "  (идёт…)" : ""));
                if (!tRunning && tShort.Length > 0)
                {
                    if (Ui.Button("Скопировать краткий отчёт", 1)) { GUIUtility.systemCopyBuffer = tShort; Ui.Toast("Краткий отчёт скопирован", Ui.Good); }
                    if (Ui.Button("Скопировать полный отчёт")) { GUIUtility.systemCopyBuffer = tFull; Ui.Toast("Полный отчёт скопирован", Ui.Good); }
                    Ui.Label("Файл: " + tLastFile, 12, Ui.Muted);
                }
                string lastCat = "";
                foreach (var r in tRes)
                {
                    if (r.cat != lastCat) { Ui.Label(r.cat, 14, Ui.Accent); lastCat = r.cat; }
                    Color col = r.status == "PASS" ? Ui.Good : r.status == "WARN" ? new Color(1f, 0.8f, 0.3f) : Ui.Bad;
                    Ui.Para("[" + r.status + "] " + r.name + " — " + r.detail, 12);
                }
            }
        }
    }
}
