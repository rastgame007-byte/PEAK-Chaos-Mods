// Темы оформления 1.12: палитра меню мода, HUD героя и стартового экрана. Выбор сохраняется в конфиге.
using UnityEngine;

namespace PeakJJK
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
            new ThemeDef { name = "Проклятая энергия", tag = "фиолетовая, чёрная",
                bg = C(0.04f, 0.025f, 0.075f, 0.97f), side = C(0.07f, 0.04f, 0.12f), card = C(0.11f, 0.07f, 0.18f), cardHi = C(0.19f, 0.12f, 0.3f),
                accent = C(0.68f, 0.38f, 1f), accent2 = C(0.25f, 0.85f, 1f), grad3 = C(1f, 0.3f, 0.45f), txt = C(0.95f, 0.93f, 1f), muted = C(0.65f, 0.58f, 0.82f), good = C(0.4f, 0.9f, 0.6f), bad = C(1f, 0.35f, 0.4f),
                orb1 = C(0.6f, 0.3f, 1f, 0.14f), orb2 = C(0.2f, 0.8f, 1f, 0.1f), orb3 = C(1f, 0.2f, 0.4f, 0.08f) },
            new ThemeDef { name = "Годзё", tag = "голубая, белая",
                bg = C(0.02f, 0.035f, 0.1f, 0.97f), side = C(0.035f, 0.055f, 0.15f), card = C(0.06f, 0.1f, 0.22f), cardHi = C(0.1f, 0.17f, 0.36f),
                accent = C(0.4f, 0.75f, 1f), accent2 = C(0.8f, 0.55f, 1f), grad3 = C(0.95f, 0.98f, 1f), txt = C(0.93f, 0.97f, 1f), muted = C(0.55f, 0.68f, 0.88f), good = C(0.4f, 0.95f, 0.8f), bad = C(1f, 0.4f, 0.5f),
                orb1 = C(0.3f, 0.65f, 1f, 0.16f), orb2 = C(0.7f, 0.4f, 1f, 0.12f), orb3 = C(1f, 1f, 1f, 0.05f) },
            new ThemeDef { name = "Сукуна", tag = "багровая, чёрная",
                bg = C(0.05f, 0.015f, 0.025f, 0.97f), side = C(0.09f, 0.025f, 0.04f), card = C(0.14f, 0.045f, 0.065f), cardHi = C(0.25f, 0.07f, 0.1f),
                accent = C(1f, 0.2f, 0.2f), accent2 = C(1f, 0.6f, 0.5f), grad3 = C(0.95f, 0.9f, 0.9f), txt = C(0.97f, 0.93f, 0.93f), muted = C(0.72f, 0.55f, 0.58f), good = C(0.5f, 0.9f, 0.55f), bad = C(1f, 0.4f, 0.3f),
                orb1 = C(1f, 0.1f, 0.12f, 0.15f), orb2 = C(0.6f, 0.1f, 0.2f, 0.12f), orb3 = C(1f, 0.5f, 0.3f, 0.07f) },
            new ThemeDef { name = "Юдзи", tag = "оранжевая, тёмная",
                bg = C(0.06f, 0.04f, 0.04f, 0.97f), side = C(0.1f, 0.06f, 0.06f), card = C(0.16f, 0.1f, 0.09f), cardHi = C(0.27f, 0.16f, 0.12f),
                accent = C(1f, 0.55f, 0.2f), accent2 = C(1f, 0.25f, 0.3f), grad3 = C(1f, 0.85f, 0.4f), txt = C(0.97f, 0.94f, 0.9f), muted = C(0.75f, 0.62f, 0.55f), good = C(0.5f, 0.9f, 0.5f), bad = C(1f, 0.35f, 0.3f),
                orb1 = C(1f, 0.5f, 0.15f, 0.14f), orb2 = C(1f, 0.2f, 0.3f, 0.1f), orb3 = C(1f, 0.85f, 0.4f, 0.07f) },
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
