// Новые планировки 1.32: L6 «Бескрайняя тьма» (огромный зал с монолитами и перегородками), L37 «Бассейновый комплекс» (3×3 зала с дверями),
// L94 «Городок» (открытые луга с разбросанными домиками, площадь и дороги).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal sealed partial class Grid
    {
        void Mono(int x0, int y0, int w, int h) { WallBox(x0, y0, w, h); MarkSolid(x0, y0, w, h); }

        // ---------- L6: один гигантский зал в темноте ----------
        public void GenVast(System.Random rnd)
        {
            LinkRect(0, 0, n, n); int mid = n / 2; var occ = new bool[n, n];
            Func<int, int, int, int, bool> free = (x, y, w, h) =>
            {
                if (x < 1 || y < 1 || x + w >= n - 1 || y + h >= n - 1) return false; if (Mathf.Abs(x + w / 2 - mid) < 5 && Mathf.Abs(y + h / 2 - mid) < 5) return false;
                for (int i = x - 1; i <= x + w; i++) for (int j = y - 1; j <= y + h; j++) if (i >= 0 && j >= 0 && i < n && j < n && occ[i, j]) return false; return true;
            };
            // монолиты — тёмные колонны-глыбы
            for (int k = 0; k < n * n / 40; k++)
            {
                int w = 1 + rnd.Next(2), h = 1 + rnd.Next(2), x = rnd.Next(n), y = rnd.Next(n); if (!free(x, y, w, h)) continue;
                for (int i = x; i < x + w; i++) for (int j = y; j < y + h; j++) occ[i, j] = true; Mono(x, y, w, h);
            }
            // длинные перегородки с проёмами
            for (int k = 0; k < n / 2; k++)
            {
                bool vert = rnd.Next(2) == 0; int len = 8 + rnd.Next(14), a = 2 + rnd.Next(Mathf.Max(1, n - len - 4)), b = 3 + rnd.Next(Mathf.Max(1, n - 6)); if (Mathf.Abs(b - mid) < 4) continue;
                int gap = a + 2 + rnd.Next(Mathf.Max(1, len - 4));
                for (int i = a; i < a + len; i++) { if (i == gap || i == gap + 1) continue; if (vert) { if (!occ[Mathf.Min(b, n - 1), Mathf.Min(i, n - 1)] && !occ[Mathf.Max(b - 1, 0), Mathf.Min(i, n - 1)]) SetV(b, i, true); } else { if (!occ[Mathf.Min(i, n - 1), Mathf.Min(b, n - 1)] && !occ[Mathf.Min(i, n - 1), Mathf.Max(b - 1, 0)]) SetH(i, b, true); } }
            }
            sx = mid; sy = mid;
        }

        // ---------- L37: комплекс 3×3 больших залов ----------
        public void GenComplex(System.Random rnd)
        {
            int a = n / 3, b = 2 * n / 3; int[] xs = { 0, a, b, n }; int[] ys = xs; int mid = n / 2;
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) Room(xs[i], ys[j], xs[i + 1] - xs[i], ys[j + 1] - ys[j], false);
            // двери между залами: по две клетки в середине + иногда запасная
            for (int i = 0; i < 2; i++) for (int j = 0; j < 3; j++)
            {
                int y0 = ys[j], y1 = ys[j + 1], ym = (y0 + y1) / 2; SetV(xs[i + 1], ym, false); SetV(xs[i + 1], ym - 1, false);
                if (rnd.NextDouble() < 0.5) { int yy = y0 + 1 + rnd.Next(Mathf.Max(1, y1 - y0 - 2)); SetV(xs[i + 1], yy, false); }
            }
            for (int j = 0; j < 2; j++) for (int i = 0; i < 3; i++)
            {
                int x0 = xs[i], x1 = xs[i + 1], xm = (x0 + x1) / 2; SetH(xm, ys[j + 1], false); SetH(xm - 1, ys[j + 1], false);
                if (rnd.NextDouble() < 0.5) { int xx = x0 + 1 + rnd.Next(Mathf.Max(1, x1 - x0 - 2)); SetH(xx, ys[j + 1], false); }
            }
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++)
            {
                int x0 = xs[i], x1 = xs[i + 1], y0 = ys[j], y1 = ys[j + 1], w = x1 - x0, h = y1 - y0;
                if (i == 1 && j == 1)
                {   // главный бассейн: четыре колонны по краям
                    foreach (int fx in new[] { x0 + w / 4, x0 + 3 * w / 4 - 1 }) foreach (int fy in new[] { y0 + h / 4, y0 + 3 * h / 4 - 1 }) if (Mathf.Abs(fx - mid) >= 3 || Mathf.Abs(fy - mid) >= 3) Mono(fx, fy, 2, 2);
                }
                else if ((i + j) % 2 == 1)
                {   // зал-кольцо: в центре остров (раздевалки)
                    int bw = Mathf.Max(2, w / 2), bh = Mathf.Max(2, h / 2); Mono(x0 + (w - bw) / 2, y0 + (h - bh) / 2, bw, bh);
                }
                else
                {   // угловые залы: дорожки-перегородки с чередующимися проходами
                    int k = 0; for (int xx = x0 + 3; xx < x1 - 2; xx += 3, k++) for (int y = y0 + 1; y < y1 - 1; y++) { bool gap = k % 2 == 0 ? y < y0 + 3 : y >= y1 - 3; if (!gap) SetV(xx, y, true); }
                }
            }
            sx = mid; sy = mid;
        }

        // ---------- L94: городок на лугах ----------
        public void GenTown(System.Random rnd)
        {
            int mid = n / 2; var open = new bool[n, n]; for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) open[x, y] = true;
            var occ = new bool[n, n]; int R = Mathf.Max(8, n / 3);
            Func<int, int, bool> road = (x, y) => Mathf.Abs(x - mid) <= 1 || Mathf.Abs(y - mid) <= 1 || ((Mathf.Abs(Mathf.Abs(x - mid) - R) <= 1 && Mathf.Abs(y - mid) <= R + 1) || (Mathf.Abs(Mathf.Abs(y - mid) - R) <= 1 && Mathf.Abs(x - mid) <= R + 1));
            var houses = new List<int[]>();
            for (int k = 0; k < n * n / 3; k++)
            {
                int w = 2 + rnd.Next(3), h = 2 + rnd.Next(2); if (rnd.Next(2) == 0) { int t = w; w = h; h = t; }
                int x = 2 + rnd.Next(Mathf.Max(1, n - w - 4)), y = 2 + rnd.Next(Mathf.Max(1, n - h - 4)); bool ok = Mathf.Abs(x + w / 2 - mid) > 7 || Mathf.Abs(y + h / 2 - mid) > 7;
                for (int i = x - 1; ok && i <= x + w; i++) for (int j = y - 1; j <= y + h; j++) if (i < 0 || j < 0 || i >= n || j >= n || occ[i, j] || road(i, j)) { ok = false; break; }
                if (!ok) continue; for (int i = x - 1; i <= x + w; i++) for (int j = y - 1; j <= y + h; j++) occ[i, j] = true; houses.Add(new[] { x, y, w, h });
            }
            foreach (var h in houses) for (int i = h[0]; i < h[0] + h[2]; i++) for (int j = h[1]; j < h[1] + h[3]; j++) open[i, j] = false;
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) if (open[x, y]) { if (x + 1 < n && open[x + 1, y]) Link(x, y, 0); if (y + 1 < n && open[x, y + 1]) Link(x, y, 2); }
            // каменные оградки на лугах — ломают линию взгляда
            for (int k = 0; k < n / 4; k++)
            {
                bool vert = rnd.Next(2) == 0; int len = 3 + rnd.Next(4), a = 2 + rnd.Next(Mathf.Max(1, n - len - 4)), b = 3 + rnd.Next(Mathf.Max(1, n - 6));
                for (int i = a; i < a + len && i < n; i++) { int cx = vert ? b : i, cy = vert ? i : b; if (road(cx, cy) || cx >= n || cy >= n || !open[cx, cy] || (vert ? !open[Mathf.Max(cx - 1, 0), cy] : !open[cx, Mathf.Max(cy - 1, 0)])) continue; if (vert) SetV(b, i, true); else SetH(i, b, true); }
            }
            solid = new bool[n, n]; for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) solid[x, y] = !open[x, y];
            sx = mid; sy = mid;
        }
    }
}
