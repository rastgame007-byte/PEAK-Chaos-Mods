// Режим героев 1.7: превращение в сверхгероя. Шесть РАЗНЫХ героев — у каждого свой полёт, свои способности, свои пассивки.
//  Патриот    — величавый полёт, лазерные глаза (держать), ударная волна голосом        (стиль Хоумлендера)
//  Неуязвимый — быстрый полёт лёжа, удар кулаком с нокаутом, пикирование-таран        (стиль Неуязвимого/Супермена)
//  Молния     — бег ×4, рывок, удар молнии с неба
//  Громила    — великан, мега-прыжок, удар по земле
//  Призрак    — полёт сквозь стены, невидим, появляется за спиной и пугает
//  Телепат    — телекинез: держит друга взглядом, швыряет, взрывная волна
// Общее для всех: «схватить за горло» (X) — друг повисает у тебя в руке и летает с тобой; B — бросить.
// Всё через физику и RPC самой игры (подножка, отброс, телепорт), поэтому друзьям мод НЕ нужен. Звуки и вспышки на экране — у тебя.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    internal enum HeroKind { None, Patriot, Invincible, Flash, Brute, Phantom, Mind, Gojo, Sukuna, Kagami, Aomine, Kise, Midorima, Kuroko, Akashi, Murasakibara }

    public partial class PeakFunPlugin
    {
        HeroKind hero = HeroKind.None;
        bool heroPoseOn = true, heroRandom;
        float heroPower = 1f;                       // множитель силы ударов
        float heroSwitchAt;
        float cdA, cdB;                             // перезарядки способностей (время, когда снова можно)
        float flyAccel = 8f, flyBoost = 2.5f;       // параметры полёта героя (читает FixedUpdate)
        float dashUntil; Vector3 dashDir; float dashSpeed;
        float poseMax;                              // насколько ложиться в полёте (0..1)
        string heroMsg = ""; float heroMsgUntil;

        // снимок настроек до превращения — чтобы откатить
        bool sFlying, sQuick, sInvinc, sInvis; float sSpeed, sJump, sClimb, sSize, sFlySpeed; bool sSaved;

        // схваченный
        Component grabbed; bool grabTele; float grabDist = 4f, nextGrabSend, nextFallSend;
        // лазер
        bool laserOn; float nextLaserHit, nextLaserFx; LineRenderer beam; GameObject beamGo;
        // призрак
        bool noclip; readonly List<Collider> noclipCols = new List<Collider>();

        static string HeroName(HeroKind k)
        {
            switch (k)
            {
                case HeroKind.Patriot: return "Патриот"; case HeroKind.Invincible: return "Неуязвимый"; case HeroKind.Flash: return "Молния";
                case HeroKind.Brute: return "Громила"; case HeroKind.Phantom: return "Призрак"; case HeroKind.Mind: return "Телепат";
                case HeroKind.Gojo: return "Архимаг Альбус"; case HeroKind.Sukuna: return "Владыка Морок";
                case HeroKind.Kagami: return "Винс Картер"; case HeroKind.Aomine: return "Аллен Айверсон"; case HeroKind.Kise: return "Коби Брайант"; case HeroKind.Midorima: return "Стивен Карри";
                case HeroKind.Kuroko: return "Крис Пол"; case HeroKind.Akashi: return "Мэджик Джонсон"; case HeroKind.Murasakibara: return "Шакил О'Нил";
            }
            return "—";
        }

        static string HeroInfo(HeroKind k)
        {
            if (IsHoop(k)) return HoopInfo(k);
            switch (k)
            {
                case HeroKind.Patriot: return "Величавый полёт с плавным разгоном. Лазерные глаза — держи [A]. Голос-удар — [B]: ударная волна вокруг. Неуязвим.";
                case HeroKind.Invincible: return "Быстрый полёт ЛЁЖА (поза Супермена), звуковой барьер при разгоне. [A] — удар кулаком: рывок и нокаут. [B] — пикирование-таран с волной. Неуязвим.";
                case HeroKind.Flash: return "Бег ×4, лазание ×4, прыжок ×1.8 без полёта. [A] — сверхрывок вперёд. [B] — удар молнии туда, куда смотришь. Неуязвим.";
                case HeroKind.Brute: return "Великан ×1.6, прыжок ×4. [A] — удар по земле перед собой. [B] — мега-прыжок и приземление с волной. Неуязвим.";
                case HeroKind.Phantom: return "Невидим, летит сквозь стены. [A] — вкл/выкл прохождение сквозь стены. [B] — появиться за спиной у друга и напугать (скример, если у него PeakFun).";
                case HeroKind.Mind: return "Телекинез: держи [A] — цель повисает перед тобой и следует за взглядом. [B] — швырнуть её (или взрывная волна, если ничего не держишь). Парение.";
                case HeroKind.Gojo: return "Сильнейший. Парит (полёт ~22 м/с), пассивка «Щит» — к тебе нельзя подойти. [A] держать — «Магнит»: притягивает всё к шару перед тобой. [B] — «Отброс»: снаряд-отталкивание. [H] — «Луч»: ломает всё на линии. [Q] — телепорт. [G] — ВЕЛИКИЙ КУПОЛ: «Звёздная бездна».";
                case HeroKind.Sukuna: return "Владыка тьмы: скорость ×2, прыжок ×2.5, 4 руки. [A] держать — «Град клинков»: серия рассекающих ударов. [B] — «Клин»: по цели или длинный по земле. [H] — «Огненная стрела»: стрела со взрывом. [G] — ВЕЛИКИЙ КУПОЛ: «Багровый чертог».";
            }
            return "";
        }

        static string HeroA(HeroKind k) { if (IsHoop(k)) return HoopAbName(k, 0); switch (k) { case HeroKind.Patriot: return "Лазер"; case HeroKind.Invincible: return "Удар"; case HeroKind.Flash: return "Рывок"; case HeroKind.Brute: return "Удар о землю"; case HeroKind.Phantom: return "Сквозь стены"; case HeroKind.Mind: return "Держать"; case HeroKind.Gojo: return "Синий"; case HeroKind.Sukuna: return "Град клинков"; } return ""; }
        static string HeroB(HeroKind k) { if (IsHoop(k)) return HoopAbName(k, 1); switch (k) { case HeroKind.Patriot: return "Ударная волна"; case HeroKind.Invincible: return "Пике-таран"; case HeroKind.Flash: return "Молния"; case HeroKind.Brute: return "Мега-прыжок"; case HeroKind.Phantom: return "За спину"; case HeroKind.Mind: return "Швырнуть"; case HeroKind.Gojo: return "Красный"; case HeroKind.Sukuna: return "Клин"; } return ""; }

        void HeroToast(string s) { heroMsg = s; heroMsgUntil = Time.time + 2.5f; }

        // ---------------- кто может быть жертвой ----------------
        List<Component> Victims()
        {
            var l = Game.Others();
            if (npcChar != null && !l.Contains(npcChar)) l.Add(npcChar);   // бот — для проверки в одиночку
            l.RemoveAll(c => c == null || Game.IsDead(c));
            return l;
        }

        Component LookedVictim(float maxDist, float minDot)
        {
            var cam = Camera.main; if (cam == null) return null;
            Vector3 o = cam.transform.position, f = cam.transform.forward;
            Component best = null; float bs = float.MaxValue;
            foreach (var c in Victims())
            {
                Vector3 to = CenterOf(c) - o; float d = to.magnitude;
                if (d > maxDist || d < 0.2f) continue;
                float dot = Vector3.Dot(f, to / d);
                if (dot < minDot) continue;
                float score = d * (2f - dot);
                if (score < bs) { bs = score; best = c; }
            }
            return best;
        }

        // ---------------- смена героя ----------------
        void SetHero(HeroKind k)
        {
            var me = Game.Me();
            if (me == null) { Ui.Toast("Нет персонажа", Ui.Bad); return; }
            if (hero != HeroKind.None) RestoreFromHero(me);
            hero = k;
            if (k == HeroKind.None) { armMode = 0; SendHero(); Ui.Toast("Герой отключён — всё вернулось как было", Ui.Muted); return; }
            CamShake.Install();
            sFlying = flying; sQuick = quickClimb; sInvinc = BuffOn("invinc"); sInvis = invisible;
            sSpeed = speedMul; sJump = jumpMul; sClimb = climbMul; sSize = sizeTarget; sFlySpeed = flySpeed; sSaved = true;
            flyAccel = 8f; flyBoost = 2.5f; poseMax = 0f;
            SetBuff("invinc", true);
            switch (k)
            {
                case HeroKind.Patriot: flySpeed = 20f; flyAccel = 3.5f; flyBoost = 2.4f; poseMax = 0.3f; SetFly(true); break;
                case HeroKind.Invincible: flySpeed = 38f; flyAccel = 9f; flyBoost = 2.6f; poseMax = 1f; SetFly(true); break;
                case HeroKind.Flash: speedMul = 4f; jumpMul = 1.8f; quickClimb = true; climbMul = 4f; if (flying) SetFly(false); break;
                case HeroKind.Brute: SetSize(1.6f); jumpMul = 4f; speedMul = 1.3f; if (flying) SetFly(false); break;
                case HeroKind.Phantom: flySpeed = 14f; flyAccel = 5f; flyBoost = 2.5f; poseMax = 0.7f; SetFly(true); SetInvisible(true); SetNoclip(true); break;
                case HeroKind.Mind: flySpeed = 8f; flyAccel = 3f; flyBoost = 2f; poseMax = 0f; SetFly(true); break;
                case HeroKind.Gojo: flySpeed = 22f; flyAccel = 5f; flyBoost = 2.2f; poseMax = 0.0f; SetFly(true); break;
                case HeroKind.Sukuna: speedMul = 2f; jumpMul = 2.5f; quickClimb = true; climbMul = 2f; if (flying) SetFly(false); break;
                case HeroKind.Kagami: speedMul = 1.5f; jumpMul = 2.4f; quickClimb = true; climbMul = 1.6f; if (flying) SetFly(false); break;
                case HeroKind.Aomine: speedMul = 1.8f; jumpMul = 2.0f; quickClimb = true; climbMul = 1.6f; if (flying) SetFly(false); break;
                case HeroKind.Kise: speedMul = 1.6f; jumpMul = 2.0f; quickClimb = true; climbMul = 1.5f; if (flying) SetFly(false); break;
                case HeroKind.Midorima: speedMul = 1.3f; jumpMul = 1.6f; quickClimb = true; climbMul = 1.3f; if (flying) SetFly(false); break;
                case HeroKind.Kuroko: speedMul = 1.6f; jumpMul = 1.5f; quickClimb = true; climbMul = 1.4f; if (flying) SetFly(false); break;
                case HeroKind.Akashi: speedMul = 1.4f; jumpMul = 1.7f; quickClimb = true; climbMul = 1.4f; if (flying) SetFly(false); break;
                case HeroKind.Murasakibara: SetSize(1.5f); speedMul = 1.2f; jumpMul = 2.6f; quickClimb = true; climbMul = 1.5f; if (flying) SetFly(false); break;
            }
            Sfx2.Whoosh(); SendHero();
            if (flying) StartCoroutine(CoTakeoff(me));
            if (IsHoop(k)) Ui.Toast("Ты — " + HeroName(k) + "!  [" + keyAb1.Value + "] " + HeroA(k) + "   [" + keyAb2.Value + "] " + HeroB(k) + "   [" + keyAb3.Value + "] " + HeroC(k) + "   [" + keyDomain.Value + "] ГОРЯЧАЯ РУКА   [" + keyBall.Value + "] мяч", Ui.Accent);
            else if (IsAnime(k)) Ui.Toast("Ты — " + HeroName(k) + "!  [" + keyAb1.Value + "] " + HeroA(k) + " (держать)   [" + keyAb2.Value + "] " + HeroB(k) + "   [" + keyAb3.Value + "] " + HeroC(k) + "   [" + keyDomain.Value + "] ВЕЛИКИЙ КУПОЛ" + (k == HeroKind.Gojo ? "   [" + keyDodge.Value + "] телепорт" : ""), Ui.Accent);
            else Ui.Toast("Ты — " + HeroName(k) + "!  [" + keyAb1.Value + "] " + HeroA(k) + "   [" + keyAb2.Value + "] " + HeroB(k) + "   [" + keyGrab.Value + "] схватить   ЛКМ — бить, ПКМ — тяжёлый удар   [" + keyDodge.Value + "] уклон", Ui.Accent);
            heroSwitchAt = Time.time + 45f;
        }

        void RestoreFromHero(Component me)
        {
            StopLaser(); ReleaseGrab(false); if (noclip) SetNoclip(false); StopAnime();
            if (sSaved)
            {
                speedMul = sSpeed; jumpMul = sJump; quickClimb = sQuick; climbMul = sClimb; flySpeed = sFlySpeed;
                SetSize(sSize);
                if (!sInvinc) SetBuff("invinc", false);
                if (invisible && !sInvis) SetInvisible(false);
                if (flying && !sFlying) SetFly(false);
                if (!flying && sFlying) SetFly(true);
            }
            sSaved = false; poseMax = 0f; flyAccel = 8f; flyBoost = 2.5f; dashUntil = 0f;
        }

        void HeroReset()
        {
            // сменился персонаж (респаун/новая сцена) — сбрасываем состояние героя без обращения к старому телу
            hero = HeroKind.None; grabbed = null; laserOn = false; noclip = false; noclipCols.Clear(); sSaved = false; poseMax = 0f; dashUntil = 0f;
            blueOn = false; disOn = false; purpleCharge = false; domCasting = false; if (domCo != null) { try { StopCoroutine(domCo); } catch { } domCo = null; } if (domActive) { domActive = false; if (domFx != null) domFx.Collapse(); }
            if (blueGo != null) { Destroy(blueGo); blueGo = null; }
            if (beamGo != null) beamGo.SetActive(false);
            CombatReset();
        }

        void HeroPanic()
        {
            StopLaser(); ReleaseGrab(false); dashUntil = 0f; CombatReset(); StopAnime();
        }

        // ---------------- прохождение сквозь стены ----------------
        void SetNoclip(bool on)
        {
            var me = Game.Me(); if (me == null) return;
            if (on == noclip) return;
            if (on)
            {
                noclipCols.Clear();
                foreach (var c in me.GetComponentsInChildren<Collider>(true))
                    if (c != null && c.enabled && !c.isTrigger) { c.enabled = false; noclipCols.Add(c); }
                foreach (var rb in Game.Rigs(me)) rb.useGravity = false;
            }
            else
            {
                foreach (var c in noclipCols) if (c != null) c.enabled = true;
                noclipCols.Clear();
            }
            noclip = on;
            HeroToast(on ? "Сквозь стены: ВКЛ" : "Сквозь стены: выкл");
        }

        // ---------------- главный тик героя (Update) ----------------
        void TickHero()
        {
            if (CamShake.Boost > 0f) CamShake.Boost = Mathf.MoveTowards(CamShake.Boost, 0f, Time.unscaledDeltaTime * 2.2f);
            var me = Game.Me();
            if (me == null) return;
            bool typing = (showMenu && cfgFreeze.Value) || puppetOn;

            if (!typing && Keys.Down(keyHero.Value)) { if (hero == HeroKind.None) SetHero(RandomHero(HeroKind.None)); else SetHero(HeroKind.None); }
            if (!typing && Keys.Down(keyGrab.Value)) ToggleGrab();
            if (!typing && Keys.Down(keyThrow.Value)) ThrowGrabbed();

            if (hero == HeroKind.None) { UpdateBeam(false, Vector3.zero, Vector3.zero); if (trails.Count > 0) ClearTrails(); return; }

            if (heroRandom && Time.time > heroSwitchAt) SetHero(RandomHero(hero));
            if (R.Get<bool>(R.Get(me, "data"), "dead")) { StopLaser(); ReleaseGrab(false); return; }

            bool grabCtx = grabbed != null && !grabTele;
            if (!typing && grabCtx)
            {
                StopLaser();
                if (!grabBusy && Keys.Down(keyAb1.Value)) StartCoroutine(CoGrabSlam(me));
                if (!grabBusy && Keys.Down(keyAb2.Value)) StartCoroutine(CoGrabSky(me));
            }
            else if (!typing)
            {
                if (hero == HeroKind.Patriot) { if (Keys.Held(keyAb1.Value)) LaserTick(me); else StopLaser(); }
                else if (hero == HeroKind.Mind) { MindTick(me, Keys.Held(keyAb1.Value)); }
                else if (IsAnime(hero)) { UseAnimeA(me, Keys.Held(keyAb1.Value)); }
                else if (Keys.Down(keyAb1.Value)) UseA();
                if (Keys.Down(keyAb2.Value)) UseB();
            }
            else StopLaser();
            TickAnimeKeys(me, typing);

            if (grabbed != null) GrabTick(me);
            TickCombat(me, typing);
            if (Time.time % 3f < Time.deltaTime && hero != HeroKind.None) { var b = BuffOn("invinc"); if (!b) SetBuff("invinc", true); }
        }

        HeroKind RandomHero(HeroKind except)
        {
            var all = new List<HeroKind>();
            foreach (HeroKind k in Enum.GetValues(typeof(HeroKind))) if (k != HeroKind.None && k != except) all.Add(k);
            return all[UnityEngine.Random.Range(0, all.Count)];
        }

        public void UseA()
        {
            if (hero == HeroKind.None) { Ui.Toast("Сначала выбери героя (вкладка «Герои», клавиша " + keyHero.Value + ")", Ui.Bad); return; }
            if (IsHoop(hero)) { HoopCast(0); return; }
            var me = Game.Me(); if (me == null || Time.time < cdA) return;
            switch (hero)
            {
                case HeroKind.Invincible: cdA = Time.time + 1.4f; StartCoroutine(CoPunch(me)); break;
                case HeroKind.Flash: cdA = Time.time + 1.2f; Dash(CamForward(), 75f, 0.45f); Sfx2.Whoosh(); Shade.Set(ShadeKind.Speed, 0.8f); CamShake.Boost = 0.5f; break;
                case HeroKind.Brute: cdA = Time.time + 1.5f; { var c = me.transform.position + FlatForward() * 3f; Shockwave(c, 8f, 0.7f); } break;
                case HeroKind.Phantom: SetNoclip(!noclip); break;
                case HeroKind.Patriot: break;   // лазер — по удержанию
                case HeroKind.Gojo: case HeroKind.Sukuna: HeroToast("Держи клавишу [" + keyAb1.Value + "] — это зажимаемая техника"); break;
            }
        }

        public void UseB()
        {
            if (hero == HeroKind.None) { Ui.Toast("Сначала выбери героя (вкладка «Герои», клавиша " + keyHero.Value + ")", Ui.Bad); return; }
            if (IsHoop(hero)) { HoopCast(1); return; }
            var me = Game.Me(); if (me == null || Time.time < cdB) return;
            switch (hero)
            {
                case HeroKind.Patriot: cdB = Time.time + 4f; Shockwave(CenterOf(me), 16f, 1f); break;
                case HeroKind.Invincible: cdB = Time.time + 4f; StartCoroutine(CoSlam(me, 60f, 12f, 1.2f)); break;
                case HeroKind.Flash: cdB = Time.time + 4f; StartCoroutine(CoLightning(me)); break;
                case HeroKind.Brute:
                    cdB = Time.time + 5f;
                    foreach (var rb in Game.Rigs(me)) Game.SetVelocity(rb, Vector3.up * 34f + FlatForward() * 14f);
                    StartCoroutine(CoSlam(me, 0f, 14f, 0.9f)); break;
                case HeroKind.Phantom: cdB = Time.time + 6f; StartCoroutine(CoBehind(me)); break;
                case HeroKind.Gojo: StartCoroutine(CoRed(me)); break;
                case HeroKind.Sukuna: StartCoroutine(CoCleave(me)); break;
                case HeroKind.Mind:
                    cdB = Time.time + 1.2f;
                    if (grabbed != null) ThrowGrabbed(); else Shockwave(CenterOf(me), 14f, 1f);
                    break;
            }
        }

        // ---------------- вспомогательное ----------------
        Vector3 CamForward() { var c = Camera.main; return c != null ? c.transform.forward : Vector3.forward; }
        Vector3 FlatForward() { var f = CamForward(); f.y = 0f; return f.sqrMagnitude < 0.01f ? Vector3.forward : f.normalized; }
        void Dash(Vector3 dir, float speed, float dur) { dashDir = dir.normalized; dashSpeed = speed; dashUntil = Time.time + dur; }

        /// <summary>Ударная волна: всех в радиусе — подножка и отброс; предметы и зомби тоже разлетаются.</summary>
        void Shockwave(Vector3 center, float radius, float power)
        {
            power *= heroPower;
            int n = 0;
            foreach (var c in Victims())
            {
                Vector3 p = CenterOf(c); float d = Vector3.Distance(p, center);
                if (d > radius) continue;
                Vector3 dir = p - center; dir.y = 0f;
                dir = dir.sqrMagnitude < 0.01f ? Vector3.up : (dir.normalized + Vector3.up * 0.8f).normalized;
                float f = 1f - 0.6f * d / radius;
                Rpc(c, "RPCA_Fall", 2.5f);
                Rpc(c, "RPCA_AddForceToBodyPart", 0, dir * launchForce * 0.7f * power * f);
                Gore.Burst(p, dir, 0.75f * f * Mathf.Sqrt(power));
                n++;
            }
            var ct = R.FindType("Character");
            foreach (var col in Physics.OverlapSphere(center, radius))
            {
                var rb = col.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                if (ct != null && col.GetComponentInParent(ct) != null) continue;
                rb.AddExplosionForce(9f * power, center, radius, 0.7f, ForceMode.VelocityChange);
            }
            CamShake.Boost = Mathf.Max(CamShake.Boost, 0.9f);
            Destruct.Smash(center + Vector3.up * 0.4f, Vector3.up, Mathf.Min(radius * 0.45f, 6f), power);
            Vfx.Ring(center + Vector3.up * 0.3f, Vector3.up, new Color(1f, 0.95f, 0.7f, 0.9f), radius, 0.6f);
            Vfx.Ring(center + Vector3.up * 0.3f, new Vector3(0.4f, 1f, 0.2f), new Color(0.7f, 0.85f, 1f, 0.6f), radius * 0.8f, 0.5f);
            RaycastHit gh; if (Physics.Raycast(center + Vector3.up * 0.5f, Vector3.down, out gh, 3f, ~0, QueryTriggerInteraction.Ignore)) Gore.Impact(gh.point, gh.normal, Mathf.Clamp(radius / 10f, 0.4f, 1.5f));
            Sfx2.Boom();
            if (!Shade.Active) Shade.Set(ShadeKind.Power, 0.9f);
            HeroToast(n > 0 ? "Волна: сбито " + n : "Волна!");
        }

        IEnumerator CoPunch(Component me)
        {
            var target = LookedVictim(14f, 0.6f);
            Vector3 dir = target != null ? (CenterOf(target) - CenterOf(me)).normalized : CamForward();
            armDir = dir; punchT0 = Time.time;
            Dash(dir, 55f, 0.5f); Sfx2.Whoosh(); Shade.Set(ShadeKind.Speed, 0.7f); StartCine(4, 0.8f);
            float end = Time.time + 0.5f;
            while (Time.time < end)
            {
                if (target != null && Vector3.Distance(CenterOf(me), CenterOf(target)) < 2.4f) break;
                yield return null;
            }
            dashUntil = 0f;
            if (target != null && Vector3.Distance(CenterOf(me), CenterOf(target)) < 3.4f)
            {
                Vector3 fd = (CenterOf(target) - CenterOf(me)); fd.y = 0f; fd = (fd.normalized + Vector3.up * 0.45f).normalized;
                Rpc(target, "RPCA_Fall", 4f);
                Rpc(target, "RPCA_AddForceToBodyPart", 0, fd * launchForce * 1.1f * heroPower);
                CamShake.Boost = 1f; Sfx2.Boom(); HeroToast("Удар по " + Game.Name(target) + "!");
                Vector3 hp = CenterOf(target) + Vector3.up * 0.3f;
                Gore.Burst(hp, fd, 1.9f); Vfx.Ring(hp, dir, new Color(1f, 1f, 1f, 0.95f), 4.5f, 0.35f);
                TrackImpact(target, 1.8f);
            }
            else HeroToast("Мимо…");
        }

        IEnumerator CoSlam(Component me, float diveSpeed, float radius, float power)
        {
            float t0 = Time.time;
            if (diveSpeed > 0f) { Dash(Vector3.down, diveSpeed, 1.6f); Sfx2.Whoosh(); if (hero == HeroKind.Invincible) StartCine(5, 1.3f); }
            yield return new WaitForSeconds(diveSpeed > 0f ? 0.15f : 0.6f);
            while (Time.time - t0 < 3f)
            {
                bool ground = Physics.Raycast(CenterOf(me), Vector3.down, 1.7f * Mathf.Max(1f, me.transform.localScale.y), ~0, QueryTriggerInteraction.Ignore);
                if (ground && Time.time - t0 > 0.35f) break;
                yield return null;
            }
            dashUntil = 0f;
            Shockwave(me.transform.position, radius, power);
            Destruct.Smash(me.transform.position, Vector3.up, Mathf.Min(radius * 0.4f, 5f), power * 1.2f);
            RaycastHit sh; if (Physics.Raycast(CenterOf(me), Vector3.down, out sh, 4f, ~0, QueryTriggerInteraction.Ignore)) Gore.Impact(sh.point, sh.normal, Mathf.Clamp(radius / 8f, 0.5f, 2f));
        }

        IEnumerator CoLightning(Component me)
        {
            var cam = Camera.main; if (cam == null) yield break;
            Vector3 hit = cam.transform.position + cam.transform.forward * 60f;
            RaycastHit h;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out h, 120f, ~0, QueryTriggerInteraction.Ignore)) hit = h.point;
            var go = new GameObject("PF_Bolt"); var lr = go.AddComponent<LineRenderer>();
            SetupLine(lr, new Color(0.7f, 0.85f, 1f), 0.35f);
            var pts = new List<Vector3>(); Vector3 top = hit + Vector3.up * 80f;
            for (int i = 0; i <= 16; i++) { var p = Vector3.Lerp(top, hit, i / 16f); if (i > 0 && i < 16) p += new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), 0, UnityEngine.Random.Range(-2.5f, 2.5f)); pts.Add(p); }
            lr.positionCount = pts.Count; lr.SetPositions(pts.ToArray());
            Sfx2.Zap();
            if (!Shade.Active) Shade.Set(ShadeKind.Power, 0.6f);
            Shockwave(hit, 9f, 1f);
            yield return new WaitForSeconds(0.25f);
            Destroy(go);
        }

        IEnumerator CoBehind(Component me)
        {
            var t = LookedVictim(120f, 0.5f);
            if (t == null) { var l = Victims(); if (l.Count > 0) t = l[UnityEngine.Random.Range(0, l.Count)]; }
            if (t == null) { HeroToast("Некого пугать"); yield break; }
            Vector3 look = R.Get<Vector3>(R.Get(t, "data"), "lookDirection_Flat"); look.y = 0f;
            if (look.sqrMagnitude < 0.01f) look = t.transform.forward; look.Normalize();
            Vector3 pos = CenterOf(t) - look * 1.6f + Vector3.up * 0.2f;
            Rpc(me, "WarpPlayerRPC", pos, false);
            Game.HaltBody(me);
            Sfx2.Whoosh();
            int actor = Game.ActorOf(t);
            if (Game.InRoom && !Game.IsMine(t) && actor >= 0) Net.Send("fx", "Scream:3.5", actor);
            HeroToast("Бу! за спиной у " + Game.Name(t));
            yield return null;
        }

        // ---------------- лазерные глаза ----------------
        void SetupLine(LineRenderer lr, Color c, float w)
        {
            Shader sh = null;
            foreach (var n in new[] { "Sprites/Default", "Universal Render Pipeline/Unlit", "Unlit/Color", "Hidden/Internal-Colored", "Legacy Shaders/Particles/Additive" })
            { sh = Shader.Find(n); if (sh != null) break; }
            if (sh != null) { var m = new Material(sh); m.color = c; lr.material = m; }
            lr.startColor = c; lr.endColor = c; lr.startWidth = w; lr.endWidth = w * 0.8f;
            lr.useWorldSpace = true; lr.numCapVertices = 4; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void LaserTick(Component me)
        {
            var cam = Camera.main; if (cam == null) return;
            Vector3 head = HeadOf(me);
            Vector3 o = cam.transform.position, f = cam.transform.forward;
            Vector3 end = o + f * 70f; Component victim = null; RaycastHit best = default(RaycastHit); bool got = false;
            var ct = R.FindType("Character");
            var hits = Physics.RaycastAll(o, f, 70f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                var cc = ct != null ? h.collider.GetComponentInParent(ct) : null;
                if (cc != null && cc == me) continue;
                best = h; got = true; if (cc != null) victim = cc; break;
            }
            if (got) end = best.point;
            if (!laserOn) { laserOn = true; Sfx2.LaserStart(); }
            Vector3 eye = head + (grabTele ? FlatForward() : FlatForward()) * 0.2f + Vector3.up * 0.03f;
            UpdateBeam(true, eye, end);
            if (Time.time >= nextBeamSend) { nextBeamSend = Time.time + 0.1f; if (Game.InRoom) Net.Send("beam", "1," + end.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + end.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + end.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)); }
            if (Time.time >= nextLaserFx) { nextLaserFx = Time.time + 0.2f; CamShake.Boost = Mathf.Max(CamShake.Boost, 0.18f); if (!Shade.Active) Shade.Set(ShadeKind.Laser, 0.5f); }
            if (got && Time.time >= nextLaserHit)
            {
                nextLaserHit = Time.time + 0.45f;
                if (victim != null && victim != me)
                {
                    Rpc(victim, "RPCA_Fall", 1.5f);
                    Rpc(victim, "RPCA_AddForceToBodyPart", 0, (f + Vector3.up * 0.3f).normalized * launchForce * 0.25f * heroPower);
                    Gore.Burst(best.point, f, 0.45f);
                    int actor = Game.ActorOf(victim);
                    if (Game.InRoom && !Game.IsMine(victim) && actor >= 0 && Time.time % 1.6f < 0.5f) Net.Send("fx", "Fire:2.5", actor);
                }
                else if (best.collider != null && best.collider.attachedRigidbody == null) { Vfx.Dust(best.point + best.normal * 0.1f, 0.8f, 3); Vfx.Sparks(best.point + best.normal * 0.05f, best.normal, 6, new Color(1f, 0.5f, 0.2f)); Destruct.Smash(best.point, f, 0.8f, 0.5f, true, Mathf.Min(Destruct.MaxSize, 3f)); }
                if (victim == null && best.collider != null && best.collider.attachedRigidbody != null && !best.collider.attachedRigidbody.isKinematic)
                    best.collider.attachedRigidbody.AddForceAtPosition(f * 6f * heroPower, best.point, ForceMode.VelocityChange);
            }
        }

        void StopLaser()
        {
            if (laserOn) { laserOn = false; Sfx2.LaserStop(); if (Game.InRoom) Net.Send("beam", "0"); }
            UpdateBeam(false, Vector3.zero, Vector3.zero);
        }

        void UpdateBeam(bool on, Vector3 a, Vector3 b)
        {
            if (!on) { if (beamGo != null && beamGo.activeSelf) beamGo.SetActive(false); return; }
            if (beamGo == null)
            {
                beamGo = new GameObject("PF_Laser"); DontDestroyOnLoad(beamGo);
                beam = beamGo.AddComponent<LineRenderer>(); SetupLine(beam, new Color(1f, 0.1f, 0.05f), 0.09f);
                beam.positionCount = 2;
            }
            if (!beamGo.activeSelf) beamGo.SetActive(true);
            float w = 0.07f + 0.03f * Mathf.Sin(Time.time * 60f);
            beam.startWidth = w; beam.endWidth = w * 0.7f;
            beam.SetPosition(0, a); beam.SetPosition(1, b);
        }

        // ---------------- схватить за горло / телекинез ----------------
        void MindTick(Component me, bool held)
        {
            if (held && grabbed == null)
            {
                var t = LookedVictim(45f, 0.9f);
                if (t != null) { grabTele = true; grabDist = Mathf.Clamp(Vector3.Distance(CenterOf(me), CenterOf(t)), 3f, 30f); GrabStart(me, t); }
            }
            else if (!held && grabbed != null && grabTele) ReleaseGrab(true);
            if (held && grabbed != null && grabTele)
            {
                float sc = Input.mouseScrollDelta.y; if (Mathf.Abs(sc) > 0.01f) grabDist = Mathf.Clamp(grabDist + sc * 1.5f, 2.5f, 35f);
            }
        }

        // ---------------- физика полёта героя (из FixedUpdate) ----------------
        /// <summary>Рывок/пике: перекрывает скорость всего тела, даже если полёт выключен.</summary>
        void HeroFixed()
        {
            var me = Game.Me(); if (me == null) return;
            if (Time.time < dashUntil)
            {
                flyVel = dashDir * dashSpeed;
                foreach (var rb in Game.Rigs(me)) Game.SetVelocity(rb, flyVel);
            }
            if (hero == HeroKind.Invincible && flying)
            {
                float sp = flyVel.magnitude;
                if (sp > 33f && !boomed) { boomed = true; SonicBoom(me); } else if (sp < 22f) boomed = false;
            }
            if (hero != HeroKind.None)
            {
                if (flying) lastFlySpeed = flyVel.magnitude;
                else if (wasFlying && lastFlySpeed > 8f)
                {
                    RaycastHit lh;
                    if (Physics.Raycast(CenterOf(me), Vector3.down, out lh, 3.2f, ~0, QueryTriggerInteraction.Ignore))
                    { landPoseUntil = Time.time + 0.9f; Gore.Impact(lh.point, lh.normal, Mathf.Clamp(lastFlySpeed / 30f, 0.4f, 1.2f)); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.5f); Sfx2.Boom(); }
                }
                wasFlying = flying;
            }
            HeroArms(me);
        }

        /// <summary>Поза полёта: ось «таз → голова» плавно поворачиваем по направлению движения (лёжа, как Супермен).</summary>
        void HeroPose(Component me, Vector3 vel)
        {
            if (!heroPoseOn || poseMax <= 0.01f || hero == HeroKind.None) return;
            float sp = vel.magnitude;
            float blend = poseMax * Mathf.Clamp01((sp - 3f) / 8f);
            Vector3 desired = blend > 0.01f ? Vector3.Slerp(Vector3.up, vel / sp, blend) : Vector3.up;
            Vector3 center = CenterOf(me), head = HeadOf(me);
            Vector3 axis = head - center; if (axis.sqrMagnitude < 0.01f) return; axis.Normalize();
            Quaternion q = Quaternion.FromToRotation(axis, desired);
            Quaternion qf = Quaternion.Slerp(Quaternion.identity, q, 0.22f);
            if (Quaternion.Angle(Quaternion.identity, qf) < 0.2f) return;
            foreach (var rb in Game.Rigs(me))
            {
                if (rb == null || rb.isKinematic) continue;
                rb.position = center + qf * (rb.position - center);
                rb.rotation = qf * rb.rotation;
                rb.angularVelocity = Vector3.zero;
            }
        }

        // HUD героя — в HeroHud.cs
        void Bar(float x, float y, float w, float h, string label, float v, float s, bool hold)
        {
            Ui.Box(new Rect(x, y, w, h), new Color(1, 1, 1, 0.1f), h * 0.5f);
            if (v > 0.02f) Ui.Box(new Rect(x, y, w * v, h), v >= 0.99f ? Ui.Good : Ui.Accent, h * 0.5f);
            Ui.Text(new Rect(x + 8 * s, y - 1, w - 12 * s, h + 2), label + (hold ? "  (держать)" : ""), (int)(12 * s), Color.white, TextAnchor.MiddleLeft, true);
        }
    }

    // ======================= звуки героев (синтез) =======================
    internal static class Sfx2
    {
        static AudioSource src, loop; static GameObject host;
        static readonly System.Random rn = new System.Random(11);

        static void Init()
        {
            if (src != null) return;
            host = new GameObject("PeakFun_HeroAudio"); UnityEngine.Object.DontDestroyOnLoad(host);
            src = host.AddComponent<AudioSource>(); src.spatialBlend = 0f; src.playOnAwake = false; src.ignoreListenerPause = true;
            loop = host.AddComponent<AudioSource>(); loop.spatialBlend = 0f; loop.playOnAwake = false; loop.loop = true; loop.ignoreListenerPause = true;
        }

        static AudioClip Make(string name, int rate, float len, Func<float, float, float> f)
        {
            int n = (int)(rate * len); var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)rate, i / (float)n), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, rate, false); c.SetData(d, 0); return c;
        }

        static float Noise() { return (float)rn.NextDouble() * 2f - 1f; }
        static AudioClip whoosh, boom, zap, hum;

        public static void Whoosh()
        {
            try
            {
                Init();
                if (whoosh == null) { float lp = 0f; whoosh = Make("pf_whoosh", 22050, 0.7f, (t, k) => { float a = Mathf.Sin(k * Mathf.PI); lp += (Noise() - lp) * Mathf.Lerp(0.05f, 0.5f, k); return lp * a * 1.4f; }); }
                src.PlayOneShot(whoosh, 0.7f);
            }
            catch { }
        }

        public static void Boom()
        {
            try
            {
                Init();
                if (boom == null) { float lp = 0f; double ph = 0; boom = Make("pf_boom", 22050, 1.1f, (t, k) => { float env = Mathf.Exp(-t * 4.5f); ph += (70.0 - 35.0 * k) / 22050.0; lp += (Noise() - lp) * 0.12f; return (Mathf.Sin((float)(ph * Mathf.PI * 2)) * 0.9f + lp * 0.6f) * env; }); }
                src.PlayOneShot(boom, 0.9f);
            }
            catch { }
        }

        public static void Zap()
        {
            try
            {
                Init();
                if (zap == null) zap = Make("pf_zap", 22050, 0.5f, (t, k) => { float env = Mathf.Exp(-t * 9f); return Noise() * env * (0.5f + 0.5f * Mathf.Sin(t * 900f)) + Mathf.Sin(t * 3000f * (1f - k)) * 0.25f * env; });
                src.PlayOneShot(zap, 0.8f);
            }
            catch { }
        }

        public static void LaserStart()
        {
            try
            {
                Init();
                if (hum == null) { double p = 0; hum = Make("pf_hum", 22050, 0.5f, (t, k) => { p += (190.0 + 25.0 * Math.Sin(t * 2 * Math.PI * 4)) / 22050.0; float saw = (float)((p % 1.0) * 2.0 - 1.0); return saw * 0.35f + Noise() * 0.06f; }); }
                loop.clip = hum; loop.volume = 0.5f; loop.Play();
            }
            catch { }
        }

        static AudioClip splat, beep, fan;
        public static void Splat(float p)
        {
            try
            {
                Init();
                if (splat == null) { float lp = 0f; splat = Make("pf_splat", 22050, 0.35f, (t, k) => { float env = Mathf.Exp(-t * 13f); lp += (Noise() - lp) * 0.3f; return (lp * 0.95f + Noise() * 0.12f) * env; }); }
                src.PlayOneShot(splat, 0.3f + 0.45f * Mathf.Clamp01(p));
            }
            catch { }
        }

        public static void Beep(float vol = 0.5f, bool high = false)
        {
            try
            {
                Init();
                if (beep == null) beep = Make("pf_beep", 22050, 0.12f, (t, k) => Mathf.Sin(t * 2f * Mathf.PI * 880f) * Mathf.Sin(k * Mathf.PI) * 0.8f);
                src.PlayOneShot(beep, vol);
            }
            catch { }
        }

        public static void Fanfare()
        {
            try
            {
                Init();
                if (fan == null) fan = Make("pf_fan", 22050, 1.4f, (t, k) =>
                {
                    float[] n = { 523.25f, 659.25f, 783.99f, 1046.5f }; int i = Mathf.Min(3, (int)(t / 0.22f)); float f = n[i];
                    float env = Mathf.Exp(-((t - i * 0.22f) * 3f)) * (t > 0.88f ? Mathf.Exp(-(t - 0.88f) * 2f) : 1f);
                    return (Mathf.Sin(t * 2f * Mathf.PI * f) + 0.4f * Mathf.Sin(t * 4f * Mathf.PI * f)) * 0.35f * env;
                });
                src.PlayOneShot(fan, 0.8f);
            }
            catch { }
        }

        static AudioClip hit, crunch;
        public static void Hit(float p)
        {
            try
            {
                Init();
                if (hit == null) { float lp = 0f; double ph = 0; hit = Make("pf_hit", 22050, 0.28f, (t, k) => { float env = Mathf.Exp(-t * 16f); ph += (140.0 - 90.0 * k) / 22050.0; lp += (Noise() - lp) * 0.35f; return (Mathf.Sin((float)(ph * Mathf.PI * 2)) * 0.9f + lp * 0.5f) * env; }); }
                src.pitch = 0.9f + UnityEngine.Random.value * 0.25f; src.PlayOneShot(hit, 0.45f + 0.5f * Mathf.Clamp01(p)); src.pitch = 1f;
            }
            catch { }
        }

        public static void Crunch(float p)
        {
            try
            {
                Init();
                if (crunch == null) { float lp = 0f; crunch = Make("pf_crunch", 22050, 0.6f, (t, k) => { float env = Mathf.Exp(-t * 7f) * (1f + 0.6f * Mathf.Sin(t * 70f)); lp += (Noise() - lp) * 0.55f; return (lp * 0.9f + Noise() * 0.25f * Mathf.Exp(-t * 25f)) * env; }); }
                src.pitch = 0.8f + UnityEngine.Random.value * 0.3f; src.PlayOneShot(crunch, 0.4f + 0.5f * Mathf.Clamp01(p)); src.pitch = 1f;
            }
            catch { }
        }

        static AudioClip domc, slashc;
        public static void DomainStart()
        {
            try
            {
                Init();
                if (domc == null)
                {
                    double p1 = 0, p2 = 0; float lp = 0f;
                    domc = Make("pf_dom", 22050, 3.4f, (t, k) =>
                    {
                        float rise = Mathf.Clamp01(t / 1.4f);
                        p1 += (48.0 + 40.0 * rise) / 22050.0; p2 += (96.0 + 120.0 * rise * rise) / 22050.0;
                        lp += (Noise() - lp) * Mathf.Lerp(0.03f, 0.4f, rise);
                        float drone = (float)(Math.Sin(p1 * 2 * Math.PI) * 0.6 + Math.Sin(p2 * 2 * Math.PI) * 0.25);
                        float env = Mathf.Min(1f, t / 0.3f) * Mathf.Exp(-Mathf.Max(0f, t - 1.4f) * 0.9f);
                        float gt = Mathf.Max(0f, t - 1.4f);
                        float gong = t > 1.4f ? (float)(Math.Sin(gt * 2 * Math.PI * 110.0) * 0.5 + Math.Sin(gt * 2 * Math.PI * 277.0) * 0.3 + Math.Sin(gt * 2 * Math.PI * 431.0) * 0.2) * Mathf.Exp(-gt * 2.2f) : 0f;
                        float nz = lp * rise * 0.5f * (t < 1.4f ? 1f : Mathf.Exp(-gt * 4f));
                        return drone * env * 0.8f + nz + gong * 0.9f;
                    });
                }
                src.PlayOneShot(domc, 1f);
            }
            catch { }
        }

        public static void Slash()
        {
            try
            {
                Init();
                if (slashc == null) { float lp = 0f; slashc = Make("pf_slash", 22050, 0.3f, (t, k) => { float env = Mathf.Exp(-t * 14f); float n = Noise(); lp += (n - lp) * 0.25f; return ((n - lp) * 0.8f * env + (float)Math.Sin(t * 2 * Math.PI * 1900.0 * (1f - 0.3f * k)) * 0.18f * Mathf.Exp(-t * 22f)); }); }
                src.pitch = 0.9f + UnityEngine.Random.value * 0.3f; src.PlayOneShot(slashc, 0.55f); src.pitch = 1f;
            }
            catch { }
        }

        public static void LaserStop() { try { if (loop != null) loop.Stop(); } catch { } }

        static AudioClip bounceC, swishC, clankC, boardC;
        public static void Bounce(float vol)
        {
            try
            {
                Init();
                if (bounceC == null) bounceC = Make("pf_bounce", 22050, 0.24f, (t, k) => { float env = Mathf.Exp(-t * 24f); return (Mathf.Sin(t * 2f * Mathf.PI * (110f - 40f * k)) * 0.95f + Noise() * 0.1f) * env; });
                src.PlayOneShot(bounceC, 0.25f + 0.7f * Mathf.Clamp01(vol));
            }
            catch { }
        }
        public static void Swish()
        {
            try
            {
                Init();
                if (swishC == null) { float lp = 0f; swishC = Make("pf_swish", 22050, 0.5f, (t, k) => { lp += (Noise() - lp) * 0.6f; float env = Mathf.Sin(Mathf.Clamp01(k * 1.4f) * Mathf.PI) * Mathf.Exp(-k * 2f); return (lp * 0.6f + Noise() * 0.25f) * env * 0.9f; }); }
                src.PlayOneShot(swishC, 0.8f);
            }
            catch { }
        }
        public static void Clank(float vol)
        {
            try
            {
                Init();
                if (clankC == null) clankC = Make("pf_clank", 22050, 0.5f, (t, k) => { float env = Mathf.Exp(-t * 8f); return (Mathf.Sin(t * 2f * Mathf.PI * 640f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 1480f) * 0.35f + Mathf.Sin(t * 2f * Mathf.PI * 2310f) * 0.2f) * env; });
                src.PlayOneShot(clankC, 0.3f + 0.6f * Mathf.Clamp01(vol));
            }
            catch { }
        }
        public static void Board()
        {
            try
            {
                Init();
                if (boardC == null) boardC = Make("pf_board", 22050, 0.45f, (t, k) => { float env = Mathf.Exp(-t * 9f); return (Mathf.Sin(t * 2f * Mathf.PI * 170f) * 0.8f + Noise() * 0.3f * Mathf.Exp(-t * 40f)) * env; });
                src.PlayOneShot(boardC, 0.8f);
            }
            catch { }
        }
    }
}
