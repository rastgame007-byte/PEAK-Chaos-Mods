// PeakMods — модификаторы прохождения для PEAK. Без читов: набор правил общий для отряда и блокируется после старта.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakMods
{
    [BepInPlugin("com.friends.peakmods", "PeakMods", Version)]
    public partial class PeakModsPlugin : BaseUnityPlugin
    {
        public const string Version = "1.0.0";
        ConfigEntry<KeyCode> keyMenu, keyHud; ConfigEntry<bool> cfgAccept, cfgFreeze, cfgDraft; ConfigEntry<float> cfgUiScale; ConfigEntry<string> cfgSel; ConfigEntry<int> cfgTarget, cfgCount;
        bool showMenu; float openT; Vector2 winPos = new Vector2(-1, -1), dragOff, scroll; const float WinW = 980f, WinH = 664f; float contentH = 600f;
        int tab; const int TRun = 0, TTest = 10, TInfo = 11;
        string seedText = ""; int rollTarget = 6, rollCount = 5; bool soloOnly; float tabIndY;

        void Awake()
        {
            I = this; BuildCatalog();
            keyMenu = Config.Bind("Keys", "Menu", KeyCode.F1, "Открыть/закрыть меню модификаторов");
            keyHud = Config.Bind("Keys", "Hud", KeyCode.F2, "Список модификаторов на экране: кратко / подробно / скрыт");
            cfgAccept = Config.Bind("Network", "AcceptFromFriends", true, "Принимать набор модификаторов от друзей, у которых стоит PeakMods");
            cfgFreeze = Config.Bind("UI", "FreezeControlsWhenMenuOpen", true, "Не двигать персонажа, пока открыто меню");
            cfgDraft = Config.Bind("Run", "DraftCards", false, "Драфт карт: на старте и в каждом биоме отряд выбирает 1 из 3 новых модификаторов");
            cfgUiScale = Config.Bind("UI", "Scale", 1f, "Масштаб интерфейса");
            cfgSel = Config.Bind("Run", "Selection", "", "Выбранный набор (служебное поле)");
            cfgTarget = Config.Bind("Run", "RandomTargetPoints", 6, "Целевая сложность случайного набора");
            cfgCount = Config.Bind("Run", "RandomCount", 5, "Сколько модификаторов в случайном наборе");
            ParseSel(cfgSel.Value); rollTarget = cfgTarget.Value; rollCount = cfgCount.Value; Ui.Dark = true;
            string ib; try { ib = InputBlock.Install(); } catch (Exception e) { ib = "Harmony недоступен: " + e.Message; }
            Logger.LogInfo("Блокировка ввода: " + ib);
            Net.OnCommand = OnNet;
            Logger.LogInfo("PeakMods " + Version + " загружен. Меню: " + keyMenu.Value + ". Модификаторов: " + All.Count + ". Игра распознана: " + Game.Ready);
        }

        void SaveSel() { cfgSel.Value = SerSel(); cfgTarget.Value = rollTarget; cfgCount.Value = rollCount; }
        void ToggleMenu() { showMenu = !showMenu; }

        void Update()
        {
            try
            {
                if (Keys.Down(keyMenu.Value)) ToggleMenu();
                if (Keys.Down(keyHud.Value)) hudMode = (hudMode + 2) % 3;
                InputBlock.Want = (showMenu && cfgFreeze.Value) || draftOpen; if (InputBlock.Want) InputBlock.Apply();
                openT = Mathf.MoveTowards(openT, showMenu ? 1f : 0f, Time.unscaledDeltaTime * 5f);
                Net.Tick(); Shade.Tick(); Vfx.Tick();
                float dt = Mathf.Min(Time.deltaTime, 0.1f);
                try { TickLightFog(dt); } catch (Exception e) { Log("свет", e); }
                if (Dry) return;
                RunTick(dt);
                if ((showMenu || draftOpen) && Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            }
            catch (Exception e) { Log("Update", e); }
        }

        void LateUpdate()
        {
            try { if (fovWant > 0f && runOn) { var c = Camera.main; if (c != null) c.fieldOfView = Mathf.Lerp(c.fieldOfView, fovWant, 0.5f); } } catch { }
        }

        void OnGUI()
        {
            try
            {
                var e = Event.current;
                if (e.type == EventType.KeyDown) { if (e.keyCode == keyMenu.Value && Keys.Fire(keyMenu.Value)) ToggleMenu(); else if (e.keyCode == keyHud.Value && Keys.Fire(keyHud.Value)) hudMode = (hudMode + 2) % 3; }
                if ((showMenu || draftOpen) && e.type == EventType.Layout) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                Ui.Init(); Ui.Dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f); Ui.Time_ = Time.unscaledTime; Ui.Alpha = 1f;
                float s = Mathf.Clamp(Screen.height / 1080f * cfgUiScale.Value, 0.7f, 3f), vw = Screen.width / s, vh = Screen.height / s;
                DrawRunHud(Screen.height / 1080f);
                var baseMatrix = Matrix4x4.Scale(new Vector3(s, s, 1f)); GUI.matrix = baseMatrix;
                Ui.DrawToasts(vw);
                if (openT > 0.002f) DrawWindow(vw, vh, baseMatrix);
                GUI.matrix = Matrix4x4.identity; Ui.Alpha = 1f;
            }
            catch (ArgumentException) { throw; }
            catch (Exception ex) { Log("OnGUI", ex); }
        }

        // ================= окно =================
        void DrawWindow(float vw, float vh, Matrix4x4 baseMatrix)
        {
            var e = Event.current;
            if (winPos.x < 0) winPos = new Vector2((vw - WinW) / 2f, (vh - WinH) / 2f);
            winPos.x = Mathf.Clamp(winPos.x, -WinW + 120, vw - 120); winPos.y = Mathf.Clamp(winPos.y, 0, vh - 40);
            float eo = Ui.EaseOut(openT); Ui.Alpha = eo; var rect = new Rect(winPos.x, winPos.y, WinW, WinH); var pivot = rect.center;
            if (openT < 0.999f) { float k = 0.9f + 0.1f * eo; GUI.matrix = baseMatrix * Matrix4x4.Translate(new Vector3(pivot.x, pivot.y + (1f - eo) * 24f, 0)) * Matrix4x4.Scale(new Vector3(k, k, 1f)) * Matrix4x4.Translate(new Vector3(-pivot.x, -pivot.y, 0)); }
            Ui.Box(new Rect(rect.x - 10, rect.y - 6, rect.width + 20, rect.height + 24), new Color(0, 0, 0, 0.3f), 26); Ui.Box(rect, Ui.Bg, 18);
            var header = new Rect(rect.x, rect.y, rect.width - 60, 64); int dragId = GUIUtility.GetControlID(FocusType.Passive);
            switch (e.GetTypeForControl(dragId))
            {
                case EventType.MouseDown: if (e.button == 0 && header.Contains(e.mousePosition)) { GUIUtility.hotControl = dragId; dragOff = e.mousePosition - winPos; e.Use(); } break;
                case EventType.MouseDrag: if (GUIUtility.hotControl == dragId) { winPos = e.mousePosition - dragOff; e.Use(); } break;
                case EventType.MouseUp: if (GUIUtility.hotControl == dragId) { GUIUtility.hotControl = 0; e.Use(); } break;
            }
            GUI.BeginGroup(rect); float t = Ui.Time_;
            Ui.Orb(new Vector2(120 + Mathf.Sin(t * 0.5f) * 80, 90 + Mathf.Cos(t * 0.4f) * 40), 420, Ui.Orb1); Ui.Orb(new Vector2(WinW - 140 + Mathf.Cos(t * 0.35f) * 90, 140), 460, Ui.Orb2);
            Ui.Gradient(new Rect(0, 0, WinW, 4), 1f, t * 0.12f);
            Ui.Text(new Rect(26, 12, 260, 40), "PeakMods", 30, Ui.Txt, TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(190, 14, 300, 20), "v" + Version + " · прохождение с модификаторами", 13, Ui.Muted, TextAnchor.MiddleLeft);
            Ui.Text(new Rect(190, 34, 300, 20), keyMenu.Value + " — закрыть меню · " + keyHud.Value + " — список на экране", 12, Ui.Muted, TextAnchor.MiddleLeft);
            int pts = runOn ? Points(active.Select(m => m.id)) : Points(sel.Keys);
            Ui.Text(new Rect(WinW - 500, 20, 440, 26), (runOn ? "ЗАБЕГ ИДЁТ" : "набор") + ": " + (pts > 0 ? "+" : "") + pts + " · " + DiffLabel(pts), 15, DiffColor(pts), TextAnchor.MiddleRight, true);
            if (Ui.ButtonRect(new Rect(WinW - 52, 14, 38, 38), "X", 2, true, 16)) showMenu = false;

            var side = new Rect(12, 72, 196, WinH - 84); Ui.Box(side, Ui.Side, 14);
            string[] names = new string[12]; names[0] = "Забег"; for (int i = 0; i < 9; i++) names[i + 1] = CatNames[i]; names[10] = "Проверка"; names[11] = "Инфо";
            float rowH = 38f, y0 = side.y + 10; float selY = y0 + tab * (rowH + 4f); if (tabIndY < 1f) tabIndY = selY; tabIndY = Mathf.Lerp(tabIndY, selY, 1f - Mathf.Exp(-14f * Ui.Dt));
            if (e.type == EventType.Repaint) { Ui.Box(new Rect(side.x + 6, tabIndY, side.width - 12, rowH), new Color(1f, 0.45f, 0.25f, 0.2f), 10); Ui.Box(new Rect(side.x + 6, tabIndY + rowH * 0.22f, 4, rowH * 0.56f), Ui.Accent, 2); }
            for (int i = 0; i < 12; i++)
            {
                var tr = new Rect(side.x + 6, y0 + i * (rowH + 4f), side.width - 12, rowH); int cnt = 0;
                if (i >= 1 && i <= 9) { foreach (var m in All) if (m.c == i - 1 && (runOn ? active.Contains(m) : sel.ContainsKey(m.id))) cnt++; }
                if (TabButton(tr, names[i] + (cnt > 0 ? "  · " + cnt : ""), i == tab, i >= 1 && i <= 9 ? CatCols[i - 1] : Ui.Txt)) { tab = i; scroll = Vector2.zero; }
            }

            var area = new Rect(220, 72, WinW - 232, WinH - 84); Ui.Box(area, new Color(1, 1, 1, 0.025f), 14);
            float cw = area.width - 26, ch = Mathf.Max(contentH, area.height);
            scroll = GUI.BeginScrollView(new Rect(area.x, area.y + 4, area.width, area.height - 8), scroll, new Rect(0, 0, area.width - 16, ch), false, false, GUIStyle.none, GUIStyle.none);
            Ui.Begin(12, 6, cw);
            try
            {
                if (tab == TRun) TabRun(); else if (tab == TTest) TabTest(); else if (tab == TInfo) TabInfo(); else TabCat(tab - 1);
            }
            catch (ArgumentException) { throw; }
            catch (Exception ex) { Log("Tab " + tab, ex); }
            if (e.type == EventType.Repaint || e.type == EventType.Layout) contentH = Ui.Y + 16;
            GUI.EndScrollView();
            if (contentH > area.height && e.type == EventType.Repaint)
            {
                float vis = (area.height - 8) / contentH, barH = Mathf.Max(30, (area.height - 16) * vis), barY = area.y + 8 + (area.height - 16 - barH) * (scroll.y / Mathf.Max(1f, contentH - (area.height - 8)));
                Ui.Box(new Rect(area.xMax - 8, barY, 4, barH), new Color(1, 1, 1, 0.25f), 2);
            }
            GUI.EndGroup(); GUI.matrix = baseMatrix; Ui.Alpha = 1f;
        }

        bool TabButton(Rect r, string label, bool selected, Color col)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive); var e = Event.current; bool clicked = false;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown: if (e.button == 0 && r.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); } break;
                case EventType.MouseUp: if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); if (r.Contains(e.mousePosition)) clicked = true; } break;
                case EventType.Repaint:
                    float h = Ui.Anim("tab" + label, r.Contains(e.mousePosition) ? 1f : 0f, 14f);
                    if (!selected && h > 0.01f) Ui.Box(r, new Color(1, 1, 1, 0.06f * h), 10);
                    Ui.Box(new Rect(r.x + 14, r.y + r.height * 0.5f - 4, 8, 8), col, 4);
                    Ui.Text(new Rect(r.x + 30 + (selected ? 4 : h * 3), r.y, r.width - 34, r.height), label, 15, selected ? Color.white : Color.Lerp(Ui.Muted, Ui.Txt, h), TextAnchor.MiddleLeft, selected);
                    break;
            }
            return clicked;
        }

        // ================= вкладка «Забег» =================
        bool CanControl { get { return !runOn || !Game.InRoom || runHost == Game.LocalActor(); } }

        void TabRun()
        {
            Ui.Header(runOn ? "Идёт забег" : "Новый забег");
            if (runOn)
            {
                int pts = Points(active.Select(m => m.id));
                Ui.Label("Сложность: " + DiffLabel(pts) + " (" + (pts > 0 ? "+" : "") + pts + ") · время " + Mmss(Time.time - runStart) + " · seed " + runSeed, 15, DiffColor(pts), true);
                Ui.Para("Правила общие и заблокированы: у гостей нет выключателей, менять набор посреди забега нельзя. " + (runDraft ? "Драфт карт включён — новые карты добавляет голосование отряда." : ""), 13, Ui.Muted);
                foreach (var m in active) { var r = Ui.Next(34, 4); Ui.Box(r, Ui.Card, 8); Ui.Box(new Rect(r.x + 8, r.y + 11, 12, 12), CatColsSafe(m.c), 6); Ui.Text(new Rect(r.x + 30, r.y, r.width - 140, r.height), m.name + (m.HasParam ? "  [" + m.ParamText() + "]" : "") + (m.broken ? "  (сбой)" : ""), 14, Ui.Txt, TextAnchor.MiddleLeft, false); Ui.Text(new Rect(r.xMax - 100, r.y, 90, r.height), (m.pts > 0 ? "+" : "") + m.pts, 14, m.pts > 0 ? Ui.Bad : m.pts < 0 ? Ui.Good : Ui.Muted, TextAnchor.MiddleRight, true); }
                Ui.Space(8);
                if (CanControl) { if (Ui.Button("ЗАВЕРШИТЬ ЗАБЕГ (для всех)", 1)) StopRun(true); } else Ui.Label("Завершить забег может только запустивший: " + Game.PlayerName(runHost), 14, Ui.Muted);
                if (LastSummary != "") { Ui.Header("Итог прошлого забега"); Ui.Para(LastSummary, 12, Ui.Muted); }
                return;
            }
            int p = Points(sel.Keys);
            Ui.Label("Выбрано: " + sel.Count + " · сложность " + DiffLabel(p) + " (" + (p > 0 ? "+" : "") + p + ")", 16, DiffColor(p), true);
            foreach (var kv in sel.OrderBy(k => ById[k.Key].c)) { var m = ById[kv.Key]; var r = Ui.Next(28, 3); Ui.Box(new Rect(r.x + 4, r.y + 9, 10, 10), CatColsSafe(m.c), 5); Ui.Text(new Rect(r.x + 22, r.y, r.width - 40, r.height), m.name + (m.HasParam ? " [" + (m.choices != null ? m.choices[Mathf.RoundToInt(kv.Value)] : kv.Value.ToString(m.fmt) + m.unit) + "]" : ""), 13, Ui.Txt, TextAnchor.MiddleLeft, false); Ui.Text(new Rect(r.xMax - 80, r.y, 70, r.height), (m.pts > 0 ? "+" : "") + m.pts, 13, m.pts > 0 ? Ui.Bad : m.pts < 0 ? Ui.Good : Ui.Muted, TextAnchor.MiddleRight, true); }
            if (sel.Count == 0) Ui.Para("Пока ничего не выбрано. Включи галочки в категориях слева, возьми пресет или брось кубики ниже.", 13, Ui.Muted);
            Ui.Space(6);
            bool draft = Ui.Toggle("Драфт карт (3 карты на старте и в каждом биоме)", cfgDraft.Value, "Отряд голосует, какой модификатор добавить. Решает большинство, ничья — по жребию"); if (draft != cfgDraft.Value) cfgDraft.Value = draft;
            if (Game.InRoom) soloOnly = Ui.Toggle("Только у меня (не рассылать друзьям)", soloOnly, "Для пробы одному. Обычно выключено — набор уходит всему отряду");
            else soloOnly = true;
            seedText = Ui.Field(seedText, "seed (пусто — случайный)");
            if (Ui.Button(Game.InRoom && !soloOnly ? "НАЧАТЬ ЗАБЕГ ДЛЯ ВСЕГО ОТРЯДА" : "НАЧАТЬ ЗАБЕГ", 0, sel.Count > 0 || draft))
            {
                int seed; if (!int.TryParse(seedText, out seed)) seed = UnityEngine.Random.Range(1, 999999);
                SaveSel(); StartRun(new Dictionary<string, float>(sel), seed, draft, !soloOnly);
            }
            if (sel.Count > 0 && Ui.Button("Сбросить выбор", 2)) { sel.Clear(); SaveSel(); }

            Ui.Header("Пресеты");
            var labels = Presets.Select(pr => pr[0]).ToArray(); int pi = Ui.Buttons(labels, 3, 0, 36);
            if (pi >= 0) { ApplyPreset(pi); SaveSel(); Ui.Toast("Пресет «" + Presets[pi][0] + "»: " + Presets[pi][1], Ui.Accent); }
            Ui.Para("Пресет заменяет текущий выбор. Потом можно докрутить галочками.", 12, Ui.Muted);

            Ui.Header("Случайный набор");
            rollTarget = Mathf.RoundToInt(Ui.Slider("Целевая сложность", rollTarget, -10, 30, "0", " очк."));
            rollCount = Mathf.RoundToInt(Ui.Slider("Сколько модификаторов", rollCount, 2, 12, "0", " шт."));
            if (Ui.Button("Бросить кубики", 0))
            {
                int seed; if (!int.TryParse(seedText, out seed)) { seed = UnityEngine.Random.Range(1, 999999); seedText = seed.ToString(); }
                sel.Clear(); foreach (var id in RollSet(seed, rollTarget, rollCount)) SelSet(id, true); SaveSel(); Ui.Toast("Случайный набор (seed " + seed + "): " + sel.Count + " мод.", Ui.Accent);
            }
            Ui.Para("С одним и тем же seed набор получается одинаковый — можно договориться заранее.", 12, Ui.Muted);
        }
        static Color CatColsSafe(int c) { return CatCols[Mathf.Clamp(c, 0, CatCols.Length - 1)]; }

        // ================= вкладка категории =================
        void TabCat(int c)
        {
            Ui.Header(CatNames[c]); Ui.Para(CatDescr[c], 13, Ui.Muted);
            if (runOn) Ui.Label("Забег идёт — набор заблокирован.", 13, Ui.Accent);
            bool prev = GUI.enabled; GUI.enabled = prev && !runOn;
            foreach (var m in All)
            {
                if (m.c != c) continue; bool on = runOn ? active.Contains(m) : sel.ContainsKey(m.id);
                string label = m.name + "   " + (m.pts > 0 ? "+" + m.pts : m.pts < 0 ? m.pts.ToString() : "±0");
                bool nv = Ui.Toggle(label, on, m.desc);
                if (nv != on && !runOn) { SelSet(m.id, nv); SaveSel(); }
                if (on && !runOn && m.HasParam)
                {
                    float v = sel[m.id];
                    if (m.choices != null) { int k = Ui.Buttons(m.choices, Mathf.Min(m.choices.Length, 4), 0, 30, true, Mathf.RoundToInt(v), 13); if (k >= 0) { sel[m.id] = k; SaveSel(); } }
                    else { float nvv = Ui.Slider("  " + m.name + " — значение", v, m.min, m.max, m.fmt, m.unit); if (Math.Abs(nvv - v) > 0.0001f) { sel[m.id] = nvv; } if (Event.current.type == EventType.MouseUp) SaveSel(); }
                }
            }
            GUI.enabled = prev;
        }
        static readonly string[] CatDescr =
        {
            "Помогают забраться. Дают минус к сложности.", "Делают подъём тяжелее.", "Правила забега: таймеры, запреты, приливы.", "Откуда берутся припасы и что в них.", "Картинка: туман, сумерки, фильтры. Только для вас, на геймплей почти не влияет.",
            "Голод, холод, сон — мелкая забота о себе по ходу подъёма.", "Опасности на склонах: ветер, камни, мины, зоны.", "Награды за успехи отряда.", "Командные правила: держаться вместе, верёвка, роли.",
        };

        // ================= инфо =================
        void TabInfo()
        {
            Ui.Header("Как это работает");
            Ui.Para("PeakMods — не чит-меню. Это набор правил для честного прохождения. Выбираете набор модификаторов (галочки, пресет, случайный набор или драфт карт), один из вас жмёт «Начать забег» — набор приходит ВСЕМ у кого стоит мод, и после старта заблокирован у каждого. Никто в одиночку ничего не выключает; закончить забег для всех может тот, кто запустил.", 14);
            Ui.Header("Сложность");
            Ui.Para("Каждый модификатор стоит очков: минус — легче, плюс — тяжелее. Сумма даёт ярлык: до −6 «для отдыха», −5…−1 «лёгкий», 0…5 «обычный», 6…12 «сложный», 13…20 «жёсткий», выше — «кошмар».", 14);
            Ui.Header("Клавиши");
            Ui.Para(keyMenu.Value + " — меню · " + keyHud.Value + " — список модификаторов на экране (кратко / подробно / скрыт). Менять в BepInEx\\config\\com.friends.peakmods.cfg.", 14);
            Ui.Header("Синхронизация");
            Ui.Para("Мод нужен у всех участников. События (ветер, приливы, камни) рассчитываются по общему seed и времени, поэтому у всех происходят одновременно. Если мода нет у кого-то из друзей, у него эти правила просто не сработают. Статус канала: " + Net.Status, 14);
            Ui.Header("Итог");
            Ui.Para("После забега в BepInEx\\PeakMods_run_last.txt сохраняется итог: время, биомы, падения, набор и журнал. Мод не проверялся в игре автором — при проблемах пришлите BepInEx\\LogOutput.log и отчёт вкладки «Проверка».", 14);
        }
    }
}
