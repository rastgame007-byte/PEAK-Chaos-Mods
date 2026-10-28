// Брызги и пена при ходьбе по воде (1.30): плоские кольца пены (быстро растут и тают), капли с гравитацией, лёгкая водяная пыль.
using System;
using UnityEngine;

namespace PeakBR
{
    internal static class Splash
    {
        const int N = 18;
        static GameObject root; static Transform[] tr; static Material[] mt; static float[] age, life, s0, s1, a0; static bool[] act; static int nxt;
        static ParticleSystem drops, mist; static Texture2D ringTex; static Mesh quad; static float lastWake;

        static Mesh Quad()
        {
            if (quad != null) return quad; quad = new Mesh { name = "BR_foam_quad" };
            quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 }; quad.RecalculateBounds(); return quad;
        }
        static Texture2D Ring()
        {
            if (ringTex != null) return ringTex;
            ringTex = Vfx.Tex(128, 128, (u, v) =>
            {
                float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f, d = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx);
                float ring = Mathf.Exp(-Mathf.Pow((d - 0.78f) / 0.11f, 2f)); float inner = Mathf.Exp(-Mathf.Pow((d - 0.5f) / 0.07f, 2f)) * 0.35f;
                float n = 0.5f + 0.5f * Mathf.PerlinNoise(Mathf.Cos(ang) * 2.2f + 5f, Mathf.Sin(ang) * 2.2f + 5f); float f = 0.4f + 0.6f * Mathf.PerlinNoise(u * 14f, v * 14f);
                float a = (ring * (0.35f + 0.65f * n) + inner) * f * Mathf.Clamp01((1f - d) * 7f);
                return new Color(0.9f, 0.97f, 1f, Mathf.Clamp01(a));
            });
            return ringTex;
        }
        static ParticleSystem MakePs(string name, Texture tex, int max, float grav)
        {
            var g = new GameObject(name); g.transform.SetParent(root.transform, false); var ps = g.AddComponent<ParticleSystem>(); var pr = g.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = Vfx.Mat(tex, Color.white); pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
            var m = ps.main; m.loop = false; m.playOnAwake = false; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = max; m.gravityModifier = grav; m.startLifetime = 0.6f; m.startSpeed = 0f;
            var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false; ps.Play(); return ps;
        }
        static void Ensure()
        {
            if (root != null) return;
            root = new GameObject("BR_splash"); tr = new Transform[N]; mt = new Material[N]; age = new float[N]; life = new float[N]; s0 = new float[N]; s1 = new float[N]; a0 = new float[N]; act = new bool[N];
            for (int i = 0; i < N; i++)
            {
                var g = new GameObject("foam"); g.transform.SetParent(root.transform, false); g.AddComponent<MeshFilter>().sharedMesh = Quad(); var r = g.AddComponent<MeshRenderer>();
                mt[i] = Vfx.Mat(Ring(), new Color(1f, 1f, 1f, 0f)); r.sharedMaterial = mt[i]; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; g.SetActive(false); tr[i] = g.transform;
            }
            drops = MakePs("drops", Vfx.Blob(), 500, 1.25f); mist = MakePs("mist", Vfx.Blob(), 120, -0.05f);
        }
        static void Foam(Vector3 p, float size0, float size1, float alpha, float time)
        {
            int i = nxt; nxt = (nxt + 1) % N; act[i] = true; age[i] = 0f; life[i] = time; s0[i] = size0; s1[i] = size1; a0[i] = alpha;
            tr[i].position = p + Vector3.up * (0.045f + 0.004f * i); tr[i].rotation = Quaternion.Euler(0f, UnityEngine.Random.value * 360f, 0f); tr[i].localScale = new Vector3(size0, 1f, size0); tr[i].gameObject.SetActive(true);
        }
        static void Drops(Vector3 p, int n, float up, float outv, float rad)
        {
            const float g = 9.81f * 1.25f;
            for (int i = 0; i < n; i++)
            {
                var ep = new ParticleSystem.EmitParams(); Vector2 d = UnityEngine.Random.insideUnitCircle.normalized; float vy = up * UnityEngine.Random.Range(0.55f, 1.2f); float vo = outv * UnityEngine.Random.Range(0.4f, 1f);
                ep.position = p + new Vector3(d.x, 0f, d.y) * UnityEngine.Random.Range(0.05f, rad) + Vector3.up * 0.03f; ep.velocity = new Vector3(d.x * vo, vy, d.y * vo);
                ep.startSize = UnityEngine.Random.Range(0.022f, 0.065f); ep.startLifetime = 2f * vy / g * 0.97f; ep.startColor = new Color(0.82f, 0.94f, 1f, 0.9f);
                drops.Emit(ep, 1);
            }
        }
        static void Mist(Vector3 p, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var ep = new ParticleSystem.EmitParams(); Vector2 d = UnityEngine.Random.insideUnitCircle;
                ep.position = p + new Vector3(d.x * 0.25f, 0.06f, d.y * 0.25f); ep.velocity = new Vector3(d.x * 0.5f, UnityEngine.Random.Range(0.25f, 0.6f), d.y * 0.5f);
                ep.startSize = UnityEngine.Random.Range(0.18f, 0.34f); ep.startLifetime = UnityEngine.Random.Range(0.6f, 1.0f); ep.startColor = new Color(0.9f, 0.97f, 1f, 0.16f);
                mist.Emit(ep, 1);
            }
        }

        /// <summary>Шаг: расходящееся кольцо, ещё одно запаздывающее, фонтанчик капель и водяная пыль.</summary>
        public static void Step(Vector3 w, float k)
        {
            try
            {
                Ensure(); float q = Play.Qual >= 3 ? 0.5f : 1f;
                Foam(w, 0.25f, 1.5f + k * 1.1f, 0.85f, 1.0f + k * 0.3f); Foam(w, 0.15f, 2.4f + k * 1.4f, 0.4f, 1.7f);
                Drops(w, (int)((9f + k * 12f) * q), 1.9f + k * 1.6f, 0.9f + k * 1.2f, 0.28f); Mist(w, (int)((3f + k * 3f) * q));
            }
            catch (Exception e) { Plugin.Warn("брызги", e); }
        }
        /// <summary>След за ногами при движении: мелкие кольца и пара капель; на бегу — «носовая волна» впереди.</summary>
        public static void Wake(Vector3 w, Vector3 dir, float spd)
        {
            try
            {
                Ensure(); float t = Time.time; if (t < lastWake) return; lastWake = t + (spd > 3.5f ? 0.16f : 0.3f);
                Vector3 p = w + (spd > 3.5f ? dir * 0.35f : Vector3.zero);
                Foam(p, 0.2f, spd > 3.5f ? 1.5f : 0.9f, spd > 3.5f ? 0.5f : 0.34f, 0.9f); Drops(p, spd > 3.5f ? 4 : 2, 1.0f + spd * 0.12f, 0.5f, 0.2f);
            }
            catch { }
        }

        public static void Tick(float dt)
        {
            if (root == null) return;
            for (int i = 0; i < N; i++)
            {
                if (!act[i]) continue; age[i] += dt; float k = age[i] / life[i];
                if (k >= 1f) { act[i] = false; tr[i].gameObject.SetActive(false); continue; }
                float e = 1f - (1f - k) * (1f - k); float s = Mathf.Lerp(s0[i], s1[i], e); tr[i].localScale = new Vector3(s, 1f, s);
                float a = a0[i] * Mathf.Pow(1f - k, 1.4f) * Mathf.Clamp01(k * 14f); if (mt[i] != null) mt[i].color = new Color(1f, 1f, 1f, a);
            }
        }
        public static void Clear()
        {
            if (root != null) UnityEngine.Object.Destroy(root); root = null; tr = null; mt = null; drops = null; mist = null; nxt = 0; lastWake = 0f;
        }
    }
}
