// Автотест 1.17: Backrooms (генерация, чемодан, модель монстра и его анимация без телепортов игрока, падение через чемодан, пол/стены/свет,
// поведение монстра по стадиям ярости, поимка, F8, таймер) и «Графика, ввод, сеть». Бот НЕ телепортирует игрока по лабиринту (ни углы, ни дверь).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        int BrHash()
        {
            int h = 17; for (int x = 0; x <= brN; x++) for (int y = 0; y < brN; y++) h = h * 31 + (brWV[x, y] ? 1 : 0);
            for (int x = 0; x < brN; x++) for (int y = 0; y <= brN; y++) h = h * 31 + (brWH[x, y] ? 1 : 0);
            return h * 31 + brExitX * 100 + brExitY;
        }

        IEnumerator BrWaitArrive(float max) { float e = Time.time + max; while (Time.time < e && !tAbort && !(brInside && brArrived)) yield return null; }
        IEnumerator BrWaitOut(float max) { float e = Time.time + max; while (Time.time < e && !tAbort && (brInside || brBusy)) yield return null; }
        IEnumerator BrWaitMon(float max) { float e = Time.time + max; while (Time.time < e && !tAbort && mon == null && brInside) yield return null; }

        string BrMonInfo() { return mon == null ? "нет" : MsNames[ms] + ", " + Vector3.Distance(new Vector3(mon.rt.position.x, 0, mon.rt.position.z), new Vector3(CenterOf(Game.Me()).x, 0, CenterOf(Game.Me()).z)).ToString("0.0") + " м"; }

        IEnumerator CoTBack(Component me, Vector3 origin)
        {
            string c = TCatNames[6];
            int oldSize = brSize; float oldTO = brTimeout; bool oldEnt = brEntity; bool oldFog = RenderSettings.fog; bool oldCam = brCamFx;
            if (brInside) { BackroomsPanic(); yield return BrWaitOut(4f); }
            RemoveCrates();
            // ---- 1. генерация и логика (без игрока) ----
            Try(c, "Генерация лабиринта 7 / 15 / 40 / 80", () =>
            {
                var sb = new System.Text.StringBuilder();
                foreach (int sz in new[] { 7, 15, 40, 80 })
                {
                    var sw = Stopwatch.StartNew(); BrGenerate(4242 + sz, sz); sw.Stop(); var d = BrBfs(brSpawnX, brSpawnY); int reach = 0;
                    for (int x = 0; x < sz; x++) for (int y = 0; y < sz; y++) if (d[x, y] >= 0) reach++;
                    if (reach != sz * sz) return "!" + sz + "×" + sz + ": достижимо только " + reach + " из " + (sz * sz) + " клеток";
                    if (d[brExitX, brExitY] < Mathf.Min(5, sz - 1)) return "!выход слишком близко к старту (" + d[brExitX, brExitY] + " шагов)";
                    sb.Append(sz + "×" + sz + " (" + (sz * 4) + " м): до выхода " + d[brExitX, brExitY] + " клеток, " + sw.ElapsedMilliseconds + " мс; ");
                }
                return sb.ToString();
            });
            Try(c, "Одинаковый уровень у всех (детерминизм по seed)", () =>
            {
                BrGenerate(777, 40); int a = BrHash(); BrGenerate(777, 40); int b = BrHash(); BrGenerate(778, 40); int d = BrHash();
                if (a != b) return "!один seed даёт разные уровни — друзья окажутся в разных местах";
                return d == a ? "?разные seed дали одинаковый уровень" : "один seed → один уровень (hash " + a + "), другой seed → другой";
            });
            Try(c, "Условие выхода через дверь EXIT (логика, без телепорта)", () =>
            {
                var ob = brBase; var oe = brExitPos; var ol = new List<Vector3>(brExitList); brExitList.Clear(); brExitList.Add(new Vector3(10f, 5f, 10f)); brExitPos = new Vector3(10f, 5f, 10f);
                bool near = BrExitReached(new Vector3(10.5f, 6.1f, 10.4f)), farXZ = BrExitReached(new Vector3(10f, 6.1f, 13f)), farY = BrExitReached(new Vector3(10f, 14f, 10f)); brBase = ob; brExitPos = oe; brExitList.Clear(); brExitList.AddRange(ol);
                return near && !farXZ && !farY ? "рядом с дверью — срабатывает, в 3 м и на другом этаже — нет" : "!near=" + near + ", farXZ=" + farXZ + ", farY=" + farY;
            });
            Try(c, "Несколько дверей EXIT, все достижимы (дверь не должна быть одна)", () =>
            {
                var sb = new System.Text.StringBuilder();
                foreach (int sz in new[] { 15, 40, 80 })
                {
                    BrGenerate(900 + sz, sz); var d = BrBfs(brSpawnX, brSpawnY); if (brExits.Count < 2) return "!" + sz + "×" + sz + ": дверей только " + brExits.Count;
                    foreach (var e in brExits) { if (d[e.x, e.y] < 0) return "!дверь в недостижимой клетке " + e; if (!BrWallDirCell(e.x, e.y)) return "!у двери нет стены " + e; }
                    sb.Append(sz + "×" + sz + ": " + brExits.Count + " двери; ");
                }
                return sb.ToString();
            });
            Try(c, "Ловушки: разные типы, пустышки, нет предсказуемости", () =>
            {
                var cnt = new int[TrapNames.Length]; var r = new System.Random(12345); for (int i = 0; i < 4000; i++) cnt[PickTrap(r)]++;
                var sb = new System.Text.StringBuilder(); for (int i = 0; i < cnt.Length; i++) { sb.Append(TrapNames[i] + " " + (cnt[i] / 40f).ToString("0") + "%, "); if (cnt[i] == 0) return "!тип «" + TrapNames[i] + "» никогда не выпадает"; }
                if (cnt[cnt.Length - 1] < 200) return "!пустышек слишком мало: " + cnt[cnt.Length - 1];
                var a = new System.Random(5 * 31 + 5); var b = new System.Random(5 * 31 + 5); if (a.Next(4) != b.Next(4) || PickTrap(a) != PickTrap(b)) return "!один seed даёт разный предмет/ловушку у друзей";
                return sb.ToString();
            });
            Try(c, "Меши тела и головы монстра (нет NaN, нормальные габариты)", () =>
            {
                string[] prof = { "thigh", "shin", "uarm", "farm", "neck", "waist", "lchest", "uchest", "finger", "strand" }; var sb = new System.Text.StringBuilder();
                foreach (var pn in prof)
                {
                    var m = BrBody.Tube(pn, -1); if (m == null || m.vertexCount < 20) return "!" + pn + ": пустой меш"; foreach (var v in m.vertices) if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)) return "!" + pn + ": NaN";
                    if (m.bounds.size.y < 1.9f || m.bounds.size.y > 2.1f) return "!" + pn + ": высота меша " + m.bounds.size.y; sb.Append(pn + " " + m.vertexCount + ", ");
                }
                Mesh up, lo; BrBody.Head(out up, out lo); if (up == null || lo == null || up.vertexCount < 300) return "!голова пустая";
                foreach (var v in up.vertices) if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)) return "!голова: NaN";
                var bb = up.bounds; if (bb.size.y < 0.18f || bb.size.y > 0.4f || bb.size.z < 0.12f) return "!габариты головы " + bb.size;
                return sb + "голова " + up.vertexCount + "+" + lo.vertexCount + " вершин, " + bb.size.x.ToString("0.00") + "×" + bb.size.y.ToString("0.00") + "×" + bb.size.z.ToString("0.00") + " м";
            });
            Try(c, "Четыре вида приманок (чемодан, сундук, рюкзак, аптечка) строятся", () =>
            {
                var sb = new System.Text.StringBuilder();
                for (int m = 0; m < 4; m++)
                {
                    var cr = MakeCrate(-779000 - m, origin + Vector3.up * 850f + Vector3.right * (m * 4), 100f, 40, 0, m, 6);
                    var rs = cr.go.GetComponentsInChildren<Renderer>(); int nullM = 0; foreach (var r in rs) if (r.sharedMaterial == null) nullM++; int cols = cr.go.GetComponentsInChildren<Collider>().Length;
                    var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds); bool ok = rs.Length >= 10 && nullM == 0 && bb.size.y > 0.3f && bb.size.y < 2.5f;
                    sb.Append(BaitNames[m] + ": " + rs.Length + " дет., " + bb.size.x.ToString("0.0") + "×" + bb.size.y.ToString("0.0") + "×" + bb.size.z.ToString("0.0") + " м; "); RemoveCrates(); if (!ok) return "!" + sb + " (мало деталей/нет материала/странные габариты)"; if (cols > 0) return "?" + sb + " (коллайдеры мешают)";
                }
                return sb.ToString();
            });
            Try(c, "Автоприманка: код выбора места доступен и MapHandler читается", () =>
            {
                var mh = FindMapHandler(); if (mh == null) return "?MapHandler не найден (карта не загружена — проверка пропущена)"; int cur = R.Get<int>(mh, "currentSegment", -99);
                var segs = R.Get(mh, "segments") as Array; var tr = segs != null && segs.Length > 0 ? R.Get(segs.GetValue(Mathf.Clamp(cur, 0, segs.Length - 1)), "reconnectSpawnPos") as Transform : null;
                return "сегмент " + cur + ", биомов " + (segs != null ? segs.Length : 0) + ", точка начала биома " + (tr != null ? tr.position.ToString("0") : "не прочитана (будет позиция игрока)") + "; " + autoInfo;
            });
            FunSelfChecks(c);
            Try(c, "Кривая ярости монстра по времени", () =>
            {
                float old = brAngerOverride; brAngerOverride = -1f; int os = brSight; brSight = 0; float s1 = brAngerSecs; brAngerSecs = 420f;
                float a0 = BrAnger(0f), a1 = BrAnger(60f), a2 = BrAnger(210f), a3 = BrAnger(420f), a4 = BrAnger(900f); brAngerOverride = old; brSight = os; brAngerSecs = s1;
                if (!(a0 < a1 && a1 < a2 && a2 < a3)) return "!ярость не растёт монотонно: " + a0 + ", " + a1 + ", " + a2 + ", " + a3;
                return "0 с → " + (a0 * 100f).ToString("0") + "%, 1 мин → " + (a1 * 100f).ToString("0") + "%, 3.5 мин → " + (a2 * 100f).ToString("0") + "%, 7 мин → " + (a3 * 100f).ToString("0") + "% (на четвереньках с 78%)";
            });
            Try(c, "Шейдеры и свет", () =>
            {
                var m = BrGfx.Surf(null, Color.white); string sv = BrGfx.Survey(); var o = BrGfx.Opaque(null, Color.white);
                if (!BrGfx.LitOk) return "?освещаемый шейдер (URP Lit) не найден — Backrooms будут без реального света и теней. Шейдеры: " + sv;
                return "свет: «" + BrGfx.LitName + "», без света: «" + BrGfx.OpName + "»; " + sv + (m == null || o == null ? " [материал null!]" : "");
            });
            Try(c, "Слой пола/стен", () => { int l = BrLayer(); return "слой " + l + " («" + LayerMask.LayerToName(l) + "»); маска земли игры: " + TerrainMask(); });
            // ---- 2. чемодан ----
            Try(c, "Чемодан-приманка: модель", () =>
            {
                Vector3 p = CenterOf(me) + FlatForward() * 9f; p.y = me.transform.position.y - 0.9f; var cr = MakeCrate(-777001, p, 200f, 15, 0);
                var rs = cr.go.GetComponentsInChildren<Renderer>(true); int nullMat = 0; foreach (var r in rs) if (r.sharedMaterial == null) nullMat++;
                int cols = cr.go.GetComponentsInChildren<Collider>(true).Length; Bounds bb = new Bounds(rs[0].bounds.center, Vector3.zero); foreach (var r in rs) if (r.name != "PF_crate_player_beam") bb.Encapsulate(r.bounds);
                string res = rs.Length + " деталей, без материала: " + nullMat + ", коллайдеров: " + cols + ", габариты " + bb.size.x.ToString("0.00") + "×" + bb.size.y.ToString("0.00") + "×" + bb.size.z.ToString("0.00") + " м";
                RemoveCrates();
                if (rs.Length < 20) return "!слишком мало деталей (" + res + ")"; if (nullMat > 0) return "!" + res; if (cols > 0) return "?" + res + " (чемодан не должен мешать)"; return res;
            });
            // ---- 3. модель монстра и анимация (отдельно от игрока, высоко в небе) ----
            if (!tAbort)
            {
                BrMon rig = null; string rigErr = null;
                try { rig = new BrMon(origin + Vector3.up * 900f); } catch (Exception e) { rigErr = (e.InnerException ?? e).GetType().Name + ": " + e.Message; }
                if (rig == null) Rec(c, "Монстр: модель", "FAIL", rigErr);
                else
                {
                    int bones = rig.root.GetComponentsInChildren<Transform>(true).Length; int rn = rig.rends.Count; int nullM = 0; foreach (var r in rig.rends) if (r.sharedMaterial == null) nullM++;
                    Rec(c, "Монстр: модель", nullM == 0 && bones > 60 ? "PASS" : "FAIL", bones + " костей/частей, " + rn + " рендереров, без материала: " + nullM);
                    var res = new System.Text.StringBuilder(); bool bad = false; float prevH = 0f, headUp = 0f;
                    float[][] cases = { new float[] { 0f, 0f, 0f }, new float[] { 0.25f, 0f, 20f }, new float[] { 0.5f, 0f, 35f }, new float[] { 0.75f, 0.35f, 45f }, new float[] { 1f, 1f, 60f } };
                    string[] names = { "обычный рост", "чуть искажён", "вытянут, согнут", "бежит согнувшись", "на четвереньках" }; float[] speeds = { 1.6f, 1.8f, 3f, 6f, 8.5f };
                    for (int k = 0; k < cases.Length && !tAbort; k++)
                    {
                        rig.cb = cases[k][1] > 0.9f ? 0.2f : 0f; rig.hunch = 0f;
                        for (int f = 0; f < 90; f++) { rig.Pose(0.033f, speeds[k], cases[k][0], cases[k][1], cases[k][2], origin + Vector3.up * 900f + new Vector3(5, 1.6f, 5), true, 0f); if (f % 30 == 29) yield return null; }
                        Vector3 hp = rig.HeadPos(); float hh = hp.y - rig.rt.position.y; float miny = rig.MinY() - rig.rt.position.y;
                        bool nan = float.IsNaN(hp.x) || float.IsNaN(hp.y) || float.IsNaN(hp.z);
                        res.Append(names[k] + ": голова " + hh.ToString("0.00") + " м, низ " + miny.ToString("0.00") + " м, ").Append(k == 4 ? "cb=" + rig.cb.ToString("0.00") + "; " : "; ");
                        if (nan) { bad = true; res.Append("[NaN!] "); }
                        if (miny < -0.25f || miny > 0.2f) { bad = true; res.Append("[не стоит на земле!] "); }
                        if (k == 2) headUp = hh; if (k == 0) prevH = hh;
                        if (k == 4 && hh > 1.5f) { bad = true; res.Append("[на четвереньках слишком высоко] "); }
                    }
                    if (headUp < prevH + 0.15f) { bad = true; res.Append("[не вытягивается] "); }
                    Rec(c, "Монстр: анимация по стадиям ярости (стойка → согнут → четвереньки)", bad ? "FAIL" : "PASS", res.ToString());
                    // поза поимки
                    rig.catching = true; for (int f = 0; f < 40; f++) rig.Pose(0.033f, 0f, 1f, 0f, 20f, origin + Vector3.up * 905f, true, 1f);
                    Vector3 hp2 = rig.HeadPos(); Rec(c, "Монстр: поза поимки (рот открыт, руки вперёд)", float.IsNaN(hp2.y) ? "FAIL" : "PASS", "голова на " + (hp2.y - rig.rt.position.y).ToString("0.00") + " м, челюсть повёрнута на " + rig.jaw.localEulerAngles.x.ToString("0") + "°");
                    UnityEngine.Object.Destroy(rig.root);
                }
            }
            // ---- 4. падение через чемодан ----
            brSize = 40; brTimeout = 0f; brEntity = true; brCamFx = true; brAngerOverride = -1f;
            Vector3 cp = CenterOf(me); cp.y = me.transform.position.y - 0.9f; float t0 = Time.time;
            MakeCrate(-777002, cp + FlatForward() * 0.4f, cfgBrHeight.Value, 40, 1);
            yield return BrWaitArrive(14f);
            bool arr = brInside && brArrived;
            Rec(c, "Падение через чемодан → вход (40×40, 160 м)", arr ? "PASS" : "FAIL", arr ? "от касания до прибытия " + (Time.time - t0).ToString("0.0") + " с; " + brLastDiag : "не прибыл за 14 с. Последний лог: " + brLastDiag + "; brInside=" + brInside + ", позиция " + (me != null ? me.transform.position.ToString() : "-"));
            RemoveCrates();
            if (arr)
            {
                yield return Wt(1.5f);
                Try(c, "Пол: игрок стоит, а не падает", () =>
                {
                    Vector3 pc = CenterOf(me); RaycastHit h; bool ok = Physics.Raycast(pc, Vector3.down, out h, 6f, ~0, QueryTriggerInteraction.Ignore); float rel = pc.y - brBase.y;
                    if (!brInside) return "!уже выбросило наружу: " + brLastDiag;
                    if (!ok) return "!под ногами нет коллайдера (ray 6 м); высота над уровнем " + rel.ToString("0.0");
                    if (rel < 0.2f || rel > 2.4f) return "!высота над полом странная: " + rel.ToString("0.00") + " м";
                    return "под ногами «" + h.collider.name + "» (слой " + h.collider.gameObject.layer + "), высота центра " + rel.ToString("0.00") + " м";
                });
                Try(c, "Стены и потолок", () =>
                {
                    Vector3 pc = CenterOf(me); int hits = 0; float nearest = 99f; RaycastHit h;
                    foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right }) if (Physics.Raycast(pc, d, out h, 14f, ~0, QueryTriggerInteraction.Ignore) && h.collider.name.StartsWith("PF_br_player")) { hits++; nearest = Mathf.Min(nearest, h.distance); }
                    bool up = Physics.Raycast(pc, Vector3.up, out h, 6f, ~0, QueryTriggerInteraction.Ignore);
                    if (hits == 0) return "!ни одной стены в радиусе 14 м по 4 направлениям"; if (!up) return "!потолок не найден";
                    return "стены найдены в " + hits + " из 4 направлений (ближайшая " + nearest.ToString("0.0") + " м); потолок " + h.distance.ToString("0.0") + " м";
                });
                Try(c, "Геометрия уровня и коллайдеры", () =>
                {
                    if (brRoot == null) return "!корень уровня не создан";
                    var mf = brRoot.GetComponentsInChildren<MeshFilter>(); long vtx = 0; foreach (var f in mf) if (f.sharedMesh != null) vtx += f.sharedMesh.vertexCount;
                    int col = brRoot.GetComponentsInChildren<Collider>().Length; int nullMat = 0; foreach (var r in brRoot.GetComponentsInChildren<Renderer>()) if (r.sharedMaterial == null) nullMat++;
                    string s2 = mf.Length + " мешей, " + vtx + " вершин, коллайдеров " + col + ", ламп " + brLampPos.Count + " (мигающих " + brFlick.Count + "), без материала: " + nullMat;
                    if (nullMat > 0) return "!" + s2; if (col > 6000) return "?слишком много коллайдеров: " + s2; return s2;
                });
                Try(c, "Реальное освещение, тени и камера-кассета", () =>
                {
                    int on = 0, sh = 0; foreach (var bl in brLights) if (bl.l != null && bl.l.intensity > 0.2f) { on++; if (bl.l.shadows != LightShadows.None) sh++; }
                    string cam = "Shade.Camera: " + (Shade.Cur == ShadeKind.Camera ? "включён" : "НЕ включён (" + Shade.Status + ")") + ", Volume " + (Shade.Ready ? "готов" : "нет");
                    string info = "света включено " + on + " из " + brLights.Count + ", с тенями " + sh + "; " + brUrpInfo + "; туман " + RenderSettings.fog + "; " + cam;
                    if (BrGfx.LitOk && on < 2) return "!свет не включился: " + info; if (!BrGfx.LitOk) return "?" + info; if (Shade.Cur != ShadeKind.Camera) return "?" + info; return info;
                });
                // ---- монстр: стадии ----
                if (!tAbort)
                {
                    // а) обычный рост: заметил → исчез
                    brAngerOverride = 0.1f; brSeenOverride = false; brMonNext = Time.time; yield return BrWaitMon(5f);
                    if (mon == null) Rec(c, "Монстр (ярость 10%): появляется и подглядывает", "FAIL", "не появился за 5 с");
                    else
                    {
                        string d0 = BrMonInfo(); bool peek = ms == MsPeek; yield return Wt(1.2f); bool stillThere = mon != null; brSeenOverride = true; float e = Time.time + 3f; while (Time.time < e && mon != null && !tAbort) yield return null; brSeenOverride = false;
                        Rec(c, "Монстр (ярость 10%): подглядывает, при взгляде исчезает", peek && stillThere && mon == null ? "PASS" : "FAIL", "после появления: " + d0 + ", не убежал сам: " + stillThere + ", исчез после взгляда: " + (mon == null));
                        if (mon != null) RemoveMon();
                    }
                    // б) среднее: заметил → убегает
                    brAngerOverride = 0.45f; brMonNext = Time.time; yield return BrWaitMon(5f);
                    if (mon == null) Rec(c, "Монстр (ярость 45%): убегает от взгляда", "FAIL", "не появился за 5 с");
                    else
                    {
                        yield return Wt(0.8f); brSeenOverride = true; float e = Time.time + 2f; bool fled = false; while (Time.time < e && mon != null && !tAbort) { if (ms == MsFlee) { fled = true; break; } yield return null; }
                        brSeenOverride = false; float e2 = Time.time + 9f; while (Time.time < e2 && mon != null && !tAbort) yield return null;
                        Rec(c, "Монстр (ярость 45%): убегает от взгляда", fled && mon == null ? "PASS" : "FAIL", "побежал: " + fled + ", пропал: " + (mon == null));
                        if (mon != null) RemoveMon();
                    }
                    // в) злой: охота и поимка
                    brAngerOverride = 0.7f; brMonNext = Time.time; yield return BrWaitMon(5f);
                    if (mon == null) Rec(c, "Монстр (ярость 70%): охота и поимка", "FAIL", "не появился за 5 с");
                    else
                    {
                        brSeenOverride = true; float e = Time.time + 2.5f; while (Time.time < e && mon != null && ms != MsHunt && !tAbort) yield return null; brSeenOverride = false;
                        bool hunt = mon != null && ms == MsHunt; float maxCb = 0f, d1 = mon != null ? Vector3.Distance(mon.rt.position, CenterOf(me)) : 0f;
                        float e3 = Time.time + 14f; while (Time.time < e3 && !brCatching && brInside && !tAbort) { if (mon != null) maxCb = Mathf.Max(maxCb, mon.cb); yield return null; }
                        bool caught = brCatching; bool inFrame = false; float hd = -1f;
                        if (caught) { yield return Wt(1.0f); if (mon != null && Camera.main != null) { hd = Vector3.Distance(mon.HeadPos(), Camera.main.transform.position); inFrame = hd < 1.8f; } }
                        yield return BrWaitOut(8f); yield return Wt(1f);
                        float back = Vector3.Distance(me.transform.position, origin); bool okExit = !brInside && back < 8f;
                        Rec(c, "Монстр (ярость 70%): охота (замечен → бежит за тобой)", hunt ? "PASS" : "FAIL", "перешёл в охоту: " + hunt + ", дистанция в начале " + d1.ToString("0") + " м, макс. «пригнулся» " + maxCb.ToString("0.00"));
                        Rec(c, "Поимка: страшная анимация и возврат", caught && inFrame && okExit ? "PASS" : "FAIL", "поймал: " + caught + ", лицо у камеры (" + hd.ToString("0.0") + " м): " + inFrame + ", вернулся на место: " + okExit + " (" + back.ToString("0") + " м)");
                    }
                }
            }
            if (brInside) { BackroomsPanic(); yield return BrWaitOut(4f); }
            // ---- 5. вторая попытка: ярость 95% — четвереньки ----
            if (!tAbort)
            {
                ForceEnter(31337, CenterOf(me) + Vector3.up * cfgBrHeight.Value, 24, true); yield return BrWaitArrive(10f);
                if (brInside && brArrived)
                {
                    brAngerOverride = 0.95f; brMonNext = Time.time; yield return BrWaitMon(5f);
                    if (mon == null) Rec(c, "Монстр (ярость 95%): на четвереньках", "FAIL", "не появился");
                    else
                    {
                        brSeenOverride = true; float e = Time.time + 2f; while (Time.time < e && mon != null && ms != MsHunt && !tAbort) yield return null; brSeenOverride = false;
                        float maxCb = 0f, maxSp = 0f, e3 = Time.time + 12f; while (Time.time < e3 && !brCatching && brInside && !tAbort) { if (mon != null) { maxCb = Mathf.Max(maxCb, mon.cb); maxSp = Mathf.Max(maxSp, mMeas); } yield return null; }
                        Rec(c, "Монстр (ярость 95%): бежит на четвереньках", maxCb > 0.5f && maxSp > 5f ? "PASS" : "WARN", "макс. «на четвереньках» " + maxCb.ToString("0.00") + " (1 = полностью), макс. скорость " + maxSp.ToString("0.0") + " м/с (быстрее тебя без бега)");
                        yield return BrWaitOut(8f);
                    }
                }
                else Rec(c, "Монстр (ярость 95%)", "FAIL", "не удалось войти: " + brLastDiag);
                if (brInside) { BackroomsPanic(); yield return BrWaitOut(4f); }
            }
            // ---- 6. F8 и уборка ----
            if (!tAbort)
            {
                brAngerOverride = -1f; ForceEnter(4711, CenterOf(me) + Vector3.up * cfgBrHeight.Value, 12, true); yield return BrWaitArrive(10f);
                if (brInside && brArrived) { yield return Wt(0.8f); BackroomsPanic(); yield return BrWaitOut(5f); yield return Wt(0.8f); }
                bool clean = brRoot == null && mon == null && !brInside && brLights.Count == 0;
                Rec(c, "Выход по СТОП [F8] + уборка уровня, света и тумана", clean && RenderSettings.fog == oldFog ? "PASS" : "FAIL", "уровень удалён: " + (brRoot == null) + ", монстр удалён: " + (mon == null) + ", туман восстановлен: " + (RenderSettings.fog == oldFog) + ", кассетный фильтр выключен: " + (Shade.Cur != ShadeKind.Camera) + ", до исходной точки " + Vector3.Distance(me.transform.position, origin).ToString("0.0") + " м");
            }
            // ---- 7. таймер ----
            if (!tAbort)
            {
                brEntity = false; brTimeout = 4f; ForceEnter(9091, CenterOf(me) + Vector3.up * cfgBrHeight.Value, 9, false); yield return BrWaitArrive(8f); t0 = Time.time;
                if (brInside && brArrived) { yield return BrWaitOut(9f); bool ok = !brInside; Rec(c, "Автовозврат по таймеру", ok ? "PASS" : "FAIL", ok ? "выброс через " + (Time.time - t0).ToString("0.0") + " с (задано 4)" : "таймер не сработал за 9 с"); yield return Wt(1f); }
                else Rec(c, "Автовозврат по таймеру", "FAIL", "не удалось войти: " + brLastDiag);
                if (brInside) { BackroomsPanic(); yield return BrWaitOut(4f); }
            }
            brAngerOverride = -1f; brSeenOverride = false; RemoveCrates(); brSize = oldSize; brTimeout = oldTO; brEntity = oldEnt; brCamFx = oldCam;
            if (me != null && Vector3.Distance(me.transform.position, origin) > 3f) { Warp(me, origin + Vector3.up * 0.3f); Game.HaltBody(me); yield return Wt(0.6f); }
        }

        IEnumerator CoTMisc(Component me)
        {
            string c = TCatNames[7];
            Try(c, "Конфликты клавиш мода", () =>
            {
                var map = new Dictionary<KeyCode, List<string>>();
                foreach (var f in typeof(PeakFunPlugin).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (f.FieldType != typeof(ConfigEntry<KeyCode>)) continue; var ce = f.GetValue(this) as ConfigEntry<KeyCode>; if (ce == null) continue;
                    List<string> l; if (!map.TryGetValue(ce.Value, out l)) { l = new List<string>(); map[ce.Value] = l; } l.Add(f.Name);
                }
                var dup = new List<string>(); foreach (var kv in map) if (kv.Value.Count > 1) dup.Add(kv.Key + ": " + string.Join(", ", kv.Value.ToArray()));
                return dup.Count == 0 ? "повторяющихся клавиш нет (" + map.Count + " различных)" : "?одна клавиша на несколько действий — " + string.Join("; ", dup.ToArray());
            });
            Try(c, "Шейдер эффектов (Sprites/Default)", () => { var s = Vfx.Sh(); return s == null ? "!нет ни одного шейдера для эффектов" : "используется «" + s.name + "»"; });
            Try(c, "Процедурные текстуры", () => { var a = Vfx.Blob(); var b = BrGfx.CaseAtlas(); return a == null || b == null ? "!текстура не создалась" : "пятно " + a.width + "×" + a.height + ", атлас чемодана " + b.width + "×" + b.height; });
            Try(c, "Камера и экран", () => { var cam = Camera.main; return cam == null ? "!Camera.main нет" : "FOV " + cam.fieldOfView.ToString("0") + ", экран " + Screen.width + "×" + Screen.height + ", culling mask " + cam.cullingMask; });
            Try(c, "Канал сети RaiseEvent", () =>
            {
                if (!Game.InRoom) return "?не в комнате — отправка пропущена (работает только в лобби)";
                bool ok = Net.Send("br", "r;0"); return ok ? "команда отправлена, статус: " + Net.Status : "?отправка не удалась: " + Net.Status;
            });
            Try(c, "Разбор сетевых команд Backrooms", () =>
            {
                int before = brCrates.Count; OnBrNet("c;-777003,1,2,3,200,15,0", 0); int mid = brCrates.Count; OnBrNet("c;bad", 0); OnBrNet("zzz", 0); OnBrNet("c;-777003,1,2,3,200,15,0", 0); int after = brCrates.Count;
                RemoveCrates();
                if (mid != before + 1) return "!команда «c» не создала ящик"; if (after != mid) return "!дубликат чемодана не отфильтрован"; return "создание чемодана, защита от дублей и мусора — работают";
            });
            yield return null;
        }
    }
}
