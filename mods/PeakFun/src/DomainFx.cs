// Визуал «Расширения территории» 1.11: купол с небом, частицы, граница; у Морока — тронный зал из костей,
// у Альбус — космос «Безграничной пустоты». Всё рисуется кодом и показывается тем, у кого стоит PeakFun.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    internal sealed class DomainFx
    {
        public HeroKind kind; public Vector3 c; public float R, dur; public bool Done, shaded; public int owner = -1; public bool Collapsing { get { return collapsing; } } float slashAcc;
        float t0, tc = -1f; bool collapsing;
        GameObject root, dome, shrine; Material domeMat; LineRenderer ring; ParticleSystem ps;
        readonly List<Transform> rings = new List<Transform>();
        readonly List<LineRenderer> ringLr = new List<LineRenderer>();
        float emitAcc;

        // ---------- текстуры ----------
        static Texture2D cosmos, crimson;
        static float Hash(float x, float y) { float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }
        static float Noise(float u, float v, float fu, float fv, float off)
        {
            float a = u * Mathf.PI * 2f;
            return Mathf.PerlinNoise(Mathf.Cos(a) * fu + 10f + off, Mathf.Sin(a) * fu + v * fv + off);
        }

        static Texture2D Cosmos()
        {
            if (cosmos != null) return cosmos;
            cosmos = Vfx.Tex(512, 256, (u, v) =>
            {
                float top = 1f - v;   // 1 — зенит (у сферы v=0 вверху)
                float n = Noise(u, v, 2.2f, 3f, 0f) * 0.6f + Noise(u, v, 4.5f, 7f, 7f) * 0.4f;
                Color c = Color.Lerp(new Color(0.01f, 0.01f, 0.05f), new Color(0.28f, 0.08f, 0.55f), Mathf.Clamp01(Mathf.Pow(n, 2.4f) * 1.8f));
                float band = Mathf.Exp(-Mathf.Pow((top - 0.5f - 0.12f * Mathf.Sin(u * 12.566f)) / 0.06f, 2f));
                c += new Color(0.1f, 0.45f, 0.8f) * band * 0.9f;
                float gx = Mathf.Floor(u * 240f), gy = Mathf.Floor(v * 120f), h = Hash(gx, gy);
                if (h > 0.955f)
                {
                    float fx = u * 240f - gx - 0.5f, fy = v * 120f - gy - 0.5f;
                    c += Color.white * Mathf.Exp(-(fx * fx + fy * fy) * 16f) * (h - 0.955f) * 30f;
                }
                c.a = 1f; return c;
            });
            cosmos.wrapMode = TextureWrapMode.Repeat;
            return cosmos;
        }

        static Texture2D Crimson()
        {
            if (crimson != null) return crimson;
            crimson = Vfx.Tex(512, 256, (u, v) =>
            {
                float top = 1f - v;
                float hor = 1f - Mathf.Abs(top - 0.5f) * 2f;   // 1 на горизонте
                float n = Noise(u, v, 2.5f, 4f, 3f) * 0.65f + Noise(u, v, 6f, 9f, 11f) * 0.35f;
                Color c = Color.Lerp(new Color(0.02f, 0f, 0.015f), new Color(0.55f, 0.02f, 0.03f), Mathf.Clamp01(hor * hor * (0.5f + n)));
                c *= (0.55f + 0.7f * n);
                float dx = (u - 0.3f) * 2f, dy = top - 0.72f, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 0.075f) c = Color.Lerp(c, new Color(0.95f, 0.82f, 0.78f), Mathf.Clamp01((0.075f - d) / 0.012f));
                else if (d < 0.16f) c += new Color(0.5f, 0.05f, 0.05f) * (0.16f - d) * 3f;
                float s = Mathf.Abs(Mathf.Sin((u * 30f + v * 14f) * 1.0f));
                if (Hash(Mathf.Floor(u * 40f), Mathf.Floor(v * 20f)) > 0.93f && s < 0.06f) c += new Color(0.9f, 0.7f, 0.7f) * 0.5f;
                c.a = 1f; return c;
            });
            crimson.wrapMode = TextureWrapMode.Repeat;
            return crimson;
        }

        // ---------- построение ----------
        GameObject Box(Transform parent, Vector3 pos, Vector3 scale, Color col, Quaternion? rot = null)
        {
            var g = new GameObject("b"); g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
            if (rot.HasValue) g.transform.localRotation = rot.Value;
            g.AddComponent<MeshFilter>().sharedMesh = Destruct.Cube();
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = Vfx.Mat(null, col); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        GameObject Ball(Transform parent, Vector3 pos, Vector3 scale, Color col)
        {
            var g = new GameObject("s"); g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = Vfx.Sphere();
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = Vfx.Mat(null, col); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        float GroundY(Vector3 p)
        {
            RaycastHit h;
            if (Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out h, 120f, ~0, QueryTriggerInteraction.Ignore)) return h.point.y;
            return p.y;
        }

        public DomainFx(HeroKind k, Vector3 center, float radius, float duration, Vector3 face)
        {
            kind = k; c = center; R = radius; dur = duration; t0 = Time.time;
            root = new GameObject("PF_Domain"); UnityEngine.Object.DontDestroyOnLoad(root); root.transform.position = c;
            if (Vfx.Sh() == null) { Done = true; return; }
            bool gojo = k == HeroKind.Gojo;
            dome = new GameObject("dome"); dome.transform.SetParent(root.transform, false);
            dome.AddComponent<MeshFilter>().sharedMesh = Vfx.Sphere();
            var mr = dome.AddComponent<MeshRenderer>(); domeMat = Vfx.Mat(gojo ? Cosmos() : Crimson(), new Color(1, 1, 1, 0));
            mr.sharedMaterial = domeMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            dome.transform.localScale = Vector3.one * R * 0.4f;

            // граница на земле
            var rg = new GameObject("ring"); rg.transform.SetParent(root.transform, false);
            ring = rg.AddComponent<LineRenderer>(); ring.material = Vfx.Mat(null, Color.white); ring.loop = true; ring.useWorldSpace = true; ring.positionCount = 96; ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.startWidth = ring.endWidth = 0.35f;
            for (int i = 0; i < 96; i++)
            {
                float a = i * Mathf.PI * 2f / 96f; Vector3 q = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * R * 0.97f; q.y = GroundY(q) + 0.2f; ring.SetPosition(i, q);
            }

            // частицы
            var pgo = new GameObject("ps"); pgo.transform.SetParent(root.transform, false);
            ps = pgo.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 700; main.gravityModifier = gojo ? 0f : -0.12f;
            var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
            if (gojo) g.SetKeys(new[] { new GradientColorKey(new Color(0.8f, 0.95f, 1f), 0), new GradientColorKey(new Color(0.5f, 0.4f, 1f), 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0, 1) });
            else g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.55f, 0.2f), 0), new GradientColorKey(new Color(0.5f, 0.02f, 0.02f), 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var pr = pgo.GetComponent<ParticleSystemRenderer>(); pr.material = Vfx.Mat(Vfx.Blob(), Color.white);
            if (gojo) { pr.renderMode = ParticleSystemRenderMode.Stretch; pr.velocityScale = 0.05f; pr.lengthScale = 3f; }
            ps.Play();

            if (gojo)
            {
                // орбитальные кольца
                for (int i = 0; i < 3; i++)
                {
                    var rgo = new GameObject("orbit"); rgo.transform.SetParent(root.transform, false);
                    rgo.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(0f, 80f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 80f));
                    var lr = rgo.AddComponent<LineRenderer>(); lr.material = Vfx.Mat(null, Color.white); lr.useWorldSpace = false; lr.loop = true; lr.positionCount = 80; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    float rr = R * (0.55f + 0.12f * i);
                    for (int j = 0; j < 80; j++) { float a = j * Mathf.PI * 2f / 80f; lr.SetPosition(j, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr); }
                    lr.startWidth = lr.endWidth = 0.12f; rings.Add(rgo.transform); ringLr.Add(lr);
                    Ball(rgo.transform, new Vector3(rr, 0f, 0f), Vector3.one * 1.2f, new Color(0.7f, 0.9f, 1f));
                }
            }
            else BuildShrine(face);
        }

        void BuildShrine(Vector3 face)
        {
            face.y = 0f; if (face.sqrMagnitude < 0.01f) face = Vector3.forward; face.Normalize();
            Vector3 sp = c - face * Mathf.Min(R * 0.6f, 17f); sp.y = GroundY(sp) - 0.2f;
            shrine = new GameObject("shrine"); shrine.transform.SetParent(root.transform, false); shrine.transform.position = sp; shrine.transform.rotation = Quaternion.LookRotation(face);
            var t = shrine.transform;
            Color stone = new Color(0.13f, 0.1f, 0.11f), red = new Color(0.4f, 0.04f, 0.06f), blk = new Color(0.05f, 0.03f, 0.04f), bone = new Color(0.92f, 0.9f, 0.84f);
            Box(t, new Vector3(0, 0.4f, 0), new Vector3(17, 0.8f, 13), stone);
            Box(t, new Vector3(0, 0.15f, 7.4f), new Vector3(9, 0.5f, 2.2f), stone);
            foreach (float x in new[] { -6.5f, -3.2f, 3.2f, 6.5f })
                foreach (float z in new[] { -4.5f, 3.5f })
                    Box(t, new Vector3(x, 4.4f, z), new Vector3(0.8f, 7.2f, 0.8f), red);
            Box(t, new Vector3(0, 4.5f, -6f), new Vector3(15, 7.2f, 0.6f), blk);
            Box(t, new Vector3(0, 8.3f, 0), new Vector3(16.5f, 0.8f, 13f), blk);
            Box(t, new Vector3(0, 9.5f, 0), new Vector3(12.5f, 0.8f, 9.5f), red);
            Box(t, new Vector3(0, 10.7f, 0), new Vector3(8f, 0.8f, 6f), blk);
            Box(t, new Vector3(0, 11.7f, 0), new Vector3(4.5f, 1.1f, 3.2f), red);
            Box(t, new Vector3(-8.4f, 8.9f, 6.6f), new Vector3(1.6f, 0.4f, 1.6f), blk, Quaternion.Euler(0, 0, 25));
            Box(t, new Vector3(8.4f, 8.9f, 6.6f), new Vector3(1.6f, 0.4f, 1.6f), blk, Quaternion.Euler(0, 0, -25));
            for (int i = 0; i < 11; i++) Ball(t, new Vector3(-7f + i * 1.4f, 8.7f, 6.8f), Vector3.one * 0.85f, bone);
            Ball(t, new Vector3(0, 12.8f, 0.4f), new Vector3(2.4f, 2f, 2.2f), bone);
            Box(t, new Vector3(-1.8f, 13.5f, 0.4f), new Vector3(2.6f, 0.45f, 0.45f), bone, Quaternion.Euler(0, 0, 35));
            Box(t, new Vector3(1.8f, 13.5f, 0.4f), new Vector3(2.6f, 0.45f, 0.45f), bone, Quaternion.Euler(0, 0, -35));
            Ball(t, new Vector3(-0.55f, 12.9f, 1.4f), new Vector3(0.5f, 0.6f, 0.3f), blk); Ball(t, new Vector3(0.55f, 12.9f, 1.4f), new Vector3(0.5f, 0.6f, 0.3f), blk);
            // черепа и кости на земле вокруг
            for (int i = 0; i < 26; i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f, r = Mathf.Sqrt(UnityEngine.Random.value) * R * 0.85f;
                Vector3 q = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r; q.y = GroundY(q) + 0.15f;
                var g = new GameObject("skull"); g.transform.SetParent(root.transform, false); g.transform.position = q; g.transform.rotation = Quaternion.Euler(0, UnityEngine.Random.value * 360f, 0);
                float s = UnityEngine.Random.Range(0.35f, 0.7f);
                Ball(g.transform, Vector3.zero, new Vector3(s, s * 0.9f, s * 1.05f), bone);
                Box(g.transform, new Vector3(0, -s * 0.28f, s * 0.35f), new Vector3(s * 0.55f, s * 0.25f, s * 0.4f), bone);
                if (i % 3 == 0) Box(g.transform, new Vector3(s, 0, 0), new Vector3(s * 2.2f, s * 0.2f, s * 0.2f), bone, Quaternion.Euler(0, UnityEngine.Random.value * 90f, 0));
            }
        }

        // ---------- анимация ----------
        public void Collapse() { if (collapsing) return; collapsing = true; tc = Time.time; }
        public void Destroy() { if (root != null) UnityEngine.Object.Destroy(root); root = null; Done = true; }
        public bool Expired { get { return Time.time - t0 > dur + 1.5f; } }

        public void Tick()
        {
            if (Done || root == null) { Done = true; return; }
            float age = Time.time - t0; bool gojo = kind == HeroKind.Gojo;
            float a = 1f, s = 1f;
            if (collapsing)
            {
                float k = (Time.time - tc) / 0.8f; if (k >= 1f) { Destroy(); return; }
                a = 1f - k; s = 1f + 0.35f * k;
            }
            else
            {
                float k = Mathf.Clamp01(age / 0.9f); float e = 1f - Mathf.Pow(1f - k, 3f);
                a = Mathf.Clamp01(age / 0.5f); s = Mathf.Lerp(0.4f, 1f, e);
                if (age > dur) Collapse();
            }
            dome.transform.localScale = Vector3.one * R * 2f * s;
            dome.transform.rotation = Quaternion.Euler(0f, Time.time * (gojo ? 6f : 2f), 0f);
            var cc = domeMat.color; cc.a = a; domeMat.color = cc;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 3f);
            var rc = gojo ? new Color(0.6f, 0.9f, 1f, a * (0.5f + 0.4f * pulse)) : new Color(1f, 0.1f, 0.1f, a * (0.5f + 0.4f * pulse));
            ring.startColor = ring.endColor = rc;
            for (int i = 0; i < rings.Count; i++)
            {
                rings[i].Rotate(0f, (i + 1) * 12f * Time.deltaTime, 0f, Space.Self);
                var lc = new Color(0.6f, 0.85f, 1f, a * 0.7f); ringLr[i].startColor = ringLr[i].endColor = lc;
            }
            if (!collapsing && !gojo)
            {
                slashAcc += Time.deltaTime * 5f;
                while (slashAcc >= 1f)
                {
                    slashAcc -= 1f;
                    Vector3 a0 = c + UnityEngine.Random.insideUnitSphere * R * 0.8f; a0.y = c.y + Mathf.Abs(a0.y - c.y) * 0.6f;
                    Vector3 d0 = UnityEngine.Random.onUnitSphere; Vector3 b0 = a0 + d0 * R * UnityEngine.Random.Range(0.5f, 1.1f);
                    AnimVfx.Line(new[] { a0, b0 }, UnityEngine.Random.value < 0.5f ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 0.2f, 0.2f, 0.9f), UnityEngine.Random.Range(0.12f, 0.4f), 0.35f);
                }
            }
            if (!collapsing)
            {
                emitAcc += Time.deltaTime * (gojo ? 90f : 50f);
                int n = (int)emitAcc; emitAcc -= n;
                for (int i = 0; i < n; i++)
                {
                    var ep = new ParticleSystem.EmitParams();
                    Vector3 rp = UnityEngine.Random.insideUnitSphere * R * 0.9f; if (!gojo) rp.y = Mathf.Abs(rp.y) * 0.4f;
                    ep.position = c + rp;
                    if (gojo) { ep.velocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(14f, 34f); ep.startSize = UnityEngine.Random.Range(0.05f, 0.12f); ep.startLifetime = UnityEngine.Random.Range(0.8f, 1.6f); }
                    else { ep.velocity = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(1.5f, 5f), UnityEngine.Random.Range(-1f, 1f)); ep.startSize = UnityEngine.Random.Range(0.12f, 0.35f); ep.startLifetime = UnityEngine.Random.Range(2f, 4f); }
                    ep.startColor = Color.white; ps.Emit(ep, 1);
                }
            }
        }
    }

    public partial class PeakFunPlugin
    {
        // ---------- заставка ----------
        string banTitle = "", banSub = ""; float banStart, banUntil; Color banCol = Color.white;

        void ShowBanner(string title, string sub, Color col, float dur)
        { banTitle = title; banSub = sub; banStart = Time.time; banUntil = Time.time + dur; banCol = col; }

        void DrawDomainBanner()
        {
            if (Event.current.type != EventType.Repaint || Time.time >= banUntil) return;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f);
            float t = Time.time - banStart, tot = banUntil - banStart;
            float a = Mathf.Clamp01(Mathf.Min(t / 0.35f, (tot - t) / 0.5f));
            float bars = Mathf.Clamp01(Mathf.Min(t / 0.4f, (tot - t) / 0.4f)) * Screen.height * 0.12f;
            Ui.Box(new Rect(0, 0, Screen.width, bars), new Color(0, 0, 0, 0.92f), 0f);
            Ui.Box(new Rect(0, Screen.height - bars, Screen.width, bars), new Color(0, 0, 0, 0.92f), 0f);
            float pop = 1f + 0.12f * Mathf.Exp(-t * 4f);
            var c = banCol; c.a = a;
            Ui.Text(new Rect(0, Screen.height * 0.38f, Screen.width, 50 * s), banTitle, (int)(30 * s), new Color(1, 1, 1, a * 0.9f), TextAnchor.MiddleCenter, true);
            Ui.Text(new Rect(0, Screen.height * 0.38f + 44 * s, Screen.width, 100 * s), banSub, (int)(64 * s * pop), c, TextAnchor.MiddleCenter, true);
        }
    }
}
