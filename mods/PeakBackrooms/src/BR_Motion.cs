// Level 94 «Motion» (1.30): пастельный городок в стиле stop-motion — игрушечные деревья, цветы, сирены на столбах, фонтан, молочные фургоны.
using System;
using UnityEngine;

namespace PeakBR
{
    internal static partial class LevelBuilder
    {
        static void Motion(Ctx x, int cx, int cy, Vector3 lc, bool open, bool nearSpawn)
        {
            if (nearSpawn) return; double q = x.rnd.NextDouble(); var b = x.b; float R0 = 0f;
            var trunk = Mk(x, "m_tr", 0.5f, 0.34f, 0.2f, 0.15f); var crown = Mk(x, "m_cr", 0.45f, 0.78f, 0.38f, 0.15f); var crown2 = Mk(x, "m_cr2", 0.62f, 0.86f, 0.45f, 0.15f);
            if (open && q < 0.1)
            {   // молочный фургон 1930-х
                bool rot = x.rnd.Next(2) == 0; Car(x, lc + new Vector3(rot ? 0f : (x.R() - 0.5f), 0, rot ? (x.R() - 0.5f) : 0f), rot, x.rnd.Next(2) == 0 ? new Color(0.96f, 0.93f, 0.82f) : new Color(0.62f, 0.8f, 0.88f));
            }
            else if (q < 0.24)
            {   // игрушечное дерево: ствол и «глиняная» крона из трёх ярусов
                Vector3 p = lc + new Vector3(x.R() > 0.5f ? 1.2f : -1.2f, 0, x.R() > 0.5f ? -1.2f : 1.2f); float h = 2.2f + x.R() * 0.8f;
                trunk.Cyl(p, p + Vector3.up * h, 0.16f, 8); x.Col("BR_tree_player", p + Vector3.up * h * 0.5f, new Vector3(0.4f, h, 0.4f));
                crown.Cyl(p + Vector3.up * (h - 0.1f), p + Vector3.up * (h + 0.9f), 1.15f, 12); crown2.Cyl(p + Vector3.up * (h + 0.8f), p + Vector3.up * (h + 1.6f), 0.85f, 12); crown.Cyl(p + Vector3.up * (h + 1.5f), p + Vector3.up * (h + 2.1f), 0.5f, 10);
            }
            else if (q < 0.34)
            {   // клумба с цветами
                var stem = Mk(x, "m_st", 0.3f, 0.6f, 0.25f, 0.1f); Color[] cs = { new Color(0.95f, 0.45f, 0.55f), new Color(0.98f, 0.85f, 0.3f), new Color(0.6f, 0.55f, 0.95f), new Color(1f, 0.6f, 0.3f) };
                Vector3 c0 = lc + new Vector3((x.R() - 0.5f) * 2.4f, 0, (x.R() - 0.5f) * 2.4f);
                for (int i = 0; i < 7; i++)
                {
                    Vector3 p = c0 + new Vector3((x.R() - 0.5f) * 0.9f, 0, (x.R() - 0.5f) * 0.9f); float h = 0.3f + x.R() * 0.25f; Color cc = cs[x.rnd.Next(cs.Length)];
                    stem.Cyl(p, p + Vector3.up * h, 0.012f, 3, false); Mk(x, "m_f" + ColKey(cc), cc.r, cc.g, cc.b, 0.2f).Cyl(p + Vector3.up * h, p + Vector3.up * (h + 0.05f), 0.07f, 7);
                }
            }
            else if (q < 0.39)
            {   // столб-сирена с рупорами (вечно играет весёлую старую музыку)
                var pole = Mk(x, "m_po", 0.55f, 0.56f, 0.6f, 0.4f, 0.4f); var horn = Gl(x, "m_ho", 0.95f, 0.85f, 0.4f, 0.6f); Vector3 p = lc + new Vector3(1.4f, 0, -1.4f);
                pole.Cyl(p, p + Vector3.up * 4.2f, 0.07f, 8); x.Col("BR_pole_player", p + Vector3.up * 2f, new Vector3(0.2f, 4f, 0.2f));
                for (int i = 0; i < 3; i++) { float a = i * 2.094f; Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); horn.Cyl(p + Vector3.up * 4.05f + d * 0.12f, p + Vector3.up * 4.05f + d * 0.5f, 0.08f, 8, true); horn.Cyl(p + Vector3.up * 4.05f + d * 0.5f, p + Vector3.up * 4.05f + d * 0.72f, 0.2f, 10, false); }
            }
            else if (open && q < 0.43 && b.g.dist[cx, cy] > 8)
            {   // фонтан «Миндальная вода»
                var stone = Mk(x, "m_fs", 0.78f, 0.76f, 0.7f, 0.3f); var wat = Gl(x, "m_fw", 0.45f, 0.75f, 0.95f, 0.5f); Vector3 p = lc;
                stone.Cyl(p, p + Vector3.up * 0.55f, 1.5f, 16); wat.Cyl(p + Vector3.up * 0.53f, p + Vector3.up * 0.58f, 1.35f, 16, true);
                stone.Cyl(p, p + Vector3.up * 1.3f, 0.2f, 8); stone.Cyl(p + Vector3.up * 1.1f, p + Vector3.up * 1.3f, 0.6f, 12); wat.Cyl(p + Vector3.up * 1.28f, p + Vector3.up * 1.31f, 0.52f, 12, true); stone.Cyl(p + Vector3.up * 1.3f, p + Vector3.up * 1.9f, 0.07f, 6);
                x.Col("BR_fountain_player", p + Vector3.up * 0.5f, new Vector3(3f, 1f, 3f));
            }
            else if (q < 0.49)
            {   // белый штакетник вдоль стены
                var wood = Mk(x, "m_pf", 0.97f, 0.96f, 0.92f, 0.2f);
                for (int d = 0; d < 4; d++) { if (b.g.Pass(cx, cy, d) || x.rnd.NextDouble() > 0.5) continue; Vector3 dd = new Vector3(Grid.DX[d], 0, Grid.DY[d]); Vector3 rt = new Vector3(-dd.z, 0, dd.x); Vector3 bp = lc + dd * (b.C * 0.5f - b.L.wallT * 0.5f - 0.5f);
                    for (int k = -4; k <= 4; k++) { Vector3 p = bp + rt * (k * 0.38f); wood.Box(p + Vector3.up * 0.4f, new Vector3(0.07f, 0.8f, 0.07f)); }
                    wood.Box(bp + Vector3.up * 0.6f, Mathf.Abs(dd.x) > 0.5f ? new Vector3(0.04f, 0.05f, 3.2f) : new Vector3(3.2f, 0.05f, 0.04f)); wood.Box(bp + Vector3.up * 0.25f, Mathf.Abs(dd.x) > 0.5f ? new Vector3(0.04f, 0.05f, 3.2f) : new Vector3(3.2f, 0.05f, 0.04f)); }
            }
            R0 += 0f;
        }
    }
}
