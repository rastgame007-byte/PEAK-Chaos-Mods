// Backrooms 1.17: чемодан-приманка (как в PEAK) → провал в огромные сгенерированные Backrooms с настоящим освещением и тенями,
// камера-кассета с помехами и монстр, который со временем звереет (см. BrMonster.cs, BrCam.cs). Выход есть всегда: дверь EXIT, F8, кнопка, таймер.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakFun
{
    internal sealed class BrCrate { public int id, n, ent, model, trap; public Vector3 pos; public float up; public GameObject go, glow; public bool used; public float spark; }
    internal sealed class BrLight { public Light l; public int lamp = -1; public float k; }

    public partial class PeakFunPlugin
    {
        const float BrC = 4f, BrH = 3.3f, BrT = 0.35f;
        readonly List<BrCrate> brCrates = new List<BrCrate>(); int brCrateSeq;
        GameObject brRoot, brHum, brHole; bool brInside, brBusy, brEntity = true, brCamFx = true; int brSize = 40; float brTimeout = 900f, brAngerSecs = 420f, brFirstAppear = 45f;
        ConfigEntry<float> cfgBrHeight; ConfigEntry<bool> cfgAutoCases;
        Vector3 brOrigin, brBase, brExitPos; readonly List<Vector3> brExitList = new List<Vector3>(); readonly List<Vector2Int> brExits = new List<Vector2Int>(); readonly List<KeyValuePair<Collider, Collider>> brIgn = new List<KeyValuePair<Collider, Collider>>(); readonly HashSet<int> brIgnSeen = new HashSet<int>(); float brIgnNext; string brIgnInfo = ""; int brN, brSeed; bool[,] brWV, brWH; int brExitX, brExitY, brSpawnX, brSpawnY;
        float brFade, brEnterT, brFlash; List<Renderer> brFlick = new List<Renderer>(); float brFlickNext;
        static Texture2D brVig;
        bool brArrived; float brArriveDeadline; int brWarpTries; Vector3 brSpawn; string brLastDiag = "";
        bool brFogSaved, brFogWas; Color brFogCol; FogMode brFogMode; float brFogDen;
        readonly List<Vector3> brLampPos = new List<Vector3>(); readonly List<Renderer> brLampRend = new List<Renderer>();
        readonly List<BrLight> brLights = new List<BrLight>(); Light brCamLight; float brLightMul = 1f, brLightScan; int brLayerC = -1;
        bool brEnvOn; UnityEngine.Rendering.AmbientMode brAmbMode; Color brAmbCol; float brAmbInt; readonly List<KeyValuePair<Light, float>> brSuns = new List<KeyValuePair<Light, float>>();
        readonly Dictionary<string, object> brUrpOld = new Dictionary<string, object>(); string brUrpInfo = "—"; bool brLock;

        void BrDiag(string m) { brLastDiag = m; TAddLog(DateTime.Now.ToString("HH:mm:ss") + " [BR] " + m); Logger.LogInfo("[PeakFun][Backrooms] " + m); }
        string BF(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

        void InitBackrooms()
        {
            CamShake.ExtraActive = () => brCatchCam; CamShake.ExtraApply = tr => { tr.position = brCamP; tr.rotation = brCamQ; };
            cfgAutoCases = Config.Bind("Backrooms", "AutoCases", true, "Сами ставить по одной приманке-ловушке в каждом биоме (внизу, на ровном месте).");
            cfgBrHeight = Config.Bind("Backrooms", "HeightM", 200f, "На сколько метров над чемоданом строятся Backrooms. Если сразу «убивает» — уменьши (например 120).");
        }

        // ======================= приманка =======================
        void SpawnCrate(int dist)
        {
            var me = Game.Me(); var cam = Camera.main; if (me == null || cam == null) { Ui.Toast("Нет персонажа", Ui.Bad); return; }
            Vector3 o = cam.transform.position, f = FlatForward(); Vector3 p = CenterOf(me) + f * dist; RaycastHit rh;
            if (Physics.Raycast(o, cam.transform.forward, out rh, dist + 2f, ~0, QueryTriggerInteraction.Ignore) && rh.distance < dist) p = rh.point;
            p.y = GroundY(p + Vector3.up * 2f, me.transform.position.y - 0.9f);
            int id = Game.LocalActor() * 1000 + (++brCrateSeq) + UnityEngine.Random.Range(0, 99999) * 7;
            var rr = new System.Random(id); int model = brPickModel >= 0 ? brPickModel : rr.Next(4), trap = brPickTrap >= 0 ? brPickTrap : PickTrap(rr);
            MakeCrate(id, p, cfgBrHeight.Value, brSize, brEntity ? 1 : 0, model, trap);
            if (Game.InRoom) Net.Send("br", "c;" + id + "," + BF(p.x) + "," + BF(p.y) + "," + BF(p.z) + "," + BF(cfgBrHeight.Value) + "," + brSize + "," + (brEntity ? 1 : 0) + "," + model + "," + trap);
            Ui.Toast("Приманка поставлена: «" + BaitNames[model] + "», ловушка: «" + TrapNames[trap] + "» (у друзей с PeakFun тоже)", Ui.Good);
        }

        GameObject BPrim(PrimitiveType t, Transform parent, Vector3 lp, Vector3 ls, Color col, bool keepCol = false, string name = "PF_br_player_part")
        {
            var g = GameObject.CreatePrimitive(t); g.name = name; g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = ls;
            var mr = g.GetComponent<MeshRenderer>(); var m = BrGfx.Surf(null, col, 0.2f); if (m != null) mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!keepCol) { var cl = g.GetComponent<Collider>(); if (cl != null) UnityEngine.Object.DestroyImmediate(cl); }
            return g;
        }

        void RemoveCrates()
        {
            foreach (var c in brCrates) { if (c.go != null) UnityEngine.Object.Destroy(c.go); if (c.glow != null) UnityEngine.Object.Destroy(c.glow); }
            brCrates.Clear(); if (brHole != null) UnityEngine.Object.Destroy(brHole); brHole = null;
        }

        bool BrExitReached(Vector3 pc)
        {
            if (brExitList.Count == 0) brExitList.Add(brExitPos);
            foreach (var e in brExitList)
                if (Vector3.Distance(new Vector3(pc.x, 0, pc.z), new Vector3(e.x, 0, e.z)) < 2.1f && Mathf.Abs(pc.y - e.y - 1f) < 3.5f) return true;
            return false;
        }

        Vector3 BrNearestExit(Vector3 pc)
        {
            Vector3 b = brExitPos; float bd = 1e9f;
            foreach (var e in brExitList) { float d = (e - pc).sqrMagnitude; if (d < bd) { bd = d; b = e; } }
            return b;
        }

        // Направление, где у клетки есть стена (для двери). Vector3.zero — стен нет.
        Vector3 BrWallDir(int x, int y)
        {
            if (!BrPass(x, y, 0)) return Vector3.right; if (!BrPass(x, y, 1)) return Vector3.left; if (!BrPass(x, y, 2)) return Vector3.forward; if (!BrPass(x, y, 3)) return Vector3.back; return Vector3.zero;
        }

        // ---- «невидимые барьеры»: игра ставит стены между биомами/сегментами (с туманом) — они пересекают уровень на высоте. Игнорируем чужие коллайдеры ----
        void BrIgnoreForeign(Component me, Vector3 center, Vector3 half, bool log)
        {
            if (me == null) return;
            try
            {
                var mine = me.GetComponentsInChildren<Collider>(); var cols = Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                int added = 0; var names = new List<string>(); var others = new HashSet<Transform>(); foreach (var o in Game.Others()) if (o != null) others.Add(o.transform.root);
                foreach (var c in cols)
                {
                    if (c == null || c.isTrigger) continue; string nm = c.name; if (nm.StartsWith("PF_") || (c.transform.root != null && c.transform.root.name.StartsWith("PF_"))) continue;
                    if (c.transform.IsChildOf(me.transform) || c.transform.root == me.transform.root) continue; if (others.Contains(c.transform.root)) continue;
                    int id = c.GetInstanceID(); if (brIgnSeen.Contains(id)) continue; brIgnSeen.Add(id);
                    foreach (var m in mine) { if (m == null || m.isTrigger) continue; try { Physics.IgnoreCollision(m, c, true); brIgn.Add(new KeyValuePair<Collider, Collider>(m, c)); } catch { } }
                    added++; if (names.Count < 5) names.Add(nm + "(" + c.gameObject.layer + ")");
                }
                if (added > 0) { brIgnInfo = "отключены столкновения с " + added + " чужими коллайдерами: " + string.Join(", ", names.ToArray()); if (log) BrDiag(brIgnInfo); }
            }
            catch (Exception e) { SafeLog("br-ignore", e); }
        }

        void BrRestoreIgnored()
        {
            foreach (var kv in brIgn) { try { if (kv.Key != null && kv.Value != null) Physics.IgnoreCollision(kv.Key, kv.Value, false); } catch { } }
            brIgn.Clear(); brIgnSeen.Clear();
        }

        // Свободный объём над чемоданом: поднимаем уровень, пока он не перестанет пересекать горы/стены игры (везде детерминированно)
        Vector3 BrPickBase(Vector3 baseP, int n)
        {
            try
            {
                float W = n * BrC; Vector3 best = baseP; int bestCnt = int.MaxValue;
                foreach (float add in new[] { 0f, 90f, 200f, 350f })
                {
                    Vector3 b = baseP + Vector3.up * add; var cols = Physics.OverlapBox(b + new Vector3(W * 0.5f, BrH * 0.5f, W * 0.5f), new Vector3(W * 0.5f + 8f, BrH * 0.5f + 8f, W * 0.5f + 8f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    int cnt = 0; foreach (var c in cols) if (c != null && !c.name.StartsWith("PF_")) cnt++;
                    if (cnt < bestCnt) { bestCnt = cnt; best = b; }
                    if (cnt == 0) break;
                }
                if (best != baseP) BrDiag("над чемоданом занято (" + bestCnt + " объектов игры) — уровень поднят на " + (best.y - baseP.y).ToString("0") + " м");
                return best;
            }
            catch { return baseP; }
        }

        void TickBackrooms()
        {
            var me = Game.Me();
            for (int i = brCrates.Count - 1; i >= 0; i--)
            {
                var c = brCrates[i];
                if (c.go == null) { brCrates.RemoveAt(i); continue; }
                if (c.used) continue;
                if (c.glow != null) { c.glow.transform.position = c.pos + Vector3.up * (3.2f + 0.15f * Mathf.Sin(Time.time * 2f)); }
                if (Time.time > c.spark) { c.spark = Time.time + 0.4f; Vfx.Sparks(c.pos + Vector3.up * 0.9f, Vector3.up, 2, new Color(1f, 0.85f, 0.3f)); }
                if (me != null && !brInside && !brBusy && !Game.IsDead(me))
                {
                    Vector3 pc = CenterOf(me); Vector3 d = pc - c.pos; float dy = Mathf.Abs(d.y); d.y = 0f;
                    if (d.magnitude < 1.8f && dy < 2.6f) { c.used = true; StartCoroutine(CoTrap(c)); }
                }
            }
            if (brFade > 0f && !brBusy && !brInside && !brCatching) brFade = Mathf.MoveTowards(brFade, 0f, Time.deltaTime * 2f);
            brFlash = Mathf.MoveTowards(brFlash, 0f, Time.deltaTime * 2.5f);
            BrTickFx(Time.deltaTime);
            if (!brInside) return;
            if (me == null) return;
            float now = Time.time;
            Vector3 pc2 = CenterOf(me);
            BrTickLights(pc2);
            if (now > brIgnNext) { brIgnNext = now + 1.0f; BrIgnoreForeign(me, pc2, new Vector3(16f, 9f, 16f), true); }
            if (!brArrived)
            {
                Vector3 l = pc2 - brBase; bool inside = l.x > -2f && l.z > -2f && l.x < brN * BrC + 2f && l.z < brN * BrC + 2f && l.y > -3f && l.y < BrH + 3f;
                if (inside)
                {
                    brArrived = true; brEnterT = now; brMonNext = now + brFirstAppear;
                    RaycastHit gh; bool ok = Physics.Raycast(pc2, Vector3.down, out gh, 6f, ~0, QueryTriggerInteraction.Ignore);
                    BrDiag("прибыл в Backrooms: " + pc2 + (ok ? "; под ногами «" + gh.collider.name + "», слой " + gh.collider.gameObject.layer + ", " + gh.distance.ToString("0.0") + " м" : "; ПОД НОГАМИ НИЧЕГО (ray 6 м)"));
                    return;
                }
                if (now > brArriveDeadline)
                {
                    if (brWarpTries < 2)
                    {
                        brWarpTries++; brArriveDeadline = now + 3f; BrDiag("телепорт не сработал, позиция " + pc2 + ", цель " + brSpawn + " — повторяю (" + brWarpTries + ")");
                        Rpc(me, "WarpPlayerRPC", brSpawn, false); Game.HaltBody(me);
                    }
                    else { BrDiag("телепорт не удался: позиция " + pc2 + ", цель " + brSpawn); StartCoroutine(CoBrExit("Не вышло телепортироваться в Backrooms (позиция " + pc2.ToString("0") + "). Попробуй меньше Height в cfg")); }
                }
                return;
            }
            float inT = now - brEnterT;
            if (now > brFlickNext && brFlick.Count > 0)
            {
                brFlickNext = now + UnityEngine.Random.Range(0.05f, 0.35f);
                var r = brFlick[UnityEngine.Random.Range(0, brFlick.Count)]; if (r != null) r.enabled = UnityEngine.Random.value > 0.35f;
            }
            if (brCatching) return;
            if (Game.IsDead(me)) { BrDiag("игрок мёртв"); StartCoroutine(CoBrExit("Ты погиб — возврат наверх")); return; }
            Vector3 lp2 = pc2 - brBase;
            if (lp2.y < -6f || lp2.y > BrH + 20f || lp2.x < -12f || lp2.z < -12f || lp2.x > brN * BrC + 12f || lp2.z > brN * BrC + 12f) { BrDiag("вылет за пределы: " + pc2 + " (локально " + lp2 + ")"); StartCoroutine(CoBrExit("Ты вылетел за стены — возврат наверх")); return; }
            if (brTimeout > 0f && inT > brTimeout) { StartCoroutine(CoBrExit("Тебя вытолкнуло обратно (время вышло)")); return; }
            if (BrExitReached(pc2)) { StartCoroutine(CoBrExit("Ты нашёл выход!")); return; }
            TickBrMonster(me, pc2, inT);
        }

        // ======================= падение =======================
        IEnumerator CoBrFall(BrCrate c)
        {
            brBusy = true; var me = Game.Me();
            Sfx2.Clank(0.7f); HeroToast("Чемодан… трещит?!");
            if (brHole != null) UnityEngine.Object.Destroy(brHole);
            brHole = new GameObject("PF_crate_player_hole"); var mf = brHole.AddComponent<MeshFilter>(); mf.sharedMesh = Vfx.Quad(); var mr = brHole.AddComponent<MeshRenderer>(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var hm = Vfx.Mat(Vfx.Blob(), new Color(0f, 0f, 0f, 0.96f)); if (hm != null) mr.sharedMaterial = hm;
            brHole.transform.position = c.pos + Vector3.up * 0.05f; brHole.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            float t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime; float k = t / 0.9f;
                if (c.go != null) { c.go.transform.position = c.pos + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)) * 0.05f * (1f + k * 3f) - Vector3.up * 0.5f * k * k; }
                brHole.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 4.2f, k);
                CamShake.Boost = Mathf.Max(CamShake.Boost, 0.25f + 0.5f * k);
                if (c.glow != null) c.glow.SetActive(false);
                yield return null;
            }
            if (c.go != null) c.go.SetActive(false);
            Sfx2.Whoosh(); Sfx2.Boom();
            float f = 0f; while (f < 1f) { f += Time.deltaTime * 2.6f; brFade = Mathf.Clamp01(f); yield return null; }
            brBusy = false;
            EnterBackrooms(c.id * 7919 + 13, c.pos + Vector3.up * c.up, c.n, c.ent == 1);
            yield return new WaitForSeconds(0.2f);
            if (brHole != null) { UnityEngine.Object.Destroy(brHole, 30f); }
        }

        void ForceEnter(int seed, Vector3 baseP, int n, bool ent)
        {
            if (brInside || brBusy) return; StartCoroutine(CoBrForce(seed, baseP, n, ent));
        }

        IEnumerator CoBrForce(int seed, Vector3 baseP, int n, bool ent)
        {
            brBusy = true; HeroToast("Пол исчез…"); CamShake.Boost = 1f; Sfx2.Whoosh();
            float f = 0f; while (f < 1f) { f += Time.deltaTime * 2.2f; brFade = Mathf.Clamp01(f); yield return null; }
            brBusy = false; EnterBackrooms(seed, baseP, n, ent);
        }

        // ======================= генерация =======================
        void BrGenerate(int seed, int n)
        {
            var rnd = new System.Random(seed); brN = n; brWV = new bool[n + 1, n]; brWH = new bool[n, n + 1];
            for (int x = 0; x <= n; x++) for (int y = 0; y < n; y++) brWV[x, y] = true;
            for (int x = 0; x < n; x++) for (int y = 0; y <= n; y++) brWH[x, y] = true;
            var seen = new bool[n, n]; var st = new Stack<Vector2Int>(); st.Push(new Vector2Int(0, 0)); seen[0, 0] = true;
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            while (st.Count > 0)
            {
                var c = st.Peek(); var opts = new List<int>();
                for (int d = 0; d < 4; d++) { int nx = c.x + dx[d], ny = c.y + dy[d]; if (nx >= 0 && ny >= 0 && nx < n && ny < n && !seen[nx, ny]) opts.Add(d); }
                if (opts.Count == 0) { st.Pop(); continue; }
                int di = opts[rnd.Next(opts.Count)]; int ax = c.x + dx[di], ay = c.y + dy[di]; BrOpen(c.x, c.y, ax, ay); seen[ax, ay] = true; st.Push(new Vector2Int(ax, ay));
            }
            for (int x = 1; x < n; x++) for (int y = 0; y < n; y++) if (brWV[x, y] && rnd.NextDouble() < 0.22) brWV[x, y] = false;
            for (int x = 0; x < n; x++) for (int y = 1; y < n; y++) if (brWH[x, y] && rnd.NextDouble() < 0.22) brWH[x, y] = false;
            // залы
            int rooms = 3 + n / 4;
            for (int r = 0; r < rooms; r++)
            {
                int w = 3 + rnd.Next(5), h = 3 + rnd.Next(4), x0 = rnd.Next(0, Mathf.Max(1, n - w)), y0 = rnd.Next(0, Mathf.Max(1, n - h));
                for (int x = x0; x < x0 + w && x < n; x++) for (int y = y0; y < y0 + h && y < n; y++) { if (x > x0) brWV[x, y] = false; if (y > y0) brWH[x, y] = false; }
            }
            brSpawnX = n / 2; brSpawnY = n / 2;
            for (int x = brSpawnX - 1; x <= brSpawnX + 1; x++) for (int y = brSpawnY - 1; y <= brSpawnY + 1; y++) { if (x > brSpawnX - 1 && x < n) brWV[x, y] = false; if (y > brSpawnY - 1 && y < n) brWH[x, y] = false; }
            var dist = BrBfs(brSpawnX, brSpawnY); int best = -1; brExitX = 0; brExitY = 0;
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) if (dist[x, y] > best && BrWallDirCell(x, y)) { best = dist[x, y]; brExitX = x; brExitY = y; }
            brExits.Clear(); brExits.Add(new Vector2Int(brExitX, brExitY));
            // ещё две двери ближе — чтобы не зависеть от одной точки
            foreach (float fr in new[] { 0.55f, 0.32f })
            {
                int want = Mathf.RoundToInt(best * fr), bx = -1, by = -1, bs = int.MaxValue;
                for (int x = 0; x < n; x++) for (int y = 0; y < n; y++)
                {
                    if (dist[x, y] < 4 || !BrWallDirCell(x, y)) continue; bool far = true; foreach (var e in brExits) if (Mathf.Abs(e.x - x) + Mathf.Abs(e.y - y) < Mathf.Max(5, n / 5)) far = false; if (!far) continue;
                    int sc = Mathf.Abs(dist[x, y] - want) * 10 + rnd.Next(7); if (sc < bs) { bs = sc; bx = x; by = y; }
                }
                if (bx >= 0) brExits.Add(new Vector2Int(bx, by));
            }
        }

        bool BrWallDirCell(int x, int y) { return BrWallDir(x, y) != Vector3.zero; }

        void BrOpen(int x, int y, int ax, int ay)
        {
            if (ax > x) brWV[ax, y] = false; else if (ax < x) brWV[x, y] = false; else if (ay > y) brWH[x, ay] = false; else brWH[x, y] = false;
        }

        bool BrPass(int x, int y, int d)   // 0 вправо, 1 влево, 2 вверх, 3 вниз
        {
            switch (d)
            {
                case 0: return x + 1 < brN && !brWV[x + 1, y];
                case 1: return x - 1 >= 0 && !brWV[x, y];
                case 2: return y + 1 < brN && !brWH[x, y + 1];
                default: return y - 1 >= 0 && !brWH[x, y];
            }
        }

        int[,] BrBfs(int sx, int sy)
        {
            var d = new int[brN, brN]; for (int x = 0; x < brN; x++) for (int y = 0; y < brN; y++) d[x, y] = -1;
            var q = new Queue<Vector2Int>(); q.Enqueue(new Vector2Int(sx, sy)); d[sx, sy] = 0; int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            while (q.Count > 0) { var c = q.Dequeue(); for (int k = 0; k < 4; k++) if (BrPass(c.x, c.y, k)) { int nx = c.x + dx[k], ny = c.y + dy[k]; if (d[nx, ny] < 0) { d[nx, ny] = d[c.x, c.y] + 1; q.Enqueue(new Vector2Int(nx, ny)); } } }
            return d;
        }

        int BrLayer()
        {
            if (brLayerC >= 0) return brLayerC;
            try
            {
                foreach (var nm in new[] { "Terrain", "Map", "Ground" }) { int l = LayerMask.NameToLayer(nm); if (l >= 0) return brLayerC = l; }
                int m = TerrainMask(); for (int i = 1; i < 32; i++) if ((m & (1 << i)) != 0) return brLayerC = i;
            }
            catch { }
            return brLayerC = 0;
        }

        int BrZone(int x, int y)
        {
            float p = Mathf.PerlinNoise(Mathf.Clamp(x, 0, brN) * 0.09f + (brSeed % 97) * 0.37f, Mathf.Clamp(y, 0, brN) * 0.09f + 3.3f);
            return p < 0.34f ? 1 : (p > 0.68f ? 2 : 0);
        }
        static readonly Color[] ZoneTint = { new Color(1f, 1f, 1f), new Color(0.88f, 0.98f, 0.76f), new Color(0.82f, 0.68f, 0.56f) };

        GameObject BrMeshObj(Transform par, string name, MeshB b, Material m)
        {
            if (b.Count == 0) return null;
            var go = new GameObject(name); go.transform.SetParent(par, false); go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh(name); var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = BrGfx.LitOk ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = BrGfx.LitOk; if (m != null) mr.sharedMaterial = m; return go;
        }

        BoxCollider BrCol(Transform T, string name, Vector3 c, Vector3 size, int layer)
        {
            var g = new GameObject(name); g.transform.SetParent(T, false); g.transform.localPosition = c; g.layer = layer; var bc = g.AddComponent<BoxCollider>(); bc.size = size; return bc;
        }

        void BrBuild(Vector3 baseP)
        {
            int n = brN, layer = BrLayer();
            brRoot = new GameObject("PF_backrooms_player"); brRoot.transform.position = baseP; var T = brRoot.transform;
            var rnd = new System.Random(brSeed ^ 0x5bd1);
            var walls = new MeshB[3]; var floors = new MeshB[3]; var ceils = new MeshB[3]; for (int i = 0; i < 3; i++) { walls[i] = new MeshB(); floors[i] = new MeshB(); ceils[i] = new MeshB(); }
            var skirt = new MeshB(); var pil = new MeshB(); var props = new MeshB(); var outl = new MeshB(); var lampB = new MeshB(); var frameB = new MeshB();
            const float tile = 4f;
            // ---- стены сплошными отрезками (меньше мешей и коллайдеров) ----
            for (int x = 0; x <= n; x++)
            {
                int y = 0;
                while (y < n)
                {
                    if (!brWV[x, y]) { y++; continue; }
                    int z = BrZone(x, y), y0 = y; while (y < n && brWV[x, y] && BrZone(x, y) == z) y++;
                    Vector3 c = new Vector3(x * BrC, BrH * 0.5f, (y0 + y) * 0.5f * BrC), sz = new Vector3(BrT, BrH, (y - y0) * BrC + BrT);
                    walls[z].WallBox(c, sz, tile, BrH); skirt.WallBox(new Vector3(c.x, 0.11f, c.z), new Vector3(sz.x + 0.07f, 0.22f, sz.z + 0.02f), 4f, 0.22f); BrCol(T, "PF_br_player_wall", c, sz, layer);
                    for (int k = y0; k < y; k++)
                    {
                        float sg = rnd.NextDouble() < 0.5 ? -1f : 1f; float zc = (k + 0.5f) * BrC;
                        if (rnd.NextDouble() < 0.12) outl.Box(new Vector3(x * BrC + sg * (BrT * 0.5f + 0.02f), 0.32f, zc + ((float)rnd.NextDouble() - 0.5f) * 2.6f), new Vector3(0.04f, 0.13f, 0.09f));
                        if (rnd.NextDouble() < 0.06) props.Box(new Vector3(x * BrC + sg * (BrT * 0.5f + 0.015f), 2.55f, zc + ((float)rnd.NextDouble() - 0.5f) * 2.2f), new Vector3(0.03f, 0.4f, 0.8f));
                    }
                }
            }
            for (int y = 0; y <= n; y++)
            {
                int x = 0;
                while (x < n)
                {
                    if (!brWH[x, y]) { x++; continue; }
                    int z = BrZone(x, y), x0 = x; while (x < n && brWH[x, y] && BrZone(x, y) == z) x++;
                    Vector3 c = new Vector3((x0 + x) * 0.5f * BrC, BrH * 0.5f, y * BrC), sz = new Vector3((x - x0) * BrC + BrT, BrH, BrT);
                    walls[z].WallBox(c, sz, tile, BrH); skirt.WallBox(new Vector3(c.x, 0.11f, c.z), new Vector3(sz.x + 0.02f, 0.22f, sz.z + 0.07f), 4f, 0.22f); BrCol(T, "PF_br_player_wall", c, sz, layer);
                    for (int k = x0; k < x; k++)
                    {
                        float sg = rnd.NextDouble() < 0.5 ? -1f : 1f; float xc = (k + 0.5f) * BrC;
                        if (rnd.NextDouble() < 0.12) outl.Box(new Vector3(xc + ((float)rnd.NextDouble() - 0.5f) * 2.6f, 0.32f, y * BrC + sg * (BrT * 0.5f + 0.02f)), new Vector3(0.09f, 0.13f, 0.04f));
                        if (rnd.NextDouble() < 0.06) props.Box(new Vector3(xc + ((float)rnd.NextDouble() - 0.5f) * 2.2f, 2.55f, y * BrC + sg * (BrT * 0.5f + 0.015f)), new Vector3(0.8f, 0.4f, 0.03f));
                    }
                }
            }
            // ---- пол, потолок, колонны, лампы ----
            brLampPos.Clear(); brLampRend.Clear(); brFlick.Clear(); var flickGO = new List<Vector3>(); var flickIdx = new List<int>();
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++)
            {
                int z = BrZone(x, y); float x0 = x * BrC, z0 = y * BrC;
                floors[z].FloorQuad(x0, z0, x0 + BrC, z0 + BrC, 0f, true); ceils[z].FloorQuad(x0, z0, x0 + BrC, z0 + BrC, BrH, false);
                Vector3 cc = new Vector3(x0 + BrC * 0.5f, 0f, z0 + BrC * 0.5f);
                bool open = BrPass(x, y, 0) && BrPass(x, y, 1) && BrPass(x, y, 2) && BrPass(x, y, 3);
                if (open && (x + y) % 2 == 0 && rnd.NextDouble() < 0.7)
                {
                    Vector3 pc = cc + Vector3.up * BrH * 0.5f; pil.WallBox(pc, new Vector3(0.9f, BrH, 0.9f), tile, BrH); skirt.WallBox(cc + Vector3.up * 0.11f, new Vector3(0.97f, 0.22f, 0.97f), 4f, 0.22f); BrCol(T, "PF_br_player_pillar", pc, new Vector3(0.9f, BrH, 0.9f), layer);
                }
                if ((x + y) % 2 == 0)
                {
                    Vector3 lc = new Vector3(x0 + BrC * 0.5f, BrH - 0.03f, z0 + BrC * 0.5f); frameB.Box(lc + Vector3.up * 0.012f, new Vector3(2.0f, 0.05f, 0.88f));
                    bool fl = rnd.NextDouble() < 0.12; brLampPos.Add(baseP + lc - Vector3.up * 0.2f);
                    if (fl) { flickGO.Add(lc - Vector3.up * 0.012f); flickIdx.Add(brLampPos.Count - 1); brLampRend.Add(null); } else { lampB.Box(lc - Vector3.up * 0.012f, new Vector3(1.8f, 0.05f, 0.7f)); brLampRend.Add(null); }
                }
            }
            var fg = BrCol(T, "PF_br_player_floorcol", new Vector3(n * BrC * 0.5f, -0.5f, n * BrC * 0.5f), new Vector3(n * BrC + 6f, 1f, n * BrC + 6f), layer);
            var cg = BrCol(T, "PF_br_player_ceilcol", new Vector3(n * BrC * 0.5f, BrH + 0.5f, n * BrC * 0.5f), new Vector3(n * BrC + 6f, 1f, n * BrC + 6f), layer);
            // ---- материалы ----
            var tw = BrWallTex(); var tc = BrCarpetTex(); var tt = BrCeilTex();
            for (int z = 0; z < 3; z++)
            {
                BrMeshObj(T, "PF_br_player_walls" + z, walls[z], BrGfx.Surf(tw, ZoneTint[z], 0.08f));
                BrMeshObj(T, "PF_br_player_floor" + z, floors[z], BrGfx.Surf(tc, ZoneTint[z] * new Color(0.95f, 0.95f, 0.95f, 1f), 0.05f));
                BrMeshObj(T, "PF_br_player_ceil" + z, ceils[z], BrGfx.Surf(tt, ZoneTint[z], 0.1f));
            }
            BrMeshObj(T, "PF_br_player_pillars", pil, BrGfx.Surf(tw, new Color(0.93f, 0.93f, 0.9f), 0.08f));
            BrMeshObj(T, "PF_br_player_skirt", skirt, BrGfx.Surf(null, new Color(0.33f, 0.27f, 0.12f), 0.2f));
            BrMeshObj(T, "PF_br_player_outlets", outl, BrGfx.Surf(null, new Color(0.88f, 0.84f, 0.7f), 0.3f));
            BrMeshObj(T, "PF_br_player_vents", props, BrGfx.Surf(null, new Color(0.24f, 0.22f, 0.19f), 0.3f, 0.4f));
            BrMeshObj(T, "PF_br_player_lampframes", frameB, BrGfx.Surf(null, new Color(0.55f, 0.53f, 0.45f), 0.3f, 0.3f));
            var lampMat = BrGfx.Surf(null, new Color(0.95f, 0.92f, 0.78f), 0.2f, 0f, new Color(2.6f, 2.45f, 1.8f));
            var lb = BrMeshObj(T, "PF_br_player_lamps", lampB, lampMat); if (lb != null) { var r = lb.GetComponent<MeshRenderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            // мигающие лампы — отдельные объекты
            for (int fi = 0; fi < flickGO.Count; fi++)
            {
                var l = GameObject.CreatePrimitive(PrimitiveType.Cube); l.name = "PF_br_player_lamp"; UnityEngine.Object.Destroy(l.GetComponent<Collider>()); l.transform.SetParent(T, false);
                l.transform.localPosition = flickGO[fi]; l.transform.localScale = new Vector3(1.8f, 0.05f, 0.7f); var mr = l.GetComponent<MeshRenderer>(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; mr.sharedMaterial = lampMat;
                brFlick.Add(mr); brLampRend[flickIdx[fi]] = mr;
            }
            // ---- двери выхода (их несколько) ----
            brExitList.Clear();
            foreach (var ex in brExits) brExitList.Add(baseP + BrBuildDoor(T, ex.x, ex.y));
            brExitPos = brExitList.Count > 0 ? brExitList[0] : baseP;
            BrLightsInit(T);
        }

        Vector3 BrBuildDoor(Transform T, int ex, int ey)
        {
            Vector3 ec = new Vector3((ex + 0.5f) * BrC, 0f, (ey + 0.5f) * BrC); Vector3 dir = BrWallDir(ex, ey);
            Vector3 dp = ec + dir * (BrC * 0.5f - BrT * 0.5f - 0.1f); Quaternion dr = dir == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(-dir);
            var frame = BPrim(PrimitiveType.Cube, T, dp + Vector3.up * 1.4f, new Vector3(1.8f, 2.8f, 0.1f), new Color(0.07f, 0.2f, 0.11f), false, "PF_br_player_exit"); frame.transform.localRotation = dr;
            var doorMat = BrGfx.Surf(null, new Color(0.25f, 1f, 0.5f), 0.2f, 0f, new Color(0.35f, 2.2f, 0.8f));
            var door = BPrim(PrimitiveType.Cube, T, dp + Vector3.up * 1.3f + dr * Vector3.forward * -0.04f, new Vector3(1.5f, 2.6f, 0.1f), Color.white, false, "PF_br_player_exit"); door.transform.localRotation = dr; door.GetComponent<MeshRenderer>().sharedMaterial = doorMat;
            var sign = BPrim(PrimitiveType.Cube, T, dp + Vector3.up * 2.95f + dr * Vector3.forward * -0.04f, new Vector3(1.3f, 0.22f, 0.1f), Color.white, false, "PF_br_player_exit"); sign.transform.localRotation = dr; sign.GetComponent<MeshRenderer>().sharedMaterial = doorMat;
            BPrim(PrimitiveType.Sphere, T, dp + Vector3.up * 1.25f + dr * Vector3.forward * -0.14f + dr * Vector3.right * 0.5f, new Vector3(0.12f, 0.12f, 0.12f), new Color(0.9f, 0.9f, 0.9f), false, "PF_br_player_exit");
            var dl = new GameObject("PF_br_player_exitlight"); dl.transform.SetParent(T, false); dl.transform.localPosition = dp + Vector3.up * 1.6f + dir * -0.5f; var dlight = dl.AddComponent<Light>(); dlight.type = LightType.Point; dlight.color = new Color(0.3f, 1f, 0.55f); dlight.range = 9f; dlight.intensity = 2.2f; dlight.shadows = LightShadows.None;
            return dp;
        }

        static Texture2D brTexW, brTexC, brTexT;
        static Texture2D BrWallTex()
        {
            if (brTexW == null) brTexW = BrGfx.MakeTex(512, 256, true, (u, v) =>
            {
                float uu = u * 4f; float fu = Mathf.Repeat(uu * 4f, 1f);       // 16 полос на 4 м
                float stripe = fu < 0.42f ? 1f : (fu < 0.5f ? 0.88f : 0.95f);
                float dxm = Mathf.Abs(Mathf.Repeat(uu * 2f, 1f) - 0.5f), dym = Mathf.Abs(Mathf.Repeat(v * 3f, 1f) - 0.5f);
                float damask = (dxm * 1.6f + dym) < 0.22f && fu > 0.5f ? 0.92f : 1f;
                float stain = Mathf.PerlinNoise(u * 6f + 3.1f, v * 2.5f);
                float st = (v > 0.5f && stain > 0.62f) ? Mathf.Lerp(1f, 0.7f, Mathf.Clamp01((stain - 0.62f) * 6f) * Mathf.Clamp01((v - 0.5f) * 3f)) : 1f;
                float dirt = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(v * 4.5f)) * Mathf.Lerp(1f, 0.8f, Mathf.Clamp01((v - 0.9f) * 9f));
                float fib = 0.96f + 0.06f * Mathf.PerlinNoise(u * 300f, v * 150f);
                float s = stripe * damask * st * dirt * fib;
                return new Color(0.84f * s, 0.76f * s, 0.38f * s, 1f);
            });
            return brTexW;
        }
        static Texture2D BrCarpetTex()
        {
            if (brTexC == null) brTexC = BrGfx.MakeTex(256, 256, true, (u, v) =>
            {
                int ix = (int)(u * 256f), iy = (int)(v * 256f); float nz = (((ix * 73856093) ^ (iy * 19349663)) & 255) / 255f - 0.5f;
                float wob = 0.93f + 0.07f * Mathf.Sin(u * 400f) * Mathf.Sin(v * 400f); float stain = Mathf.PerlinNoise(u * 4f + 9f, v * 4f) > 0.72f ? 0.8f : 1f;
                float s = (0.8f + nz * 0.18f) * wob * stain; return new Color(0.6f * s, 0.52f * s, 0.27f * s, 1f);
            });
            return brTexC;
        }
        static Texture2D BrCeilTex()
        {
            if (brTexT == null) brTexT = BrGfx.MakeTex(256, 256, true, (u, v) =>
            {
                float fu = Mathf.Repeat(u * 2f, 1f), fv = Mathf.Repeat(v * 2f, 1f); float e = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv));
                float s = e < 0.025f ? 0.42f : 0.9f; int ix = (int)(u * 256f), iy = (int)(v * 256f); s += ((((ix * 83492791) ^ (iy * 2654435)) & 255) / 255f - 0.5f) * 0.07f;
                if (e >= 0.025f && Mathf.PerlinNoise(u * 7f, v * 7f) > 0.8f) s *= 0.86f;
                return new Color(0.88f * s, 0.86f * s, 0.72f * s, 1f);
            });
            return brTexT;
        }

        // ======================= освещение и окружение =======================
        void BrLightsInit(Transform T)
        {
            brLights.Clear();
            for (int i = 0; i < 10; i++)
            {
                var g = new GameObject("PF_br_player_light"); g.transform.SetParent(T, false); var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.94f, 0.76f); l.range = 14f; l.intensity = 0f;
                l.shadows = i < 4 ? LightShadows.Soft : LightShadows.None; l.shadowStrength = 0.92f; l.shadowBias = 0.04f; l.shadowNearPlane = 0.2f; brLights.Add(new BrLight { l = l });
            }
            var cg = new GameObject("PF_br_player_camlight"); cg.transform.SetParent(T, false); brCamLight = cg.AddComponent<Light>(); brCamLight.type = LightType.Point; brCamLight.color = new Color(1f, 0.96f, 0.85f); brCamLight.range = 6.5f; brCamLight.intensity = 0.55f; brCamLight.shadows = LightShadows.None;
        }

        readonly int[] brNear = new int[10];
        void BrTickLights(Vector3 pc)
        {
            if (brLights.Count == 0) return; float dt = Time.deltaTime; var cam = Camera.main; if (brCamLight != null) { if (brCatchCam) { brCamLight.transform.position = brCamP + brCamQ * Vector3.forward * 0.25f; } else if (cam != null) brCamLight.transform.position = cam.transform.position + cam.transform.forward * 0.25f; brCamLight.intensity = 0.55f * brCamLightK; }
            if (!brCatching) brLightMul = Mathf.MoveTowards(brLightMul, 1f, dt * 1.4f);
            if (Time.time > brLightScan)
            {
                brLightScan = Time.time + 0.2f; var bd = new float[10]; int cnt = 0;
                for (int i = 0; i < brLampPos.Count; i++)
                {
                    float d = (brLampPos[i] - pc).sqrMagnitude; if (d > 26f * 26f) continue;
                    if (cnt < 10) { brNear[cnt] = i; bd[cnt] = d; cnt++; }
                    else { int w = 0; for (int k = 1; k < 10; k++) if (bd[k] > bd[w]) w = k; if (d < bd[w]) { brNear[w] = i; bd[w] = d; } }
                }
                foreach (var bl in brLights) { bool keep = false; for (int k = 0; k < cnt; k++) if (brNear[k] == bl.lamp) keep = true; if (!keep && bl.lamp >= 0) bl.lamp = -2 - bl.lamp; }   // -2-… = гаснет, потом освободится
                for (int k = 0; k < cnt; k++)
                {
                    bool has = false; foreach (var bl in brLights) if (bl.lamp == brNear[k]) has = true; if (has) continue;
                    foreach (var bl in brLights) if (bl.lamp == -1) { bl.lamp = brNear[k]; bl.k = 0f; bl.l.transform.position = brLampPos[bl.lamp]; break; }
                }
            }
            foreach (var bl in brLights)
            {
                if (bl.lamp <= -2) { bl.k = Mathf.MoveTowards(bl.k, 0f, dt * 5f); if (bl.k <= 0.01f) { bl.lamp = -1; } }
                else if (bl.lamp >= 0)
                {
                    var rr = brLampRend[bl.lamp]; float target = (rr == null || rr.enabled) ? 1f : 0.06f;
                    if (mon != null && brInside && !brCatching)
                    {
                        float md = Vector3.Distance(brLampPos[bl.lamp], mon.rt.position);
                        if (md < 13f) { float nz = Mathf.PerlinNoise(Time.time * 9f + bl.lamp * 3.7f, 1.3f); float thr = Mathf.Lerp(0.12f, 0.65f, 1f - md / 13f) * (0.45f + 0.55f * BrAnger(Time.time - brEnterT)); if (nz < thr) target = 0.03f; }
                    } bl.k = Mathf.MoveTowards(bl.k, target, dt * 6f);
                }
                bl.l.intensity = bl.lamp == -1 ? 0f : bl.k * 2.3f * brLightMul;
            }
        }

        void BrEnv(bool on)
        {
            try
            {
                if (on && !brEnvOn)
                {
                    brEnvOn = true;
                    brFogSaved = true; brFogWas = RenderSettings.fog; brFogCol = RenderSettings.fogColor; brFogMode = RenderSettings.fogMode; brFogDen = RenderSettings.fogDensity;
                    RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = BrGfx.LitOk ? 0.015f : 0.022f; RenderSettings.fogColor = new Color(0.25f, 0.21f, 0.08f);
                    brAmbMode = RenderSettings.ambientMode; brAmbCol = RenderSettings.ambientLight; brAmbInt = RenderSettings.ambientIntensity;
                    if (BrGfx.LitOk) { RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.2f, 0.17f, 0.09f); RenderSettings.ambientIntensity = 1f; }
                    brSuns.Clear(); foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>()) if (l != null && l.type == LightType.Directional && l.enabled && l.intensity > 0f) { brSuns.Add(new KeyValuePair<Light, float>(l, l.intensity)); l.intensity = 0f; }
                    BrUrp(true);
                }
                else if (!on && brEnvOn)
                {
                    brEnvOn = false;
                    if (brFogSaved) { RenderSettings.fog = brFogWas; RenderSettings.fogColor = brFogCol; RenderSettings.fogMode = brFogMode; RenderSettings.fogDensity = brFogDen; brFogSaved = false; }
                    RenderSettings.ambientMode = brAmbMode; RenderSettings.ambientLight = brAmbCol; RenderSettings.ambientIntensity = brAmbInt;
                    foreach (var kv in brSuns) if (kv.Key != null) kv.Key.intensity = kv.Value; brSuns.Clear();
                    BrUrp(false);
                }
            }
            catch (Exception e) { SafeLog("brenv", e); }
        }

        void BrUrp(bool on)
        {
            try
            {
                var gs = typeof(UnityEngine.Rendering.GraphicsSettings); object asset = null;
                foreach (var pn in new[] { "currentRenderPipeline", "renderPipelineAsset", "defaultRenderPipeline" }) { var pr = gs.GetProperty(pn, BindingFlags.Public | BindingFlags.Static); if (pr != null) { asset = pr.GetValue(null, null); if (asset != null) break; } }
                if (asset == null) { brUrpInfo = "URP-ассет не найден"; return; }
                if (!on) { foreach (var kv in brUrpOld) { var f = asset.GetType().GetField(kv.Key, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); if (f != null) f.SetValue(asset, kv.Value); } brUrpOld.Clear(); return; }
                var ok = new List<string>();
                Action<string, object> set = (name, val) =>
                {
                    var f = asset.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); if (f == null) return;
                    if (!brUrpOld.ContainsKey(name)) brUrpOld[name] = f.GetValue(asset);
                    object v = val; if (f.FieldType.IsEnum) v = Enum.ToObject(f.FieldType, val); f.SetValue(asset, v); ok.Add(name.Replace("m_", ""));
                };
                set("m_AdditionalLightsRenderingMode", 1); set("m_AdditionalLightsPerObjectLimit", 8); set("m_AdditionalLightShadowsSupported", true); set("m_AdditionalLightsShadowmapResolution", 1024);
                brUrpInfo = ok.Count == 0 ? "URP: поля не найдены (тени от ламп могут быть выключены в настройках игры)" : "URP: " + string.Join(", ", ok.ToArray());
            }
            catch (Exception e) { brUrpInfo = "URP: " + e.GetType().Name; }
        }

        // ======================= вход / выход =======================
        void EnterBackrooms(int seed, Vector3 baseP, int n, bool ent)
        {
            var me = Game.Me(); if (me == null || brInside) { brFade = 0f; return; }
            CamShake.Install(); brOrigin = me.transform.position; brSeed = seed;
            baseP = BrPickBase(baseP, n); brBase = baseP; BrRestoreIgnored(); brIgnNext = Time.time + 1f; brIgnInfo = "";
            BrGenerate(seed, n); BrEnv(true); BrBuild(baseP);
            BrIgnoreForeign(me, baseP + new Vector3(n * BrC * 0.5f, BrH * 0.5f, n * BrC * 0.5f), new Vector3(n * BrC * 0.5f + 12f, BrH * 0.5f + 40f, n * BrC * 0.5f + 12f), true);
            Vector3 sp = baseP + new Vector3((brSpawnX + 0.5f) * BrC, 1.1f, (brSpawnY + 0.5f) * BrC); brSpawn = sp;
            BrDiag("вход: seed " + seed + ", размер " + n + "×" + n + ", база " + baseP + ", из " + brOrigin + ", шейдеры: " + BrGfx.LitName + " / " + BrGfx.OpName + "; " + brUrpInfo);
            Rpc(me, "WarpPlayerRPC", sp, false); Game.HaltBody(me);
            brArrived = false; brWarpTries = 0; brArriveDeadline = Time.time + 3.5f;
            brInside = true; brEnterT = Time.time; brEntity = ent; brMonNext = Time.time + 9999f; brCatching = false; brFlickNext = Time.time + 1f; brLightMul = 1f; brAngerOverride = -1f; brSight = 0;
            BrHumStart(); if (brCamFx) { try { Shade.Set(ShadeKind.Camera, 0f); } catch { } }
            HeroToast("Ты провалился в Backrooms…");
            Ui.Toast("BACKROOMS: найди зелёную дверь EXIT. Выйти: [" + keyPanic.Value + "] или кнопка в меню" + (brTimeout > 0f ? ", либо подожди " + Mathf.RoundToInt(brTimeout / 60f) + " мин" : ""), Ui.Accent);
            StartCoroutine(CoBrFadeIn());
        }

        IEnumerator CoBrFadeIn() { yield return new WaitForSeconds(0.25f); while (brFade > 0f) { brFade = Mathf.MoveTowards(brFade, 0f, Time.deltaTime * 1.6f); yield return null; } }

        IEnumerator CoBrExit(string why)
        {
            if (!brInside || brBusy) yield break; brBusy = true; brInside = false;
            float f = brFade; while (f < 1f) { f += Time.deltaTime * 2.6f; brFade = Mathf.Clamp01(f); yield return null; }
            BrCleanup(); var me = Game.Me();
            if (me != null) { Rpc(me, "WarpPlayerRPC", brOrigin + Vector3.up * 1.2f, false); Game.HaltBody(me); }
            brBusy = false; Ui.Toast(why, Ui.Good); Sfx2.Whoosh();
            while (brFade > 0f) { brFade = Mathf.MoveTowards(brFade, 0f, Time.deltaTime * 1.5f); yield return null; }
        }

        void BrCleanup()
        {
            RemoveMon(); brCatching = false; brLock = false; BrRestoreIgnored(); brExitList.Clear();
            if (brRoot != null) UnityEngine.Object.Destroy(brRoot); brRoot = null; brLights.Clear(); brCamLight = null; brLampPos.Clear(); brLampRend.Clear();
            if (brHum != null) UnityEngine.Object.Destroy(brHum); brHum = null; brFlick.Clear(); BrEnv(false);
            try { if (Shade.Cur == ShadeKind.Camera) Shade.Off(); } catch { }
            BrAudioStop();
        }

        void BackroomsPanic() { if (brInside) StartCoroutine(CoBrExit("Выход из Backrooms (СТОП)")); }

        void BrHumStart()
        {
            if (brHum != null) return;
            brHum = new GameObject("PF_br_player_hum"); var src = brHum.AddComponent<AudioSource>(); int sr = 44100, len = sr * 2; var data = new float[len]; var rn = new System.Random(5);
            for (int i = 0; i < len; i++) { float t = i / (float)sr; data[i] = 0.14f * Mathf.Sin(2f * Mathf.PI * 120f * t) + 0.07f * Mathf.Sin(2f * Mathf.PI * 240.5f * t) + 0.02f * ((float)rn.NextDouble() - 0.5f); }
            var clip = AudioClip.Create("br_hum", len, 1, sr, false); clip.SetData(data, 0); src.clip = clip; src.loop = true; src.spatialBlend = 0f; src.volume = 0.45f; src.Play();
        }

        // ======================= сеть =======================
        void OnBrNet(string arg, int sender)
        {
            if (string.IsNullOrEmpty(arg)) return;
            int i = arg.IndexOf(';'); string op = i < 0 ? arg : arg.Substring(0, i); var n = Nums(i < 0 ? "0" : arg.Substring(i + 1));
            try
            {
                switch (op)
                {
                    case "c": if (n.Length >= 7 && brCrates.Count < 16) MakeCrate((int)n[0], new Vector3(n[1], n[2], n[3]), n[4], (int)n[5], (int)n[6], n.Length > 7 ? (int)n[7] : 0, n.Length > 8 ? (int)n[8] : 0); break;
                    case "e": if (n.Length >= 6) ForceEnter((int)n[0], new Vector3(n[1], n[2], n[3]), Mathf.Clamp((int)n[4], 7, 80), n[5] > 0.5f); break;
                    case "r": RemoveCrates(); break;
                    case "x": BackroomsPanic(); break;
                }
            }
            catch (Exception e) { SafeLog("br", e); }
        }

        void SendEveryoneDown(bool includeMe)
        {
            var me = Game.Me(); if (me == null) return;
            int seed = UnityEngine.Random.Range(1000, 99999999); Vector3 b = CenterOf(me) + Vector3.up * cfgBrHeight.Value;
            if (Game.InRoom) Net.Send("br", "e;" + seed + "," + BF(b.x) + "," + BF(b.y) + "," + BF(b.z) + "," + brSize + "," + (brEntity ? 1 : 0));
            if (includeMe) ForceEnter(seed, b, brSize, brEntity);
        }

        // ======================= интерфейс =======================
        void DrawBackrooms()
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f), W = Screen.width, H = Screen.height; var cam = Camera.main;
            if (brInside) DrawBrCam(W, H, s, cam);
            if (brFlash > 0.01f) { GUI.color = new Color(1f, 1f, 1f, brFlash); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }
            if (brFade > 0.01f) { GUI.color = new Color(0f, 0f, 0f, brFade); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }
        }

        void TabBack()
        {
            Ui.Header("Backrooms — приманки и ловушки");
            Ui.Para("Ставишь приманку: чемодан из самолёта PEAK, сундук, забытый рюкзак или аптечку. Вид предмета ничего не говорит о ловушке: внутри может быть Backrooms (самая частая), катапульта, скример, споры, сон, холод или просто пустышка. Кто подойдёт — попадётся. Приманки ещё появляются сами: по одной в каждом биоме внизу, на ровном месте (не на подъёме в гору).", 13, Ui.Muted);
            Ui.Label("Предмет:", 13, Ui.Muted);
            int pm = Ui.Buttons(new[] { "Случайный", "Чемодан", "Сундук", "Рюкзак", "Аптечка" }, 5, 11, 30f, true, brPickModel + 1, 13); if (pm >= 0) brPickModel = pm - 1;
            Ui.Label("Ловушка:", 13, Ui.Muted);
            var tl = new List<string> { "Случайная" }; tl.AddRange(TrapNames); int pt = Ui.Buttons(tl.ToArray(), 4, 12, 30f, true, brPickTrap + 1, 13); if (pt >= 0) brPickTrap = pt - 1;
            if (Ui.Button("Поставить приманку перед собой (6 м)", 1)) Safe("приманка", () => SpawnCrate(6));
            if (Ui.Button("Поставить приманку подальше (15 м)")) Safe("приманка", () => SpawnCrate(15));
            cfgAutoCases.Value = Ui.Toggle("Сами появляются: по одной в каждом биоме", cfgAutoCases.Value, "Внизу биома (рядом с началом, на ровной земле), не на подъёме. Вид и ловушка случайные.");
            if (Ui.Button("Поставить автоприманку в этом биоме сейчас")) AutoCaseNow();
            Ui.Label("Авто: " + autoInfo, 11, Ui.Muted);
            if (Ui.Button("Провалиться в Backrooms прямо сейчас", 0, !brInside && !brBusy)) Safe("backrooms", () => SendEveryoneDown(true));
            if (Ui.Button("Отправить в Backrooms всех друзей с PeakFun (не меня)", 0, Game.InRoom)) Safe("backrooms", () => SendEveryoneDown(false));
            if (Ui.Button("Выйти из Backrooms", 2, brInside)) BackroomsPanic();
            if (Ui.Button("Убрать все приманки", 2)) { RemoveCrates(); if (Game.InRoom) Net.Send("br", "r;0"); }
            Ui.Header("Настройки");
            brSize = Mathf.RoundToInt(Ui.Slider("Размер лабиринта (клеток в стороне, 1 клетка = 4 м)", brSize, 7, 80, "0", ""));
            brTimeout = Ui.Slider("Автовозврат через (0 — только выход/кнопка)", brTimeout, 0f, 1800f, "0", " с");
            brEntity = Ui.Toggle("Монстр (подглядывает, потом звереет)", brEntity, "Появляется не сразу; чем дольше ты в лабиринте, тем он злее: обычный рост → вытянутый → на четвереньках.");
            brFirstAppear = Ui.Slider("Монстр впервые появится через", brFirstAppear, 10f, 300f, "0", " с");
            brAngerSecs = Ui.Slider("До полной ярости (на четвереньках)", brAngerSecs, 60f, 1200f, "0", " с");
            brCamFx = Ui.Toggle("Камера-кассета: помехи, REC, таймкод", brCamFx);
            Ui.Label("Высота уровня над чемоданом: " + cfgBrHeight.Value.ToString("0") + " м (меняется в .cfg: [Backrooms] HeightM)", 12, Ui.Muted);
            Ui.Label("Графика: " + BrGfx.LitName + " | " + BrGfx.OpName, 11, Ui.Muted);
            if (brIgnInfo.Length > 0) Ui.Label("Барьеры: " + brIgnInfo, 11, Ui.Muted);
            if (brLastDiag.Length > 0) Ui.Label("Последнее: " + brLastDiag, 11, Ui.Muted);
            if (brInside) Ui.Label("Ты в Backrooms: " + Mathf.RoundToInt(Time.time - brEnterT) + " с, ярость монстра: " + Mathf.RoundToInt(BrAnger(Time.time - brEnterT) * 100f) + "%", 13, Ui.Accent);
        }
    }
}
