// «Только Backrooms»: на время забега прячем остров (рендер и коллайдеры слоя местности), чтобы уровни не упирались в горы.
// Ничего не удаляется: всё включается обратно при выходе из Backrooms.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static class Island
    {
        public static bool Enabled = true, Hidden; public static int Count, extraTotal; public static string Info = "";
        static readonly List<Renderer> rs = new List<Renderer>(); static readonly List<Collider> cs = new List<Collider>(); static readonly List<Terrain> ts = new List<Terrain>(); static float next, lastScan;

        static readonly System.Text.RegularExpressions.Regex weather = new System.Text.RegularExpressions.Regex("fog|cloud|mist|haze|sky|weather|storm|snow|rain|smoke|vapor|steam|dust|aurora|atmos|blizzard|wind|lens|flare|godray|volumetric", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static int Mask()
        {
            int m = Util.TerrainMask(); if (m != 0 && m != ~0) return m; m = 0;
            foreach (var nm in new[] { "Terrain", "Map", "Ground" }) { int l = LayerMask.NameToLayer(nm); if (l >= 0) m |= 1 << l; }
            return m;
        }
        static bool Mine(GameObject go)
        {
            if (go.name.StartsWith("BR_") || go.name.StartsWith("PF_")) return true; var r = go.transform.root; return r != null && (r.name.StartsWith("BR_") || r.name.StartsWith("PF_"));
        }

        public static void Hide() { if (!Enabled) return; if (Hidden && Time.time - lastScan < 3f) return; Hidden = true; next = Time.time + 15f; Scan(); }
        public static void Tick() { if (!Hidden || Time.time < next) return; next = Time.time + 15f; Scan(); }

        static void Scan()
        {
            try
            {
                lastScan = Time.time; int m = Mask(); if (m == 0) { Info = "слой местности не найден — остров не скрыт"; return; }
                var others = new HashSet<Transform>(); foreach (var o in Game.Everyone(false)) if (o != null) others.Add(o.transform.root);
                foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                {
                    if (r == null || !r.enabled) continue; var go = r.gameObject; if (((1 << go.layer) & m) == 0 || Mine(go) || others.Contains(go.transform.root)) continue;
                    r.enabled = false; rs.Add(r);
                }
                // погода и «туман игры» рядом с уровнем: частицы и объекты с названиями fog/cloud/sky/…
                if (Play.On && Play.BaseP != Vector3.zero)
                {
                    int nn = Play.Cur != null ? Play.Cur.n : 80; Vector3 ctr = Play.BaseP + new Vector3(nn * 2f, 6f, nn * 2f); float R = nn * 3.2f + 120f; int extra = 0;
                    foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                    {
                        if (r == null || !r.enabled) continue; var go = r.gameObject; if (Mine(go) || others.Contains(go.transform.root)) continue; if (r is TrailRenderer || r is LineRenderer) continue;
                        bool hit = r is ParticleSystemRenderer || weather.IsMatch(go.name) || weather.IsMatch(go.transform.root.name);
                        if (!hit) { var sm = r.sharedMaterial; if (sm != null && sm.shader != null && weather.IsMatch(sm.shader.name) && !(r is SkinnedMeshRenderer)) hit = true; }
                        if (!hit) continue; if ((r.bounds.center - ctr).magnitude > R + r.bounds.extents.magnitude) continue;
                        r.enabled = false; rs.Add(r); extra++;
                    }
                    extraTotal += extra;
                }
                foreach (var c in UnityEngine.Object.FindObjectsOfType<Collider>())
                {
                    if (c == null || !c.enabled || c.isTrigger) continue; var go = c.gameObject; if (((1 << go.layer) & m) == 0 || Mine(go) || others.Contains(go.transform.root)) continue;
                    c.enabled = false; cs.Add(c);
                }
                foreach (var t in UnityEngine.Object.FindObjectsOfType<Terrain>()) { if (t == null || !t.drawHeightmap) continue; t.drawHeightmap = false; t.drawTreesAndFoliage = false; ts.Add(t); var tc = t.GetComponent<Collider>(); if (tc != null && tc.enabled) { tc.enabled = false; cs.Add(tc); } }
                Count = rs.Count + cs.Count + ts.Count; Info = "остров скрыт: " + rs.Count + " видимых и " + cs.Count + " твёрдых объектов" + (extraTotal > 0 ? " · +" + extraTotal + " погодных/туманных рядом" : "");
            }
            catch (Exception e) { Plugin.Warn("остров", e); }
        }

        /// <summary>Отчёт «что у игры рядом с камерой» — в файл BepInEx\PeakFun_fog_report.txt (чтобы понять, откуда белый туман).</summary>
        public static void Report(string tag)
        {
            try
            {
                var sb = new System.Text.StringBuilder(); var cam = Camera.main; sb.AppendLine("PeakFun fog report " + tag + " " + DateTime.Now);
                sb.AppendLine("fog=" + RenderSettings.fog + " mode=" + RenderSettings.fogMode + " density=" + RenderSettings.fogDensity + " color=" + RenderSettings.fogColor + " ambient=" + RenderSettings.ambientLight + " ambMode=" + RenderSettings.ambientMode + " skybox=" + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "null"));
                sb.AppendLine("Island: " + Info + "; FogMul=" + Play.FogMul);
                if (cam != null)
                {
                    sb.AppendLine("camera " + cam.name + " pos=" + cam.transform.position + " far=" + cam.farClipPlane + " clear=" + cam.clearFlags + " bg=" + cam.backgroundColor + " mask=" + cam.cullingMask);
                    int k = 0; foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
                    {
                        if (r == null || !r.enabled || Mine(r.gameObject)) continue; float d = (r.bounds.center - cam.transform.position).magnitude; if (d > 90f) continue; if (k++ > 90) break;
                        string sh = r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "-";
                        sb.AppendLine(string.Format("  {0:0}м  {1} / root {2} / {3} / layer {4} / {5} / размер {6}", d, r.name, r.transform.root.name, r.GetType().Name, LayerMask.LayerToName(r.gameObject.layer), sh, r.bounds.size));
                    }
                    foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>()) if (l != null && l.enabled && !l.name.StartsWith("BR_")) sb.AppendLine("  light " + l.name + " " + l.type + " int=" + l.intensity + " d=" + (l.transform.position - cam.transform.position).magnitude.ToString("0"));
                }
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { try { var vt = a.GetType("UnityEngine.Rendering.Volume"); if (vt != null) { foreach (var v in UnityEngine.Object.FindObjectsOfType(vt)) sb.AppendLine("  volume " + ((Component)v).name); break; } } catch { } }
                System.IO.File.WriteAllText(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "PeakFun_fog_report.txt"), sb.ToString());
            }
            catch (Exception e) { Plugin.Warn("fogreport", e); }
        }

        public static void Restore()
        {
            foreach (var r in rs) { try { if (r != null) r.enabled = true; } catch { } }
            foreach (var c in cs) { try { if (c != null) c.enabled = true; } catch { } }
            foreach (var t in ts) { try { if (t != null) { t.drawHeightmap = true; t.drawTreesAndFoliage = true; } } catch { } }
            rs.Clear(); cs.Clear(); ts.Clear(); Hidden = false; Count = 0; extraTotal = 0; Info = "";
        }
    }
}
