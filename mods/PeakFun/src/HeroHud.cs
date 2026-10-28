// HUD героя 1.12: круглые слоты способностей с радиальной перезарядкой (GL), плашка героя, подсветка готовности. Цвета — из темы.
using System;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        static Material hudMat;

        struct Slot { public string key, name; public float ready; public bool active; public float left; }

        // сектор от верха по часовой, доля frac (0..1) закрашена
        static void Pie(Vector2 c, float r, float frac, Color col)
        {
            if (frac <= 0.001f || Event.current.type != EventType.Repaint) return;
            try
            {
                if (hudMat == null) hudMat = Vfx.Mat(null, Color.white);
                if (hudMat == null) return;
                hudMat.SetPass(0);
                GL.PushMatrix(); GL.LoadPixelMatrix(); GL.Begin(GL.TRIANGLES);
                int n = Mathf.Max(2, Mathf.CeilToInt(56f * frac)); float H = Screen.height;
                for (int i = 0; i < n; i++)
                {
                    float a0 = Mathf.PI * 0.5f - Mathf.PI * 2f * frac * i / n, a1 = Mathf.PI * 0.5f - Mathf.PI * 2f * frac * (i + 1) / n;
                    GL.Color(col); GL.Vertex3(c.x, H - c.y, 0f);
                    GL.Vertex3(c.x + Mathf.Cos(a0) * r, H - (c.y - Mathf.Sin(a0) * r) + 0f, 0f);
                    GL.Vertex3(c.x + Mathf.Cos(a1) * r, H - (c.y - Mathf.Sin(a1) * r) + 0f, 0f);
                }
                GL.End(); GL.PopMatrix();
            }
            catch { }
        }

        static float Frac(float until, float total) { return 1f - Mathf.Clamp01((until - Time.time) / Mathf.Max(0.1f, total)); }

        void DrawHeroHud()
        {
            if (hero == HeroKind.None && grabbed == null) return;
            if (Event.current.type != EventType.Repaint) return;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f), W = Screen.width, H = Screen.height;
            var th = Themes.Now; float t = Time.unscaledTime;
            var slots = new System.Collections.Generic.List<Slot>();
            if (hero != HeroKind.None)
            {
                bool g = hero == HeroKind.Gojo, sk = hero == HeroKind.Sukuna;
                float totA = g ? 4f : sk ? 0.5f : 1.5f, totB = g ? 6f : sk ? 3f : 5f;
                if (IsHoop(hero)) { totA = hoopTot[0]; totB = hoopTot[1]; }
                bool actA = hero == HeroKind.Patriot ? laserOn : g ? blueOn : sk ? disOn : false;
                slots.Add(new Slot { key = keyAb1.Value.ToString(), name = HeroA(hero), ready = actA ? 1f : Frac(cdA, totA), active = actA, left = cdA - Time.time });
                slots.Add(new Slot { key = keyAb2.Value.ToString(), name = HeroB(hero), ready = Frac(cdB, totB), left = cdB - Time.time });
                if (IsHoop(hero))
                {
                    slots.Add(new Slot { key = keyAb3.Value.ToString(), name = HeroC(hero), ready = Frac(cdC, hoopTot[2]), left = cdC - Time.time });
                    float zv = zoneOn ? Mathf.Clamp01((zoneEnd - Time.time) / Mathf.Max(1f, zoneDur)) : (zoneCasting ? 1f : Frac(cdZone, Mathf.Max(1f, zoneCd)));
                    slots.Add(new Slot { key = keyDomain.Value.ToString(), name = "ГОРЯЧАЯ РУКА", ready = zv, active = zoneOn || zoneCasting, left = zoneOn ? zoneEnd - Time.time : cdZone - Time.time });
                }
                else if (IsAnime(hero))
                {
                    slots.Add(new Slot { key = keyAb3.Value.ToString(), name = HeroC(hero), ready = Frac(cdC, g ? 22f : 15f), active = purpleCharge, left = cdC - Time.time });
                    float dv = domActive ? Mathf.Clamp01((domEnd - Time.time) / Mathf.Max(1f, domDur)) : (domCasting ? 1f : Frac(cdDom, Mathf.Max(1f, domCd)));
                    slots.Add(new Slot { key = keyDomain.Value.ToString(), name = "Великий купол", ready = dv, active = domActive || domCasting, left = domActive ? domEnd - Time.time : cdDom - Time.time });
                }
            }
            float d = 76f * s, gap = 14f * s; int n = slots.Count;
            float total = n * d + (n - 1) * gap, x0 = W * 0.5f - total * 0.5f, y0 = H - d - 62f * s;
            if (n > 0)
            {
                // плашка с именем героя
                string nm = HeroName(hero).ToUpper();
                float pw = Mathf.Max(total + 40f * s, 300f * s), px = W * 0.5f - pw * 0.5f, py = y0 - 52f * s;
                Ui.Box(new Rect(px, py, pw, 40f * s), new Color(0, 0, 0, 0.5f), 20f * s);
                Ui.Gradient(new Rect(px + 20f * s, py + 36f * s, pw - 40f * s, 3f * s), 0.95f, t * 0.15f);
                Ui.Text(new Rect(px, py, pw, 38f * s), nm, (int)(24f * s), th.accent, TextAnchor.MiddleCenter, true);
                Ui.Box(new Rect(x0 - 16f * s, y0 - 8f * s, total + 32f * s, d + 52f * s), new Color(0, 0, 0, 0.38f), 22f * s);
            }
            for (int i = 0; i < n; i++)
            {
                var sl = slots[i]; float x = x0 + i * (d + gap); var c = new Vector2(x + d * 0.5f, y0 + d * 0.5f);
                bool rdy = sl.ready >= 0.99f;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f + i);
                // кольцо
                var ring = sl.active ? th.accent2 : rdy ? th.accent : new Color(1, 1, 1, 0.25f);
                if (rdy || sl.active) Ui.Orb(c, d * (1.9f + 0.25f * pulse), new Color(ring.r, ring.g, ring.b, 0.20f + 0.1f * pulse));
                Ui.Box(new Rect(x - 3f * s, y0 - 3f * s, d + 6f * s, d + 6f * s), ring, (d + 6f * s) * 0.5f);
                Ui.Box(new Rect(x, y0, d, d), new Color(0.04f, 0.04f, 0.07f, 0.92f), d * 0.5f);
                // перезарядка — тёмный сектор, оставшаяся часть
                if (!rdy) Pie(c, d * 0.5f - 1f, 1f - sl.ready, new Color(0f, 0f, 0f, 0.7f));
                if (sl.active) Pie(c, d * 0.5f - 1f, Mathf.Clamp01(sl.ready), new Color(th.accent2.r, th.accent2.g, th.accent2.b, 0.35f));
                // клавиша
                Ui.Text(new Rect(x, y0 + 4f * s, d, d - 8f * s), sl.key, (int)(sl.key.Length > 2 ? 22f * s : 34f * s), rdy || sl.active ? Color.white : new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter, true);
                if (!rdy && !sl.active && sl.left > 0.05f)
                    Ui.Text(new Rect(x, y0 + d * 0.62f, d, d * 0.3f), sl.left >= 10f ? Mathf.CeilToInt(sl.left).ToString() : sl.left.ToString("0.0"), (int)(15f * s), th.accent, TextAnchor.MiddleCenter, true);
                Ui.Text(new Rect(x - gap * 0.5f, y0 + d + 6f * s, d + gap, 22f * s), sl.name, (int)(13f * s), rdy || sl.active ? Color.white : new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, true);
            }
            string gs = grabbed != null ? (grabTele ? "Держишь: " : "За горло: ") + Game.Name(grabbed) + "   [" + keyThrow.Value + "] бросить" : (hero != HeroKind.None ? "[" + keyGrab.Value + "] схватить за горло" : "");
            if (gs.Length > 0)
            {
                float gw = 420f * s;
                Ui.Text(new Rect(W * 0.5f - gw * 0.5f, H - 40f * s, gw, 26f * s), gs, (int)(15f * s), grabbed != null ? th.accent2 : new Color(1, 1, 1, 0.65f), TextAnchor.MiddleCenter, true);
            }
            if (Time.time < heroMsgUntil)
                Ui.Text(new Rect(W * 0.5f - 300 * s, H * 0.72f, 600 * s, 34 * s), heroMsg, (int)(22 * s), Color.white, TextAnchor.MiddleCenter, true);
            if (laserOn)
            {
                float cx = W * 0.5f, cy = H * 0.5f;
                Ui.Box(new Rect(cx - 5 * s, cy - 5 * s, 10 * s, 10 * s), new Color(1f, 0.15f, 0.1f, 0.9f), 5 * s);
            }
        }
    }
}
