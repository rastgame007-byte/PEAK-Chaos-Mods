// PeakFun 1.3 — весёлый мод для PEAK (BepInEx 5) для игры с друзьями.
// F5 — меню, F6 — полёт, F7 — хаос, F8 — «стоп всё» (убрать пугалки с экрана), F9 — возродиться. Клавиши меняются в BepInEx/config/com.friends.peakfun.cfg
using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakFun
{
    #if LITE
    [BepInPlugin(Guid, "PeakHeroes", Version)]
#elif DEV
    [BepInPlugin(Guid, "PeakFunDEV", Version)]
#else
    [BepInPlugin(Guid, "PeakFun", Version)]
#endif
    public partial class PeakFunPlugin : BaseUnityPlugin
    {
        #if LITE
        public const string Guid = "com.friends.peakheroes";
#elif DEV
        public const string Guid = "com.friends.peakfun.dev";
#else
        public const string Guid = "com.friends.peakfun";
#endif
        public const string Version = "1.32.0";

        ConfigEntry<KeyCode> keyMenu, keyFly, keyChaos, keyPanic, keyRespawn, keyHero, keyThird, keyPuppet, keyAb1, keyAb2, keyGrab, keyThrow;
        ConfigEntry<bool> cfgUnlockSkins, cfgAccept, cfgFreeze, cfgScreamSound, cfgAllowQuit, cfgQuakePhysics;
        ConfigEntry<float> cfgScreamVol, cfgUiScale;

        // окно
        bool showMenu;
        float openT;                       // 0..1 — анимация открытия
        Vector2 winPos = new Vector2(-1, -1);
        const float WinW = 980f, WinH = 664f;
        int tab = 0;
        float lastErr;

        Component lastChar;
        string inputBlockInfo = "";

        /// <summary>Если рядом стоит ещё одна копия мода (обычный PeakFun вместе с DEV), две копии строят уровни поверх друг друга — «уровни перемешиваются».
        /// Обычная сборка в этом случае отключает себя целиком, работает только DEV.</summary>
        void Start()
        {
            try
            {
                var infos = BepInEx.Bootstrap.Chainloader.PluginInfos; var names = new System.Text.StringBuilder(); foreach (var k in infos.Keys) if (k.StartsWith("com.friends.")) names.Append(k).Append(' ');
                Logger.LogInfo("Моды семейства PeakFun в игре: " + names);
#if !DEV && !LITE
                if (infos.ContainsKey("com.friends.peakfun.dev"))
                {
                    enabled = false; PeakFun.Net.OnCommand = null;
                    try { PeakBR.Play.Dormant = true; PeakBR.Net.OnCommand = null; if (PeakBR.Plugin.Inst != null) PeakBR.Plugin.Inst.enabled = false; } catch { }
                    Logger.LogWarning("Найден PeakFunDEV — обычный PeakFun ОТКЛЮЧЁН (две копии строят уровни друг поверх друга). Удали или переименуй один из файлов в .off.");
                }
#endif
#if DEV
                if (infos.ContainsKey("com.friends.peakfun")) Logger.LogWarning("Рядом стоит обычный PeakFun — он отключён автоматически. Работает только DEV.");
#endif
            }
            catch (Exception e) { Logger.LogWarning("проверка копий мода: " + e.Message); }
        }

        void Awake()
        {
            Instance = this;
            InitTheme(); InitHoops(); InitTest(); InitBackrooms(); InitBrHub(); InitBrMenu();
            keyMenu = Config.Bind("Keys", "Menu", KeyCode.F5, "Открыть/закрыть меню");
            keyHero = Config.Bind("Keys", "Hero", KeyCode.F10, "Режим героя вкл/выкл");
            keyThird = Config.Bind("Keys", "ThirdPerson", KeyCode.F11, "Камера от третьего лица вкл/выкл");
            keyPuppet = Config.Bind("Keys", "Puppet", KeyCode.F4, "Управлять персонажем друга (на кого смотришь) / отпустить");
            keyAb1 = Config.Bind("Keys", "HeroAbilityA", KeyCode.Z, "Способность героя A (лазер/удар/рывок…)");
            keyAb2 = Config.Bind("Keys", "HeroAbilityB", KeyCode.V, "Способность героя B (волна/пике/молния…)");
            keyGrab = Config.Bind("Keys", "HeroGrab", KeyCode.X, "Схватить друга за горло / отпустить");
            keyThrow = Config.Bind("Keys", "HeroThrow", KeyCode.B, "Бросить схваченного");
            keyDodge = Config.Bind("Keys", "Dodge", KeyCode.Q, "Уклон-рывок героя");
            keyGuard = Config.Bind("Keys", "Guard", KeyCode.C, "Защитная стойка героя (держать)");
            keyAb3 = Config.Bind("Keys", "HeroAbilityC", KeyCode.H, "Третья техника героя (Луч Альбус / Огненная стрела Морока)");
            keyObserve = Config.Bind("Keys", "Observer", KeyCode.F2, "Камера-наблюдатель: спереди / глазами бота / обычная");
            keyTest = Config.Bind("Keys", "SoloTest", KeyCode.F3, "Тест в одиночку: бот-жертва и эхо «глазами друга»");
            keyDomain = Config.Bind("Keys", "Domain", KeyCode.G, "Великий купол (Альбус / Владыка Морок)");
            keyFly = Config.Bind("Keys", "Fly", KeyCode.F6, "Включить/выключить полёт");
            keyChaos = Config.Bind("Keys", "Chaos", KeyCode.F7, "Включить/выключить режим хаоса");
            keyPanic = Config.Bind("Keys", "Panic", KeyCode.F8, "Стоп: убрать все пугалки/оверлеи с экрана");
            keyRespawn = Config.Bind("Keys", "Respawn", KeyCode.F9, "Возродить себя (если ты мёртв)");
            cfgAccept = Config.Bind("Pranks", "AcceptFromFriends", true, "Принимать пранки/эффекты от друзей с PeakFun");
            cfgUnlockSkins = Config.Bind("Cosmetics", "UnlockAllSkins", false, "Открыть все скины/косметику. Обратимо: false — всё закрыто как в обычной игре. В сохранения ничего не пишется");
            cfgAllowQuit = Config.Bind("Pranks", "AllowFriendsToCloseMyGame", false, "Разрешить друзьям с PeakFun НАСТОЯЩЕ закрывать мне игру («жёсткий» пранк «вылет»). По умолчанию выключено — тогда у тебя будет только фейковый крэш");
            cfgQuakePhysics = Config.Bind("Pranks", "QuakeShakesBody", true, "Землетрясение тоже слегка трясёт тело персонажа (не только камеру)");
            cfgFreeze = Config.Bind("UI", "FreezeControlsWhenMenuOpen", true, "Не двигать персонажа, пока открыто меню");
            cfgScreamSound = Config.Bind("Pranks", "ScreamerSound", true, "Звук скримера");
            cfgScreamVol = Config.Bind("Pranks", "ScreamerVolume", 0.6f, "Громкость скримера 0..1");
            cfgUiScale = Config.Bind("UI", "Scale", 1f, "Масштаб интерфейса меню");
            Fx.ScreamSound = cfgScreamSound.Value;
            Fx.ScreamVolume = cfgScreamVol.Value;

            baseGravity = Physics.gravity;
            Lag.Host = this; Lag.OnRubber = StartRubber;
            string ib = "?"; try { ib = InputBlock.Install(); } catch (Exception e) { ib = "Harmony недоступен: " + e.Message; }
            inputBlockInfo = ib;
            Logger.LogInfo("Блокировка ввода при открытом меню: " + ib);
            Net.OnCommand = OnNetCommand;
            SkinUnlock.Set(cfgUnlockSkins.Value);
            Logger.LogInfo("PeakFun " + Version + " загружен. Меню: " + keyMenu.Value + ". Игра распознана: " + Game.Ready);
        }

        void SafeLog(string where, Exception e)
        {
            if (Time.realtimeSinceStartup - lastErr < 5f) return;
            lastErr = Time.realtimeSinceStartup;
            Logger.LogWarning(where + ": " + (e.InnerException ?? e));
        }

        void Safe(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { SafeLog(what, e); Ui.Toast("Не вышло: " + what, Ui.Bad); }
        }

        // ================= циклы =================
        void ToggleMenu()
        {
            showMenu = !showMenu;
            if (showMenu && tab == TItems && (itemNames.Count == 0 || Time.realtimeSinceStartup - itemsLoadedAt > 40f)) LoadItems();
        }

        void Update()
        {
            try { BrMenuTick(); } catch (Exception bme) { BrWarn("brmenu", bme); }
            try
            {
                if (Keys.Down(keyMenu.Value)) ToggleMenu();
#if !LITE
                if (Keys.Down(keyFly.Value)) SetFly(!flying);
                if (Keys.Down(keyChaos.Value)) SetChaos(!chaos);
                if (Keys.Down(keyRespawn.Value)) RespawnMe();
#endif
                if (Keys.Down(keyPanic.Value)) Panic();
                if (Keys.Down(keyTest.Value)) SetSoloTest(!soloTest);

                InputBlock.Want = (showMenu && cfgFreeze.Value) || hideBlind || brLock;
                if (InputBlock.Want) InputBlock.Apply();
                Lag.Tick();
                TickInvisible();
                TickCompanion();
                TickHero();
                TickView();
                Vfx.Tick(); Gore.Tick(); TickRemoteBeams(); TickGames(); TickFun(); TickAnime(); TickCine(); TickHoops();

                openT = Mathf.MoveTowards(openT, showMenu ? 1f : 0f, Time.unscaledDeltaTime * 5f);
                Net.Tick();

                var me = Game.Me();
                if (me == null) { lastChar = null; return; }

                if (me != lastChar)
                {
                    lastChar = me;
                    baseForce = baseJump = -1f;
                    flying = false; flyVel = Vector3.zero; HeroReset(); PuppetReset();
                    climbComps.Clear(); baseClimb.Clear(); climbWasOn = false;
                    inputComp = null; inputOff = false;
                    curSize = 1f; sizeSent = -1f;
                    if (sizeTarget != 1f) curSize = sizeTarget;
                    LoadItemsIfNeeded();
                }

                ApplyMovement(me);
                ApplyClimb(me);
                TickBuffs(me);
                TickSize(me);
                TickProximity(me);
                TickBurn(me);
                TickRespawn(me);
                TickRubber(me);

                if (lowGravity) Physics.gravity = baseGravity * gravityScale;
                else if (Physics.gravity != baseGravity) Physics.gravity = baseGravity;

                if (chaos && Time.time >= nextChaos) { RunChaosEvent(); ScheduleChaos(); }
            }
            catch (Exception e) { SafeLog("Update", e); }
        }

        void LateUpdate()
        {
            try
            {
                var me = Game.Me();
                if (me != null) ApplyClimb(me);
                Shade.Tick();
                try { TickCostumes(); } catch (Exception e) { SafeLog("Costume", e); }
                if (showMenu) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                if (InputBlock.Want) InputBlock.Apply();
                FreezeControls(me);
            }
            catch (Exception e) { SafeLog("LateUpdate", e); }
        }

        float nextQuake;
        void TickQuake()
        {
            float lv = Hard.QuakeLevel;
            if (lv <= 0.01f || !cfgQuakePhysics.Value || Time.time < nextQuake) return;
            var me = Game.Me(); if (me == null) return;
            nextQuake = Time.time + UnityEngine.Random.Range(0.06f, 0.16f);
            Vector3 imp = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-0.3f, 0.5f), UnityEngine.Random.Range(-1f, 1f)) * (0.55f * lv);
            foreach (var rb in Game.Rigs(me)) if (rb != null && !rb.isKinematic) rb.AddForce(imp, ForceMode.VelocityChange);
        }

        void FixedUpdate()
        {
            try { TickQuake(); } catch (Exception e) { SafeLog("Quake", e); }
            try { HeroFixed(); } catch (Exception e) { SafeLog("HeroFixed", e); }
            if (!flying) return;
            try
            {
                var me = Game.Me();
                if (me == null || Camera.main == null) return;
                var cam = Camera.main.transform;
                Vector3 dir = Vector3.zero;
                bool typing = showMenu && cfgFreeze.Value;
                if (!typing)
                {
                    if (Keys.Held(KeyCode.W)) dir += cam.forward;
                    if (Keys.Held(KeyCode.S)) dir -= cam.forward;
                    if (Keys.Held(KeyCode.D)) dir += cam.right;
                    if (Keys.Held(KeyCode.A)) dir -= cam.right;
                    if (Keys.Held(KeyCode.Space)) dir += Vector3.up;
                    if (Keys.Held(KeyCode.LeftControl)) dir -= Vector3.up;
                }
                float speed = flySpeed * (Keys.Held(KeyCode.LeftShift) ? flyBoost : 1f);
                Vector3 target = dir.sqrMagnitude > 0.01f ? dir.normalized * speed : Vector3.zero;
                if (Time.time >= dashUntil) flyVel = Vector3.Lerp(flyVel, target, Time.fixedDeltaTime * flyAccel);
                foreach (var rb in Game.Rigs(me)) Game.SetVelocity(rb, flyVel);
                HeroPose(me, flyVel);
            }
            catch (Exception e) { SafeLog("Fly", e); }
        }

        void OnDestroy()
        {
            Physics.gravity = baseGravity;
            Fx.Clear();
            RestoreAllInv();
        }

        void Panic()
        {
            Fx.Clear(); HeroPanic(); StopPuppet(false); StopGame(false); FunPanic(); BackroomsPanic(); BrHubPanic();
            Ui.Toast("Все пугалки на экране убраны", Ui.Good);
        }

        // ================= управление персонажем при открытом меню =================
        Component inputComp; bool inputOff;

        void FreezeControls(Component me)
        {
            bool want = ((showMenu && cfgFreeze.Value) || brLock) && me != null;
            if (want)
            {
                if (inputComp == null) inputComp = R.Get(me, "input") as Component ?? R.Get(R.Get(me, "refs"), "input") as Component;
                if (inputComp == null) return;
                var b = inputComp as Behaviour;
                if (b != null && b.enabled) { b.enabled = false; inputOff = true; }
                R.Set(inputComp, "movementInput", Vector2.zero);
                R.Set(inputComp, "lookInput", Vector2.zero);
                R.Set(inputComp, "jumpIsPressed", false);
                R.Set(inputComp, "sprintIsPressed", false);
                R.Set(inputComp, "useIsPressed", false);
            }
            else if (inputOff)
            {
                var b = inputComp as Behaviour;
                if (b != null) b.enabled = true;
                inputOff = false;
            }
        }

        // ================= GUI =================
        void OnGUI()
        {
            try
            {
                var e = Event.current;
                if (e.type == EventType.KeyDown)
                {
                    if (e.keyCode == keyMenu.Value && Keys.Fire(keyMenu.Value)) ToggleMenu();
#if !LITE
                    else if (e.keyCode == keyFly.Value && Keys.Fire(keyFly.Value)) SetFly(!flying);
                    else if (e.keyCode == keyChaos.Value && Keys.Fire(keyChaos.Value)) SetChaos(!chaos);
                    else if (e.keyCode == keyRespawn.Value && Keys.Fire(keyRespawn.Value)) RespawnMe();
#endif
                    else if (e.keyCode == keyPanic.Value && Keys.Fire(keyPanic.Value)) Panic();
                    else if (e.keyCode == keyTest.Value && Keys.Fire(keyTest.Value)) SetSoloTest(!soloTest);
                }

                if (showMenu && e.type == EventType.Layout) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                Ui.Init();
                Ui.Dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                Ui.Time_ = Time.unscaledTime;
                Ui.Alpha = 1f;

                float s = Mathf.Clamp(Screen.height / 1080f * cfgUiScale.Value, 0.7f, 3f);
                float vw = Screen.width / s, vh = Screen.height / s;

                DrawStartScreen();
                if (markers && e.type == EventType.Repaint) DrawMarkers();
                Shade.Draw(new Rect(0, 0, Screen.width, Screen.height), Screen.height / 1080f);
                DrawNpcMarker();
                DrawHeroHud(); DrawHoopHud();
                DrawDomainBanner();
                DrawCineBars();
                DrawImpactFrame();
                DrawPuppetHud();
                DrawGamesHud(); DrawFunHud();
                DrawCombatHud();

                var baseMatrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
                GUI.matrix = baseMatrix;
                Ui.DrawToasts(vw);
                if (openT > 0.002f) DrawWindow(vw, vh, baseMatrix);
                GUI.matrix = Matrix4x4.identity;
                Ui.Alpha = 1f;

                Fx.ScreamSound = cfgScreamSound.Value;
                Fx.ScreamVolume = cfgScreamVol.Value;
                Fx.Draw();
            }
            catch (ArgumentException) { throw; }   // рассинхрон Layout/Repaint — пусть Unity разберётся
            catch (Exception ex) { SafeLog("OnGUI", ex); }
        }

        void DrawMarkers()
        {
            var cam = Camera.main; var me = Game.Me();
            if (cam == null || me == null) return;
            foreach (var c in Game.Others())
            {
                Vector3 sp = cam.WorldToScreenPoint(c.transform.position + Vector3.up * 1.2f);
                if (sp.z <= 0f) continue;
                float dist = Vector3.Distance(me.transform.position, c.transform.position);
                string label = Game.Name(c) + "  " + Mathf.RoundToInt(dist) + " м" + (Game.IsDead(c) ? "  (мёртв)" : "");
                var r = new Rect(sp.x - 110, Screen.height - sp.y - 14, 220, 28);
                Ui.Box(new Rect(r.x + 40, r.y + 2, r.width - 80, r.height - 4), new Color(0, 0, 0, 0.45f), 8);
                Ui.Text(r, label, 14, Color.white, TextAnchor.MiddleCenter, true);
            }
        }

        const int THome = 0, TEffects = 1, TMove = 2, TItems = 3, TLook = 4, TFriends = 5, TPranks = 6, THard = 7, TMonsters = 8, TBot = 9, TShade = 10, TChaos = 11, TInfo = 12, THero = 13, TPuppet = 14, TGames = 15, TTest = 16, TBack = 17, TFun = 18, TBack2 = 19;
        // раздел меню: (подпись группы или null, id вкладки, название)
