// Реалистичные пранк-«лаги»: фриз кадра, рывки, слайд-шоу, откат назад, потеря связи, артефакты видеокарты,
// синий экран, «PEAK перестал работать», уведомление Steam и комбо из всего этого.
// Ничего не ломает на самом деле: всё — картинка поверх игры и временное снижение FPS (восстанавливается само, F8 — сразу).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PeakFun
{
    internal static class Lag
    {
        public static MonoBehaviour Host;
        /// <summary>Откат позиции назад — реализует плагин (у него есть история позиций).</summary>
        public static Action<float> OnRubber;

        static float Now { get { return Time.unscaledTime; } }
        static readonly System.Random rnd = new System.Random();
        static float Rf(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        // ---------- снимок экрана ----------
        static Texture2D snap; static bool capturing; static float snapTime = -1f;
        static float holdUntil, holdStart; static bool glitchHold;
        static float muteUntil;
        static bool audioPaused, prevPause;

        // слайд-шоу (реальное падение FPS)
        static float slideUntil; static int prevFps; static int prevVsync; static bool slideOn;
        // микрофризы
        static float stutUntil, stutNext;

        struct Sched { public float at; public FxKind kind; public float dur; }
        static readonly List<Sched> sched = new List<Sched>();

        public static bool IsLagKind(FxKind k)
        {
            switch (k)
            {
                case FxKind.Freeze: case FxKind.Stutter: case FxKind.Slideshow: case FxKind.Rubber: case FxKind.Disconnect:
                case FxKind.Bsod: case FxKind.Crash: case FxKind.Glitch: case FxKind.SteamDown: case FxKind.Combo: return true;
            }
            return false;
        }

        public static void OnStart(FxKind k, float dur)
        {
            switch (k)
            {
                case FxKind.Freeze: Hold(dur, false); muteUntil = Mathf.Max(muteUntil, Now + dur); break;
                case FxKind.Crash: Hold(dur, false); muteUntil = Mathf.Max(muteUntil, Now + dur); break;
                case FxKind.Glitch: Hold(dur, true); break;
                case FxKind.Bsod: muteUntil = Mathf.Max(muteUntil, Now + dur); break;
                case FxKind.Stutter: stutUntil = Now + dur; stutNext = Now + 0.15f; break;
                case FxKind.Slideshow: StartSlide(Mathf.Min(dur, 12f)); break;
                case FxKind.Rubber: if (OnRubber != null) OnRubber(dur); break;
                case FxKind.Combo:
                    {
                        float d = Mathf.Max(dur, 10f);
                        sched.Add(new Sched { at = Now + 0.05f, kind = FxKind.Stutter, dur = d * 0.5f });
                        sched.Add(new Sched { at = Now + d * 0.12f, kind = FxKind.Rubber, dur = d * 0.5f });
                        sched.Add(new Sched { at = Now + d * 0.42f, kind = FxKind.Slideshow, dur = 2.6f });
                        sched.Add(new Sched { at = Now + d * 0.55f, kind = FxKind.Disconnect, dur = Mathf.Max(3.5f, d * 0.4f) });
                        break;
                    }
            }
        }

        /// <summary>Заморозить кадр на dur секунд (для «жёстких» пранков).</summary>
        public static void HoldFor(float dur) { Hold(dur, false); }
        public static void MuteFor(float dur) { muteUntil = Mathf.Max(muteUntil, Now + dur); }

        public static void Reset()
        {
            holdUntil = 0; muteUntil = 0; stutUntil = 0; slideUntil = 0; sched.Clear();
            Tick();
            if (snap != null && !capturing) { UnityEngine.Object.Destroy(snap); snap = null; }
        }

        // ---------- каждый кадр (из Update плагина) ----------
        public static void Tick()
        {
            float now = Now;
            for (int i = sched.Count - 1; i >= 0; i--)
                if (now >= sched[i].at) { var s = sched[i]; sched.RemoveAt(i); Fx.Start(s.kind, s.dur); }

            if (now < stutUntil && now >= stutNext && now >= holdUntil)
            {
                float hold = Rf(0.12f, 0.5f);
                Hold(hold, false);
                stutNext = now + hold + Rf(0.25f, 1.1f);
            }

            if (slideOn && now > slideUntil) StopSlide();

            bool want = now < holdUntil || now < muteUntil;
            if (want != audioPaused) { SetAudio(want); }

            if (snap != null && !capturing && now > holdUntil + 1f) { UnityEngine.Object.Destroy(snap); snap = null; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void SetAudio(bool pause)
        {
            try
            {
                if (pause) { prevPause = AudioListener.pause; AudioListener.pause = true; audioPaused = true; }
                else { AudioListener.pause = prevPause; audioPaused = false; }
            }
            catch (Exception e) { Debug.LogWarning("[PeakFun] аудио-пауза: " + e.Message); audioPaused = pause; }
        }

        static void StartSlide(float dur)
        {
            try
            {
                if (!slideOn) { prevFps = Application.targetFrameRate; prevVsync = QualitySettings.vSyncCount; }
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = rnd.Next(6, 10);
                slideOn = true;
                slideUntil = Now + dur;
            }
            catch (Exception e) { Debug.LogWarning("[PeakFun] слайд-шоу: " + e.Message); }
        }

        static void StopSlide()
        {
            try { Application.targetFrameRate = prevFps; QualitySettings.vSyncCount = prevVsync; }
            catch { }
            slideOn = false;
        }

        // ---------- кадр-снимок ----------
        static void Hold(float dur, bool glitch)
        {
            holdStart = Now; holdUntil = Now + dur; glitchHold = glitch;
            if (!capturing && Host != null) Host.StartCoroutine(CoSnap());
        }

        static IEnumerator CoSnap()
        {
            capturing = true;
            yield return new WaitForEndOfFrame();
            try
            {
                var t = Grab();
                if (t != null)
                {
                    if (snap != null) UnityEngine.Object.Destroy(snap);
                    snap = t; snapTime = Now;
                }
            }
            catch (Exception e) { Debug.LogWarning("[PeakFun] снимок экрана недоступен: " + e.Message); }
            capturing = false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static Texture2D Grab() { return ScreenCapture.CaptureScreenshotAsTexture(); }

        // ---------- рисование ----------
        static void Tint(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white;
        }

        /// <summary>Замороженный кадр (нижний слой всех эффектов).</summary>
        public static void DrawHold(Rect full, float k)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (snap == null || Now >= holdUntil || snapTime < holdStart - 0.001f) return;
            GUI.color = Color.white;
            GUI.DrawTexture(full, snap, ScaleMode.StretchToFill, false);
            if (!glitchHold) return;

            float W = full.width, H = full.height;
            var rr = new System.Random((int)(Now * 16f) * 977 + 11);
            int strips = 7 + rr.Next(8);
            for (int i = 0; i < strips; i++)
            {
                float y0 = (float)rr.NextDouble() * H, h = (6f + (float)rr.NextDouble() * 70f) * k;
                if (y0 + h > H) h = H - y0;
                float dx = ((float)rr.NextDouble() * 2f - 1f) * (30f + (float)rr.NextDouble() * 260f) * k;
                GUI.DrawTextureWithTexCoords(new Rect(dx, y0, W, h), snap, new Rect(0f, 1f - (y0 + h) / H, 1f, h / H));
            }
            Color[] pal = { new Color(1, 0, 1), new Color(0.2f, 1, 0.2f), new Color(0, 1, 1), Color.white, new Color(1, 0.1f, 0.1f), Color.black };
            int blocks = 4 + rr.Next(8);
            for (int i = 0; i < blocks; i++)
            {
                var c = pal[rr.Next(pal.Length)]; c.a = 0.35f + (float)rr.NextDouble() * 0.5f;
                float bw = (20f + (float)rr.NextDouble() * 320f) * k, bh = (6f + (float)rr.NextDouble() * 90f) * k;
                Tint(new Rect((float)rr.NextDouble() * W, (float)rr.NextDouble() * H, bw, bh), c);
            }
            if (rr.Next(100) < 28)
            {
                int cols = 14 + rr.Next(26);
                for (int i = 0; i < cols; i++)
                {
                    var c = pal[rr.Next(pal.Length - 1)]; c.a = 0.55f;
                    Tint(new Rect((float)rr.NextDouble() * W, 0, (2f + (float)rr.NextDouble() * 9f) * k, H), c);
                }
            }
            if (rr.Next(100) < 20) Tint(full, new Color(0.1f, 1f, 0.3f, 0.16f));
        }

        public static void Draw(FxKind kind, float t, float dur, float k, Rect full)
        {
            switch (kind)
            {
                case FxKind.Freeze: DrawFreeze(full, t, dur, k); break;
                case FxKind.Crash: DrawCrash(full, t, dur, k); break;
                case FxKind.Disconnect: DrawDisconnect(full, t, dur, k); break;
                case FxKind.Bsod: DrawBsod(full, t, dur, k); break;
                case FxKind.SteamDown: DrawSteam(full, t, dur, k); break;
                case FxKind.Stutter: case FxKind.Slideshow: case FxKind.Rubber: case FxKind.Combo: DrawPing(full, t, k); break;
            }
        }

        // ----- мелочи -----
        static void Ring(Vector2 c, float rad, float t, Color col, float k, int dots = 10, float dot = 6f)
        {
            if (Event.current.type != EventType.Repaint) return;
            for (int i = 0; i < dots; i++)
            {
                float ang = (i / (float)dots) * Mathf.PI * 2f - t * 6f;
                float al = ((i + (int)(t * 9f)) % dots) / (float)dots;
                float d = dot * k;
                var cc = col; cc.a = 0.15f + 0.85f * al;
                Ui.Box(new Rect(c.x + Mathf.Cos(ang) * rad * k - d / 2, c.y + Mathf.Sin(ang) * rad * k - d / 2, d, d), cc, d / 2);
            }
        }

        /// <summary>Красный значок «связь нестабильна» в углу — подозрение падает на интернет.</summary>
        static void DrawPing(Rect full, float t, float k)
        {
            if (Event.current.type != EventType.Repaint) return;
            float x = full.width - 190 * k, y = 18 * k;
            Ui.Box(new Rect(x, y, 170 * k, 34 * k), new Color(0, 0, 0, 0.5f), 8 * k);
            int ping = (int)(380 + 900 * Mathf.PerlinNoise(t * 3f, 1.7f) + (rnd.Next(100) < 8 ? 2500 : 0));
            for (int i = 0; i < 4; i++)
            {
                bool on = i < 1 || (i == 1 && (int)(t * 4f) % 3 == 0);
                Ui.Box(new Rect(x + 10 * k + i * 7 * k, y + (24 - i * 5) * k, 5 * k, (4 + i * 5) * k), on ? new Color(1f, 0.3f, 0.25f) : new Color(1, 1, 1, 0.2f), 1);
            }
            Ui.Text(new Rect(x + 46 * k, y, 120 * k, 34 * k), ping + " мс", (int)(17 * k), new Color(1f, 0.45f, 0.4f), TextAnchor.MiddleLeft, true);
        }

        internal static void WinDialog(Rect r, string title, string body, string b1, string b2, float k, float a)
        {
            Ui.Alpha = a;
            Ui.Box(new Rect(r.x - 3 * k, r.y - 3 * k, r.width + 6 * k, r.height + 6 * k), new Color(0, 0, 0, 0.3f), 3 * k);
            Ui.Box(r, new Color(0.94f, 0.94f, 0.94f, 1f), 0f);
            Ui.Box(new Rect(r.x, r.y, r.width, 34 * k), Color.white, 0f);
            Ui.Text(new Rect(r.x + 12 * k, r.y, r.width - 60 * k, 34 * k), title, (int)(15 * k), new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleLeft);
            Ui.Text(new Rect(r.xMax - 44 * k, r.y, 44 * k, 34 * k), "X", (int)(15 * k), new Color(0.1f, 0.1f, 0.1f), TextAnchor.MiddleCenter);
            Ui.TextWrap(new Rect(r.x + 22 * k, r.y + 52 * k, r.width - 44 * k, r.height - 130 * k), body, (int)(16 * k), new Color(0.05f, 0.1f, 0.35f));
            float bh = 32 * k, by = r.yMax - 52 * k;
            if (!string.IsNullOrEmpty(b2))
            {
                float w2 = Mathf.Min(r.width - 40 * k, 330 * k);
                var rb2 = new Rect(r.xMax - 20 * k - w2, by - 40 * k, w2, bh);
                Ui.Box(new Rect(rb2.x - 1, rb2.y - 1, rb2.width + 2, rb2.height + 2), new Color(0.65f, 0.65f, 0.65f), 0f);
                Ui.Box(rb2, new Color(0.88f, 0.88f, 0.88f), 0f);
                Ui.Text(rb2, b2, (int)(14 * k), Color.black, TextAnchor.MiddleCenter);
            }
            if (!string.IsNullOrEmpty(b1))
            {
                float w1 = Mathf.Min(r.width - 40 * k, 330 * k);
                var rb1 = new Rect(r.xMax - 20 * k - w1, by, w1, bh);
                Ui.Box(new Rect(rb1.x - 1, rb1.y - 1, rb1.width + 2, rb1.height + 2), new Color(0.65f, 0.65f, 0.65f), 0f);
                Ui.Box(rb1, new Color(0.88f, 0.88f, 0.88f), 0f);
                Ui.Text(rb1, b1, (int)(14 * k), Color.black, TextAnchor.MiddleCenter);
            }
            Ui.Alpha = 1f;
        }

        // ----- фриз -----
        static void DrawFreeze(Rect full, float t, float dur, float k)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (t > 1.1f) Tint(full, new Color(1f, 1f, 1f, Mathf.Min(0.1f, (t - 1.1f) * 0.1f)));   // окно «выцветает» как у зависшей программы
            if (t > 0.9f) Ring(full.center + new Vector2(0, 0), 14f, t, new Color(0.2f, 0.5f, 1f), k, 8, 5f);
            if (t > 2.6f && dur - t > 0.5f)
            {
                float w = 520 * k, h = 230 * k;
                WinDialog(new Rect(full.center.x - w / 2, full.center.y - h / 2 + 40 * k, w, h), "PEAK",
                    "Программа не отвечает.\nЕсли закрыть программу, можно потерять несохранённые данные.",
                    "Закрыть программу", "Подождать, пока программа отреагирует", k, Mathf.Clamp01((t - 2.6f) / 0.25f));
            }
        }

        // ----- крэш -----
        static void DrawCrash(Rect full, float t, float dur, float k)
        {
            if (Event.current.type != EventType.Repaint) return;
            Tint(full, new Color(1f, 1f, 1f, Mathf.Clamp01(t / 0.5f) * 0.12f));
            if (t < 0.45f) return;
            float w = 560 * k, h = 270 * k;
            var r = new Rect(full.center.x - w / 2, full.center.y - h / 2, w, h);
            WinDialog(r, "PEAK.exe", "Прекращена работа программы «PEAK»\nWindows ищет решение этой проблемы…",
                "Закрыть программу", null, k, Mathf.Clamp01((t - 0.45f) / 0.15f));
            // бегущая полоска
            float px = r.x + 22 * k, py = r.y + 120 * k, pw = r.width - 44 * k;
            Ui.Box(new Rect(px, py, pw, 16 * k), new Color(0.82f, 0.82f, 0.82f), 0f);
            float seg = pw * 0.22f;
            float pos = Mathf.Repeat(t * 0.55f, 1.25f) * (pw + seg) - seg;
            float x0 = Mathf.Max(0, pos), x1 = Mathf.Min(pw, pos + seg);
            if (x1 > x0) Ui.Box(new Rect(px + x0, py, x1 - x0, 16 * k), new Color(0.1f, 0.75f, 0.2f), 0f);
        }

        // ----- потеря соединения -----
        static void DrawDisconnect(Rect full, float t, float dur, float k)
        {
            float f = Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((dur - t) / 0.4f);
            Tint(full, new Color(0, 0, 0, 0.5f * f));
            float restoreAt = Mathf.Max(dur * 0.7f, dur - 1.6f);
            bool ok = t >= restoreAt;
            float w = 640 * k, h = 230 * k;
            var r = new Rect(full.center.x - w / 2, full.center.y - h / 2 - 40 * k, w, h);
            Ui.Alpha = f;
            Ui.Box(new Rect(r.x - 3 * k, r.y - 3 * k, r.width + 6 * k, r.height + 6 * k), ok ? new Color(0.2f, 0.8f, 0.4f, 0.5f) : new Color(0.95f, 0.25f, 0.2f, 0.5f), 16 * k);
            Ui.Box(r, new Color(0.08f, 0.09f, 0.13f, 0.98f), 14 * k);
            if (!ok)
            {
                Ui.Text(new Rect(r.x + 100 * k, r.y + 22 * k, r.width - 120 * k, 40 * k), "Соединение потеряно", (int)(28 * k), new Color(1f, 0.4f, 0.35f), TextAnchor.MiddleLeft, true);
                int tries = 1 + Mathf.Min(4, (int)(t / (restoreAt / 5f)));
                string dots = new string('.', 1 + (int)(t * 2.5f) % 3);
                Ui.Text(new Rect(r.x + 100 * k, r.y + 74 * k, r.width - 120 * k, 30 * k), "Нет ответа от сервера (ClientTimeout)", (int)(19 * k), new Color(0.82f, 0.86f, 1f), TextAnchor.MiddleLeft);
                Ui.Text(new Rect(r.x + 100 * k, r.y + 106 * k, r.width - 120 * k, 30 * k), "Переподключение, попытка " + tries + " из 5" + dots, (int)(19 * k), new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleLeft);
                Ui.Text(new Rect(r.x + 100 * k, r.y + 150 * k, r.width - 120 * k, 30 * k), "Не закрывайте игру. Потеря пакетов: " + (60 + (int)(t * 7f) % 39) + "%", (int)(15 * k), Ui.Muted, TextAnchor.MiddleLeft);
                Ring(new Vector2(r.x + 52 * k, r.center.y), 22f, t, new Color(1f, 0.6f, 0.2f), k);
            }
            else
            {
                Ui.Text(new Rect(r.x + 100 * k, r.y + 50 * k, r.width - 120 * k, 50 * k), "Соединение восстановлено", (int)(30 * k), new Color(0.35f, 0.95f, 0.55f), TextAnchor.MiddleLeft, true);
                Ui.Text(new Rect(r.x + 100 * k, r.y + 112 * k, r.width - 120 * k, 30 * k), "Синхронизация с лобби…", (int)(19 * k), new Color(0.82f, 0.86f, 1f), TextAnchor.MiddleLeft);
                Ui.Box(new Rect(r.x + 30 * k, r.y + 70 * k, 44 * k, 44 * k), new Color(0.2f, 0.8f, 0.4f), 22 * k);
                Ui.Text(new Rect(r.x + 30 * k, r.y + 70 * k, 44 * k, 44 * k), "OK", (int)(18 * k), Color.white, TextAnchor.MiddleCenter, true);
            }
            Ui.Alpha = 1f;
            DrawPing(full, t, k);
        }

        // ----- синий экран -----
        static bool[,] qr;
        static void DrawBsod(Rect full, float t, float dur, float k)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (t < 0.08f) return;
            Tint(full, new Color(0f, 0.47f, 0.84f, 1f));
            float x = full.width * 0.1f, y = full.height * 0.14f;
            Ui.Text(new Rect(x, y, 300 * k, 190 * k), ":(", (int)(150 * k), Color.white, TextAnchor.MiddleLeft);
            float tw = full.width * 0.62f;
            Ui.TextWrap(new Rect(x, y + 200 * k, tw, 140 * k),
                "На вашем ПК возникла проблема, и его необходимо перезагрузить. Мы лишь собираем некоторые сведения об ошибке, а затем вы сможете перезагрузить компьютер.", (int)(30 * k), Color.white);
            float p = Mathf.Clamp01(t / Mathf.Max(1f, dur * 0.88f));
            int pct = Mathf.FloorToInt(100f * (p < 0.5f ? p * 0.6f : 0.3f + (p - 0.5f) * 1.4f));
            Ui.Text(new Rect(x, y + 330 * k, tw, 40 * k), Mathf.Clamp(pct, 0, 100) + "% завершено", (int)(30 * k), Color.white, TextAnchor.MiddleLeft);
            if (qr == null)
            {
                qr = new bool[21, 21];
                var q = new System.Random(42);
                for (int i = 0; i < 21; i++) for (int j = 0; j < 21; j++) qr[i, j] = q.Next(2) == 0;
                for (int c = 0; c < 3; c++)
                {
                    int ox = c == 1 ? 14 : 0, oy = c == 2 ? 14 : 0;
                    for (int i = 0; i < 7; i++) for (int j = 0; j < 7; j++)
                        qr[ox + i, oy + j] = (i == 0 || j == 0 || i == 6 || j == 6) || (i >= 2 && i <= 4 && j >= 2 && j <= 4);
                }
            }
            float cell = 5.6f * k, qx = x, qy = y + 390 * k;
            Tint(new Rect(qx - 6 * k, qy - 6 * k, cell * 21 + 12 * k, cell * 21 + 12 * k), Color.white);
            for (int i = 0; i < 21; i++) for (int j = 0; j < 21; j++)
                    if (qr[i, j]) Tint(new Rect(qx + i * cell, qy + j * cell, cell + 0.5f, cell + 0.5f), Color.black);
            float tx = qx + cell * 21 + 30 * k;
            Ui.TextWrap(new Rect(tx, qy - 4 * k, full.width * 0.5f, 60 * k), "Дополнительные сведения об этой проблеме и возможных способах ее устранения см. на веб-сайте https://www.windows.com/stopcode", (int)(15 * k), Color.white);
            Ui.TextWrap(new Rect(tx, qy + 56 * k, full.width * 0.5f, 60 * k), "Если вы позвоните в службу поддержки, сообщите им следующие сведения:\nКод остановки: CRITICAL_PROCESS_DIED", (int)(15 * k), Color.white);
        }

        // ----- Steam -----
        static void DrawSteam(Rect full, float t, float dur, float k)
        {
            float a = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((dur - t) / 0.4f);
            float w = 360 * k, h = 96 * k;
            float slide = (1f - Ui.EaseOut(Mathf.Clamp01(t / 0.4f))) * (w + 40 * k);
            var r = new Rect(full.width - w - 24 * k + slide, full.height - h - 70 * k, w, h);
            Ui.Alpha = Mathf.Max(a, 0.001f);
            Ui.Box(r, new Color(0.09f, 0.1f, 0.13f, 0.97f), 4 * k);
            Ui.Box(new Rect(r.x, r.y, 4 * k, r.height), new Color(0.4f, 0.75f, 1f), 0f);
            Ui.Box(new Rect(r.x + 16 * k, r.y + 22 * k, 52 * k, 52 * k), new Color(0.1f, 0.35f, 0.6f), 26 * k);
            Ui.Text(new Rect(r.x + 16 * k, r.y + 22 * k, 52 * k, 52 * k), "S", (int)(26 * k), Color.white, TextAnchor.MiddleCenter, true);
            Ui.Text(new Rect(r.x + 82 * k, r.y + 10 * k, w - 90 * k, 30 * k), "Steam", (int)(19 * k), Color.white, TextAnchor.MiddleLeft, true);
            Ui.TextWrap(new Rect(r.x + 82 * k, r.y + 38 * k, w - 96 * k, 56 * k), "Нет соединения с сетью Steam. Игра перешла в автономный режим.", (int)(14 * k), new Color(0.78f, 0.82f, 0.9f));
            Ui.Alpha = 1f;
            if (t > 1.5f) DrawPing(full, t, k);
        }
    }
}
