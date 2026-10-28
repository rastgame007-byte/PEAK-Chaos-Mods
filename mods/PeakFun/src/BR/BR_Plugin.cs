// PeakBackrooms — отдельный мод для PEAK: огромные Backrooms из 16 уровней и монстры. Вход кнопкой в меню, весь отряд вместе.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakBR
{
    #if DEV
    [BepInPlugin("com.friends.peakbackrooms.dev", "PeakBackroomsDEV", Version)]
#else
    [BepInPlugin("com.friends.peakbackrooms", "PeakBackrooms", Version)]
#endif
    public class Plugin : BaseUnityPlugin
    {
        public const string Version = "1.0.0";
        public static Plugin Inst; public static Action OnStartHook;
        ConfigEntry<KeyCode> keyMenu, keyPanic; ConfigEntry<bool> cfgAccept, cfgCam; ConfigEntry<int> cfgSize, cfgDiff, cfgStart; ConfigEntry<float> cfgUiScale;
        bool showMenu; float openT; Vector2 winPos = new Vector2(-1, -1), dragOff, scroll; const float WinW = 940f, WinH = 640f; float contentH = 600f; int tab; float tabIndY;
        string[] tabNames = { "Вход", "Уровни", "Монстры", "Проверка", "Инфо" };

        public static void Log(string s) { try { if (Inst != null) Inst.Logger.LogInfo(s); } catch { } }
        public static void Warn(string where, Exception e) { try { if (Inst != null) Inst.Logger.LogWarning("[" + where + "] " + e.GetType().Name + ": " + e.Message); } catch { } }

        void Awake()
        {
            Inst = this;
            keyMenu = Config.Bind("Keys", "Menu", KeyCode.Home, "Открыть/закрыть меню Backrooms");
            keyPanic = Config.Bind("Keys", "Leave", KeyCode.End, "Аварийный выход из Backrooms (только для себя)");
            Torch.Key = Config.Bind("Keys", "Torch", KeyCode.F, "Фонарик в Backrooms (включить/выключить)").Value;
            cfgAccept = Config.Bind("Network", "AcceptFromFriends", true, "Автоматически входить в Backrooms, когда друг с PeakBackrooms нажал «Войти»");
            cfgCam = Config.Bind("Run", "CassetteCamera", true, "Эффект видеокамеры-кассеты (помехи, REC)");
            cfgSize = Config.Bind("Run", "MapSize", 2, "Размер карт: 0 малая, 1 обычная, 2 огромная, 3 безумная");
            cfgDiff = Config.Bind("Run", "Difficulty", 2, "Сложность: 0 лёгкая, 1 средняя, 2 жёсткая");
            cfgStart = Config.Bind("Run", "StartLevel", 0, "С какого уровня начинать (0 — Лобби)");
            cfgUiScale = Config.Bind("UI", "Scale", 1f, "Масштаб интерфейса");
            Play.AcceptRun = cfgAccept.Value; Play.PanicKey = keyPanic.Value; Play.SizeIdx = Mathf.Clamp(cfgSize.Value, 0, 3); Play.Diff = Mathf.Clamp(cfgDiff.Value, 0, 2); Play.CamFxOn = cfgCam.Value; Ui.Dark = true;
            string ib; try { ib = InputBlock.Install(); } catch (Exception e) { ib = "Harmony недоступен: " + e.Message; }
            Logger.LogInfo("Блокировка ввода: " + ib);
            Net.OnCommand = Play.OnNet;
            Logger.LogInfo("PeakBackrooms " + Version + " загружен. Меню: " + keyMenu.Value + ". Уровней: " + Levels.All.Length + ". Игра распознана: " + Game.Ready);
        }

        void Update()
        {
            try
            {
                if (Keys.Down(keyMenu.Value)) showMenu = !showMenu;
                InputBlock.Want = (showMenu && !Play.Catching) || Play.Catching; if (InputBlock.Want) InputBlock.Apply();
                openT = Mathf.MoveTowards(openT, showMenu ? 1f : 0f, Time.unscaledDeltaTime * 5f);
                Net.Tick(); Shade.Tick(); Vfx.Tick();
                float dt = Mathf.Min(Time.deltaTime, 0.1f);
                try { Play.Tick(dt); } catch (Exception e) { Warn("Play.Tick", e); }
                try { Dev.Tick(dt); } catch (Exception e) { Warn("Dev", e); }
                if (showMenu && Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            }
            catch (Exception e) { Warn("Update", e); }
        }

        void OnGUI()
        {
            try
            {
                var e = Event.current;
                if (e.type == EventType.KeyDown && e.keyCode == keyMenu.Value && Keys.Fire(keyMenu.Value)) showMenu = !showMenu;
                if (showMenu && e.type == EventType.Layout) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                Ui.Init(); Ui.Dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f); Ui.Time_ = Time.unscaledTime; Ui.Alpha = 1f;
                float s = Mathf.Clamp(Screen.height / 1080f * cfgUiScale.Value, 0.7f, 3f), vw = Screen.width / s, vh = Screen.height / s;
                if (e.type == EventType.Repaint) { Play.DrawOverlay(Screen.width, Screen.height, Screen.height / 1080f); Dev.Draw(Screen.width, Screen.height, Screen.height / 1080f); }
                var baseMatrix = Matrix4x4.Scale(new Vector3(s, s, 1f)); GUI.matrix = baseMatrix;
                Ui.DrawToasts(vw);
                if (openT > 0.002f) DrawWindow(vw, vh, baseMatrix);
                GUI.matrix = Matrix4x4.identity; Ui.Alpha = 1f;
            }
            catch (ArgumentException) { throw; }
            catch (Exception ex) { Warn("OnGUI", ex); }
        }

        void DrawWindow(float vw, float vh, Matrix4x4 baseMatrix)
        {
            var e = Event.current;
            if (winPos.x < 0) winPos = new Vector2((vw - WinW) / 2f, (vh - WinH) / 2f);
            winPos.x = Mathf.Clamp(winPos.x, -WinW + 120, vw - 120); winPos.y = Mathf.Clamp(winPos.y, 0, vh - 40);
            float eo = Ui.EaseOut(openT); Ui.Alpha = eo; var rect = new Rect(winPos.x, winPos.y, WinW, WinH); var pivot = rect.center;
            if (openT < 0.999f) { float k = 0.9f + 0.1f * eo; GUI.matrix = baseMatrix * Matrix4x4.Translate(new Vector3(pivot.x, pivot.y + (1f - eo) * 24f, 0)) * Matrix4x4.Scale(new Vector3(k, k, 1f)) * Matrix4x4.Translate(new Vector3(-pivot.x, -pivot.y, 0)); }
            Ui.Box(new Rect(rect.x - 10, rect.y - 6, rect.width + 20, rect.height + 24), new Color(0, 0, 0, 0.3f), 26); Ui.Box(rect, Ui.Bg, 18); Ui.Border(rect, new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.38f), 1f);
            var header = new Rect(rect.x, rect.y, rect.width - 60, 64); int dragId = GUIUtility.GetControlID(FocusType.Passive);
            switch (e.GetTypeForControl(dragId))
            {
                case EventType.MouseDown: if (e.button == 0 && header.Contains(e.mousePosition)) { GUIUtility.hotControl = dragId; dragOff = e.mousePosition - winPos; e.Use(); } break;
                case EventType.MouseDrag: if (GUIUtility.hotControl == dragId) { winPos = e.mousePosition - dragOff; e.Use(); } break;
                case EventType.MouseUp: if (GUIUtility.hotControl == dragId) { GUIUtility.hotControl = 0; e.Use(); } break;
            }
            GUI.BeginGroup(rect); float t = Ui.Time_;
            Ui.Orb(new Vector2(120 + Mathf.Sin(t * 0.5f) * 80, 90 + Mathf.Cos(t * 0.4f) * 40), 420, new Color(1f, 0.9f, 0.2f, 0.10f)); Ui.Orb(new Vector2(WinW - 140 + Mathf.Cos(t * 0.35f) * 90, 140), 460, new Color(1f, 0.3f, 0.2f, 0.08f));
            Ui.Scan(new Rect(0, 0, WinW, WinH), 0.07f); Ui.Brackets(new Rect(3, 3, WinW - 6, WinH - 6), new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.85f), 20f, 2f);
            Ui.Gradient(new Rect(0, 0, WinW, 4), 1f, t * 0.12f);
            Ui.Text(new Rect(26, 12, 300, 40), "PeakBackrooms", 30, Ui.Txt, TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(260, 14, 400, 20), "v" + Version + " · " + Levels.All.Length + " уровней, монстры, весь отряд вместе", 13, Ui.Muted, TextAnchor.MiddleLeft);
            Ui.Text(new Rect(260, 34, 500, 20), keyMenu.Value + " — закрыть меню · " + keyPanic.Value + " — выйти из Backrooms", 12, Ui.Muted, TextAnchor.MiddleLeft);
            if (Play.On) Ui.Text(new Rect(WinW - 500, 20, 440, 26), "ЗАБЕГ ИДЁТ: " + (Play.L != null ? Play.L.name : ""), 15, Ui.Accent, TextAnchor.MiddleRight, true);
            if (Ui.ButtonRect(new Rect(WinW - 52, 14, 38, 38), "X", 2, true, 16)) showMenu = false;
            var side = new Rect(12, 72, 190, WinH - 84); Ui.Box(side, Ui.Side, 14); float rowH = 38f, y0 = side.y + 10; float selY = y0 + tab * (rowH + 4f); if (tabIndY < 1f) tabIndY = selY; tabIndY = Mathf.Lerp(tabIndY, selY, 1f - Mathf.Exp(-14f * Ui.Dt));
            if (e.type == EventType.Repaint) { Ui.Box(new Rect(side.x + 6, tabIndY, side.width - 12, rowH), new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.17f), 10); Ui.Border(new Rect(side.x + 6, tabIndY, side.width - 12, rowH), new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.4f), 1f); Ui.Box(new Rect(side.x + 6, tabIndY + rowH * 0.22f, 4, rowH * 0.56f), Ui.Accent, 2); }
            for (int i = 0; i < tabNames.Length; i++) { if (TabButton(new Rect(side.x + 6, y0 + i * (rowH + 4f), side.width - 12, rowH), tabNames[i], i == tab)) { tab = i; scroll = Vector2.zero; } }
            var area = new Rect(214, 72, WinW - 226, WinH - 84); Ui.Box(area, new Color(1, 1, 1, 0.025f), 14);
            float cw = area.width - 26, ch = Mathf.Max(contentH, area.height);
            scroll = GUI.BeginScrollView(new Rect(area.x, area.y + 4, area.width, area.height - 8), scroll, new Rect(0, 0, area.width - 16, ch), false, false, GUIStyle.none, GUIStyle.none);
            Ui.Begin(12, 6, cw);
            try { if (tab == 0) TabEnter(); else if (tab == 1) TabLevels(); else if (tab == 2) TabMonsters(); else if (tab == 3) TabTest(); else TabInfo(); }
            catch (ArgumentException) { throw; }
            catch (Exception ex) { Warn("Tab " + tab, ex); }
            if (e.type == EventType.Repaint || e.type == EventType.Layout) contentH = Ui.Y + 16;
            GUI.EndScrollView();
            if (contentH > area.height && e.type == EventType.Repaint)
            {
                float vis = (area.height - 8) / contentH, barH = Mathf.Max(30, (area.height - 16) * vis), barY = area.y + 8 + (area.height - 16 - barH) * (scroll.y / Mathf.Max(1f, contentH - (area.height - 8)));
                Ui.Box(new Rect(area.xMax - 8, barY, 4, barH), new Color(1, 1, 1, 0.25f), 2);
            }
            GUI.EndGroup(); GUI.matrix = baseMatrix; Ui.Alpha = 1f;
        }

        /// <summary>Вызов из меню PeakFun: рисует вкладку Backrooms внутри чужого окна. Возвращает новую позицию Y.</summary>
        public static float DrawEmbedded(int which, float x, float y, float w, float alpha)
        {
            if (Inst == null) return y;
            Ui.Init(); Ui.Dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f); Ui.Time_ = Time.unscaledTime; Ui.Alpha = alpha; Ui.Begin(x, y, w);
            try { switch (which) { case 0: Inst.TabEnter(); break; case 1: Inst.TabLevels(); break; case 2: Inst.TabMonsters(); break; case 3: Inst.TabTest(); break; default: Inst.TabInfo(); break; } }
            catch (ArgumentException) { throw; }
            catch (Exception ex) { Warn("embedded " + which, ex); }
            return Ui.Y;
        }
        public static void OpenOwnMenu() { if (Inst != null) Inst.showMenu = true; }

        bool TabButton(Rect r, string label, bool selected)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive); var e = Event.current; bool clicked = false;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown: if (e.button == 0 && r.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); } break;
                case EventType.MouseUp: if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); if (r.Contains(e.mousePosition)) clicked = true; } break;
                case EventType.Repaint:
                    float h = Ui.Anim("tab" + label, r.Contains(e.mousePosition) ? 1f : 0f, 14f);
                    if (!selected && h > 0.01f) Ui.Box(r, new Color(1, 1, 1, 0.06f * h), 10);
                    Ui.Text(new Rect(r.x + 18 + (selected ? 4 : h * 3), r.y, r.width - 22, r.height), label, 16, selected ? Color.white : Color.Lerp(Ui.Muted, Ui.Txt, h), TextAnchor.MiddleLeft, selected);
                    break;
            }
            return clicked;
        }

        // ================= вкладки =================
        void TabEnter()
        {
            Ui.Header("Войти в Backrooms");
            Ui.Para("Нажми кнопку — все друзья с этим модом в твоей комнате автоматически войдут вместе с тобой (телепорт в огромный лабиринт над горой). Цель: пройти все " + Levels.All.Length + " уровней — выход каждого ведёт в следующий. Последний возвращает вас на гору. Если поймали — вернут в начало уровня.", 14, Ui.Muted);
            Ui.Space(6);
            if (Play.On)
            {
                Ui.Label("Забег идёт · уровень " + (Play.Level + 1) + "/" + Levels.All.Length + " · " + (Play.L != null ? Play.L.name : ""), 16, Ui.Good, true);
                if (Ui.Button("Выйти из Backrooms (только я)", 2)) Play.Leave(false, "из меню");
                if (Play.IsHost && Ui.Button("Пропустить уровень (для отряда, только хост)", 0)) Play.Skip();
                Ui.Para("Хост сейчас — игрок с наименьшим номером в комнате; он считает монстров, остальные видят то же самое. Если хост выйдет, роль перейдёт другому.", 13, Ui.Muted);
                return;
            }
            Ui.Label("Размер карт", 15, Ui.Txt, true);
            int sz = Ui.Buttons(new[] { "Малая ×0.7", "Обычная ×1", "Огромная ×1.5", "Безумная ×2" }, 4, 0, 34f, true, Play.SizeIdx, 14); if (sz >= 0) { Play.SizeIdx = sz; cfgSize.Value = sz; }
            int lv0 = Levels.All[0].baseN; Ui.Para("Лобби при этом размере: " + Play.LevelN(Levels.All[0], Play.SizeIdx) + "×" + Play.LevelN(Levels.All[0], Play.SizeIdx) + " клеток по 4 м ≈ " + (Play.LevelN(Levels.All[0], Play.SizeIdx) * 4) + " м в ширину. Больше карта — дольше загрузка уровня.", 13, Ui.Muted);
            Ui.Label("Сложность (жёсткий хоррор)", 15, Ui.Txt, true);
            int df = Ui.Buttons(new[] { "Лёгкая", "Средняя", "Жёсткая" }, 3, 0, 34f, true, Play.Diff, 14); if (df >= 0) { Play.Diff = df; cfgDiff.Value = df; }
            Ui.Para("Лёгкая: монстры медленнее, поимка без штрафа. Жёсткая: быстрее, после поимки — отравление и сонливость.", 13, Ui.Muted);
            bool cm = Ui.Toggle("Камера-кассета (помехи, REC, таймкод)", Play.CamFxOn); if (cm != Play.CamFxOn) { Play.CamFxOn = cm; cfgCam.Value = cm; }
            bool ac = Ui.Toggle("Входить автоматически, когда друг нажал «Войти»", Play.AcceptRun); if (ac != Play.AcceptRun) { Play.AcceptRun = ac; cfgAccept.Value = ac; }
            Ui.Label("С какого уровня начать", 15, Ui.Txt, true); var names = new string[Levels.All.Length]; for (int i = 0; i < names.Length; i++) names[i] = Levels.All[i].id; int stl = Ui.Buttons(names, 8, 0, 34f, true, cfgStart.Value, 13); if (stl >= 0) cfgStart.Value = stl;
            Ui.Para("Выбран: " + Levels.All[Mathf.Clamp(cfgStart.Value, 0, Levels.All.Length - 1)].name, 13, Ui.Muted);
            Ui.Space(8);
            if (Ui.Button("ВОЙТИ В BACKROOMS — ВСЕМ ОТРЯДОМ", 1)) { showMenu = false; if (OnStartHook != null) OnStartHook(); Play.StartRun(Mathf.Clamp(cfgStart.Value, 0, Levels.All.Length - 1)); }
            Ui.Para("Перед входом соберитесь вместе: мод у ВСЕХ должен быть установлен. Выйти можно в любой момент клавишей " + keyPanic.Value + " (только для себя).", 13, Ui.Muted);
        }

        static string MonsterList(LevelDef d)
        {
            var l = new List<string>(); if (d.stealers > 0) l.Add("Вор Кожи ×" + d.stealers); if (d.smilers > 0) l.Add("Смайлер ×" + d.smilers); if (d.hounds > 0) l.Add("Гончие ×" + d.hounds); if (d.party > 0) l.Add("Гости ×" + d.party); if (d.moths > 0) l.Add("Мотыльки ×" + d.moths);
            if (d.bacteria > 0) l.Add("Бактерия ×" + d.bacteria); if (d.clumps > 0) l.Add("Комки ×" + d.clumps); if (d.windows > 0) l.Add("Окна ×" + d.windows); if (d.dullers > 0) l.Add("Дуллеры ×" + d.dullers); if (d.faces > 0) l.Add("Безликие ×" + d.faces); if (d.kids > 0) l.Add("Безликие-дети ×" + d.kids); if (d.wretches > 0) l.Add("Горемыки ×" + d.wretches); if (d.things > 0) l.Add("Твари с Ур.7 ×" + d.things); if (d.watchers > 0) l.Add("Дозорные ×" + d.watchers);
            if (d.rest) l.Add("безопасно (но тёмная вода — яд)"); return l.Count == 0 ? "—" : string.Join(", ", l.ToArray());
        }
        void TabLevels()
        {
            Ui.Header("Уровни (по порядку)");
            for (int i = 0; i < Levels.All.Length; i++)
            {
                var d = Levels.All[i]; Ui.Label((i + 1) + ". " + d.name + "  ·  " + d.eng, 16, Ui.Accent, true);
                Ui.Para(d.blurb, 13, Ui.Txt); Ui.Para("Монстры (при размере ×1): " + MonsterList(d) + "  ·  основа " + d.baseN + "×" + d.baseN + (d.style == Style.Snake ? " (длинный коридор)" : "") + "\nСовет: " + d.tip, 12, Ui.Muted); Ui.Space(6);
            }
        }
        void TabMonsters()
        {
            Ui.Header("Монстры");
            string[,] m = {
                { "Вор Кожи (Skin-Stealer)", "Серая кожа, провалившиеся глаза, длинные руки. Сначала только подглядывает издалека и исчезает, если на него долго смотреть. Потом подкрадывается, а затем бежит и рычит. Чем дольше ты на уровне — тем он злее и искажённее." },
                { "Смайлер (Smiler)", "Чёрная тень со светящимися глазами и улыбкой. Двигается ТОЛЬКО когда на него никто не смотрит. Пока смотришь — стоит. Не оборачивайся надолго." },
                { "Гончая (Hound)", "Собака с человеческой кожей и бледными глазами. Патрулирует, при виде тебя рычит и вся стая бросается в погоню. Чуть быстрее обычного бега. Теряет след, если ты скрылся на 7 секунд." },
                { "Гость вечеринки (Partygoer)", "Высокий жёлтый, с красным шаром. Пока ничего не замечает — танцует. Заметит — гонятся все гости рядом. Присядь (кнопка приседания), и они успокоятся." },
                { "Мотылёк-Смерть (Deathmoth)", "Летает у ламп. Налетает и оставляет споры (яд и сонливость). Не ловит, но изматывает." },
                { "Бактерия (Bacteria)", "Чёрный тонкий силуэт со светящимися отверстиями вместо лица. Спит на Уровне 0 первые минуты, потом слышит шумный бег издалека. Заметила — бежит почти как ты. Ходи шагом или приседай." },
                { "Комок (Clump)", "Висит на потолке: тело в мясных отростках и пасть. Приманка — длинная рука. Бросается, если подошёл близко или шумишь, потом уползает обратно наверх." },
                { "Окно (Window)", "Тёмная фигура в окне Уровня 4 стучит по стеклу. Не подходи к окнам ближе 2–3 метров — схватит и утащит." },
                { "Дуллер (Duller)", "Тонкий чёрный силуэт в стене с длинными руками. Тянется к тому, кто двигается рядом; присядь и стой — не заметит. В Пригороде (Ур. 9) Дуллеры ходят и бегут за тобой, но замирают, если на них смотреть." },
                { "Безликие (Faceless)", "Взрослые мирно бродят и просто смотрят. Дети (с ножами) нападают стаей — присядь на месте, и они успокоятся." },
                { "Горемыка (Wretch)", "Тощий человек без глаз, держится стаей возле своей территории. Заметили — бежит вся стая, уходит назад, если оторваться." },
                { "Тварь с Уровня 7 (Thing)", "Длинное морское существо с огромной пастью. Идёт по следу и реагирует на шумный бег. Затаись и не шуми." },
                { "Дозорный (Neighborhood Watch)", "Гигантский глаз на четырёх ногах с прожектором вместо зрачка. Патрулирует Пригород, заметив — шипит секунду и бежит чуть быстрее обычного бега. Прячься за стенами, теряет за 6 секунд." },
                { "Howler (событие)", "Время от времени где-то воет: свет гаснет, камера трясётся, а монстры бросаются на поиски. В темноте — слушай." },
            };
            for (int i = 0; i < m.GetLength(0); i++) { Ui.Label(m[i, 0], 16, Ui.Accent2, true); Ui.Para(m[i, 1], 13, Ui.Txt); Ui.Space(6); }
            Ui.Para("Поимка: экран падает, свет гаснет, тебя возвращают в начало уровня. Остальные продолжают. Выход уровня открывается, когда рядом с дверью весь отряд (или кто-то стоит у двери 20 секунд).", 13, Ui.Muted);
        }

        void TabInfo()
        {
            Ui.Header("Как это работает");
            Ui.Para("• Мод должен стоять у всех друзей (тот же файл PeakBackrooms.dll).\n• Уровни строятся из процедурных стен, текстур и света над горой. Монстры считаются у «хоста» (игрок с наименьшим номером), остальным приходят снимки по сети.\n• Лут: по карте стоят деревянные ящики (подсказка ▣ на экране рядом). Удерживай E — откроешь: внутри вода, палочка, радио-приманка, карта выходов, талисман — или пусто, или ящик завоет и позовёт монстров. Предметы — клавиши 5–9.\n• Выход (End) — для себя. Если все вышли — забег закончен.\n• Если вы застряли — End вернёт на гору.\n• Файл итога забега: BepInEx\\PeakBackrooms_run_last.txt, результаты проверки: BepInEx\\PeakBackrooms_check_latest.txt.\n• Мод можно держать вместе с PeakFun/PeakMods — у него свой сетевой канал.\n• Честно: мод написан без запуска игры — сообщите, если что-то работает иначе.", 14, Ui.Txt);
            Ui.Header("Клавиши"); Ui.Para(keyMenu.Value + " — меню · " + keyPanic.Value + " — выйти из Backrooms. Клавиши меняются в BepInEx\\config\\com.friends.peakbackrooms.cfg", 14, Ui.Txt);
            Ui.Para("Игра: " + (Game.Ready ? "распознана" : "НЕ распознана") + " · шейдеры: " + BrGfx.OpName + " · свет: " + BrGfx.LitName + " · " + Env.UrpInfo, 12, Ui.Muted);
        }

        // ================= проверка =================
        bool checking; string report = "Отчёт появится здесь."; 
        void TabTest()
        {
            Ui.Header("Проверка");
            Ui.Para("Генерация всех уровней (достижимость дверей), построение и удаление уровней, создание монстров и звуков. Запускай в игре вне забега. Результат также в BepInEx\\PeakBackrooms_check_latest.txt.", 13, Ui.Muted);
            if (Ui.Button(checking ? "Идёт проверка…" : "Запустить проверку", 1, !checking && !Play.On)) StartCoroutine(RunCheck());
            Ui.Para(report, 12, Ui.Txt);
        }

        System.Collections.IEnumerator RunCheck()
        {
            checking = true; var sb = new StringBuilder(); int bad = 0; var me = Game.Me(); sb.AppendLine("PeakBackrooms " + Version + " — проверка " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Игра распознана: " + Game.Ready + " · игрок: " + (me != null) + " · комната: " + Game.InRoom);
            report = "Идёт…"; yield return null;
            // 1. генерация
            foreach (var L in Levels.All)
                foreach (int sz in new[] { 0, 2, 3 })
                {
                    int n = Play.LevelN(L, sz); var sw = System.Diagnostics.Stopwatch.StartNew(); Grid g = null; string err = null; try { g = Grid.Make(L, n, 1234 + sz); } catch (Exception e) { err = e.Message; }
                    if (g == null) { bad++; sb.AppendLine("ОШИБКА генерации " + L.id + " n=" + n + ": " + err); continue; }
                    bool ok = g.exits.Count > 0; foreach (var ex in g.exits) if (g.dist[ex.x, ex.y] < 0) ok = false; int reach = 0, un = 0; for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) if (g.dist[x, y] >= 0) reach++; else un++;
                    if (L.style != Style.Snake && un > 0) ok = false; if (!ok) bad++;
                    sb.AppendLine((ok ? "OK  " : "FAIL") + " генерация " + L.id.PadRight(3) + " n=" + n.ToString().PadRight(3) + " достижимо " + reach + ", дверей " + g.exits.Count + ", " + sw.ElapsedMilliseconds + " мс");
                }
            report = sb.ToString(); yield return null;
            // 2. построение и удаление, монстры
            if (me != null)
            {
                Vector3 bp = Util.CenterOf(me) + Vector3.up * 700f;
                foreach (var L in Levels.All)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew(); string err = null; Built b = null; int ents = 0, lootN = 0;
                    try
                    {
                        int n = Play.LevelN(L, 1); var g = Grid.Make(L, n, 77); b = LevelBuilder.Build(g, L, bp, 77, L.dark); Ents.DiffV = 1f; Ents.Spawn(b, 77, 1f); ents = Ents.All.Count;
                        Loot.Build(b, 77); lootN = Loot.Count; Loot.Clear(); var pl = new List<PL>(); pl.Add(new PL { c = me, actor = Game.LocalActor(), pos = b.spawn }); for (int i = 0; i < 20; i++) { Ents.HostTick(0.05f, pl); Ents.Visual(0.05f, true); }
                    }
                    catch (Exception e) { err = e.GetType().Name + ": " + e.Message; }
                    yield return null;
                    string line = (err == null ? "OK  " : "FAIL") + " построение " + L.id.PadRight(3) + (b != null ? " сеток " + b.meshCount + ", коллайдеров " + b.colCount + ", ламп " + b.rig.LampPos.Count + ", ловушек " + b.hazards.Count + ", монстров " + ents + ", ящиков " + lootN : "") + ", " + sw.ElapsedMilliseconds + " мс" + (err != null ? " — " + err : "");
                    if (err != null) bad++; sb.AppendLine(line); try { Ents.Clear(); if (b != null) b.Destroy(); } catch { }
                    report = sb.ToString(); yield return null;
                }
            }
            else sb.AppendLine("Построение пропущено: нет персонажа (зайди в игру).");
            // 3. звуки и текстуры
            int snd = 0; foreach (var k in new[] { "hum", "buzz", "pipe", "water", "party", "siren", "dark", "growl", "howl", "hiss", "boom", "ding", "zap", "splash", "flap", "step", "sea", "cave", "wind", "rain", "city", "knock" }) { if (Snd2.Get(k) != null) snd++; else bad++; }
            sb.AppendLine("Звуков синтезировано: " + snd + "/22"); try { for (int i = 0; i < 16; i++) { Tex.Wall(i); Tex.Floor(i); if (i < 11) Tex.Ceil(i); } sb.AppendLine("Текстуры: OK (карт нормалей: " + BrGfx.NormalCount + ")"); } catch (Exception e) { bad++; sb.AppendLine("Текстуры: FAIL " + e.Message); }
            sb.AppendLine(); sb.AppendLine("Графика: " + BrGfx.Survey()); sb.AppendLine(Env.UrpInfo); sb.AppendLine("Хост сейчас: " + Play.HostActor() + ", я: " + Game.LocalActor());
            sb.AppendLine(); sb.AppendLine(bad == 0 ? "ИТОГ: всё в порядке." : "ИТОГ: проблем — " + bad);
            report = sb.ToString(); try { File.WriteAllText(Path.Combine(Paths.BepInExRootPath, "PeakBackrooms_check_latest.txt"), report); } catch { }
            Ui.Toast(bad == 0 ? "Проверка Backrooms: всё в порядке" : "Проверка Backrooms: проблем " + bad, bad == 0 ? Ui.Good : Ui.Bad); checking = false;
        }
    }
}