#if LITE
        static readonly object[][] Menu = { new object[] { "ГЕРОИ", THero, "Герои" } };
#else
        static readonly object[][] Menu =
        {
            new object[] { "ГЛАВНОЕ", THome, "Главная" },
            new object[] { "Я САМ", TEffects, "Эффекты" }, new object[] { null, TMove, "Движение" }, new object[] { null, TItems, "Предметы" }, new object[] { null, TLook, "Облик" }, new object[] { null, THero, "Герои" },
            new object[] { "ДРУЗЬЯ", TFriends, "Друзья" }, new object[] { null, TPranks, "Пранки" }, new object[] { null, THard, "Жёсткие" }, new object[] { null, TMonsters, "Монстры" }, new object[] { null, TBot, "Бот-спутник" }, new object[] { null, TPuppet, "Кукловод" }, new object[] { null, TGames, "Мини-игры" }, new object[] { null, TFun, "Развлечения" }, new object[] { null, TBack, "Backrooms: приманки" }, new object[] { null, TBack2, "Backrooms ×16" },
            new object[] { "ВИЗУАЛ", TShade, "Шейдеры" },
            new object[] { "ПРОЧЕЕ", TChaos, "Хаос" }, new object[] { null, TTest, "Автотест" }, new object[] { null, TInfo, "Инфо" },
        };
