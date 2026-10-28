// Шейдеры 1.5 — НАСТОЯЩАЯ пост-обработка на видеокарте.
// PEAK рисует через URP (Universal Render Pipeline) — это подтверждено по готовым модам для игры (PeakFilter, NoMoreMotionBlur):
// у камеры есть Volume-профили с Bloom / Motion Blur и т.д. Мы создаём свой глобальный Volume с повышенным приоритетом и включаем
// в нём штатные эффекты URP (Bloom, цветокоррекция, хроматическая аберрация, плёночное зерно, виньетка, дисторсия линзы,
// глубина резкости, баланс белого, раздельное тонирование) — они считаются штатными шейдерами игры на GPU.
// Для цветовых «пресетов» (негатив, тепловизор, Game Boy, ночное видение…) мы генерируем собственную 3D-таблицу цвета (LUT)
// и отдаём её штатному эффекту «Color Lookup» — то есть любое преобразование цвета делает GPU, а не процессор.
// Всё через рефлексию: мод по-прежнему собирается без DLL игры. Ничего не рисуется поверх экрана, HUD игры остаётся родным.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal enum ShadeKind
    {
        None,
        // кино и цвет
        Film, Noir, Sunset, Cyber, Dream, Night, Thermal, GameBoy, Poster, Negative,
        // искажения и безумие
        Vhs, Glitch, Drunk, Acid, Fisheye, Water, Tunnel,
        // страшное
        Rage, Radiation, Horror,
        // эффекты героев (короткие вспышки)
        Speed, Laser, Power,
        // камера-кассета Backrooms
        Camera
    }

    internal static class Shade
    {
        public static ShadeKind Cur = ShadeKind.None;
        public static float Boost;                   // внешний «накал» помех (монстр рядом) 0..1
        public static float Strength = 1f;           // «сила» — множитель веса Volume
        public static string Status = "не запускалось";
        public static bool Ready;                    // Volume создан и рабочий
        static float start, until;
        static float Now { get { return Time.unscaledTime; } }
        static readonly System.Random rnd = new System.Random();

        public static bool Active { get { return Cur != ShadeKind.None && Now < until; } }

        public static string RuName(ShadeKind k)
        {
            switch (k)
            {
                case ShadeKind.Film: return "Старая плёнка"; case ShadeKind.Noir: return "Нуар (ч/б кино)"; case ShadeKind.Sunset: return "Золотой закат";
                case ShadeKind.Cyber: return "Киберпанк"; case ShadeKind.Dream: return "Сон"; case ShadeKind.Night: return "Ночное видение";
                case ShadeKind.Thermal: return "Тепловизор"; case ShadeKind.GameBoy: return "Game Boy"; case ShadeKind.Poster: return "Постер / комикс";
                case ShadeKind.Negative: return "Негатив"; case ShadeKind.Vhs: return "VHS-кассета"; case ShadeKind.Glitch: return "Глитч";
                case ShadeKind.Drunk: return "Пьяный"; case ShadeKind.Acid: return "Кислота (радуга)"; case ShadeKind.Fisheye: return "Рыбий глаз";
                case ShadeKind.Water: return "Под водой"; case ShadeKind.Tunnel: return "Паника (туннель)"; case ShadeKind.Rage: return "Ярость";
                case ShadeKind.Radiation: return "Радиация"; case ShadeKind.Horror: return "Хоррор";
                case ShadeKind.Camera: return "Камера-кассета"; case ShadeKind.Speed: return "Сверхскорость"; case ShadeKind.Laser: return "Лазер"; case ShadeKind.Power: return "Удар силы";
            }
            return "—";
        }

        // ============================ сборка Volume ============================
        static readonly string[] CompNames = { "Bloom", "ColorAdjustments", "ChromaticAberration", "FilmGrain", "Vignette", "LensDistortion",
                                               "DepthOfField", "WhiteBalance", "SplitToning", "ColorLookup", "MotionBlur" };
        static GameObject go; static Component vol; static object profile;
        static readonly Dictionary<string, object> comps = new Dictionary<string, object>();
        static readonly HashSet<string> used = new HashSet<string>();
        static float curW; static float nextCamCheck; static int volLayer; static bool triedBuild;
        static int lutSize = 32;

        static bool Build()
        {
            if (Ready && go != null) return true;
            if (triedBuild && go == null && Status.StartsWith("нет")) return false;
            triedBuild = true;
            try
            {
                var volT = R.FindType("UnityEngine.Rendering.Volume");
                if (volT == null) { Status = "нет URP: тип Volume не найден — игра рисует не через URP"; return false; }
                go = new GameObject("PeakBR_ShaderVolume");
                go.SetActive(false);               // компонент добавляем в «спящем» объекте — Volume зарегистрируется уже с нужным слоем и приоритетом
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                volLayer = PickLayer();
                go.layer = volLayer;
                vol = go.AddComponent(volT);
                R.Set(vol, "isGlobal", true);
                R.Set(vol, "priority", 1000f);
                R.Set(vol, "weight", 0f);
                profile = R.Get(vol, "profile");
                if (profile == null)
                {
                    var pt = R.FindType("UnityEngine.Rendering.VolumeProfile");
                    var so = pt != null ? ScriptableObject.CreateInstance(pt) : null;
                    if (so == null) { Status = "нет URP: не удалось создать VolumeProfile"; Cleanup(); return false; }
                    R.Set(vol, "sharedProfile", so); profile = so;
                }
                int ok = 0; var missing = new List<string>();
                foreach (var n in CompNames)
                {
                    var t = R.FindType("UnityEngine.Rendering.Universal." + n);
                    object c = null;
                    if (t != null) { try { R.TryInvoke(profile, "Add", out c, t, false); } catch (Exception e) { Debug.LogWarning("[PeakFun] Volume.Add " + n + ": " + e.Message); } }
                    if (c != null) { comps[n] = c; ok++; R.Set(c, "active", false); } else missing.Add(n);
                }
                if (ok == 0) { Status = "нет URP-эффектов в игре (ни один компонент не добавился)"; Cleanup(); return false; }
                lutSize = 32;
                try
                {
                    var gs = R.FindType("UnityEngine.Rendering.GraphicsSettings");
                    var pipe = gs != null ? (R.GetStatic(gs, "currentRenderPipeline") ?? R.GetStatic(gs, "renderPipelineAsset")) : null;
                    int sz = R.Get<int>(pipe, "colorGradingLutSize", 32);
                    if (sz >= 8 && sz <= 65) lutSize = sz;
                }
                catch { }
                go.SetActive(false);
                Ready = true;
                Status = "URP Volume готов: эффектов " + ok + "/" + CompNames.Length + (missing.Count > 0 ? " (нет: " + string.Join(", ", missing.ToArray()) + ")" : "") + ", слой " + volLayer + ", LUT " + lutSize;
                Debug.Log("[PeakFun] Шейдеры: " + Status);
                return true;
            }
            catch (Exception e)
            {
                Status = "ошибка запуска: " + e.GetType().Name + ": " + e.Message;
                Debug.LogWarning("[PeakFun] Шейдеры: " + e);
                Cleanup();
                return false;
            }
        }

        static void Cleanup()
        {
            try { if (go != null) UnityEngine.Object.Destroy(go); } catch { }
            go = null; vol = null; profile = null; comps.Clear(); Ready = false;
        }

        static int PickLayer()
        {
            try
            {
                var cam = Camera.main; if (cam == null) return 0;
                var cd = cam.GetComponent("UniversalAdditionalCameraData");
                if (cd == null) return 0;
                var m = R.Get(cd, "volumeLayerMask");
                if (m is LayerMask lm)
                {
                    if ((lm.value & 1) != 0) return 0;
                    for (int i = 0; i < 32; i++) if ((lm.value & (1 << i)) != 0) return i;
                }
            }
            catch { }
            return 0;
        }

        static void EnsureCamera()
        {
            if (Now < nextCamCheck) return; nextCamCheck = Now + 1f;
            try
            {
                var cam = Camera.main; if (cam == null) return;
                var cd = cam.GetComponent("UniversalAdditionalCameraData");
                if (cd == null) return;
                if (!R.Get<bool>(cd, "renderPostProcessing", true)) R.Set(cd, "renderPostProcessing", true);
                var m = R.Get(cd, "volumeLayerMask");
                if (m is LayerMask lm && go != null && (lm.value & (1 << go.layer)) == 0) go.layer = PickLayer();
            }
            catch { }
        }

        // ============================ управление ============================
        public static void Set(ShadeKind k, float dur)
        {
            if (k == ShadeKind.None) { Off(); return; }
            if (!Build()) { Ui.Toast("Шейдеры недоступны: " + Status, Ui.Bad); Cur = ShadeKind.None; return; }
            Cur = k; start = Now; until = dur <= 0f ? Now + 86400f : Now + dur;
            ResetOverrides();
            curW *= 0.4f;
            if (go != null && !go.activeSelf) go.SetActive(true);
        }

        public static void Off() { Cur = ShadeKind.None; }

        static void ResetOverrides()
        {
            used.Clear();
            foreach (var kv in comps)
            {
                R.Set(kv.Value, "active", false);
                var ps = R.Get(kv.Value, "parameters") as IEnumerable;
                if (ps != null) foreach (var p in ps) R.Set(p, "overrideState", false);
            }
        }

        static void P(string comp, string field, object v)
        {
            object c; if (!comps.TryGetValue(comp, out c)) return;
            if (used.Add(comp)) R.Set(c, "active", true);
            var prm = R.Get(c, field); if (prm == null) return;
            R.Set(prm, "overrideState", true);
            R.Set(prm, "value", v);
        }

        // ============================ тик (из Update плагина) ============================
        static float nextClick, nextGlitch, glitchUntil, gHue, gChroma, gLens, gX = 1f, gY = 1f, nextFlick, flick;
        static readonly Dictionary<string, Texture2D> lutCache = new Dictionary<string, Texture2D>();

        public static void Tick()
        {
            if (!Ready && Cur == ShadeKind.None) return;
            if (Cur != ShadeKind.None && Now >= until) Off();
            try
            {
                if (go == null) { if (Cur != ShadeKind.None) Off(); Ready = false; return; }
                EnsureCamera();
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                float target = 0f;
                if (Cur != ShadeKind.None)
                {
                    float fin = Mathf.Clamp01((Now - start) / 1.2f), fout = Mathf.Clamp01((until - Now) / 1.2f);
                    target = Mathf.Clamp01(Strength) * Mathf.Min(fin, fout);
                }
                curW = Mathf.MoveTowards(curW, target, dt * 1.6f);
                R.Set(vol, "weight", curW);
                if (Cur == ShadeKind.None)
                {
                    if (curW <= 0.001f && go.activeSelf) { ResetOverrides(); go.SetActive(false); }
                    return;
                }
                Apply(Cur, Now - start);
                if (Cur == ShadeKind.Radiation && Now >= nextClick)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Now * 0.8f);
                    nextClick = Now + Mathf.Lerp(0.35f, 0.04f, pulse) * (0.4f + (float)rnd.NextDouble());

                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PeakFun] шейдер отключён: " + e);
                Ui.Toast("Эффект «" + RuName(Cur) + "» не заработал и выключен: " + e.GetType().Name, Ui.Bad);
                Cur = ShadeKind.None; curW = 0f;
                try { ResetOverrides(); if (go != null) go.SetActive(false); } catch { }
            }
        }

        public static void Draw(Rect full, float k) { }   // раньше рисовало оверлеи; теперь всё делает GPU через Volume

        static float Rnd(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        static float Sin(float t, float spd) { return Mathf.Sin(t * spd); }

        static void Lut(ShadeKind k, float contribution)
        {
            string key = k + "/" + lutSize;
            Texture2D tex;
            if (!lutCache.TryGetValue(key, out tex) || tex == null) { tex = BuildLut(k, lutSize); lutCache[key] = tex; }
            P("ColorLookup", "texture", tex);
            P("ColorLookup", "contribution", contribution);
        }

        // ============================ пресеты ============================
        static void Apply(ShadeKind k, float t)
        {
            switch (k)
            {
                case ShadeKind.Film:
                    if (Now >= nextFlick) { nextFlick = Now + Rnd(0.05f, 0.14f); flick = Rnd(-0.18f, 0.12f); }
                    Lut(k, 1f);
                    P("FilmGrain", "intensity", 1f); P("FilmGrain", "response", 0.35f);
                    P("Vignette", "intensity", 0.45f); P("Vignette", "smoothness", 0.5f);
                    P("ColorAdjustments", "contrast", 22f); P("ColorAdjustments", "postExposure", flick);
                    P("ChromaticAberration", "intensity", 0.12f);
                    break;
                case ShadeKind.Noir:
                    Lut(k, 1f);
                    P("FilmGrain", "intensity", 0.75f); P("FilmGrain", "response", 0.5f);
                    P("Vignette", "intensity", 0.6f); P("Vignette", "smoothness", 0.45f);
                    P("ColorAdjustments", "contrast", 30f);
                    break;
                case ShadeKind.Sunset:
                    P("WhiteBalance", "temperature", 42f); P("WhiteBalance", "tint", 12f);
                    P("SplitToning", "shadows", new Color(0.35f, 0.25f, 0.6f)); P("SplitToning", "highlights", new Color(1f, 0.65f, 0.3f)); P("SplitToning", "balance", 10f);
                    P("Bloom", "intensity", 1.6f); P("Bloom", "threshold", 0.8f); P("Bloom", "scatter", 0.75f);
                    P("ColorAdjustments", "saturation", 25f); P("ColorAdjustments", "contrast", 8f);
                    P("Vignette", "intensity", 0.25f);
                    break;
                case ShadeKind.Cyber:
                    Lut(k, 0.9f);
                    P("Bloom", "intensity", 4f); P("Bloom", "threshold", 0.7f); P("Bloom", "scatter", 0.8f); P("Bloom", "tint", new Color(1f, 0.5f, 1f));
                    P("ChromaticAberration", "intensity", 0.45f + 0.1f * Sin(t, 3f));
                    P("Vignette", "intensity", 0.3f);
                    P("ColorAdjustments", "contrast", 20f); P("ColorAdjustments", "saturation", 25f);
                    break;
                case ShadeKind.Dream:
                    Lut(k, 0.85f);
                    P("Bloom", "intensity", 6f); P("Bloom", "threshold", 0.4f); P("Bloom", "scatter", 1f);
                    P("Vignette", "intensity", 0.35f); P("Vignette", "smoothness", 1f); P("Vignette", "color", new Color(1f, 0.85f, 0.95f));
                    P("LensDistortion", "intensity", 0.08f * Sin(t, 0.8f));
                    P("DepthOfField", "mode", 1); P("DepthOfField", "gaussianStart", 6f); P("DepthOfField", "gaussianEnd", 30f); P("DepthOfField", "gaussianMaxRadius", 1.2f);
                    P("WhiteBalance", "temperature", 14f);
                    break;
                case ShadeKind.Night:
                    Lut(k, 1f);
                    P("Bloom", "intensity", 3f); P("Bloom", "threshold", 0.5f);
                    P("FilmGrain", "intensity", 0.7f); P("FilmGrain", "response", 0.6f);
                    P("Vignette", "intensity", 0.55f); P("Vignette", "rounded", true); P("Vignette", "smoothness", 0.4f);
                    P("ColorAdjustments", "postExposure", 1.3f); P("ChromaticAberration", "intensity", 0.12f);
                    break;
                case ShadeKind.Thermal:
                    Lut(k, 1f);
                    P("Bloom", "intensity", 1.2f); P("Bloom", "threshold", 0.8f);
                    P("FilmGrain", "intensity", 0.3f); P("Vignette", "intensity", 0.25f);
                    break;
                case ShadeKind.GameBoy:
                    Lut(k, 1f);
                    P("FilmGrain", "intensity", 0.25f); P("Vignette", "intensity", 0.2f); P("ColorAdjustments", "contrast", 12f);
                    break;
                case ShadeKind.Poster:
                    Lut(k, 1f);
                    P("ColorAdjustments", "saturation", 30f); P("Bloom", "intensity", 0.5f); P("Bloom", "threshold", 1f);
                    P("Vignette", "intensity", 0.15f);
                    break;
                case ShadeKind.Negative:
                    Lut(k, 1f);
                    break;
                case ShadeKind.Vhs:
                {
                    if (Now >= nextGlitch) { nextGlitch = Now + Rnd(2.5f, 6f); glitchUntil = Now + Rnd(0.12f, 0.3f); }
                    bool g = Now < glitchUntil;
                    Lut(k, 0.7f);
                    P("ChromaticAberration", "intensity", g ? 1f : 0.5f + 0.2f * Sin(t, 6f));
                    P("LensDistortion", "intensity", g ? Rnd(-0.35f, 0.35f) : 0.06f * Sin(t, 0.9f));
                    P("FilmGrain", "intensity", 0.6f); P("FilmGrain", "response", 0.5f);
                    P("ColorAdjustments", "saturation", -14f); P("ColorAdjustments", "contrast", 12f);
                    P("Bloom", "intensity", 0.7f); P("Bloom", "threshold", 0.9f);
                    P("Vignette", "intensity", 0.3f);
                    break;
                }
                case ShadeKind.Camera:
                {
                    float b = Boost; if (Now >= nextGlitch) { nextGlitch = Now + Rnd(3f, 8f) * (1.1f - 0.75f * b); glitchUntil = Now + Rnd(0.1f, 0.28f); }
                    bool g = Now < glitchUntil || (b > 0.65f && rnd.NextDouble() < 0.25);
                    Lut(ShadeKind.Vhs, 0.55f);
                    P("ChromaticAberration", "intensity", Mathf.Clamp01(0.42f + 0.3f * b + (g ? 0.5f : 0f)));
                    P("LensDistortion", "intensity", g ? Rnd(-0.32f, 0.32f) : -0.14f + 0.03f * Sin(t, 0.7f));
                    P("FilmGrain", "intensity", Mathf.Clamp01(0.7f + 0.3f * b)); P("FilmGrain", "response", 0.4f);
                    P("ColorAdjustments", "saturation", -20f - 28f * b); P("ColorAdjustments", "contrast", 14f);
                    P("Bloom", "intensity", 0.9f); P("Bloom", "threshold", 0.85f);
                    P("Vignette", "intensity", 0.38f + 0.25f * b);
                    break;
                }
                case ShadeKind.Glitch:
                    if (Now >= nextGlitch)
                    {
                        nextGlitch = Now + Rnd(0.08f, 0.5f);
                        gChroma = Rnd(0f, 1f); gLens = Rnd(-0.45f, 0.45f); gX = Rnd(0.3f, 1f); gY = Rnd(0.3f, 1f);
                        gHue = rnd.NextDouble() < 0.35 ? Rnd(-120f, 120f) : 0f;
                    }
                    P("ChromaticAberration", "intensity", gChroma);
                    P("LensDistortion", "intensity", gLens); P("LensDistortion", "xMultiplier", gX); P("LensDistortion", "yMultiplier", gY);
                    P("ColorAdjustments", "hueShift", gHue); P("ColorAdjustments", "saturation", gHue != 0f ? 40f : 0f);
                    P("FilmGrain", "intensity", 0.6f);
                    break;
                case ShadeKind.Drunk:
                    P("LensDistortion", "intensity", 0.4f * Sin(t, 0.9f) + 0.1f * Sin(t, 2.3f));
                    P("LensDistortion", "center", new Vector2(0.5f + 0.18f * Sin(t, 0.7f), 0.5f + 0.14f * Mathf.Cos(t * 0.9f)));
                    P("LensDistortion", "scale", 0.92f + 0.08f * Sin(t, 1.1f));
                    P("ChromaticAberration", "intensity", 0.4f + 0.3f * Sin(t, 1.6f));
                    P("DepthOfField", "mode", 1); P("DepthOfField", "gaussianStart", 0f); P("DepthOfField", "gaussianEnd", 7f + 3f * Sin(t, 0.8f)); P("DepthOfField", "gaussianMaxRadius", 1.6f);
                    P("MotionBlur", "intensity", 1f);
                    P("WhiteBalance", "tint", 25f * Sin(t, 0.6f)); P("ColorAdjustments", "hueShift", 18f * Sin(t, 0.5f));
                    P("Vignette", "intensity", 0.35f);
                    break;
                case ShadeKind.Acid:
                    P("ColorAdjustments", "hueShift", Mathf.Repeat(t * 80f, 360f) - 180f); P("ColorAdjustments", "saturation", 70f); P("ColorAdjustments", "contrast", 15f);
                    P("Bloom", "intensity", 3f); P("Bloom", "threshold", 0.6f); P("Bloom", "scatter", 0.8f);
                    P("ChromaticAberration", "intensity", 0.4f + 0.2f * Sin(t, 2f));
                    P("LensDistortion", "intensity", 0.15f * Sin(t, 2f));
                    break;
                case ShadeKind.Fisheye:
                    P("LensDistortion", "intensity", 0.85f); P("LensDistortion", "scale", 0.75f);
                    P("ChromaticAberration", "intensity", 0.35f); P("Vignette", "intensity", 0.3f);
                    break;
                case ShadeKind.Water:
                    Lut(k, 1f);
                    P("LensDistortion", "intensity", 0.22f * Sin(t, 1.3f)); P("LensDistortion", "center", new Vector2(0.5f + 0.06f * Sin(t, 0.9f), 0.5f));
                    P("ChromaticAberration", "intensity", 0.3f);
                    P("DepthOfField", "mode", 1); P("DepthOfField", "gaussianStart", 2f); P("DepthOfField", "gaussianEnd", 16f); P("DepthOfField", "gaussianMaxRadius", 1.6f);
                    P("Bloom", "intensity", 1.2f); P("Vignette", "intensity", 0.35f); P("Vignette", "color", new Color(0f, 0.1f, 0.2f));
                    P("ColorAdjustments", "postExposure", -0.3f);
                    break;
                case ShadeKind.Tunnel:
                {
                    float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 7f)), 3f);
                    P("Vignette", "intensity", 0.62f + 0.14f * beat); P("Vignette", "smoothness", 0.35f); P("Vignette", "rounded", true);
                    P("DepthOfField", "mode", 1); P("DepthOfField", "gaussianStart", 0f); P("DepthOfField", "gaussianEnd", 12f); P("DepthOfField", "gaussianMaxRadius", 1.2f);
                    P("ChromaticAberration", "intensity", 0.2f + 0.15f * beat);
                    P("ColorAdjustments", "saturation", -25f);
                    break;
                }
                case ShadeKind.Rage:
                {
                    float p = 0.5f + 0.5f * Sin(t, 6f);
                    Lut(k, 0.85f);
                    P("Vignette", "color", new Color(0.7f, 0f, 0f)); P("Vignette", "intensity", 0.45f + 0.2f * p); P("Vignette", "smoothness", 0.5f);
                    P("ChromaticAberration", "intensity", 0.3f + 0.2f * p);
                    P("ColorAdjustments", "contrast", 30f); P("ColorAdjustments", "saturation", 20f);
                    P("Bloom", "intensity", 0.9f); P("LensDistortion", "intensity", -0.12f * p);
                    break;
                }
                case ShadeKind.Radiation:
                {
                    float pulse = 0.5f + 0.5f * Sin(Now, 0.8f);
                    Lut(k, 1f);
                    P("FilmGrain", "intensity", 0.9f); P("FilmGrain", "response", 0.4f);
                    P("ChromaticAberration", "intensity", 0.25f + 0.3f * pulse);
                    P("Bloom", "intensity", 2f); P("Bloom", "threshold", 0.7f);
                    P("Vignette", "color", new Color(0.1f, 0.3f, 0f)); P("Vignette", "intensity", 0.4f);
                    break;
                }
                case ShadeKind.Speed:
                    P("ChromaticAberration", "intensity", 0.8f); P("LensDistortion", "intensity", -0.22f); P("MotionBlur", "intensity", 1f);
                    P("Vignette", "intensity", 0.35f); P("ColorAdjustments", "contrast", 12f);
                    break;
                case ShadeKind.Laser:
                    P("Vignette", "color", new Color(0.8f, 0f, 0f)); P("Vignette", "intensity", 0.35f); P("Bloom", "intensity", 3f); P("Bloom", "threshold", 0.6f);
                    P("ColorAdjustments", "colorFilter", new Color(1f, 0.85f, 0.85f)); P("ChromaticAberration", "intensity", 0.25f);
                    break;
                case ShadeKind.Power:
                    P("Bloom", "intensity", 6f); P("Bloom", "threshold", 0.5f); P("ChromaticAberration", "intensity", 0.9f);
                    P("LensDistortion", "intensity", 0.3f * Mathf.Exp(-t * 4f)); P("ColorAdjustments", "postExposure", 1.2f * Mathf.Exp(-t * 3f));
                    break;
                case ShadeKind.Horror:
                {
                    if (Now >= nextFlick) { nextFlick = Now + Rnd(0.05f, 1.6f); flick = rnd.NextDouble() < 0.18 ? Rnd(-1.4f, 0.4f) : 0f; }
                    P("ColorAdjustments", "saturation", -60f); P("ColorAdjustments", "postExposure", -0.8f + flick); P("ColorAdjustments", "contrast", 35f);
                    P("ColorAdjustments", "colorFilter", new Color(1f, 0.82f, 0.82f));
                    P("Vignette", "intensity", 0.62f); P("Vignette", "smoothness", 0.6f);
                    P("FilmGrain", "intensity", 0.7f); P("ChromaticAberration", "intensity", 0.2f + (flick != 0f ? 0.4f : 0f));
                    break;
                }
            }
        }

        // ============================ таблицы цвета (LUT) ============================
        static float Lum(float r, float g, float b) { return 0.299f * r + 0.587f * g + 0.114f * b; }
        static Color Ramp(Color[] s, float t)
        {
            t = Mathf.Clamp01(t) * (s.Length - 1);
            int i = Mathf.Min((int)t, s.Length - 2);
            return Color.Lerp(s[i], s[i + 1], t - i);
        }
        static Color C(int hex) { return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f); }

        static Color Grade(ShadeKind k, float r, float g, float b)
        {
            float l = Lum(r, g, b);
            Color o;
            switch (k)
            {
                case ShadeKind.Negative: return new Color(1f - r, 1f - g, 1f - b);
                case ShadeKind.Thermal:
                    return Ramp(new[] { C(0x000010), C(0x1a0a6e), C(0x7a1aa8), C(0xd2294a), C(0xff7a00), C(0xffd820), C(0xffffe0) }, Mathf.Pow(l, 0.85f));
                case ShadeKind.GameBoy:
                    { int q = Mathf.Clamp((int)(l * 4f), 0, 3); return new[] { C(0x0f380f), C(0x306230), C(0x8bac0f), C(0x9bbc0f) }[q]; }
                case ShadeKind.Poster:
                    {
                        float q(float v) { return Mathf.Round(v * 3f) / 3f; }
                        float ml = Lum(q(r), q(g), q(b));
                        return new Color(Mathf.Clamp01(ml + (q(r) - ml) * 1.4f), Mathf.Clamp01(ml + (q(g) - ml) * 1.4f), Mathf.Clamp01(ml + (q(b) - ml) * 1.4f));
                    }
                case ShadeKind.Noir:
                    { float v = Mathf.SmoothStep(0f, 1f, Mathf.SmoothStep(0f, 1f, l)); return new Color(v * 0.97f, v * 0.99f, v * 1.03f); }
                case ShadeKind.Night:
                    { float v = Mathf.Clamp01(l * 1.5f); return new Color(v * 0.15f, v, v * 0.25f); }
                case ShadeKind.Film:
                    return new Color(Mathf.Clamp01(r * 0.393f + g * 0.769f + b * 0.189f), Mathf.Clamp01(r * 0.349f + g * 0.686f + b * 0.168f), Mathf.Clamp01(r * 0.272f + g * 0.534f + b * 0.131f));
                case ShadeKind.Rage:
                    return new Color(Mathf.Clamp01(l * 1.1f + r * 0.5f), Mathf.Clamp01(g * 0.35f), Mathf.Clamp01(b * 0.3f));
                case ShadeKind.Water:
                    return new Color(r * 0.35f, Mathf.Clamp01(g * 0.9f + 0.03f), Mathf.Clamp01(b * 1.1f + 0.08f));
                case ShadeKind.Radiation:
                    return new Color(Mathf.Clamp01(l * 0.85f + r * 0.2f), Mathf.Clamp01(l * 1.05f + 0.04f), Mathf.Clamp01(b * 0.25f));
                case ShadeKind.Cyber:
                    {
                        Color tone = Color.Lerp(new Color(0f, 0.75f, 0.85f), new Color(1f, 0.15f, 0.8f), Mathf.SmoothStep(0f, 1f, l));
                        o = Color.Lerp(new Color(r, g, b), tone * (0.35f + l * 0.9f), 0.55f);
                        return new Color(Mathf.Clamp01(o.r), Mathf.Clamp01(o.g), Mathf.Clamp01(o.b));
                    }
                case ShadeKind.Dream:
                    {
                        float d = 0.35f; float rr = Mathf.Lerp(r, l, d) * 0.85f + 0.14f, gg = Mathf.Lerp(g, l, d) * 0.85f + 0.11f, bb = Mathf.Lerp(b, l, d) * 0.85f + 0.16f;
                        return new Color(Mathf.Clamp01(rr), Mathf.Clamp01(gg), Mathf.Clamp01(bb));
                    }
                case ShadeKind.Vhs:
                    return new Color(Mathf.Clamp01(r * 0.95f + 0.05f), Mathf.Clamp01(g * 0.98f + 0.04f), Mathf.Clamp01(b * 1.05f + 0.06f));
            }
            return new Color(r, g, b);
        }

        static Texture2D BuildLut(ShadeKind k, int size)
        {
            var tex = new Texture2D(size * size, size, TextureFormat.RGBA32, false, true);
            tex.name = "PeakBR_LUT_" + k; tex.hideFlags = HideFlags.HideAndDontSave;
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size * size];
            float m = size - 1f;
            for (int g = 0; g < size; g++)
                for (int b = 0; b < size; b++)
                    for (int r = 0; r < size; r++)
                    {
                        Color c = Grade(k, r / m, g / m, b / m);
                        px[g * size * size + b * size + r] = new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255), 255);
                    }
            tex.SetPixels32(px); tex.Apply(false, false);
            return tex;
        }
    }
}
