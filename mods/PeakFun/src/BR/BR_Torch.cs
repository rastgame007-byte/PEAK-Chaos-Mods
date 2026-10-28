// Фонарик (1.28): клавиша F (меняется в конфиге). Настоящий прожектор-спот с мягким краем и тенями, модель в руке с покачиванием,
// видимый луч (лёгкая дымка), пылинки в луче, батарея (садится при работе, подзаряжается в выключенном виде), мерцание рядом с опасностью.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static class Torch
    {
        public static KeyCode Key = KeyCode.F;
        public static bool On; public static float Battery = 1f;
        static GameObject root, model, beamGo; static Light lt, hot; static Material beamMat, lensMat; static ParticleSystem dust; static float hold, flick, sway, lastYaw, lastPitch, swayX, swayY; static Quaternion rot = Quaternion.identity; static bool haveRot, lowWarned;
        static Mesh beamMesh; static Transform camPar;

        static Mesh Beam()
        {
            if (beamMesh != null) return beamMesh;
            // два вложенных конуса: широкий слабый и узкий плотный; альфа вдоль длины нарастает и плавно гаснет
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>(); const int S = 20; const int R = 7; float len = 16f;
            for (int layer = 0; layer < 2; layer++)
            {
                float ang = (layer == 0 ? 25f : 13f) * Mathf.Deg2Rad, a0 = layer == 0 ? 0.07f : 0.11f; int baseI = v.Count;
                for (int r = 0; r <= R; r++)
                {
                    float q = r / (float)R, z = 0.15f + q * len, rad = z * Mathf.Tan(ang); float al = a0 * Mathf.Clamp01(q * 5f) * (1f - q) * (1f - q * 0.3f);
                    for (int s = 0; s < S; s++) { float a = s / (float)S * Mathf.PI * 2f; v.Add(new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad, z)); c.Add(new Color(1f, 0.95f, 0.8f, al)); }
                }
                for (int r = 0; r < R; r++) for (int s = 0; s < S; s++) { int a = baseI + r * S + s, b = baseI + r * S + (s + 1) % S, d = baseI + (r + 1) * S + s, e = baseI + (r + 1) * S + (s + 1) % S; t.Add(a); t.Add(d); t.Add(b); t.Add(b); t.Add(d); t.Add(e); }
            }
            beamMesh = new Mesh { name = "BR_torch_beam" }; beamMesh.SetVertices(v); beamMesh.SetColors(c); beamMesh.SetTriangles(t, 0); beamMesh.RecalculateBounds(); return beamMesh;
        }

        static void Ensure(Camera cam)
        {
            if (root != null) return;
            root = new GameObject("BR_torch");
            var lg = new GameObject("light"); lg.transform.SetParent(root.transform, false); lt = lg.AddComponent<Light>(); lt.type = LightType.Spot; lt.range = 42f; lt.spotAngle = 72f; try { lt.innerSpotAngle = 30f; } catch { } try { lt.renderMode = LightRenderMode.ForcePixel; } catch { }
            lt.color = new Color(1f, 0.93f, 0.78f); lt.intensity = 0f; lt.shadows = Play.Qual >= 3 ? LightShadows.None : LightShadows.Soft; lt.shadowStrength = 0.95f; lt.shadowBias = 0.03f; lt.shadowNearPlane = 0.25f;
            // узкое «горячее пятно» — яркий центр луча (в URP свет убывает как 1/d², поэтому мощность намного выше, чем у ламп уровня)
            var hg = new GameObject("hot"); hg.transform.SetParent(root.transform, false); hot = hg.AddComponent<Light>(); hot.type = LightType.Spot; hot.range = 55f; hot.spotAngle = 26f; try { hot.innerSpotAngle = 10f; } catch { } try { hot.renderMode = LightRenderMode.ForcePixel; } catch { }
            hot.color = new Color(1f, 0.95f, 0.82f); hot.intensity = 0f; hot.shadows = LightShadows.None;
            // видимый луч
            beamGo = new GameObject("beam"); beamGo.transform.SetParent(lg.transform, false); var mf = beamGo.AddComponent<MeshFilter>(); mf.sharedMesh = Beam(); var mr = beamGo.AddComponent<MeshRenderer>();
            beamMat = Vfx.Mat(null, Color.white); mr.sharedMaterial = beamMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            // пылинки в луче
            var dg = new GameObject("dust"); dg.transform.SetParent(lg.transform, false); dust = dg.AddComponent<ParticleSystem>(); var pr = dg.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = Vfx.Mat(Vfx.Blob(), Color.white); pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var m = dust.main; m.loop = true; m.startLifetime = 3.5f; m.startSpeed = 0.06f; m.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f); m.startColor = new Color(1f, 0.96f, 0.82f, 0.7f); m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 160;
            var em = dust.emission; em.rateOverTime = 28f; var sh = dust.shape; sh.shapeType = ParticleSystemShapeType.ConeVolume; sh.angle = 17f; sh.radius = 0.1f; sh.length = 11f;
            var col = dust.colorOverLifetime; col.enabled = true; var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.25f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) }); col.color = gr;
            var noise = dust.noise; noise.enabled = true; noise.strength = 0.12f; noise.frequency = 0.5f;
            // модель в руке
            model = new GameObject("model"); var mb = new MeshB(); var gb = new MeshB();
            mb.Cyl(new Vector3(0, 0, -0.11f), new Vector3(0, 0, 0.07f), 0.0225f, 14); mb.Cyl(new Vector3(0, 0, 0.07f), new Vector3(0, 0, 0.13f), 0.034f, 16); mb.Cyl(new Vector3(0, 0, -0.115f), new Vector3(0, 0, -0.105f), 0.025f, 14);
            for (int i = 0; i < 6; i++) mb.Cyl(new Vector3(0, 0, -0.07f + i * 0.012f), new Vector3(0, 0, -0.066f + i * 0.012f), 0.0238f, 14, false);   // накатка рукояти
            mb.Box(new Vector3(0, 0.0235f, 0.005f), new Vector3(0.012f, 0.008f, 0.03f));                                                              // кнопка
            gb.Cyl(new Vector3(0, 0, 0.128f), new Vector3(0, 0, 0.134f), 0.029f, 16);                                                                 // линза
            var body = BrGfx.Surf(null, new Color(0.08f, 0.085f, 0.09f), 0.55f, 0.85f); lensMat = BrGfx.Surf(null, new Color(1f, 0.95f, 0.8f), 0.9f, 0f, new Color(2.2f, 2f, 1.5f));
            var o1 = new GameObject("body"); o1.transform.SetParent(model.transform, false); o1.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("BR_torch_body"); var r1 = o1.AddComponent<MeshRenderer>(); r1.sharedMaterial = body; r1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var o2 = new GameObject("lens"); o2.transform.SetParent(model.transform, false); o2.AddComponent<MeshFilter>().sharedMesh = gb.ToMesh("BR_torch_lens"); var r2 = o2.AddComponent<MeshRenderer>(); r2.sharedMaterial = lensMat; r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public static void Stop()
        {
            if (root != null) UnityEngine.Object.Destroy(root); if (model != null) UnityEngine.Object.Destroy(model); root = null; model = null; lt = null; hot = null; dust = null; beamGo = null; haveRot = false;
            On = false; Battery = 1f; hold = 0f; lowWarned = false; camPar = null;
        }

        public static void Tick(float dt, Vector3 pc)
        {
            var cam = Camera.main; if (cam == null || !Play.On || Play.Cur == null || Play.Catching || Play.CatchCam) { if (lt != null) lt.enabled = false; if (hot != null) hot.enabled = false; if (model != null) model.SetActive(false); return; }
            if (Keys.Down(Key) && !Play.Busy) { if (!On && Battery <= 0.02f) Ui.Toast("Батарея фонарика села — подожди, она медленно подзаряжается", Ui.Accent); else { On = !On; try { Snd2.Flat("click", 0.5f); } catch { } } }
            Ensure(cam);
            if (On) { Battery = Mathf.Max(0f, Battery - dt / 330f); if (Battery <= 0f) { On = false; try { Snd2.Flat("click", 0.5f); } catch { } Ui.Toast("Батарея фонарика села", Ui.Accent); } }
            else Battery = Mathf.Min(1f, Battery + dt / 160f);
            if (Battery > 0.3f) lowWarned = false; else if (On && !lowWarned && Battery < 0.2f) { lowWarned = true; Ui.Toast("Фонарик тускнеет — батарея на исходе", Ui.Accent); }
            hold = Mathf.MoveTowards(hold, On ? 1f : 0f, dt * 5f);
            // направление: плавно следует за камерой (лёгкая инерция)
            Quaternion target = cam.transform.rotation; rot = haveRot ? Quaternion.Slerp(rot, target, 1f - Mathf.Exp(-dt * 14f)) : target; haveRot = true;
            // мерцание: на исходе батареи и при приближении опасности
            float f = 1f; if (On)
            {
                float t = Time.time; if (Battery < 0.2f) f *= Mathf.Lerp(0.35f, 1f, Mathf.PerlinNoise(t * 11f, 3.1f) > (0.5f - Battery * 1.5f) ? 1f : 0f) * (0.55f + Battery * 2.2f);
                float danger = Mathf.Max(Play.Prox * 0.7f, Dread.Level * 0.12f); if (danger > 0.05f && Mathf.PerlinNoise(t * 17f, 8.7f) < danger * 0.5f) f *= 0.12f;
            }
            flick = Mathf.Lerp(flick, f, 1f - Mathf.Exp(-dt * 30f));
            lt.enabled = hold > 0.02f; Vector3 right = rot * Vector3.right, up = rot * Vector3.up, fw = rot * Vector3.forward;
            lg(lt, cam, right, up, fw);
            lt.intensity = 34f * hold * flick * Play.Cur.rig.CamK; if (hot != null) { hot.enabled = hold > 0.02f; lg(hot, cam, right, up, fw); hot.intensity = 70f * hold * flick * Play.Cur.rig.CamK; }
            try { var cl = Play.Cur.rig.camLight; if (cl != null) { if (hold > 0.02f) { cl.range = 16f; cl.intensity = cl.intensity * (1f + 2.2f * hold * flick); } else cl.range = Play.Cur.rig.FlashOn ? 15f : 6.5f; } } catch { } lt.shadows = Play.Qual >= 3 ? LightShadows.None : LightShadows.Soft;
            if (beamMat != null) beamMat.color = new Color(1f, 1f, 1f, Mathf.Clamp01(hold * flick * 1.6f));
            if (beamGo != null) beamGo.SetActive(hold > 0.02f);
            if (dust != null) { var em = dust.emission; em.enabled = On && flick > 0.3f; }
            if (lensMat != null && lensMat.HasProperty("_EmissionColor")) lensMat.SetColor("_EmissionColor", new Color(2.2f, 2f, 1.5f) * (0.15f + 0.85f * hold * flick));
            // модель в руке: инерция от поворотов, дыхание
            float yaw = cam.transform.eulerAngles.y, pitch = cam.transform.eulerAngles.x; float dy = Mathf.DeltaAngle(lastYaw, yaw), dp = Mathf.DeltaAngle(lastPitch, pitch); lastYaw = yaw; lastPitch = pitch;
            swayX = Mathf.Lerp(swayX, Mathf.Clamp(-dy * 0.004f, -0.05f, 0.05f), 1f - Mathf.Exp(-dt * 10f)); swayY = Mathf.Lerp(swayY, Mathf.Clamp(dp * 0.004f, -0.05f, 0.05f), 1f - Mathf.Exp(-dt * 10f)); sway += dt * (1.6f + Play.Prox);
            if (model != null)
            {
                model.SetActive(true); if (model.transform.parent != cam.transform) { model.transform.SetParent(cam.transform, false); }
                Vector3 shown = new Vector3(0.22f + swayX, -0.2f + swayY + Mathf.Sin(sway) * 0.003f, 0.34f), hidden = new Vector3(0.24f, -0.42f, 0.28f); float e = Mathf.SmoothStep(0f, 1f, hold);
                model.transform.localPosition = Vector3.Lerp(hidden, shown, e); model.transform.localScale = Vector3.one * 1.7f; model.transform.localRotation = Quaternion.Euler(Mathf.Lerp(28f, 4f, e) + swayY * 120f, -6f + swayX * 120f, 0f);
                foreach (var r in model.GetComponentsInChildren<Renderer>()) r.enabled = hold > 0.02f || e > 0.02f;
            }
        }
        static void lg(Light l, Camera cam, Vector3 right, Vector3 up, Vector3 fw) { l.transform.position = cam.transform.position + right * 0.17f - up * 0.1f + fw * 0.2f; Vector3 aim = cam.transform.position + fw * 25f; l.transform.rotation = Quaternion.LookRotation((aim - l.transform.position).normalized, up); }

        public static void Hud(float W, float H, float s)
        {
            if (!Play.On || Play.Cur == null || Play.Catching) return;
            float w = 150f * s, h = 34f * s, x = W - w - 18f * s, y = H - h - 18f * s; var r = new Rect(x, y, w, h);
            Ui.Box(r, new Color(0.02f, 0.017f, 0.01f, On ? 0.7f : 0.45f), 4f * s); Ui.Border(r, new Color(1f, 0.9f, 0.5f, On ? 0.6f : 0.25f), 1f);
            Ui.Text(new Rect(x + 8f * s, y + 2f * s, w - 16f * s, 14f * s), "ФОНАРИК [" + Key + "]  " + (On ? "ВКЛ" : "выкл"), (int)(10f * s), new Color(1f, 0.95f, 0.8f, On ? 0.95f : 0.55f), TextAnchor.MiddleLeft, true);
            var bar = new Rect(x + 8f * s, y + 20f * s, w - 16f * s, 7f * s); Ui.Box(bar, new Color(0f, 0f, 0f, 0.6f), 2f * s);
            Color bc = Battery > 0.35f ? new Color(0.95f, 0.85f, 0.4f) : (Battery > 0.15f ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.2f, 0.15f)); if (Battery < 0.15f && On && Mathf.Repeat(Time.time * 3f, 1f) < 0.5f) bc.a = 0.3f;
            Ui.Box(new Rect(bar.x, bar.y, bar.width * Battery, bar.height), bc, 2f * s);
        }
    }
}
