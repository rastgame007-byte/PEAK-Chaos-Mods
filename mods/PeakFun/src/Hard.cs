// «Жёсткие» пранки: фейковый вылет на рабочий стол, обновление Windows, «взлом», вспышка со звоном в ушах,
// землетрясение, вирусные окна и (по желанию) настоящий выход из игры.
// Всё, кроме последнего, — только картинка/звук поверх игры. Настоящий выход закрывает ТОЛЬКО игру и только по явному согласию.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace PeakFun
{
    internal static class Hard
    {
        static float Now { get { return Time.unscaledTime; } }

        public static bool IsHard(FxKind k)
        {
            switch (k)
            {
                case FxKind.FakeCrash: case FxKind.WinUpdate: case FxKind.Hack: case FxKind.Flash:
                case FxKind.Quake: case FxKind.Virus: case FxKind.Quit: return true;
            }
            return false;
        }

        public static float Fix(FxKind k, float dur)
        {
            switch (k)
            {
                case FxKind.FakeCrash: return 13.5f;
                case FxKind.WinUpdate: return Mathf.Max(dur, 12f);
                case FxKind.Hack: return Mathf.Max(dur, 11f);
                case FxKind.Flash: return 5.5f;
                case FxKind.Quake: return Mathf.Max(dur, 5f);
                case FxKind.Virus: return Mathf.Max(dur, 12f);
                case FxKind.Quit: return 6f;
            }
            return dur;
        }

        // ======================= запуск / остановка =======================
        public static float QuakeUntil, QuakeStart;
        static bool quitDone;

        public static void OnStart(FxKind k, float dur)
        {
            switch (k)
            {
                case FxKind.FakeCrash: Lag.HoldFor(1.7f); Lag.MuteFor(dur - 0.5f); break;
                case FxKind.Quit: Lag.HoldFor(dur); Lag.MuteFor(dur); quitDone = false; break;
                case FxKind.Flash: Lag.MuteFor(1.4f); Sfx.Whine(Mathf.Min(dur, 5.5f)); break;
                case FxKind.Quake: QuakeStart = Now; QuakeUntil = Now + dur; Sfx.Rumble(Mathf.Min(dur, 12f)); CamShake.Install(); break;
                case FxKind.WinUpdate: Lag.MuteFor(dur * 0.5f); break;
            }
        }

        public static void Reset() { QuakeUntil = 0f; Sfx.Stop(); }

        /// <summary>Сила тряски 0..1 (для камеры и физики).</summary>
        public static float QuakeLevel
        {
            get
            {
                if (Now >= QuakeUntil) return 0f;
                float t = Now - QuakeStart, left = QuakeUntil - Now;
                return Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01(left / 1.2f);
            }
        }

        // ======================= рисование =======================
        public static void Draw(FxKind kind, float t, float dur, float k, Rect full)
        {
            if (Event.current.type != EventType.Repaint) return;
            switch (kind)
            {
                case FxKind.FakeCrash: DrawFakeCrash(full, t, dur, k); break;
                case FxKind.WinUpdate: DrawUpdate(full, t, dur, k); break;
                case FxKind.Hack: DrawHack(full, t, dur, k); break;
                case FxKind.Flash: DrawFlash(full, t, dur, k); break;
                case FxKind.Quake: DrawQuake(full, t, dur, k); break;
                case FxKind.Virus: DrawVirus(full, t, dur, k); break;
                case FxKind.Quit: DrawQuit(full, t, dur, k); break;
            }
        }

        static void Tint(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
        static readonly System.Random rnd = new System.Random();

        // ---------- фейковый вылет на рабочий стол ----------
        static Texture2D cursorTex;
        static readonly string[] cursorArt =
        {
            "B...........", "BB..........", "BWB.........", "BWWB........", "BWWWB.......", "BWWWWB......", "BWWWWWB.....",
            "BWWWWWWB....", "BWWWWWWWB...", "BWWWWWWWWB..", "BWWWWWWWWWB.", "BWWWWWBBBBB.", "BWWBWWB.....", "BWB.BWWB....",
            "BB..BWWB....", "B....BWWB...", ".....BWWB...", "......BB...."
        };

        static void EnsureCursor()
        {
            if (cursorTex != null) return;
            int w = 12, h = cursorArt.Length;
            cursorTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char c = cursorArt[y][x];
                    cursorTex.SetPixel(x, h - 1 - y, c == 'B' ? Color.black : c == 'W' ? Color.white : new Color(0, 0, 0, 0));
                }
            cursorTex.Apply();
        }

        static void Cursor_(Vector2 p, float k)
        {
            EnsureCursor();
            float s = 1.7f * k;
            GUI.DrawTexture(new Rect(p.x, p.y, 12 * s, cursorArt.Length * s), cursorTex);
        }

        struct Way { public float t, x, y; public Way(float t, float x, float y) { this.t = t; this.x = x; this.y = y; } }

        static Vector2 Path(Way[] w, float t)
        {
            if (t <= w[0].t) return new Vector2(w[0].x, w[0].y);
            for (int i = 1; i < w.Length; i++)
                if (t <= w[i].t)
                {
                    float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(w[i - 1].t, w[i].t, t));
                    return Vector2.Lerp(new Vector2(w[i - 1].x, w[i - 1].y), new Vector2(w[i].x, w[i].y), u);
                }
            return new Vector2(w[w.Length - 1].x, w[w.Length - 1].y);
        }

        static void Icon(Rect r, string label, Color c, string glyph, bool sel, float k)
        {
            if (sel) Ui.Box(new Rect(r.x - 4 * k, r.y - 4 * k, r.width + 8 * k, r.height + 30 * k), new Color(0.6f, 0.8f, 1f, 0.28f), 3 * k);
            Ui.Box(new Rect(r.x + 8 * k, r.y + 4 * k, r.width - 16 * k, r.width - 16 * k), c, 10 * k);
            Ui.Text(new Rect(r.x + 8 * k, r.y + 4 * k, r.width - 16 * k, r.width - 16 * k), glyph, (int)(26 * k), Color.white, TextAnchor.MiddleCenter, true);
            Ui.Text(new Rect(r.x - 10 * k, r.y + r.width - 8 * k, r.width + 20 * k, 22 * k), label, (int)(13 * k), Color.white, TextAnchor.MiddleCenter, true);
        }

        static void Desktop(Rect full, float k, int selIcon)
        {
            // обои: мягкий синий градиент
            int bands = 28;
            for (int i = 0; i < bands; i++)
            {
                float u = i / (float)(bands - 1);
                Tint(new Rect(0, full.height * i / bands, full.width, full.height / bands + 1), Color.Lerp(new Color(0.02f, 0.09f, 0.28f), new Color(0.08f, 0.42f, 0.78f), u));
            }
            Ui.Orb(new Vector2(full.width * 0.62f, full.height * 0.42f), 900 * k, new Color(0.4f, 0.75f, 1f, 0.35f));
            // значки
            string[] names = { "Корзина", "Steam", "Discord", "Google Chrome", "Telegram", "Блокнот" };
            string[] gl = { "\u267B", "S", "D", "G", "T", "N" };
            Color[] cols = { new Color(0.4f, 0.45f, 0.5f), new Color(0.1f, 0.25f, 0.45f), new Color(0.35f, 0.4f, 0.95f), new Color(0.9f, 0.4f, 0.25f), new Color(0.2f, 0.6f, 0.9f), new Color(0.3f, 0.7f, 0.9f) };
            for (int i = 0; i < names.Length; i++)
                Icon(IconRect(i, k), names[i], cols[i], gl[i], i == selIcon, k);
            // панель задач
            float th = 46 * k;
            Tint(new Rect(0, full.height - th, full.width, th), new Color(0.07f, 0.08f, 0.11f, 0.94f));
            float sx = full.width / 2f - 120 * k, sy = full.height - th + 14 * k, q = 8 * k;
            for (int i = 0; i < 4; i++) Tint(new Rect(sx + (i % 2) * (q + 2 * k), sy + (i / 2) * (q + 2 * k), q, q), new Color(0.35f, 0.7f, 1f));
            for (int i = 0; i < 5; i++) Ui.Box(new Rect(sx + 50 * k + i * 44 * k, full.height - th + 7 * k, 32 * k, 32 * k), new Color(1, 1, 1, i == 1 ? 0.22f : 0.1f), 6 * k);
            var now = DateTime.Now;
            Ui.Text(new Rect(full.width - 110 * k, full.height - th + 2 * k, 100 * k, 22 * k), now.ToString("HH:mm"), (int)(14 * k), Color.white, TextAnchor.MiddleRight);
            Ui.Text(new Rect(full.width - 110 * k, full.height - th + 22 * k, 100 * k, 20 * k), now.ToString("dd.MM.yyyy"), (int)(12 * k), new Color(0.85f, 0.88f, 0.95f), TextAnchor.MiddleRight);
        }

        static Rect IconRect(int i, float k) { return new Rect(26 * k, (22 + i * 104) * k, 84 * k, 84 * k); }

        static void DrawFakeCrash(Rect full, float t, float dur, float k)
        {
            // 0–1.7: замороженный кадр (рисует Lag.DrawHold) + «не отвечает»
            if (t < 1.7f)
            {
                if (t > 0.35f) Tint(full, new Color(1f, 1f, 1f, Mathf.Min(0.1f, (t - 0.35f) * 0.12f)));
                return;
            }
            // 1.7–2.7: чёрный экран — «игра закрылась»
            float fadeOut = Mathf.Clamp01((dur - t) / 0.5f);
            if (t < 2.9f) { Tint(full, Color.black); return; }
            float u = t - 2.9f;
            Ui.Alpha = 1f;
            Desktop(full, k, u > 5.4f && u < 6.4f ? 1 : -1);

            // окно ошибки: u 0.7 … 3.3
            var dlg = new Rect(full.center.x - 280 * k, full.center.y - 150 * k, 560 * k, 270 * k);
            float w1 = Mathf.Min(dlg.width - 40 * k, 330 * k);
            var btn = new Vector2(dlg.xMax - 20 * k - w1 / 2f, dlg.yMax - 52 * k + 16 * k);
            var steam = IconRect(1, k).center;
            var way = new[]
            {
                new Way(0f, 0.5f * full.width, 0.8f * full.height), new Way(1.2f, 0.5f * full.width, 0.8f * full.height),
                new Way(2.9f, btn.x, btn.y), new Way(3.4f, btn.x, btn.y),
                new Way(5.3f, steam.x, steam.y), new Way(7.6f, steam.x, steam.y)
            };
            if (u > 0.7f && u < 3.4f)
                Lag.WinDialog(dlg, "PEAK.exe", "Прекращена работа программы «PEAK»\nWindows ищет решение этой проблемы…", "Закрыть программу", null, k, Mathf.Clamp01((u - 0.7f) / 0.12f));

            // Steam-окно «Запуск PEAK»: u 6.4 … 11
            if (u > 6.2f)
            {
                float a = Mathf.Clamp01((u - 6.2f) / 0.3f);
                float w = 420 * k, h = 150 * k;
                var r = new Rect(full.center.x - w / 2, full.center.y - h / 2, w, h);
                Ui.Alpha = a;
                Ui.Box(new Rect(r.x - 2 * k, r.y - 2 * k, r.width + 4 * k, r.height + 4 * k), new Color(0, 0, 0, 0.35f), 4 * k);
                Ui.Box(r, new Color(0.09f, 0.11f, 0.15f, 1f), 3 * k);
                Ui.Box(new Rect(r.x + 18 * k, r.y + 22 * k, 54 * k, 54 * k), new Color(0.1f, 0.35f, 0.6f), 27 * k);
                Ui.Text(new Rect(r.x + 18 * k, r.y + 22 * k, 54 * k, 54 * k), "S", (int)(28 * k), Color.white, TextAnchor.MiddleCenter, true);
                Ui.Text(new Rect(r.x + 88 * k, r.y + 20 * k, w - 100 * k, 28 * k), "Запуск PEAK", (int)(20 * k), Color.white, TextAnchor.MiddleLeft, true);
                float p = Mathf.Clamp01((u - 6.6f) / 3.8f);
                string st = p < 0.35f ? "Проверка файлов игры…" : p < 0.8f ? "Подготовка к запуску…" : "Запуск…";
                Ui.Text(new Rect(r.x + 88 * k, r.y + 50 * k, w - 100 * k, 24 * k), st, (int)(14 * k), new Color(0.75f, 0.8f, 0.9f), TextAnchor.MiddleLeft);
                Ui.Box(new Rect(r.x + 18 * k, r.y + 104 * k, w - 36 * k, 10 * k), new Color(1, 1, 1, 0.12f), 5 * k);
                Ui.Box(new Rect(r.x + 18 * k, r.y + 104 * k, Mathf.Max(8 * k, (w - 36 * k) * p), 10 * k), new Color(0.35f, 0.75f, 1f), 5 * k);
                Ui.Alpha = 1f;
            }
            // курсор
            if (u > 0.2f && u < 8.6f)
            {
                var cp = Path(way, u);
                bool click = (u > 3.25f && u < 3.4f) || (u > 5.55f && u < 5.7f) || (u > 5.85f && u < 6.0f);
                if (click) Ui.Box(new Rect(cp.x - 14 * k, cp.y - 14 * k, 28 * k, 28 * k), new Color(1, 1, 1, 0.35f), 14 * k);
                Cursor_(cp, k);
            }
            // иконка Steam подсвечивается при наведении
            // затухание в конце
            if (dur - t < 0.5f) Tint(full, new Color(0, 0, 0, 1f - fadeOut));
            // полный чёрный перед возвратом в игру
            if (u > 10.0f) Tint(full, new Color(0, 0, 0, Mathf.Clamp01((u - 10.0f) / 0.5f)));
        }

        // ---------- обновление Windows ----------
        static void DrawUpdate(Rect full, float t, float dur, float k)
        {
            float a = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((dur - t) / 0.6f);
            Tint(full, new Color(0f, 0.36f, 0.66f, a > 0.99f ? 1f : a));
            float cx = full.center.x, cy = full.center.y - 70 * k;
            for (int i = 0; i < 6; i++)
            {
                float ph = Mathf.Repeat(t * 0.62f - i * 0.075f, 1f);
                float ang = (ph * 1.1f) * Mathf.PI * 2f - Mathf.PI / 2f;
                float vis = ph < 0.9f ? 1f : 0f;
                float rr = 34 * k, d = 8 * k;
                float sp = Mathf.Clamp01(ph / 0.9f);
                float ease = Mathf.Sin(sp * Mathf.PI * 0.5f);
                ang = ease * Mathf.PI * 2f - Mathf.PI / 2f;
                if (vis > 0f) Tint(new Rect(cx + Mathf.Cos(ang) * rr - d / 2, cy + Mathf.Sin(ang) * rr - d / 2, d, d), new Color(1, 1, 1, a));
            }
            int baseP = 21;
            int pct = Mathf.Min(99, baseP + (int)(t / dur * 70f));
            Ui.Alpha = a;
            Ui.Text(new Rect(0, full.center.y + 10 * k, full.width, 50 * k), "Работа с обновлениями", (int)(34 * k), Color.white, TextAnchor.MiddleCenter);
            Ui.Text(new Rect(0, full.center.y + 60 * k, full.width, 40 * k), "Выполнено " + pct + "%", (int)(28 * k), Color.white, TextAnchor.MiddleCenter);
            Ui.Text(new Rect(0, full.center.y + 120 * k, full.width, 40 * k), "Не выключайте компьютер", (int)(24 * k), Color.white, TextAnchor.MiddleCenter);
            Ui.Alpha = 1f;
        }

        // ---------- «взлом» ----------
        static int[] mHead; static float mNext; static string[] mTrail; static string[] mHeadCh;
        const string MChars = "01ABCDEF#$%&*+<>=?/\\|{}";

        static void DrawHack(Rect full, float t, float dur, float k)
        {
            float a = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((dur - t) / 0.6f);
            Tint(full, new Color(0, 0.02f, 0, 0.93f * a));
            int fs = Mathf.Max(10, (int)(17 * k));
            float colW = fs * 1.15f, lineH = fs * 1.2f;
            int cols = Mathf.Max(8, (int)(full.width / colW));
            int rows = Mathf.Max(8, (int)(full.height / lineH));
            if (mHead == null || mHead.Length != cols) { mHead = new int[cols]; mTrail = new string[cols]; mHeadCh = new string[cols]; for (int i = 0; i < cols; i++) mHead[i] = -rnd.Next(rows); mNext = 0; }
            if (Now >= mNext)
            {
                mNext = Now + 0.075f;
                for (int i = 0; i < cols; i++)
                {
                    mHead[i]++;
                    if (mHead[i] > rows + 14) mHead[i] = -rnd.Next(rows / 2);
                    int L = 10 + (i * 7) % 9;
                    var sb = new System.Text.StringBuilder();
                    for (int j = 0; j < L; j++) { sb.Append(MChars[rnd.Next(MChars.Length)]); if (j < L - 1) sb.Append('\n'); }
                    mTrail[i] = sb.ToString(); mHeadCh[i] = MChars[rnd.Next(MChars.Length)].ToString();
                }
            }
            for (int i = 0; i < cols; i++)
            {
                int h = mHead[i]; int L = 10 + (i * 7) % 9;
                if (h < 0) continue;
                float x = i * colW;
                Ui.Alpha = a * 0.7f;
                Ui.Text(new Rect(x, (h - L) * lineH, colW, L * lineH), mTrail[i], fs, new Color(0.1f, 0.85f, 0.25f), TextAnchor.UpperCenter);
                Ui.Alpha = a;
                Ui.Text(new Rect(x, h * lineH, colW, lineH), mHeadCh[i], fs, new Color(0.85f, 1f, 0.9f), TextAnchor.UpperCenter, true);
            }
            // терминал
            float w = Mathf.Min(full.width * 0.7f, 900 * k), hh = 330 * k;
            var r = new Rect(full.center.x - w / 2, full.center.y - hh / 2, w, hh);
            Ui.Alpha = a;
            Ui.Box(new Rect(r.x - 3 * k, r.y - 3 * k, r.width + 6 * k, r.height + 6 * k), new Color(0.1f, 1f, 0.3f, 0.7f), 4 * k);
            Ui.Box(r, new Color(0.0f, 0.04f, 0.0f, 0.96f), 3 * k);
            string me = "игрок"; try { var c = Game.Me(); if (c != null) me = Game.Name(c); } catch { }
            var rs = new System.Random(77);
            string ip = "10." + rs.Next(10, 250) + "." + rs.Next(0, 250) + "." + rs.Next(2, 250);
            var lines = new List<string>
            {
                "root@peak-lobby:~# ./connect --target \"" + me + "\"",
                "[+] Подключение к " + ip + " ...",
                "[+] Обход Photon-авторизации ... OK",
                "[+] Подмена ключа сессии ... OK",
                "[+] Загрузка файлов: " + Mathf.Min(100, (int)((t - 3.0f) / (dur - 6.5f) * 100f)).ToString().PadLeft(3) + "%  " + Bar(Mathf.Clamp01((t - 3.0f) / (dur - 6.5f)), 24),
                "[!] ДОСТУП ПОЛУЧЕН. Управление клиентом перехвачено.",
                "[+] Загрузка модуля PEAK-ROOT.dll ... готово"
            };
            float[] at = { 0.5f, 1.3f, 2.0f, 2.6f, 3.0f, dur - 3.5f, dur - 2.6f };
            float y = r.y + 16 * k;
            for (int i = 0; i < lines.Count; i++)
            {
                if (t < at[i]) break;
                int n = lines[i].Length;
                if (i != 4) n = Mathf.Min(n, (int)((t - at[i]) * 70f));
                Color col = i == 5 ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.4f);
                Ui.Text(new Rect(r.x + 20 * k, y, r.width - 40 * k, 28 * k), lines[i].Substring(0, Mathf.Min(n, lines[i].Length)), (int)(18 * k), col, TextAnchor.MiddleLeft);
                y += 30 * k;
            }
            if (t > dur - 1.9f)
                Ui.Text(new Rect(r.x, r.yMax - 66 * k, r.width, 44 * k), "ШУТКА :)  Ничего не взломано — это пранк от друга", (int)(22 * k), new Color(1f, 0.95f, 0.4f), TextAnchor.MiddleCenter, true);
            else if (((int)(t * 2f)) % 2 == 0) Ui.Text(new Rect(r.x + 20 * k, y, 30 * k, 28 * k), "_", (int)(18 * k), new Color(0.3f, 1f, 0.4f), TextAnchor.MiddleLeft);
            Ui.Alpha = 1f;
        }

        static string Bar(float p, int n)
        {
            int f = Mathf.RoundToInt(p * n);
            return "[" + new string('#', f) + new string('.', n - f) + "]";
        }

        // ---------- вспышка ----------
        static void DrawFlash(Rect full, float t, float dur, float k)
        {
            float a = t < 0.7f ? 1f : Mathf.Pow(1f - Mathf.Clamp01((t - 0.7f) / (dur - 0.7f)), 1.7f);
            Tint(full, new Color(1f, 1f, 0.97f, a));
            if (t > 0.6f)
            {   // «остаточное изображение» — размытое тёмное пятно в центре, расплывается
                float b = Mathf.Clamp01((t - 0.6f) / 0.8f) * Mathf.Clamp01((dur - t) / 2.0f);
                Ui.Orb(full.center + new Vector2(Mathf.Sin(t * 1.3f) * 14 * k, Mathf.Cos(t * 1.1f) * 10 * k), 700 * k, new Color(0.35f, 0.35f, 0.45f, 0.55f * b));
                Ui.Orb(full.center + new Vector2(-220 * k, 90 * k), 260 * k, new Color(0.25f, 0.45f, 0.35f, 0.35f * b));
            }
        }

        // ---------- землетрясение ----------
        static void DrawQuake(Rect full, float t, float dur, float k)
        {
            float lv = QuakeLevel;
            if (lv <= 0.01f) return;
            Tint(full, new Color(0.1f, 0.06f, 0.02f, 0.18f * lv));
            // сыплется пыль и мелкие камни
            var rr = new System.Random((int)(Now * 14f) * 31 + 5);
            int n = (int)(70 * lv);
            for (int i = 0; i < n; i++)
            {
                float x = (float)rr.NextDouble() * full.width, y = (float)rr.NextDouble() * full.height;
                float s = (1.5f + (float)rr.NextDouble() * 5f) * k;
                Tint(new Rect(x, y, s, s * (1f + (float)rr.NextDouble() * 3f)), new Color(0.55f, 0.46f, 0.36f, 0.5f * lv));
            }
            // полосы-«дрожь»
            for (int i = 0; i < 3; i++)
                Tint(new Rect(0, (float)rr.NextDouble() * full.height, full.width, (2f + (float)rr.NextDouble() * 6f) * k), new Color(0, 0, 0, 0.18f * lv));
        }

        // ---------- вирусные окна ----------
        static readonly string[] vTitles = { "Windows Security", "ОШИБКА", "Антивирус", "System", "PEAK.exe", "Предупреждение" };
        static readonly string[] vBodies =
        {
            "Обнаружено 3 угрозы:\nTrojan:Win32/Peak.Gen!A", "Ваш компьютер заражён!\nНемедленно нажмите ОК", "Критическая ошибка 0x0000007B\nINACCESSIBLE_BOOT_DEVICE",
            "Обнаружена активность майнера\nв процессе PEAK.exe", "Недостаточно памяти.\nЗакройте часть программ.", "Файл PEAK.dll повреждён\nи будет удалён", "Подозрительное подключение\nк 185.220.101.4:443"
        };

        static void DrawVirus(Rect full, float t, float dur, float k)
        {
            float end = Mathf.Clamp01((dur - t) / 0.35f);
            int n = Mathf.Min(16, (int)(t / 0.5f) + 1);
            var rr = new System.Random(2024);
            for (int i = 0; i < n; i++)
            {
                float w = 400 * k, h = 200 * k;
                float x = (float)rr.NextDouble() * (full.width - w), y = (float)rr.NextDouble() * (full.height - h);
                string ti = vTitles[rr.Next(vTitles.Length)], bo = vBodies[rr.Next(vBodies.Length)];
                float age = t - i * 0.5f;
                float a = Mathf.Clamp01(age / 0.1f) * end;
                if (a <= 0f) continue;
                // дрожание «новых» окон
                float sh = age < 0.4f ? Mathf.Sin(age * 80f) * 4f * k : 0f;
                Lag.WinDialog(new Rect(x + sh, y, w, h), ti, bo, "ОК", null, k, a);
            }
            if (end < 1f) Tint(full, new Color(0, 0, 0, 0f));
        }

        // ---------- настоящий выход ----------
        static void DrawQuit(Rect full, float t, float dur, float k)
        {
            Lag.Draw(FxKind.Crash, t, dur, k, full);
            if (t > dur - 1.2f && !quitDone)
            {
                quitDone = true;
                try { Application.Quit(); } catch (Exception e) { Debug.LogWarning("[PeakFun] выход: " + e.Message); }
            }
        }
    }

    // ======================= звук =======================
    internal static class Sfx
    {
        static AudioSource src; static GameObject host;

        static AudioSource Src()
        {
            if (src != null) return src;
            host = new GameObject("PeakFun_Audio2");
            UnityEngine.Object.DontDestroyOnLoad(host);
            src = host.AddComponent<AudioSource>();
            src.spatialBlend = 0f; src.playOnAwake = false; src.ignoreListenerPause = true;
            return src;
        }

        public static void Stop() { if (src != null) src.Stop(); }

        static AudioClip geiger;
        public static void Geiger()
        {
            try
            {
                if (geiger == null)
                {
                    int rate = 22050, n = 380; var d = new float[n]; var r = new System.Random(3);
                    for (int i = 0; i < n; i++) d[i] = ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-i / 55f) * 0.8f;
                    geiger = AudioClip.Create("pf_geiger", n, 1, rate, false); geiger.SetData(d, 0);
                }
                Src().PlayOneShot(geiger, 0.45f);
            }
            catch { }
        }

        static void Play(AudioClip c, float vol)
        {
            try { var s = Src(); s.Stop(); s.clip = c; s.volume = vol; s.Play(); }
            catch (Exception e) { Debug.LogWarning("[PeakFun] звук: " + e.Message); }
        }

        /// <summary>Звон в ушах после светошумовой: два тона + шипение, затухает.</summary>
        public static void Whine(float len)
        {
            int rate = 22050, n = (int)(rate * (len + 0.4f));
            var d = new float[n]; var r = new System.Random(5);
            double p1 = 0, p2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Pow(Mathf.Clamp01(1f - t / (len + 0.3f)), 1.3f) * Mathf.Clamp01(t / 0.02f);
                p1 += 5300.0 / rate; p2 += 3950.0 / rate;
                float s = Mathf.Sin((float)(p1 * Mathf.PI * 2)) * 0.5f + Mathf.Sin((float)(p2 * Mathf.PI * 2)) * 0.28f;
                s += ((float)r.NextDouble() * 2f - 1f) * 0.12f * env;
                d[i] = s * env * 0.55f;
            }
            var c = AudioClip.Create("pf_whine", n, 1, rate, false); c.SetData(d, 0);
            Play(c, 0.55f);
        }

        /// <summary>Низкий гул землетрясения.</summary>
        public static void Rumble(float len)
        {
            int rate = 22050, n = (int)(rate * (len + 1f));
            var d = new float[n]; var r = new System.Random(9);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((len + 0.6f - t) / 1.0f);
                float noise = (float)r.NextDouble() * 2f - 1f;
                lp += (noise - lp) * 0.03f; lp2 += (lp - lp2) * 0.05f;
                float sub = Mathf.Sin(t * Mathf.PI * 2f * (34f + Mathf.Sin(t * 3f) * 6f)) * 0.5f;
                float crack = (r.NextDouble() < 0.0004 ? ((float)r.NextDouble() * 2f - 1f) : 0f);
                d[i] = (lp2 * 9f + sub + crack * 0.7f) * env * 0.8f;
            }
            var c = AudioClip.Create("pf_rumble", n, 1, rate, false); c.SetData(d, 0);
            Play(c, 0.9f);
        }
    }

    // ======================= тряска камеры =======================
    internal static class CamShake
    {
        public static float Boost, Fov;
        public static Func<bool> ViewActive; public static Action<Transform> ViewApply; public static Func<bool> ExtraActive; public static Action<Transform> ExtraApply;   // третье лицо и кукловод
        static bool installed, applied, fovSet; static Vector3 sp; static Quaternion sr; static Camera cur; static float sfov;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Install()
        {
            if (installed) return;
            installed = true;
            try
            {
                Camera.onPreCull += Pre; Camera.onPostRender += Post;
                RenderPipelineManager.beginCameraRendering += PreSrp; RenderPipelineManager.endCameraRendering += PostSrp;
            }
            catch (Exception e) { Debug.LogWarning("[PeakFun] тряска камеры недоступна: " + e.Message); }
        }

        static void PreSrp(ScriptableRenderContext c, Camera cam) { Pre(cam); }
        static void PostSrp(ScriptableRenderContext c, Camera cam) { Post(cam); }

        static void Pre(Camera cam)
        {
            float lv = Mathf.Max(Hard.QuakeLevel, Boost);
            if (applied || cam == null || cam != Camera.main) return;
            bool view = false;
            try { view = ViewActive != null && ViewActive(); } catch { }
            bool extra = false; try { extra = ExtraActive != null && ExtraActive(); } catch { }
            if (lv <= 0.001f && !view && !extra) return;
            var tr = cam.transform;
            cur = cam; sp = tr.position; sr = tr.rotation; applied = true;
            Fov = 0f;
            if (view) { try { ViewApply(tr); } catch { } }
            if (extra) { try { ExtraApply(tr); } catch { } }
            if (Fov > 1f) { try { sfov = cam.fieldOfView; cam.fieldOfView = Fov; fovSet = true; } catch { } }
            if (lv <= 0.001f) return;
            float t = Time.unscaledTime;
            Vector3 off = new Vector3(Mathf.Sin(t * 47f) + Mathf.Sin(t * 91f) * 0.5f, Mathf.Sin(t * 53f + 1f) + Mathf.Sin(t * 83f) * 0.5f, 0f) * 0.045f * lv;
            tr.position = tr.position + tr.TransformDirection(off);
            tr.rotation = sr * Quaternion.Euler(Mathf.Sin(t * 41f) * 0.7f * lv, Mathf.Sin(t * 37f + 2f) * 0.7f * lv, Mathf.Sin(t * 29f) * 1.6f * lv);
        }

        static void Post(Camera cam)
        {
            if (!applied || cam != cur) return;
            if (cur != null) { cur.transform.position = sp; cur.transform.rotation = sr; if (fovSet) { try { cur.fieldOfView = sfov; } catch { } } }
            fovSet = false;
            applied = false;
        }
    }
}
