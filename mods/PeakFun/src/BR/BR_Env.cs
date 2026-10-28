// Окружение (туман, свет, URP), пул ламп и игнорирование «невидимых барьеров» игры.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PeakBR
{
    internal static class Env
    {
        static bool on, fogSaved; static bool fogWas; static Color fogCol, ambCol; static FogMode fogMode; static float fogDen, ambInt; static UnityEngine.Rendering.AmbientMode ambMode;
        static readonly List<KeyValuePair<Light, float>> suns = new List<KeyValuePair<Light, float>>(); static readonly Dictionary<string, object> urpOld = new Dictionary<string, object>();
        public static string UrpInfo = "—"; static LevelDef curL; static bool hooked; public static string FogInfo = "";
        static void Enforce()
        {
            var L = curL; if (L == null) return;
            try
            {
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = L.fogDen * (BrGfx.LitOk ? 1f : 1.4f) * Play.FogMul; RenderSettings.fogColor = L.fog;
                if (BrGfx.LitOk) { RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = L.amb; RenderSettings.ambientIntensity = 1f; }
            }
            catch { }
        }
        static void Hook()
        {
            if (hooked) return; hooked = true;
            try { Camera.onPreCull += c => Enforce(); } catch { }
            try { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += (ctx, c) => Enforce(); } catch { }
        }

        public static void Apply(LevelDef L)
        {
            try
            {
                if (!on)
                {
                    on = true; fogSaved = true; fogWas = RenderSettings.fog; fogCol = RenderSettings.fogColor; fogMode = RenderSettings.fogMode; fogDen = RenderSettings.fogDensity;
                    ambMode = RenderSettings.ambientMode; ambCol = RenderSettings.ambientLight; ambInt = RenderSettings.ambientIntensity;
                    suns.Clear(); foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>()) if (l != null && l.type == LightType.Directional && l.enabled && l.intensity > 0f) { suns.Add(new KeyValuePair<Light, float>(l, l.intensity)); l.intensity = 0f; }
                    Urp(true);
                }
                curL = L; Hook();
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = L.fogDen * (BrGfx.LitOk ? 1f : 1.4f) * Play.FogMul; RenderSettings.fogColor = L.fog;
                if (BrGfx.LitOk) { RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = L.amb; RenderSettings.ambientIntensity = 1f; }
            }
            catch (Exception e) { Plugin.Warn("env", e); }
        }
        public static void Restore()
        {
            try
            {
                curL = null; if (!on) return; on = false;
                if (fogSaved) { RenderSettings.fog = fogWas; RenderSettings.fogColor = fogCol; RenderSettings.fogMode = fogMode; RenderSettings.fogDensity = fogDen; fogSaved = false; }
                RenderSettings.ambientMode = ambMode; RenderSettings.ambientLight = ambCol; RenderSettings.ambientIntensity = ambInt;
                foreach (var kv in suns) if (kv.Key != null) kv.Key.intensity = kv.Value; suns.Clear(); Urp(false);
            }
            catch (Exception e) { Plugin.Warn("env-restore", e); }
        }
        public static void SetFog(Color c, float den) { RenderSettings.fogColor = c; RenderSettings.fogDensity = den; }

        static void Urp(bool set)
        {
            try
            {
                var gs = typeof(UnityEngine.Rendering.GraphicsSettings); object asset = null;
                foreach (var pn in new[] { "currentRenderPipeline", "renderPipelineAsset", "defaultRenderPipeline" }) { var pr = gs.GetProperty(pn, BindingFlags.Public | BindingFlags.Static); if (pr != null) { asset = pr.GetValue(null, null); if (asset != null) break; } }
                if (asset == null) { UrpInfo = "URP-ассет не найден"; return; }
                if (!set) { foreach (var kv in urpOld) { var f = asset.GetType().GetField(kv.Key, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); if (f != null) f.SetValue(asset, kv.Value); } urpOld.Clear(); return; }
                var ok = new List<string>();
                Action<string, object> put = (name, val) =>
                {
                    var f = asset.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); if (f == null) return;
                    if (!urpOld.ContainsKey(name)) urpOld[name] = f.GetValue(asset);
                    object v = val; if (f.FieldType.IsEnum) v = Enum.ToObject(f.FieldType, val); f.SetValue(asset, v); ok.Add(name.Replace("m_", ""));
                };
                put("m_AdditionalLightsRenderingMode", 1); put("m_AdditionalLightsPerObjectLimit", 8); put("m_AdditionalLightShadowsSupported", true); put("m_AdditionalLightsShadowmapResolution", 1024);
                UrpInfo = ok.Count == 0 ? "URP: поля не найдены (тени ламп могут быть выключены)" : "URP: " + string.Join(", ", ok.ToArray());
            }
            catch (Exception e) { UrpInfo = "URP: " + e.GetType().Name; }
        }
    }

    /// <summary>Игра ставит невидимые стены между биомами — они пересекают наш уровень. Отключаем столкновения только для себя.</summary>
    internal static class Foreign
    {
        static readonly List<KeyValuePair<Collider, Collider>> ign = new List<KeyValuePair<Collider, Collider>>(); static readonly HashSet<int> seen = new HashSet<int>(); public static string Info = "";
        public static void Scan(Component me, Vector3 center, Vector3 half)
        {
            if (me == null) return;
            try
            {
                var mine = me.GetComponentsInChildren<Collider>(); var cols = Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                int added = 0; var names = new List<string>(); var others = new HashSet<Transform>(); foreach (var o in Game.Others()) if (o != null) others.Add(o.transform.root);
                foreach (var c in cols)
                {
                    if (c == null || c.isTrigger) continue; string nm = c.name; if (nm.StartsWith("BR_") || (c.transform.root != null && c.transform.root.name.StartsWith("BR_"))) continue;
                    if (nm.StartsWith("PF_") || (c.transform.root != null && c.transform.root.name.StartsWith("PF_"))) continue;   // уровни PeakFun — не трогаем
                    if (c.transform.IsChildOf(me.transform) || c.transform.root == me.transform.root) continue; if (others.Contains(c.transform.root)) continue;
                    int id = c.GetInstanceID(); if (seen.Contains(id)) continue; seen.Add(id);
                    foreach (var m in mine) { if (m == null || m.isTrigger) continue; try { Physics.IgnoreCollision(m, c, true); ign.Add(new KeyValuePair<Collider, Collider>(m, c)); } catch { } }
                    added++; if (names.Count < 5) names.Add(nm);
                }
                if (added > 0) Info = "отключены столкновения с " + added + " чужими коллайдерами: " + string.Join(", ", names.ToArray());
            }
            catch (Exception e) { Plugin.Warn("foreign", e); }
        }
        public static void Restore()
        {
            foreach (var kv in ign) { try { if (kv.Key != null && kv.Value != null) Physics.IgnoreCollision(kv.Key, kv.Value, false); } catch { } }
            ign.Clear(); seen.Clear();
        }
        /// <summary>Свободный объём: поднимаем уровень, пока он не перестанет пересекать горы (везде одинаково по детерминированному набору высот).</summary>
        public static Vector3 PickBase(Vector3 baseP, int n, float H)
        {
            try
            {
                float W = n * 4f; Vector3 best = baseP; int bestCnt = int.MaxValue;
                foreach (float add in new[] { 0f, 90f, 200f, 350f, 520f })
                {
                    Vector3 b = baseP + Vector3.up * add; var cols = Physics.OverlapBox(b + new Vector3(W * 0.5f, H * 0.5f, W * 0.5f), new Vector3(W * 0.5f + 8f, H * 0.5f + 8f, W * 0.5f + 8f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    int cnt = 0; foreach (var c in cols) if (c != null && !c.name.StartsWith("BR_") && !c.name.StartsWith("PF_")) cnt++;
                    if (cnt < bestCnt) { bestCnt = cnt; best = b; }
                    if (cnt == 0) break;
                }
                return best;
            }
            catch { return baseP; }
        }
    }

    /// <summary>Десять настоящих точечных ламп, которые переезжают к ближайшим светильникам, и фонарь камеры.</summary>
    internal sealed class Rig
    {
        sealed class Bl { public Light l; public int lamp = -1; public float k; }
        readonly List<Bl> lights = new List<Bl>(); public Light camLight; readonly int[] near = new int[10]; float scan; public float Mul = 1f, CamK = 1f; public float Intensity = 2.3f; public Color Col = Color.white;
        public List<Vector3> LampPos = new List<Vector3>(); public List<Renderer> LampRend = new List<Renderer>(); public Func<Vector3, float> Disturb; public bool FlashOn;

        public void Init(Transform T, Color col, float inten, bool flashlight)
        {
            Col = col; Intensity = inten;
            for (int i = 0; i < 10; i++)
            {
                var g = new GameObject("BR_light_player"); g.transform.SetParent(T, false); var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = col; l.range = 14f; l.intensity = 0f;
                l.shadows = i < 2 ? LightShadows.Soft : LightShadows.None; l.shadowStrength = 0.92f; l.shadowBias = 0.04f; l.shadowNearPlane = 0.2f; lights.Add(new Bl { l = l });
            }
            var cg = new GameObject("BR_camlight_player"); cg.transform.SetParent(T, false); camLight = cg.AddComponent<Light>(); camLight.type = LightType.Point; camLight.color = new Color(1f, 0.96f, 0.85f);
            camLight.range = flashlight ? 15f : 6.5f; camLight.intensity = flashlight ? 1.4f : 0.55f; camLight.shadows = flashlight ? LightShadows.Soft : LightShadows.None; FlashOn = flashlight;
        }
        /// <summary>Сколько ламп отбрасывают тени (авто-оптимизация снижает до 1 или 0).</summary>
        public void SetShadows(int n)
        {
            for (int i = 0; i < lights.Count; i++) lights[i].l.shadows = i < n ? LightShadows.Soft : LightShadows.None;
            if (camLight != null && n <= 0) camLight.shadows = LightShadows.None;
        }
        public void Tick(Vector3 pc)
        {
            if (lights.Count == 0) return; float dt = Time.deltaTime; var cam = Camera.main;
            if (camLight != null)
            {
                Vector3 cp = pc, cf = Vector3.forward; if (cam != null) { cp = cam.transform.position; cf = cam.transform.forward; }
                if (Play.CatchCam) { cp = Play.CatchPos; cf = Play.CatchRot * Vector3.forward; }
                camLight.transform.position = cp + cf * 0.25f; camLight.intensity = (FlashOn ? 1.4f : 0.55f) * CamK;
            }
            Mul = Mathf.MoveTowards(Mul, 1f, dt * 1.4f);
            if (Time.time > scan)
            {
                scan = Time.time + 0.2f; var bd = new float[10]; int cnt = 0;
                for (int i = 0; i < LampPos.Count; i++)
                {
                    float d = (LampPos[i] - pc).sqrMagnitude; if (d > 26f * 26f) continue;
                    if (cnt < 10) { near[cnt] = i; bd[cnt] = d; cnt++; }
                    else { int w = 0; for (int k = 1; k < 10; k++) if (bd[k] > bd[w]) w = k; if (d < bd[w]) { near[w] = i; bd[w] = d; } }
                }
                foreach (var bl in lights) { bool keep = false; for (int k = 0; k < cnt; k++) if (near[k] == bl.lamp) keep = true; if (!keep && bl.lamp >= 0) bl.lamp = -2 - bl.lamp; }
                for (int k = 0; k < cnt; k++)
                {
                    bool has = false; foreach (var bl in lights) if (bl.lamp == near[k]) has = true; if (has) continue;
                    foreach (var bl in lights) if (bl.lamp == -1) { bl.lamp = near[k]; bl.k = 0f; bl.l.transform.position = LampPos[bl.lamp]; break; }
                }
            }
            foreach (var bl in lights)
            {
                if (bl.lamp <= -2) { bl.k = Mathf.MoveTowards(bl.k, 0f, dt * 5f); if (bl.k <= 0.01f) bl.lamp = -1; }
                else if (bl.lamp >= 0)
                {
                    var rr = LampRend[bl.lamp]; float target = (rr == null || rr.enabled) ? 1f : 0.06f;
                    if (Disturb != null) { float dd = Disturb(LampPos[bl.lamp]); if (dd > 0f) { float nz = Mathf.PerlinNoise(Time.time * 9f + bl.lamp * 3.7f, 1.3f); if (nz < dd) target = 0.03f; } }
                    bl.k = Mathf.MoveTowards(bl.k, target, dt * 6f);
                }
                bl.l.intensity = bl.lamp == -1 ? 0f : bl.k * Intensity * Mul;
            }
        }
    }
}
