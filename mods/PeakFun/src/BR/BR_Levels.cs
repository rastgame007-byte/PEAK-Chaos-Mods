// Описание уровней Backrooms и генератор планировок (сетка клеток 4×4 м со стенами между клетками).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakBR
{
    internal enum Style { Maze, Open, Pipes, Rooms, Hall, Snake, Dark, Garage, Shelves, Hotel, Apart, School, Cave, Field, Suburb, City, Courtyard, Tunnels, Duct, Office, Pools, Lab, Vast, Complex, Town }

    internal sealed class LevelDef
    {
        public string id, name, eng, blurb, tip; public int baseN; public Style style; public float H = 3.3f;
        public int wallTex, floorTex, ceilTex; public Color tint = Color.white, fog, amb, lamp; public float fogDen = 0.015f, lampInt = 2.3f, lampKeep = 1f, flicker = 0.12f, anger0 = 0.1f;
        public int stealers, smilers, hounds, party, moths; public bool rest, dark, chaseStart; public int exits = 2; public float timeLimit;
        public Color exitCol = new Color(0.25f, 1f, 0.5f); public string humKind = "hum";
        // новые уровни и монстры (1.21): счётчики, «небо» вместо потолка, затопление
        public float wallT = 0.35f; public int bacteria, clumps, windows, dullers, faces, kids, wretches, things, watchers; public float bactDelay; public bool sky, dome, poles = true, flood, dullHunt;
    }

    internal sealed partial class Grid
    {
        public int n; public bool[,] wv, wh; public int sx, sy; public List<Vector2Int> exits = new List<Vector2Int>(); public int[,] dist;
        public Grid(int n)
        {
            this.n = n; wv = new bool[n + 1, n]; wh = new bool[n, n + 1];
            for (int x = 0; x <= n; x++) for (int y = 0; y < n; y++) wv[x, y] = true;
            for (int x = 0; x < n; x++) for (int y = 0; y <= n; y++) wh[x, y] = true;
        }
        public bool Pass(int x, int y, int d)   // 0 вправо, 1 влево, 2 вверх, 3 вниз
        {
            switch (d)
            {
                case 0: return x + 1 < n && !wv[x + 1, y];
                case 1: return x - 1 >= 0 && !wv[x, y];
                case 2: return y + 1 < n && !wh[x, y + 1];
                default: return y - 1 >= 0 && !wh[x, y];
            }
        }
        public void Open(int x, int y, int ax, int ay)
        {
            if (ax > x) wv[ax, y] = false; else if (ax < x) wv[x, y] = false; else if (ay > y) wh[x, ay] = false; else wh[x, y] = false;
        }
        public static readonly int[] DX = { 1, -1, 0, 0 }, DY = { 0, 0, 1, -1 };
        public int[,] Bfs(int sx0, int sy0)
        {
            var d = new int[n, n]; for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) d[x, y] = -1;
            var q = new Queue<Vector2Int>(); q.Enqueue(new Vector2Int(sx0, sy0)); d[sx0, sy0] = 0;
            while (q.Count > 0) { var c = q.Dequeue(); for (int k = 0; k < 4; k++) if (Pass(c.x, c.y, k)) { int nx = c.x + DX[k], ny = c.y + DY[k]; if (d[nx, ny] < 0) { d[nx, ny] = d[c.x, c.y] + 1; q.Enqueue(new Vector2Int(nx, ny)); } } }
            return d;
        }
        /// <summary>Направление, где у клетки есть стена (для двери). Vector3.zero — стен нет.</summary>
        public Vector3 WallDir(int x, int y)
        {
            if (!Pass(x, y, 0)) return Vector3.right; if (!Pass(x, y, 1)) return Vector3.left; if (!Pass(x, y, 2)) return Vector3.forward; if (!Pass(x, y, 3)) return Vector3.back; return Vector3.zero;
        }
        public void Border() { for (int y = 0; y < n; y++) { wv[0, y] = true; wv[n, y] = true; } for (int x = 0; x < n; x++) { wh[x, 0] = true; wh[x, n] = true; } }
        public void OpenAllInner() { for (int x = 1; x < n; x++) for (int y = 0; y < n; y++) wv[x, y] = false; for (int x = 0; x < n; x++) for (int y = 1; y < n; y++) wh[x, y] = false; }

        /// <summary>Гарантия связности: недостижимые клетки соединяются с достижимыми.</summary>
        public void Connect(System.Random rnd)
        {
            for (int guard = 0; guard < 400; guard++)
            {
                var d = Bfs(sx, sy); var cand = new List<int[]>();
                for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) if (d[x, y] < 0) for (int k = 0; k < 4; k++) { int nx = x + DX[k], ny = y + DY[k]; if (nx >= 0 && ny >= 0 && nx < n && ny < n && d[nx, ny] >= 0 && (solid == null || !solid[x, y])) cand.Add(new[] { x, y, nx, ny }); }
                if (cand.Count == 0) return;
                int cnt = Mathf.Max(1, cand.Count / 6); for (int i = 0; i < cnt; i++) { var c = cand[rnd.Next(cand.Count)]; Open(c[0], c[1], c[2], c[3]); }
            }
        }

        // ================= генераторы =================
        public void GenMaze(System.Random rnd, double loops, int halls, double longBias)
        {
            var seen = new bool[n, n]; var st = new Stack<Vector2Int>(); var dirs = new Stack<int>(); st.Push(new Vector2Int(Mathf.Clamp(sx, 0, n - 1), Mathf.Clamp(sy, 0, n - 1))); dirs.Push(-1); seen[st.Peek().x, st.Peek().y] = true;
            while (st.Count > 0)
            {
                var c = st.Peek(); int last = dirs.Peek(); var opts = new List<int>();
                for (int d = 0; d < 4; d++) { int nx = c.x + DX[d], ny = c.y + DY[d]; if (nx >= 0 && ny >= 0 && nx < n && ny < n && !seen[nx, ny]) opts.Add(d); }
                if (opts.Count == 0) { st.Pop(); dirs.Pop(); continue; }
                int di = opts[rnd.Next(opts.Count)]; if (last >= 0 && opts.Contains(last) && rnd.NextDouble() < longBias) di = last;
                int ax = c.x + DX[di], ay = c.y + DY[di]; Open(c.x, c.y, ax, ay); seen[ax, ay] = true; st.Push(new Vector2Int(ax, ay)); dirs.Push(di);
            }
            for (int x = 1; x < n; x++) for (int y = 0; y < n; y++) if (wv[x, y] && rnd.NextDouble() < loops) wv[x, y] = false;
            for (int x = 0; x < n; x++) for (int y = 1; y < n; y++) if (wh[x, y] && rnd.NextDouble() < loops) wh[x, y] = false;
            for (int r = 0; r < halls; r++)
            {
                int w = 3 + rnd.Next(5), h = 3 + rnd.Next(4), x0 = rnd.Next(0, Mathf.Max(1, n - w)), y0 = rnd.Next(0, Mathf.Max(1, n - h));
                for (int x = x0; x < x0 + w && x < n; x++) for (int y = y0; y < y0 + h && y < n; y++) { if (x > x0) wv[x, y] = false; if (y > y0) wh[x, y] = false; }
            }
        }

        public void GenOpen(System.Random rnd, int lines, int minLen, int maxLen)
        {
            OpenAllInner();
            for (int i = 0; i < lines; i++)
            {
                bool horiz = rnd.Next(2) == 0; int len = minLen + rnd.Next(Mathf.Max(1, maxLen - minLen)); int a = rnd.Next(1, n - 1), b = rnd.Next(0, Mathf.Max(1, n - len));
                int gap1 = b + rnd.Next(len), gap2 = rnd.NextDouble() < 0.6 ? b + rnd.Next(len) : -1;
                for (int k = b; k < b + len && k < n; k++) { if (k == gap1 || k == gap2) continue; if (horiz) wh[k, a] = true; else wv[a, k] = true; }
            }
        }

        struct R2 { public int x, y, w, h; }
        public void GenRooms(System.Random rnd, int minLeaf, double extraDoor, int doorW)
        {
            OpenAllInner(); var stack = new Stack<R2>(); stack.Push(new R2 { x = 0, y = 0, w = n, h = n });
            while (stack.Count > 0)
            {
                var r = stack.Pop(); bool canV = r.w >= minLeaf * 2, canH = r.h >= minLeaf * 2; if (!canV && !canH) continue;
                if (r.w <= minLeaf * 3 && r.h <= minLeaf * 3 && rnd.NextDouble() < 0.25) continue;
                bool vert = canV && (!canH || r.w > r.h || (r.w == r.h && rnd.Next(2) == 0));
                if (vert)
                {
                    int cut = r.x + minLeaf + rnd.Next(r.w - 2 * minLeaf + 1); int dw = Mathf.Min(doorW, r.h);
                    for (int y = r.y; y < r.y + r.h; y++) wv[cut, y] = true;
                    int d0 = r.y + rnd.Next(r.h - dw + 1); for (int k = 0; k < dw; k++) wv[cut, d0 + k] = false;
                    if (rnd.NextDouble() < extraDoor) { int e0 = r.y + rnd.Next(r.h - dw + 1); for (int k = 0; k < dw; k++) wv[cut, e0 + k] = false; }
                    stack.Push(new R2 { x = r.x, y = r.y, w = cut - r.x, h = r.h }); stack.Push(new R2 { x = cut, y = r.y, w = r.x + r.w - cut, h = r.h });
                }
                else
                {
                    int cut = r.y + minLeaf + rnd.Next(r.h - 2 * minLeaf + 1); int dw = Mathf.Min(doorW, r.w);
                    for (int x = r.x; x < r.x + r.w; x++) wh[x, cut] = true;
                    int d0 = r.x + rnd.Next(r.w - dw + 1); for (int k = 0; k < dw; k++) wh[d0 + k, cut] = false;
                    if (rnd.NextDouble() < extraDoor) { int e0 = r.x + rnd.Next(r.w - dw + 1); for (int k = 0; k < dw; k++) wh[e0 + k, cut] = false; }
                    stack.Push(new R2 { x = r.x, y = r.y, w = r.w, h = cut - r.y }); stack.Push(new R2 { x = r.x, y = cut, w = r.w, h = r.y + r.h - cut });
                }
            }
        }

        /// <summary>Длинный коридор-змейка: ряды через один, на концах рядов переходы.</summary>
        public void GenSnake()
        {
            int row = 0;
            for (int y = 0; y < n; y += 2)
            {
                for (int x = 1; x < n; x++) wv[x, y] = false;
                if (y + 2 < n) { int xe = (row % 2 == 0) ? n - 1 : 0; wh[xe, y + 1] = false; wh[xe, y + 2] = false; }
                row++;
            }
        }

        public const int MinExitCells = 25; public bool[,] behind;
        public void PickExits(System.Random rnd, int count, bool snake)
        {
            dist = Bfs(sx, sy); int best = -1, ex = 0, ey = 0;
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) if (dist[x, y] > best && WallDir(x, y) != Vector3.zero) { best = dist[x, y]; ex = x; ey = y; }
            exits.Clear();
            if (snake) { exits.Add(new Vector2Int(ex, ey)); return; }   // змейка: дверь в конце коридора
            // двери в случайных местах: главная — на случайном удалении 35–95% от самой дальней точки, остальные — в других случайных поясах
            if (count < 3 && count >= 2) count = 3;
            var cand = new List<Vector2Int>(); int lo = Mathf.Max(4, Mathf.RoundToInt(best * 0.35f)), hi = Mathf.Max(lo + 1, Mathf.RoundToInt(best * 0.95f));
            // 1.30: выход всегда далеко — не меньше ~100 м по коридорам (25 клеток по 4 м); на совсем маленьких картах — не ближе 75% самой дальней точки
            int minD = Mathf.Min(MinExitCells, Mathf.RoundToInt(best * 0.75f)); lo = Mathf.Max(lo, minD); hi = Mathf.Max(lo + 1, hi);
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) if (dist[x, y] >= lo && dist[x, y] <= hi && WallDir(x, y) != Vector3.zero) cand.Add(new Vector2Int(x, y));
            if (cand.Count == 0) { exits.Add(new Vector2Int(ex, ey)); return; }
            exits.Add(cand[rnd.Next(cand.Count)]); if (count < 2) return;
            foreach (float fr in new[] { 0.55f, 0.32f, 0.75f })
            {
                if (exits.Count >= count) break;
                int want = Mathf.RoundToInt(best * Mathf.Clamp(fr + ((float)rnd.NextDouble() - 0.5f) * 0.3f, 0.2f, 0.95f)), bx = -1, by = -1, bs = int.MaxValue;
                for (int x = 0; x < n; x++) for (int y = 0; y < n; y++)
                {
                    if (dist[x, y] < Mathf.Max(4, minD) || WallDir(x, y) == Vector3.zero) continue; bool far = true; foreach (var e in exits) if (Mathf.Abs(e.x - x) + Mathf.Abs(e.y - y) < Mathf.Max(5, n / 5)) far = false; if (!far) continue;
                    int sc = Mathf.Abs(dist[x, y] - want) * 10 + rnd.Next(7); if (sc < bs) { bs = sc; bx = x; by = y; }
                }
                if (bx >= 0) exits.Add(new Vector2Int(bx, by));
            }
        }

        public static Grid Make(LevelDef L, int n, int seed)
        {
            var rnd = new System.Random(seed); var g = new Grid(n); bool snake = L.style == Style.Snake;
            g.sx = snake ? 0 : n / 2; g.sy = snake ? 0 : n / 2;
            switch (L.style)
            {
                case Style.Maze: g.GenMaze(rnd, 0.3, 2 + n / 14, 0.0); g.Braid(rnd, 0.8); break;
                case Style.Dark: g.GenWide(rnd); break;
                case Style.Pipes: g.GenMaze(rnd, 0.05, 0, 0.7); break;
                case Style.Tunnels: g.GenTunnels(rnd); break;
                case Style.Duct: g.GenDuct(rnd); break;
                case Style.Open: g.GenIslands(rnd); break;
                case Style.Rooms: g.GenStation(rnd); break;
                case Style.Office: g.GenOffice(rnd); break;
                case Style.Pools: g.GenPools(rnd); break;
                case Style.Hall: g.GenRooms(rnd, 5, 0.9, 2); break;
                case Style.Garage: g.GenParking(rnd); break;
                case Style.Shelves: g.GenShelves(rnd); break;
                case Style.Hotel: g.GenHotel(rnd); break;
                case Style.Apart: g.GenLattice(rnd, 11, 4, 2, 3, 0.95, 0.04); break;
                case Style.School: g.GenLattice(rnd, 7, 5, 3, 3, 0.9, 0.05); break;
                case Style.Cave: if (!g.GenCave(rnd, 0.47, 4, 34)) g.GenMaze(rnd, 0.1, 0, 0.3); break;
                case Style.Field: g.GenTrails(rnd); break;
                case Style.Suburb: g.GenSuburb(rnd); break;
                case Style.Lab: g.GenLab(rnd); break;
                case Style.Vast: g.GenVast(rnd); break;
                case Style.Complex: g.GenComplex(rnd); break;
                case Style.Town: g.GenTown(rnd); break;
                case Style.City: g.GenBlocks(rnd, 9, 2, 0.08, 0.5); break;
                case Style.Courtyard: g.GenBlocks(rnd, 13, 5, 0.0, 0.0); break;
                default: g.GenSnake(); break;
            }
            g.Border();
            if (snake && L.chaseStart)
            {   // «Беги!»: игрок стартует глубже в коридоре, а всё, что осталось позади (тупик начала), — территория погони
                var d0 = g.Bfs(0, 0); int maxd0 = 0; for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) maxd0 = Mathf.Max(maxd0, d0[x, y]);
                int tgt = Mathf.Clamp(maxd0 / 4, 6, 14); int bx = -1, by = -1;
                for (int x = 0; x < n && bx < 0; x++) for (int y = 0; y < n; y++) if (d0[x, y] == tgt) { bx = x; by = y; break; }
                if (bx >= 0) { g.behind = new bool[n, n]; for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) g.behind[x, y] = d0[x, y] >= 0 && d0[x, y] < tgt; g.sx = bx; g.sy = by; }
            }
            if (!snake)
            {
                // зона спавна — свободная площадка 3×3
                for (int x = g.sx - 1; x <= g.sx + 1; x++) for (int y = g.sy - 1; y <= g.sy + 1; y++) { if (x > g.sx - 1 && x < n) g.wv[x, y] = false; if (y > g.sy - 1 && y < n) g.wh[x, y] = false; }
                g.Connect(rnd);
            }
            g.PickExits(rnd, L.exits, snake || L.style == Style.Duct); return g;
        }
    }

    internal static class Levels
    {
        public static readonly LevelDef[] All = Build();
        static Color C(float r, float g, float b) { return new Color(r, g, b); }

        static LevelDef[] Build()
        {
            return new[]
            {
                new LevelDef { id = "0", name = "Уровень 0 — Лобби", eng = "The Lobby", baseN = 56, style = Style.Maze, H = 3.3f, wallTex = 0, floorTex = 0, ceilTex = 0, tint = C(1f, 1f, 1f), fog = C(0.3f, 0.25f, 0.1f), amb = C(0.32f, 0.27f, 0.13f), lamp = C(1f, 0.94f, 0.76f), fogDen = 0.012f, lampInt = 3.1f, bacteria = 1, stealers = 1, 
                    bactDelay = 45f, anger0 = 0.05f, humKind = "hum", blurb = "Бесконечные жёлтые комнаты, мокрый ковёр и гул ламп. Отсюда начинают все. Дверь EXIT — зелёная, на дальней стене.", tip = "Монстр сначала лишь подглядывает. Не стой на месте. Через 3 минуты в Лобби приходит Бактерия — она слышит бег и прыжки." },
                new LevelDef { id = "1", name = "Уровень 1 — Обитаемая зона", eng = "Habitable Zone", baseN = 64, style = Style.Garage, H = 4.6f, wallTex = 1, floorTex = 1, ceilTex = 1, tint = C(0.95f, 0.97f, 1f), fog = C(0.1f, 0.11f, 0.13f), amb = C(0.12f, 0.13f, 0.16f), lamp = C(0.85f, 0.92f, 1f), fogDen = 0.012f, lampKeep = 0.8f, lampInt = 2.6f, smilers = 1, hounds = 1, dullers = 1, 
                    anger0 = 0.15f, humKind = "hum", blurb = "Подземный гараж без конца: сетка бетонных колонн, брошенные машины, разметка на полу, закрытые подсобки. Звук разносится далеко.", tip = "Кто-то в чужой одежде идёт к тебе слишком ровно. Это не друг." },
                new LevelDef { id = "2", name = "Уровень 2 — Трубы", eng = "Pipe Dreams", baseN = 66, style = Style.Tunnels, wallT = 1.4f, H = 3.2f, wallTex = 2, floorTex = 2, ceilTex = 2, tint = C(0.85f, 0.95f, 0.9f), fog = C(0.04f, 0.07f, 0.06f), amb = C(0.06f, 0.09f, 0.08f), lamp = C(0.7f, 1f, 0.85f), fogDen = 0.03f, lampKeep = 0.5f, lampInt = 2.0f, flicker = 0.25f, smilers = 2, hounds = 1, stealers = 1, 
                    anger0 = 0.25f, humKind = "pipe", blurb = "Тесные коридоры, низкий потолок, трубы и жар. Лампы редкие, во тьме — улыбки.", tip = "Улыбки двигаются, только пока на них не смотришь. Смотри, пока отступаешь." },
                new LevelDef { id = "3", name = "Уровень 3 — Электростанция", eng = "Electrical Station", baseN = 60, style = Style.Rooms, H = 3.8f, wallTex = 3, floorTex = 3, ceilTex = 1, tint = C(0.9f, 0.95f, 1f), fog = C(0.05f, 0.06f, 0.09f), amb = C(0.08f, 0.1f, 0.15f), lamp = C(0.8f, 0.9f, 1f), fogDen = 0.02f, lampKeep = 0.55f, flicker = 0.3f, smilers = 1, hounds = 2, moths = 2, stealers = 1, 
                    anger0 = 0.35f, humKind = "buzz", blurb = "Залы с трансформаторами. На полу лужи с током — они искрят. Собаки Подвала ходят стаей.", tip = "Обходи искрящие лужи. Собаки теряют след, если ты пропал из виду." },
                new LevelDef { id = "4", name = "Уровень 4 — Заброшенный офис", eng = "Abandoned Office", baseN = 60, style = Style.Office, H = 2.9f, wallTex = 4, floorTex = 4, ceilTex = 0, tint = C(1f, 0.97f, 0.92f), fog = C(0.14f, 0.13f, 0.1f), amb = C(0.16f, 0.15f, 0.12f), lamp = C(1f, 0.97f, 0.85f), fogDen = 0.018f, lampKeep = 0.7f, hounds = 1, dullers = 2, 
                    anger0 = 0.3f, humKind = "hum", blurb = "Перегородки, столы, мониторы, запах бумаги. Тишина давит. В стенах — окна, а за стёклами кто-то стоит.", tip = "Двое Воров Кожи подбираются с разных сторон. Не подходи близко к окнам: тот, кто за стеклом, затянет внутрь." },
                new LevelDef { id = "5", name = "Уровень 5 — Отель Ужаса", eng = "Terror Hotel", baseN = 56, style = Style.Hotel, H = 3.2f, wallTex = 5, floorTex = 5, ceilTex = 3, tint = C(1f, 0.95f, 0.95f), fog = C(0.1f, 0.04f, 0.04f), amb = C(0.14f, 0.08f, 0.07f), lamp = C(1f, 0.78f, 0.55f), fogDen = 0.022f, lampKeep = 0.65f, flicker = 0.18f, moths = 7, watchers = 1, 
                    anger0 = 0.4f, humKind = "hum", exitCol = C(1f, 0.8f, 0.3f), blurb = "Бордовые ковры, золото и обои. Мотыльки-Смерти кружат у ламп, а в номерах кто-то ходит.", tip = "Мотыльки жалят спорами. Они летят на свет — ты тоже светишься." },
                new LevelDef { id = "6", name = "Уровень 6 — Выключи свет", eng = "Lights Out", baseN = 70, style = Style.Vast, H = 3.3f, wallTex = 9, floorTex = 9, ceilTex = 1, tint = C(0.7f, 0.7f, 0.75f), fog = C(0.01f, 0.01f, 0.015f), amb = C(0.015f, 0.015f, 0.025f), lamp = C(0.7f, 0.8f, 1f), fogDen = 0.035f, lampKeep = 0.07f, lampInt = 1.8f, flicker = 0.4f, dark = true,
                    anger0 = 0.55f, humKind = "dark", exitCol = C(0.4f, 1f, 0.6f), blurb = "Почти полная темнота, редкие мёртвые лампы. У камеры есть слабый фонарь. Дальше — глубже.", tip = "Слушай шаги и дыхание. Дверь выхода светится — держись подальше от собак." },
                new LevelDef { id = "7", name = "Уровень 7 — Таласофобия", eng = "Thalassophobia", baseN = 56, style = Style.Open, H = 3.4f, wallTex = 10, floorTex = 10, ceilTex = 5, tint = C(0.85f, 0.95f, 1f), fog = C(0.09f, 0.12f, 0.14f), amb = C(0.09f, 0.11f, 0.13f), lamp = C(0.7f, 0.88f, 1f), fogDen = 0.028f, lampKeep = 0.5f, lampInt = 2.1f, flicker = 0.3f, things = 1,  dome = true,
                    flood = true, anger0 = 0.3f, humKind = "sea", exitCol = C(0.3f, 0.85f, 1f), blurb = "Затопленные отсеки и каюты: вода по щиколотку, ржавый металл, скрип. Где-то в глубине ходит нечто огромное — Тварь с Уровня 7.", tip = "Тварь чует бег и плеск. Иди шагом, присядь, если она рядом, и прячься за переборками." },
                new LevelDef { id = "8", name = "Уровень 8 — Пещеры", eng = "Cave System", baseN = 64, style = Style.Cave, wallT = 1.0f, H = 4.4f, wallTex = 11, floorTex = 11, ceilTex = 6, tint = C(0.8f, 0.76f, 0.72f), fog = C(0.012f, 0.015f, 0.018f), amb = C(0.02f, 0.025f, 0.03f), lamp = C(0.35f, 1f, 0.85f), fogDen = 0.032f, lampKeep = 0.16f, lampInt = 1.6f, flicker = 0.1f, dark = true, sky = true, poles = false, wretches = 2, clumps = 2, 
                    anger0 = 0.35f, humKind = "cave", exitCol = C(0.4f, 1f, 0.7f), blurb = "Узкие проходы, сталактиты, капает вода. Редкие светящиеся грибы. В стенах что-то есть — оно слышит шаги.", tip = "Дуллеры тянут руки сквозь стену, когда слышат шаги рядом. Иди приседая вдоль стен. Комки висят на потолке — не подходи." },
                new LevelDef { id = "9", name = "Уровень 9 — Тёмный пригород", eng = "Darkened Suburbs", baseN = 60, style = Style.Suburb, H = 5.2f, wallTex = 12, floorTex = 12, ceilTex = 7, tint = C(0.78f, 0.82f, 0.86f), fog = C(0.035f, 0.04f, 0.052f), amb = C(0.05f, 0.055f, 0.07f), lamp = C(1f, 0.78f, 0.45f), fogDen = 0.03f, lampKeep = 0.55f, lampInt = 2.7f, flicker = 0f, sky = true, smilers = 1, stealers = 1, hounds = 1, moths = 2, wretches = 3, watchers = 2,  dome = true,
                    dullHunt = true, anger0 = 0.4f, humKind = "wind", exitCol = C(0.35f, 1f, 0.5f), blurb = "Вечная ночь, туман, дома и фонари. По улицам ходят огромные глаза на руках-ногах — Дозорные.", tip = "Дозорный быстрее тебя. Спрячься за домом, чтобы он потерял тебя из виду. Дуллер боится взгляда — смотри на него." },
                new LevelDef { id = "10", name = "Уровень 10 — Пшеничное поле", eng = "Bumper Crop", baseN = 66, style = Style.Field, H = 2.3f, wallTex = 13, floorTex = 13, ceilTex = 8, tint = C(1f, 1f, 0.95f), fog = C(0.3f, 0.3f, 0.26f), amb = C(0.34f, 0.33f, 0.28f), lamp = C(0.95f, 0.95f, 0.8f), fogDen = 0.022f, lampKeep = 0.5f, lampInt = 1.5f, flicker = 0f, sky = true, poles = false, dome = true,
                    anger0 = 0.25f, humKind = "rain", exitCol = C(1f, 0.9f, 0.4f), blurb = "Дождь и бесконечная пшеница выше головы. Среди колосьев стоят чучела… не все из них — чучела.", tip = "Безликие дети бросаются стаей, если заметят. Присядь и не двигайся — успокоятся. Взрослые безвредны, но смотрят." },
                new LevelDef { id = "11", name = "Уровень 11 — Бесконечный город", eng = "Endless City", baseN = 64, style = Style.City, H = 8f, wallTex = 14, floorTex = 14, ceilTex = 9, tint = C(0.9f, 0.92f, 1f), fog = C(0.07f, 0.075f, 0.11f), amb = C(0.09f, 0.095f, 0.14f), lamp = C(0.85f, 0.92f, 1f), fogDen = 0.014f, lampKeep = 0.75f, lampInt = 2.6f, flicker = 0f, sky = true, faces = 5, hounds = 2, stealers = 1, wretches = 1,  dome = true,
                    anger0 = 0.4f, humKind = "city", exitCol = C(0.4f, 0.9f, 1f), blurb = "Небоскрёбы, пустые улицы, горящие окна. Город живёт без людей — только Безликие бродят в переулках.", tip = "Улицы длинные — выбирай переулки. Стаи Безликих детей и Вор Кожи ходят сами по себе." },
                new LevelDef { id = "15", name = "Уровень 15 — Футуристические залы", eng = "Futuristic Halls", baseN = 58, style = Style.Lab, H = 3.5f, wallTex = 22, floorTex = 22, ceilTex = 7, tint = C(0.92f, 0.97f, 1f), fog = C(0.07f, 0.09f, 0.11f), amb = C(0.13f, 0.16f, 0.19f), lamp = C(0.82f, 0.94f, 1f), fogDen = 0.016f, lampKeep = 0.85f, lampInt = 2.7f, flicker = 0.18f,
                    anger0 = 0.2f, humKind = "buzz", exitCol = C(0.4f, 0.95f, 1f), blurb = "Белые лабораторные залы, стойки с серверами, капсулы с зелёной жидкостью. На полу засохшая кровь, у стен — люди в халатах и мёртвые Псы. Живых здесь нет. Пока.", tip = "Широкий зал в центре — единственный ориентир. Лаборатории по бокам — тупики." },
                new LevelDef { id = "37", name = "Уровень 37 — Бассейны", eng = "The Poolrooms", baseN = 52, style = Style.Complex, H = 3.8f, wallTex = 7, floorTex = 7, ceilTex = 4, tint = C(1f, 1f, 1f), fog = C(0.22f, 0.34f, 0.36f), amb = C(0.3f, 0.4f, 0.42f), lamp = C(0.8f, 1f, 1f), fogDen = 0.008f, lampInt = 2.6f, flicker = 0f,
                    rest = true, anger0 = 0f, humKind = "water", exitCol = C(0.4f, 0.9f, 1f), blurb = "Белая плитка, бирюзовая вода, свет из ниоткуда. Здесь почти безопасно: можно отдохнуть. Только избегай тёмной воды.", tip = "Тёмные бассейны — Гидролитис. Не заходи в воду темнее обычной." },
                new LevelDef { id = "94", name = "Уровень 94 — Движение", eng = "Motion", baseN = 58, style = Style.Town, H = 4.4f, wallTex = 21, floorTex = 21, ceilTex = 7, tint = C(1f, 0.98f, 0.94f), fog = C(0.74f, 0.83f, 0.92f), amb = C(0.6f, 0.62f, 0.66f), lamp = C(1f, 0.9f, 0.65f), fogDen = 0.011f, lampKeep = 0.4f, lampInt = 1.4f, flicker = 0.05f, sky = true, smilers = 1, stealers = 1, hounds = 1, moths = 1,  dome = true,
                    anger0 = 0.45f, humKind = "party", exitCol = C(1f, 0.85f, 0.3f), blurb = "Игрушечный городок 1930-х в стиле stop-motion: пастельные картонные домики, молочные фургоны, фонтан и столбы-сирены с весёлой старой музыкой. Всё немного дёргается и зернит, как глина. Где-то над холмами парит замок.", tip = "Музыка в тишине — не к добру. Ходи между домами, не стой посреди улицы: куклы-Анимации быстрее тебя." },
                new LevelDef { id = "fun", name = "Уровень Fun — Вечеринка", eng = "Fun", baseN = 48, style = Style.Hall, H = 4.2f, wallTex = 6, floorTex = 6, ceilTex = 3, tint = C(1f, 1f, 1f), fog = C(0.12f, 0.06f, 0.12f), amb = C(0.22f, 0.16f, 0.22f), lamp = C(1f, 0.85f, 0.95f), fogDen = 0.012f, lampInt = 3f, flicker = 0.05f,
                    party = 14, anger0 = 0.3f, humKind = "party", exitCol = C(1f, 0.4f, 0.8f), blurb = "Серпантин, шары, столы и торты. Гости танцуют на месте и не любят, когда на них смотрят.", tip = "Гости замечают тебя вблизи — тогда все разом. Прячься от взглядов." },
                new LevelDef { id = "!", name = "Уровень ! — Беги!", eng = "Run For Your Life", baseN = 26, style = Style.Snake, H = 3.0f, wallTex = 8, floorTex = 8, ceilTex = 0, tint = C(1f, 0.85f, 0.85f), fog = C(0.2f, 0.02f, 0.02f), amb = C(0.2f, 0.04f, 0.04f), lamp = C(1f, 0.2f, 0.15f), fogDen = 0.02f, lampInt = 2.4f, flicker = 0.5f,
                    smilers = 1, party = 6, anger0 = 0.9f, chaseStart = true, exits = 1, humKind = "siren", exitCol = C(1f, 0.3f, 0.3f), blurb = "Длинный красный коридор больницы. Сирены. Улыбка и шестеро гостей — за спиной с первой секунды.", tip = "Не останавливайся. Не оглядывайся. Просто беги вперёд." },
            };
        }
    }
}
