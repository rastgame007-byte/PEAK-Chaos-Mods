// Стрелка к выходу (1.30): через некоторое время после входа на уровень на экране появляется стрелка, указывающая ПУТЬ
// (по коридорам, а не по прямой) к ближайшему выходу, с расстоянием в метрах. Следует за поворотом камеры.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static class Arrow
    {
        public const float Delay = 120f;   // секунд после входа на уровень
        static Built owner; static List<int[,]> dists = new List<int[,]>(); static bool unlocked; static float nextCalc; static Vector3 target; static float distM = -1f; static bool have; static Texture2D tex;

        static Texture2D Tex()
        {
            if (tex != null) return tex;
            tex = Vfx.Tex(128, 128, (u, v) =>
            {
                float x = (u - 0.5f), y = v;   // стрелка вверх
                float dShaft = Mathf.Max(Mathf.Abs(x) - 0.085f, Mathf.Max(0.12f - y, y - 0.58f));
                float hw = (0.93f - y) * 0.52f; float dHead = Mathf.Max(Mathf.Abs(x) - hw, Mathf.Max(0.5f - y, y - 0.93f)); float d = Mathf.Min(dShaft, dHead);
                if (d < 0f) { float rim = Mathf.Clamp01(-d * 38f); Color c = Color.Lerp(new Color(1f, 1f, 0.9f, 1f), new Color(1f, 0.78f, 0.18f, 1f), rim); c.a = 1f; return c; }
                float glow = Mathf.Exp(-d * 18f) * 0.55f; return new Color(1f, 0.85f, 0.35f, glow);
            });
            return tex;
        }

        public static void Reset() { owner = null; dists.Clear(); unlocked = false; have = false; distM = -1f; nextCalc = 0f; }

        static void Calc(Vector3 pc)
        {
            var b = Play.Cur; if (b == null) return; var g = b.g;
            if (owner != b) { owner = b; dists.Clear(); foreach (var e in g.exits) dists.Add(g.Bfs(e.x, e.y)); }
            int px, py; have = false; if (!b.CellOf(pc, out px, out py) || dists.Count == 0) return;
            int bi = -1, bd = int.MaxValue; for (int i = 0; i < dists.Count; i++) { int d = dists[i][px, py]; if (d >= 0 && d < bd) { bd = d; bi = i; } }
            if (bi < 0) return; var D = dists[bi]; int cx = px, cy = py;
            for (int s = 0; s < 3; s++)
            {   // идём по градиенту к выходу на несколько клеток вперёд — стрелка показывает поворот заранее
                if (D[cx, cy] <= 0) break; bool moved = false;
                for (int k = 0; k < 4; k++) if (g.Pass(cx, cy, k)) { int nx = cx + Grid.DX[k], ny = cy + Grid.DY[k]; if (D[nx, ny] >= 0 && D[nx, ny] == D[cx, cy] - 1) { cx = nx; cy = ny; moved = true; break; } }
                if (!moved) break;
            }
            target = b.CellC(cx, cy); if (D[cx, cy] <= 0 && bi < b.exitPos.Count) target = b.exitPos[bi];
            distM = bd * b.C; have = true;
        }

        public static void Hud(float W, float H, float s, Camera cam)
        {
            if (!Play.On || Play.Cur == null || Play.Catching || cam == null || Play.Cur.g.exits.Count == 0) return;
            float t = Time.time - Play.LevelT0;
            if (t < Delay)
            {
                if (t > Delay - 45f) Ui.Text(new Rect(W * 0.5f - 160f * s, 54f * s, 320f * s, 16f * s), "стрелка к выходу проснётся через " + Mathf.CeilToInt(Delay - t) + " с", (int)(10f * s), new Color(1f, 0.9f, 0.6f, 0.45f), TextAnchor.MiddleCenter);
                return;
            }
            if (!unlocked) { unlocked = true; Ui.Toast("Стрелка проснулась: она ведёт по коридорам к ближайшему выходу", Ui.Good); try { Snd2.Flat("click", 0.6f); } catch { } }
            Vector3 pc = cam.transform.position; if (Time.time >= nextCalc) { nextCalc = Time.time + 0.25f; try { Calc(pc); } catch (Exception e) { Plugin.Warn("стрелка", e); have = false; } }
            if (!have) return;
            Vector3 to = target - pc; to.y = 0f; Vector3 fw = cam.transform.forward; fw.y = 0f; if (to.sqrMagnitude < 0.01f || fw.sqrMagnitude < 0.0001f) return;
            float ang = Vector3.SignedAngle(fw.normalized, to.normalized, Vector3.up);
            float sz = 86f * s; var c = new Vector2(W * 0.5f, 92f * s); float pulse = 0.82f + 0.18f * Mathf.Sin(Time.time * 3f);
            var back = new Rect(c.x - 62f * s, c.y - 52f * s, 124f * s, 122f * s); Ui.Box(back, new Color(0.02f, 0.015f, 0.01f, 0.42f), 10f * s); Ui.Border(back, new Color(1f, 0.85f, 0.4f, 0.35f), 1f);
            var old = GUI.matrix; var oc = GUI.color;
            try { GUIUtility.RotateAroundPivot(ang, c); GUI.color = new Color(1f, 1f, 1f, pulse); GUI.DrawTexture(new Rect(c.x - sz * 0.5f, c.y - sz * 0.5f - 4f * s, sz, sz), Tex(), ScaleMode.StretchToFill, true); }
            finally { GUI.matrix = old; GUI.color = oc; }
            Ui.Text(new Rect(c.x - 60f * s, c.y + 38f * s, 120f * s, 18f * s), "ВЫХОД · " + Mathf.RoundToInt(distM) + " м", (int)(11f * s), new Color(1f, 0.93f, 0.7f, 0.95f), TextAnchor.MiddleCenter, true);
        }
    }
}
