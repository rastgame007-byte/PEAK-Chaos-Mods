// Кинематографичные способности 1.12: кинокамера (лицо, низкий ракурс, из-за плеча, боковой трек, погоня сверху, отъезд),
// чёрные полосы и «ударный кадр» (белая вспышка и линии скорости). Камера плавно уходит от обычной и плавно возвращается.
using System;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        // 1 лицо  2 низкий орбитальный  3 из-за плеча  4 боковой трек  5 погоня сверху  6 отъезд-откровение
        bool cineOn = true, cineBarsOn = true; int cineKind; float cineT0, cineDur, cineStrength = 1f; bool cineWas; float cineYawSign = 1f;

        bool CineActive { get { return cineOn && Time.unscaledTime < cineT0 + cineDur; } }

        void StartCine(int kind, float dur)
        {
            if (!cineOn || Game.Me() == null || puppetOn) return;
            if (kind == 4) cineYawSign = -cineYawSign;
            cineKind = kind; cineT0 = Time.unscaledTime; cineDur = dur; cineWas = true;
            CamShake.Install(); HookView(); ViewPatch.Install(); ViewPatch.Want = true;
        }

        void TickCine()
        {
            if (cineWas && !CineActive)
            {
                cineWas = false;
                if (!thirdOn && obsMode == 0) ViewPatch.Want = false;
            }
        }

        float CineW()
        {
            float t = Time.unscaledTime - cineT0;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.22f)), b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((cineDur - t) / 0.3f));
            return Mathf.Min(a, b) * Mathf.Clamp01(cineStrength);
        }

        /// <summary>Возвращает true, если кинокамера взяла управление.</summary>
        bool CineApply(Transform tr)
        {
            if (!CineActive || puppetOn) return false;
            var me = Game.Me(); if (me == null) return false;
            float t = Time.unscaledTime - cineT0, u = Mathf.Clamp01(t / cineDur), e = Mathf.SmoothStep(0f, 1f, u), w = CineW();
            Vector3 up = Vector3.up, head = HeadOf(me), center = CenterOf(me);
            Vector3 f = tr.forward; f.y = 0f; f = f.sqrMagnitude < 0.01f ? Vector3.forward : f.normalized;
            Vector3 r = Vector3.Cross(up, f);
            Vector3 pos, look; float fov, roll = 0f;
            switch (cineKind)
            {
                case 1:   // лицо: наезд, широкая оптика сжимается
                    {
                        float d = Mathf.Lerp(1.7f, 0.85f, e);
                        pos = head + f * d + r * Mathf.Lerp(0.18f, 0f, e) + up * 0.04f; look = head + up * 0.02f; fov = Mathf.Lerp(46f, 27f, e); roll = Mathf.Lerp(5f, 0f, e);
                        break;
                    }
                case 2:   // низкий ракурс, облёт на пол-оборота
                    {
                        float ang = Mathf.Lerp(-50f, 40f, e);
                        Vector3 dir = Quaternion.AngleAxis(ang, up) * f;
                        pos = center + dir * Mathf.Lerp(3.6f, 2.6f, e) - up * 0.75f; look = head + up * 0.25f; fov = Mathf.Lerp(62f, 74f, e); roll = Mathf.Lerp(-6f, 3f, e);
                        break;
                    }
                case 3:   // из-за плеча, смотрим туда, куда летит техника
                    {
                        float back = Mathf.Lerp(2.9f, 1.9f, e);
                        pos = head - f * back + r * 0.75f + up * 0.45f; look = head + f * 9f + up * 0.3f; fov = Mathf.Lerp(62f, 50f, e);
                        break;
                    }
                case 4:   // боковой трек вдоль удара
                    {
                        pos = center + r * (3.2f * cineYawSign) + f * Mathf.Lerp(-1.0f, 1.2f, e) + up * 0.35f; look = center + f * 0.9f; fov = 48f; roll = 2f * cineYawSign;
                        break;
                    }
                case 5:   // погоня сверху за пике
                    {
                        pos = center + up * Mathf.Lerp(4.2f, 5.5f, e) - f * 1.4f; look = center - up * 3.5f + f * 0.4f; fov = Mathf.Lerp(80f, 95f, e);
                        break;
                    }
                case 7:   // камера летит за мячом
                    {
                        Vector3 bp = ballGo != null ? ballGo.transform.position : head; Vector3 bv = ballRb != null && !ballRb.isKinematic ? ballRb.velocity : f;
                        Vector3 dv2 = bv.sqrMagnitude > 1f ? bv.normalized : f;
                        pos = bp - dv2 * 3.4f + up * 1.2f + r * 0.9f; look = bp + dv2 * 1.8f; fov = 58f;
                        break;
                    }
                default:  // отъезд: камера уезжает от героя и показывает масштаб
                    {
                        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u));
                        pos = center + f * Mathf.Lerp(2.4f, 12f, k) + up * Mathf.Lerp(0.9f, 5.5f, k) + r * Mathf.Lerp(0.5f, 3f, k); look = head + up * Mathf.Lerp(0f, 1.5f, k); fov = Mathf.Lerp(58f, 68f, k);
                        break;
                    }
            }
            // стены не мешают камере
            Vector3 from = cineKind == 7 && ballGo != null ? ballGo.transform.position : (cineKind == 1 || cineKind == 2 || cineKind == 6 ? head : center), dv = pos - from; float dl = dv.magnitude;
            if (dl > 0.2f) { float cd = ClearDist(from, dv / dl, dl, 0.18f); pos = from + dv / dl * Mathf.Max(0.3f, cd - 0.1f); }
            Quaternion rot = Quaternion.LookRotation((look - pos).sqrMagnitude < 1e-4f ? f : (look - pos), up) * Quaternion.Euler(0f, 0f, roll);
            var cam = Camera.main; float baseFov = cam != null ? cam.fieldOfView : 70f;
            tr.position = Vector3.Lerp(tr.position, pos, w);
            tr.rotation = Quaternion.Slerp(tr.rotation, rot, w);
            CamShake.Fov = Mathf.Lerp(baseFov, fov, w);
            return true;
        }

        // ---------- чёрные полосы ----------
        void DrawCineBars()
        {
            if (!cineBarsOn || !CineActive || Event.current.type != EventType.Repaint) return;
            float k = CineW() * (cineKind == 4 || cineKind == 5 ? 0.5f : 1f), h = Screen.height * 0.11f * k;
            var c = new Color(0, 0, 0, 1f);
            Ui.Box(new Rect(0, 0, Screen.width, h), c, 0f); Ui.Box(new Rect(0, Screen.height - h, Screen.width, h), c, 0f);
        }

        // ---------- ударный кадр ----------
        float impT0 = -9f; int impSeed;

        void ImpactFrame()
        {
            if (!cineOn) return;
            impT0 = Time.unscaledTime; impSeed = UnityEngine.Random.Range(1, 99999);
        }

        void DrawImpactFrame()
        {
            if (Event.current.type != EventType.Repaint) return;
            float t = Time.unscaledTime - impT0; if (t < 0f || t > 0.22f) return;
            float W = Screen.width, H = Screen.height;
            float a = t < 0.06f ? 0.9f : Mathf.Lerp(0.55f, 0f, (t - 0.06f) / 0.16f);
            Ui.Box(new Rect(0, 0, W, H), new Color(1f, 1f, 1f, a), 0f);
            if (t > 0.1f) return;
            try
            {
                if (hudMat == null) hudMat = Vfx.Mat(null, Color.white);
                if (hudMat == null) return;
                hudMat.SetPass(0);
                GL.PushMatrix(); GL.LoadPixelMatrix(); GL.Begin(GL.TRIANGLES);
                var rn = new System.Random(impSeed); Vector2 c = new Vector2(W * 0.5f, H * 0.5f); float R0 = H * 0.14f, R1 = Mathf.Max(W, H);
                for (int i = 0; i < 46; i++)
                {
                    float ang = (float)rn.NextDouble() * Mathf.PI * 2f, wd = 0.006f + (float)rn.NextDouble() * 0.02f, r0 = R0 * (0.8f + (float)rn.NextDouble() * 1.2f);
                    Vector2 d0 = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    Vector2 p0 = c + d0 * r0, p1 = c + new Vector2(Mathf.Cos(ang - wd), Mathf.Sin(ang - wd)) * R1, p2 = c + new Vector2(Mathf.Cos(ang + wd), Mathf.Sin(ang + wd)) * R1;
                    GL.Color(new Color(0, 0, 0, 0.95f));
                    GL.Vertex3(p0.x, H - p0.y, 0f); GL.Vertex3(p1.x, H - p1.y, 0f); GL.Vertex3(p2.x, H - p2.y, 0f);
                }
                GL.End(); GL.PopMatrix();
            }
            catch { }
        }

        // ---------- настройки ----------
        void CineSettings()
        {
            Ui.Header("Кинокамера способностей");
            cineOn = Ui.Toggle("Кинокамера (лицо Альбус, отъезд, пике, удары)", cineOn, "При расширении, Луче, Фуге, сильных ударах камера на мгновение уходит в кинематографичный ракурс");
            cineBarsOn = Ui.Toggle("Чёрные полосы кино", cineBarsOn);
            cineStrength = Ui.Slider("Сила смены ракурса", cineStrength, 0.3f, 1f, "0.00");
            if (Ui.Button("Показать: лицо (Альбус)")) StartCine(1, 1.6f);
            if (Ui.Button("Показать: низкий облёт")) StartCine(2, 1.6f);
            if (Ui.Button("Показать: из-за плеча")) StartCine(3, 1.6f);
            if (Ui.Button("Показать: боковой трек")) StartCine(4, 1.0f);
            if (Ui.Button("Показать: погоня сверху")) StartCine(5, 1.4f);
            if (Ui.Button("Показать: отъезд")) StartCine(6, 2.0f);
            if (Ui.Button("Ударный кадр")) ImpactFrame();
        }
    }
}
