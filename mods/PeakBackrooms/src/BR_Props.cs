// Модели и эффекты окружения (1.26): трубы, фланцы, вентили, конусы сталактитов, неровные валуны, повёрнутые коробки;
// отражающие лужи (реальный ReflectionProbe), пар/капли/искры/дождь, наборы звуков по уровням.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal struct Fx { public int kind; public Vector3 pos; public float t; }   // kind: 1 пар, 2 капля/рябь, 3 искры, 4 вентилятор

    internal static class MeshX
    {
        static void T(MeshB m, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            int i = m.v.Count; m.v.Add(a); m.v.Add(b); m.v.Add(c); m.n.Add(na); m.n.Add(nb); m.n.Add(nc); m.uv.Add(ua); m.uv.Add(ub); m.uv.Add(uc);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), na + nb + nc) >= 0f) { m.t.Add(i); m.t.Add(i + 1); m.t.Add(i + 2); } else { m.t.Add(i); m.t.Add(i + 2); m.t.Add(i + 1); }
        }
        static void Basis(Vector3 d, out Vector3 u, out Vector3 v) { u = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized; v = Vector3.Cross(d, u).normalized; }

        /// <summary>Труба/цилиндр между двумя точками (гладкие нормали, плоские торцы).</summary>
        public static void Cyl(this MeshB m, Vector3 a, Vector3 b, float r, int sides = 10, bool caps = true)
        {
            Vector3 d = b - a; float len = d.magnitude; if (len < 0.001f) return; d /= len; Vector3 u, v; Basis(d, out u, out v);
            int i0 = m.v.Count;
            for (int i = 0; i <= sides; i++)
            {
                float an = i / (float)sides * Mathf.PI * 2f; Vector3 nn = u * Mathf.Cos(an) + v * Mathf.Sin(an); float uu = i / (float)sides;
                m.v.Add(a + nn * r); m.n.Add(nn); m.uv.Add(new Vector2(uu, 0)); m.v.Add(b + nn * r); m.n.Add(nn); m.uv.Add(new Vector2(uu, 1));
            }
            for (int i = 0; i < sides; i++)
            {
                int p0 = i0 + i * 2, q0 = p0 + 1, p1 = p0 + 2, q1 = p0 + 3; Vector3 nn = m.n[p0] + m.n[p1];
                if (Vector3.Dot(Vector3.Cross(m.v[q0] - m.v[p0], m.v[q1] - m.v[p0]), nn) >= 0f) { m.t.Add(p0); m.t.Add(q0); m.t.Add(q1); m.t.Add(p0); m.t.Add(q1); m.t.Add(p1); }
                else { m.t.Add(p0); m.t.Add(q1); m.t.Add(q0); m.t.Add(p0); m.t.Add(p1); m.t.Add(q1); }
            }
            if (!caps) return;
            for (int e = 0; e < 2; e++)
            {
                Vector3 c = e == 0 ? a : b, nd = e == 0 ? -d : d; int ci = m.v.Count; m.v.Add(c); m.n.Add(nd); m.uv.Add(new Vector2(.5f, .5f));
                for (int i = 0; i <= sides; i++) { float an = i / (float)sides * Mathf.PI * 2f; Vector3 nn = u * Mathf.Cos(an) + v * Mathf.Sin(an); m.v.Add(c + nn * r); m.n.Add(nd); m.uv.Add(new Vector2(.5f + .5f * Mathf.Cos(an), .5f + .5f * Mathf.Sin(an))); }
                for (int i = 0; i < sides; i++)
                {
                    int p0 = ci + 1 + i, p1 = p0 + 1;
                    if (Vector3.Dot(Vector3.Cross(m.v[p0] - c, m.v[p1] - c), nd) >= 0f) { m.t.Add(ci); m.t.Add(p0); m.t.Add(p1); } else { m.t.Add(ci); m.t.Add(p1); m.t.Add(p0); }
                }
            }
        }
        /// <summary>Конус от основания к вершине (сталактиты, кристаллы, шипы).</summary>
        public static void Cone(this MeshB m, Vector3 baseC, Vector3 tip, float r, int sides = 8)
        {
            Vector3 d = tip - baseC; float len = d.magnitude; if (len < 0.001f) return; d /= len; Vector3 u, v; Basis(d, out u, out v);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f; Vector3 n0 = u * Mathf.Cos(a0) + v * Mathf.Sin(a0), n1 = u * Mathf.Cos(a1) + v * Mathf.Sin(a1);
                Vector3 nt = ((n0 + n1) * 0.5f * len + d * r).normalized; Vector3 nn0 = (n0 * len + d * r).normalized, nn1 = (n1 * len + d * r).normalized;
                T(m, baseC + n0 * r, tip, baseC + n1 * r, nn0, nt, nn1, new Vector2(0, 0), new Vector2(.5f, 1), new Vector2(1, 0)); T(m, baseC, baseC + n0 * r, baseC + n1 * r, -d, -d, -d, new Vector2(.5f, .5f), Vector2.zero, Vector2.one);
            }
        }
        /// <summary>Неровный камень/мешок/куст — «лепной» эллипсоид.</summary>
        public static void Blob(this MeshB m, Vector3 c, Vector3 size, int seed, float amp = 0.18f)
        {
            const int LAT = 6, LON = 9; var pts = new Vector3[LAT + 1, LON + 1]; var rnd = new System.Random(seed);
            var ph = new float[LON]; for (int j = 0; j < LON; j++) ph[j] = 1f + ((float)rnd.NextDouble() - 0.5f) * 2f * amp;
            for (int i = 0; i <= LAT; i++) for (int j = 0; j <= LON; j++)
                {
                    float p = i / (float)LAT * Mathf.PI, l = j / (float)LON * Mathf.PI * 2f; float k = ph[j % LON] * (1f + 0.5f * amp * Mathf.Sin(i * 2.1f + seed));
                    pts[i, j] = c + new Vector3(Mathf.Sin(p) * Mathf.Cos(l) * size.x, Mathf.Cos(p) * size.y, Mathf.Sin(p) * Mathf.Sin(l) * size.z) * 0.5f * k;
                }
            for (int i = 0; i < LAT; i++) for (int j = 0; j < LON; j++)
                {
                    Vector3 a = pts[i, j], b = pts[i + 1, j], cc = pts[i + 1, j + 1], d = pts[i, j + 1]; Vector3 n = ((a + b + cc + d) * 0.25f - c).normalized;
                    T(m, a, b, cc, n, n, n, Vector2.zero, Vector2.up, Vector2.one); T(m, a, cc, d, n, n, n, Vector2.zero, Vector2.one, Vector2.right);
                }
        }
        /// <summary>Коробка, повёрнутая вокруг вертикали.</summary>
        public static void BoxY(this MeshB m, Vector3 c, Vector3 s, float yawDeg)
        {
            var q = Quaternion.Euler(0f, yawDeg, 0f); Vector3 h = s * 0.5f; Vector3[] k = new Vector3[8];
            for (int i = 0; i < 8; i++) k[i] = c + q * new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            Action<int, int, int, int, Vector3> Q = (a, b, cc, d, nn) => { Vector3 n = q * nn; T(m, k[a], k[b], k[cc], n, n, n, Vector2.zero, Vector2.up, Vector2.one); T(m, k[a], k[cc], k[d], n, n, n, Vector2.zero, Vector2.one, Vector2.right); };
            Q(1, 3, 7, 5, Vector3.right); Q(0, 2, 6, 4, Vector3.left); Q(2, 3, 7, 6, Vector3.up); Q(0, 1, 5, 4, Vector3.down); Q(4, 5, 7, 6, Vector3.forward); Q(0, 1, 3, 2, Vector3.back);
        }
        /// <summary>Штурвал вентиля: обод из сегментов, 4 спицы и ступица.</summary>
        public static void Wheel(this MeshB m, Vector3 c, Vector3 axis, float R, float r)
        {
            axis.Normalize(); Vector3 u, v; Basis(axis, out u, out v); const int N = 12;
            for (int k = 0; k < N; k++) { float a0 = k / (float)N * Mathf.PI * 2f, a1 = (k + 1) / (float)N * Mathf.PI * 2f; m.Cyl(c + (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * R, c + (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * R, r, 5, false); }
            for (int k = 0; k < 4; k++) { float a0 = k * Mathf.PI * 0.5f; m.Cyl(c, c + (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * R, r * 0.7f, 4, false); }
            m.Cyl(c - axis * 0.03f, c + axis * 0.03f, r * 2.2f, 8);
        }
        /// <summary>Неровная плоская лужа (веер по контуру), нормаль вверх.</summary>
        public static void Puddle(this MeshB m, Vector3 c, float rx, float rz, float rot, int seed)
        {
            const int N = 22; var rnd = new System.Random(seed); float p1 = (float)rnd.NextDouble() * 6f, p2 = (float)rnd.NextDouble() * 6f; var q = Quaternion.Euler(0, rot, 0); var pts = new Vector3[N + 1];
            for (int i = 0; i <= N; i++) { float a = i / (float)N * Mathf.PI * 2f; float rr = 1f + 0.22f * Mathf.Sin(3f * a + p1) + 0.12f * Mathf.Sin(5f * a + p2); pts[i] = c + q * new Vector3(Mathf.Cos(a) * rx * rr, 0f, Mathf.Sin(a) * rz * rr); }
            for (int i = 0; i < N; i++) T(m, c, pts[i], pts[i + 1], Vector3.up, Vector3.up, Vector3.up, new Vector2(.5f, .5f), Vector2.zero, Vector2.one);
        }
    }

    /// <summary>Реальные отражения: тёмный кубмап как запасной вариант + ReflectionProbe, который следует за игроком.</summary>
    internal static class Refl
    {
        public static bool On = true; static ReflectionProbe probe; static float next; static bool saved; static UnityEngine.Rendering.DefaultReflectionMode oldMode; static Texture oldTex; static float oldInt; static Cubemap cm; public static string Info = "";
        static System.Reflection.PropertyInfo CustomProp()
        {
            var t = typeof(RenderSettings); return t.GetProperty("customReflectionTexture") ?? t.GetProperty("customReflection");
        }
        static Texture GetCustom() { try { var p = CustomProp(); return p == null ? null : p.GetValue(null, null) as Texture; } catch { return null; } }
        static void SetCustom(Texture tx) { try { var p = CustomProp(); if (p != null) p.SetValue(null, tx, null); } catch { } }
        public static void Start(LevelDef L)
        {
            if (!On) return;
            try
            {
                if (!saved) { saved = true; oldMode = RenderSettings.defaultReflectionMode; oldTex = GetCustom(); oldInt = RenderSettings.reflectionIntensity; }
                cm = new Cubemap(16, TextureFormat.RGBA32, false); Color c = Color.Lerp(L.fog, L.amb, 0.5f); var px = new Color[256]; for (int i = 0; i < 256; i++) px[i] = c;
                foreach (CubemapFace f in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ }) cm.SetPixels(px, f); cm.Apply();
                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom; SetCustom(cm); RenderSettings.reflectionIntensity = 1f;
                var go = new GameObject("BR_reflprobe"); probe = go.AddComponent<ReflectionProbe>(); probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime; probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces; probe.resolution = 64; probe.hdr = false; probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor; probe.backgroundColor = Color.Lerp(L.fog, Color.black, 0.4f);
                probe.size = new Vector3(44f, 12f, 44f); probe.boxProjection = true; probe.farClipPlane = 38f; probe.intensity = 1.25f; probe.importance = 10; next = Time.time + 0.6f; watch = 0f; spikes = 0; chk = 0f; near = false; Info = "probe ok";
            }
            catch (Exception e) { Info = "probe fail: " + e.Message; }
        }
        static float chk, watch; static int spikes; static bool near;
        /// <summary>Проба обновляется редко (раз в 2,5 с, по граням в разные кадры) и только если рядом лужа; при просадке FPS отключается сама.</summary>
        public static void Tick(Vector3 pc, float dt)
        {
            if (probe == null) return;
            try
            {
                if (Time.time < watch && Time.unscaledDeltaTime > 0.05f && ++spikes >= 3) { Plugin.Log("Отражения в лужах: просадка FPS при обновлении пробы — отключаю на эту сессию"); On = false; Info = "отключено (FPS)"; Stop(); return; }
                if (Time.time > chk)
                {
                    chk = Time.time + 1f; near = false; var c = Play.Cur;
                    if (c != null) foreach (var f in c.fx) if (f.kind == 5 && (c.baseP + f.pos - pc).sqrMagnitude < 26f * 26f) { near = true; break; }
                }
                probe.transform.position = pc + Vector3.up * 0.9f;
                if (near && Time.time > next) { next = Time.time + 2.5f; watch = Time.time + 1.5f; probe.RenderProbe(); }
            }
            catch { }
        }
        public static void Stop()
        {
            try { if (probe != null) UnityEngine.Object.Destroy(probe.gameObject); probe = null; if (cm != null) UnityEngine.Object.Destroy(cm); cm = null; }
            catch { }
            if (saved) { try { RenderSettings.defaultReflectionMode = oldMode; SetCustom(oldTex); RenderSettings.reflectionIntensity = oldInt; } catch { } saved = false; }
        }
    }

    /// <summary>Атмосфера уровня: пар из труб, капли и рябь, искры, дождь/пыльца, свои «далёкие звуки».</summary>
    internal static class Atmos
    {
        static Built B; static LevelDef L; static GameObject root; static readonly List<ParticleSystem> steam = new List<ParticleSystem>(); static ParticleSystem weather; static float scan, dripT, sparkT; static List<int> nearSteam = new List<int>();

        static ParticleSystem MakePS(string name, Material mat)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false); var ps = go.AddComponent<ParticleSystem>(); var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; return ps;
        }
        public static string[] AmbientSet(string id)
        {
            switch (id)
            {
                case "2": return new[] { "pipe", "hiss", "knock", "creak", "drip", "pipe" };
                case "96": return new[] { "pipe", "step", "creak", "flap", "pipe" };
                case "3": return new[] { "zap", "hiss", "boom", "pipe", "zap" };
                case "4": return new[] { "step", "knock", "creak", "step" };
                case "13": return new[] { "knock", "step", "creak", "knock" };
                case "5": return new[] { "creak", "knock", "step", "ding", "creak" };
                case "37": return new[] { "drip", "splash", "creak", "drip", "hiss" };
                case "6": return new[] { "step", "creak", "step", "hiss" };
                case "19": return new[] { "creak", "step", "flap", "creak", "knock" };
                case "7": return new[] { "splash", "creak", "boom", "drip" };
                case "20": return new[] { "boom", "creak", "step", "knock", "pipe" };
                case "8": return new[] { "drip", "creak", "hiss", "drip", "step" };
                case "52": return new[] { "knock", "step", "creak", "ding" };
                case "9": case "11": case "188": return new[] { "creak", "hiss", "knock", "step" };
                case "15": return new[] { "hiss", "pipe", "knock", "step", "drip" };
                case "94": return new[] { "knock", "creak", "step", "knock", "boom" };
                case "1": return new[] { "step", "boom", "pipe", "drip", "knock" };
                default: return new[] { "knock", "step", "hiss", "pipe", "step", "knock" };
            }
        }
        public static void Start(Built b, LevelDef l)
        {
            Stop(); B = b; L = l; if (b == null) return;
            try
            {
                root = new GameObject("BR_atmos"); bool anySteam = false; foreach (var f in b.fx) if (f.kind == 1) { anySteam = true; break; }
                if (anySteam)
                {
                    var mat = Vfx.Mat(Vfx.Blob(), Color.white);
                    for (int i = 0; i < 12; i++)
                    {
                        var ps = MakePS("steam", mat); var m = ps.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.5f); m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
                        m.startColor = new Color(0.95f, 0.95f, 0.95f, 0.3f); m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = -0.04f; m.maxParticles = 70;
                        var em = ps.emission; em.rateOverTime = 20f; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14f; sh.radius = 0.03f; sh.rotation = new Vector3(90f, 0f, 0f);
                        var col = ps.colorOverLifetime; col.enabled = true; var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.4f, 0.2f), new GradientAlphaKey(0f, 1f) }); col.color = gr;
                        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.8f)); ps.gameObject.SetActive(false); steam.Add(ps);
                    }
                }
                string hk = l.humKind; int mode = hk == "rain" ? 1 : (l.id == "10" ? 2 : (l.id == "19" || l.id == "20" || l.id == "13" ? 3 : 0));
                if (mode > 0)
                {
                    weather = MakePS("weather", Vfx.Mat(Vfx.Blob(), Color.white)); var m = weather.main; var em = weather.emission; var sh = weather.shape; var r = weather.GetComponent<ParticleSystemRenderer>(); sh.shapeType = ParticleSystemShapeType.Box;
                    if (mode == 1) { m.startLifetime = 0.8f; m.startSpeed = 0f; m.startSize = 0.03f; m.startColor = new Color(0.75f, 0.85f, 1f, 0.35f); m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = 3.2f; m.maxParticles = 1500; em.rateOverTime = 900f; sh.scale = new Vector3(26f, 0.1f, 26f); r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 6f; r.velocityScale = 0.04f; }
                    else { m.startLifetime = 9f; m.startSpeed = 0.15f; m.startSize = mode == 2 ? 0.05f : 0.025f; m.startColor = mode == 2 ? new Color(1f, 0.95f, 0.6f, 0.5f) : new Color(1f, 0.92f, 0.8f, 0.35f); m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 400; em.rateOverTime = 45f; sh.scale = new Vector3(22f, 4f, 22f); var vel = weather.velocityOverLifetime; vel.enabled = true; vel.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.45f); vel.y = new ParticleSystem.MinMaxCurve(-0.04f, 0.06f); vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f); }
                }
            }
            catch (Exception e) { Plugin.Warn("atmos", e); }
        }
        public static void Stop()
        {
            try { if (root != null) UnityEngine.Object.Destroy(root); } catch { }
            root = null; steam.Clear(); weather = null; B = null; L = null;
        }
        public static void Tick(float dt, Vector3 pc)
        {
            if (B == null || root == null) return; float now = Time.time;
            if (weather != null) { weather.transform.position = pc + Vector3.up * (L.humKind == "rain" ? 7f : 1.5f); }
            if (now > scan)
            {
                scan = now + 0.8f; nearSteam.Clear(); var cand = new List<KeyValuePair<float, int>>();
                for (int i = 0; i < B.fx.Count; i++) if (B.fx[i].kind == 1) { float d = (B.baseP + B.fx[i].pos - pc).sqrMagnitude; if (d < 34f * 34f) cand.Add(new KeyValuePair<float, int>(d, i)); }
                cand.Sort((a, b) => a.Key.CompareTo(b.Key));
                for (int i = 0; i < steam.Count; i++) { if (i < cand.Count) { steam[i].transform.position = B.baseP + B.fx[cand[i].Value].pos; if (!steam[i].gameObject.activeSelf) { steam[i].gameObject.SetActive(true); steam[i].Play(); } } else if (steam[i].gameObject.activeSelf) steam[i].gameObject.SetActive(false); }
            }
            if (now > dripT)
            {
                dripT = now + UnityEngine.Random.Range(0.6f, 1.6f); int tries = 0;
                while (tries++ < 6) { int i = UnityEngine.Random.Range(0, Mathf.Max(1, B.fx.Count)); if (i >= B.fx.Count || B.fx[i].kind != 2) continue; Vector3 w = B.baseP + B.fx[i].pos; float d = (w - pc).magnitude; if (d > 22f) continue; Vfx.Ring(w + Vector3.up * 0.03f, Vector3.up, new Color(0.8f, 0.9f, 1f, 0.5f), 0.35f, 1.1f); if (d < 14f) Snd2.At("drip", w, 0.25f * (1f - d / 14f) + 0.04f); break; }
            }
            if (now > sparkT && L.id == "3")
            {
                sparkT = now + UnityEngine.Random.Range(1.5f, 4.5f); for (int k = 0; k < 8; k++) { int i = UnityEngine.Random.Range(0, Mathf.Max(1, B.fx.Count)); if (i >= B.fx.Count || B.fx[i].kind != 3) continue; Vector3 w = B.baseP + B.fx[i].pos; float d = (w - pc).magnitude; if (d > 20f) continue; Vfx.Sparks(w, Vector3.down, 10, new Color(0.7f, 0.85f, 1f)); Snd2.At("zap", w, Mathf.Clamp01(1f - d / 20f) * 0.35f); break; }
            }
        }
    }
}
