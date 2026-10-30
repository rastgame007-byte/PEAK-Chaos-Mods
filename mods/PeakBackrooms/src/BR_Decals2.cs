// Размещение картинок и мелких моделей на стенах (1.28).
using System;
using UnityEngine;

namespace PeakBR
{
    internal static partial class LevelBuilder
    {
        static void DecorWall(Ctx x, int cx, int cy, Vector3 cc, bool nearSpawn)
        {
            if (nearSpawn) return; var b = x.b; var L = b.L; var g = b.g; string id = L.id; float Cw = b.C, H = b.H, T = L.wallT, iw = (Cw - T) * 0.5f; Vector3 lc = cc - b.baseP; var rnd = x.rnd;
            float prob; var pool = Decals.Pool(id, out prob); bool inter = Decals.Interior(id); prob = id == "0" ? 0.24f : Mathf.Min(0.3f, prob * 2.4f); if (!inter && pool == null) return;
            for (int d = 0; d < 4; d++)
            {
                if (g.Pass(cx, cy, d)) continue;
                Vector3 dd = new Vector3(Grid.DX[d], 0, Grid.DY[d]), rt = Vector3.Cross(Vector3.up, dd), up = Vector3.up; bool alongX = Mathf.Abs(rt.x) > 0.5f; float room = Cw - T - 0.5f;
                if (pool != null && rnd.NextDouble() < prob)
                {   // картина / плакат / знак
                    int idx = pool[rnd.Next(pool.Length)]; var sz = Decals.Size[idx]; float w = sz[0], h = sz[1], yc = Mathf.Min(sz[2], H - h * 0.5f - 0.25f);
                    Vector3 c = lc + dd * (iw - 0.02f) + up * yc + rt * (((float)rnd.NextDouble() - 0.5f) * Mathf.Max(0f, room - w)); int col = idx % 4, row = idx / 4; const float e = 0.004f;
                    float u0 = col * 0.25f + e, u1 = col * 0.25f + 0.25f - e, v0 = row * 0.25f + e, v1 = row * 0.25f + 0.25f - e;
                    var dm = x.M("decal", () => BrGfx.Surf(Decals.Atlas(), Color.white, 0.3f));
                    dm.Quad(c - rt * (w * 0.5f) - up * (h * 0.5f), c - rt * (w * 0.5f) + up * (h * 0.5f), c + rt * (w * 0.5f) + up * (h * 0.5f), c + rt * (w * 0.5f) - up * (h * 0.5f), -dd, new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0));
                    continue;
                }
                if (!inter || rnd.NextDouble() > 0.05) continue;
                Vector3 face = lc + dd * (iw - 0.005f) + rt * (((float)rnd.NextDouble() - 0.5f) * Mathf.Max(0f, room - 0.3f)); double q = rnd.NextDouble();
                if (q < 0.34)
                {   // розетка
                    var pl = Mk(x, "ot_p", 0.86f, 0.85f, 0.8f, 0.4f); var sl = Mk(x, "ot_s", 0.06f, 0.06f, 0.06f, 0.3f); Vector3 p = face + up * 0.38f - dd * 0.006f; Vector3 sx = new Vector3(alongX ? 0.075f : 0.012f, 0.115f, alongX ? 0.012f : 0.075f);
                    pl.Box(p, sx); for (int k = -1; k <= 1; k += 2) sl.Box(p - dd * 0.007f + up * 0.012f + rt * (k * 0.014f), new Vector3(alongX ? 0.008f : 0.004f, 0.026f, alongX ? 0.004f : 0.008f)); sl.Box(p - dd * 0.007f - up * 0.026f, new Vector3(alongX ? 0.012f : 0.004f, 0.012f, alongX ? 0.004f : 0.012f));
                }
                else if (q < 0.5)
                {   // выключатель
                    var pl = Mk(x, "ot_p", 0.86f, 0.85f, 0.8f, 0.4f); var kb = Mk(x, "ot_k", 0.92f, 0.91f, 0.86f, 0.45f); Vector3 p = face + up * 1.2f - dd * 0.006f;
                    pl.Box(p, new Vector3(alongX ? 0.08f : 0.012f, 0.12f, alongX ? 0.012f : 0.08f)); kb.Box(p - dd * 0.012f, new Vector3(alongX ? 0.026f : 0.012f, 0.05f, alongX ? 0.012f : 0.026f));
                }
                else if (q < 0.68)
                {   // огнетушитель на кронштейне
                    var red = Mk(x, "ex_r", 0.72f, 0.06f, 0.05f, 0.55f, 0.2f); var blk = Mk(x, "ex_b", 0.05f, 0.05f, 0.05f, 0.4f, 0.4f); var met = Mk(x, "ex_m", 0.7f, 0.7f, 0.72f, 0.7f, 0.9f);
                    Vector3 p = face - dd * 0.09f; red.Cyl(p + up * 0.3f, p + up * 0.76f, 0.07f, 14); red.Cyl(p + up * 0.76f, p + up * 0.84f, 0.035f, 10); met.Box(p + up * 0.88f - dd * 0.02f * 0f, new Vector3(0.06f, 0.035f, 0.06f));
                    blk.Cyl(p + up * 0.86f - dd * 0.0f, p + up * 0.86f - dd * 0.0f + rt * 0.07f, 0.01f, 6, false); blk.Cyl(p + up * 0.84f, p + up * 0.62f + rt * 0.1f, 0.012f, 6, false); blk.Cyl(p + up * 0.58f, p + up * 0.62f, 0.045f, 8);
                    met.Cyl(p + up * 0.55f, p + up * 0.58f, 0.078f, 12); met.Cyl(p + up * 0.46f, p + up * 0.49f, 0.078f, 12); blk.Box(face + up * 0.52f - dd * 0.02f, new Vector3(alongX ? 0.05f : 0.03f, 0.3f, alongX ? 0.03f : 0.05f));
                }
                else if (q < 0.78)
                {   // пожарная кнопка
                    var red = Mk(x, "fa_r", 0.72f, 0.05f, 0.04f, 0.5f); var wh = Mk(x, "fa_w", 0.92f, 0.92f, 0.9f, 0.4f); Vector3 p = face + up * 1.3f - dd * 0.02f;
                    red.Box(p, new Vector3(alongX ? 0.11f : 0.04f, 0.13f, alongX ? 0.04f : 0.11f)); wh.Box(p - dd * 0.022f, new Vector3(alongX ? 0.05f : 0.006f, 0.06f, alongX ? 0.006f : 0.05f));
                }
                else if (q < 0.89)
                {   // вентиляционная решётка
                    var fr = Mk(x, "vt_f", 0.55f, 0.56f, 0.55f, 0.5f, 0.6f); var sl = Mk(x, "vt_s", 0.03f, 0.03f, 0.03f, 0.3f); Vector3 p = face + up * (H - 0.5f) - dd * 0.012f;
                    fr.Box(p, new Vector3(alongX ? 0.62f : 0.025f, 0.26f, alongX ? 0.025f : 0.62f)); sl.Box(p - dd * 0.004f, new Vector3(alongX ? 0.56f : 0.025f, 0.2f, alongX ? 0.025f : 0.56f));
                    for (int k = -2; k <= 2; k++) fr.Box(p - dd * 0.008f + up * (k * 0.035f), new Vector3(alongX ? 0.58f : 0.012f, 0.012f, alongX ? 0.012f : 0.58f));
                }
                else if (id == "0" || id == "1" || id == "4" || id == "!" || id == "6" || id == "3")
                {   // камера наблюдения
                    var body = Mk(x, "cam_b", 0.72f, 0.73f, 0.72f, 0.5f, 0.3f); var lens = Mk(x, "cam_l", 0.02f, 0.02f, 0.025f, 0.9f); var led = Gl(x, "cam_led", 1f, 0.05f, 0.04f, 2.6f); Vector3 p = face + up * (H - 0.32f) - dd * 0.15f;
                    body.Box(p, new Vector3(alongX ? 0.09f : 0.24f, 0.09f, alongX ? 0.24f : 0.09f)); body.Cyl(face + up * (H - 0.32f) - dd * 0.01f, face + up * (H - 0.32f) - dd * 0.06f, 0.03f, 8);
                    lens.Cyl(p - dd * 0.12f, p - dd * 0.15f, 0.035f, 10); led.Box(p - dd * 0.11f + up * 0.05f + rt * 0.03f, new Vector3(0.012f, 0.012f, 0.012f));
                }
            }
        }
    }
}
