// Развлечения 1.19: «Колесо судьбы» (общая рулетка событий), «Гонка до точки» и «Страж на склонах» (Weeping-Angel-страшилка с монстром из Backrooms).
// Всё синхронизируется по каналу RaiseEvent (нужен PeakFun у друзей): wh — колесо, rc — гонка, wt — Страж.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        // ======================================================= КОЛЕСО СУДЬБЫ =======================================================
        // kind: 0 — добро, 1 — беда, 2 — странное
        internal static readonly string[] WheelNames =
        {
            "Подарок судьбы", "Исцеление", "Заряд бодрости", "Великан 20 с", "Крошка 20 с", "Подбросило!", "Споткнулся", "Сон", "Яд", "Холод",
            "Помехи экрана", "Диско", "Пьяный экран", "Страж на склонах", "К другу!", "Проклятие", "Жара"
        };
        internal static readonly int[] WheelKind = { 0, 0, 0, 2, 2, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 1, 1 };
        internal static readonly int[] WheelW = { 9, 8, 8, 6, 6, 7, 8, 5, 6, 6, 7, 6, 6, 4, 6, 4, 6 };

        internal static int WheelPick(System.Random r)
        {
            int tot = 0; foreach (var w in WheelW) tot += w; int x = r.Next(tot);
            for (int i = 0; i < WheelW.Length; i++) { x -= WheelW[i]; if (x < 0) return i; }
            return 0;
        }

        bool wheelAuto; float wheelEvery = 60f, wheelNext; int wheelMode;          // 0 — случайному игроку, 1 — всем сразу
        bool wheelOn; float wheelT0; int wheelId, wheelIdx, wheelActor; bool wheelApplied; const float WheelDur = 4.6f; const int WheelLand = 34;
        int[] wheelSeq; float wheelSizeBack = -1f;

        void WheelSpinNow()
        {
            if (wheelOn && Time.time - wheelT0 < WheelDur + 2f) { Ui.Toast("Колесо ещё крутится", Ui.Muted); return; }
            var all = Game.Everyone(true); if (all.Count == 0) { Ui.Toast("Нет игроков", Ui.Bad); return; }
            int actor = -1;
            if (wheelMode == 0) { var v = all[UnityEngine.Random.Range(0, all.Count)]; actor = Game.ActorOf(v); if (actor < 0) actor = Game.LocalActor(); }
            int id = UnityEngine.Random.Range(1, 1 << 28), idx = WheelPick(new System.Random(id ^ 0x5bd1));
            if (Game.InRoom) Net.Send("wh", id + "," + idx + "," + actor);
            WheelBegin(id, idx, actor);
        }

        void WheelBegin(int id, int idx, int actor)
        {
            idx = Mathf.Clamp(idx, 0, WheelNames.Length - 1); wheelId = id; wheelIdx = idx; wheelActor = actor; wheelT0 = Time.time; wheelOn = true; wheelApplied = false;
            var r = new System.Random(id); wheelSeq = new int[WheelLand + 6]; for (int i = 0; i < wheelSeq.Length; i++) wheelSeq[i] = i == WheelLand ? idx : WheelPick(r);
        }

        bool WheelIsMe() { return wheelActor < 0 || wheelActor == Game.LocalActor(); }

        void TickWheel()
        {
            var me = Game.Me();
            if (wheelSizeBack > 0f && Time.time >= wheelSizeBack) { wheelSizeBack = -1f; SetSize(1f); }
            if (wheelAuto && !wheelOn && me != null && Time.time >= wheelNext) { wheelNext = Time.time + wheelEvery; WheelSpinNow(); }
            if (!wheelOn) return;
            float t = Time.time - wheelT0;
            if (!wheelApplied && t >= WheelDur)
            {
                wheelApplied = true;
                if (WheelIsMe() && me != null && !Game.IsDead(me) && (cfgAccept.Value || wheelActor == Game.LocalActor())) { try { WheelApply(wheelIdx, me); } catch (Exception e) { SafeLog("WheelApply", e); } }
            }
            if (t > WheelDur + 3.2f) wheelOn = false;
        }

        void WheelApply(int idx, Component me)
        {
            switch (idx)
            {
                case 0: if (itemNames.Count > 0) { var item = itemNames[UnityEngine.Random.Range(0, itemNames.Count)]; SpawnWorldItem(item, me.transform.position + Vector3.up * 1.5f); Ui.Toast("Колесо подарило: " + ItemTitle(item), Ui.Good); } else ApplyEffectLocal("heal", 1f); break;
                case 1: ApplyEffectLocal("heal", 1f); break;
                case 2: ApplyEffectLocal("energy", 15f); break;
                case 3: SetSize(2.4f); wheelSizeBack = Time.time + 20f; break;
                case 4: SetSize(0.4f); wheelSizeBack = Time.time + 20f; break;
                case 5: Launch(me); break;
                case 6: Trip(me); break;
                case 7: PassOut(me); break;
                case 8: ApplyEffectLocal("poison", 10f); break;
                case 9: ApplyEffectLocal("cold", 10f); break;
                case 10: OnNetCommandLocal("fx", "Static:6"); break;
                case 11: OnNetCommandLocal("fx", "Disco:8"); break;
                case 12: Shade.Set(ShadeKind.Drunk, 20f); break;
                case 13: WatcherStart(180f, true); break;
                case 14:
                    {
                        var o = Game.Others(); o.RemoveAll(c => c == null || Game.IsDead(c)); if (o.Count == 0) { ApplyEffectLocal("heal", 1f); break; }
                        Warp(me, o[UnityEngine.Random.Range(0, o.Count)].transform.position + Vector3.up * 1.5f); Ui.Toast("Колесо унесло тебя к другу", Ui.Accent2); break;
                    }
                case 15: ApplyEffectLocal("curse", 10f); break;
                case 16: ApplyEffectLocal("hot", 10f); break;
            }
        }

        static Color WheelCol(int k) { return k == 0 ? new Color(0.2f, 0.62f, 0.34f) : k == 1 ? new Color(0.72f, 0.2f, 0.26f) : new Color(0.46f, 0.3f, 0.8f); }

        void DrawWheel(float s)
        {
            if (!wheelOn || wheelSeq == null) return;
            float t = Time.time - wheelT0; float k = Mathf.Clamp01(t / WheelDur); float e = 1f - Mathf.Pow(1f - k, 3.4f); float pos = e * WheelLand;
            float cw = 176f * s, ch = 78f * s, cx = Screen.width * 0.5f, cy = Screen.height * 0.13f, pw = cw * 5.4f;
            float fade = Mathf.Clamp01(Mathf.Min(t / 0.25f, (WheelDur + 3.2f - t) / 0.4f));
            var old = GUI.color; Ui.Box(new Rect(cx - pw * 0.5f, cy - 40f * s, pw, ch + 82f * s), new Color(0.03f, 0.04f, 0.09f, 0.82f * fade), 16f * s);
            Ui.Text(new Rect(cx - pw * 0.5f, cy - 36f * s, pw, 30f * s), "КОЛЕСО СУДЬБЫ — " + (wheelActor < 0 ? "ВСЕМ" : Game.PlayerName(wheelActor)), (int)(20f * s), new Color(1f, 0.85f, 0.3f, fade), TextAnchor.MiddleCenter, true);
            for (int i = Mathf.Max(0, Mathf.FloorToInt(pos - 3f)); i <= Mathf.Min(wheelSeq.Length - 1, Mathf.CeilToInt(pos + 3f)); i++)
            {
                float d = i - pos; float a = Mathf.Clamp01(1f - Mathf.Abs(d) / 2.9f) * fade; if (a < 0.02f) continue;
                float x = cx + d * cw - cw * 0.5f + 4f * s; var c = WheelCol(WheelKind[wheelSeq[i]]); c.a = a;
                var rr = new Rect(x, cy, cw - 8f * s, ch); Ui.Box(rr, c, 12f * s); Ui.Text(rr, WheelNames[wheelSeq[i]], (int)(17f * s), new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter, true);
            }
            Ui.Box(new Rect(cx - 2f * s, cy - 8f * s, 4f * s, ch + 16f * s), new Color(1f, 0.85f, 0.3f, fade), 2f * s);
            if (t >= WheelDur) Ui.Text(new Rect(cx - pw * 0.5f, cy + ch + 8f * s, pw, 30f * s), "Выпало: " + WheelNames[wheelIdx] + (WheelIsMe() ? "  ← это тебе!" : ""), (int)(21f * s), new Color(1f, 1f, 1f, fade), TextAnchor.MiddleCenter, true);
            GUI.color = old;
        }

        // ======================================================= ГОНКА =======================================================
        bool raceOn; int racePhase, raceId; Vector3 racePos; float raceR = 6f, raceGo, raceEnd, raceNetT; float raceCd = 5f; bool raceLoserTrip, raceSelfLoser;
        string raceWinner = ""; readonly Dictionary<int, int> raceWins = new Dictionary<int, int>(); readonly Dictionary<int, float> raceDist = new Dictionary<int, float>();
        float raceStartDist;

        void RaceStartHere()
        {
            var me = Game.Me(); var cam = Camera.main; if (me == null || cam == null) { Ui.Toast("Нет персонажа/камеры", Ui.Bad); return; }
            Vector3 p; RaycastHit h; if (Physics.Raycast(cam.transform.position + cam.transform.forward * 1.2f, cam.transform.forward, out h, 700f, ~0, QueryTriggerInteraction.Ignore)) p = h.point; else { Ui.Toast("Посмотри на землю — туда поставлю финиш", Ui.Bad); return; }
            int id = UnityEngine.Random.Range(1, 1 << 28); var inv = CultureInfo.InvariantCulture;
            if (Game.InRoom) Net.Send("rc", string.Format(inv, "s,{0:F1},{1:F1},{2:F1},{3:F1},{4:F1},{5},{6}", p.x, p.y, p.z, raceR, raceCd, id, raceLoserTrip ? 1 : 0));
            RaceBegin(p, raceR, raceCd, id, raceLoserTrip);
        }

        void RaceBegin(Vector3 p, float r, float cd, int id, bool lt)
        {
            racePos = p; raceR = Mathf.Clamp(r, 2f, 30f); raceId = id; racePhase = 0; raceOn = true; raceGo = Time.time + Mathf.Clamp(cd, 2f, 15f); raceEnd = raceGo + 1200f; raceWinner = ""; raceDist.Clear(); raceSelfLoser = lt;
            var me = Game.Me(); raceStartDist = me != null ? Vector3.Distance(me.transform.position, p) : 1f; GameToast("ГОНКА! Финиш — золотой столб. Старт по сигналу!", false);
        }

        void RaceStop(bool net)
        {
            if (!raceOn) return; raceOn = false; racePhase = 0; if (net && Game.InRoom) Net.Send("rc", "x");
        }

        void RaceFinish(int actor, int id)
        {
            if (!raceOn || racePhase >= 2 || id != raceId) return;
            racePhase = 2; raceEnd = Time.time + 9f; raceWinner = actor == Game.LocalActor() ? (Game.Me() != null ? Game.Name(Game.Me()) : "ты") : Game.PlayerName(actor);
            int w; raceWins.TryGetValue(actor, out w); raceWins[actor] = w + 1; Vfx.Sparks(racePos + Vector3.up * 0.5f, Vector3.up, 70, new Color(1f, 0.85f, 0.2f)); GameToast("ПОБЕДИТЕЛЬ: " + raceWinner + "!", false);
            var me = Game.Me();
            if (raceSelfLoser && actor != Game.LocalActor() && me != null && !Game.IsDead(me) && cfgAccept.Value) { Trip(me); Ui.Toast("Проиграл гонку — падай!", Ui.Accent2); }
        }

        void TickRace()
        {
            if (!raceOn) return; var me = Game.Me(); if (me == null) return;
            if (racePhase == 0 && Time.time >= raceGo) { racePhase = 1; Ui.Toast("ГО!", Ui.Good); }
            if (racePhase == 2 && Time.time > raceEnd) { raceOn = false; return; }
            if (racePhase == 1 && Time.time > raceEnd) { raceOn = false; return; }
            if (racePhase == 1 && !Game.IsDead(me))
            {
                Vector3 pp = me.transform.position; float dx = pp.x - racePos.x, dz = pp.z - racePos.z; float dist = Mathf.Sqrt(dx * dx + dz * dz); raceDist[Game.LocalActor()] = dist;
                if (Time.time >= raceNetT) { raceNetT = Time.time + 1f; if (Game.InRoom) Net.Send("rc", "d," + Game.LocalActor() + "," + dist.ToString("0", CultureInfo.InvariantCulture)); }
                if (RaceReached(pp, racePos, raceR)) { if (Game.InRoom) Net.Send("rc", "w," + Game.LocalActor() + "," + raceId); RaceFinish(Game.LocalActor(), raceId); }
            }
        }

        internal static bool RaceReached(Vector3 p, Vector3 fin, float r) { float dx = p.x - fin.x, dz = p.z - fin.z; return dx * dx + dz * dz <= r * r && Mathf.Abs(p.y - fin.y) < 14f; }

        void OnRaceNet(string arg, int sender)
        {
            var inv = CultureInfo.InvariantCulture; var p = arg.Split(',');
            try
            {
                if (p[0] == "s" && cfgAccept.Value) RaceBegin(new Vector3(float.Parse(p[1], inv), float.Parse(p[2], inv), float.Parse(p[3], inv)), float.Parse(p[4], inv), float.Parse(p[5], inv), int.Parse(p[6]), p.Length > 7 && p[7] == "1");
                else if (p[0] == "w") RaceFinish(int.Parse(p[1]), int.Parse(p[2]));
                else if (p[0] == "d") raceDist[int.Parse(p[1])] = float.Parse(p[2], inv);
                else if (p[0] == "x") { raceOn = false; Ui.Toast("Гонка отменена", Ui.Muted); }
            }
            catch (Exception e) { SafeLog("race-net", e); }
        }

        void DrawRace(float s)
        {
            if (!raceOn) return; var me = Game.Me(); var cam = Camera.main; float cx = Screen.width * 0.5f;
            if (racePhase == 0)
            {
                float left = raceGo - Time.time; int n = Mathf.CeilToInt(left);
                float pulse = 1f + 0.35f * (1f - (left - Mathf.Floor(left)));
                Ui.Text(new Rect(0, Screen.height * 0.3f, Screen.width, 140f * s), n > 0 ? n.ToString() : "ГО!", (int)(110f * s * pulse), new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, true);
                Ui.Text(new Rect(0, Screen.height * 0.3f + 130f * s, Screen.width, 36f * s), "до финиша ≈ " + Mathf.RoundToInt(raceStartDist) + " м — не стартуй раньше сигнала", (int)(20f * s), Color.white, TextAnchor.MiddleCenter, true); return;
            }
            string arrow = "";
            if (me != null && cam != null && racePhase == 1)
            {
                Vector3 to = racePos - me.transform.position; to.y = 0f; Vector3 fw = cam.transform.forward; fw.y = 0f; float ang = Vector3.SignedAngle(fw, to, Vector3.up); string[] ar = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };
                arrow = ar[((Mathf.RoundToInt(ang / 45f) % 8) + 8) % 8];
            }
            float w = 330f * s, x = Screen.width - w - 24f * s, y = 24f * s; var rows = raceDist.OrderBy(k => k.Value).Take(5).ToList(); float h = (52f + rows.Count * 22f) * s;
            Ui.Box(new Rect(x, y, w, h), new Color(0.04f, 0.05f, 0.1f, 0.78f), 14f * s);
            Ui.Text(new Rect(x + 14f * s, y + 6f * s, w - 28f * s, 28f * s), racePhase == 2 ? "ПОБЕДА: " + raceWinner : "ГОНКА  " + arrow, (int)(19f * s), racePhase == 2 ? Ui.Good : Ui.Accent, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < rows.Count; i++) Ui.Text(new Rect(x + 14f * s, y + 40f * s + i * 22f * s, w - 28f * s, 22f * s), (i + 1) + ". " + (rows[i].Key == Game.LocalActor() ? "ты" : Game.PlayerName(rows[i].Key)) + " — " + Mathf.RoundToInt(rows[i].Value) + " м", (int)(15f * s), i == 0 ? Color.white : Ui.Txt, TextAnchor.MiddleLeft, i == 0);
        }

        // ======================================================= СТРАЖ НА СКЛОНАХ =======================================================
        // Правила «плачущего ангела»: пока ты на него смотришь — он стоит; отвернулся — подходит. Первые появления: стоит вдали и исчезает, если на него посмотреть.
        bool wtOn, wtTrip = true; float wtEnd, wtNext, wtLook, wtAge, wtHeartVol, wtHeartPitch = 1f; int wtLevel, wtSeen, wtCaught; BrMon wtMon; float wtMinutes = 6f; string wtInfo = "";

        void WatcherStart(float secs, bool announce)
        {
            if (brInside) return; wtOn = true; wtEnd = Time.time + secs; wtLevel = 0; wtNext = Time.time + UnityEngine.Random.Range(10f, 20f); wtSeen = 0; wtCaught = 0; WtRemove();
            if (announce) Ui.Toast("Что-то бродит по склону… не оборачивайся (F8 — убрать)", Ui.Muted);
        }

        void WatcherStop() { wtOn = false; WtRemove(); }

        void WtRemove() { if (wtMon != null && wtMon.root != null) UnityEngine.Object.Destroy(wtMon.root); wtMon = null; wtHeartVol = 0f; wtLook = 0f; wtAge = 0f; }

        internal static bool WtInView(Vector3 camPos, Vector3 camFwd, Vector3 target, float maxAng) { Vector3 to = target - camPos; return to.sqrMagnitude > 0.01f && Vector3.Angle(camFwd, to) < maxAng; }

        static bool WtGround(Vector3 p, out Vector3 g, out Vector3 n)
        {
            g = p; n = Vector3.up; int mask = 0; try { mask = TerrainMask(); } catch { } if (mask == 0) mask = ~0; RaycastHit h;
            if (!Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out h, 90f, mask, QueryTriggerInteraction.Ignore)) return false;
            g = h.point; n = h.normal; return true;
        }

        void WtSpawn(Component me, Camera cam)
        {
            Vector3 cp = cam.transform.position; Vector3 f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude < 0.01f) return; f.Normalize();
            float lv = Mathf.Clamp01(wtLevel / 5f); int mask = 0; try { mask = TerrainMask(); } catch { } if (mask == 0) mask = ~0;
            for (int i = 0; i < 28; i++)
            {
                float yaw = UnityEngine.Random.Range(-55f, 55f); float d = Mathf.Lerp(40f, 15f, lv) * UnityEngine.Random.Range(0.8f, 1.2f);
                Vector3 p = cp + Quaternion.Euler(0f, yaw, 0f) * f * d; Vector3 g, n; if (!WtGround(p, out g, out n)) continue; if (n.y < 0.72f || Mathf.Abs(g.y - cp.y) > 14f) continue;
                if (Physics.Linecast(cp, g + Vector3.up * 1.9f, mask, QueryTriggerInteraction.Ignore)) continue;
                wtMon = new BrMon(g); wtMon.rt.rotation = Quaternion.LookRotation(new Vector3(-(p - cp).x, 0f, -(p - cp).z).normalized); wtAge = 0f; wtLook = 0f; wtInfo = "появился в " + Mathf.RoundToInt(d) + " м (уровень " + wtLevel + ")";
                for (int k = 0; k < 6; k++) wtMon.Pose(0.03f, 0f, 0.1f, 0f, 0f, cp, true, 0f);
                BrSnd.Sting(g, 0.25f); return;
            }
            wtNext = Time.time + 4f; wtInfo = "не нашёл ровное место перед тобой, пробую снова";
        }

        void WtVanish(bool seen)
        {
            if (wtMon == null) return; Vector3 p = wtMon.rt.position; if (seen) { BrSnd.Sting(p, 0.5f); wtSeen++; try { Shade.Set(ShadeKind.Glitch, 0.7f); } catch { } }
            wtLevel = Mathf.Min(wtLevel + 1, 7); WtRemove(); wtNext = Time.time + UnityEngine.Random.Range(14f, 32f) * Mathf.Lerp(1f, 0.45f, wtLevel / 7f);
        }

        void WtCatch(Component me)
        {
            wtCaught++; try { Shade.Set(ShadeKind.Glitch, 3f); } catch { } CamShake.Boost = Mathf.Max(CamShake.Boost, 1.6f); BrSnd.Rasp(); Sfx2.Boom();
            if (wtTrip && !Game.IsDead(me)) Trip(me);
            WtRemove(); wtLevel = 0; wtNext = Time.time + 55f; wtInfo = "дотронулся (#" + wtCaught + "), отдыхает";
        }

        void TickWatcher()
        {
            if (!wtOn) { if (wtMon != null) WtRemove(); return; }
            var me = Game.Me(); var cam = Camera.main;
            if (me == null || cam == null || Game.IsDead(me) || brInside) { if (wtMon != null) WtRemove(); return; }
            if (Time.time > wtEnd && wtMon == null) { wtOn = false; Ui.Toast("Страж потерял интерес и ушёл", Ui.Muted); return; }
            float dt = Time.deltaTime;
            if (wtMon == null) { if (Time.time >= wtNext && Time.time < wtEnd) WtSpawn(me, cam); return; }
            if (wtMon.root == null) { wtMon = null; return; }
            Vector3 cp = cam.transform.position, pos = wtMon.rt.position; Vector3 head = wtMon.HeadPos(); float dist = Vector3.Distance(cp, head);
            int mask = 0; try { mask = TerrainMask(); } catch { } if (mask == 0) mask = ~0;
            bool los = !Physics.Linecast(cp, head, mask, QueryTriggerInteraction.Ignore);
            bool inView = los && WtInView(cp, cam.transform.forward, head, 42f), stare = los && WtInView(cp, cam.transform.forward, head, 7f);
            wtAge += dt; float speed = 0f;
            if (wtLevel >= 2 && !inView) speed = Mathf.Lerp(2.2f, 4.6f, Mathf.Clamp01((wtLevel - 2) / 4f));
            Vector3 flat = cp - pos; flat.y = 0f; Vector3 dir = flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward;
            if (speed > 0f)
            {
                Vector3 np = pos + dir * speed * dt; Vector3 g, n;
                if (WtGround(np, out g, out n) && Mathf.Abs(g.y - pos.y) < 1.4f) wtMon.rt.position = g;
                else speed = 0f;
            }
            wtMon.rt.rotation = Quaternion.Slerp(wtMon.rt.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-dt * (inView ? 1.2f : 6f)));
            wtMon.Pose(dt, speed, Mathf.Lerp(0.1f, 0.85f, wtLevel / 7f), 0f, Mathf.Lerp(0f, 30f, wtLevel / 7f), cp, true, 0f);
            wtHeartVol = dist < 30f ? Mathf.Clamp01((30f - dist) / 26f) * 0.8f : 0f; wtHeartPitch = 0.9f + wtHeartVol * 0.7f;
            wtLook = stare ? wtLook + dt : Mathf.Max(0f, wtLook - dt * 0.5f);
            if (wtLevel < 2 && (wtLook > 1.3f || dist < 7f)) { WtVanish(wtLook > 0.2f || dist < 7f); return; }
            if (wtLevel >= 2 && wtLook > 3.2f && dist > 5f && UnityEngine.Random.value < 0.5f) { WtVanish(true); return; }   // иногда сдаётся, если смотреть долго
            if (dist < 2.4f && wtLevel >= 2) { WtCatch(me); return; }
            if (wtAge > 55f) WtVanish(false);
        }

        void OnWatcherNet(string arg)
        {
            float secs; if (!float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out secs)) return;
            if (secs <= 0f) { WatcherStop(); return; }
            if (!cfgAccept.Value) return; WatcherStart(Mathf.Clamp(secs, 30f, 1800f), true);
        }

        // ======================================================= общее =======================================================
        void FunPanic() { wheelOn = false; wheelAuto = false; if (wheelSizeBack > 0f) { wheelSizeBack = -1f; SetSize(1f); } RaceStop(false); WatcherStop(); }
        void TickFun() { try { TickWheel(); TickRace(); TickWatcher(); } catch (Exception e) { SafeLog("Fun", e); } }
        void DrawFunHud() { if (Event.current.type != EventType.Repaint) return; float s = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f); DrawWheel(s); DrawRace(s); }

        void TabFun()
        {
            Ui.Header("Развлечения");
            Ui.Para("Три новых затеи для компании. У всех друзей должен стоять PeakFun (команды идут по каналу мода); F8 останавливает всё это.", 14, Ui.Muted);
            if (!NeedMe()) return;

            Ui.Header("Колесо судьбы");
            Ui.Para("На экране у всех одновременно крутится рулетка и выбирает событие: от подарка и исцеления до подброса, сна, великана или Стража. Выпавшее применяется к жертве (или ко всем).", 14);
            bool wasAuto = wheelAuto; wheelAuto = Ui.Toggle("Крутить само каждые N секунд", wheelAuto, "Автоколесо крутится у того, кто включил тумблер");
            if (wheelAuto && !wasAuto) wheelNext = Time.time + wheelEvery;
            if (wheelAuto) { wheelEvery = Mathf.Round(Ui.Slider("Интервал", wheelEvery, 20f, 240f, "0", " с")); }
            int m = Ui.Buttons(new[] { "Случайному игроку", "Всем сразу" }, 2, 0, 34f, true, wheelMode); if (m >= 0) wheelMode = m;
            if (Ui.Button("Крутить колесо сейчас", 1)) WheelSpinNow();

            Ui.Header("Гонка до точки");
            Ui.Para("Смотришь на место на земле — там вырастает золотой столб. Отсчёт, потом все бегут (карабкаются) к нему; на экране стрелка, расстояние и места. Кто первым в круге — побеждает. Победы копятся.", 14);
            raceR = Ui.Slider("Радиус финиша", raceR, 3f, 25f, "0", " м"); raceCd = Mathf.Round(Ui.Slider("Отсчёт", raceCd, 3f, 12f, "0", " с"));
            raceLoserTrip = Ui.Toggle("Проигравшие спотыкаются", raceLoserTrip, "У друзей сработает, если они принимают пранки");
            if (Ui.Button("Поставить финиш туда, куда смотрю, и начать", 1)) RaceStartHere();
            if (raceOn && Ui.Button("Отменить гонку", 2)) RaceStop(true);
            if (raceWins.Count > 0) { Ui.Label("Победы:", 14, Ui.Muted); foreach (var kv in raceWins.OrderByDescending(k => k.Value)) Ui.Para((kv.Key == Game.LocalActor() ? "ты" : Game.PlayerName(kv.Key)) + ": " + kv.Value, 14); }

            Ui.Header("Страж на склонах");
            Ui.Para("Бледный высокий человек из Backrooms бродит по горе. Сначала стоит далеко и пропадает, если на него посмотреть, но с каждым появлением подходит ближе. Позже двигается, пока ты НЕ смотришь, и замирает под взглядом. Если дотронется — помехи и (по желанию) падение.", 14);
            wtMinutes = Mathf.Round(Ui.Slider("Сколько минут бродит", wtMinutes, 1f, 30f, "0", " мин")); wtTrip = Ui.Toggle("Касание роняет", wtTrip);
            if (Ui.Button("Включить себе", 1)) WatcherStart(wtMinutes * 60f, true);
            if (Ui.Button("Подселить друзьям (и себе)")) { if (Game.InRoom) Net.Send("wt", (wtMinutes * 60f).ToString("0", CultureInfo.InvariantCulture)); WatcherStart(wtMinutes * 60f, true); }
            if (wtOn && Ui.Button("Выключить у всех", 2)) { if (Game.InRoom) Net.Send("wt", "0"); WatcherStop(); }
            if (wtOn) Ui.Para("Страж: уровень " + wtLevel + ", замечен " + wtSeen + " раз, дотронулся " + wtCaught + "; " + (wtMon != null ? wtInfo : "ждёт (" + Mathf.Max(0, Mathf.RoundToInt(wtNext - Time.time)) + " с)"), 13, Ui.Accent);
        }

        // ---- автотест ----
        void FunSelfChecks(string c)
        {
            Try(c, "Колесо судьбы: события, веса, одинаковая лента у всех", () =>
            {
                if (WheelNames.Length != WheelKind.Length || WheelNames.Length != WheelW.Length) return "!списки колеса разной длины";
                var cnt = new int[WheelNames.Length]; var r = new System.Random(9); for (int i = 0; i < 6000; i++) cnt[WheelPick(r)]++;
                for (int i = 0; i < cnt.Length; i++) if (cnt[i] == 0) return "!событие «" + WheelNames[i] + "» не выпадает";
                int g = 0, b = 0, w = 0; for (int i = 0; i < cnt.Length; i++) { if (WheelKind[i] == 0) g += cnt[i]; else if (WheelKind[i] == 1) b += cnt[i]; else w += cnt[i]; }
                var a1 = new System.Random(1234); var a2 = new System.Random(1234); for (int i = 0; i < 20; i++) if (WheelPick(a1) != WheelPick(a2)) return "!один seed даёт разные ленты у друзей";
                return WheelNames.Length + " событий; добро " + g / 60 + "%, беда " + b / 60 + "%, странное " + w / 60 + "%";
            });
            Try(c, "Гонка: условие финиша и разбор сетевых команд", () =>
            {
                var fin = new Vector3(100f, 50f, 100f); bool inn = RaceReached(new Vector3(103f, 52f, 102f), fin, 6f), outer = RaceReached(new Vector3(110f, 50f, 100f), fin, 6f), other = RaceReached(new Vector3(100f, 90f, 100f), fin, 6f);
                if (!inn || outer || other) return "!внутри=" + inn + ", снаружи=" + outer + ", другой этаж=" + other;
                bool was = raceOn; int wi = raceId, wp = racePhase; var wpos = racePos; raceOn = true; racePhase = 1; raceId = 77; raceWinner = "";
                RaceFinish(5, 76); bool wrongId = racePhase == 1; RaceFinish(5, 77); bool ok = racePhase == 2 && raceWins.ContainsKey(5); raceWins.Remove(5); raceOn = was; raceId = wi; racePhase = wp; racePos = wpos;
                if (!wrongId) return "!финиш с чужим id засчитан"; if (!ok) return "!победа не записана";
                return "круг/высота работают, чужая гонка игнорируется, победа записывается";
            });
            Try(c, "Страж на склонах: тело строится, поле зрения считается верно", () =>
            {
                var m = new BrMon(Vector3.up * 950f); for (int i = 0; i < 30; i++) m.Pose(0.03f, i > 15 ? 3f : 0f, 0.5f, 0f, 10f, Vector3.up * 950f + Vector3.forward * 20f, true, 0f);
                float h = m.HeadPos().y - m.rt.position.y; UnityEngine.Object.Destroy(m.root);
                bool front = WtInView(Vector3.zero, Vector3.forward, new Vector3(0, 0, 30f), 42f), side = WtInView(Vector3.zero, Vector3.forward, new Vector3(30f, 0, 5f), 42f), stare = WtInView(Vector3.zero, Vector3.forward, new Vector3(2f, 0, 30f), 7f);
                if (!front || side || !stare) return "!впереди=" + front + ", сбоку=" + side + ", «в упор»=" + stare;
                if (float.IsNaN(h) || h < 1.4f || h > 3.4f) return "!странный рост " + h;
                return "рост " + h.ToString("0.00") + " м; угол обзора и «взгляд в упор» верны";
            });
        }
    }
}
