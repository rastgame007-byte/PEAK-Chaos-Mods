// «Кровь» при ударах 1.9: брызги-капли (частицы, которые на самом деле сталкиваются с миром), туман, пятна на стенах/земле/телах,
// лужи, кратеры. Цвет и густота настраиваются; можно заменить на зелёную/синюю/конфетти. Друзьям с PeakFun событие уходит по сети,
// у остальных этого просто нет (визуал локальный).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    internal sealed class GoreHit : MonoBehaviour
    {
        public ParticleSystem ps; readonly List<ParticleCollisionEvent> ev = new List<ParticleCollisionEvent>();
        void OnParticleCollision(GameObject other)
        {
            if (ps == null) return;
            int n = ps.GetCollisionEvents(other, ev);
            int cap = Mathf.Min(n, 3);
            for (int i = 0; i < cap; i++) Gore.Decal(ev[i].intersection, ev[i].normal, other != null ? other.transform : null, -1f, 0.9f);
        }
    }

    internal static class Gore
    {
        public static bool On = true;
        public static float Level = 2f;           // 0.5..3
        public static int Palette;                // 0 красная, 1 тёмная, 2 зелёная, 3 синяя, 4 конфетти
        public static string[] PaletteNames = { "Красная", "Тёмно-бордовая", "Зелёная (инопланетная)", "Синяя", "Конфетти (безобидно)" };

        static ParticleSystem spray, mist; static bool inited;
        static readonly System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
        static float lastNet; static Type charT;

        public static Color Pick(int pal)
        {
            switch (pal)
            {
                case 1: return new Color(0.32f, 0.01f, 0.03f, 1f);
                case 2: return new Color(0.25f, 0.85f, 0.15f, 1f);
                case 3: return new Color(0.15f, 0.45f, 1f, 1f);
                case 4: return Color.HSVToRGB(UnityEngine.Random.value, 0.85f, 1f);
            }
            float r = UnityEngine.Random.Range(0.55f, 0.85f);
            return new Color(r, 0.02f, 0.03f, 1f);
        }

        static ParticleSystem Make(string name, bool collide, bool stretch)
        {
            var go = new GameObject(name); UnityEngine.Object.DontDestroyOnLoad(go);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 2500;
            main.gravityModifier = collide ? 1.3f : 0f;
            var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.material = Vfx.Mat(Vfx.Blob(), Color.white);
            if (stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 1.8f; r.velocityScale = 0.06f; }
            if (collide)
            {
                var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.mode = ParticleSystemCollisionMode.Collision3D;
                col.bounce = 0f; col.dampen = 1f; col.lifetimeLoss = 1f; col.maxCollisionShapes = 128; col.radiusScale = 0.6f; col.collidesWith = ~0;
                col.quality = ParticleSystemCollisionQuality.Medium; col.sendCollisionMessages = true;
                var gh = go.AddComponent<GoreHit>(); gh.ps = ps;
            }
            else
            {
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 1.5f));
                var cl = ps.colorOverLifetime; cl.enabled = true;
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.7f, 0), new GradientAlphaKey(0, 1) });
                cl.color = g;
            }
            ps.Play();
            return ps;
        }

        static void Init()
        {
            if (inited) return;
            inited = true;
            charT = R.FindType("Character");
            if (Vfx.Sh() == null) return;
            spray = Make("PF_Blood", true, true); mist = Make("PF_BloodMist", false, false);
        }

        // ---------- пятна (декали) ----------
        sealed class D { public GameObject go; public Mesh mesh; public MeshRenderer mr; public float born, life, size; public bool grow; public Color col; public bool fading; }
        static readonly List<D> pool = new List<D>(); static int poolMax = 260; static int next;
        static Material[] splatMat; static Material crackMat;

        static D NewDecal()
        {
            if (pool.Count < poolMax)
            {
                var go = new GameObject("PF_Decal"); UnityEngine.Object.DontDestroyOnLoad(go);
                var mf = go.AddComponent<MeshFilter>(); var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                var m = UnityEngine.Object.Instantiate(Vfx.Quad()); mf.sharedMesh = m;
                var d = new D { go = go, mesh = m, mr = mr }; pool.Add(d); return d;
            }
            var o = pool[next]; next = (next + 1) % pool.Count; o.go.transform.SetParent(null, false); return o;
        }

        static bool IsLiving(Transform t)
        {
            if (t == null) return false;
            if (charT == null) charT = R.FindType("Character");
            return charT != null && t.GetComponentInParent(charT) != null;
        }

        /// <summary>Пятно на поверхности. size &lt; 0 — случайный.</summary>
        public static void Decal(Vector3 p, Vector3 n, Transform parent, float size, float sizeMul, bool grow = false, bool crack = false, float life = 55f, Color? tint = null)
        {
            if (!On && !crack) return;
            try
            {
                if (Vfx.Sh() == null) return;
                var d = NewDecal();
                if (crack) { if (crackMat == null) crackMat = Vfx.Mat(Vfx.Crack(), Color.white); d.mr.sharedMaterial = crackMat; }
                else
                {
                    if (splatMat == null) { splatMat = new Material[4]; for (int i = 0; i < 4; i++) splatMat[i] = Vfx.Mat(Vfx.Splat(i), Color.white); }
                    d.mr.sharedMaterial = splatMat[UnityEngine.Random.Range(0, 4)];
                }
                d.col = crack ? new Color(0.12f, 0.1f, 0.09f, 0.95f) : (tint ?? Pick(Palette)); if (!crack && Palette != 4) d.col.a = 0.93f;
                d.born = Time.time; d.life = life; d.grow = grow; d.fading = false;
                d.size = size > 0 ? size : UnityEngine.Random.Range(0.18f, 0.6f) * sizeMul;
                var t = d.go.transform; t.SetParent(null, false);
                t.position = p + n * 0.02f;
                t.rotation = Quaternion.LookRotation(-n, Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up) * Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));
                t.localScale = Vector3.one * (grow ? d.size * 0.3f : d.size);
                if (parent != null && IsLiving(parent) && !crack)
                {
                    t.SetParent(parent, true);
                    var ls = parent.lossyScale; t.localScale = new Vector3(d.size / Mathf.Max(0.05f, ls.x), d.size / Mathf.Max(0.05f, ls.y), d.size / Mathf.Max(0.05f, ls.z));
                    d.grow = false;
                }
                var cols = new[] { d.col, d.col, d.col, d.col }; d.mesh.colors = cols;
                d.go.SetActive(true);
            }
            catch (Exception e) { Debug.LogWarning("[PeakFun] декаль: " + e.Message); }
        }

        public static void Tick()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var d = pool[i];
                if (d.go == null || !d.go.activeSelf) continue;
                float age = Time.time - d.born;
                if (age >= d.life) { d.go.SetActive(false); continue; }
                if (d.grow && age < 3f) { float k = 1f - Mathf.Pow(1f - age / 3f, 2f); d.go.transform.localScale = Vector3.one * d.size * Mathf.Lerp(0.3f, 1f, k); }
                if (age > d.life - 6f)
                {
                    float a = (d.life - age) / 6f; var c = d.col; c.a *= a;
                    d.mesh.colors = new[] { c, c, c, c };
                }
            }
        }

        public static void Clear() { foreach (var d in pool) if (d.go != null) d.go.SetActive(false); }

        // ---------- взрыв брызг ----------
        /// <summary>Брызги крови из точки pos в сторону dir. power ~ 0.3 (укол) … 2.5 (удар в стену).</summary>
        public static void Burst(Vector3 pos, Vector3 dir, float power, bool net = true)
        {
            if (!On) return;
            try
            {
                Init(); if (spray == null) return;
                float k = Mathf.Max(0.15f, power) * Level * 0.5f;
                dir = dir.sqrMagnitude < 0.01f ? Vector3.up : dir.normalized;
                int n = Mathf.Clamp(Mathf.RoundToInt(34 * k), 3, 220);
                for (int i = 0; i < n; i++)
                {
                    var ep = new ParticleSystem.EmitParams();
                    Vector3 v = (dir * UnityEngine.Random.Range(2f, 9f) + UnityEngine.Random.insideUnitSphere * 4f + Vector3.up * UnityEngine.Random.Range(0f, 3f)) * (0.6f + 0.5f * Mathf.Sqrt(power));
                    ep.position = pos + UnityEngine.Random.insideUnitSphere * 0.15f; ep.velocity = v;
                    ep.startSize = UnityEngine.Random.Range(0.05f, 0.16f) * (0.8f + 0.2f * power); ep.startLifetime = 2.2f; ep.startColor = Pick(Palette);
                    spray.Emit(ep, 1);
                }
                int m = Mathf.Clamp(Mathf.RoundToInt(9 * k), 2, 40);
                for (int i = 0; i < m; i++)
                {
                    var ep = new ParticleSystem.EmitParams();
                    ep.position = pos + UnityEngine.Random.insideUnitSphere * 0.2f; ep.velocity = dir * UnityEngine.Random.Range(0.5f, 2.5f) + UnityEngine.Random.insideUnitSphere * 1.2f;
                    ep.startSize = UnityEngine.Random.Range(0.35f, 0.85f) * (0.8f + 0.3f * power); ep.startLifetime = UnityEngine.Random.Range(0.4f, 0.8f); ep.startColor = Pick(Palette);
                    mist.Emit(ep, 1);
                }
                // лужа под жертвой при сильном ударе
                if (power >= 1.1f)
                {
                    RaycastHit h;
                    if (Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out h, 4f, ~0, QueryTriggerInteraction.Ignore) && !IsLiving(h.collider.transform))
                        Decal(h.point, h.normal, null, 0.7f * Mathf.Sqrt(power) * (0.7f + 0.3f * Level), 1f, true, false, 70f);
                }
                Sfx2.Splat(Mathf.Clamp01(power / 2f));
                if (net && Time.time - lastNet > 0.12f && Game.InRoom)
                {
                    lastNet = Time.time;
                    Net.Send("gore", string.Format(inv, "{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7}", pos.x, pos.y, pos.z, dir.x, dir.y, dir.z, power, Palette));
                }
            }
            catch (Exception e) { Debug.LogWarning("[PeakFun] кровь: " + e.Message); }
        }

        public static void OnNet(string arg)
        {
            try
            {
                var p = arg.Split(',');
                if (p.Length < 8) return;
                Func<int, float> f = i => float.Parse(p[i], inv);
                int pal = Palette; Palette = int.Parse(p[7]);
                Burst(new Vector3(f(0), f(1), f(2)), new Vector3(f(3), f(4), f(5)), f(6), false);
                Palette = pal;
            }
            catch { }
        }

        /// <summary>Кратер + пыль + кольцо: приземление героя, удар об землю.</summary>
        public static void Impact(Vector3 point, Vector3 normal, float power, bool net = true)
        {
            try
            {
                Decal(point, normal, null, 1.4f * Mathf.Sqrt(power) + 0.4f, 1f, false, true, 100f);
                Vfx.Dust(point + normal * 0.1f, 2.5f + power * 2f, (int)(18 + power * 12));
                Vfx.Ring(point + normal * 0.15f, normal, new Color(1f, 0.95f, 0.8f, 0.9f), 5f + power * 5f, 0.6f);
                Destruct.Chips(point, normal, power);
                if (net && Game.InRoom) Net.Send("ring", string.Format(inv, "{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2}", point.x, point.y, point.z, normal.x, normal.y, normal.z, power));
            }
            catch (Exception e) { Debug.LogWarning("[PeakFun] кратер: " + e.Message); }
        }

        public static void OnNetRing(string arg)
        {
            try
            {
                var p = arg.Split(','); Func<int, float> f = i => float.Parse(p[i], inv);
                Impact(new Vector3(f(0), f(1), f(2)), new Vector3(f(3), f(4), f(5)), f(6), false);
            }
            catch { }
        }
    }
}
