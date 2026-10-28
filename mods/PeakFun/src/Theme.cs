// Темы оформления 1.12: палитра меню мода, HUD героя и стартового экрана. Выбор сохраняется в конфиге.
using UnityEngine;

namespace PeakFun
{
    internal sealed class ThemeDef
    {
        public string name, tag;
        public Color bg, side, card, cardHi, accent, accent2, grad3, txt, muted, good, bad, orb1, orb2, orb3;
        public bool dark = true;
    }

    internal static class Themes
    {
        static Color C(float r, float g, float b, float a = 1f) { return new Color(r, g, b, a); }

        public static readonly ThemeDef[] All =
        {
            new ThemeDef { name = "Бэкрумс", tag = "натриевая лампа, тёмная",
                bg = C(0.04f, 0.037f, 0.028f, 0.975f), side = C(0.065f, 0.06f, 0.044f), card = C(0.1f, 0.093f, 0.066f), cardHi = C(0.18f, 0.16f, 0.1f),
                accent = C(0.95f, 0.8f, 0.3f), accent2 = C(0.78f, 0.5f, 0.16f), grad3 = C(0.6f, 0.52f, 0.28f), txt = C(0.94f, 0.91f, 0.8f), muted = C(0.62f, 0.58f, 0.45f), good = C(0.55f, 0.86f, 0.45f), bad = C(0.92f, 0.27f, 0.2f),
                orb1 = C(1f, 0.85f, 0.3f, 0.07f), orb2 = C(0.8f, 0.5f, 0.15f, 0.06f), orb3 = C(0.9f, 0.8f, 0.4f, 0.04f) },
            new ThemeDef { name = "Классика", tag = "оранжево-розовая",
                bg = C(0.055f, 0.07f, 0.125f, 0.97f), side = C(0.075f, 0.095f, 0.17f), card = C(0.105f, 0.13f, 0.225f), cardHi = C(0.16f, 0.19f, 0.31f),
                accent = C(1f, 0.55f, 0.24f), accent2 = C(1f, 0.24f, 0.5f), grad3 = C(0.55f, 0.36f, 0.96f), txt = C(0.92f, 0.94f, 1f), muted = C(0.56f, 0.62f, 0.78f), good = C(0.3f, 0.87f, 0.5f), bad = C(1f, 0.32f, 0.36f),
                orb1 = C(1f, 0.45f, 0.2f, 0.10f), orb2 = C(0.5f, 0.3f, 1f, 0.12f), orb3 = C(1f, 0.2f, 0.45f, 0.08f) },
            new ThemeDef { name = "Красный", tag = "багровая, чёрная",
                bg = C(0.05f, 0.02f, 0.035f, 0.97f), side = C(0.09f, 0.03f, 0.05f), card = C(0.14f, 0.05f, 0.075f), cardHi = C(0.24f, 0.08f, 0.11f),
                accent = C(1f, 0.2f, 0.22f), accent2 = C(0.55f, 0.3f, 1f), grad3 = C(0.9f, 0.9f, 0.95f), txt = C(0.96f, 0.93f, 0.93f), muted = C(0.7f, 0.55f, 0.6f), good = C(0.4f, 0.9f, 0.55f), bad = C(1f, 0.4f, 0.3f),
                orb1 = C(1f, 0.1f, 0.15f, 0.14f), orb2 = C(0.4f, 0.2f, 0.9f, 0.12f), orb3 = C(1f, 0.3f, 0.2f, 0.08f) },
            new ThemeDef { name = "Космос", tag = "голубая, космос",
                bg = C(0.02f, 0.03f, 0.09f, 0.97f), side = C(0.035f, 0.05f, 0.14f), card = C(0.06f, 0.09f, 0.21f), cardHi = C(0.1f, 0.16f, 0.34f),
                accent = C(0.45f, 0.82f, 1f), accent2 = C(0.72f, 0.45f, 1f), grad3 = C(0.95f, 0.98f, 1f), txt = C(0.92f, 0.97f, 1f), muted = C(0.55f, 0.68f, 0.86f), good = C(0.4f, 0.95f, 0.8f), bad = C(1f, 0.4f, 0.55f),
                orb1 = C(0.3f, 0.7f, 1f, 0.14f), orb2 = C(0.6f, 0.3f, 1f, 0.14f), orb3 = C(0.9f, 0.95f, 1f, 0.06f) },
            new ThemeDef { name = "Неон", tag = "бирюза и маджента",
                bg = C(0.025f, 0.025f, 0.06f, 0.97f), side = C(0.04f, 0.04f, 0.1f), card = C(0.07f, 0.06f, 0.15f), cardHi = C(0.12f, 0.1f, 0.26f),
                accent = C(0f, 1f, 0.85f), accent2 = C(1f, 0.1f, 0.8f), grad3 = C(1f, 0.9f, 0.2f), txt = C(0.94f, 0.98f, 1f), muted = C(0.55f, 0.65f, 0.85f), good = C(0.3f, 1f, 0.6f), bad = C(1f, 0.25f, 0.4f),
                orb1 = C(0f, 1f, 0.85f, 0.12f), orb2 = C(1f, 0.1f, 0.8f, 0.12f), orb3 = C(1f, 0.9f, 0.2f, 0.06f) },
            new ThemeDef { name = "Горный", tag = "светлая, природа", dark = false,
                bg = C(0.93f, 0.95f, 0.91f, 0.98f), side = C(0.85f, 0.89f, 0.84f), card = C(0.97f, 0.98f, 0.95f), cardHi = C(0.84f, 0.91f, 0.83f),
                accent = C(0.16f, 0.55f, 0.36f), accent2 = C(0.95f, 0.58f, 0.2f), grad3 = C(0.35f, 0.6f, 0.9f), txt = C(0.08f, 0.14f, 0.11f), muted = C(0.33f, 0.42f, 0.37f), good = C(0.1f, 0.6f, 0.3f), bad = C(0.85f, 0.2f, 0.2f),
                orb1 = C(0.2f, 0.7f, 0.4f, 0.14f), orb2 = C(1f, 0.7f, 0.3f, 0.12f), orb3 = C(0.4f, 0.65f, 0.95f, 0.1f) },
        };

        public static int Cur;

        public static ThemeDef Now { get { return All[Mathf.Clamp(Cur, 0, All.Length - 1)]; } }

        public static void Apply(int i)
        {
            Cur = Mathf.Clamp(i, 0, All.Length - 1); var t = All[Cur];
            Ui.Bg = t.bg; Ui.Side = t.side; Ui.Card = t.card; Ui.CardHi = t.cardHi; Ui.Accent = t.accent; Ui.Accent2 = t.accent2; Ui.GradC = t.grad3;
            Ui.Txt = t.txt; Ui.Muted = t.muted; Ui.Good = t.good; Ui.Bad = t.bad; Ui.Orb1 = t.orb1; Ui.Orb2 = t.orb2; Ui.Orb3 = t.orb3; Ui.Dark = t.dark;
            Ui.RebuildGradient();
        }
    }
}
