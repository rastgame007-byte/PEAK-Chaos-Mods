// Стартовый экран 1.12: интро-заставка при запуске, оформление главного меню / экрана загрузки (поверх игры), подсказки,
// выбор темы. Свои кнопки живут только в углу, остальное не перехватывает мышь, так что игровое меню работает как обычно.
using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        ConfigEntry<bool> cfgIntro; ConfigEntry<int> cfgTheme;
        float introT0 = -1f; bool introDone, introSkipped, canvasDumped; float menuSeenAt = -1f; int tipIdx; float nextTip;
        const float IntroLen = 3.8f;

        static readonly string[] Tips =
        {
            "F10 — стать героем. Альбус и Владыка Морок — в списке «Герои».",
            "G — «Великий купол» Альбуса и Морока. Перед кастом включи полноэкранную заставку.",
            "F3 — тест в одиночку: бот-жертва и эхо «глазами друга». F2 — камера-наблюдатель.",
            "Зажми ПКМ и отпусти — тяжёлый удар; X — схватить друга за горло.",
            "У друзей без мода физика работает, у друзей с PeakFun — ещё и купол, лучи и костюмы.",
            "F8 — стоп: убирает все эффекты с экрана. F4 — управлять другом.",
            "Тему оформления можно сменить в углу этого экрана или во вкладке «Инфо».",
        };

        void InitTheme()
        {
            cfgTheme = Config.Bind("UI", "Theme", 0, "Тема оформления: 0 Бэкрумс, 1 Классика, 2 Красный, 3 Космос, 4 Неон, 5 Горный");
            cfgIntro = Config.Bind("UI", "IntroOnStart", true, "Показывать заставку при запуске игры");
            
            Themes.Apply(cfgTheme.Value);
        }

        void SetTheme(int i)
        {
            Themes.Apply(i); cfgTheme.Value = Themes.Cur;
        }

        // ---------- вкладка/блок выбора темы ----------
        void ThemePicker()
        {
            Ui.Header("Тема оформления");
            var names = new string[Themes.All.Length];
            for (int i = 0; i < names.Length; i++) names[i] = Themes.All[i].name;
            int c = Ui.Buttons(names, 3, 1, 38f, true, Themes.Cur, 14);
            if (c >= 0) SetTheme(c);
            Ui.Para(Themes.Now.name + " — " + Themes.Now.tag + ". Меняет меню, HUD героя и стартовый экран.", 12, Ui.Muted);
            cfgIntro.Value = Ui.Toggle("Заставка при запуске игры", cfgIntro.Value);
            if (Ui.Button("Показать заставку ещё раз")) { introT0 = Time.unscaledTime; introDone = false; introSkipped = false; }
        }

        // ---------- рисование ----------
        void DrawStartScreen()
        {
            var e = Event.current;
            if (introT0 < 0f) introT0 = Time.unscaledTime;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.5f);
            if (!introDone && cfgIntro.Value) { DrawIntro(s); return; }
            introDone = true;
            DrawBrMenuScreen(s);
        }

        void DrawIntro(float s)
        {
            float t = Time.unscaledTime - introT0;
            if (!introSkipped && t > 0.8f && (Input.anyKeyDown || Input.GetMouseButtonDown(0))) { introSkipped = true; introT0 = Time.unscaledTime - (IntroLen - 0.6f); t = IntroLen - 0.6f; }
            if (t >= IntroLen) { introDone = true; return; }
            if (cfgBrMenu != null && cfgBrMenu.Value) { DrawIntroBR(s, t); return; }
            if (Event.current.type != EventType.Repaint) return;
            float fadeIn = Mathf.Clamp01(t / 0.5f), fadeOut = Mathf.Clamp01((IntroLen - t) / 0.7f), a = Mathf.Min(fadeIn, fadeOut);
            float W = Screen.width, H = Screen.height;
            var th = Themes.Now;
            var bg = th.dark ? new Color(0.01f, 0.01f, 0.025f, 1f) : new Color(0.95f, 0.97f, 0.93f, 1f);
            bg.a = a; Ui.Box(new Rect(0, 0, W, H), bg, 0f);
            // диагональные полосы акцентного цвета
            for (int i = 0; i < 7; i++)
            {
                float k = Mathf.Clamp01((t - 0.1f * i) / 0.9f); float e = 1f - Mathf.Pow(1f - k, 3f);
                var col = Color.Lerp(th.accent, th.accent2, i / 6f); col.a = (0.10f + 0.04f * (i % 3)) * a;
                float y = H * (0.12f + i * 0.12f);
                Ui.Box(new Rect(-W * 0.3f + W * 1.3f * e - W * 0.2f * (i % 2), y, W * 0.6f, 6f * s * (1f + i % 3)), col, 0f);
            }
            Ui.Orb(new Vector2(W * 0.5f, H * 0.5f), H * (1.1f + 0.1f * Mathf.Sin(t * 2f)), new Color(th.accent.r, th.accent.g, th.accent.b, 0.18f * a));
            // логотип по буквам
            string logo = "PEAK"; float fs = 150f * s; float lw = fs * 0.78f; float x0 = W * 0.5f - lw * logo.Length * 0.5f;
            for (int i = 0; i < logo.Length; i++)
            {
                float k = Mathf.Clamp01((t - 0.3f - i * 0.12f) / 0.45f); float e = Ui.EaseOutBack(k);
                var col = th.txt; col.a = k * a;
                Ui.Text(new Rect(x0 + lw * i, H * 0.5f - fs * 0.78f + (1f - e) * 70f * s, lw, fs), logo[i].ToString(), (int)fs, col, TextAnchor.MiddleCenter, true);
            }
            float k2 = Mathf.Clamp01((t - 1.0f) / 0.6f);
            var c2 = th.accent; c2.a = k2 * a;
            Ui.Text(new Rect(0, H * 0.5f + 12f * s, W, 70f * s), "F  U  N   P  A  C  K", (int)(46f * s), c2, TextAnchor.MiddleCenter, true);
            var c3 = th.muted; c3.a = Mathf.Clamp01((t - 1.5f) / 0.6f) * a;
            Ui.Text(new Rect(0, H * 0.5f + 80f * s, W, 36f * s), "Герои  ·  Баскетбол  ·  Кино   —   v" + Version, (int)(20f * s), c3, TextAnchor.MiddleCenter, false);
            // линия прогресса
            float p = Mathf.Clamp01(t / (IntroLen - 0.4f)); var pc = th.accent; pc.a = a;
            Ui.Box(new Rect(W * 0.3f, H * 0.82f, W * 0.4f * p, 3f * s), pc, 0f);
            var hint = th.muted; hint.a = a * 0.7f;
            Ui.Text(new Rect(0, H * 0.84f, W, 26f * s), "любая клавиша — пропустить", (int)(14f * s), hint, TextAnchor.MiddleCenter, false);
            // чёрные полосы кино
            float bar = (1f - Mathf.Clamp01(t / 0.6f)) * H * 0.15f + H * 0.06f;
            Ui.Box(new Rect(0, 0, W, bar * a), new Color(0, 0, 0, 1f), 0f); Ui.Box(new Rect(0, H - bar * a, W, bar * a), new Color(0, 0, 0, 1f), 0f);
        }

    }
}