#endif
#if LITE
        const string HeaderTitle = "ГЕРОИ";
#else
        const string HeaderTitle = "PEAK FUN";
#endif
        float tabIndY;

        void DrawWindow(float vw, float vh, Matrix4x4 baseMatrix)
        {
            var e = Event.current;
            if (winPos.x < 0) winPos = new Vector2((vw - WinW) / 2f, (vh - WinH) / 2f);
            winPos.x = Mathf.Clamp(winPos.x, -WinW + 120, vw - 120);
            winPos.y = Mathf.Clamp(winPos.y, 0, vh - 40);

            float eo = Ui.EaseOut(openT);
            float back = Ui.EaseOutBack(openT);
            Ui.Alpha = eo;
            var rect = new Rect(winPos.x, winPos.y, WinW, WinH);
            var pivot = rect.center;
            if (openT < 0.999f)
                GUI.matrix = baseMatrix * Matrix4x4.Translate(new Vector3(pivot.x, pivot.y + (1f - eo) * 24f, 0)) * Matrix4x4.Scale(new Vector3(0.88f + 0.12f * back, 0.88f + 0.12f * back, 1f)) * Matrix4x4.Translate(new Vector3(-pivot.x, -pivot.y, 0));

            // тень и фон
            Ui.Box(new Rect(rect.x - 10, rect.y - 6, rect.width + 20, rect.height + 24), new Color(0, 0, 0, 0.3f), 26);
            Ui.Box(rect, Ui.Bg, 18); Ui.Border(rect, new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.38f), 1f);

            // перетаскивание за шапку
            var header = new Rect(rect.x, rect.y, rect.width - 60, 64);
            int dragId = GUIUtility.GetControlID(FocusType.Passive);
            switch (e.GetTypeForControl(dragId))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && header.Contains(e.mousePosition)) { GUIUtility.hotControl = dragId; dragOff = e.mousePosition - winPos; e.Use(); }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == dragId) { winPos = e.mousePosition - dragOff; e.Use(); }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == dragId) { GUIUtility.hotControl = 0; e.Use(); }
                    break;
            }

            GUI.BeginGroup(rect);
            float t = Ui.Time_;
            // дрейфующие светящиеся шары
            Ui.Orb(new Vector2(120 + Mathf.Sin(t * 0.5f) * 80, 90 + Mathf.Cos(t * 0.4f) * 40), 420, Ui.Orb1);
            Ui.Orb(new Vector2(WinW - 140 + Mathf.Cos(t * 0.35f) * 90, 140 + Mathf.Sin(t * 0.45f) * 60), 460, Ui.Orb2);
            Ui.Orb(new Vector2(WinW * 0.5f + Mathf.Sin(t * 0.3f) * 160, WinH - 40), 520, Ui.Orb3);

            // шапка
            Ui.Scan(new Rect(0, 0, WinW, WinH), 0.07f); Ui.Brackets(new Rect(3, 3, WinW - 6, WinH - 6), new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.85f), 20f, 2f);
            Ui.Gradient(new Rect(0, 0, WinW, 4), 1f, t * 0.12f);
            Ui.Text(new Rect(26, 12, 220, 40), HeaderTitle, 30, Ui.Txt, TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(190, 14, 300, 20), "v" + Version, 13, Ui.Muted, TextAnchor.MiddleLeft);
            Ui.Text(new Rect(190, 34, 300, 20), keyMenu.Value + " — закрыть меню", 12, Ui.Muted, TextAnchor.MiddleLeft);
            string st = Game.Me() != null ? (Game.InRoom ? (Game.IsMaster() ? "лобби · ты хост" : "лобби · ты гость") : "ты один") : "персонаж не найден";
            Ui.Text(new Rect(WinW - 520, 22, 250, 26), st, 13, Game.Me() != null ? Ui.Good : Ui.Bad, TextAnchor.MiddleRight, true);
            if (Ui.ButtonRect(new Rect(WinW - 262, 14, 150, 38), "СТОП  [" + keyPanic.Value + "]", 1, true, 15)) Panic();
            if (Ui.ButtonRect(new Rect(WinW - 100, 14, 38, 38), "X", 2, true, 16)) showMenu = false;

            // боковая панель с группами
            var side = new Rect(12, 72, 196, WinH - 84);
            Ui.Box(side, Ui.Side, 14);
            float yy = side.y + 8, selY = side.y + 8;
            var rowY = new float[Menu.Length];
            float hdrSum = 0f; for (int i = 0; i < Menu.Length; i++) if (Menu[i][0] != null) hdrSum += (i == 0 ? 0f : 8f) + 20f;
            float pitch = Mathf.Clamp((side.height - 16f - hdrSum) / Menu.Length, 23f, 35f), rowH = pitch - 3f;
            for (int i = 0; i < Menu.Length; i++)
            {
                if (Menu[i][0] != null) yy += (i == 0 ? 0f : 8f) + 20f;
                rowY[i] = yy;
                if ((int)Menu[i][1] == tab) selY = yy;
                yy += pitch;
            }
            if (tabIndY < 1f) tabIndY = selY;
            tabIndY = Mathf.Lerp(tabIndY, selY, 1f - Mathf.Exp(-14f * Ui.Dt));
            if (Event.current.type == EventType.Repaint)
            {
                Ui.Box(new Rect(side.x + 6, tabIndY, side.width - 12, rowH), new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.17f), 10); Ui.Border(new Rect(side.x + 6, tabIndY, side.width - 12, rowH), new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.4f), 1f);
                Ui.Box(new Rect(side.x + 6, tabIndY + rowH * 0.22f, 4, rowH * 0.56f), Ui.Accent, 2);
            }
            for (int i = 0; i < Menu.Length; i++)
            {
                if (Menu[i][0] != null)
                    Ui.Text(new Rect(side.x + 16, rowY[i] - 20, side.width - 24, 18), (string)Menu[i][0], 11, new Color(Ui.Muted.r, Ui.Muted.g, Ui.Muted.b, 0.75f), TextAnchor.MiddleLeft, true);
                int id = (int)Menu[i][1];
                var tr = new Rect(side.x + 6, rowY[i], side.width - 12, rowH);
                if (TabButton(tr, (string)Menu[i][2], id == tab)) { tab = id; scroll = Vector2.zero; if (id == TItems && (itemNames.Count == 0 || Time.realtimeSinceStartup - itemsLoadedAt > 40f)) LoadItems(); }
            }

            // содержимое
            var area = new Rect(220, 72, WinW - 232, WinH - 84);
            Ui.Box(area, new Color(1, 1, 1, 0.025f), 14);
            float cw = area.width - 26;
            float ch = Mathf.Max(contentH, area.height);
            scroll = GUI.BeginScrollView(new Rect(area.x, area.y + 4, area.width, area.height - 8), scroll, new Rect(0, 0, area.width - 16, ch), false, false, GUIStyle.none, GUIStyle.none);
            Ui.Begin(12, 6, cw);
            try
            {
#if LITE
                tab = THero;
#endif
                switch (tab)
                {
                    case THome: TabHome(); break;
                    case TEffects: TabEffects(); break;
                    case TMove: TabMove(); break;
                    case TItems: TabItems(); break;
                    case TLook: TabLook(); break;
                    case TFriends: TabFriends(); break;
                    case TPranks: TabPranks(); break;
                    case THard: TabHard(); break;
                    case TMonsters: TabMonsters(); break;
                    case TBot: TabBot(); break;
                    case THero: TabHero(); break;
                    case TPuppet: TabPuppet(); break;
                    case TGames: TabGames(); break;
                    case TFun: TabFun(); break;
                    case TShade: TabShade(); break;
                    case TChaos: TabChaos(); break;
                    case TTest: TabTest(); break;
                    case TBack: TabBack(); break;
                    case TBack2: TabBack2(); break;
                    default: TabInfo(); break;
                }
            }
            catch (ArgumentException) { throw; }
            catch (Exception ex) { SafeLog("Tab " + tab, ex); }
            if (e.type == EventType.Repaint || e.type == EventType.Layout) contentH = Ui.Y + 16;
            GUI.EndScrollView();

            // тонкий скроллбар
            if (contentH > area.height && Event.current.type == EventType.Repaint)
            {
                float vis = (area.height - 8) / contentH;
                float barH = Mathf.Max(30, (area.height - 16) * vis);
                float barY = area.y + 8 + (area.height - 16 - barH) * (scroll.y / Mathf.Max(1f, contentH - (area.height - 8)));
                Ui.Box(new Rect(area.xMax - 8, barY, 4, barH), new Color(1, 1, 1, 0.25f), 2);
            }
            GUI.EndGroup();

            GUI.matrix = baseMatrix;
            Ui.Alpha = 1f;
        }

        Vector2 dragOff, scroll;
        float contentH = 600f;

        bool TabButton(Rect r, string label, bool selected)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current; bool clicked = false;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && r.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); if (r.Contains(e.mousePosition)) clicked = true; }
                    break;
                case EventType.Repaint:
                    float h = Ui.Anim("tab" + label, r.Contains(e.mousePosition) ? 1f : 0f, 14f);
                    if (!selected && h > 0.01f) Ui.Box(r, new Color(1, 1, 1, 0.06f * h), 10);
                    Ui.Text(new Rect(r.x + 20 + (selected ? 4 : h * 3), r.y, r.width - 24, r.height), label, 15, selected ? Color.white : Color.Lerp(Ui.Muted, Ui.Txt, h), TextAnchor.MiddleLeft, selected);
                    break;
            }
            return clicked;
        }
    }
}
