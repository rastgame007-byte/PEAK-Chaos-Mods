// PeakJujutsu — «Магическая битва» для PEAK: персонажи, техники, проклятия, боссы, домены, прокачка. Без сети, только у тебя.
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakJJK
{
    [BepInPlugin("com.friends.peakjujutsu", "PeakJujutsu", Version)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string Version = "1.0.1";
        public static Plugin I;
        static ConfigEntry<bool> eShake, eHitStop, eFlash, eSound; static ConfigEntry<float> eVolume, eDamage, ePower, eEnemyHp, eXp, eCooldown;
        public static bool CfgShake { get { return eShake == null || eShake.Value; } }
        public static bool CfgHitStop { get { return eHitStop == null || eHitStop.Value; } }
        public static bool CfgFlash { get { return eFlash == null || eFlash.Value; } }
        public static bool CfgSound { get { return eSound == null || eSound.Value; } }
        public static float CfgVolume { get { return eVolume == null ? 0.8f : eVolume.Value; } }
        public static float CfgDamage { get { return eDamage == null ? 1f : eDamage.Value; } }
        public static float CfgPower { get { return ePower == null ? 1f : ePower.Value; } }
        public static float CfgEnemyHp { get { return eEnemyHp == null ? 1f : eEnemyHp.Value; } }
        public static float CfgXp { get { return eXp == null ? 1f : eXp.Value; } }
        public static float CfgCooldown { get { return eCooldown == null ? 1f : eCooldown.Value; } }
        public static ConfigEntry<bool> CfgUnlockAll, CfgInfiniteCE, CfgHud; public static ConfigEntry<float> CfgHudScale, CfgUiScale; public static ConfigEntry<string> CfgChar;
        public static ConfigEntry<int> CfgLevel, CfgKills, CfgBestWave, CfgMaxAlive, CfgTheme; public static ConfigEntry<float> CfgXpSaved; static ConfigEntry<KeyCode> keyMenu;
        public static bool MenuOpen; public static bool TypingNow { get { return false; } }
        static readonly Dictionary<string, int> logged = new Dictionary<string, int>();

        public static void Log(string where, Exception e)
        {
            int n; logged.TryGetValue(where, out n); if (n >= 3) return; logged[where] = n + 1; if (I != null) I.Logger.LogWarning(where + ": " + e.GetType().Name + " " + e.Message + (n == 0 ? "\n" + e.StackTrace : ""));
        }

        void Awake()
        {
            I = this;
            keyMenu = Config.Bind("Keys", "Menu", KeyCode.F4, "Открыть/закрыть меню PeakJujutsu");
            eShake = Config.Bind("Effects", "CameraShake", true, "Тряска камеры");
            eHitStop = Config.Bind("Effects", "HitStop", true, "Короткая заморозка времени при мощных ударах");
            eFlash = Config.Bind("Effects", "ScreenFlash", true, "Вспышки на весь экран (отключи при светочувствительности)");
            eSound = Config.Bind("Effects", "Sound", true, "Звуки техник и ударов");
            eVolume = Config.Bind("Effects", "Volume", 0.8f, "Громкость звуков мода (0–1.5)");
            CfgHud = Config.Bind("UI", "Hud", true, "Показывать игровой интерфейс (слоты техник, ЦЭ, полоски врагов)");
            CfgHudScale = Config.Bind("UI", "HudScale", 1f, "Масштаб игрового интерфейса");
            CfgUiScale = Config.Bind("UI", "MenuScale", 1f, "Масштаб меню");
            CfgTheme = Config.Bind("UI", "Theme", 0, "Тема меню (0–3)");
            eDamage = Config.Bind("Balance", "IncomingDamage", 1f, "Множитель урона, который проклятия наносят тебе");
            ePower = Config.Bind("Balance", "PlayerPower", 1f, "Множитель твоего урона");
            eEnemyHp = Config.Bind("Balance", "EnemyHealth", 1f, "Множитель здоровья врагов");
            eXp = Config.Bind("Balance", "XpGain", 1f, "Множитель опыта");
            eCooldown = Config.Bind("Balance", "CooldownScale", 1f, "Множитель перезарядки техник (0.5 — вдвое быстрее)");
            CfgMaxAlive = Config.Bind("Balance", "MaxEnemiesAlive", 12, "Сколько врагов одновременно в «Охоте» (меньше — быстрее FPS)");
            CfgUnlockAll = Config.Bind("Progress", "UnlockAllCharacters", true, "Все персонажи доступны сразу (иначе открываются по уровню)");
            CfgInfiniteCE = Config.Bind("Progress", "InfiniteCursedEnergy", false, "Бесконечная проклятая энергия и ульт");
            CfgChar = Config.Bind("Progress", "Character", "yuji", "Текущий персонаж (служебное)");
            CfgLevel = Config.Bind("Progress", "Level", 1, "Уровень (служебное)");
            CfgXpSaved = Config.Bind("Progress", "Xp", 0f, "Опыт на уровне (служебное)");
            CfgKills = Config.Bind("Progress", "Kills", 0, "Убито проклятий (служебное)");
            CfgBestWave = Config.Bind("Progress", "BestWave", 0, "Рекорд волны (служебное)");
            Prog.Load(); Skills.SelectById(CfgChar.Value); Themes.Apply(CfgTheme.Value); Ui.Dark = true;
            string ib; try { ib = InputBlock.Install(); } catch (Exception e) { ib = "Harmony недоступен: " + e.Message; }
            Logger.LogInfo("PeakJujutsu " + Version + " загружен. Меню: " + keyMenu.Value + ". Игра распознана: " + Game.Ready + ". Ввод: " + ib);
        }

        float openT, nonePlayerT; bool wasAlive;
        void Update()
        {
            try
            {
                if (Keys.Down(keyMenu.Value)) ToggleMenu();
                InputBlock.Want = MenuOpen; if (MenuOpen) InputBlock.Apply();
                openT = Mathf.MoveTowards(openT, MenuOpen ? 1f : 0f, Time.unscaledDeltaTime * 5f);
                if (MenuOpen && Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                Fx.HookCamera(); Fx.Tick();
                float dt = Mathf.Min(Time.deltaTime, 0.1f);
                var me = Game.Me();
                if (me == null) { nonePlayerT += Time.unscaledDeltaTime; if (nonePlayerT > 3f && (Hunt.Mode != 0 || World.E.Count > 0)) { Hunt.Stop(); World.ClearAll(); Allies.Clear(); Domains.Clear(); Skills.ClearBuffs(); } return; }
                nonePlayerT = 0f;
                bool alive = !Game.IsDead(me); if (wasAlive && !alive) { Skills.ClearBuffs(); Domains.Clear(); Allies.Clear(); Pl.Reset(); } wasAlive = alive;
                Pl.Tick(); Pl.StunTick(); Combo.Tick(); Skills.Tick(dt); Hunt.Tick(); World.Tick(); Allies.Tick(dt); Domains.Tick();
            }
            catch (Exception e) { Log("Update", e); }
        }
        void ToggleMenu() { MenuOpen = !MenuOpen; if (!MenuOpen) { Prog.Save(); } }

        void OnDestroy() { try { Fx.ResetTime(); Pl.Reset(); } catch { } }

        void OnGUI()
        {
            try
            {
                var e = Event.current;
                if (e.type == EventType.KeyDown && e.keyCode == keyMenu.Value && Keys.Fire(keyMenu.Value)) ToggleMenu();
                if (MenuOpen && e.type == EventType.Layout) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
                Ui.Init(); Ui.Dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f); Ui.Time_ = Time.unscaledTime; Ui.Alpha = 1f;
                Fx.DrawPopups(); Hud.Draw(); Fx.DrawOverlay(); Domains.DrawOverlay();
                float s = Mathf.Clamp(Screen.height / 1080f * CfgUiScale.Value, 0.7f, 3f), vw = Screen.width / s, vh = Screen.height / s;
                var baseMatrix = Matrix4x4.Scale(new Vector3(s, s, 1f)); GUI.matrix = baseMatrix;
                Ui.DrawToasts(vw);
                if (openT > 0.002f) DrawWindow(vw, vh, baseMatrix);
                GUI.matrix = Matrix4x4.identity; Ui.Alpha = 1f;
            }
            catch (ArgumentException) { throw; }
            catch (Exception ex) { Log("OnGUI", ex); }
        }
    }
}
