// 1.30: настоящие 3D-стебли пшеницы вдоль стен поля (Level 10) и препятствия на бегу (Level «!»).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal static partial class LevelBuilder
    {
        // ---- пшеница 1.32: широкие двусторонние ленточные листья, сужающиеся к кончику; пучки вдоль стен, низкие кустики на полу, колосья ----
        static void WBlade(MeshB m1, MeshB m2, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 wd, float w0)
        {
            float w1 = w0 * 0.72f; Vector3 a = p0 - wd * w0, b = p0 + wd * w0, c = p1 + wd * w1, d = p1 - wd * w1;
            Vector3 n1 = Vector3.Cross(wd, (p1 - p0).normalized).normalized; if (n1.sqrMagnitude < 0.01f) n1 = Vector3.up;
            Vector2 u0 = new Vector2(0, 0), u1 = new Vector2(1, 0), u2 = new Vector2(1, 1), u3 = new Vector2(0, 1);
            m1.Quad(a, b, c, d, n1, u0, u1, u2, u3); m1.Quad(a, b, c, d, -n1, u0, u1, u2, u3);
            Vector3 e = p1 - wd * w1 * 0.92f, f = p1 + wd * w1 * 0.92f; Vector3 n2 = Vector3.Cross(wd, (p2 - p1).normalized).normalized; if (n2.sqrMagnitude < 0.01f) n2 = Vector3.up;
            m2.Quad(e, f, p2, p2, n2, u0, u1, u2, u3); m2.Quad(e, f, p2, p2, -n2, u0, u1, u2, u3);
        }

        static void Wheat(Ctx x, int cx, int cy, Vector3 lc)
        {
            var b = x.b; var g = b.g; float C = b.C, H = b.H, T = b.L.wallT; float half = C * 0.5f - T * 0.5f - 0.03f; int q = Play.Qual;
            var la = Mk(x, "wh_a", 0.40f, 0.42f, 0.13f, 0.1f); var lb = Mk(x, "wh_b", 0.80f, 0.64f, 0.20f, 0.12f); var lcc = Mk(x, "wh_c", 0.92f, 0.78f, 0.34f, 0.12f);
            var st = Mk(x, "wh_s", 0.62f, 0.5f, 0.16f, 0.1f); var ear = Mk(x, "wh_e", 0.86f, 0.64f, 0.18f, 0.15f); var ear2 = Mk(x, "wh_f", 0.7f, 0.5f, 0.14f, 0.15f);
            // низкие кустики на полу — клетка не выглядит голой
            int tufts = q >= 3 ? 0 : (q >= 2 ? 1 : 2);
            for (int t = 0; t < tufts; t++)
            {
                Vector3 c0 = lc + new Vector3((x.R() - 0.5f) * (C - T - 0.6f), 0f, (x.R() - 0.5f) * (C - T - 0.6f)); int nb = 5;
                for (int k = 0; k < nb; k++)
                {
                    float ang = (k + x.R()) / nb * Mathf.PI * 2f; Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)); float hh = 0.5f + 0.4f * x.R();
                    Vector3 p0 = c0 + dir * 0.05f, p1 = p0 + Vector3.up * hh * 0.6f + dir * (0.1f + 0.1f * x.R()), p2 = p0 + Vector3.up * hh * 0.88f + dir * (0.28f + 0.2f * x.R());
                    WBlade(la, x.rnd.Next(2) == 0 ? lb : lcc, p0, p1, p2, new Vector3(-dir.z, 0, dir.x), 0.035f + 0.015f * x.R());
                }
            }
            for (int d = 0; d < 4; d++)
            {
                if (g.Pass(cx, cy, d)) continue; Vector3 dd = new Vector3(Grid.DX[d], 0, Grid.DY[d]); Vector3 rt = new Vector3(-dd.z, 0, dd.x);
                int bundles = q >= 3 ? 3 : (q >= 2 ? 4 : 5), leaves = q >= 3 ? 6 : (q >= 2 ? 8 : 10);
                for (int k = 0; k < bundles; k++)
                {
                    float u = ((k + x.R()) / bundles - 0.5f) * (C - T - 0.25f); Vector3 c0 = lc + dd * (half - x.R() * 0.22f) + rt * u; c0.y = 0f;
                    float tall = 1.7f + 0.8f * x.R();
                    for (int l = 0; l < leaves; l++)
                    {
                        float hgt = tall * (0.62f + 0.38f * x.R()), lean = 0.2f + 0.4f * x.R(); float yaw = (x.R() - 0.5f) * 2.4f; Vector3 side = rt * Mathf.Sin(yaw) * 0.35f * lean;
                        Vector3 p0 = c0 + rt * ((x.R() - 0.5f) * 0.3f) - dd * (x.R() * 0.06f);
                        Vector3 p1 = p0 + Vector3.up * hgt * 0.55f - dd * lean * 0.3f + side * 0.4f, p2 = p0 + Vector3.up * hgt * (0.9f + 0.1f * x.R()) - dd * lean + side;
                        Vector3 wd = (rt * Mathf.Cos(yaw) + dd * Mathf.Sin(yaw) * 0.6f).normalized;
                        WBlade(la, x.rnd.Next(3) == 0 ? lcc : lb, p0, p1, p2, wd, 0.03f + 0.02f * x.R());
                    }
                    if (x.R() < 0.6f)
                    {   // колосистый стебель — поверх листьев
                        float hgt = tall + 0.1f + 0.3f * x.R(), lean = 0.15f + 0.3f * x.R(); Vector3 sw = rt * ((x.R() - 0.5f) * 0.25f);
                        Vector3 p0 = c0, p1 = p0 + Vector3.up * hgt * 0.62f - dd * lean * 0.3f + sw * 0.4f, p2 = p0 + Vector3.up * hgt - dd * lean + sw;
                        st.Cyl(p0, p1, 0.012f, 3, false); st.Cyl(p1, p2, 0.008f, 3, false);
                        Vector3 dir = (Vector3.up * 0.9f - dd * 0.45f + sw * 0.8f).normalized; (x.R() < 0.5f ? ear : ear2).Cyl(p2 - dir * 0.02f, p2 + dir * 0.24f, 0.026f, 4, true);
                    }
                }
            }
        }

        /// <summary>«Беги!»: перевёрнутые каталки, шкафчики, обломки и стопки мебели — слалом: зазор не меньше 1.5 м, стороны чередуются.</summary>
        static void RunObstacles(Ctx x, int cx, int cy, Vector3 lc, bool nearSpawn)
        {
            var b = x.b; var g = b.g; if (nearSpawn || g.dist == null || g.dist[cx, cy] < 3) return;
            bool ew = g.Pass(cx, cy, 0) || g.Pass(cx, cy, 1), ns = g.Pass(cx, cy, 2) || g.Pass(cx, cy, 3); if (ew == ns) return;
            foreach (var e in g.exits) if (Mathf.Abs(e.x - cx) + Mathf.Abs(e.y - cy) < 2) return;
            if (x.rnd.NextDouble() > 0.55) return;
            float C = b.C, T = b.L.wallT, inner = C - T, gap = 1.55f, ow = inner - gap; float side = (((cx * 7 + cy * 13) & 1) == 0) ? 1f : -1f;
            Vector3 along = ew ? Vector3.right : Vector3.forward, cross = ew ? Vector3.forward : Vector3.right;
            Vector3 c = lc + along * ((x.R() - 0.5f) * 1.0f) + cross * (side * (inner * 0.5f - ow * 0.5f - 0.02f)); c.y = 0f;
            Func<float, float, Vector3> sz = (thick, h) => ew ? new Vector3(thick, h, ow) : new Vector3(ow, h, thick);
            var metal = Mk(x, "ob_m", 0.6f, 0.63f, 0.64f, 0.5f, 0.6f); var cloth = Mk(x, "ob_c", 0.72f, 0.7f, 0.64f, 0.2f); var rub = Mk(x, "ob_r", 0.34f, 0.32f, 0.29f, 0.1f);
            var lock_ = Mk(x, "ob_l", 0.3f, 0.07f, 0.07f, 0.4f, 0.3f); var blood = Mk(x, "ob_b", 0.25f, 0.02f, 0.02f, 0.8f); var wood = Mk(x, "ob_w", 0.4f, 0.28f, 0.16f, 0.2f);
            int kind = x.rnd.Next(5); float yaw = ew ? 0f : 90f; float h0 = 1.0f;
            switch (kind)
            {
                case 0: // перевёрнутая каталка: матрас-стенка, рельсы и колёса
                    cloth.Box(c + Vector3.up * 0.55f, sz(0.2f, 0.85f)); metal.Box(c + Vector3.up * 0.98f, sz(0.08f, 0.06f)); metal.Box(c + Vector3.up * 0.1f, sz(0.08f, 0.06f));
                    for (int i = -1; i <= 1; i += 2) { Vector3 w = c + cross * (i * ow * 0.38f); metal.Cyl(w + along * 0.1f - Vector3.up * 0f + Vector3.up * 0.55f, w + along * 0.32f + Vector3.up * 0.55f, 0.03f, 6, true); metal.Cyl(w + along * 0.3f + Vector3.up * 0.55f - Vector3.up * 0.12f, w + along * 0.3f + Vector3.up * 0.55f + Vector3.up * 0.12f, 0.06f, 8, true); }
                    blood.Box(c + along * 0.5f + Vector3.up * 0.012f, sz(0.9f, 0.012f) * 0.9f); h0 = 1.0f; break;
                case 1: // упавший шкафчик
                    lock_.Box(c + Vector3.up * 0.45f, sz(0.7f, 0.9f)); metal.Box(c + along * (0.36f) + Vector3.up * 0.45f, sz(0.03f, 0.8f) * 0.96f);
                    for (int i = 0; i < 3; i++) metal.Box(c + along * 0.37f + cross * ((i - 1) * ow * 0.3f) + Vector3.up * 0.65f, ew ? new Vector3(0.02f, 0.03f, 0.35f) : new Vector3(0.35f, 0.03f, 0.02f)); h0 = 0.9f; break;
                case 2: // обломки потолка
                    for (int i = 0; i < 6; i++) { Vector3 o = c + along * ((x.R() - 0.5f) * 0.7f) + cross * ((x.R() - 0.5f) * ow * 0.8f) + Vector3.up * (0.1f + x.R() * 0.55f); rub.BoxY(o, new Vector3(0.4f + x.R() * 0.5f, 0.2f + x.R() * 0.3f, 0.4f + x.R() * 0.5f), x.R() * 180f); }
                    h0 = 0.95f; break;
                case 3: // груда стульев и столов
                    wood.Box(c + Vector3.up * 0.4f, sz(0.5f, 0.06f) * 1f + Vector3.zero); wood.BoxY(c + Vector3.up * 0.55f, sz(0.6f, 0.05f) * 0.9f, 25f); wood.BoxY(c + Vector3.up * 0.8f, sz(0.55f, 0.05f) * 0.7f, -35f);
                    for (int i = -1; i <= 1; i += 2) for (int j = -1; j <= 1; j += 2) wood.Cyl(c + along * (i * 0.2f) + cross * (j * ow * 0.4f), c + along * (i * 0.2f) + cross * (j * ow * 0.4f) + Vector3.up * 0.95f, 0.03f, 5, true);
                    h0 = 1.0f; break;
                default: // тележка с мешками
                    metal.Box(c + Vector3.up * 0.35f, sz(0.5f, 0.05f)); cloth.BoxY(c + Vector3.up * 0.6f, sz(0.45f, 0.4f) * 0.85f, 10f); metal.Cyl(c + Vector3.up * 0.4f + cross * (ow * 0.4f), c + Vector3.up * 1.1f + cross * (ow * 0.4f) + along * 0.4f, 0.025f, 6, true); h0 = 1.0f; break;
            }
            x.Col("BR_obstacle_player", c + Vector3.up * (h0 * 0.5f), sz(0.8f, h0));
        }
    }
}
