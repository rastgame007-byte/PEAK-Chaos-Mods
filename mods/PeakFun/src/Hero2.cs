// Герои 1.9, часть 2: анимации рук/взлёта, новый захват за горло (рука тянется к шее, плавное ведение, прижим к стене,
// удар о землю, взлёт и сброс), след удара у брошенного, чужие лазерные лучи.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        public static PeakFunPlugin Instance;

        bool heroArmsOn = true, boomed, grabBusy, grabBelow;
        int armMode; float armW, punchT0 = -10f, nextBeamSend, pinCool, nextStruggle; Vector3 armDir, grabSmooth; int grabFails;

        // ---------- вспомогательное ----------
        static Type charType2;
        static bool WorldCast(Vector3 origin, float radius, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default(RaycastHit);
            if (charType2 == null) charType2 = R.FindType("Character");
            var hits = Physics.SphereCastAll(origin, radius, dir, dist, ~0, QueryTriggerInteraction.Ignore);
            float bd = float.MaxValue; bool got = false;
            foreach (var h in hits)
            {
                if (h.collider == null || h.distance <= 0.001f) continue;
                if (charType2 != null && h.collider.GetComponentInParent(charType2) != null) continue;
                if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue;   // предметы стеной не считаем
                if (h.distance < bd) { bd = h.distance; best = h; got = true; }
            }
            return got;
        }

        static int Ping()
        {
            object o; if (R.TryInvokeStatic(Game.NetType, "GetPing", out o) && o is int i) return i; return 60;
        }

        // ---------- анимация рук (позиции звеньев руки ведём сами; костюм рисуется по ним) ----------
        void PoseArm(Rigidbody sh, Rigidbody el, Rigidbody ha, Vector3 d, float reach, float w, Vector3 vel)
        {
            if (sh == null || ha == null) return;
            Vector3 s = sh.position;
            float l1 = el != null ? Vector3.Distance(s, el.position) : 0.3f, l2 = el != null ? Vector3.Distance(el.position, ha.position) : 0.55f;
            l1 = Mathf.Clamp(l1, 0.1f, 0.8f); l2 = Mathf.Clamp(l2, 0.1f, 0.8f);
            d.Normalize();
            if (el != null) { el.position = Vector3.Lerp(el.position, s + d * l1, w); el.velocity = vel; el.angularVelocity = Vector3.zero; }
            Vector3 hg = s + d * (l1 + l2) * reach;
            ha.position = Vector3.Lerp(ha.position, hg, w); ha.velocity = vel; ha.angularVelocity = Vector3.zero;
        }

        void HeroArms(Component me)
        {
            if (!heroArmsOn || (hero == HeroKind.None && grabbed == null && ballArm == 0)) { armW = 0f; armMode = 0; return; }
            var pm = PartMap.Of(me); if (pm == null || !pm.limbs) return;
            Vector3 up = pm.head.position - pm.hip.position; up = up.sqrMagnitude < 0.01f ? Vector3.up : up.normalized;
            Vector3 fw = FlatForward(); Vector3 side = Vector3.Cross(up, fw).normalized;
            float spd = flyVel.magnitude; Vector3 fd = spd > 2f ? flyVel / spd : CamForward();
            int mode = 0; Vector3 dir = fd;
            float pt = Time.time - punchT0, at = Time.time - atkT0;
            bool grabArm = grabbed != null && !grabTele;
            if (Time.time < animUntil) mode = animMode;
            else if (atkType != 0 && at < (atkType == 4 ? 0.7f : 0.5f)) mode = 8;
            else if (Time.time < ramUntil) mode = 9;
            else if (pt >= 0f && pt < 0.65f && !grabArm) mode = 4;
            else if (grabArm) mode = 5;
            else if (charging) mode = 10;
            else if (guardOn && hero != HeroKind.None) mode = 11;
            else if (grabbed != null && grabTele) { mode = 1; dir = CamForward(); }
            else if (Time.time < landPoseUntil && hero != HeroKind.None) mode = 7;
            else if (hero == HeroKind.Invincible && flying) mode = spd > 4f ? 2 : 7;
            else if (hero == HeroKind.Patriot && flying) mode = spd > 6f ? 3 : 6;
            else if (hero == HeroKind.Phantom && flying && spd > 3f) mode = 1;
            else if (hero == HeroKind.Mind && flying) mode = 3;
            else if (hero == HeroKind.Gojo && flying) mode = 7;
            if (ballArm != 0 && (mode == 0 || mode == 7 || mode == 11)) mode = ballArm;
            if (armMode != mode) armMode = mode;
            float dt = Time.fixedDeltaTime;
            armW = Mathf.MoveTowards(armW, mode == 0 ? 0f : 1f, dt * (mode == 4 || mode == 8 ? 14f : 6f));
            if (armW < 0.03f) return;
            Vector3 vel = pm.hip.velocity; float w = Mathf.Clamp01(armW * 0.7f);
            Vector3 down = -up + side * 0.1f;
            Vector3 fwd3 = CamForward();
            switch (mode)
            {
                case 1: PoseArm(pm.armL, pm.elbL, pm.handL, dir, 1f, w, vel); PoseArm(pm.armR, pm.elbR, pm.handR, dir, 1f, w, vel); break;
                case 2: PoseArm(pm.armR, pm.elbR, pm.handR, dir, 1f, w, vel); PoseArm(pm.armL, pm.elbL, pm.handL, -up - side * 0.1f, 1f, w, vel); break;
                case 3: PoseArm(pm.armL, pm.elbL, pm.handL, -up - side * 0.12f, 1f, w, vel); PoseArm(pm.armR, pm.elbR, pm.handR, down, 1f, w, vel); break;
                case 4:
                    {
                        Vector3 ad = armDir.sqrMagnitude < 0.01f ? fw : armDir.normalized; Vector3 pd; float r;
                        if (pt < 0.14f) { pd = (-ad * 0.7f + side * 0.35f + up * 0.1f).normalized; r = 0.8f; }
                        else if (pt < 0.3f) { pd = ad; r = 1.12f; }
                        else { pd = Vector3.Slerp(ad, -up, Mathf.Clamp01((pt - 0.3f) / 0.35f)); r = 1f; }
                        PoseArm(pm.armR, pm.elbR, pm.handR, pd, r, 1f, vel);
                        PoseArm(pm.armL, pm.elbL, pm.handL, (-up * 0.7f + ad * 0.5f - side * 0.2f).normalized, 1f, w, vel);
                        break;
                    }
                case 5: NeckGrip(pm, up, fw, side, vel, w); break;
                case 6:   // руки скрещены на груди (Хоумлендер завис в воздухе)
                    PoseArm(pm.armL, pm.elbL, pm.handL, fw * 0.55f + side * 0.55f - up * 0.35f, 0.7f, w, vel);
                    PoseArm(pm.armR, pm.elbR, pm.handR, fw * 0.55f - side * 0.55f - up * 0.35f, 0.7f, w, vel); break;
                case 7:   // героическая стойка: руки чуть в стороны
                    PoseArm(pm.armL, pm.elbL, pm.handL, -up * 0.9f - side * 0.45f, 1f, w, vel);
                    PoseArm(pm.armR, pm.elbR, pm.handR, -up * 0.9f + side * 0.45f, 1f, w, vel); break;
                case 8:
                    {
                        Vector3 ad = atkDir.sqrMagnitude < 0.01f ? fw : atkDir.normalized;
                        bool useL = grabArm ? true : atkType == 1;
                        Vector3 sd = useL ? -side : side; float wind;
                        Vector3 wdir, sdir;
                        switch (atkType)
                        {
                            case 1: wdir = -ad * 0.6f + sd * 0.25f + up * 0.1f; sdir = ad; wind = 0.09f; break;
                            case 2: wdir = sd * 0.9f - ad * 0.25f; sdir = ad - sd * 0.15f; wind = 0.09f; break;
                            case 3: wdir = -up * 0.8f - ad * 0.3f + sd * 0.2f; sdir = ad + up * 0.8f; wind = 0.15f; break;
                            default: wdir = up * 0.9f - ad * 0.3f; sdir = ad - up * 0.6f; wind = 0.13f; break;
                        }
                        wdir.Normalize(); sdir.Normalize();
                        Vector3 d; float r;
                        if (at < wind) { d = wdir; r = 0.8f; }
                        else if (at < wind + 0.12f) { float k = (at - wind) / 0.12f; d = Vector3.Slerp(wdir, sdir, k); r = Mathf.Lerp(0.8f, 1.15f, k); }
                        else { float k = Mathf.Clamp01((at - wind - 0.12f) / 0.3f); d = Vector3.Slerp(sdir, -up, k); r = Mathf.Lerp(1.15f, 1f, k); }
                        if (useL) PoseArm(pm.armL, pm.elbL, pm.handL, d, r, 1f, vel); else PoseArm(pm.armR, pm.elbR, pm.handR, d, r, 1f, vel);
                        if (grabArm) NeckGripRight(pm, up, fw, vel);
                        else
                        {
                            Vector3 od = atkType == 3 ? -ad * 0.5f - up * 0.4f : ad * 0.5f + up * 0.25f - sd * 0.3f;
                            if (useL) PoseArm(pm.armR, pm.elbR, pm.handR, od, 0.75f, w, vel); else PoseArm(pm.armL, pm.elbL, pm.handL, od, 0.75f, w, vel);
                        }
                        break;
                    }
                case 9:
                    PoseArm(pm.armL, pm.elbL, pm.handL, atkDir + side * 0.04f, 1f, 1f, vel); PoseArm(pm.armR, pm.elbR, pm.handR, atkDir - side * 0.04f, 1f, 1f, vel); break;
                case 10:  // заряд тяжёлого удара
                    PoseArm(pm.armR, pm.elbR, pm.handR, (-fwd3 * 0.9f + up * 0.25f + side * 0.2f).normalized, 0.8f, 1f, vel);
                    PoseArm(pm.armL, pm.elbL, pm.handL, (fwd3 * 0.7f + up * 0.3f - side * 0.2f).normalized, 0.9f, w, vel); break;
                case 12:  // расширение: одна рука вверх, вторая со знаком перед грудью
                    if (hero == HeroKind.Gojo)   // скрещённые пальцы перед лицом
                    {
                        PoseArm(pm.armR, pm.elbR, pm.handR, (fw * 0.8f + up * 0.55f - side * 0.06f).normalized, 0.55f, w, vel);
                        PoseArm(pm.armL, pm.elbL, pm.handL, (fw * 0.8f + up * 0.5f + side * 0.1f).normalized, 0.5f, w, vel);
                    }
                    else if (hero == HeroKind.Sukuna)   // руки широко: «моё»
                    {
                        PoseArm(pm.armR, pm.elbR, pm.handR, (side * -0.95f + up * 0.35f).normalized, 1.1f, w, vel);
                        PoseArm(pm.armL, pm.elbL, pm.handL, (side * 0.95f + up * 0.35f).normalized, 1.1f, w, vel);
                    }
                    else
                    {
                        PoseArm(pm.armR, pm.elbR, pm.handR, (up * 0.9f + fw * 0.3f + side * 0.15f).normalized, 1f, w, vel);
                        PoseArm(pm.armL, pm.elbL, pm.handL, (fw * 0.8f + up * 0.35f + side * 0.3f).normalized, 0.55f, w, vel);
                    }
                    break;
                case 13:  // техника двумя руками вперёд (Синий/Красный/Луч)
                    PoseArm(pm.armR, pm.elbR, pm.handR, (fwd3 + side * 0.08f).normalized, 1f, w, vel);
                    PoseArm(pm.armL, pm.elbL, pm.handL, (fwd3 * 0.9f - side * 0.25f + up * 0.1f).normalized, 0.9f, w, vel); break;
                case 14:  // взмах рукой (клинки)
                    {
                        float ph = Mathf.Sin(Time.time * 20f);
                        PoseArm(pm.armR, pm.elbR, pm.handR, (fwd3 + side * 0.45f * ph + up * 0.2f * ph).normalized, 1.1f, w, vel);
                        PoseArm(pm.armL, pm.elbL, pm.handL, (fwd3 * 0.6f - side * 0.5f - up * 0.2f).normalized, 0.7f, w, vel); break;
                    }
                case 15:  // натяжение «лука» Фуги
                    PoseArm(pm.armR, pm.elbR, pm.handR, (-fwd3 * 0.8f + side * 0.5f + up * 0.1f).normalized, 0.7f, w, vel);
                    PoseArm(pm.armL, pm.elbL, pm.handL, fwd3, 1f, w, vel); break;
                case 16:  // ведение мяча: рука следует за мячом
                    {
                        Rigidbody sh = dribArmR ? pm.armR : pm.armL; Rigidbody el = dribArmR ? pm.elbR : pm.elbL, ha = dribArmR ? pm.handR : pm.handL;
                        Rigidbody osh = dribArmR ? pm.armL : pm.armR, oel = dribArmR ? pm.elbL : pm.elbR, oha = dribArmR ? pm.handL : pm.handR;
                        if (ballGo != null && sh != null)
                        {
                            Vector3 bd = ballGo.transform.position + up * 0.12f - sh.position; float dl = bd.magnitude;
                            PoseArm(sh, el, ha, bd, Mathf.Clamp(dl / 0.85f, 0.35f, 1.1f), 1f, vel);
                        }
                        PoseArm(osh, oel, oha, (fw * 0.55f - side * (dribArmR ? 0.55f : -0.55f) - up * 0.15f).normalized, 0.7f, w, vel);
                        break;
                    }
                case 17:  // зарядка броска: мяч над головой двумя руками
                    PoseArm(pm.armR, pm.elbR, pm.handR, (fw * 0.55f + up * 0.85f + side * 0.12f).normalized, 0.9f, 1f, vel);
                    PoseArm(pm.armL, pm.elbL, pm.handL, (fw * 0.55f + up * 0.85f - side * 0.12f).normalized, 0.9f, 1f, vel); break;
                case 18:  // кисти вслед за мячом после броска
                    PoseArm(pm.armR, pm.elbR, pm.handR, (fw * 0.8f + up * 0.6f).normalized, 1.1f, 1f, vel);
                    PoseArm(pm.armL, pm.elbL, pm.handL, (fw * 0.8f + up * 0.55f - side * 0.1f).normalized, 1.05f, 1f, vel); break;
                case 20:  // данк: правая рука с мячом вверх, левая в сторону
                    PoseArm(pm.armR, pm.elbR, pm.handR, (up * 0.95f + fw * 0.3f).normalized, 1.1f, 1f, vel);
                    PoseArm(pm.armL, pm.elbL, pm.handL, (up * 0.3f - side * 0.8f + fw * 0.2f).normalized, 1f, w, vel); break;
                case 21:  // данк-мельница: правая рука с мячом описывает круг
                    {
                        float ang = dunkPhase * Mathf.PI * 2f;
                        PoseArm(pm.armR, pm.elbR, pm.handR, (up * Mathf.Cos(ang) + fw * Mathf.Sin(ang)).normalized, 1.1f, 1f, vel);
                        PoseArm(pm.armL, pm.elbL, pm.handL, (up * 0.4f - side * 0.8f + fw * 0.2f).normalized, 1f, w, vel); break;
                    }
                case 22:  // данк двумя руками
                    PoseArm(pm.armR, pm.elbR, pm.handR, (up * 0.9f + fw * 0.35f + side * 0.12f).normalized, 1.1f, 1f, vel);
                    PoseArm(pm.armL, pm.elbL, pm.handL, (up * 0.9f + fw * 0.35f - side * 0.12f).normalized, 1.1f, 1f, vel); break;
                case 23:  // томагавк: обе руки с мячом назад-вверх, затем рубящий удар вперёд-вниз
                    {
                        Vector3 dd = dunkPhase < 0.62f ? (-fw * 0.5f + up * 0.9f) : (dunkPhase < 0.82f ? (fw * 0.1f + up * 0.9f) : (fw * 0.7f - up * 0.8f));
                        PoseArm(pm.armR, pm.elbR, pm.handR, (dd + side * 0.12f).normalized, 1.1f, 1f, vel);
                        PoseArm(pm.armL, pm.elbL, pm.handL, (dd - side * 0.12f).normalized, 1.1f, 1f, vel); break;
                    }
                case 24:  // «размах»: руки широко в стороны, в конце — вверх с мячом
                    {
                        float kk = Mathf.InverseLerp(0.55f, 0.85f, dunkPhase);
                        PoseArm(pm.armR, pm.elbR, pm.handR, Vector3.Slerp((side * 0.95f + up * 0.35f).normalized, (up * 0.95f + fw * 0.3f).normalized, kk), 1.1f, 1f, vel);
                        PoseArm(pm.armL, pm.elbL, pm.handL, (-side * 0.95f + up * Mathf.Lerp(0.35f, 0.2f, kk)).normalized, 1.1f, 1f, vel); break;
                    }
                case 25:  // мяч у груди двумя руками
                    if (ballGo != null)
                    {
                        if (pm.armR != null) { Vector3 bd = ballGo.transform.position + side * 0.1f - pm.armR.position; PoseArm(pm.armR, pm.elbR, pm.handR, bd, Mathf.Clamp(bd.magnitude / 0.85f, 0.35f, 1.1f), 1f, vel); }
                        if (pm.armL != null) { Vector3 bd = ballGo.transform.position - side * 0.1f - pm.armL.position; PoseArm(pm.armL, pm.elbL, pm.handL, bd, Mathf.Clamp(bd.magnitude / 0.85f, 0.35f, 1.1f), 1f, vel); }
                    }
                    break;
                case 11:  // защитная стойка
                    PoseArm(pm.armL, pm.elbL, pm.handL, fw * 0.7f + up * 0.5f + side * 0.25f, 0.7f, w, vel);
                    PoseArm(pm.armR, pm.elbR, pm.handR, fw * 0.7f + up * 0.5f - side * 0.25f, 0.7f, w, vel); break;
            }
            // ноги в полёте: Супермен — вытянуты назад, зависание — вместе вниз
            if (flying && hero != HeroKind.None && hero != HeroKind.Brute && (mode == 2 || mode == 3 || mode == 6 || mode == 9 || mode == 1 || mode == 7))
            {
                bool fast = spd > 4f; Vector3 ld = fast ? -fd : -up + fw * 0.08f;
                PoseArm(pm.legL, pm.kneeL, pm.footL, ld - side * 0.05f, 1f, w * 0.8f, vel);
                PoseArm(pm.legR, pm.kneeR, pm.footR, ld + side * 0.05f, 1f, w * 0.8f, vel);
            }
        }

        void NeckGripRight(PartMap pm, Vector3 up, Vector3 fw, Vector3 vel)
        {
            Vector3 target = grabSmooth + up * 0.2f; Vector3 dd = target - pm.armR.position;
            PoseArm(pm.armR, pm.elbR, pm.handR, dd.sqrMagnitude < 0.01f ? fw : dd, Mathf.Clamp(dd.magnitude / 0.85f, 0.4f, 1.15f), 1f, vel);
        }

        void NeckGrip(PartMap pm, Vector3 up, Vector3 fw, Vector3 side, Vector3 vel, float w)
        {
            NeckGripRight(pm, up, fw, vel);
            PoseArm(pm.armL, pm.elbL, pm.handL, -up - side * 0.1f, 1f, w, vel);
        }

        // ---------- взлёт и звуковой барьер ----------
        IEnumerator CoTakeoff(Component me)
        {
            Vector3 gp = me.transform.position, gn = Vector3.up; RaycastHit h;
            if (Physics.Raycast(CenterOf(me), Vector3.down, out h, 3f, ~0, QueryTriggerInteraction.Ignore)) { gp = h.point; gn = h.normal; Vfx.Dust(gp + gn * 0.1f, 3f, 22); }
            Vfx.Ring(gp + gn * 0.1f, gn, new Color(1f, 1f, 1f, 0.9f), 6f, 0.6f);
            Dash(Vector3.up, 15f, 0.3f); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.5f); Sfx2.Whoosh();
            yield return null;
        }

        void SonicBoom(Component me)
        {
            Vector3 c = CenterOf(me), d = flyVel.normalized;
            Vfx.Ring(c, d, new Color(1f, 1f, 1f, 0.95f), 10f, 0.8f); Vfx.Ring(c - d * 1.2f, d, new Color(0.7f, 0.85f, 1f, 0.7f), 7f, 0.6f);
            Sfx2.Boom(); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.8f); Shade.Set(ShadeKind.Speed, 1.4f);
            HeroToast("Звуковой барьер!");
        }

        // ---------- захват за горло ----------
        void ToggleGrab()
        {
            var me = Game.Me(); if (me == null) return;
            if (grabbed != null) { if (!grabBusy) ReleaseGrab(true); return; }
            var t = LookedVictim(4.8f, 0.35f);
            if (t == null) { Ui.Toast("Рядом никого нет — подойди к другу ближе и смотри на него", Ui.Bad); return; }
            grabTele = false; GrabStart(me, t);
        }

        void GrabStart(Component me, Component t)
        {
            grabbed = t; GrabInit(t); nextGrabSend = 0f; nextFallSend = 0f; grabSmooth = CenterOf(t); grabBelow = false; grabBusy = false; grabFails = 0; pinCool = 0f;
            if (!grabTele) { try { Game.Rpc(me, "RPCA_StartReaching"); } catch { } }
            Sfx2.Whoosh();
            HeroToast(grabTele ? "Телекинез: " + Game.Name(t) + "   [" + keyThrow.Value + "] швырнуть" :
                "Схватил за горло: " + Game.Name(t) + "   [" + keyAb1.Value + "] удар о землю · [" + keyAb2.Value + "] вверх и сброс · [" + keyThrow.Value + "] бросок · [" + keyGrab.Value + "] отпустить");
        }

        void ReleaseGrab(bool toast)
        {
            if (grabbed == null) return;
            var me = Game.Me();
            if (me != null && !grabTele) { try { Game.Rpc(me, "RPCA_StopReaching"); } catch { } }
            string n = Game.Name(grabbed);
            grabbed = null; grabBusy = false; grabBelow = false;
            if (toast) HeroToast("Отпустил " + n);
        }

        void ThrowGrabbed()
        {
            var me = Game.Me(); if (me == null || grabbed == null || grabBusy) return;
            var t = grabbed; Vector3 f = CamForward();
            ReleaseGrab(false);
            Rpc(t, "RPCA_Fall", 4f);
            Rpc(t, "RPCA_AddForceToBodyPart", 0, (f + Vector3.up * 0.2f).normalized * launchForce * 1.6f * heroPower);
            CamShake.Boost = 0.6f; Sfx2.Whoosh(); punchT0 = Time.time; armDir = f;
            TrackImpact(t, 4f);
            HeroToast("Швырнул " + Game.Name(t) + "!");
        }

        // ---------- плавное ведение схваченного ----------
        bool grabPhys = true; float grabStiff = 22f; float gMass = 60f; int gIdx; Vector3 gLast, gVel; bool gFresh; float gBadT;

        void GrabInit(Component t)
        {
            gMass = 0f; gIdx = 0; int i = 0; var pm = PartMap.Of(t);
            foreach (var rb in Game.Rigs(t)) { if (rb != null) { gMass += rb.mass; if (pm != null && rb == pm.hip) gIdx = i; } i++; }
            if (gMass < 5f) gMass = 60f;
            gLast = CenterOf(t); gVel = Vector3.zero; gFresh = false; gBadT = 0f;
        }

        void GrabTick(Component me)
        {
            if (grabbed == null) return;
            if (Game.IsDead(grabbed)) { ReleaseGrab(false); return; }
            var cam = Camera.main; if (cam == null) return;
            var pm = PartMap.Of(me);
            Vector3 from = CenterOf(me), head = HeadOf(me);
            float sc = Mathf.Max(0.5f, me.transform.lossyScale.y);
            Vector3 hold;
            if (grabTele) hold = cam.transform.position + cam.transform.forward * grabDist;
            else if (grabBelow) hold = from + Vector3.down * 1.1f * sc;
            else hold = head - Vector3.up * 0.15f * sc + FlatForward() * 0.95f * sc;
            bool pinned = false;
            Vector3 dd = hold - from; float dl = dd.magnitude;
            if (!grabTele && dl > 0.1f)
            {
                RaycastHit hit;
                if (WorldCast(from, 0.3f, dd / dl, dl + 0.25f, out hit))
                {
                    pinned = true; hold = hit.point + hit.normal * 0.5f;
                    float sp = pm != null && pm.hip != null ? Vector3.Dot(pm.hip.velocity, -hit.normal) : flyVel.magnitude;
                    if (sp > 11f && Time.time > pinCool && !grabBusy) PinImpact(hit.point, hit.normal, sp);
                }
            }
            grabSmooth = Vector3.Lerp(grabSmooth, hold, 1f - Mathf.Exp(-24f * Time.deltaTime));
            Vector3 vc = CenterOf(grabbed);
            if (Time.deltaTime > 0f) gVel = Vector3.Lerp(gVel, (vc - gLast) / Time.deltaTime, 0.25f);
            gLast = vc;
            if (Time.time >= nextFallSend) { nextFallSend = Time.time + 2.2f; Game.Rpc(grabbed, "RPCA_Fall", 3f); }

            Vector3 err = grabSmooth - vc; float el = err.magnitude;
            bool phys = grabPhys && !grabTele;
            if (phys)
            {
                // физический захват: тянем силой с демпфированием, телепорт — только если улетел далеко
                if (gFresh || el > 3.4f)
                {
                    if (Time.time >= nextGrabSend)
                    {
                        nextGrabSend = Time.time + 0.2f;
                        string err2 = Game.Rpc(grabbed, "WarpPlayerRPC", grabSmooth, false);
                        if (err2 != null) { grabFails++; if (grabFails > 8) { Ui.Toast("Телепорт не работает: " + err2, Ui.Bad); ReleaseGrab(false); return; } } else { grabFails = 0; gFresh = false; }
                        if (el < 3.4f) gFresh = false;
                    }
                }
                else if (Time.time >= nextGrabSend)
                {
                    nextGrabSend = Time.time + 0.04f;
                    Vector3 vHold = pinned ? Vector3.zero : (pm != null && pm.hip != null ? pm.hip.velocity : flyVel);
                    float kp = grabStiff, kd = 1.8f * Mathf.Sqrt(kp);
                    Vector3 a = err * kp + (vHold - gVel) * kd + Vector3.up * 9.81f;
                    a = Vector3.ClampMagnitude(a, 85f);
                    Vector3 F = a * (gMass / 0.5f);              // сила действует один шаг физики из двух (25 Гц против 50 Гц)
                    F = Vector3.ClampMagnitude(F, launchForce * 0.45f);
                    Game.Rpc(grabbed, "RPCA_AddForceToBodyPart", gIdx, F);
                }
                // страховка: если физика ведёт себя странно — возвращаемся к телепорту
                if (el > 5.5f || gVel.magnitude > 60f) { gBadT += Time.deltaTime; if (gBadT > 0.5f) { grabPhys = false; Ui.Toast("Физический захват не удержал — включён запасной (телепорт). Можно вернуть в «Герои»", Ui.Bad); } }
                else gBadT = Mathf.Max(0f, gBadT - Time.deltaTime);
            }
            else
            {
                float lead = Mathf.Clamp(Ping() * 0.0006f, 0.03f, 0.18f);
                Vector3 pos = grabSmooth + (pinned ? Vector3.zero : flyVel * lead);
                if (Time.time >= nextGrabSend)
                {
                    nextGrabSend = Time.time + 0.045f;
                    string err2 = Game.Rpc(grabbed, "WarpPlayerRPC", pos, false);
                    if (err2 != null) { grabFails++; if (grabFails > 8) { Ui.Toast("Телепорт не работает: " + err2, Ui.Bad); ReleaseGrab(false); return; } } else grabFails = 0;
                }
            }
            if (!grabTele && Time.time >= nextStruggle)
            {
                nextStruggle = Time.time + UnityEngine.Random.Range(0.35f, 0.7f);
                Game.Rpc(grabbed, "RPCA_AddForceToBodyPart", UnityEngine.Random.Range(0, 14), UnityEngine.Random.onUnitSphere * launchForce * 0.02f);
            }
        }

        void PinImpact(Vector3 point, Vector3 normal, float speed)
        {
            pinCool = Time.time + 0.9f;
            float pw = Mathf.Clamp(speed / 14f, 0.9f, 2.4f);
            Gore.Burst(point + normal * 0.25f, normal, pw * 1.3f);
            Gore.Decal(point, normal, null, 1.1f * pw, 1f, false, false, 80f);
            Gore.Impact(point, normal, pw * 0.8f);
            Destruct.Smash(point, -normal, 2.2f * pw, pw);
            if (grabbed != null) Game.Rpc(grabbed, "RPCA_Fall", 4f);
            CamShake.Boost = 1f; Sfx2.Boom(); HeroToast("Впечатал в стену!");
        }

        IEnumerator CoGrabSlam(Component me)
        {
            if (grabbed == null) yield break;
            grabBusy = true; grabBelow = true;
            Dash(Vector3.down, 48f, 1.5f); Sfx2.Whoosh();
            float t0 = Time.time; yield return new WaitForSeconds(0.2f);
            RaycastHit gh = default(RaycastHit); bool hit = false;
            while (Time.time - t0 < 1.7f)
            {
                hit = Physics.Raycast(CenterOf(me), Vector3.down, out gh, 2.2f * Mathf.Max(1f, me.transform.lossyScale.y), ~0, QueryTriggerInteraction.Ignore);
                if (hit && Time.time - t0 > 0.25f) break;
                yield return null;
            }
            dashUntil = 0f;
            Vector3 gp = hit ? gh.point : me.transform.position, gn = hit ? gh.normal : Vector3.up;
            var v = grabbed;
            if (v != null)
            {
                Gore.Burst(gp + gn * 0.4f, gn + FlatForward() * 0.3f, 2.4f);
                Gore.Impact(gp, gn, 1.8f);
                Destruct.Smash(gp, Vector3.up, 3.5f, 1.8f);
                Game.Rpc(v, "RPCA_Fall", 6f);
                ReleaseGrab(false);
            }
            Shockwave(gp, 9f, 0.7f);
            HeroToast("Удар о землю!");
            grabBusy = false; grabBelow = false;
        }

        IEnumerator CoGrabSky(Component me)
        {
            if (grabbed == null) yield break;
            grabBusy = true;
            Dash(Vector3.up, 32f, 1.2f); Sfx2.Whoosh(); Shade.Set(ShadeKind.Speed, 1.2f);
            yield return new WaitForSeconds(1.2f);
            var v = grabbed; grabBusy = false;
            if (v != null)
            {
                ReleaseGrab(false);
                Rpc(v, "RPCA_Fall", 6f);
                Rpc(v, "RPCA_AddForceToBodyPart", 0, Vector3.down * launchForce * 1.9f * heroPower);
                Dash(Vector3.up, 6f, 0.2f);
                TrackImpact(v, 5f);
                HeroToast("Сброс с высоты!");
            }
        }

        // ---------- след удара у брошенного ----------
        void TrackImpact(Component v, float maxT) { StartCoroutine(CoTrack(v, maxT)); }

        IEnumerator CoTrack(Component v, float maxT)
        {
            Vector3 last = CenterOf(v); float t0 = Time.time;
            yield return new WaitForSeconds(0.1f);
            while (Time.time - t0 < maxT && v != null)
            {
                Vector3 cur = CenterOf(v), d = cur - last; float dist = d.magnitude;
                if (dist > 0.03f)
                {
                    RaycastHit h;
                    if (WorldCast(last, 0.3f, d / dist, dist + 0.45f, out h) && dist / 0.05f > 9f)
                    {
                        ImpactFx(h.point, h.normal, dist / 0.05f);
                        yield break;
                    }
                }
                last = cur;
                yield return new WaitForSeconds(0.05f);
            }
        }

        void ImpactFx(Vector3 point, Vector3 normal, float speed)
        {
            float pw = Mathf.Clamp(speed / 14f, 0.8f, 2.4f);
            Gore.Burst(point + normal * 0.15f, normal, pw * 1.2f);
            Gore.Decal(point, normal, null, 1f * pw, 1f, false, false, 80f);
            if (normal.y > 0.5f) Gore.Impact(point, normal, pw * 0.8f); else Vfx.Dust(point + normal * 0.1f, 1.5f, 10);
            CamShake.Boost = Mathf.Max(CamShake.Boost, 0.7f); Sfx2.Boom(); HeroToast("Бам!");
        }

        // ---------- чужие лазерные лучи ----------
        internal sealed class RBeam { public Vector3 end; public float t; public GameObject go; public LineRenderer lr; }
        internal static readonly Dictionary<int, RBeam> remoteBeam = new Dictionary<int, RBeam>();

        void OnBeam(string arg, int sender)
        {
            if (!costumeSync) return;
            try
            {
                var p = arg.Split(',');
                if (p[0] == "0") { RBeam o; if (remoteBeam.TryGetValue(sender, out o)) { if (o.go != null) Destroy(o.go); remoteBeam.Remove(sender); } return; }
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                RBeam b; if (!remoteBeam.TryGetValue(sender, out b)) remoteBeam[sender] = b = new RBeam();
                b.end = new Vector3(float.Parse(p[1], inv), float.Parse(p[2], inv), float.Parse(p[3], inv)); b.t = Time.time;
            }
            catch { }
        }

        void TickRemoteBeams()
        {
            if (remoteBeam.Count == 0) return;
            List<int> rm = null;
            foreach (var kv in remoteBeam)
            {
                var b = kv.Value; var c = Game.ByActor(kv.Key);
                if (c == null || Time.time - b.t > 0.7f) { if (rm == null) rm = new List<int>(); rm.Add(kv.Key); continue; }
                if (b.go == null)
                {
                    b.go = new GameObject("PF_RBeam"); b.lr = b.go.AddComponent<LineRenderer>(); b.lr.positionCount = 2;
                    SetupLine(b.lr, new Color(1f, 0.1f, 0.05f), 0.09f);
                }
                Vector3 head = HeadOf(c); var data = R.Get(c, "data"); Vector3 look = R.Get<Vector3>(data, "lookDirection"); if (look.sqrMagnitude < 0.01f) look = c.transform.forward;
                b.lr.SetPosition(0, head + look.normalized * 0.2f + Vector3.up * 0.03f); b.lr.SetPosition(1, b.end);
                float w = 0.07f + 0.03f * Mathf.Sin(Time.time * 60f); b.lr.startWidth = w; b.lr.endWidth = w * 0.7f;
            }
            if (rm != null) foreach (var k in rm) { RBeam o; if (remoteBeam.TryGetValue(k, out o) && o.go != null) Destroy(o.go); remoteBeam.Remove(k); }
        }
    }
}
