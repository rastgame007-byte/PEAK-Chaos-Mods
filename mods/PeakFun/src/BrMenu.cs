// Главное меню в стиле Backrooms (1.22): вместо острова за меню — бесконечный жёлтый коридор (настоящие стены, лампы, туман),
// название «BACKROOMS: PEAK» вместо логотипа, VHS-оформление и заставка. Только когда мод установлен; в углу кнопка «Классическое меню».
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        ConfigEntry<bool> cfgBrDread, cfgBrMenu, cfgBrHum, cfgBrIsland, cfgBrAuto, cfgBrNormals, cfgBrHalos, cfgBrRefl; ConfigEntry<float> cfgBrFog; float brAutoT; bool brAutoDone;
        const string BrTitle = "BACKROOMS: PEAK";

        void InitBrMenu()
        {
            cfgBrMenu = Config.Bind("UI", "BackroomsMenu", true, "Главное меню и заставка в стиле Backrooms (false — классическое меню игры)");
            cfgBrHum = Config.Bind("UI", "BackroomsMenuHum", true, "Тихий гул ламп в главном меню");
            cfgBrIsland = Config.Bind("Backrooms", "HideIsland", true, "Прятать остров на время забега в Backrooms (после выхода всё возвращается)");
            PeakBR.Island.Enabled = cfgBrIsland.Value;
            cfgBrAuto = Config.Bind("Backrooms", "AutoEnter", true, "Сразу входить в Backrooms, когда персонаж появился в игре (первым начинает игрок с наименьшим номером; остальные присоединяются сами)");
            cfgBrFog = Config.Bind("Backrooms", "FogScale", 0.7f, "Густота тумана в Backrooms (1 = как задумано, меньше — лучше видно)");
            cfgBrNormals = Config.Bind("Backrooms", "NormalMaps", true, "Рельеф текстур (карты нормалей). Если стены выглядят чёрными или странными — поставь false");
            cfgBrRefl = Config.Bind("Backrooms", "PuddleReflections", true, "Отражения в лужах (реальный ReflectionProbe, чуть нагружает видеокарту)"); PeakBR.Refl.On = cfgBrRefl.Value;
            cfgBrHalos = Config.Bind("Backrooms", "LampHalos", true, "Мягкое свечение вокруг ламп на потолке");
            cfgBrDread = Config.Bind("Backrooms", "Dread", true, "Давящая атмосфера: растущая тревога, тишина, шёпот, шаги за спиной, силуэты на краю зрения"); PeakBR.Dread.Enabled = cfgBrDread.Value;
            PeakBR.Torch.Key = Config.Bind("Backrooms", "TorchKey", KeyCode.F, "Клавиша фонарика в Backrooms").Value;
            PeakBR.BrGfx.NormalMaps = cfgBrNormals.Value; PeakBR.LevelBuilder.Halos = cfgBrHalos.Value;
            PeakBR.Play.FogMul = Mathf.Clamp(cfgBrFog.Value, 0.1f, 2f); PeakBR.Play.AutoJoin = cfgBrAuto.Value;
        }

        // ---------- состояние меню-туннеля ----------
        float brFar; GameObject brTun; Transform brScroll; Camera brCam; Material mnFlick, brPanelOn; Light[] mnLights; GameObject brFigure; float brFigT, brBlack, brNextCheck, brMenuT, brNextFig = 18f;
        AudioSource brHumSrc; bool brEnvSaved; Color brAmb, brFogC; bool brFog; float brFogD; UnityEngine.Rendering.AmbientMode mnAmbMode; FogMode mnFogMode;
        readonly List<Behaviour> brHidden = new List<Behaviour>(); readonly List<Light> brSunOff = new List<Light>(); bool brLogged;
        bool brFailed; static Type tTmp, tImg, tRaw; static bool tScanned;

        bool InBrMenu { get { try { return cfgBrMenu != null && cfgBrMenu.Value && !brFailed && Game.Me() == null && !PeakBR.Play.On; } catch { return false; } } }

        float brWarnT; void BrWarn(string w, Exception e) { if (Time.unscaledTime < brWarnT) return; brWarnT = Time.unscaledTime + 20f; Logger.LogWarning("[" + w + "] " + e); }

        void BrAutoTick()
        {
            if (cfgBrAuto == null) return; PeakBR.Play.AutoJoin = cfgBrAuto.Value; PeakBR.Play.FogMul = Mathf.Clamp(cfgBrFog.Value, 0.1f, 2f);
            var me = Game.Me();
            if (me == null) { brAutoT = 0f; brAutoDone = false; PeakBR.Play.OptOut = false; return; }
            if (!cfgBrAuto.Value || brAutoDone || PeakBR.Play.On || PeakBR.Play.Busy || PeakBR.Plugin.Inst == null) return;
            brAutoT += Time.unscaledDeltaTime; if (brAutoT < 7f) return;
            if (Game.InRoom)
            {
                int low = int.MaxValue; foreach (var c in Game.Everyone(true)) { int a = Game.ActorOf(c); if (a < low) low = a; }
                if (low != Game.LocalActor() && brAutoT < 30f) return;   // ждём «go» от игрока с наименьшим номером
            }
            if (Game.IsDead(me)) return;
            PeakBR.Play.StartRun(0); if (PeakBR.Play.On || PeakBR.Play.Busy) brAutoDone = true; else brAutoT -= 3f;
        }

        void BrMenuTick()
        {
            try { BrAutoTick(); } catch (Exception ae) { BrWarn("автовход", ae); }
            bool want = InBrMenu && !brFailed;
            if (want) brMenuT += Time.unscaledDeltaTime; else brMenuT = 0f;
            if (!want) { if (brTun != null || brHidden.Count > 0 || brEnvSaved) BrMenuOff(); return; }
            if (brMenuT < 0.4f) return;
            var cam = PickCam(); if (cam == null) return;
            if (brTun == null || brCam != cam) { BrMenuOff(); BuildBrTunnel(cam); }
            if (brTun == null) return;
            float t = Time.unscaledTime;
            brScroll.localPosition = new Vector3(0f, -1.45f, -Mathf.Repeat(t * 1.15f, 8f));
            // мерцание ламп и редкие «отключения света»
            brBlack = Mathf.Max(0f, brBlack - Time.unscaledDeltaTime);
            if (t > brNextFig) { brNextFig = t + UnityEngine.Random.Range(26f, 48f); brBlack = UnityEngine.Random.Range(0.7f, 1.4f); brFigT = brBlack + 0.2f; if (brFigure != null) { brFigure.SetActive(true); } }
            float fl = brBlack > 0f ? 0.02f : (Mathf.PerlinNoise(t * 7f, 0.3f) > 0.18f ? 1f : 0.25f);
            float st = brBlack > 0f ? 0.0f : 1f;
            if (mnFlick != null) mnFlick.SetColor("_EmissionColor", new Color(1f, 0.93f, 0.62f) * (2.2f * fl));
            if (brPanelOn != null) brPanelOn.SetColor("_EmissionColor", new Color(1f, 0.93f, 0.62f) * (2.2f * st));
            if (mnLights != null) for (int i = 0; i < mnLights.Length; i++) if (mnLights[i] != null) mnLights[i].intensity = 2.4f * (i % 2 == 0 ? fl : st) * (0.92f + 0.08f * Mathf.Sin(t * 31f + i));
            if (brFigure != null && brFigure.activeSelf) { brFigT -= Time.unscaledDeltaTime; if (brFigT <= 0f) brFigure.SetActive(false); }
            if (cfgBrHum.Value) { if (brHumSrc == null) { try { brHumSrc = PeakBR.Snd2.Ambient("hum", 0.22f); } catch { } } } else if (brHumSrc != null) { UnityEngine.Object.Destroy(brHumSrc.gameObject); brHumSrc = null; }
            if (t > brNextCheck) { brNextCheck = t + 1.5f; HideLogos(); }
        }

        Camera PickCam()
        {
            Camera best = null; var main = Camera.main; if (main != null && main.enabled && main.targetTexture == null && (main.cullingMask & 1) != 0) return main;
            foreach (var c in Camera.allCameras) { if (c == null || !c.enabled || c.targetTexture != null || c.name.StartsWith("BR_") || c.name.StartsWith("PF_") || (c.cullingMask & 1) == 0) continue; if (best == null || c.depth > best.depth) best = c; }
            return best;
        }

        static Material BrMat(Texture tex, Color col, float sx, float sy, float sm = 0.1f)
        {
            var m = PeakBR.BrGfx.Surf(tex, col, sm); try { m.mainTextureScale = new Vector2(sx, sy); if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", new Vector2(sx, sy)); } catch { } return m;
        }
        static GameObject Bx(Transform par, string n, Vector3 pos, Vector3 size, Material m, int layer)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "BR_menu_" + n; var c = g.GetComponent<Collider>(); if (c != null) UnityEngine.Object.DestroyImmediate(c);
            g.transform.SetParent(par, false); g.transform.localPosition = pos; g.transform.localScale = size; g.layer = layer; var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; return g;
        }

        void BuildBrTunnel(Camera cam)
        {
            try
            {
                int layer = 0; if ((cam.cullingMask & 1) == 0) { for (int i = 0; i < 32; i++) if (i != 5 && (cam.cullingMask & (1 << i)) != 0) { layer = i; break; } }
                brCam = cam; brFar = cam.farClipPlane; try { Logger.LogInfo("Backrooms-меню: камера «" + cam.name + "» far=" + cam.farClipPlane + " near=" + cam.nearClipPlane + " clear=" + cam.clearFlags + " mask=" + cam.cullingMask); if (cam.farClipPlane < 140f) cam.farClipPlane = 140f; } catch { }
                brTun = new GameObject("BR_menu_tunnel"); brTun.layer = layer; var T = brTun.transform; T.SetParent(cam.transform, false); T.localPosition = Vector3.zero; T.localRotation = Quaternion.identity;
                float W = 4.2f, H = 2.9f, z0 = -20f, z1 = 60f, L = z1 - z0, zc = (z0 + z1) * 0.5f;
                var tw = PeakBR.Tex.Wall(0); var tf = PeakBR.Tex.Floor(0); var tc = PeakBR.Tex.Ceil(0);
                var mw = BrMat(tw, new Color(0.9f, 0.86f, 0.55f), L / 4f, H / 4f, 0.08f); var mf = BrMat(tf, new Color(0.75f, 0.68f, 0.45f), W / 4f, L / 4f, 0.05f); var mc = BrMat(tc, new Color(0.9f, 0.9f, 0.8f), W / 4f, L / 4f, 0.05f);
                var mend = BrMat(tw, new Color(0.85f, 0.8f, 0.5f), W / 4f, H / 4f, 0.08f); var mpil = BrMat(tw, new Color(0.85f, 0.8f, 0.5f), 0.4f, H / 4f, 0.08f);
                mnFlick = PeakBR.BrGfx.Surf(null, new Color(0.95f, 0.95f, 0.9f), 0.4f, 0f, new Color(2f, 1.9f, 1.2f)); brPanelOn = PeakBR.BrGfx.Surf(null, new Color(0.95f, 0.95f, 0.9f), 0.4f, 0f, new Color(2f, 1.9f, 1.2f));
                var root = new GameObject("BR_menu_scroll"); root.layer = layer; brScroll = root.transform; brScroll.SetParent(T, false);
                float cy = H * 0.5f; // низ коридора в локальных координатах скролла = 0
                Bx(brScroll, "wl", new Vector3(-W * 0.5f - 0.5f, cy, zc), new Vector3(1f, H, L), mw, layer); Bx(brScroll, "wr", new Vector3(W * 0.5f + 0.5f, cy, zc), new Vector3(1f, H, L), mw, layer);
                Bx(brScroll, "fl", new Vector3(0f, -0.5f, zc), new Vector3(W + 2f, 1f, L), mf, layer); Bx(brScroll, "ce", new Vector3(0f, H + 0.5f, zc), new Vector3(W + 2f, 1f, L), mc, layer);
                Bx(brScroll, "bk", new Vector3(0f, cy, z0 - 0.5f), new Vector3(W + 2f, H, 1f), mend, layer); Bx(brScroll, "fr", new Vector3(0f, cy, z1 + 0.5f), new Vector3(W + 2f, H, 1f), mend, layer);
                for (float z = z0 + 2f; z < z1; z += 4f) { int k = Mathf.RoundToInt((z - z0) / 4f); Bx(brScroll, "lamp", new Vector3(0f, H - 0.03f, z), new Vector3(1.5f, 0.06f, 0.6f), k % 3 == 1 ? mnFlick : brPanelOn, layer); }
                for (int k = 0; k < 12; k++) { float z = z0 + 4f + k * 8f; float sd = (k % 2 == 0) ? -1f : 1f; Bx(brScroll, "pil", new Vector3(sd * (W * 0.5f - 0.65f), cy, z), new Vector3(1.3f, H, 0.6f), mpil, layer); }
                // «туман» из полупрозрачных плоскостей: настоящий туман в меню не всегда работает, поэтому конец коридора затемняем так
                try { var fm = PeakBR.Vfx.Mat(Texture2D.whiteTexture, new Color(0.10f, 0.08f, 0.03f, 0.10f)); for (int k = 0; k < 11; k++) { var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "BR_menu_fog"; var qc = q.GetComponent<Collider>(); if (qc != null) UnityEngine.Object.DestroyImmediate(qc); q.layer = layer; q.transform.SetParent(T, false); q.transform.localPosition = new Vector3(0f, -1.45f + H * 0.5f, 5f + k * 5f); q.transform.localScale = new Vector3(W, H, 1f); var qr = q.GetComponent<MeshRenderer>(); qr.sharedMaterial = fm; qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; qr.receiveShadows = false; } } catch (Exception fe) { BrWarn("туман меню", fe); }
                // реальные лампы (не скроллятся)
                mnLights = new Light[6]; for (int i = 0; i < mnLights.Length; i++) { var g = new GameObject("BR_menu_light"); g.layer = layer; g.transform.SetParent(T, false); g.transform.localPosition = new Vector3(0f, -1.45f + H - 0.35f, 2f + i * 8f); var l = g.AddComponent<Light>(); l.type = LightType.Point; l.range = 12f; l.intensity = 2.4f; l.color = new Color(1f, 0.92f, 0.62f); l.shadows = LightShadows.None; mnLights[i] = l; }
                // силуэт вдали — виден только в темноте
                brFigure = new GameObject("BR_menu_figure"); brFigure.layer = layer; brFigure.transform.SetParent(T, false); brFigure.transform.localPosition = new Vector3(0.2f, -1.45f, 17f); var blk = PeakBR.BrGfx.Surf(null, new Color(0.01f, 0.01f, 0.012f), 0.6f); var eye = PeakBR.BrGfx.Surf(null, new Color(1f, 0.98f, 0.85f), 0.4f, 0f, new Color(4f, 3.9f, 3.4f));
                Bx(brFigure.transform, "b", new Vector3(0, 1.15f, 0), new Vector3(0.55f, 1.5f, 0.3f), blk, layer); Bx(brFigure.transform, "h", new Vector3(0, 2.0f, 0), new Vector3(0.34f, 0.46f, 0.3f), blk, layer);
                Bx(brFigure.transform, "e1", new Vector3(-0.07f, 2.05f, -0.16f), new Vector3(0.06f, 0.08f, 0.02f), eye, layer); Bx(brFigure.transform, "e2", new Vector3(0.07f, 2.05f, -0.16f), new Vector3(0.06f, 0.08f, 0.02f), eye, layer); brFigure.SetActive(false);
                // окружение: туман, амбиент, без солнца
                if (!brEnvSaved) { brEnvSaved = true; brAmb = RenderSettings.ambientLight; mnAmbMode = RenderSettings.ambientMode; brFog = RenderSettings.fog; brFogC = RenderSettings.fogColor; brFogD = RenderSettings.fogDensity; mnFogMode = RenderSettings.fogMode; }
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.3f, 0.27f, 0.13f); RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogColor = new Color(0.17f, 0.145f, 0.06f); RenderSettings.fogDensity = 0.05f;
                brSunOff.Clear(); foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>()) if (l != null && l.enabled && l.type == LightType.Directional && !l.name.StartsWith("BR_")) { l.enabled = false; brSunOff.Add(l); }
                Logger.LogInfo("Backrooms-меню: туннель построен на камере «" + cam.name + "» (слой " + layer + ")");
            }
            catch (Exception e) { BrWarn("backrooms-меню", e); brFailed = true; BrMenuOff(); }
        }

        void BrMenuOff()
        {
            try { if (brTun != null) UnityEngine.Object.Destroy(brTun); } catch { }
            try { if (brCam != null && brFar > 0f) brCam.farClipPlane = brFar; } catch { }
            brTun = null; brScroll = null; mnLights = null; brFigure = null; brCam = null;
            if (brHumSrc != null) { try { UnityEngine.Object.Destroy(brHumSrc.gameObject); } catch { } brHumSrc = null; }
            foreach (var l in brSunOff) { try { if (l != null) l.enabled = true; } catch { } }
            brSunOff.Clear();
            if (brEnvSaved) { try { RenderSettings.ambientMode = mnAmbMode; RenderSettings.ambientLight = brAmb; RenderSettings.fog = brFog; RenderSettings.fogColor = brFogC; RenderSettings.fogDensity = brFogD; RenderSettings.fogMode = mnFogMode; } catch { } brEnvSaved = false; }
            foreach (var b in brHidden) { try { if (b != null) b.enabled = true; } catch { } }
            brHidden.Clear(); brLogged = false;
        }

        // ---------- логотип игры прячем (через рефлексию — ссылок на UI-сборки нет) ----------
        static void ScanTypes()
        {
            if (tScanned) return; tScanned = true;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { if (tTmp == null) tTmp = a.GetType("TMPro.TMP_Text"); if (tImg == null) tImg = a.GetType("UnityEngine.UI.Image"); if (tRaw == null) tRaw = a.GetType("UnityEngine.UI.RawImage"); } catch { }
            }
        }
        void HideLogos()
        {
            try
            {
                ScanTypes(); var found = new List<string>();
                if (tTmp != null)
                {
                    var pText = tTmp.GetProperty("text");
                    foreach (var o in UnityEngine.Object.FindObjectsOfType(tTmp))
                    {
                        var b = o as Behaviour; if (b == null || !b.enabled || !b.gameObject.activeInHierarchy) continue; string tx = null; try { tx = pText.GetValue(o, null) as string; } catch { }
                        if (string.IsNullOrEmpty(tx)) continue; string n = tx.Trim(); string plain = System.Text.RegularExpressions.Regex.Replace(n, "<[^>]+>", "").Trim();
                        if (plain.Length < 40 && plain.ToUpperInvariant().Contains("PEAK") && !plain.StartsWith("BACKROOMS")) { found.Add("TMP:" + b.name + "='" + plain + "'"); if (plain.Length <= 14) { b.enabled = false; brHidden.Add(b); } }
                    }
                }
                foreach (var t in new[] { tImg, tRaw })
                {
                    if (t == null) continue;
                    foreach (var o in UnityEngine.Object.FindObjectsOfType(t))
                    {
                        var b = o as Behaviour; if (b == null || !b.enabled || !b.gameObject.activeInHierarchy) continue; string nm = b.name.ToLowerInvariant();
                        if (nm.Contains("logo") || nm.Contains("gametitle") || nm.Contains("game_title") || nm.Contains("peaklogo")) { found.Add("IMG:" + b.name); b.enabled = false; brHidden.Add(b); }
                    }
                }
                foreach (var sr in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>()) { if (sr == null || !sr.enabled) continue; string nm = sr.name.ToLowerInvariant(); if (nm.Contains("logo")) { found.Add("SPR:" + sr.name); sr.enabled = false; brHidden.Add(null); } }
                if (!brLogged && found.Count > 0) { brLogged = true; Logger.LogInfo("Backrooms-меню: спрятан логотип: " + string.Join("; ", found.ToArray())); }
            }
            catch (Exception e) { BrWarn("логотип", e); }
        }

        // ---------- IMGUI: название, VHS, кнопка ----------
        void DrawBrMenuScreen(float s)
        {
            if (!InBrMenu || brTun == null) return; var e = Event.current; float W = Screen.width, H = Screen.height; float t = Time.unscaledTime;
            var Y = new Color(1f, 0.93f, 0.45f);
            if (e.type == EventType.Repaint)
            {
                // сканлайны и виньетка
                for (float y = 0; y < H; y += 7f * s) Ui.Box(new Rect(0, y, W, 1.5f * s), new Color(0, 0, 0, 0.10f), 0f);
                for (int i = 0; i < 10; i++) { float a = 0.05f * (10 - i) / 10f * 2.4f; float w = (6f + i * 10f) * s; Ui.Box(new Rect(0, 0, W, w * 0.6f), new Color(0, 0, 0, a), 0f); Ui.Box(new Rect(0, H - w * 0.6f, W, w * 0.6f), new Color(0, 0, 0, a), 0f); Ui.Box(new Rect(0, 0, w, H), new Color(0, 0, 0, a), 0f); Ui.Box(new Rect(W - w, 0, w, H), new Color(0, 0, 0, a), 0f); }
                float gl = Mathf.PerlinNoise(t * 9f, 1.7f) > 0.965f ? 1f : 0f; float gx = gl * (Mathf.PerlinNoise(t * 30f, 2f) - 0.5f) * 18f * s;
                float fs = 84f * s; float ty = H * 0.045f;
                Ui.Text(new Rect(-3f * s + gx, ty, W, fs * 1.2f), BrTitle, (int)fs, new Color(1f, 0.1f, 0.1f, 0.55f * gl), TextAnchor.MiddleCenter, true);
                Ui.Text(new Rect(3f * s + gx, ty, W, fs * 1.2f), BrTitle, (int)fs, new Color(0.1f, 0.9f, 1f, 0.5f * gl), TextAnchor.MiddleCenter, true);
                float flick = 0.88f + 0.12f * Mathf.Sin(t * 23f) * (Mathf.PerlinNoise(t * 5f, 0.4f) > 0.2f ? 1f : 0.2f);
                Ui.Text(new Rect(3f * s, ty + 4f * s, W, fs * 1.2f), BrTitle, (int)fs, new Color(0, 0, 0, 0.7f), TextAnchor.MiddleCenter, true);
                Ui.Text(new Rect(gx, ty, W, fs * 1.2f), BrTitle, (int)fs, new Color(Y.r, Y.g, Y.b, flick), TextAnchor.MiddleCenter, true);
                Ui.Text(new Rect(0, ty + fs * 1.05f, W, 30f * s), PeakBR.Levels.All.Length + " УРОВНЕЙ   ·   ВЫХОДА НЕТ… ИЛИ ЕСТЬ?", (int)(20f * s), new Color(Y.r, Y.g, Y.b, 0.75f), TextAnchor.MiddleCenter, false);
                int sec = (int)(t % 3600f); bool rec = ((int)(t * 1.6f)) % 2 == 0;
                Ui.Text(new Rect(28f * s, H - 64f * s, 400f * s, 28f * s), (rec ? "● " : "  ") + "REC   " + string.Format("{0:00}:{1:00}:{2:00}", sec / 3600, sec / 60 % 60, sec % 60), (int)(20f * s), new Color(1f, 0.25f, 0.2f, 0.9f), TextAnchor.MiddleLeft, true);
                Ui.Text(new Rect(0, H - 34f * s, W, 22f * s), "PeakFun " + Version + " · Backrooms-режим · чтобы вернуть обычное меню — кнопка слева внизу", (int)(13f * s), new Color(Y.r, Y.g, Y.b, 0.45f), TextAnchor.MiddleCenter, false);
            }
            var br = new Rect(W - 250f * s, H - 56f * s, 230f * s, 38f * s);
            if (GUI.Button(br, "Классическое меню")) { cfgBrMenu.Value = false; BrMenuOff(); }
        }

        void DrawIntroBR(float s, float t)
        {
            if (Event.current.type != EventType.Repaint) return;
            float W = Screen.width, H = Screen.height; float fadeIn = Mathf.Clamp01(t / 0.4f), fadeOut = Mathf.Clamp01((IntroLen - t) / 0.7f), a = Mathf.Min(fadeIn, fadeOut);
            Ui.Box(new Rect(0, 0, W, H), new Color(0.015f, 0.012f, 0.004f, a), 0f);
            var Y = new Color(1f, 0.93f, 0.45f);
            for (float y = 0; y < H; y += 6f * s) Ui.Box(new Rect(0, y, W, 1.4f * s), new Color(Y.r, Y.g, Y.b, 0.035f * a), 0f);
            int shown = Mathf.Clamp(Mathf.FloorToInt((t - 0.35f) * 14f), 0, BrTitle.Length); string txt = BrTitle.Substring(0, shown);
            bool blink = Mathf.PerlinNoise(t * 11f, 0.8f) > 0.12f; float fs = 96f * s;
            if (blink) { Ui.Text(new Rect(3f * s, H * 0.42f + 3f * s, W, fs * 1.3f), txt, (int)fs, new Color(0, 0, 0, a), TextAnchor.MiddleCenter, true); Ui.Text(new Rect(0, H * 0.42f, W, fs * 1.3f), txt, (int)fs, new Color(Y.r, Y.g, Y.b, a), TextAnchor.MiddleCenter, true); }
            float k2 = Mathf.Clamp01((t - 1.7f) / 0.6f);
            Ui.Text(new Rect(0, H * 0.42f + fs * 1.15f, W, 36f * s), "если ты это читаешь — ты уже внутри", (int)(22f * s), new Color(Y.r, Y.g, Y.b, 0.8f * k2 * a), TextAnchor.MiddleCenter, false);
            Ui.Text(new Rect(0, H * 0.42f + fs * 1.15f + 40f * s, W, 28f * s), "v" + Version + " · " + PeakBR.Levels.All.Length + " уровней · любая клавиша — пропустить", (int)(15f * s), new Color(Y.r, Y.g, Y.b, 0.5f * k2 * a), TextAnchor.MiddleCenter, false);
            float p = Mathf.Clamp01(t / (IntroLen - 0.4f)); Ui.Box(new Rect(W * 0.35f, H * 0.82f, W * 0.3f * p, 3f * s), new Color(Y.r, Y.g, Y.b, a), 0f);
        }
    }
}
