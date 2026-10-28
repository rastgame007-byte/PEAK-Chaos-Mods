// Разрушение 1.10: сильные удары реально ломают объекты карты — коллайдер отключается, модель прячется, вместо неё летят обломки.
// Земля и крупные скалы (больше «макс. размера») так сломать нельзя — на них остаются кратеры, трещины и осыпь.
// Всё восстанавливается: по таймеру или кнопкой «Восстановить всё». Друзья с PeakFun видят то же самое.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    internal static class Destruct
    {
        public static bool On = true, Protect = true;
        public static float MaxSize = 6f, RestoreAfter = 120f;
        static readonly System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;

        sealed class Broken { public Collider col; public Renderer[] rs; public float t; }
        sealed class Deb { public GameObject go; public float born, life; public Vector3 sc; }
        static readonly List<Broken> broken = new List<Broken>();
        static readonly List<Deb> debris = new List<Deb>();
        const int MaxDebris = 150;
        static Mesh cube; static int dLayer = -1; static bool layerDone; static float lastNet;
        static readonly Dictionary<int, Material> mats = new Dictionary<int, Material>();
        static readonly string[] skip = { "campfire", "checkpoint", "summit", "flag", "airport", "kiln", "player", "character", "ghost", "beacon", "bonfire" };
        static Type charT;

        public static int BrokenCount { get { return broken.Count; } }

        internal static Mesh Cube()
        {
            if (cube != null) return cube;
            var vs = new List<Vector3>(); var cs = new List<Color>(); var tr = new List<int>();
            Vector3[] n = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            float[] sh = { 1f, 0.55f, 0.75f, 0.85f, 0.9f, 0.65f };
            for (int f = 0; f < 6; f++)
            {
                Vector3 a = n[f], u = Vector3.Cross(a, Mathf.Abs(a.y) > 0.9f ? Vector3.right : Vector3.up).normalized, v = Vector3.Cross(a, u);
                int b = vs.Count;
                vs.Add((a + u + v) * 0.5f); vs.Add((a - u + v) * 0.5f); vs.Add((a - u - v) * 0.5f); vs.Add((a + u - v) * 0.5f);
                for (int i = 0; i < 4; i++) cs.Add(new Color(sh[f], sh[f], sh[f], 1f));
                tr.Add(b); tr.Add(b + 1); tr.Add(b + 2); tr.Add(b); tr.Add(b + 2); tr.Add(b + 3);
                tr.Add(b); tr.Add(b + 2); tr.Add(b + 1); tr.Add(b); tr.Add(b + 3); tr.Add(b + 2);
            }
            cube = new Mesh { name = "pf_cube" }; cube.SetVertices(vs); cube.SetColors(cs); cube.SetTriangles(tr, 0); cube.RecalculateBounds(); cube.RecalculateNormals();
            return cube;
        }

        static void SetupLayer()
        {
            if (layerDone) return; layerDone = true;
            try
            {
                for (int i = 31; i >= 8; i--) if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) { dLayer = i; break; }
                var me = Game.Me();
                if (dLayer >= 0 && me != null)
                {
                    foreach (var rb in Game.Rigs(me)) { if (rb.gameObject.layer != 0) Physics.IgnoreLayerCollision(dLayer, rb.gameObject.layer, true); break; }   // обломки не толкают персонажей
                }
            }
            catch { }
        }

        static Material MatFor(Color c)
        {
            int key = Mathf.RoundToInt(c.r * 8f) * 100 + Mathf.RoundToInt(c.g * 8f) * 10 + Mathf.RoundToInt(c.b * 8f);
            Material m; if (mats.TryGetValue(key, out m) && m != null) return m;
            m = Vfx.Mat(null, c); mats[key] = m; return m;
        }

        static Color ColorOf(Renderer r)
        {
            Color c = new Color(0.5f, 0.45f, 0.4f);
            try
            {
                var m = r != null ? r.sharedMaterial : null;
                if (m != null)
                {
                    if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor"); else if (m.HasProperty("_Color")) c = m.GetColor("_Color");
                }
            }
            catch { }
            float g = c.grayscale;
            if (g > 0.8f || c.a < 0.4f) c = new Color(0.55f, 0.5f, 0.44f);
            else if (g < 0.14f) c = Color.Lerp(c, new Color(0.35f, 0.32f, 0.3f), 0.55f);
            c.a = 1f; return c;
        }

        static Color SurfaceColor(Vector3 p, Vector3 n)
        {
            try
            {
                RaycastHit h;
                if (Physics.Raycast(p + n * 0.4f, -n, out h, 1.2f, ~0, QueryTriggerInteraction.Ignore) && h.collider != null)
                {
                    var r = h.collider.GetComponentInParent<Renderer>() ?? h.collider.GetComponentInChildren<Renderer>();
                    return ColorOf(r);
                }
            }
            catch { }
            return new Color(0.5f, 0.45f, 0.4f);
        }

        static void SpawnDebris(Vector3 pos, Vector3 vel, float size, Color col)
        {
            if (Vfx.Sh() == null || debris.Count >= MaxDebris) return;
            SetupLayer();
            var go = new GameObject("PF_Deb"); go.transform.position = pos; go.transform.rotation = UnityEngine.Random.rotation;
            if (dLayer >= 0) go.layer = dLayer;
            Vector3 sc = new Vector3(UnityEngine.Random.Range(0.6f, 1.3f), UnityEngine.Random.Range(0.5f, 1.1f), UnityEngine.Random.Range(0.6f, 1.3f)) * size;
            go.transform.localScale = sc;
            go.AddComponent<MeshFilter>().sharedMesh = Cube();
            var mr = go.AddComponent<MeshRenderer>(); float j = UnityEngine.Random.Range(0.8f, 1.15f);
            mr.sharedMaterial = MatFor(new Color(Mathf.Clamp01(col.r * j), Mathf.Clamp01(col.g * j), Mathf.Clamp01(col.b * j), 1f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<BoxCollider>();
            var rb = go.AddComponent<Rigidbody>(); rb.mass = Mathf.Max(0.05f, size * 2f); rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 8f;
            Game.SetVelocity(rb, vel);
            debris.Add(new Deb { go = go, born = Time.time, life = UnityEngine.Random.Range(6f, 11f), sc = sc });
        }

        /// <summary>Осыпь при ударе по поверхности (камни, щебень).</summary>
        public static void Chips(Vector3 p, Vector3 n, float power)
        {
            if (!On) return;
            try
            {
                Color c = SurfaceColor(p, n);
                int cnt = Mathf.Clamp(Mathf.RoundToInt(5 + 7 * power), 5, 20);
                for (int i = 0; i < cnt; i++)
                    SpawnDebris(p + n * 0.15f + UnityEngine.Random.insideUnitSphere * 0.4f, n * UnityEngine.Random.Range(3f, 8f) * (0.6f + 0.4f * power) + UnityEngine.Random.insideUnitSphere * 4f,
                        UnityEngine.Random.Range(0.07f, 0.26f) * (0.7f + 0.3f * power), c);
            }
            catch { }
        }

        static bool IsLivingCol(Collider c)
        {
            if (charT == null) charT = R.FindType("Character");
            return charT != null && c.GetComponentInParent(charT) != null;
        }

        /// <summary>Ломает объекты карты вокруг точки. Возвращает, сколько сломано.</summary>
        public static int Smash(Vector3 p, Vector3 dir, float radius, float power, bool net = true, float maxOverride = -1f)
        {
            if (!On) return 0;
            int count = 0;
            try
            {
                float maxSz = maxOverride > 0f ? maxOverride : MaxSize;
                dir = dir.sqrMagnitude < 0.01f ? Vector3.up : dir.normalized;
                Collider ground = null;
                var me = Game.Me();
                if (Protect && me != null)
                {
                    RaycastHit gh;
                    if (Physics.Raycast(PeakFunPlugin.CenterOf(me), Vector3.down, out gh, 3.5f, ~0, QueryTriggerInteraction.Ignore)) ground = gh.collider;
                }
                var cols = Physics.OverlapSphere(p, radius, ~0, QueryTriggerInteraction.Ignore);
                foreach (var col in cols)
                {
                    if (col == null || !col.enabled || col is TerrainCollider) continue;
                    if (col.gameObject.name == "PF_Deb") continue;
                    if (IsLivingCol(col)) continue;
                    var rb = col.attachedRigidbody;
                    if (rb != null && !rb.isKinematic)
                    {
                        rb.AddExplosionForce(power * 7f, p - dir * 0.3f, radius * 1.5f, 0.5f, ForceMode.VelocityChange);
                        continue;
                    }
                    var b = col.bounds; float sz = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    if (sz > maxSz || sz < 0.05f) continue;
                    if (ground != null && col == ground) continue;
                    string nm = (col.transform.root.name + "|" + col.name).ToLowerInvariant(); bool bad = false;
                    foreach (var t in skip) if (nm.Contains(t)) { bad = true; break; }
                    if (bad) continue;
                    if (BreakOne(col, b, sz, p, dir, power)) { count++; if (count >= 10) break; }
                }
                if (count > 0)
                {
                    Vfx.Dust(p, Mathf.Clamp(radius, 1f, 4f), 10 + count * 3); Sfx2.Crunch(Mathf.Clamp01(power * 0.5f));
                    if (net && Game.InRoom && Time.time - lastNet > 0.1f)
                    {
                        lastNet = Time.time;
                        Net.Send("brk", string.Format(inv, "{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F1}", p.x, p.y, p.z, dir.x, dir.y, dir.z, radius, power, maxSz));
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[PeakFun] разрушение: " + e.Message); }
            return count;
        }

        static bool BreakOne(Collider col, Bounds b, float sz, Vector3 p, Vector3 dir, float power)
        {
            var list = new List<Renderer>();
            foreach (var r in col.GetComponentsInChildren<Renderer>(false))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!r.enabled) continue;
                var rb2 = r.bounds;
                if (rb2.size.magnitude > b.size.magnitude * 2f + 1.5f || !rb2.Intersects(b)) continue;
                list.Add(r);
            }
            if (list.Count == 0) return false;
            Color c = ColorOf(list[0]);
            foreach (var r in list) r.enabled = false;
            col.enabled = false;
            broken.Add(new Broken { col = col, rs = list.ToArray(), t = Time.time });
            int n = Mathf.Clamp(Mathf.RoundToInt(sz * 3f), 5, 16);
            for (int i = 0; i < n; i++)
            {
                Vector3 q = new Vector3(UnityEngine.Random.Range(b.min.x, b.max.x), UnityEngine.Random.Range(b.min.y, b.max.y), UnityEngine.Random.Range(b.min.z, b.max.z));
                Vector3 outv = (q - p); outv = outv.sqrMagnitude < 0.01f ? UnityEngine.Random.onUnitSphere : outv.normalized;
                SpawnDebris(q, (outv * UnityEngine.Random.Range(2f, 7f) + dir * UnityEngine.Random.Range(1f, 5f) + Vector3.up * UnityEngine.Random.Range(1f, 4f)) * (0.6f + 0.4f * power),
                    Mathf.Clamp(sz * UnityEngine.Random.Range(0.08f, 0.2f), 0.08f, 0.6f), c);
            }
            return true;
        }

        public static void OnNet(string arg)
        {
            try
            {
                var s = arg.Split(','); if (s.Length < 9) return;
                Func<int, float> f = i => float.Parse(s[i], inv);
                Smash(new Vector3(f(0), f(1), f(2)), new Vector3(f(3), f(4), f(5)), f(6), f(7), false, f(8));
            }
            catch { }
        }

        public static void Tick()
        {
            for (int i = debris.Count - 1; i >= 0; i--)
            {
                var d = debris[i];
                if (d.go == null) { debris.RemoveAt(i); continue; }
                float age = Time.time - d.born;
                if (age > d.life)
                {
                    float k = (age - d.life) / 0.6f;
                    if (k >= 1f) { UnityEngine.Object.Destroy(d.go); debris.RemoveAt(i); continue; }
                    d.go.transform.localScale = d.sc * (1f - k);
                }
            }
            if (broken.Count == 0) return;
            for (int i = broken.Count - 1; i >= 0; i--)
            {
                var b = broken[i];
                if (b.col == null) { broken.RemoveAt(i); continue; }
                if (RestoreAfter > 0f && Time.time - b.t > RestoreAfter)
                {
                    if (AnyoneInside(b.col.bounds)) { b.t += 5f; continue; }
                    Restore(b); broken.RemoveAt(i);
                }
            }
        }

        static bool AnyoneInside(Bounds b)
        {
            b.Expand(0.6f);
            foreach (var c in Game.Everyone()) if (c != null && b.Contains(PeakFunPlugin.CenterOf(c))) return true;
            return false;
        }

        static void Restore(Broken b)
        {
            try { if (b.col != null) b.col.enabled = true; foreach (var r in b.rs) if (r != null) r.enabled = true; } catch { }
        }

        public static void RestoreAll()
        {
            foreach (var b in broken) Restore(b);
            broken.Clear();
            foreach (var d in debris) if (d.go != null) UnityEngine.Object.Destroy(d.go);
            debris.Clear();
        }
    }
}
