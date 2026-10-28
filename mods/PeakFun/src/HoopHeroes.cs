// Герои «Баскетбола Крис Пол» 1.13: Винс Картер, Аллен Айверсон, Коби Брайант, Стивен Карри, Крис Пол, Мэджик Джонсон, Шакил О'Нил + «Горячая рука» (Зона) на клавишу G.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        enum Hb { Leap, Meteor, Roar, Formless, Drive, Ankle, Copy, Steal, Deep, Lucky, High, Vanish, Ignite, VDrive, Block, Eye }

        internal static bool IsHoop(HeroKind k) { return k >= HeroKind.Kagami; }

        static Hb[] HoopAbs(HeroKind k)
        {
            switch (k)
            {
                case HeroKind.Kagami: return new[] { Hb.Leap, Hb.Meteor, Hb.Roar };
                case HeroKind.Aomine: return new[] { Hb.Formless, Hb.Drive, Hb.Ankle };
                case HeroKind.Kise: return new[] { Hb.Copy, Hb.Steal, Hb.Block };
                case HeroKind.Midorima: return new[] { Hb.Deep, Hb.Lucky, Hb.High };
                case HeroKind.Kuroko: return new[] { Hb.Vanish, Hb.Ignite, Hb.VDrive };
                case HeroKind.Akashi: return new[] { Hb.Eye, Hb.Ankle, Hb.Steal };
                default: return new[] { Hb.Meteor, Hb.Block, Hb.Roar };
            }
        }

        static string HbName(HeroKind k, Hb a)
        {
            if (k == HeroKind.Murasakibara && a == Hb.Meteor) return "Тяжёлый данк";
            if (k == HeroKind.Akashi && a == Hb.Ankle) return "Подсечка";
            switch (a)
            {
                case Hb.Leap: return "Прыжок"; case Hb.Meteor: return "Данк с высоты"; case Hb.Roar: return "Рёв"; case Hb.Formless: return "Бросок без стойки";
                case Hb.Drive: return "Прорыв"; case Hb.Ankle: return "Подсечка"; case Hb.Copy: return "Копия приёма"; case Hb.Steal: return "Перехват";
                case Hb.Deep: return "Бросок с центра"; case Hb.Lucky: return "Талисман"; case Hb.High: return "Высокая дуга"; case Hb.Vanish: return "Невидимка";
                case Hb.Ignite: return "Быстрый пас"; case Hb.VDrive: return "Резкий драйв"; case Hb.Block: return "Блок"; default: return "Видение площадки";
            }
        }

        static float HbCd(Hb a)
        {
            switch (a)
            {
                case Hb.Leap: return 3f; case Hb.Meteor: return 9f; case Hb.Roar: return 14f; case Hb.Formless: return 2.5f; case Hb.Drive: return 5f; case Hb.Ankle: return 9f;
                case Hb.Copy: return 6f; case Hb.Steal: return 6f; case Hb.Deep: return 3.5f; case Hb.Lucky: return 14f; case Hb.High: return 5f; case Hb.Vanish: return 14f;
                case Hb.Ignite: return 4.5f; case Hb.VDrive: return 9f; case Hb.Block: return 8f; default: return 20f;
            }
        }

        static string HoopAbName(HeroKind k, int slot) { return HbName(k, HoopAbs(k)[slot]); }

        static string ZoneName(HeroKind k)
        {
            switch (k)
            {
                case HeroKind.Kagami: return "ГОРЯЧАЯ РУКА"; case HeroKind.Aomine: return "ГОРЯЧАЯ РУКА"; case HeroKind.Kise: return "ГОРЯЧАЯ РУКА";
                case HeroKind.Midorima: return "ГОРЯЧАЯ РУКА"; case HeroKind.Kuroko: return "ГОРЯЧАЯ РУКА"; case HeroKind.Akashi: return "ГОРЯЧАЯ РУКА";
                default: return "ГОРЯЧАЯ РУКА";
            }
        }

        static Color ZoneColor(HeroKind k)
        {
            switch (k)
            {
                case HeroKind.Kagami: return new Color(1f, 0.2f, 0.1f); case HeroKind.Aomine: return new Color(0.3f, 0.55f, 1f); case HeroKind.Kise: return new Color(1f, 0.85f, 0.2f);
                case HeroKind.Midorima: return new Color(0.2f, 0.9f, 0.45f); case HeroKind.Kuroko: return new Color(0.55f, 0.9f, 1f); case HeroKind.Akashi: return new Color(1f, 0.1f, 0.2f);
                default: return new Color(0.7f, 0.35f, 1f);
            }
        }

        static string HoopInfo(HeroKind k)
        {
            string tail = " [G] — ГОРЯЧАЯ РУКА (Зона): дверь открывается, броски без промаха, скорость и прыжок выше, мяч сам липнет к рукам. [R] взять мяч, ЛКМ — бросок, Пробел+ЛКМ — данк, ПКМ — пас, [Q] — кроссовер.";
            switch (k)
            {
                case HeroKind.Kagami: return "Мощный форвард. [A] — прыжок тигра. [B] — Данк с высоты с огромной дистанции (нужно кольцо). [H] — рёв: волна и ускорение." + tail;
                case HeroKind.Aomine: return "Уличный стиль. [A] — бросок без формы: мгновенно, из любого положения. [B] — прорыв: рывок с мячом, сбивает на пути. [H] — подсечка." + tail;
                case HeroKind.Kise: return "Копирует приёмы. [A] — идеальная копия: случайный приём других асов. [B] — перехват мяча. [H] — блок." + tail;
                case HeroKind.Midorima: return "Снайпер трёхочковых. [A] — бросок с центра поля (никогда не промахивается, камера летит за мячом). [B] — лаки-предмет: случайный бафф. [H] — небесный бросок." + tail;
                case HeroKind.Kuroko: return "Мастер передач. [A] — исчезновение (невидим для друзей). [B] — игнайт-пас: молниеносная передача, сбивает на пути. [H] — исчезающий драйв." + tail;
                case HeroKind.Akashi: return "Видит всю площадку. [A] — глаз императора: 10 с идеальных бросков и магнит мяча. [B] — подсечка. [H] — перехват." + tail;
                default: return "Гигант ×1.5. [A] — громовой молот (данк с ударной волной). [B] — блок: отбивает мяч и всех рядом. [H] — рёв." + tail;
            }
        }

        // ---------- состояние ----------
        bool zoneOn, zoneCasting; float zoneEnd, cdZone, zoneDur = 15f, zoneCd = 45f, nextZoneFx; GameObject zoneAura; bool zoneApplied; int buffGen;
        readonly float[] hoopTot = { 3f, 8f, 10f };
        sealed class RZ { public HeroKind kind; public float until; public float nextFx; }
        readonly Dictionary<int, RZ> remoteFlow = new Dictionary<int, RZ>();

        internal static bool ZoneOn(Component c)
        {
            var me = Game.Me(); var pl = Instance; if (pl == null) return false;
            if (c == me) return pl.zoneOn || pl.zoneCasting;
            RZ z; return pl.remoteFlow.TryGetValue(Game.ActorOf(c), out z) && Time.time < z.until;
        }

        // ---------- каст способностей ----------
        void HoopCast(int slot)
        {
            var me = Game.Me(); if (me == null || !IsHoop(hero)) return;
            Hb a = HoopAbs(hero)[slot];
            float until = slot == 0 ? cdA : slot == 1 ? cdB : cdC;
            if (Time.time < until) { HeroToast(HbName(hero, a) + ": перезарядка " + Mathf.CeilToInt(until - Time.time) + " с"); return; }
            if (zoneCasting || dunkBusy) return;
            if (!DoHb(a, me, 0)) return;
            float cd = HbCd(a) * (zoneOn ? 0.55f : 1f); hoopTot[slot] = cd;
            if (slot == 0) cdA = Time.time + cd; else if (slot == 1) cdB = Time.time + cd; else cdC = Time.time + cd;
        }

        bool NeedBall(Component me)
        {
            if (ballHeld) return true;
            if (ballRemoteHeld) { HeroToast("Мяч у другого игрока — сначала перехвати"); return false; }
            TakeBall(me); return true;
        }

        bool DoHb(Hb a, Component me, int depth)
        {
            Vector3 fw = FlatForward(), up = Vector3.up; HeroKind hk = hero;
            switch (a)
            {
                case Hb.Leap:
                    {
                        Vector3 v = up * 22f + fw * 7f; foreach (var rb in Game.Rigs(me)) Game.SetVelocity(rb, v);
                        Vfx.Ring(me.transform.position + up * 0.1f, up, new Color(1f, 0.3f, 0.1f, 0.9f), 4f, 0.4f); Sfx2.Whoosh(); Shade.Set(ShadeKind.Speed, 0.4f); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.5f);
                        return true;
                    }
                case Hb.Meteor:
                    {
                        var h = NearestHoop(CenterOf(me), zoneOn ? 32f : 24f);
                        if (h == null) { HeroToast("Нужно кольцо рядом (до 24 м): вкладка «Герои → Баскетбол»"); return false; }
                        if (!NeedBall(me)) return false;
                        StartCoroutine(CoDunk(h, true, me)); return true;
                    }
                case Hb.Roar:
                    Shockwave(CenterOf(me), 9f, 0.9f); Sfx2.Boom(); Shade.Set(ShadeKind.Power, 0.8f);
                    StartCoroutine(CoBuff(1.5f, 1.2f, 5f)); HeroToast("РРРРР!"); return true;
                case Hb.Formless:
                    {
                        if (!NeedBall(me)) return false;
                        Shoot(me, 0.1f, 0f, true, 0); HeroToast("Бросок без стойки!"); Shade.Set(ShadeKind.Speed, 0.25f); return true;
                    }
                case Hb.Drive:
                    Dash(fw + up * 0.05f, 30f, 0.45f); StartCoroutine(CoDriveHit(me, 0.45f)); Sfx2.Whoosh(); Shade.Set(ShadeKind.Speed, 0.6f);
                    if (ballHeld) { dribSide = -dribSide; crossUntil = Time.time + 0.4f; }
                    StartCine(4, 0.7f); return true;
                case Hb.Ankle:
                    {
                        int n = 0; var cam = Camera.main; Vector3 side = Vector3.Cross(up, fw).normalized;
                        foreach (var v in Victims())
                        {
                            Vector3 to = CenterOf(v) - CenterOf(me); if (to.magnitude > 8f || Vector3.Dot(fw, to.normalized) < 0.2f) continue;
                            Rpc(v, "RPCA_Fall", 2.6f); Vfx.Sparks(CenterOf(v), up, 16); n++;
                            if (ballRemoteHeld && ballOwner == Game.ActorOf(v)) TryStealAt(me, ballOwner, true);
                        }
                        Dash(side * (UnityEngine.Random.value < 0.5f ? 1f : -1f) + fw * 0.4f, 18f, 0.22f); Sfx2.Whoosh(); HeroToast(n > 0 ? "ПОДСЕЧКА! Упали: " + n : "Подсечка… никого рядом");
                        return true;
                    }
                case Hb.Copy:
                    {
                        if (depth > 0) return false;
                        var pool = new List<Hb> { Hb.Formless, Hb.Deep, Hb.Meteor, Hb.Drive };
                        for (int i = 0; i < 4 && pool.Count > 0; i++)
                        {
                            Hb p = pool[UnityEngine.Random.Range(0, pool.Count)]; pool.Remove(p);
                            if (DoHb(p, me, 1)) { HeroToast("Копия приёма: «" + HbName(HeroKind.Aomine, p) + "»"); Shade.Set(ShadeKind.Glitch, 0.4f); return true; }
                        }
                        return false;
                    }
                case Hb.Steal: return HbSteal(me);
                case Hb.Deep:
                case Hb.High:
                    {
                        var h = AimHoop(me, 200f);
                        if (h == null) { HeroToast("Посмотри на кольцо — бросок наведётся"); return false; }
                        if (!NeedBall(me)) return false;
                        float d = Vector3.Distance(new Vector3(CenterOf(me).x, 0, CenterOf(me).z), new Vector3(h.rimC.x, 0, h.rimC.z));
                        float rise = a == Hb.High ? 18f : Mathf.Clamp(2f + 0.22f * d, 3f, 22f);
                        float T = Shoot(me, 0f, rise, true, d > ThreeDist * h.scale ? 3 : 2);
                        StartCine(7, T + 0.7f); HeroToast(a == Hb.High ? "ВЫСОКАЯ ДУГА!" : "Бросок с центра — не промахнётся!");
                        return true;
                    }
                case Hb.Lucky:
                    {
                        int r = UnityEngine.Random.Range(0, 4);
                        if (r == 0) { StartCoroutine(CoBuff(2f, 1f, 8f)); HeroToast("Талисман: ботинки скорости ×2 (8 с)"); }
                        else if (r == 1) { StartCoroutine(CoBuff(1f, 2f, 8f)); HeroToast("Талисман: пружины, прыжок ×2 (8 с)"); }
                        else if (r == 2) { perfectUntil = Time.time + 15f; HeroToast("Талисман: идеальные броски 15 с"); }
                        else { magnetUntil = Time.time + 15f; HeroToast("Талисман: магнит для мяча 15 с"); }
                        AnimVfx.Flash(CenterOf(me), new Color(0.3f, 1f, 0.5f, 0.6f), 3f, 0.6f); Sfx2.Fanfare(); return true;
                    }
                case Hb.Vanish:
                    StartCoroutine(CoVanish(7f, false)); StartCoroutine(CoBuff(1.3f, 1f, 7f)); return true;
                case Hb.Ignite:
                    if (!NeedBall(me)) return false;
                    Pass(me, true); StartCine(3, 0.9f); Shade.Set(ShadeKind.Laser, 0.3f); return true;
                case Hb.VDrive:
                    ballHideUntil = Time.time + 1.3f; StartCoroutine(CoVanish(1.3f, true));
                    Dash(fw + up * 0.05f, 26f, 0.45f); StartCoroutine(CoDriveHit(me, 0.45f)); Sfx2.Whoosh(); return true;
                case Hb.Block:
                    {
                        Shockwave(CenterOf(me), 5.5f, 0.5f);
                        if (ballGo != null && Vector3.Distance(ballGo.transform.position, CenterOf(me)) < 9f)
                        {
                            if (ballRemoteHeld) TryStealAt(me, ballOwner, true);
                            else
                            {
                                if (ballHeld) DropBall(me, 0f);
                                ballOwner = Game.LocalActor(); ballRb.isKinematic = false; ballCol.enabled = true;
                                Vector3 away = ballGo.transform.position - CenterOf(me); away.y = 0f; away = away.sqrMagnitude < 0.01f ? fw : away.normalized;
                                Game.SetVelocity(ballRb, away * 15f + Vector3.up * 2f); ballTrail.emitting = true; SendBall(false);
                            }
                        }
                        HeroToast("БЛОК!"); return true;
                    }
                default:   // Eye
                    perfectUntil = Time.time + 10f; magnetUntil = Time.time + 10f;
                    AnimVfx.Flash(CenterOf(me), new Color(1f, 0.1f, 0.15f, 0.6f), 5f, 0.8f); Shade.Set(ShadeKind.Rage, 1.0f); Sfx2.Zap();
                    HeroToast("Видение площадки: 10 с идеальных бросков"); return true;
            }
        }

        bool HbSteal(Component me)
        {
            if (ballGo == null) { HeroToast("Нет мяча — создай его во вкладке «Баскетбол»"); return false; }
            float d = Vector3.Distance(ballGo.transform.position, CenterOf(me));
            if (ballRemoteHeld && d < 10f) { TryStealAt(me, ballOwner, true); StartCoroutine(CoCatchSoon(me)); return true; }
            if (!ballHeld && !ballRemoteHeld && d < 28f) { StartCoroutine(CoMagnet(me)); return true; }
            HeroToast("Мяч слишком далеко"); return false;
        }

        IEnumerator CoCatchSoon(Component me)
        {
            yield return new WaitForSeconds(0.4f);
            if (me != null && ballGo != null && !ballHeld && !ballRemoteHeld && Vector3.Distance(ballGo.transform.position, CenterOf(me)) < 7f) { TakeBall(me); HeroToast("Перехвачено!"); }
        }

        IEnumerator CoMagnet(Component me)
        {
            ballOwner = Game.LocalActor(); ballRb.isKinematic = true; ballCol.enabled = false; ballTrail.emitting = true;
            float t = 0f; Vector3 from = ballGo.transform.position;
            while (t < 0.4f && me != null && ballGo != null)
            {
                t += Time.deltaTime; ballGo.transform.position = Vector3.Lerp(from, HandPos(me), Mathf.SmoothStep(0f, 1f, t / 0.4f)); yield return null;
            }
            if (me != null && ballGo != null) { TakeBall(me); HeroToast("Мяч у тебя!"); }
        }

        IEnumerator CoDriveHit(Component me, float dur)
        {
            float end = Time.time + dur; var hit = new HashSet<Component>();
            while (Time.time < end && me != null)
            {
                foreach (var v in Victims())
                {
                    if (hit.Contains(v) || Vector3.Distance(CenterOf(v), CenterOf(me)) > 2.2f) continue;
                    hit.Add(v); Vector3 d = (CenterOf(v) - CenterOf(me)); d.y = 0f; d = (d.normalized + Vector3.up * 0.4f).normalized;
                    Rpc(v, "RPCA_Fall", 2.5f); Rpc(v, "RPCA_AddForceToBodyPart", 0, d * launchForce * 0.6f);
                    Vfx.Sparks(CenterOf(v), d, 18); Sfx2.Hit(0.6f); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.5f);
                    if (ballRemoteHeld && ballOwner == Game.ActorOf(v)) TryStealAt(me, ballOwner, true);
                }
                yield return null;
            }
        }

        IEnumerator CoBuff(float spd, float jmp, float dur)
        {
            int gen = buffGen; speedMul *= spd; jumpMul *= jmp; buffSpd *= spd; buffJmp *= jmp;
            yield return new WaitForSeconds(dur);
            if (gen == buffGen) { speedMul /= spd; jumpMul /= jmp; buffSpd /= spd; buffJmp /= jmp; }
        }

        float buffSpd = 1f, buffJmp = 1f;
        void CancelBuffs() { speedMul /= buffSpd; jumpMul /= buffJmp; buffSpd = buffJmp = 1f; buffGen++; }

        IEnumerator CoVanish(float dur, bool quiet)
        {
            bool was = invisible; if (!was) SetInvisible(true);
            yield return new WaitForSeconds(dur);
            if (!was && invisible) SetInvisible(false);
        }

        // ======================= ГОРЯЧАЯ РУКА (Зона) =======================
        void TryZone()
        {
            var me = Game.Me(); if (me == null || !IsHoop(hero)) return;
            if (zoneOn) { EndZone(); return; }
            if (zoneCasting) return;
            if (Time.time < cdZone) { HeroToast("Горячая рука: перезарядка " + Mathf.CeilToInt(cdZone - Time.time) + " с"); return; }
            StartCoroutine(CoZone(me));
        }

        IEnumerator CoZone(Component me)
        {
            HeroKind k = hero; var col = ZoneColor(k); zoneCasting = true;
            ShowBanner("ГОРЯЧАЯ РУКА", ZoneName(k), col, 3.2f);
            Vector3 head = HeadOf(me), f = FlatForward();
            StartCine(1, 1.5f); DoorFx(head - f * 1.8f + Vector3.down * 0.9f, f, col);
            Sfx2.DomainStart(); CamShake.Boost = 1.1f; Shade.Set(ShadeKind.Power, 1.0f); AnimPose(7, 1.3f);
            if (Game.InRoom) Net.Send("bb", "z;" + (int)k + ",1");
            float t0 = Time.time;
            while (Time.time - t0 < 1.3f) { if (hero != k || me == null) { zoneCasting = false; if (Game.InRoom) Net.Send("bb", "z;" + (int)k + ",0"); yield break; } yield return null; }
            zoneCasting = false; zoneOn = true; zoneEnd = Time.time + zoneDur; zoneApplied = true; speedMul *= 1.5f; jumpMul *= 1.35f;
            StartCine(6, 1.2f); ImpactFrame(); Sfx2.Boom(); CamShake.Boost = 1.6f;
            Vfx.Ring(CenterOf(me), Vector3.up, col, 12f, 0.8f); Vfx.Ring(CenterOf(me), new Vector3(0.3f, 1f, 0.2f), col, 9f, 0.7f);
            HeroToast(ZoneName(k) + "!");
            while (Time.time < zoneEnd && hero == k && me != null) yield return null;
            EndZone();
        }

        void EndZone()
        {
            if (zoneApplied) { speedMul /= 1.5f; jumpMul /= 1.35f; zoneApplied = false; }
            if (zoneOn || zoneCasting) { cdZone = Time.time + zoneCd; if (Game.InRoom) Net.Send("bb", "z;" + (int)hero + ",0"); }
            zoneOn = false; zoneCasting = false;
            if (zoneAura != null) { UnityEngine.Object.Destroy(zoneAura); zoneAura = null; }
        }

        void OnRemoteZone(int sender, float[] n)
        {
            if (n.Length < 2) return; int hk = (int)n[0];
            if (hk < 9 || hk > 15) return;
            if (n[1] > 0.5f)
            {
                remoteFlow[sender] = new RZ { kind = (HeroKind)hk, until = Time.time + 45f };
                if (costumeSync) { ShowBanner("ГОРЯЧАЯ РУКА", Game.PlayerName(sender) + ": " + ZoneName((HeroKind)hk), ZoneColor((HeroKind)hk), 3f); Sfx2.Zap(); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.5f); }
            }
            else remoteFlow.Remove(sender);
        }

        void TickZoneFx()
        {
            var me = Game.Me();
            if (zoneOn && me != null && IsHoop(hero))
            {
                var col = ZoneColor(hero); Vector3 c = CenterOf(me);
                if (zoneAura == null) zoneAura = AnimVfx.OrbGo(new Color(col.r, col.g, col.b, 0.0f), col, 1.1f);
                if (zoneAura != null) { zoneAura.transform.position = c; zoneAura.transform.localScale = Vector3.one * (1.8f + 0.25f * Mathf.Sin(Time.time * 7f)); }
                if (Time.time >= nextZoneFx)
                {
                    nextZoneFx = Time.time + 0.28f;
                    Vfx.Ring(c - Vector3.up * 0.6f, Vector3.up, new Color(col.r, col.g, col.b, 0.7f), 2.6f, 0.5f);
                    Vfx.Sparks(c, Vector3.up, 6, col);
                    if (hero == HeroKind.Aomine) BoltAround(c, col);
                }
            }
            else if (zoneAura != null) { UnityEngine.Object.Destroy(zoneAura); zoneAura = null; }
            if (remoteFlow.Count > 0)
            {
                List<int> rm = null;
                foreach (var kv in remoteFlow)
                {
                    if (Time.time > kv.Value.until) { (rm ?? (rm = new List<int>())).Add(kv.Key); continue; }
                    if (Time.time < kv.Value.nextFx) continue; kv.Value.nextFx = Time.time + 0.4f;
                    var c = Game.ByActor(kv.Key); if (c == null) continue; var col = ZoneColor(kv.Value.kind);
                    Vfx.Ring(CenterOf(c) - Vector3.up * 0.6f, Vector3.up, new Color(col.r, col.g, col.b, 0.7f), 2.4f, 0.5f); Vfx.Sparks(CenterOf(c), Vector3.up, 5, col);
                }
                if (rm != null) foreach (var k in rm) remoteFlow.Remove(k);
            }
        }

        void BoltAround(Vector3 c, Color col)
        {
            var pts = new Vector3[6]; Vector3 d = UnityEngine.Random.onUnitSphere; d.y = Mathf.Abs(d.y);
            for (int i = 0; i < 6; i++) pts[i] = c + d * (0.5f + i * 0.45f) + UnityEngine.Random.insideUnitSphere * 0.3f * (i > 0 && i < 5 ? 1f : 0f);
            AnimVfx.Line(pts, new Color(0.7f, 0.85f, 1f, 1f), 0.1f, 0.18f);
        }

        void DoorFx(Vector3 pos, Vector3 face, Color col)
        {
            if (Vfx.Sh() == null) return;
            var root = new GameObject("PF_door"); root.transform.position = pos; root.transform.rotation = Quaternion.LookRotation(-face, Vector3.up);
            Func<Vector3, Vector3, Color, Material> Q = (lp, ls, c) =>
            {
                var g = new GameObject("q"); g.transform.SetParent(root.transform, false); g.transform.localPosition = lp; g.transform.localScale = ls;
                g.AddComponent<MeshFilter>().sharedMesh = Vfx.Quad(); var mr = g.AddComponent<MeshRenderer>(); var m = Vfx.Mat(null, c); mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; return m;
            };
            var frame = Q(new Vector3(0, 1.2f, 0.05f), new Vector3(2.2f, 2.6f, 1f), new Color(0.05f, 0.05f, 0.08f, 1f));
            var glow = Q(new Vector3(0, 1.2f, 0.03f), new Vector3(2.0f, 2.4f, 1f), new Color(col.r, col.g, col.b, 0f));
            var pl = Q(new Vector3(-0.5f, 1.2f, 0f), new Vector3(1f, 2.4f, 1f), new Color(0.12f, 0.12f, 0.18f, 1f));
            var pr = Q(new Vector3(0.5f, 1.2f, 0f), new Vector3(1f, 2.4f, 1f), new Color(0.12f, 0.12f, 0.18f, 1f));
            var tl = root.transform.GetChild(2); var tr = root.transform.GetChild(3);
            Vfx.Add(root, 1.8f, (k, go) =>
            {
                float o = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.25f) / 0.45f)), w = 1f * (1f - o);
                tl.localScale = new Vector3(Mathf.Max(0.001f, w), 2.4f, 1f); tl.localPosition = new Vector3(-1f + w * 0.5f, 1.2f, 0f);
                tr.localScale = new Vector3(Mathf.Max(0.001f, w), 2.4f, 1f); tr.localPosition = new Vector3(1f - w * 0.5f, 1.2f, 0f);
                var gc = col; gc.a = Mathf.Clamp01(o * 1.1f) * (1f - Mathf.Pow(Mathf.Clamp01((k - 0.7f) / 0.3f), 2f)); glow.color = gc;
                var fc = frame.color; fc.a = 1f - Mathf.Clamp01((k - 0.8f) / 0.2f); frame.color = fc;
            });
        }
    }
}
