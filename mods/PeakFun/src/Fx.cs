// Экранные эффекты-пугалки: огонь, помехи Скаутмастера, «вы погибли», фальшивый кик, лаги, чёрный экран, диско, скример.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PeakFun
{
    internal enum FxKind { Fire, Static, Death, Kick, Lag, Black, Disco, Scream, Freeze, Stutter, Slideshow, Rubber, Disconnect, Bsod, Crash, Glitch, SteamDown, Combo, FakeCrash, WinUpdate, Hack, Flash, Quake, Virus, Quit }

    internal static class Fx
    {
        class Active { public FxKind kind; public float start, dur; }
        static readonly List<Active> list = new List<Active>();

        public static float ScreamVolume = 0.7f;
        public static bool ScreamSound = true;
        /// <summary>Интенсивность помех от «Скаутмастера-друга» (0..1), выставляется снаружи каждый кадр.</summary>
        public static float ProximityStatic;

        public static bool Any { get { return list.Count > 0; } }
        public static bool FireActive { get { foreach (var a in list) if (a.kind == FxKind.Fire) return true; return false; } }

        static float Now { get { return Time.unscaledTime; } }

        public static void Start(FxKind k, float dur)
        {
            if (Hard.IsHard(k)) dur = Hard.Fix(k, dur);
            // один и тот же эффект перезапускаем
            list.RemoveAll(a => a.kind == k);
            list.Add(new Active { kind = k, start = Now, dur = dur });
            if (k == FxKind.Scream) { try { if (ScreamSound) Snd.Play(ScreamVolume); } catch (Exception e) { Debug.LogWarning("[PeakFun] звук недоступен: " + e.Message); } }
            if (k == FxKind.Fire) { Array.Clear(heat, 0, heat.Length); }
            if (Lag.IsLagKind(k)) Lag.OnStart(k, dur);
            if (Hard.IsHard(k)) Hard.OnStart(k, dur);
        }

        public static void Clear() { list.Clear(); try { Snd.Stop(); } catch { } try { Lag.Reset(); } catch { } try { Hard.Reset(); } catch { } try { Shade.Off(); } catch { } }

        // ---------- ресурсы ----------
        static Texture2D noiseTex, fireTex, vignetteTex, screamTex;
        static Color32[] noisePx, firePx;
        const int NW = 96, NH = 54, FW = 64, FH = 40;
        static int[] heat = new int[FW * FH];
        static Color32[] palette;
        static float nextNoise, nextFire;
        static System.Random rnd = new System.Random();
        static bool inited;
        

        static void Init()
        {
            if (inited && noiseTex != null) return;
            inited = true;
            noiseTex = new Texture2D(NW, NH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            noisePx = new Color32[NW * NH];
            fireTex = new Texture2D(FW, FH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            firePx = new Color32[FW * FH];
            palette = new Color32[256];
            for (int i = 0; i < 256; i++)
            {
                float t = i / 255f;
                palette[i] = new Color(Mathf.Min(1f, t * 2.2f), Mathf.Clamp01((t - 0.3f) * 1.9f), Mathf.Clamp01((t - 0.82f) * 5f), Mathf.Clamp01(t * 3f));
            }
            vignetteTex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.15f, d));
                    vignetteTex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            vignetteTex.Apply();

            try { LoadScreamTex(); }
            catch (Exception e) { Debug.LogWarning("[PeakFun] картинка скримера не загрузилась: " + e.Message); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void LoadScreamTex()
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var s = asm.GetManifestResourceStream("scream.jpg"))
            {
                if (s == null) return;
                var bytes = new byte[s.Length]; s.Read(bytes, 0, bytes.Length);
                screamTex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                ImageConversion.LoadImage(screamTex, bytes);
            }
        }

        // ---------- отрисовка ----------
        public static void Draw()
        {
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown && Event.current.type != EventType.MouseUp && Event.current.type != EventType.Layout) return;
            Ui.Init();
            Init();
            float oldAlpha = Ui.Alpha; Ui.Alpha = 1f;
            list.RemoveAll(a => Now - a.start > a.dur);
            var full = new Rect(0, 0, Screen.width, Screen.height);
            float k = Screen.height / 1080f;

            Lag.DrawHold(full, k);
            if (ProximityStatic > 0.01f && Event.current.type == EventType.Repaint) DrawStatic(full, ProximityStatic, k);

            foreach (var a in list.ToArray())
            {
                float t = Now - a.start;
                float fin = Mathf.Clamp01(t / 0.5f), fout = Mathf.Clamp01((a.dur - t) / 0.7f);
                float lvl = Mathf.Min(fin, fout);
                switch (a.kind)
                {
                    case FxKind.Fire: DrawFire(full, t, a.dur, lvl, k); break;
                    case FxKind.Static: DrawStatic(full, lvl, k); break;
                    case FxKind.Death: DrawDeath(full, t, a.dur, k); break;
                    case FxKind.Kick: DrawKick(full, t, a, k); break;
                    case FxKind.Lag: DrawLag(full, t, lvl, k); break;
                    case FxKind.Black: DrawBlack(full, t, a.dur); break;
                    case FxKind.Disco: DrawDisco(full, t, lvl, k); break;
                    case FxKind.Scream: DrawScream(full, t, a.dur, k); break;
                    default: if (Hard.IsHard(a.kind)) Hard.Draw(a.kind, t, a.dur, k, full); else Lag.Draw(a.kind, t, a.dur, k, full); break;
                }
            }
            Ui.Alpha = oldAlpha;
        }

        static void Tint(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white;
        }

        static void Vignette(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = c; GUI.DrawTexture(r, vignetteTex); GUI.color = Color.white;
        }

        static void DrawStatic(Rect full, float lvl, float k)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (Now >= nextNoise)
            {
                nextNoise = Now + 1f / 22f;
                for (int i = 0; i < noisePx.Length; i++)
                {
                    byte v = (byte)rnd.Next(30, 255);
                    noisePx[i] = new Color32(v, v, (byte)Mathf.Min(255, v + 12), 255);
                }
                noiseTex.SetPixels32(noisePx); noiseTex.Apply(false);
            }
            Tint(full, new Color(0.35f, 0.38f, 0.45f, 0.25f * lvl));
            GUI.color = new Color(1, 1, 1, 0.5f * lvl);
            GUI.DrawTexture(full, noiseTex, ScaleMode.StretchToFill);
            GUI.color = Color.white;
            // глитч-полосы
            int bars = (int)(lvl * 4f);
            for (int i = 0; i < bars; i++)
            {
                float y = (float)rnd.NextDouble() * full.height;
                float h = (4f + (float)rnd.NextDouble() * 22f) * k;
                Tint(new Rect(0, y, full.width, h), rnd.Next(2) == 0 ? new Color(1, 1, 1, 0.12f * lvl) : new Color(0, 0, 0, 0.35f * lvl));
            }
            Vignette(full, new Color(0, 0, 0, 0.85f * lvl));
        }

        static void DrawFire(Rect full, float t, float dur, float lvl, float k)
        {
            if (Event.current.type != EventType.Repaint) return;
            // источник огня
            if (Now >= nextFire)
            {
                nextFire = Now + 1f / 30f;
                float src_ = lvl;
                for (int x = 0; x < FW; x++)
                    heat[(FH - 1) * FW + x] = src_ <= 0.01f ? 0 : (int)(255 * src_ * (0.65f + 0.35f * rnd.NextDouble()));
                for (int y = 1; y < FH; y++)
                    for (int x = 0; x < FW; x++)
                    {
                        int s = y * FW + x;
                        int p = heat[s];
                        if (p == 0) { heat[s - FW] = 0; continue; }
                        int r = rnd.Next(4);
                        int dx = Mathf.Clamp(x - r + 1, 0, FW - 1);
                        heat[(y - 1) * FW + dx] = Mathf.Max(0, p - r * 3 - 1);
                    }
                for (int y = 0; y < FH; y++)
                    for (int x = 0; x < FW; x++)
                        firePx[(FH - 1 - y) * FW + x] = palette[Mathf.Clamp(heat[y * FW + x], 0, 255)];
                fireTex.SetPixels32(firePx); fireTex.Apply(false);
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 9f) * (0.6f + 0.4f * Mathf.Sin(t * 3.1f));
            Tint(full, new Color(1f, 0.25f, 0.05f, (0.08f + 0.07f * pulse) * lvl));
            Vignette(full, new Color(1f, 0.3f, 0.05f, (0.55f + 0.25f * pulse) * lvl));
            float fh = full.height * 0.62f;
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(lvl * 1.4f));
            GUI.DrawTexture(new Rect(0, full.height - fh, full.width, fh), fireTex, ScaleMode.StretchToFill);
            GUI.color = Color.white;
            // текст
            float sh = (float)Math.Sin(t * 40f) * 3f * k;
            Ui.Alpha = lvl;
            Ui.Text(new Rect(sh, 70 * k, full.width, 90 * k), "ТЫ ГОРИШЬ!", (int)(64 * k), new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, true);
            Ui.Alpha = 1f;
        }

        static void DrawDeath(Rect full, float t, float dur, float k)
        {
            float f = Mathf.Clamp01(t / 0.7f) * Mathf.Clamp01((dur - t) / 0.8f);
            Tint(full, new Color(0.18f, 0f, 0f, 0.88f * f));
            Vignette(full, new Color(0f, 0f, 0f, 0.9f * f));
            float s = Ui.EaseOutBack(Mathf.Clamp01((t - 0.25f) / 0.6f));
            Ui.Alpha = f;
            int size = (int)(Mathf.Lerp(40f, 96f, s) * k);
            Ui.Text(new Rect(0, full.height * 0.38f, full.width, 120 * k), "ВЫ ПОГИБЛИ", size, new Color(0.85f, 0.1f, 0.12f), TextAnchor.MiddleCenter, true);
            Ui.Text(new Rect(0, full.height * 0.38f + 110 * k, full.width, 40 * k), "Ждите, пока друзья вас воскресят…", (int)(24 * k), new Color(0.8f, 0.8f, 0.85f), TextAnchor.MiddleCenter);
            Ui.Alpha = 1f;
        }

        static void DrawKick(Rect full, float t, Active a, float k)
        {
            float f = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((a.dur - t) / 0.3f);
            Tint(full, new Color(0, 0, 0, 0.62f * f));
            float w = 560 * k, h = 250 * k;
            float sc = Ui.EaseOutBack(t / 0.35f);
            var r = new Rect(full.center.x - w / 2, full.center.y - h / 2 + (1f - sc) * 40 * k, w, h);
            Ui.Alpha = f;
            Ui.Box(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(0.9f, 0.2f, 0.25f, 0.5f), 14 * k);
            Ui.Box(r, new Color(0.09f, 0.1f, 0.15f, 0.98f), 12 * k);
            Ui.Box(new Rect(r.x, r.y, r.width, 48 * k), new Color(0.8f, 0.18f, 0.25f), 12 * k);
            Ui.Text(new Rect(r.x + 18 * k, r.y, r.width, 48 * k), "Ошибка соединения", (int)(22 * k), Color.white, TextAnchor.MiddleLeft, true);
            Ui.TextWrap(new Rect(r.x + 22 * k, r.y + 62 * k, r.width - 44 * k, 100 * k),
                "Вы были отключены от игры.\nПричина: подозрение на читерство (код 403).\nАнтичит PEAK заблокировал ваш аккаунт.", (int)(19 * k), new Color(0.85f, 0.88f, 1f));
            var ok = new Rect(r.xMax - 150 * k, r.yMax - 58 * k, 125 * k, 40 * k);
            if (Ui.ButtonRect(ok, "OK", 2, true, (int)(18 * k))) { Ui.Alpha = 1f; list.RemoveAll(x => x == a); return; }
            Ui.Alpha = 1f;
        }

        static void DrawLag(Rect full, float t, float lvl, float k)
        {
            Tint(full, new Color(0, 0, 0, 0.35f * lvl));
            Ui.Alpha = lvl;
            float w = 360 * k, h = 90 * k;
            var r = new Rect(full.center.x - w / 2, full.height * 0.12f, w, h);
            Ui.Box(r, new Color(0.08f, 0.09f, 0.14f, 0.95f), 14 * k);
            Ui.Text(new Rect(r.x + 90 * k, r.y + 8 * k, r.width, 36 * k), "Плохое соединение", (int)(21 * k), new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleLeft, true);
            int ping = 900 + rnd.Next(0, 4000);
            Ui.Text(new Rect(r.x + 90 * k, r.y + 44 * k, r.width, 30 * k), "Пинг: " + ping + " мс   Потеря пакетов: " + rnd.Next(40, 99) + "%", (int)(14 * k), new Color(0.75f, 0.78f, 0.9f), TextAnchor.MiddleLeft);
            // спиннер
            var c = new Vector2(r.x + 45 * k, r.center.y);
            for (int i = 0; i < 10; i++)
            {
                float ang = (i / 10f) * Mathf.PI * 2f - t * 6f;
                float al = ((i + (int)(t * 10f)) % 10) / 10f;
                float d = 7f * k;
                Ui.Box(new Rect(c.x + Mathf.Cos(ang) * 24 * k - d / 2, c.y + Mathf.Sin(ang) * 24 * k - d / 2, d, d), new Color(1f, 0.7f, 0.2f, 0.2f + 0.8f * al), d / 2);
            }
            Ui.Alpha = 1f;
        }

        static void DrawBlack(Rect full, float t, float dur)
        {
            float a;
            if (t < 1.4f) a = (Mathf.PerlinNoise(t * 14f, 3f) > 0.45f) ? 1f : 0.1f;
            else a = Mathf.Clamp01((dur - t) / 0.6f);
            Tint(full, new Color(0, 0, 0, a));
        }

        static void DrawDisco(Rect full, float t, float lvl, float k)
        {
            Color c = Color.HSVToRGB((t * 0.6f) % 1f, 0.9f, 1f);
            float beat = 0.5f + 0.5f * Mathf.Sin(t * 12f);
            Tint(full, new Color(c.r, c.g, c.b, (0.14f + 0.14f * beat) * lvl));
            Color c2 = Color.HSVToRGB((t * 0.6f + 0.5f) % 1f, 1f, 1f);
            Vignette(full, new Color(c2.r, c2.g, c2.b, 0.6f * lvl));
            Ui.Alpha = lvl;
            Ui.Text(new Rect(0, 60 * k, full.width, 70 * k), "DISCO!", (int)(56 * k * (0.9f + 0.2f * beat)), c, TextAnchor.MiddleCenter, true);
            Ui.Alpha = 1f;
        }

        static void DrawScream(Rect full, float t, float dur, float k)
        {
            if (Event.current.type != EventType.Repaint) return;
            float fade = Mathf.Clamp01((dur - t) / 0.25f);
            Tint(full, new Color(0, 0, 0, fade));
            int step = (int)(t * 30f);
            var rr = new System.Random(step * 7 + 3);
            float zoom = 1.04f + 0.3f * (t / dur);
            float jx = (float)(rr.NextDouble() * 2 - 1) * 14f * k, jy = (float)(rr.NextDouble() * 2 - 1) * 14f * k;
            var img = new Rect(full.center.x - full.width * zoom / 2 + jx, full.center.y - full.height * zoom / 2 + jy, full.width * zoom, full.height * zoom);
            if (screamTex != null)
            {
                GUI.color = new Color(1, 1, 1, fade);
                GUI.DrawTexture(img, screamTex, ScaleMode.ScaleAndCrop);
                GUI.color = Color.white;
            }
            else DrawFallbackFace(img, fade);
            Tint(full, new Color(0.8f, 0f, 0f, (0.18f + 0.12f * (float)rr.NextDouble()) * fade));
            Vignette(full, new Color(0, 0, 0, 0.7f * fade));
            float flash = Mathf.Clamp01(1f - t / 0.14f);
            if (flash > 0) Tint(full, new Color(1, 1, 1, flash));
        }

        static void DrawFallbackFace(Rect img, float fade)
        {
            Ui.Alpha = fade;
            Tint(img, new Color(0.05f, 0.05f, 0.07f, fade));
            float cx = img.center.x, cy = img.center.y, s = img.height;
            Ui.Box(new Rect(cx - s * 0.32f, cy - s * 0.42f, s * 0.64f, s * 0.84f), new Color(0.88f, 0.9f, 0.85f), s * 0.3f);
            Ui.Box(new Rect(cx - s * 0.22f, cy - s * 0.18f, s * 0.15f, s * 0.22f), Color.black, s * 0.1f);
            Ui.Box(new Rect(cx + s * 0.07f, cy - s * 0.18f, s * 0.15f, s * 0.22f), Color.black, s * 0.1f);
            Ui.Box(new Rect(cx - s * 0.2f, cy + s * 0.12f, s * 0.4f, s * 0.18f), Color.black, s * 0.08f);
            Ui.Alpha = 1f;
        }
    }

    // Звук вынесен в отдельный класс, чтобы отсутствие AudioModule не ломало остальной мод.
    internal static class Snd
    {
        static AudioClip clip;
        static AudioSource src;
        static GameObject host;

        public static void Stop() { if (src != null) src.Stop(); }

        public static void Play(float volume)
        {
            try
            {
                if (clip == null) clip = Make();
                if (src == null)
                {
                    host = new GameObject("PeakFun_Audio");
                    UnityEngine.Object.DontDestroyOnLoad(host);
                    src = host.AddComponent<AudioSource>();
                    src.spatialBlend = 0f; src.playOnAwake = false; src.ignoreListenerPause = true;
                }
                src.volume = Mathf.Clamp01(volume);
                src.clip = clip;
                src.Play();
            }
            catch (Exception e) { Debug.LogWarning("[PeakFun] звук: " + e.Message); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static AudioClip Make()
        {
            int rate = 44100; float len = 1.7f; int n = (int)(rate * len);
            var data = new float[n];
            var r = new System.Random(1337);
            double ph1 = 0, ph2 = 0, ph3 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f = Mathf.Lerp(1100f, 260f, Mathf.Clamp01(t / 1.4f)) + Mathf.Sin(t * 70f) * 60f;
                ph1 += f / rate; ph2 += f * 1.51f / rate; ph3 += f * 0.5f / rate;
                float saw1 = (float)(ph1 % 1.0) * 2f - 1f;
                float saw2 = (float)(ph2 % 1.0) * 2f - 1f;
                float sq = ((ph3 % 1.0) < 0.5 ? 1f : -1f);
                float noise = (float)(r.NextDouble() * 2 - 1);
                float env = Mathf.Clamp01(t / 0.01f) * (t < 1.2f ? 1f : Mathf.Clamp01((len - t) / 0.5f));
                float trem = 0.75f + 0.25f * Mathf.Sin(t * 2f * Mathf.PI * 28f);
                float v = (saw1 * 0.5f + saw2 * 0.35f + sq * 0.25f + noise * 0.45f) * env * trem;
                data[i] = (float)Math.Tanh(v * 2.6f) * 0.95f;
            }
            var c = AudioClip.Create("pf_scream", n, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
