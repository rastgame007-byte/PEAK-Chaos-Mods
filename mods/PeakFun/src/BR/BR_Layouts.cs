// Планировки уровней Backrooms (1.24): каждый уровень строится по своему «архитектурному» принципу, а не лабиринтом как Уровень 0.
// Гараж с колоннами, стеллажные ряды склада, решётка отельных коридоров, квартиры с дверями вдоль коридора,
// школьные классы, пещеры/поле (клеточный автомат), улицы с кварталами (пригород, город, двор окон).
// «Сплошные» клетки (solid) недостижимы и не строятся — они превращаются в фасады зданий и толщу породы.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal sealed partial class Grid
    {
        public bool[,] solid; public byte[,] kind;   // kind: 1 — комната, 0 — коридор/улица
        public int Kind(int x, int y) { return kind == null || x < 0 || y < 0 || x >= n || y >= n ? 0 : kind[x, y]; }
        void MarkRoom(int x0, int y0, int w, int h) { if (kind == null) kind = new byte[n, n]; for (int x = x0; x < x0 + w && x < n; x++) for (int y = y0; y < y0 + h && y < n; y++) kind[x, y] = 1; }

        void Link(int x, int y, int d)
        {
            int nx = x + DX[d], ny = y + DY[d]; if (nx < 0 || ny < 0 || nx >= n || ny >= n) return; Open(x, y, nx, ny);
        }
        void LinkRect(int x0, int y0, int w, int h)   // убрать все внутренние стены прямоугольника
        {
            for (int x = x0; x < x0 + w && x < n; x++) for (int y = y0; y < y0 + h && y < n; y++) { if (x > x0 && x < n) wv[x, y] = false; if (y > y0 && y < n) wh[x, y] = false; }
        }
        void MarkSolid(int x0, int y0, int w, int h)
        {
            if (solid == null) solid = new bool[n, n];
            for (int x = x0; x < x0 + w && x < n; x++) for (int y = y0; y < y0 + h && y < n; y++) if (x >= 0 && y >= 0) solid[x, y] = true;
        }

        // ---------- решётка коридоров: отель (5), квартиры (13), школа (52) ----------
        // px/py — период линий коридоров, rw/rh — размер комнаты в клетках, roomP — вероятность, что комната есть (иначе глухая толща),
        // cut — доля убранных звеньев коридоров (тупики и обходы)
        public void GenLattice(System.Random rnd, int px, int py, int rw, int rh, double roomP, double cut)
        {
            int ox = 2, oy = 2;
            Func<int, bool> colC = x => x >= ox && (x - ox) % px == 0, rowC = y => y >= oy && (y - oy) % py == 0;
            Func<int, int, bool> isCor = (x, y) => colC(x) || rowC(y);
            // коридоры
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++)
                {
                    if (!isCor(x, y)) continue;
                    if (rowC(y) && x + 1 < n && rnd.NextDouble() >= cut) Link(x, y, 0);
                    if (colC(x) && y + 1 < n && rnd.NextDouble() >= cut) Link(x, y, 2);
                }
            // блоки между коридорами
            for (int bx = ox + 1; bx < n; bx += px)
                for (int by = oy + 1; by < n; by += py)
                {
                    int bw = Mathf.Min(px - 1, n - bx), bh = Mathf.Min(py - 1, n - by); if (bw <= 0 || bh <= 0) continue;
                    for (int rx = bx; rx < bx + bw; rx += rw)
                        for (int ry = by; ry < by + bh; ry += rh)
                        {
                            int w = Mathf.Min(rw, bx + bw - rx), h = Mathf.Min(rh, by + bh - ry);
                            if (rnd.NextDouble() > roomP) { MarkSolid(rx, ry, w, h); continue; }
                            LinkRect(rx, ry, w, h); MarkRoom(rx, ry, w, h);
                            // двери в коридор — с тех сторон, где комната касается коридора
                            var doors = new List<int[]>();
                            if (rx == bx) for (int y = ry; y < ry + h; y++) doors.Add(new[] { rx, y, 1 });
                            if (rx + w == bx + bw) for (int y = ry; y < ry + h; y++) doors.Add(new[] { rx + w - 1, y, 0 });
                            if (ry == by) for (int x = rx; x < rx + w; x++) doors.Add(new[] { x, ry, 3 });
                            if (ry + h == by + bh) for (int x = rx; x < rx + w; x++) doors.Add(new[] { x, ry + h - 1, 2 });
                            if (doors.Count == 0) { MarkSolid(rx, ry, w, h); continue; }
                            var dd = doors[rnd.Next(doors.Count)]; Link(dd[0], dd[1], dd[2]);
                            if (doors.Count > 3 && rnd.NextDouble() < 0.2) { var d2 = doors[rnd.Next(doors.Count)]; Link(d2[0], d2[1], d2[2]); }
                        }
                }
            // спавн — на перекрёстке
            int cx = ox + Mathf.Clamp(Mathf.RoundToInt((n / 2f - ox) / px), 0, (n - 2 - ox) / px) * px, cy = oy + Mathf.Clamp(Mathf.RoundToInt((n / 2f - oy) / py), 0, (n - 2 - oy) / py) * py;
            sx = Mathf.Clamp(cx, 1, n - 2); sy = Mathf.Clamp(cy, 1, n - 2);
        }

        // ---------- улицы и кварталы: пригород (9), город (11), двор окон (188) ----------
        public void GenBlocks(System.Random rnd, int P, int sw, double parkP, double alleyP)
        {
            Func<int, bool> st = v => v % P < sw;
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++)
                {
                    bool s = st(x) || st(y); if (!s) continue;
                    if (x + 1 < n && (st(x + 1) || st(y))) Link(x, y, 0);
                    if (y + 1 < n && (st(y + 1) || st(x))) Link(x, y, 2);
                }
            int B = P - sw;
            for (int bx = sw; bx < n; bx += P)
                for (int by = sw; by < n; by += P)
                {
                    int w = Mathf.Min(B, n - bx), h = Mathf.Min(B, n - by); if (w <= 0 || h <= 0) continue;
                    double q = rnd.NextDouble();
                    if (q < parkP)
                    {   // сквер/площадь: открытая площадка, примыкающая к улицам по всему периметру
                        LinkRect(bx, by, w, h);
                        for (int x = bx; x < bx + w; x++) { if (by > 0) Link(x, by, 3); if (by + h - 1 < n - 1) Link(x, by + h - 1, 2); }
                        for (int y = by; y < by + h; y++) { if (bx > 0) Link(bx, y, 1); if (bx + w - 1 < n - 1) Link(bx + w - 1, y, 0); }
                        continue;
                    }
                    if (w >= 5 && h >= 5 && rnd.NextDouble() < alleyP)
                    {   // переулок между двумя зданиями
                        bool horiz = rnd.Next(2) == 0;
                        if (horiz) { int ay = by + h / 2; for (int x = bx; x < bx + w - 1; x++) Link(x, ay, 0); Link(bx, ay, 1); if (rnd.NextDouble() < 0.7) Link(bx + w - 1, ay, 0); MarkSolid(bx, by, w, h / 2); MarkSolid(bx, ay + 1, w, by + h - ay - 1); }
                        else { int ax = bx + w / 2; for (int y = by; y < by + h - 1; y++) Link(ax, y, 2); Link(ax, by, 3); if (rnd.NextDouble() < 0.7) Link(ax, by + h - 1, 2); MarkSolid(bx, by, w / 2, h); MarkSolid(ax + 1, by, bx + w - ax - 1, h); }
                        continue;
                    }
                    MarkSolid(bx, by, w, h);
                }
            // спавн — перекрёсток улиц ближе к центру
            int k = Mathf.Max(0, Mathf.RoundToInt((n / 2f) / P)); sx = Mathf.Clamp(k * P, 0, n - 1); sy = sx;
        }


        // ---------- коммуникационные тоннели (Уровень 2): длинные прямые магистрали, пересечения, насосные залы, тупиковые отводы ----------
        public void GenTunnels(System.Random rnd)
        {
            int ph = 6 + rnd.Next(2), pv = 7 + rnd.Next(2), ox = 2 + rnd.Next(3), oy = 2 + rnd.Next(3); var used = new bool[n, n];
            var xs = new List<int>(); for (int x = ox; x < n - 1; x += pv) xs.Add(x); var ys = new List<int>(); for (int y = oy; y < n - 1; y += ph) ys.Add(y);
            for (int j = 0; j < ys.Count; j++) for (int i = 0; i + 1 < xs.Count + 1; i++)
                {
                    int xa = i == 0 ? 0 : xs[i - 1], xb = i < xs.Count ? xs[i] : n - 1; if (i == 0) xa = 0; if (rnd.NextDouble() > 0.9 && j > 0 && j < ys.Count - 1) continue;
                    for (int x = xa; x <= xb; x++) { used[x, ys[j]] = true; if (x < xb) Link(x, ys[j], 0); }
                }
            for (int i = 0; i < xs.Count; i++) for (int j = 0; j <= ys.Count; j++)
                {
                    int ya = j == 0 ? 0 : ys[j - 1], yb = j < ys.Count ? ys[j] : n - 1; if (rnd.NextDouble() > 0.88 && i > 0 && i < xs.Count - 1) continue;
                    for (int y = ya; y <= yb; y++) { used[xs[i], y] = true; if (y < yb) Link(xs[i], y, 2); }
                }
            // насосные залы 3×3 на части пересечений
            for (int i = 0; i < xs.Count; i++) for (int j = 0; j < ys.Count; j++) if (rnd.NextDouble() < 0.35 && xs[i] > 1 && ys[j] > 1 && xs[i] < n - 2 && ys[j] < n - 2)
                    { LinkRect(xs[i] - 1, ys[j] - 1, 3, 3); MarkRoom(xs[i] - 1, ys[j] - 1, 3, 3); for (int x = xs[i] - 1; x <= xs[i] + 1; x++) for (int y = ys[j] - 1; y <= ys[j] + 1; y++) used[x, y] = true; Link(xs[i] - 1, ys[j], 1); Link(xs[i] + 1, ys[j], 0); Link(xs[i], ys[j] - 1, 3); Link(xs[i], ys[j] + 1, 2); }
            // тупиковые отводы
            for (int k = 0; k < n / 2; k++)
            {
                int x = rnd.Next(1, n - 1), y = rnd.Next(1, n - 1); if (!used[x, y]) continue; int d = rnd.Next(4), len = 2 + rnd.Next(3); int cx = x, cy = y; bool ok = true; var path = new List<Vector2Int>();
                for (int s2 = 0; s2 < len; s2++) { int nx = cx + DX[d], ny = cy + DY[d]; if (nx < 1 || ny < 1 || nx >= n - 1 || ny >= n - 1 || used[nx, ny]) { ok = false; break; } path.Add(new Vector2Int(nx, ny)); cx = nx; cy = ny; }
                if (!ok || path.Count < 2) continue; int px = x, py = y; foreach (var c in path) { Open(px, py, c.x, c.y); used[c.x, c.y] = true; px = c.x; py = c.y; }
            }
            // спавн — насосный зал у центра
            int bsx = xs[xs.Count / 2], bsy = ys[ys.Count / 2]; bsx = Mathf.Clamp(bsx, 2, n - 3); bsy = Mathf.Clamp(bsy, 2, n - 3); sx = bsx; sy = bsy; LinkRect(sx - 1, sy - 1, 3, 3); MarkRoom(sx - 1, sy - 1, 3, 3);
            for (int x = sx - 1; x <= sx + 1; x++) for (int y = sy - 1; y <= sy + 1; y++) used[x, y] = true;
            solid = new bool[n, n]; for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) solid[x, y] = !used[x, y];
        }

        // ---------- вентиляция (Уровень 96): одна длинная магистральная шахта с короткими тупиковыми отводами ----------
        public void GenDuct(System.Random rnd)
        {
            var used = new bool[n, n]; int x = 1, y = n / 2; sx = 1; sy = y; var spine = new List<Vector2Int> { new Vector2Int(x, y) }; used[x, y] = true;
            while (x < n - 3)
            {
                int run = 2 + rnd.Next(4); for (int k = 0; k < run && x < n - 3; k++) { Open(x, y, x + 1, y); x++; used[x, y] = true; spine.Add(new Vector2Int(x, y)); }
                int dy = (rnd.Next(2) == 0 ? 1 : -1) * (1 + rnd.Next(3)); int step = dy > 0 ? 1 : -1;
                for (int k = 0; k < Mathf.Abs(dy); k++) { int ny = y + step; if (ny < 2 || ny > n - 3) break; Open(x, y, x, ny); y = ny; used[x, y] = true; spine.Add(new Vector2Int(x, y)); }
            }
            for (int i = 2; i < spine.Count - 2; i++)
            {
                if (i % 3 != 0 || rnd.NextDouble() > 0.55) continue; var c = spine[i]; int d = rnd.Next(2) == 0 ? 2 : 3; int len = 2 + rnd.Next(4); int px = c.x, py = c.y;
                for (int s2 = 0; s2 < len; s2++) { int nx = px + DX[d], ny = py + DY[d]; if (nx < 1 || ny < 1 || nx >= n - 1 || ny >= n - 1 || used[nx, ny]) break; Open(px, py, nx, ny); used[nx, ny] = true; px = nx; py = ny; if (s2 == 1 && rnd.NextDouble() < 0.4) { int d2 = rnd.Next(2) == 0 ? 0 : 1; int qx = px + DX[d2], qy = py + DY[d2]; if (qx > 0 && qx < n - 1 && !used[qx, qy]) { Open(px, py, qx, qy); used[qx, qy] = true; } } }
            }
            LinkRect(sx - 1, sy - 1, 3, 3); for (int xx = sx - 1; xx <= sx + 1; xx++) for (int yy = sy - 1; yy <= sy + 1; yy++) if (xx >= 0 && yy >= 0 && xx < n && yy < n) used[xx, yy] = true;
            solid = new bool[n, n]; for (int xx = 0; xx < n; xx++) for (int yy = 0; yy < n; yy++) solid[xx, yy] = !used[xx, yy];
        }

        // ---------- клеточный автомат: пещеры, поле, чердак ----------
        public bool GenCave(System.Random rnd, double fill, int iters, int minOpenPct)
        {
            bool[,] open = null; int total = 0;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                open = new bool[n, n];
                for (int x = 1; x < n - 1; x++) for (int y = 1; y < n - 1; y++) open[x, y] = rnd.NextDouble() >= fill;
                for (int it = 0; it < iters; it++)
                {
                    var nx = new bool[n, n];
                    for (int x = 1; x < n - 1; x++) for (int y = 1; y < n - 1; y++)
                        {
                            int walls = 0; for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) { if (dx == 0 && dy == 0) continue; if (!open[x + dx, y + dy]) walls++; }
                            nx[x, y] = open[x, y] ? walls < 5 : walls < 4;
                        }
                    open = nx;
                }
                for (int x = n / 2 - 2; x <= n / 2 + 2; x++) for (int y = n / 2 - 2; y <= n / 2 + 2; y++) open[x, y] = true;
                // оставляем только область, достижимую из центра
                var comp = new bool[n, n]; var q = new Queue<Vector2Int>(); q.Enqueue(new Vector2Int(n / 2, n / 2)); comp[n / 2, n / 2] = true; total = 0;
                while (q.Count > 0)
                {
                    var c = q.Dequeue(); total++;
                    for (int d = 0; d < 4; d++) { int ax = c.x + DX[d], ay = c.y + DY[d]; if (ax < 1 || ay < 1 || ax >= n - 1 || ay >= n - 1 || comp[ax, ay] || !open[ax, ay]) continue; comp[ax, ay] = true; q.Enqueue(new Vector2Int(ax, ay)); }
                }
                open = comp;
                if (total * 100 >= n * n * minOpenPct) break; fill -= 0.03;
            }
            solid = new bool[n, n];
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) { solid[x, y] = !open[x, y]; if (open[x, y]) { if (x + 1 < n && open[x + 1, y]) Link(x, y, 0); if (y + 1 < n && open[x, y + 1]) Link(x, y, 2); } }
            sx = n / 2; sy = n / 2; return total * 100 >= n * n * (minOpenPct / 2);
        }

        // ---------- гараж: колонны, открытые площади, закрытые подсобки ----------
        public void GenGarage(System.Random rnd)
        {
            OpenAllInner();
            int rooms = Mathf.Max(3, n / 7);
            for (int i = 0; i < rooms; i++)
            {
                int w = 3 + rnd.Next(3), h = 3 + rnd.Next(3), x0 = 2 + rnd.Next(Mathf.Max(1, n - w - 4)), y0 = 2 + rnd.Next(Mathf.Max(1, n - h - 4));
                if (Mathf.Abs(x0 + w / 2 - n / 2) < 5 && Mathf.Abs(y0 + h / 2 - n / 2) < 5) continue;
                for (int y = y0; y < y0 + h; y++) { wv[x0, y] = true; wv[x0 + w, y] = true; }
                for (int x = x0; x < x0 + w; x++) { wh[x, y0] = true; wh[x, y0 + h] = true; }
                int side = rnd.Next(4), t = rnd.Next(0, side < 2 ? h : w);
                if (side == 0) wv[x0, y0 + t] = false; else if (side == 1) wv[x0 + w, y0 + t] = false; else if (side == 2) wh[x0 + t, y0] = false; else wh[x0 + t, y0 + h] = false;
            }
            // длинные бетонные перегородки с проёмами — делят гараж на секции
            for (int i = 0; i < n / 6; i++)
            {
                bool horiz = rnd.Next(2) == 0; int len = 8 + rnd.Next(14), a = 3 + rnd.Next(Mathf.Max(1, n - 6)), b = rnd.Next(0, Mathf.Max(1, n - len)); int gap = b + 2 + rnd.Next(Mathf.Max(1, len - 4));
                if (Mathf.Abs(a - n / 2) < 4) continue;
                for (int k = b; k < b + len && k < n; k++) { if (k == gap || k == gap + 1) continue; if (horiz) wh[k, a] = true; else wv[a, k] = true; }
            }
        }

        // ---------- склад: параллельные ряды стеллажей с поперечными проходами ----------
        public void GenShelves(System.Random rnd)
        {
            OpenAllInner();
            int hall = n / 2;
            for (int y = 3; y < n - 2; y += 2)
            {
                if (Mathf.Abs(y - hall) < 4) continue;                    // центральная площадка — свободна
                int x = 1;
                while (x < n - 1)
                {
                    int seg = 5 + rnd.Next(9); int end = Mathf.Min(n - 1, x + seg);
                    if (rnd.NextDouble() > 0.1) for (int k = x; k < end; k++) wh[k, y] = true;
                    x = end + 1 + rnd.Next(2);                              // поперечный проход 1–2 клетки
                }
            }
            // несколько вертикальных стен делят склад на секции
            for (int i = 0; i < 3; i++)
            {
                int a = 6 + rnd.Next(Mathf.Max(1, n - 12)); if (Mathf.Abs(a - n / 2) < 4) continue; int gap = rnd.Next(2, Mathf.Max(3, n - 2));
                for (int k = 0; k < n; k++) { if (Mathf.Abs(k - gap) < 2 || Mathf.Abs(k - hall) < 3) continue; wv[a, k] = true; }
            }
        }

        public static void Dump(Grid g, System.IO.TextWriter w)
        {
            // схема со стенами вокруг старта (для тестового харнесса): # — стена, пробел — проход, : — толща, E — дверь, S — старт
            int x0 = Mathf.Max(0, g.sx - 22), x1 = Mathf.Min(g.n, g.sx + 22), y0 = Mathf.Max(0, g.sy - 14), y1 = Mathf.Min(g.n, g.sy + 14);
            for (int y = y1; y >= y0; y--)
            {
                var top = new System.Text.StringBuilder(); var mid = new System.Text.StringBuilder();
                for (int x = x0; x < x1; x++)
                {
                    bool hw = y < g.n + 1 && g.wh[x, y]; top.Append('+'); top.Append(hw ? "##" : "  ");
                    if (y < y1)
                    {
                        mid.Append(g.wv[x, y] ? '#' : ' ');
                        char c = g.dist != null && g.dist[x, y] < 0 ? ':' : ' '; if (x == g.sx && y == g.sy) c = 'S'; foreach (var e in g.exits) if (e.x == x && e.y == y) c = 'E'; mid.Append(c); mid.Append(c);
                    }
                }
                w.WriteLine(top.ToString()); if (y < y1 && y > y0 - 1) w.WriteLine(mid.ToString());
            }
        }
    }
}
