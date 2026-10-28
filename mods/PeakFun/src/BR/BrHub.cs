// Мост: вкладка «Backrooms ×16» в меню PeakFun (сам мод — папка BR, namespace PeakBR).
using System;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        int brSub;

        void InitBrHub()
        {
            PeakBR.Plugin.OnStartHook = () => { showMenu = false; };
        }

        void TabBack2()
        {
            Ui.Header("Backrooms ×16 — все уровни, весь отряд");
            if (PeakBR.Plugin.Inst == null) { Ui.Para("Модуль Backrooms не запустился (см. LogOutput.log). Проверь, что рядом нет отдельного PeakBackrooms.dll — удали его.", 13, Ui.Bad); return; }
            Ui.Para("Вход всем отрядом, " + PeakBR.Levels.All.Length + " уровней, монстры. Отдельное меню тоже есть: Home. Выход из Backrooms — End (или F8).", 12, Ui.Muted);
            brSub = Ui.Buttons(new[] { "Вход", "Уровни", "Монстры", "Проверка", "Инфо" }, 5, 0, 32f, true, brSub, 14) is int c && c >= 0 ? c : brSub;
            bool hi = Ui.Toggle("Прятать остров на время забега (потом вернётся)", cfgBrIsland.Value); if (hi != cfgBrIsland.Value) { cfgBrIsland.Value = hi; PeakBR.Island.Enabled = hi; }
            bool au = Ui.Toggle("Сразу входить в Backrooms при появлении в игре", cfgBrAuto.Value); if (au != cfgBrAuto.Value) { cfgBrAuto.Value = au; PeakBR.Play.AutoJoin = au; }
            float fg = Ui.Slider("Густота тумана (меньше — лучше видно)", cfgBrFog.Value, 0.1f, 1.5f, "0.00"); if (Mathf.Abs(fg - cfgBrFog.Value) > 0.005f) { cfgBrFog.Value = fg; PeakBR.Play.FogMul = fg; }
            bool dd = Ui.Toggle("Нагнетающая атмосфера (шёпот, тишина, шаги за спиной, силуэты)", cfgBrDread.Value); if (dd != cfgBrDread.Value) { cfgBrDread.Value = dd; PeakBR.Dread.Enabled = dd; }
            bool nm = Ui.Toggle("Рельеф текстур (нормали) — со следующего уровня", cfgBrNormals.Value); if (nm != cfgBrNormals.Value) { cfgBrNormals.Value = nm; PeakBR.BrGfx.NormalMaps = nm; }
            bool ha = Ui.Toggle("Ореолы вокруг ламп — со следующего уровня", cfgBrHalos.Value); if (ha != cfgBrHalos.Value) { cfgBrHalos.Value = ha; PeakBR.LevelBuilder.Halos = ha; }
            bool rf = Ui.Toggle("Отражения в лужах — со следующего уровня", cfgBrRefl.Value); if (rf != cfgBrRefl.Value) { cfgBrRefl.Value = rf; PeakBR.Refl.On = rf; }
            bool bm = Ui.Toggle("Главное меню и заставка в стиле Backrooms", cfgBrMenu.Value); if (bm != cfgBrMenu.Value) cfgBrMenu.Value = bm;
            if (PeakBR.Island.Info != "") Ui.Para(PeakBR.Island.Info, 12, Ui.Muted);
            Ui.Space(4);
            Ui.Y = PeakBR.Plugin.DrawEmbedded(brSub, Ui.X, Ui.Y, Ui.W, Ui.Alpha);
        }

        void BrHubPanic() { try { if (PeakBR.Play.On) PeakBR.Play.Leave(false, "F8"); } catch { } }
    }
}
