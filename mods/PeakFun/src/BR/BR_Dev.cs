// DEV-сборка Backrooms (PeakFunDEV.dll): единственный чит — «пропустить уровень» (F10), маленькая плашка в углу.
// В обычной сборке (без символа DEV) весь код выключен, флаги всегда false.
using System;
using UnityEngine;

namespace PeakBR
{
    internal static class Dev
    {
        public static bool God, Freeze, Bright, Esp, MapOn;
#if DEV
        public const bool Active = true;
        static float skipAt, flashT;

        public static void Tick(float dt)
        {
            if (Play.On && !Play.Busy && Keys.Down(KeyCode.F10) && Time.unscaledTime > skipAt)
            {
                skipAt = Time.unscaledTime + 1.2f; flashT = Time.unscaledTime + 0.8f;
                if (Play.IsHost) Play.Skip(); else Ui.Toast("Пропускает уровень только хост", Ui.Bad);
            }
        }
        public static void Draw(float W, float H, float s)
        {
            if (!Play.On) return;
            float w = 238f * s, h = 30f * s; var r = new Rect(14f * s, 14f * s, w, h); bool flash = Time.unscaledTime < flashT;
            Ui.Box(r, new Color(0.03f, 0.025f, 0.02f, flash ? 0.92f : 0.62f), 8f * s); Ui.Border(r, flash ? new Color(1f, 1f, 1f, 0.9f) : new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.55f), 1f);
            Ui.Text(new Rect(r.x + 12f * s, r.y, w - 24f * s, h), "▶  ПРОПУСТИТЬ УРОВЕНЬ   [F10]", (int)(11f * s), flash ? Color.white : new Color(1f, 0.93f, 0.8f, 0.92f), TextAnchor.MiddleLeft, true);
        }
        public static void Reset() { }
#else
        public const bool Active = false;
        public static void Tick(float dt) { }
        public static void Draw(float W, float H, float s) { }
        public static void Reset() { }
#endif
    }
}
