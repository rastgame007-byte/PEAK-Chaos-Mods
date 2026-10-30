// Планировки 1.26.2: у популярных уровней — настоящие узнаваемые карты, а не вариации одного генератора.
// Станция (вложенные кольца машинных залов), Офис (кабинеты по периметру + ядро с лифтами + open-space),
// Отель (гребёнка коридоров с номерами и лобби), Бассейны (анфилада залов), Lights Out (лабиринт с коридорами в 2 клетки),
// Гараж (парковочные ряды + служебные блоки), Пригород (ряды домов вдоль улиц), Поле (тропы в пшенице + ферма),
// Таласофобия (острова на воде и мостки).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal sealed partial class Grid
    {
        /// <summary>Убирает тупики: у большинства тупиковых клеток открывается ещё один проход (лабиринт с петлями, без «закрытых комнат»).</summary>
        public void Braid(System.Random rnd, double p)
        {
            for (int x = 1; x < n - 1; x++) for (int y = 1; y < n - 1; y++)
            {
                if (solid != null && solid[x, y]) continue; int cnt = 0; for (int d = 0; d < 4; d++) if (Pass(x, y, d)) cnt++;
                if (cnt != 1 || rnd.NextDouble() > p) continue;
                var opts = new List<int>(); for (int d = 0; d < 4; d++) { int nx = x + DX[d], ny = y + DY[d]; if (!Pass(x, y, d) && nx >= 1 && ny >= 1 && nx < n - 1 && ny < n - 1 && (solid == null || !solid[nx, ny])) opts.Add(d); }
                if (opts.Count > 0) Link(x, y, opts[rnd.Next(opts.Count)]);
            }
        }

        // ---------- помощники ----------
        void SetV(int x, int y, bool v) { if (x >= 0 && x <= n && y >= 0 && y < n) wv[x, y] = v; }
        void SetH(int x, int y, bool v) { if (x >= 0 && x < n && y >= 0 && y <= n) wh[x, y] = v; }
        void Room(int x0, int y0, int w, int h, bool room)
        {
            if (w <= 0 || h <= 0) return; LinkRect(x0, y0, w, h); if (room) MarkRoom(x0, y0, w, h);
        }
        void WallBox(int x0, int y0, int w, int h)
        {
            for (int x = x0; x < x0 + w; x++) { SetH(x, y0, true); SetH(x, y0 + h, true); }
            for (int y = y0; y < y0 + h; y++) { SetV(x0, y, true); SetV(x0 + w, y, true); }
        }
        List<int[]> Chunks(System.Random rnd, int s0, int s1, int lo, int hi)
        {
            var l = new List<int[]>(); int pos = s0;
            while (pos <= s1) { int len = lo + rnd.Next(hi - lo + 1); if (s1 - (pos + len - 1) < lo) len = s1 - pos + 1; l.Add(new[] { pos, pos + len - 1 }); pos += len; }
            return l;
        }
        void SolidUnused(bool[,] used) { for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) if (!used[x, y]) MarkSolid(x, y, 1, 1); }
        void Line(int x0, int y0, int x1, int y1, bool[,] used)   // Г-образная дорожка в 1 клетку (сначала по x, потом по y)
        {
            int x = x0, y = y0; used[x, y] = true;
            while (x != x1) { int d = x1 > x ? 0 : 1; Link(x, y, d); x += DX[d]; used[x, y] = true; }
            while (y != y1) { int d = y1 > y ? 2 : 3; Link(x, y, d); y += DY[d]; used[x, y] = true; }
        }

        // ================= L3: электростанция — вложенные кольца =================
        public void GenStation(System.Random rnd)
        {
            const int P = 6; var rings = new List<int>(); int a = 2;
            while (n - 2 * (a + 1) >= 14) { rings.Add(a); a += P; }
            if (rings.Count == 0) { Room(1, 1, n - 2, n - 2, false); return; }
            MarkSolid(0, 0, n, 2); MarkSolid(0, n - 2, n, 2); MarkSolid(0, 0, 2, n); MarkSolid(n - 2, 0, 2, n);
            var isRing = new HashSet<int>(rings);
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++)
            {
                int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(n - 1 - x, n - 1 - y)); if (!isRing.Contains(d)) continue;
                int d1 = Mathf.Min(Mathf.Min(x + 1, y), Mathf.Min(n - 2 - x, n - 1 - y)), d2 = Mathf.Min(Mathf.Min(x, y + 1), Mathf.Min(n - 1 - x, n - 2 - y));
                if (x + 1 < n && d1 == d) Open(x, y, x + 1, y); if (y + 1 < n && d2 == d) Open(x, y, x, y + 1);
            }
            for (int k = 0; k + 1 < rings.Count; k++)
            {
                int r = rings[k], lo = r + 1, hi = n - 2 - r, yb1 = r + 1, yb2 = r + P - 1, yt1 = n - r - P, yt2 = n - r - 2;
                int lyLo = r + P, lyHi = n - 1 - r - P;
                StationStrip(rnd, true, lo, hi, yb1, yb2, 3, r + P, n - 1 - r - P, true);
                StationStrip(rnd, true, lo, hi, yt1, yt2, 2, r + P, n - 1 - r - P, false);
                StationStrip(rnd, false, lyLo, lyHi, r + 1, r + P - 1, 1, lyLo, lyHi, true);
                StationStrip(rnd, false, lyLo, lyHi, n - r - P, n - r - 2, 0, lyLo, lyHi, false);
            }
            int aL = rings[rings.Count - 1], c0 = aL + 1, cs = n - 2 * c0, c1 = n - 1 - c0;
            Room(c0, c0, cs, cs, false);
            foreach (int t in new[] { n / 2 - 3, n / 2 + 2 }) { Link(t, c0, 3); Link(t, c1, 2); Link(c0, t, 1); Link(c1, t, 0); }
            int[] bxs = { c0 + 2, c1 - 4 };
            foreach (int bx in bxs) foreach (int by in bxs)
            {
                WallBox(bx, by, 3, 3); MarkRoom(bx, by, 3, 3);
                if (bx < n / 2) Link(bx + 2, by + 1, 0); else Link(bx, by + 1, 1);
            }
        }
        void StationStrip(System.Random rnd, bool horiz, int s0, int s1, int t0, int t1, int outerD, int innerLo, int innerHi, bool low)
        {
            // horiz: цепочка камер вдоль x, толщина по y [t0,t1]; иначе — вдоль y, толщина по x.
            var ch = Chunks(rnd, s0, s1, 7, 11);
            for (int i = 0; i < ch.Count; i++)
            {
                int c0 = ch[i][0], c1 = ch[i][1]; int len = c1 - c0 + 1, th = t1 - t0 + 1;
                if (horiz) Room(c0, t0, len, th, true); else Room(t0, c0, th, len, true);
                int outerPos = low ? t0 : t1, innerPos = low ? t1 : t0, innerD = low ? (horiz ? 2 : 0) : (horiz ? 3 : 1);
                int doors = len >= 9 ? 2 : 1;
                for (int q = 0; q < doors; q++)
                {
                    int s = c0 + 1 + rnd.Next(Mathf.Max(1, len - 2));
                    if (horiz) Link(s, outerPos, outerD); else Link(outerPos, s, outerD);
                }
                int lo = Mathf.Max(c0, innerLo), hi = Mathf.Min(c1, innerHi);
                if (hi >= lo) { int s = lo + rnd.Next(hi - lo + 1); if (horiz) Link(s, innerPos, innerD); else Link(innerPos, s, innerD); }
                if (i + 1 < ch.Count && rnd.NextDouble() < 0.6) { int m = t0 + rnd.Next(th); if (horiz) Link(c1, m, 0); else Link(m, c1, 2); }
            }
        }

        // ================= L4: офис =================
        public void GenOffice(System.Random rnd)
        {
            int R = 4; if (n < 30) { Room(1, 1, n - 2, n - 2, false); return; }
            foreach (int side in new[] { 0, 1 })
            {
                int x0 = side == 0 ? 0 : n - R;
                var ch = new List<int[]>(); ch.Add(new[] { 0, R - 1 }); ch.AddRange(Chunks(rnd, R, n - 1 - R, 3, 5)); ch.Add(new[] { n - R, n - 1 });
                foreach (var c in ch) { Room(x0, c[0], R, c[1] - c[0] + 1, true); int ym = (c[0] + c[1]) / 2; if (side == 0) Link(R - 1, ym, 0); else Link(n - R, ym, 1); }
                int y0 = side == 0 ? 0 : n - R;
                foreach (var c in Chunks(rnd, R, n - 1 - R, 3, 5)) { Room(c[0], y0, c[1] - c[0] + 1, R, true); int xm = (c[0] + c[1]) / 2; if (side == 0) Link(xm, R - 1, 2); else Link(xm, n - R, 3); }
            }
            int m = n - 2 * R; Room(R, R, m, m, false);
            int cx0 = n / 2 - 5, cy0 = n / 2 - 5;
            // перегородки-«кубиклы» рядами со сдвигом
            for (int row = 0, yy = R + 3; yy < n - R - 2; yy += 4, row++)
                for (int x = R + 2; x < n - R - 2; x++)
                    if ((x + row * 2) % 6 < 4 && !(x >= cx0 - 2 && x <= cx0 + 11 && yy >= cy0 - 2 && yy <= cy0 + 12)) SetH(x, yy, true);
            // переговорные с дверью
            var occ = new bool[n, n]; int tries = 0, made = 0;
            while (made < Mathf.Max(3, n / 9) && tries++ < 200)
            {
                int w = 4 + rnd.Next(3), h = 3 + rnd.Next(3), x0 = R + 2 + rnd.Next(Mathf.Max(1, n - 2 * R - w - 4)), y0 = R + 2 + rnd.Next(Mathf.Max(1, n - 2 * R - h - 4)); bool bad = false;
                if (x0 + w > cx0 - 2 && x0 < cx0 + 12 && y0 + h > cy0 - 2 && y0 < cy0 + 12) bad = true;
                for (int x = x0 - 1; x <= x0 + w && !bad; x++) for (int y = y0 - 1; y <= y0 + h; y++) if (x >= 0 && y >= 0 && x < n && y < n && occ[x, y]) { bad = true; break; }
                if (bad) continue;
                for (int x = x0; x < x0 + w; x++) for (int y = y0; y < y0 + h; y++) occ[x, y] = true;
                Room(x0, y0, w, h, true); WallBox(x0, y0, w, h);
                int s = rnd.Next(4); if (s == 0) Link(x0 + w - 1, y0 + h / 2, 0); else if (s == 1) Link(x0, y0 + h / 2, 1); else if (s == 2) Link(x0 + w / 2, y0 + h - 1, 2); else Link(x0 + w / 2, y0, 3);
                made++;
            }
            // ядро: холл лифтов 10×10 с четырьмя кабинами
            Room(cx0, cy0, 10, 10, false); WallBox(cx0, cy0, 10, 10);
            for (int k = -1; k <= 0; k++) { Link(n / 2 + k, cy0, 3); Link(n / 2 + k, cy0 + 9, 2); Link(cx0, n / 2 + k, 1); Link(cx0 + 9, n / 2 + k, 0); }
            int[] bx = { cx0 + 1, cx0 + 7 }, by = { cy0 + 1, cy0 + 7 };
            foreach (int x in bx) foreach (int y in by) { WallBox(x, y, 3, 3); MarkRoom(x, y, 3, 3); if (y < n / 2) Link(x + 1, y + 2, 2); else Link(x + 1, y, 3); }
        }

        // ================= L5: отель — гребёнка коридоров с номерами =================
        public void GenHotel(System.Random rnd)
        {
            if (n < 24) { Room(1, 1, n - 2, n - 2, false); return; }
            int K = (n - 9) / 9 + 1; var ycs = new List<int>(); for (int k = 0; k < K; k++) ycs.Add(4 + 9 * k);
            int top = ycs[ycs.Count - 1] + 4; if (top < n - 1) MarkSolid(0, top + 1, n, n - 1 - top);
            int ycL = ycs[0]; foreach (int y in ycs) if (Mathf.Abs(y - n / 2) < Mathf.Abs(ycL - n / 2)) ycL = y;
            sx = n / 2; sy = ycL; int lx0 = n / 2 - 5, lx1 = n / 2 + 5;
            var xv = new List<int> { 1, n - 2, n / 4, 3 * n / 4 };
            foreach (int yc in ycs) for (int x = 1; x < n - 2; x++) Link(x, yc, 0);
            foreach (int xc in xv) for (int y = 0; y < top; y++) Link(xc, y, 2);
            foreach (int yc in ycs) foreach (int sgn in new[] { -1, 1 })
            {
                int y0 = sgn < 0 ? yc - 4 : yc + 1; int x = 2;
                while (x < n - 2)
                {
                    bool blocked = xv.Contains(x) || (yc == ycL && x >= lx0 && x <= lx1);
                    if (blocked) { x++; continue; }
                    int e = x; while (e + 1 < n - 2 && !xv.Contains(e + 1) && !(yc == ycL && e + 1 >= lx0 && e + 1 <= lx1)) e++;
                    foreach (var c in Chunks(rnd, x, e, 2, 4))
                    {
                        int w = c[1] - c[0] + 1; if (w < 2) continue; Room(c[0], y0, w, 4, true); int xm = (c[0] + c[1]) / 2;
                        if (sgn < 0) Link(xm, yc - 1, 2); else Link(xm, yc + 1, 3);
                    }
                    x = e + 1;
                }
            }
            Room(lx0, ycL - 4, lx1 - lx0 + 1, 9, false);
        }

        // ================= L37: бассейны — анфилада залов =================
        public void GenPools(System.Random rnd)
        {
            var xs = Chunks(rnd, 0, n - 1, 6, 10); var ys = Chunks(rnd, 0, n - 1, 6, 10);
            for (int i = 0; i < xs.Count; i++) for (int j = 0; j < ys.Count; j++) Room(xs[i][0], ys[j][0], xs[i][1] - xs[i][0] + 1, ys[j][1] - ys[j][0] + 1, false);
            for (int k = 0; k < xs.Count * ys.Count / 7; k++)   // несколько слитых больших залов
            {
                int i = rnd.Next(Mathf.Max(1, xs.Count - 1)), j = rnd.Next(Mathf.Max(1, ys.Count - 1)); if (i + 1 >= xs.Count || j + 1 >= ys.Count) continue;
                Room(xs[i][0], ys[j][0], xs[i + 1][1] - xs[i][0] + 1, ys[j + 1][1] - ys[j][0] + 1, false);
            }
            for (int i = 0; i + 1 < xs.Count; i++) for (int j = 0; j < ys.Count; j++)
            {
                int bx = xs[i][1] + 1, y0 = ys[j][0], y1 = ys[j][1]; if (rnd.NextDouble() > 0.75) continue;
                int ym = y0 + 1 + rnd.Next(Mathf.Max(1, y1 - y0 - 1)); for (int dy = 0; dy <= 1; dy++) if (ym + dy >= y0 && ym + dy <= y1) SetV(bx, ym + dy, false);
            }
            for (int j = 0; j + 1 < ys.Count; j++) for (int i = 0; i < xs.Count; i++)
            {
                int by = ys[j][1] + 1, x0 = xs[i][0], x1 = xs[i][1]; if (rnd.NextDouble() > 0.75) continue;
                int xm = x0 + 1 + rnd.Next(Mathf.Max(1, x1 - x0 - 1)); for (int dx = 0; dx <= 1; dx++) if (xm + dx >= x0 && xm + dx <= x1) SetH(xm + dx, by, false);
            }
        }

        // ================= L6: Lights Out — лабиринт с коридорами в 2 клетки =================
        public void GenWide(System.Random rnd)
        {
            int m = n / 2; if (n % 2 == 1) { MarkSolid(n - 1, 0, 1, n); MarkSolid(0, n - 1, n, 1); }
            for (int i = 0; i < m; i++) for (int j = 0; j < m; j++) Room(2 * i, 2 * j, 2, 2, false);
            var seen = new bool[m, m]; var st = new Stack<Vector2Int>(); var s0 = new Vector2Int(Mathf.Clamp(sx / 2, 0, m - 1), Mathf.Clamp(sy / 2, 0, m - 1)); st.Push(s0); seen[s0.x, s0.y] = true;
            while (st.Count > 0)
            {
                var c = st.Peek(); var opts = new List<int>();
                for (int d = 0; d < 4; d++) { int nx = c.x + DX[d], ny = c.y + DY[d]; if (nx >= 0 && ny >= 0 && nx < m && ny < m && !seen[nx, ny]) opts.Add(d); }
                if (opts.Count == 0) { st.Pop(); continue; }
                int dd = opts[rnd.Next(opts.Count)]; WideEdge(c.x, c.y, dd); var nn = new Vector2Int(c.x + DX[dd], c.y + DY[dd]); seen[nn.x, nn.y] = true; st.Push(nn);
            }
            for (int k = 0; k < m * m / 8; k++) { int i = rnd.Next(m), j = rnd.Next(m), d = rnd.Next(4); int nx = i + DX[d], ny = j + DY[d]; if (nx >= 0 && ny >= 0 && nx < m && ny < m) WideEdge(i, j, d); }
            for (int k = 0; k < m * m / 40; k++) { int i = rnd.Next(Mathf.Max(1, m - 2)), j = rnd.Next(Mathf.Max(1, m - 2)); if (i + 3 <= m && j + 3 <= m) Room(2 * i, 2 * j, 6, 6, true); }
        }
        void WideEdge(int i, int j, int d)
        {
            if (d == 0) { Link(2 * i + 1, 2 * j, 0); Link(2 * i + 1, 2 * j + 1, 0); }
            else if (d == 1) { Link(2 * i, 2 * j, 1); Link(2 * i, 2 * j + 1, 1); }
            else if (d == 2) { Link(2 * i, 2 * j + 1, 2); Link(2 * i + 1, 2 * j + 1, 2); }
            else { Link(2 * i, 2 * j, 3); Link(2 * i + 1, 2 * j, 3); }
        }

        // ================= L1: гараж — парковочные ряды + служебные блоки =================
        public void GenParking(System.Random rnd)
        {
            OpenAllInner(); var occ = new bool[n, n];
            int cx = n / 2, cy = n / 2;
            for (int x = cx - 8; x <= cx + 8; x++) for (int y = cy - 8; y <= cy + 8; y++) if (x >= 0 && y >= 0 && x < n && y < n) occ[x, y] = true;   // площадь у спавна
            int blocks = Mathf.Max(3, n / 12), tries = 0, made = 0;
            while (made < blocks && tries++ < 300)
            {
                bool wide = rnd.Next(2) == 0; int w = wide ? 9 : 7, h = wide ? 7 : 9; if (n < w + 6 || n < h + 6) break;
                int x0 = 2 + rnd.Next(n - w - 4), y0 = 2 + rnd.Next(n - h - 4); bool bad = false;
                for (int x = x0 - 2; x < x0 + w + 2 && !bad; x++) for (int y = y0 - 2; y < y0 + h + 2; y++) if (x >= 0 && y >= 0 && x < n && y < n && occ[x, y]) { bad = true; break; }
                if (bad) continue;
                for (int x = x0; x < x0 + w; x++) for (int y = y0; y < y0 + h; y++) occ[x, y] = true;
                ServiceBlock(x0, y0, w, h, wide); made++;
            }
            // ряды парковочных мест: перегородки через клетку на 2 ряда глубиной
            for (int y = 3; y + 1 < n - 2; y += 7)
            {
                                for (int x = 2; x < n - 2; x++)
                {
                    if (occ[x, y] || occ[x, y + 1]) continue;
                    if (x % 2 == 0) { SetV(x, y, true); SetV(x, y + 1, true); }
                }
            }
            // две-три бетонные перегородки с широкими проёмами (пандусы)
            for (int i = 0; i < n / 20; i++)
            {
                bool horiz = rnd.Next(2) == 0; int a = 5 + rnd.Next(Mathf.Max(1, n - 10)), len = 12 + rnd.Next(14), b = rnd.Next(2, Mathf.Max(3, n - len - 2)), gap = b + 3 + rnd.Next(Mathf.Max(1, len - 8));
                if (Mathf.Abs(a - n / 2) < 10) continue;
                for (int k = b; k < b + len && k < n - 1; k++) { if (k >= gap && k < gap + 4) continue; if (horiz) { if (!occ[k, a] && !occ[k, Mathf.Max(0, a - 1)]) SetH(k, a, true); } else { if (!occ[a, k] && !occ[Mathf.Max(0, a - 1), k]) SetV(a, k, true); } }
            }
        }
        void ServiceBlock(int x0, int y0, int w, int h, bool wide)
        {
            LinkRect(x0, y0, w, h); WallBox(x0, y0, w, h);
            if (wide)
            {   // коридор вдоль x посередине, по три комнаты сверху и снизу
                int ym = y0 + 3; for (int x = x0; x < x0 + w; x++) { SetH(x, ym, true); SetH(x, ym + 1, true); }
                for (int y = y0; y < ym; y++) { SetV(x0 + 3, y, true); SetV(x0 + 6, y, true); }
                for (int y = ym + 1; y < y0 + h; y++) { SetV(x0 + 3, y, true); SetV(x0 + 6, y, true); }
                for (int i = 0; i < 3; i++) { Link(x0 + i * 3 + 1, ym - 1, 2); Link(x0 + i * 3 + 1, ym + 1, 3); MarkRoom(x0 + i * 3, y0, 3, 3); MarkRoom(x0 + i * 3, ym + 1, 3, 3); }
                SetV(x0, ym, false); SetV(x0 + w, ym, false);
            }
            else
            {
                int xm = x0 + 3; for (int y = y0; y < y0 + h; y++) { SetV(xm, y, true); SetV(xm + 1, y, true); }
                for (int x = x0; x < xm; x++) { SetH(x, y0 + 3, true); SetH(x, y0 + 6, true); }
                for (int x = xm + 1; x < x0 + w; x++) { SetH(x, y0 + 3, true); SetH(x, y0 + 6, true); }
                for (int i = 0; i < 3; i++) { Link(xm - 1, y0 + i * 3 + 1, 0); Link(xm + 1, y0 + i * 3 + 1, 1); MarkRoom(x0, y0 + i * 3, 3, 3); MarkRoom(xm + 1, y0 + i * 3, 3, 3); }
                SetH(xm, y0, false); SetH(xm, y0 + h, false);
            }
        }

        // ================= L9: пригород — ряды домов вдоль улиц =================
        public void GenSuburb(System.Random rnd)
        {
            OpenAllInner();
            for (int by = 3; by + 5 < n - 2; by += 16) for (int row = 0; row < 2; row++)
            {
                int y0 = by + row * 9, depth = 4 + rnd.Next(2); if (y0 + depth > n - 2) continue;
                int x = 2 + rnd.Next(4);
                while (x < n - 9)
                {
                    int w = 5 + rnd.Next(3), gap = 3 + rnd.Next(3);
                    bool nearSpawn = Mathf.Abs(x + w / 2 - n / 2) < w / 2 + 7 && Mathf.Abs(y0 + depth / 2 - n / 2) < depth / 2 + 7;
                    if (!nearSpawn)
                    {
                        MarkSolid(x, y0, w, depth); WallBox(x, y0, w, depth);
                        if (gap >= 4 && rnd.Next(2) == 0) { MarkSolid(x + w, y0, 2, 2); WallBox(x + w, y0, 2, 2); }
                        if (rnd.NextDouble() < 0.6)
                        {   // забор между участками (спереди и сзади)
                            int xd = x + w + gap / 2 + 1;
                            for (int k = 0; k < 3; k++) { SetV(xd, row == 0 ? y0 + depth + k : y0 - 1 - k, true); SetV(xd, row == 0 ? y0 - 1 - Mathf.Min(k, 1) : y0 + depth + Mathf.Min(k, 1), true); }
                        }
                    }
                    x += w + gap;
                }
            }
        }

        // ================= L10: пшеничное поле — тропы + ферма =================
        public void GenTrails(System.Random rnd)
        {
            var used = new bool[n, n]; var carved = new List<Vector2Int>();
            Action<int, int> stamp = (px, py) => { px = Mathf.Clamp(px, 1, n - 3); py = Mathf.Clamp(py, 1, n - 3); LinkRect(px, py, 2, 2); used[px, py] = used[px + 1, py] = used[px, py + 1] = used[px + 1, py + 1] = true; carved.Add(new Vector2Int(px, py)); };
            Action<int, int, int, int> clear = (x0, y0, w, h) => { x0 = Mathf.Clamp(x0, 1, n - 2 - w); y0 = Mathf.Clamp(y0, 1, n - 2 - h); LinkRect(x0, y0, w, h); for (int x = x0; x < x0 + w; x++) for (int y = y0; y < y0 + h; y++) used[x, y] = true; carved.Add(new Vector2Int(x0 + w / 2, y0 + h / 2)); };
            clear(n / 2 - 3, n / 2 - 3, 7, 7);
            int walkers = n / 3 + 8;
            for (int wk = 0; wk < walkers; wk++)
            {
                var s = carved[wk < 4 ? 0 : rnd.Next(carved.Count)]; int x = s.x, y = s.y, d = rnd.Next(4), len = 14 + rnd.Next(30);
                for (int i = 0; i < len; i++)
                {
                    stamp(x, y); if (rnd.NextDouble() < 0.1) d = (d < 2) ? 2 + rnd.Next(2) : rnd.Next(2);
                    int nx = x + DX[d], ny = y + DY[d]; if (nx < 1 || ny < 1 || nx > n - 4 || ny > n - 4) { d = rnd.Next(4); continue; } x = nx; y = ny;
                }
                if (rnd.NextDouble() < 0.35) clear(x - 2, y - 2, 6, 6);
            }
            // две фермы: двор, амбар, дом, силос и дорога к спавну
            for (int f = 0; f < 2 && n >= 40; f++)
            {
                int fx = 0, fy = 0, tries = 0; do { fx = 3 + rnd.Next(n - 22); fy = 3 + rnd.Next(n - 18); } while (tries++ < 50 && Mathf.Abs(fx + 7 - n / 2) + Mathf.Abs(fy + 5 - n / 2) < n / 3);
                clear(fx, fy, 15, 11);
                MarkSolid(fx + 2, fy + 2, 6, 4); WallBox(fx + 2, fy + 2, 6, 4);
                MarkSolid(fx + 10, fy + 6, 3, 3); WallBox(fx + 10, fy + 6, 3, 3);
                MarkSolid(fx + 10, fy + 1, 2, 2); WallBox(fx + 10, fy + 1, 2, 2);
                var tgt = carved[rnd.Next(Mathf.Max(1, carved.Count / 2))];
                int x = fx + 7, y = fy, ex = tgt.x, ey = tgt.y;
                while (x != ex) { int dd = ex > x ? 0 : 1; stamp(x, y); x += DX[dd]; if (x < 1 || x > n - 4) break; }
                while (y != ey) { int dd = ey > y ? 2 : 3; stamp(x, y); y += DY[dd]; if (y < 1 || y > n - 4) break; }
            }
            SolidUnused(used);
        }

        // ================= L7: острова на воде и мостки =================
        public void GenIslands(System.Random rnd)
        {
            var rects = new List<int[]>(); rects.Add(new[] { n / 2 - 4, n / 2 - 4, 9, 9 });
            int target = Mathf.Max(5, n * n / 330), tries = 0;
            while (rects.Count < target && tries++ < 800)
            {
                int w = 5 + rnd.Next(6), h = 5 + rnd.Next(6), x0 = 2 + rnd.Next(Mathf.Max(1, n - w - 4)), y0 = 2 + rnd.Next(Mathf.Max(1, n - h - 4)); bool bad = false;
                foreach (var r in rects) if (x0 < r[0] + r[2] + 4 && x0 + w + 4 > r[0] && y0 < r[1] + r[3] + 4 && y0 + h + 4 > r[1]) { bad = true; break; }
                if (!bad) rects.Add(new[] { x0, y0, w, h });
            }
            var used = new bool[n, n];
            foreach (var r in rects) { Room(r[0], r[1], r[2], r[3], false); for (int x = r[0]; x < r[0] + r[2]; x++) for (int y = r[1]; y < r[1] + r[3]; y++) used[x, y] = true; }
            int cnt = rects.Count; var inTree = new bool[cnt]; inTree[0] = true; var pairs = new HashSet<long>();
            for (int it = 1; it < cnt; it++)
            {
                int ba = -1, bb = -1, bd = int.MaxValue;
                for (int a = 0; a < cnt; a++) if (inTree[a]) for (int b = 0; b < cnt; b++) if (!inTree[b])
                {
                    int dd = Mathf.Abs(rects[a][0] + rects[a][2] / 2 - rects[b][0] - rects[b][2] / 2) + Mathf.Abs(rects[a][1] + rects[a][3] / 2 - rects[b][1] - rects[b][3] / 2);
                    if (dd < bd) { bd = dd; ba = a; bb = b; }
                }
                inTree[bb] = true; pairs.Add((long)ba * 1000 + bb);
            }
            for (int k = 0; k < cnt / 3; k++) { int a = rnd.Next(cnt), b = rnd.Next(cnt); if (a != b) pairs.Add((long)a * 1000 + b); }
            foreach (long p in pairs)
            {
                var A = rects[(int)(p / 1000)]; var B = rects[(int)(p % 1000)];
                int ax = A[0] + 1 + rnd.Next(Mathf.Max(1, A[2] - 2)), ay = A[1] + 1 + rnd.Next(Mathf.Max(1, A[3] - 2)), bx = B[0] + 1 + rnd.Next(Mathf.Max(1, B[2] - 2)), by = B[1] + 1 + rnd.Next(Mathf.Max(1, B[3] - 2));
                Line(ax, ay, bx, by, used);
            }
            SolidUnused(used);
        }
    }
}
