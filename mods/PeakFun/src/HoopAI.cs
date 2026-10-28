// Баскетбол 1.14: умный соперник 1×1. Своё тело из примитивов (CharacterController), собственный «мозг»:
// защита между игроком и кольцом с упреждением, подбор мяча, перехваты, блок-шоты по таймингу, финты (ловится на «Между ног» и финт),
// атака: проход с кроссовером, бросок с ошибкой от защиты, данки, шаг назад. 4 уровня сложности, матч до N очков.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        const int AiId = -77;
        GameObject aiGo; CharacterController aiCc; Transform aiModel, aiLegL, aiLegR, aiArmL, aiArmR;
        int aiLevel = 1; string aiName = "Соперник"; bool aiTalk = true; int matchTo = 11;
        bool aiHasBall, aiShotLive, aiBusy, aiScripted, aiInbound, aiContested; int aiShotPts, aiPose;
        float aiShotUntil, aiPhase, aiDribPhase = 0.5f, aiSide = 1f, aiPoseUntil, aiPoseK, aiStunUntil, aiStealCd, aiDecideAt, aiShotClock, aiTalkAt, aiSwipeUntil, aiInboundAt = -1f;
        float aiJukeSign = 1f, aiJukeUntil, aiCrossAt, aiNoGrabUntil, aiStuckT, aiVy, aiYaw, aiStunK, aiGrabBlendUntil, aiBlockUntil, aiIgnoreEnd, matchResetAt = -1f, playerGiveAt = -1f, aiTakeAt = -1f, aiHandSfx;
        Vector3 aiVel, aiSeen, aiPrevPP, aiPVel, aiBallPrev, aiWantDir, aiFaceTo; float aiWantSpd; bool aiGuard;
        Quaternion aiSpin = Quaternion.identity; int aiPlan;

        static readonly string[] LvNames = { "Новичок", "Игрок", "Звезда", "Легенда" };
        static readonly float[] LvSpeed = { 4.6f, 5.8f, 6.8f, 7.8f };
        static readonly float[] LvReact = { 3f, 6f, 11f, 20f };
        static readonly float[] LvSteal = { 0.08f, 0.16f, 0.26f, 0.36f };
        static readonly float[] LvBlock = { 0.12f, 0.32f, 0.52f, 0.72f };
        static readonly float[] LvErr = { 0.62f, 0.38f, 0.22f, 0.1f };
        static readonly float[] LvBite = { 0.7f, 0.5f, 0.3f, 0.12f };

        static readonly string[] SayScore = { "Лёгкая!", "Это мой дом!", "Смотри и учись.", "Два очка в копилку.", "Даже не подпрыгнул." };
        static readonly string[] SayLose = { "Повезло…", "Ладно, это было неплохо.", "Не расслабляйся!", "Один — ноль в твою пользу? Посмотрим." };
        static readonly string[] SaySteal = { "Мяч мой!", "Ты потерял!", "Не зевай!" };
        static readonly string[] SayBlock = { "Не сегодня!", "Убрано!", "В моём доме так не играют." };
        static readonly string[] SayAnkle = { "Ай, мои лодыжки!", "Ого, обыграл…", "Красиво, чёрт." };

        void AiSay(string[] arr) { AiSay(arr[UnityEngine.Random.Range(0, arr.Length)]); }
        void AiSay(string s, bool force = false)
        {
            if (!aiTalk || (!force && Time.time < aiTalkAt)) return;
            aiTalkAt = Time.time + 3.5f; HeroToast(aiName + ": " + s);
        }

        string ActorName(int a) { return a == AiId ? aiName : (a == Game.LocalActor() ? "Ты" : Game.PlayerName(a)); }

        // ======================= постройка тела =======================
        void SpawnAi()
        {
            var me = Game.Me();
            if (me == null) { Ui.Toast("Нет персонажа", Ui.Bad); return; }
            if (hoops.Count == 0) { Ui.Toast("Сначала поставь кольцо", Ui.Bad); return; }
            if (aiGo != null) { Ui.Toast("Соперник уже на площадке", Ui.Muted); return; }
            Vector3 fw = FlatForward(); Vector3 p = CenterOf(me) + fw * 3.5f;
            p.y = GroundY(p + Vector3.up * 3f, me.transform.position.y - 0.9f) + 0.05f;
            BuildAi(p); aiYaw = Yaw(-fw);
            if (ballGo == null) EnsureBall(CenterOf(me) + fw * 1.2f);
            TakeBall(me);
            aiHasBall = false; aiShotLive = false; aiInbound = false; aiBusy = false; aiScripted = false; aiSpin = Quaternion.identity; aiVel = Vector3.zero;
            scores[Game.LocalActor()] = 0; scores[AiId] = 0; streak = 0; matchResetAt = -1f; playerGiveAt = aiTakeAt = -1f;
            aiSeen = CenterOf(me); aiPrevPP = aiSeen;
            Ui.Toast(aiName + " (" + LvNames[aiLevel] + ") вышел на площадку. Матч до " + (matchTo > 0 ? matchTo.ToString() : "∞") + ". Мяч у тебя!", Ui.Good);
            AiSay("Ну давай, покажи, что умеешь.", true);
        }

        void BuildAi(Vector3 pos)
        {
            aiGo = new GameObject("PF_ai_player"); aiGo.transform.position = pos;
            aiCc = aiGo.AddComponent<CharacterController>(); aiCc.height = 1.9f; aiCc.radius = 0.32f; aiCc.center = new Vector3(0f, 0.98f, 0f); aiCc.stepOffset = 0.4f; aiCc.skinWidth = 0.04f;
            var mg = new GameObject("PF_ai_player_model"); mg.transform.SetParent(aiGo.transform, false); aiModel = mg.transform;
            Color jersey = new Color(0.82f, 0.1f, 0.12f), shorts = new Color(0.1f, 0.1f, 0.13f), skin = new Color(0.78f, 0.56f, 0.42f), white = new Color(0.95f, 0.95f, 0.95f), hair = new Color(0.08f, 0.06f, 0.05f);
            string n = "PF_ai_player_part";
            PrimGO(PrimitiveType.Capsule, aiModel, new Vector3(0f, 1.3f, 0f), new Vector3(0.46f, 0.34f, 0.27f), jersey, false, n);
            PrimGO(PrimitiveType.Cube, aiModel, new Vector3(0f, 1.3f, -0.14f), new Vector3(0.3f, 0.34f, 0.02f), white, false, n);   // номер на спине
            PrimGO(PrimitiveType.Sphere, aiModel, new Vector3(0f, 1.84f, 0f), new Vector3(0.26f, 0.29f, 0.26f), skin, false, n);
            PrimGO(PrimitiveType.Sphere, aiModel, new Vector3(0f, 1.92f, -0.02f), new Vector3(0.28f, 0.2f, 0.28f), hair, false, n);
            PrimGO(PrimitiveType.Cube, aiModel, new Vector3(0f, 0.98f, 0f), new Vector3(0.46f, 0.3f, 0.28f), shorts, false, n);
            aiLegL = Limb(aiModel, new Vector3(-0.12f, 0.9f, 0f), 0.42f, 0.16f, skin, true, shorts, white);
            aiLegR = Limb(aiModel, new Vector3(0.12f, 0.9f, 0f), 0.42f, 0.16f, skin, true, shorts, white);
            aiArmL = Limb(aiModel, new Vector3(-0.31f, 1.52f, 0f), 0.34f, 0.11f, skin, false, jersey, skin);
            aiArmR = Limb(aiModel, new Vector3(0.31f, 1.52f, 0f), 0.34f, 0.11f, skin, false, jersey, skin);
            AiIgnorePlayer(Game.Me());
        }

        Transform Limb(Transform par, Vector3 pivotPos, float half, float thick, Color col, bool leg, Color topCol, Color endCol)
        {
            var pv = new GameObject("PF_ai_player_pivot"); pv.transform.SetParent(par, false); pv.transform.localPosition = pivotPos;
            string n = "PF_ai_player_part";
            PrimGO(PrimitiveType.Capsule, pv.transform, new Vector3(0f, -half, 0f), new Vector3(thick, half, thick), col, false, n);
            if (leg) PrimGO(PrimitiveType.Cube, pv.transform, new Vector3(0f, -half * 2f + 0.02f, 0.06f), new Vector3(thick * 1.05f, 0.1f, 0.28f), endCol, false, n);
            else
            {
                PrimGO(PrimitiveType.Capsule, pv.transform, new Vector3(0f, -half * 0.35f, 0f), new Vector3(thick * 1.25f, half * 0.4f, thick * 1.25f), topCol, false, n);
                PrimGO(PrimitiveType.Sphere, pv.transform, new Vector3(0f, -half * 2f - 0.02f, 0f), Vector3.one * 0.13f, endCol, false, n);
            }
            return pv.transform;
        }

        void AiIgnorePlayer(Component me)
        {
            if (me == null || aiCc == null) return;
            foreach (var c in me.GetComponentsInChildren<Collider>()) if (c != null) Physics.IgnoreCollision(aiCc, c, true);
        }

        void RemoveAi()
        {
            if (aiGo != null) UnityEngine.Object.Destroy(aiGo);
            aiGo = null; aiCc = null;
            if (aiHasBall && ballGo != null) { aiHasBall = false; ballOwner = -1; ballRb.isKinematic = false; ballCol.enabled = true; Game.SetVelocity(ballRb, Vector3.zero); }
            aiHasBall = false; aiShotLive = false; aiBusy = false; scores.Remove(AiId);
        }

        // ======================= мяч у соперника =======================
        void AiHold()
        {
            if (ballGo == null) return;
            if (ballHeld) { ballHeld = false; shootCharging = false; ballArm = 0; }
            ballRemoteHeld = false; ballOwner = AiId; ballRb.isKinematic = true; ballCol.enabled = false; ballTrail.emitting = false;
            ballRend.enabled = true; aiHasBall = true; aiShotLive = false; shotLive = false; aiShotClock = 0f; aiDribPhase = 0.5f; aiGrabBlendUntil = Time.time + 0.4f;
        }

        void AiRelease(Vector3 pos, Vector3 vel)
        {
            aiHasBall = false; ballGo.transform.position = pos; ballCol.enabled = true; ballRb.isKinematic = false;
            Game.SetVelocity(ballRb, vel); ballRb.angularVelocity = UnityEngine.Random.onUnitSphere * 6f;
            ballOwner = AiId; rimTouched = false; shotLive = false; ballTrail.Clear(); ballTrail.emitting = true;
            if (aiCc != null) StartCoroutine(CoBallIgnoreAi(0.45f));
            SendBall(false);
        }

        IEnumerator CoBallIgnoreAi(float t)
        {
            var cc = aiCc; var bc = ballCol; if (cc == null || bc == null) yield break;
            Physics.IgnoreCollision(bc, cc, true); yield return new WaitForSeconds(t);
            if (cc != null && bc != null) Physics.IgnoreCollision(bc, cc, false);
        }

        void AiGrab(bool inbound, Vector3 spot)
        {
            AiHold(); aiInbound = inbound; aiInboundAt = -1f; aiPlan = 0;
            if (inbound && aiGo != null)
            {
                if (Vector3.Distance(new Vector3(spot.x, 0, spot.z), new Vector3(aiGo.transform.position.x, 0, aiGo.transform.position.z)) > 14f)
                { aiCc.enabled = false; aiGo.transform.position = spot; aiCc.enabled = true; }
            }
        }

        // ======================= главный тик =======================
        void TickAi()
        {
            if (matchResetAt > 0f && Time.time >= matchResetAt) { matchResetAt = -1f; scores[Game.LocalActor()] = 0; if (aiGo != null) scores[AiId] = 0; }
            if (aiGo == null) return;
            var me = Game.Me();
            if (me == null || aiCc == null) return;
            if (hoops.Count == 0) { RemoveAi(); return; }
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f), now = Time.time;
            if (Time.frameCount % 150 == 0) AiIgnorePlayer(me);
            var h = NearestHoop(aiGo.transform.position, 150f) ?? NearestHoop(CenterOf(me), 150f);
            if (h == null) return;
            Vector3 ap = aiGo.transform.position, pp = CenterOf(me), hf = new Vector3(h.rimC.x, ap.y, h.rimC.z);
            aiPVel = Vector3.Lerp(aiPVel, (pp - aiPrevPP) / dt, 0.2f); aiPVel.y = 0f; aiPrevPP = pp;
            aiSeen = Vector3.Lerp(aiSeen, pp, 1f - Mathf.Exp(-LvReact[aiLevel] * dt));
            float sc = Mathf.Max(0.7f, h.scale);
            if (ap.y < h.baseP.y - 30f) { aiCc.enabled = false; aiGo.transform.position = h.baseP + h.fwd * 4f + Vector3.up; aiCc.enabled = true; return; }

            if (playerGiveAt > 0f && now >= playerGiveAt) { playerGiveAt = -1f; AiGiveToPlayer(me); }
            if (aiTakeAt > 0f && now >= aiTakeAt) { aiTakeAt = -1f; AiGrab(true, hf + h.fwd * (ThreeDist * sc + 1.3f)); }
            // мяч улетел за пределы — вернуть в игру
            if (ballGo != null && !ballHeld && !aiHasBall && (Vector3.Distance(ballGo.transform.position, h.rimC) > 70f || ballGo.transform.position.y < h.baseP.y - 25f) && aiTakeAt < 0f && playerGiveAt < 0f) aiTakeAt = now + 0.2f;

            bool pHas = ballHeld;
            bool loose = ballGo != null && !pHas && !aiHasBall && !ballRemoteHeld && now >= aiNoGrabUntil && playerGiveAt < 0f && aiTakeAt < 0f;
            bool stunned = now < aiStunUntil;
            aiWantDir = Vector3.zero; aiWantSpd = 0f; aiGuard = false; aiFaceTo = pp;
            float spdMax = LvSpeed[aiLevel] * (zoneOn ? 0.9f : 1f);

            if (!aiBusy && !stunned)
            {
                if (aiHasBall) AiOffense(h, ap, pp, hf, sc, spdMax, now, dt);
                else if (loose) AiChase(h, ap, pp, hf, sc, spdMax, now);
                else AiDefense(h, ap, pp, hf, sc, spdMax, now, pHas);
            }
            AiApplyMove(ap, dt, stunned);
            AiAnimate(dt, stunned, pHas);
            if (aiHasBall) AiBallFollow(ap, dt, now);
            AiBlockCheck(now);
            AiCheckGoal(now);
            if (aiHasBall && ballOwner == AiId && now >= nextBallSend) SendBall(false);
            else if (aiShotLive && now >= nextBallSend) SendBall(false);
        }

        // ---------- защита ----------
        void AiDefense(HoopObj h, Vector3 ap, Vector3 pp, Vector3 hf, float sc, float spdMax, float now, bool pHas)
        {
            Vector3 P = new Vector3(aiSeen.x, ap.y, aiSeen.z); Vector3 toH = hf - P; float dH = toH.magnitude; Vector3 dirPH = dH > 0.1f ? toH / dH : h.fwd;
            float pSpd = aiPVel.magnitude;
            float gap = Mathf.Clamp(1.0f + pSpd * 0.2f + (dH > ThreeDist * sc ? 0.9f : 0f), 1.0f, 2.9f);
            if (!pHas) gap = 1.8f;
            Vector3 target = P + dirPH * Mathf.Min(gap, Mathf.Max(0.5f, dH - 0.7f)) + aiPVel * 0.12f;
            Vector3 d = target - ap; d.y = 0f; float dist = d.magnitude;
            if (dist > 0.12f) { aiWantDir = d / dist; aiWantSpd = Mathf.Min(spdMax, dist * 5f); }
            aiFaceTo = pp;
            float pd = FlatDist(ap, pp); aiGuard = pd < 4.2f; if (pd < 2.2f) aiWantSpd = Mathf.Min(aiWantSpd, spdMax);
            if (!shootCharging) aiContested = false;
            // шаг навстречу броску: прыжок на блок, пока игрок заряжает бросок
            if (pHas && shootCharging && !aiContested && pd < 3.4f)
            {
                float c = (now - shootT0) / 0.95f;
                if (c > 0.62f) { aiContested = true; if (UnityEngine.Random.value < LvBlock[aiLevel] + 0.25f) AiJumpBlock(); }
            }
            // попытка выбить мяч
            if (pHas && now >= aiStealCd && pd < 1.5f && !aiInbound && Vector3.Dot(aiGo.transform.forward, (pp - ap).normalized) > 0.5f) AiSteal(pp, ap, now);
        }

        float FlatDist(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

        void AiSteal(Vector3 pp, Vector3 ap, float now)
        {
            aiStealCd = now + Mathf.Lerp(1.5f, 0.7f, aiLevel / 3f); aiSwipeUntil = now + 0.3f; Sfx2.Whoosh();
            float p = LvSteal[aiLevel] * (aiPVel.magnitude < 1.5f ? 1.5f : 0.8f) * (zoneOn ? 0.3f : 1f) * (shootCharging ? 1.4f : 1f) * (holdBlend > 0.5f ? 1.3f : 1f);
            if (dribMove != 0 && now < dribMoveEnd) p *= 0.1f;
            if (UnityEngine.Random.value < p && ballHeld)
            {
                Vector3 dir = (ap - pp); dir.y = 0f; dir = dir.normalized;
                ReleaseBall(ballGo.transform.position, dir * 3f + Vector3.up * 3.2f, 0, false);
                aiStatSteals++; Sfx2.Hit(0.5f); HeroToast("У тебя выбили мяч!"); AiSay(SaySteal); aiNoGrabUntil = 0f;
            }
        }

        void AiJumpBlock()
        {
            if (!aiCc.isGrounded || aiBusy) return;
            aiVy = 7.4f; AiPose(2, 0.75f); aiBlockUntil = Time.time + 0.75f;
        }

        // ---------- подбор ----------
        void AiChase(HoopObj h, Vector3 ap, Vector3 pp, Vector3 hf, float sc, float spdMax, float now)
        {
            Vector3 bp = ballGo.transform.position, bv = ballRb != null && !ballRb.isKinematic ? ballRb.velocity : Vector3.zero;
            bool inFlight = (shotLive || aiShotLive) && !rimTouched && bp.y > ap.y + 2.6f;
            Vector3 target;
            if (inFlight) { Vector3 pf = new Vector3(pp.x, 0, pp.z) - new Vector3(hf.x, 0, hf.z); target = hf + pf.normalized * 1.4f * sc; }
            else target = bp + bv * 0.25f;
            Vector3 d = target - ap; d.y = 0f; float dist = d.magnitude;
            if (dist > 0.1f) { aiWantDir = d / dist; aiWantSpd = spdMax; }
            aiFaceTo = bp; aiGuard = false;
            float bd = FlatDist(ap, bp);
            if (!inFlight && bd < 1.3f && bp.y < ap.y + 2.5f && !(ballRb != null && bv.y > 3f && (shotLive || aiShotLive)))
            {
                if (bp.y > ap.y + 1.5f && aiCc.isGrounded) { aiVy = 6.6f; AiPose(2, 0.5f); }
                else { AiGrab(false, ap); AiSay("Мой!"); }
            }
        }

        // ---------- атака ----------
        void AiOffense(HoopObj h, Vector3 ap, Vector3 pp, Vector3 hf, float sc, float spdMax, float now, float dt)
        {
            if (aiInbound)
            {
                Vector3 spot = hf + h.fwd * (ThreeDist * sc + 1.3f); spot.y = ap.y;
                Vector3 dd = spot - ap; dd.y = 0f; float dl = dd.magnitude;
                if (dl > 0.7f) { aiWantDir = dd / dl; aiWantSpd = Mathf.Min(spdMax, dl * 4f); aiInboundAt = -1f; }
                else { if (aiInboundAt < 0f) aiInboundAt = now + 1.0f; else if (now >= aiInboundAt) { aiInbound = false; aiInboundAt = -1f; AiSay("Мяч в игре!"); } }
                aiFaceTo = hf; return;
            }
            aiShotClock += dt;
            Vector3 toH = hf - ap; toH.y = 0f; float D = toH.magnitude; Vector3 dH = D > 0.1f ? toH / D : h.fwd;
            Vector3 toP = pp - ap; toP.y = 0f; float pd = toP.magnitude; float t = Vector3.Dot(toP, dH); float perp = (toP - dH * t).magnitude;
            bool blocked = t > 0.2f && t < D && perp < 1.4f; bool open = pd > 2.3f + (3 - aiLevel) * 0.15f;
            float arc = ThreeDist * sc;
            if (now >= aiDecideAt)
            {
                aiDecideAt = now + 0.25f;
                if (D < 2.9f) aiPlan = (!blocked || aiLevel >= 2) && aiLevel >= 1 ? 2 : 1;
                else if (aiShotClock > 8f && D < arc + 3f) aiPlan = 1;
                else if (D <= arc + 0.3f)
                {
                    if (open && UnityEngine.Random.value < 0.5f + 0.1f * aiLevel) aiPlan = 1;
                    else if (pd < 1.5f && D > 3.8f && aiLevel >= 1 && UnityEngine.Random.value < 0.35f) aiPlan = 3;
                    else aiPlan = 0;
                }
                else aiPlan = open && D < arc + 1.6f && aiLevel >= 1 && UnityEngine.Random.value < 0.45f ? 1 : 0;
            }
            aiFaceTo = hf;
            if (aiPlan == 1 && aiCc.isGrounded) { StartCoroutine(CoAiShoot(h, D < 2.9f)); aiPlan = 0; return; }
            if (aiPlan == 2) { if (D < 3.1f && aiCc.isGrounded) { StartCoroutine(CoAiDunk(h)); aiPlan = 0; return; } }
            if (aiPlan == 3) { StartCoroutine(CoAiStepback(h, -dH)); aiPlan = 0; return; }
            // проход к кольцу с обводкой защитника
            if (blocked && now >= aiJukeUntil) { aiJukeSign = -aiJukeSign; aiJukeUntil = now + 0.9f; }
            Vector3 lateral = Vector3.Cross(Vector3.up, dH) * aiJukeSign;
            Vector3 target = hf - dH * 0.6f + (blocked ? lateral * 2.0f : Vector3.zero);
            Vector3 d = target - ap; d.y = 0f; float dist = d.magnitude;
            if (dist > 0.1f) { aiWantDir = d / dist; aiWantSpd = spdMax * 0.92f; }
            if (blocked && pd < 2.2f && now >= aiCrossAt && aiLevel >= 1)
            {
                aiCrossAt = now + 1.0f; aiSide = -aiSide; aiVel += lateral * (3.5f + aiLevel); Sfx2.Whoosh();
            }
        }

        IEnumerator CoAiStepback(HoopObj h, Vector3 away)
        {
            aiBusy = true; float t = 0f; away.y = 0f; away = away.normalized;
            while (t < 0.26f && aiGo != null && aiHasBall) { t += Time.deltaTime; aiVel = away * 6.5f; aiFaceTo = ballGo.transform.position; yield return null; }
            yield return new WaitForSeconds(0.06f);
            aiBusy = false;
            if (aiGo != null && aiHasBall) StartCoroutine(CoAiShoot(h, false));
        }

        IEnumerator CoAiShoot(HoopObj h, bool layup)
        {
            if (aiBusy && !layup) { }
            aiBusy = true; AiPose(1, 0.95f);
            if (aiCc != null && aiCc.isGrounded) aiVy = layup ? 6.0f : 6.8f;
            float t = 0f;
            while (t < 0.32f) { t += Time.deltaTime; yield return null; if (aiGo == null || !aiHasBall) { aiBusy = false; yield break; } }
            Vector3 ap = aiGo.transform.position; Vector3 s = ap + Vector3.up * 2.15f + aiGo.transform.forward * 0.25f;
            float D = FlatDist(ap, h.rimC); bool three = D >= ThreeDist * h.scale; int pts = three ? 3 : 2;
            var me = Game.Me(); float pd = me != null ? FlatDist(ap, CenterOf(me)) : 9f;
            float contest = pd < 1.2f ? 1.6f : pd < 2.2f ? 0.9f : pd < 3.2f ? 0.4f : 0f;
            float err = LvErr[aiLevel] * h.scale * (0.7f + D / 8f) * (1f + contest);
            Vector2 o = UnityEngine.Random.insideUnitCircle * err;
            Vector3 tgt = h.rimC + new Vector3(o.x, 0f, o.y);
            float rise = layup ? 0.8f : Mathf.Lerp(1.0f, 2.2f, Mathf.InverseLerp(2f, 14f, D)); float T;
            Vector3 v = SolveVel(s, tgt, rise, ShotG(), out T);
            aiStatShots++; AiRelease(s, v); aiShotPts = pts; aiShotLive = true; aiShotUntil = Time.time + 7f; aiBallPrev = s;
            StartCoroutine(CoGuide(tgt, T, h)); Sfx2.Whoosh(); AiPose(1, 0.45f);
            Logger.LogInfo("[PeakFun] соперник бросает: дистанция " + D.ToString("0.0") + " м, ошибка " + err.ToString("0.00") + " м, давление " + contest.ToString("0.0"));
            yield return new WaitForSeconds(0.5f);
            aiBusy = false;
        }

        IEnumerator CoAiDunk(HoopObj h)
        {
            aiBusy = true; aiScripted = true; aiCc.enabled = false;
            int style = UnityEngine.Random.Range(0, 4); bool hang = UnityEngine.Random.value < 0.25f;
            Vector3 start = aiGo.transform.position; Vector3 fromRim = start - h.rimC; fromRim.y = 0f; fromRim = fromRim.sqrMagnitude < 0.01f ? h.fwd : fromRim.normalized;
            Vector3 tgt = new Vector3(h.rimC.x, h.rimC.y - 2.0f * Mathf.Max(0.8f, h.scale), h.rimC.z) + fromRim * 0.7f * Mathf.Max(0.8f, h.scale);
            float T = 0.78f + (style == 3 ? 0.15f : 0f), arcH = 0.7f + (style == 3 ? 0.9f : 0f), t = 0f;
            AiPose(style == 2 ? 5 : 3, T + 1.3f); AiSay("Смотри!", true); Sfx2.Whoosh();
            while (t < T && aiGo != null && aiHasBall)
            {
                t += Time.deltaTime; float k = Mathf.Clamp01(t / T); aiPoseK = k;
                Vector3 pos = Vector3.Lerp(start, tgt, Mathf.SmoothStep(0f, 1f, k)) + Vector3.up * (arcH * Mathf.Sin(Mathf.PI * k));
                aiGo.transform.position = pos; aiYaw = Yaw(-fromRim); aiGo.transform.rotation = Quaternion.Euler(0f, aiYaw, 0f);
                float sk = Mathf.InverseLerp(0.15f, 0.8f, k);
                aiSpin = style == 1 ? Quaternion.AngleAxis(360f * sk, Vector3.up) : style == 3 ? Quaternion.AngleAxis(360f * sk, Vector3.right) : Quaternion.identity;
                aiVel = Vector3.zero; yield return null;
            }
            if (aiGo != null && aiHasBall)
            {
                foreach (var hc in h.cols) if (hc != null && ballCol != null) Physics.IgnoreCollision(ballCol, hc, true);
                AiRelease(new Vector3(h.rimC.x, h.rimC.y + 0.26f * h.scale, h.rimC.z), Vector3.down * 8f);
                StartCoroutine(CoRestoreRim(h));
                h.shake = 2f; Sfx2.Board(); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.5f); Vfx.Sparks(h.rimC, Vector3.down, 14);
                AiGoal(h, 2, false, true);
                if (hang) { float t2 = 0f; while (t2 < 0.7f && aiGo != null) { t2 += Time.deltaTime; h.shake = Mathf.Max(h.shake, 1.2f); aiGo.transform.position = tgt + Vector3.down * 0.05f * Mathf.Sin(t2 * 10f); yield return null; } }
            }
            aiSpin = Quaternion.identity; aiScripted = false; aiVy = 0f;
            if (aiCc != null) aiCc.enabled = true;
            aiBusy = false;
        }

        // ======================= гол соперника =======================
        void AiCheckGoal(float now)
        {
            if (ballGo == null || ballRb == null) return;
            Vector3 p = ballGo.transform.position;
            if (!aiShotLive || ballRb.isKinematic) { aiBallPrev = p; return; }
            foreach (var h in hoops)
            {
                if (h.root == null) continue;
                if (aiBallPrev.y > h.rimC.y && p.y <= h.rimC.y && now - lastGoalT > 1.2f)
                {
                    float f = (aiBallPrev.y - h.rimC.y) / Mathf.Max(0.0001f, aiBallPrev.y - p.y); Vector3 pc = Vector3.Lerp(aiBallPrev, p, f);
                    float d = Vector2.Distance(new Vector2(pc.x, pc.z), new Vector2(h.rimC.x, h.rimC.z));
                    if (d < h.rimR - 0.03f * h.scale && ballRb.velocity.y < 0f) { lastGoalT = now; AiGoal(h, aiShotPts > 0 ? aiShotPts : 2, !rimTouched, false); break; }
                }
            }
            if (now > aiShotUntil) aiShotLive = false;
            aiBallPrev = p;
        }

        void AiGoal(HoopObj h, int pts, bool clean, bool dunk)
        {
            aiShotLive = false; streak = 0; aiStatMakes++; AddScore(AiId, pts);
            if (Game.InRoom) Net.Send("bb", "g;" + AiId + "," + pts + "," + h.id + "," + (clean ? 1 : 0));
            GoalFx(h, AiId, pts, clean, false, dunk);
            if (dunk) BigText(aiName + ": " + (UnityEngine.Random.value < 0.5f ? "ДАНК!" : "ПОСТЕР!"), new Color(1f, 0.4f, 0.2f));
            var v = ballRb.velocity; Game.SetVelocity(ballRb, new Vector3(v.x * 0.3f, v.y * 0.5f, v.z * 0.3f));
            AiSay(SayScore); aiNoGrabUntil = Time.time + 2.2f; playerGiveAt = Time.time + 1.7f;
            if (!CheckMatchEnd(AiId)) { }
        }

        /// <summary>Игрок забил — мяч уходит к сопернику (вбрасывание).</summary>
        void AiOnPlayerScored()
        {
            if (aiGo == null) return;
            AiSay(SayLose); aiNoGrabUntil = Time.time + 1.8f; aiTakeAt = Time.time + 1.5f;
            CheckMatchEnd(Game.LocalActor());
        }

        bool CheckMatchEnd(int who)
        {
            if (matchTo <= 0 || aiGo == null) return false;
            int a; scores.TryGetValue(who, out a);
            if (a < matchTo) return false;
            int mine, his; scores.TryGetValue(Game.LocalActor(), out mine); scores.TryGetValue(AiId, out his);
            BigText(who == AiId ? "ПОРАЖЕНИЕ  " + mine + ":" + his : "ПОБЕДА!  " + mine + ":" + his, who == AiId ? new Color(1f, 0.35f, 0.3f) : new Color(0.4f, 1f, 0.5f));
            AiSay(who == AiId ? "Вот так и играют профи." : "Реванш? Я не против.", true);
            matchResetAt = Time.time + 4f; return true;
        }

        void AiGiveToPlayer(Component me)
        {
            if (ballGo == null) EnsureBall(CenterOf(me) + FlatForward());
            aiHasBall = false; aiShotLive = false; TakeBall(me); HeroToast("Твой мяч — начинай атаку");
        }

        // ======================= игрок ↔ соперник =======================
        /// <summary>Игрок делает финт: соперник может «купиться» и споткнуться.</summary>
        void AiOnPlayerMove(int kind)
        {
            if (aiGo == null || aiBusy) return;
            var me = Game.Me(); if (me == null) return;
            float pd = FlatDist(aiGo.transform.position, CenterOf(me));
            if (pd > 2.8f || aiHasBall) return;
            float bite = LvBite[aiLevel] * (kind == 5 ? 0.8f : kind == 3 ? 0.9f : kind == 4 ? 1.1f : 1f);
            if (UnityEngine.Random.value < bite)
            {
                aiStunUntil = Time.time + 1.0f; aiVel = Vector3.zero; BigText("ЛОДЫЖКИ!", new Color(1f, 0.85f, 0.2f)); AiSay(SayAnkle); Sfx2.Hit(0.3f);
            }
        }

        void AiOnPlayerFake()
        {
            if (aiGo == null) return;
            var me = Game.Me(); if (me == null) return;
            if (aiBlockUntil > Time.time || FlatDist(aiGo.transform.position, CenterOf(me)) < 3.4f)
                if (UnityEngine.Random.value < LvBite[aiLevel] + 0.15f) { if (aiCc.isGrounded) { aiVy = 7.2f; AiPose(2, 0.7f); } aiStunUntil = Time.time + 0.55f; HeroToast("Купился на финт!"); }
        }

        /// <summary>Множитель ошибки для броска игрока: защитник рядом мешает.</summary>
        float AiContest(Component me)
        {
            if (aiGo == null) return 0f;
            Vector3 ap = aiGo.transform.position, pp = CenterOf(me); float pd = FlatDist(ap, pp);
            var h = NearestHoop(pp, 80f); if (h == null) return 0f;
            Vector3 toH = h.rimC - pp; toH.y = 0f; Vector3 toA = ap - pp; toA.y = 0f;
            if (Vector3.Dot(toH.normalized, toA.normalized) < 0.2f) return 0f;
            float c = pd < 1.0f ? 1.1f : pd < 2.0f ? 0.7f : pd < 3.0f ? 0.3f : 0f;
            if (aiPose == 2 && Time.time < aiPoseUntil) c *= 1.5f;
            return c * (0.6f + 0.25f * aiLevel);
        }

        void PlayerStealFromAi(Component me)
        {
            if (!aiHasBall || aiGo == null) return;
            float d = FlatDist(aiGo.transform.position, CenterOf(me));
            if (d > 2.3f) { HeroToast("Слишком далеко для перехвата"); return; }
            if (aiInbound) { HeroToast("Соперник вводит мяч — подожди"); return; }
            float ph = aiDribPhase; float low = (ph < 0.2f || ph > 0.8f) ? 1.6f : 1f;
            float p = 0.3f * low * (Time.time < aiStunUntil ? 3f : 1f) * (aiLevel == 0 ? 1.4f : aiLevel == 1 ? 1f : aiLevel == 2 ? 0.75f : 0.55f);
            if (aiBusy) p *= 0.5f;
            if (UnityEngine.Random.value < p)
            {
                aiHasBall = false; TakeBall(me); aiStealCd = Time.time + 1.2f; BigText("ПЕРЕХВАТ!", new Color(0.4f, 0.9f, 1f)); Sfx2.Hit(0.5f); AiSay("Эй!");
            }
            else { HeroToast("Не вышло — соперник прикрыл мяч"); Sfx2.Whoosh(); }
        }

        void AiBlockCheck(float now)
        {
            if (aiPose != 2 || now > aiPoseUntil || !shotLive || ballGo == null || ballHeld || ballRb == null || ballRb.isKinematic) return;
            Vector3 hr = aiArmR.position + aiArmR.rotation * Vector3.down * 0.76f, hl = aiArmL.position + aiArmL.rotation * Vector3.down * 0.76f; Vector3 b = ballGo.transform.position;
            if (Vector3.Distance(b, hr) < 0.6f || Vector3.Distance(b, hl) < 0.6f)
            {
                Vector3 dir = b - aiGo.transform.position; dir.y = 0f; dir = dir.sqrMagnitude < 0.01f ? -aiGo.transform.forward : dir.normalized;
                Game.SetVelocity(ballRb, dir * 7f + Vector3.up * 2.5f + UnityEngine.Random.insideUnitSphere * 1.5f);
                shotLive = false; guideOn = false; aiPoseUntil = 0f; Sfx2.Hit(0.9f); BigText("БЛОК!", new Color(1f, 0.35f, 0.3f)); AiSay(SayBlock); aiStatBlocks++;
                CamShake.Boost = Mathf.Max(CamShake.Boost, 0.35f); Vfx.Sparks(b, dir, 12); streak = 0; aiNoGrabUntil = 0f;
            }
        }

        // ======================= движение / анимация =======================
        void AiPose(int p, float dur) { aiPose = p; aiPoseUntil = Time.time + dur; }

        void AiApplyMove(Vector3 ap, float dt, bool stunned)
        {
            if (aiScripted) return;
            Vector3 wantV = (aiBusy || stunned) ? Vector3.zero : aiWantDir * aiWantSpd;
            float acc = aiWantSpd > 0.1f ? 24f : 32f; if (aiBusy && aiCc.isGrounded == false) acc = 2f;
            Vector3 hv = new Vector3(aiVel.x, 0f, aiVel.z); hv = Vector3.MoveTowards(hv, wantV, acc * dt); aiVel = hv;
            aiVy -= 24f * dt; if (aiCc.isGrounded && aiVy < 0f) aiVy = -2f;
            Vector3 before = aiGo.transform.position;
            aiCc.Move((aiVel + Vector3.up * aiVy) * dt);
            Vector3 mv = aiGo.transform.position - before; mv.y = 0f;
            // застрял — подпрыгнуть/обойти
            if (aiWantSpd > 2f && mv.magnitude < aiWantSpd * dt * 0.25f) { aiStuckT += dt; if (aiStuckT > 0.6f) { aiStuckT = 0f; if (aiCc.isGrounded) aiVy = 7f; aiJukeSign = -aiJukeSign; } } else aiStuckT = 0f;
            Vector3 fl = aiFaceTo - aiGo.transform.position; fl.y = 0f;
            if (fl.sqrMagnitude > 0.04f) aiYaw = Mathf.MoveTowardsAngle(aiYaw, Yaw(fl), 520f * dt);
            aiGo.transform.rotation = Quaternion.Euler(0f, aiYaw, 0f);
        }

        void AimLimb(Transform limb, Vector3 worldDir, float k)
        {
            if (limb == null || worldDir.sqrMagnitude < 0.0001f) return;
            limb.rotation = Quaternion.Slerp(limb.rotation, Quaternion.FromToRotation(Vector3.down, worldDir.normalized), k);
        }

        void AiAnimate(float dt, bool stunned, bool pHas)
        {
            float now = Time.time; Vector3 hv = new Vector3(aiVel.x, 0f, aiVel.z); float spd = hv.magnitude;
            bool grounded = aiCc != null && (aiCc.isGrounded || aiScripted == false && aiVy <= 0f && aiVy > -3f);
            bool airborne = aiScripted || (aiCc != null && !aiCc.isGrounded);
            aiPhase += dt * (3f + spd * 1.9f);
            float amp = Mathf.Clamp(spd * 9f, 0f, 55f), sw = Mathf.Sin(aiPhase);
            aiStunK = Mathf.MoveTowards(aiStunK, stunned ? 1f : 0f, dt * (stunned ? 5f : 2.5f));
            float lean = Mathf.Clamp(spd * 2.4f, 0f, 16f) - 70f * aiStunK;
            bool defStance = !aiHasBall && spd < 5f;
            aiModel.localRotation = aiSpin * Quaternion.Euler(lean, 0f, 0f);
            aiModel.localPosition = new Vector3(0f, (defStance ? -0.07f : 0f) - 0.35f * aiStunK, 0f);
            float k = 1f - Mathf.Exp(-16f * dt);
            if (airborne) { aiLegL.localRotation = Quaternion.Slerp(aiLegL.localRotation, Quaternion.Euler(-34f, 0f, -4f), k); aiLegR.localRotation = Quaternion.Slerp(aiLegR.localRotation, Quaternion.Euler(-10f, 0f, 4f), k); }
            else
            {
                float spread = defStance ? 13f : 3f;
                aiLegL.localRotation = Quaternion.Slerp(aiLegL.localRotation, Quaternion.Euler(sw * amp, 0f, -spread), k);
                aiLegR.localRotation = Quaternion.Slerp(aiLegR.localRotation, Quaternion.Euler(-sw * amp, 0f, spread), k);
            }
            if (aiPose != 0 && now > aiPoseUntil) aiPose = 0;
            Vector3 fw = aiGo.transform.forward, up = Vector3.up, side = aiGo.transform.right; float ka = 1f - Mathf.Exp(-20f * dt);
            Vector3 ball = ballGo != null ? ballGo.transform.position : aiGo.transform.position + fw;
            if (stunned) { AimLimb(aiArmL, -side * 0.9f + up * 0.7f, ka); AimLimb(aiArmR, side * 0.9f + up * 0.7f, ka); return; }
            if (now < aiSwipeUntil) { float sk = 1f - (aiSwipeUntil - now) / 0.3f; AimLimb(aiArmR, Vector3.Slerp(side * 0.9f + fw * 0.1f, fw * 0.9f - up * 0.3f, sk), ka); AimLimb(aiArmL, -side * 0.7f + fw * 0.4f + up * 0.2f, ka); return; }
            switch (aiPose)
            {
                case 1: AimLimb(aiArmR, fw * 0.45f + up * 0.9f + side * 0.1f, ka); AimLimb(aiArmL, fw * 0.45f + up * 0.9f - side * 0.1f, ka); return;
                case 2: AimLimb(aiArmR, up * 1f + fw * 0.12f + side * 0.1f, ka); AimLimb(aiArmL, up * 1f + fw * 0.12f - side * 0.1f, ka); return;
                case 3: AimLimb(aiArmR, up * 0.95f + fw * 0.3f, ka); AimLimb(aiArmL, up * 0.3f - side * 0.8f + fw * 0.2f, ka); return;
                case 5: { float a = aiPoseK * Mathf.PI * 2f; AimLimb(aiArmR, up * Mathf.Cos(a) + fw * Mathf.Sin(a), ka); AimLimb(aiArmL, up * 0.3f - side * 0.8f, ka); return; }
            }
            if (aiHasBall)
            {
                bool right = aiSide >= 0f; Transform dr = right ? aiArmR : aiArmL, orr = right ? aiArmL : aiArmR;
                AimLimb(dr, ball - dr.position + up * 0.1f, ka); AimLimb(orr, fw * 0.5f + (right ? -side : side) * 0.6f - up * 0.3f, ka);
            }
            else if (aiGuard)
            {
                AimLimb(aiArmR, fw * 0.4f + side * 0.85f + up * 0.35f + side * sw * 0.1f, ka); AimLimb(aiArmL, fw * 0.4f - side * 0.85f + up * 0.35f - side * sw * 0.1f, ka);
            }
            else
            {
                AimLimb(aiArmR, Quaternion.AngleAxis(-sw * amp * 0.8f, side) * (-up) + side * 0.1f, ka); AimLimb(aiArmL, Quaternion.AngleAxis(sw * amp * 0.8f, side) * (-up) - side * 0.1f, ka);
            }
        }

        Vector3 AiHand(Transform arm) { return arm.position + arm.rotation * Vector3.down * 0.76f; }

        void AiBallFollow(Vector3 ap, float dt, float now)
        {
            if (ballGo == null) return;
            Vector3 target;
            if (aiPose == 1 || aiPose == 3 || aiPose == 5 || aiBusy)
            {
                Vector3 hr = AiHand(aiArmR), hl = AiHand(aiArmL);
                target = aiPose == 1 ? (hr + hl) * 0.5f + Vector3.up * 0.14f : (aiPose == 3 || aiPose == 5 ? hr + Vector3.up * 0.1f : ballGo.transform.position);
            }
            else
            {
                Vector3 fw = aiGo.transform.forward, side = aiGo.transform.right; float spd = new Vector2(aiVel.x, aiVel.z).magnitude;
                float period = Mathf.Lerp(0.5f, 0.32f, Mathf.Clamp01(spd / 7f));
                aiDribPhase += dt / period;
                Vector3 hc = ap + side * 0.38f * aiSide + fw * (0.4f + Mathf.Clamp(spd * 0.04f, 0f, 0.25f)) + new Vector3(aiVel.x, 0, aiVel.z) * 0.04f;
                float gy = GroundY(hc + Vector3.up * 1f, ap.y), r = 0.15f * hoopScale, top = ap.y + 1.0f, A = Mathf.Max(0.2f, top - gy - r);
                if (aiDribPhase >= 1f) { aiDribPhase -= 1f; if (!aiBusy) { Sfx2.Bounce(0.3f); Vfx.Dust(new Vector3(hc.x, gy, hc.z), 0.12f, 1); } }
                A *= Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(spd / 7f));
                target = new Vector3(hc.x, gy + r + A * 4f * aiDribPhase * (1f - aiDribPhase), hc.z);
            }
            ballGo.transform.position = now < aiGrabBlendUntil ? Vector3.Lerp(ballGo.transform.position, target, 1f - Mathf.Exp(-14f * dt)) : target;
            ballGo.transform.Rotate(60f * dt, 0f, 0f, Space.World);
        }

        // ======================= HUD =======================
        void DrawAiTag()
        {
            if (aiGo == null || Event.current.type != EventType.Repaint) return;
            var cam = Camera.main; if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(aiGo.transform.position + Vector3.up * 2.45f); if (sp.z < 0.5f || sp.z > 60f) return;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f), w = 220f * s; float y = Screen.height - sp.y;
            Ui.Text(new Rect(sp.x - w * 0.5f, y - 28f * s, w, 24f * s), aiName + "  ·  " + LvNames[aiLevel], (int)(15f * s), new Color(1f, 0.55f, 0.5f), TextAnchor.MiddleCenter, true);
            if (aiHasBall) Ui.Text(new Rect(sp.x - w * 0.5f, y - 8f * s, w, 20f * s), "с мячом", (int)(12f * s), new Color(1f, 0.8f, 0.4f), TextAnchor.MiddleCenter, true);
        }

        void AiTab()
        {
            Ui.Header("Соперник (умный ИИ)");
            Ui.Para("Живой соперник 1×1: играет в защите (держится между тобой и кольцом, читает движение), перехватывает мяч, прыгает на блок, когда ты заряжаешь бросок, подбирает отскок, атакует проходом, кроссовером, броском с дуги, данком и шагом назад. Финты ([T] между ног, [Y] за спиной, [U] разворот, [J] финт, двойной [Q]) заставляют его спотыкаться; ПКМ во время заряда броска — финт броском. Виден только тебе.", 12);
            if (Ui.Button(aiGo == null ? "Вызвать соперника" : "Убрать соперника", aiGo == null ? 1 : 2)) { if (aiGo == null) Safe("соперник", SpawnAi); else RemoveAi(); }
            string[] lv = { "Новичок", "Игрок", "Звезда", "Легенда" };
            int sel = Ui.Buttons(lv, 4, 1, 34f, true, aiLevel, 14); if (sel >= 0) { aiLevel = sel; if (aiGo != null) Ui.Toast("Сложность: " + LvNames[aiLevel], Ui.Muted); }
            matchTo = Mathf.RoundToInt(Ui.Slider("Матч до очков (0 — без конца)", matchTo, 0, 21, "0", ""));
            aiTalk = Ui.Toggle("Соперник болтает (подколки)", aiTalk);
        }
    }
}
