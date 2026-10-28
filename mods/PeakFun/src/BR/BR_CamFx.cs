// Тряска камеры, падение камеры при поимке и оверлей «камеры-кассеты».
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace PeakBR
{
    internal static class CamFx
    {
        public static float Boost, Fov; public static Func<bool> ExtraActive; public static Action<Transform> ExtraApply;
        static bool installed, applied, fovSet; static Vector3 sp; static Quaternion sr; static Camera cur; static float sfov;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Install()
        {
            if (installed) return; installed = true;
            try { Camera.onPreCull += Pre; Camera.onPostRender += Post; RenderPipelineManager.beginCameraRendering += PreSrp; RenderPipelineManager.endCameraRendering += PostSrp; }
            catch (Exception e) { Debug.LogWarning("[PeakBR] тряска камеры недоступна: " + e.Message); }
        }
        static void PreSrp(ScriptableRenderContext c, Camera cam) { Pre(cam); }
        static void PostSrp(ScriptableRenderContext c, Camera cam) { Post(cam); }
        static void Pre(Camera cam)
        {
            float lv = Boost; if (applied || cam == null || cam != Camera.main) return;
            bool extra = false; try { extra = ExtraActive != null && ExtraActive(); } catch { }
            bool dr = Dread.Level > 0.04f && !extra; if (lv <= 0.001f && !extra && !dr) return;
            var tr = cam.transform; cur = cam; sp = tr.position; sr = tr.rotation; applied = true; Fov = 0f;
            if (extra) { try { ExtraApply(tr); } catch { } } if (dr) { try { Dread.Sway(tr); } catch { } }
            if (Fov > 1f) { try { sfov = cam.fieldOfView; cam.fieldOfView = Fov; fovSet = true; } catch { } }
            if (lv <= 0.001f) return; float t = Time.unscaledTime;
            Vector3 off = new Vector3(Mathf.Sin(t * 47f) + Mathf.Sin(t * 91f) * 0.5f, Mathf.Sin(t * 53f + 1f) + Mathf.Sin(t * 83f) * 0.5f, 0f) * 0.045f * lv;
            tr.position = tr.position + tr.TransformDirection(off);
            tr.rotation = tr.rotation * Quaternion.Euler(Mathf.Sin(t * 41f) * 0.7f * lv, Mathf.Sin(t * 37f + 2f) * 0.7f * lv, Mathf.Sin(t * 29f) * 1.6f * lv);
        }
        static void Post(Camera cam)
        {
            if (!applied || cam != cur) return;
            if (cur != null) { cur.transform.position = sp; cur.transform.rotation = sr; if (fovSet) { try { cur.fieldOfView = sfov; } catch { } } }
            fovSet = false; applied = false;
        }

        static Texture2D[] brNoise; static Texture2D brScan, brEdge, brVig;
        static void CassInit()
        {
            if (brNoise != null) return;
            brNoise = new Texture2D[4]; var rn = new System.Random(9);
            for (int i = 0; i < 4; i++)
            {
                var t = new Texture2D(128, 128, TextureFormat.RGBA32, false); t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Repeat; var px = new Color[128 * 128];
                for (int k = 0; k < px.Length; k++) { float v = (float)rn.NextDouble(); v = v * v; px[k] = new Color(v, v, v, 1f); }
                t.SetPixels(px); t.Apply(false, false); brNoise[i] = t;
            }
            brScan = new Texture2D(1, 4, TextureFormat.RGBA32, false); brScan.filterMode = FilterMode.Point; brScan.wrapMode = TextureWrapMode.Repeat;
            brScan.SetPixels(new[] { new Color(0, 0, 0, 0f), new Color(0, 0, 0, 0.55f), new Color(0, 0, 0, 0f), new Color(0, 0, 0, 0.12f) }); brScan.Apply(false, false);
            brEdge = new Texture2D(64, 1, TextureFormat.RGBA32, false); brEdge.wrapMode = TextureWrapMode.Clamp; var ep = new Color[64]; for (int i = 0; i < 64; i++) { float a = 1f - i / 63f; ep[i] = new Color(1, 1, 1, a * a); }
            brEdge.SetPixels(ep); brEdge.Apply(false, false);
        }

        static void BrRect(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); }

        public static void Draw(float W, float H, float s, Camera cam)
        {
            CassInit();
            float inT = Time.time - Play.EnterT; float prox = Play.Prox, gl = Play.Glitch;
            if (brVig == null) brVig = Vfx.Tex(128, 128, (u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 1.42f; float a = Mathf.Clamp01((d - 0.38f) / 0.62f); return new Color(0.04f, 0.03f, 0f, a * a * 0.92f); });
            GUI.color = Color.white; GUI.DrawTexture(new Rect(0, 0, W, H), brVig); Dread.Draw(W, H, s);
            if (!Play.CamFxOn)
            {
                GUI.color = Color.white; return;
            }
            // --- шум ---
            var nt = brNoise[Time.frameCount % 4]; float nA = 0.07f + 0.04f * Mathf.PerlinNoise(Time.time * 7f, 1f) + 0.12f * prox + 0.55f * gl;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(nA)); GUI.DrawTextureWithTexCoords(new Rect(0, 0, W, H), nt, new Rect(UnityEngine.Random.value, UnityEngine.Random.value, W / 128f / (2.2f * s), H / 128f / (2.2f * s)));
            // --- строчная развёртка ---
            float pitch = Mathf.Max(1f, 1.4f * s); GUI.color = new Color(1, 1, 1, 0.5f); GUI.DrawTextureWithTexCoords(new Rect(0, 0, W, H), brScan, new Rect(0, 0, 1, H / (4f * pitch)));
            // --- плавающая полоса трекинга ---
            float by = Mathf.Repeat(Time.time * 0.11f, 1.35f) * H - 0.2f * H, bh = 0.12f * H;
            GUI.color = new Color(1, 1, 1, 0.06f + 0.1f * prox); GUI.DrawTextureWithTexCoords(new Rect(0, by, W, bh), nt, new Rect(UnityEngine.Random.value, 0f, W / 128f / (2f * s), bh / 128f / (2f * s)));
            BrRect(new Rect(0, by + bh * 0.5f, W, 2f * s), new Color(0.85f, 1f, 1f, 0.07f));
            // --- сбои трекинга ---
            float seed = Mathf.Floor(Time.time / 5.3f); float ph = Time.time - seed * 5.3f; bool burst = ph < 0.22f && Mathf.Abs(Mathf.Sin(seed * 12.9898f) * 43758f) % 1f < 0.8f; if (gl > 0.2f || prox > 0.7f && UnityEngine.Random.value < 0.06f) burst = true;
            if (burst)
            {
                int cnt = 4 + (int)(gl * 8f);
                for (int i = 0; i < cnt; i++)
                {
                    float yy = UnityEngine.Random.value * H, hh = UnityEngine.Random.Range(3f, 26f) * s, ox = UnityEngine.Random.Range(-40f, 40f) * s;
                    GUI.color = i % 2 == 0 ? new Color(0.7f, 1f, 1f, 0.3f) : new Color(1f, 0.6f, 0.6f, 0.26f);
                    GUI.DrawTextureWithTexCoords(new Rect(ox, yy, W, hh), nt, new Rect(UnityEngine.Random.value, UnityEngine.Random.value, W / 128f / (2f * s), 0.2f));
                    BrRect(new Rect(ox * 0.5f, yy, W, 1.5f * s), new Color(1, 1, 1, 0.22f));
                }
            }
            // --- хроматика по краям ---
            GUI.color = new Color(1f, 0.15f, 0.1f, 0.16f + 0.1f * gl); GUI.DrawTexture(new Rect(0, 0, 70f * s, H), brEdge);
            GUI.color = new Color(0.1f, 0.8f, 1f, 0.16f + 0.1f * gl); GUI.matrix = Matrix4x4.TRS(new Vector3(W, 0, 0), Quaternion.identity, new Vector3(-1, 1, 1)); GUI.DrawTexture(new Rect(0, 0, 70f * s, H), brEdge); GUI.matrix = Matrix4x4.identity;
            // --- рамка видоискателя ---
            float m = 34f * s, l = 46f * s, th = 3f * s; Color fc = new Color(1f, 1f, 1f, 0.8f);
            BrRect(new Rect(m, m, l, th), fc); BrRect(new Rect(m, m, th, l), fc); BrRect(new Rect(W - m - l, m, l, th), fc); BrRect(new Rect(W - m - th, m, th, l), fc);
            BrRect(new Rect(m, H - m - th, l, th), fc); BrRect(new Rect(m, H - m - l, th, l), fc); BrRect(new Rect(W - m - l, H - m - th, l, th), fc); BrRect(new Rect(W - m - th, H - m - l, th, l), fc);
            BrRect(new Rect(W * 0.5f - 12f * s, H * 0.5f - 1f * s, 24f * s, 2f * s), new Color(1, 1, 1, 0.35f)); BrRect(new Rect(W * 0.5f - 1f * s, H * 0.5f - 12f * s, 2f * s, 24f * s), new Color(1, 1, 1, 0.35f));
            // --- REC, таймкод, батарея ---
            bool blink = Mathf.Repeat(Time.time, 1.2f) < 0.7f; Color wc = new Color(1f, 1f, 1f, 0.92f);
            if (blink) { BrRect(new Rect(m + 4f * s, m + 18f * s, 16f * s, 16f * s), new Color(0.95f, 0.1f, 0.08f, 0.95f)); }
            Ui.Text(new Rect(m + 28f * s, m + 10f * s, 120f * s, 32f * s), "REC", (int)(24f * s), wc, TextAnchor.MiddleLeft, true);
            var ts = TimeSpan.FromSeconds(Mathf.Max(0f, inT)); string tc = string.Format("{0:00}:{1:00}:{2:00}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            Ui.Text(new Rect(m + 4f * s, m + 44f * s, 260f * s, 30f * s), "SP  " + tc, (int)(22f * s), wc, TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(W - m - 300f * s, m + 10f * s, 300f * s, 30f * s), "CAM-01", (int)(22f * s), wc, TextAnchor.MiddleRight, true);
            float bx = W - m - 66f * s, byy = m + 46f * s; int bars = prox > 0.8f ? 1 : (inT > 600f ? 2 : 3); bool low = bars == 1 && blink;
            BrRect(new Rect(bx, byy, 50f * s, 22f * s), wc); BrRect(new Rect(bx + 3f * s, byy + 3f * s, 44f * s, 16f * s), new Color(0, 0, 0, 0.85f)); BrRect(new Rect(bx + 50f * s, byy + 6f * s, 5f * s, 10f * s), wc);
            for (int i = 0; i < bars; i++) if (!low) BrRect(new Rect(bx + (5f + i * 14.5f) * s, byy + 5f * s, 12f * s, 12f * s), wc);
            string date = DateTime.Now.AddYears(-30).ToString("MMM dd yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
            Ui.Text(new Rect(m + 4f * s, H - m - 74f * s, 300f * s, 30f * s), date, (int)(22f * s), wc, TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(m + 4f * s, H - m - 44f * s, 300f * s, 30f * s), DateTime.Now.ToString("hh:mm tt", CultureInfo.InvariantCulture), (int)(22f * s), wc, TextAnchor.MiddleLeft, true);
            if (Play.TapeEnd > 0.01f)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.35f * Play.TapeEnd); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, 0.35f * Play.TapeEnd); GUI.DrawTextureWithTexCoords(new Rect(0, 0, W, H), brNoise[Time.frameCount % 4], new Rect(UnityEngine.Random.value, UnityEngine.Random.value, W / 128f / (2.2f * s), H / 128f / (2.2f * s)));
                if (Play.TapeEnd > 0.5f) Ui.Text(new Rect(0, H * 0.46f, W, 60f * s), "ЗАПИСЬ ПРЕРВАНА", (int)(34f * s), new Color(1f, 1f, 1f, Mathf.Clamp01((Play.TapeEnd - 0.5f) * 3f) * (UnityEngine.Random.value < 0.15f ? 0.4f : 1f)), TextAnchor.MiddleCenter, true);
            }
            GUI.color = Color.white;
        }

    }
}
