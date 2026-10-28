// Боёвка героев 1.10.
//   ЛКМ — комбо (Патриот/Громила: 3 удара, Неуязвимый: 5 быстрых), последний удар — мощный отброс.
//   Зажать ПКМ — зарядить тяжёлый удар, отпустить — рывок и удар с кратером и разрушением.
//   Q — уклон-рывок, C (держать) — стойка защиты.
//   Лёта быстро (>14 м/с) + ЛКМ — таран: проносишься сквозь врагов и ломаешь всё на пути.
//   Пока держишь за горло: ЛКМ — бить схваченного (он остаётся в руке), ПКМ — удар о землю.
using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        ConfigEntry<KeyCode> keyDodge, keyGuard;
        bool combatOn = true, combatItems, hitStop = true;
        int comboN, comboCount; float comboT, nextAtk, comboShowUntil, nextDodge, chargeT0, ramUntil, landPoseUntil, lastHitT;
        bool charging, guardOn, strikeBusy, wasFlying; float lastFlySpeed;
        int atkType; float atkT0 = -10f; Vector3 atkDir;
        static bool hsActive;

        // ---------- вспомогательное ----------
        bool GroundBelow(Component c)
        {
            RaycastHit h; return WorldCast(CenterOf(c), 0.25f, Vector3.down, 1.9f, out h);
        }

        Component BestTarget(float maxDist)
        {
            var me = Game.Me(); var cam = Camera.main; if (me == null || cam == null) return null;
            Vector3 o = CenterOf(me), f = cam.transform.forward; Component best = null; float bs = float.MaxValue;
            foreach (var c in Victims())
            {
                Vector3 to = CenterOf(c) - o; float d = to.magnitude; if (d > maxDist || d < 0.05f) continue;
                float dot = Vector3.Dot(f, to / d); if (dot < 0.15f) continue;
                float score = d * (2.2f - dot); if (score < bs) { bs = score; best = c; }
            }
            return best;
        }

        bool HoldingItem(Component me)
        {
            var it = R.Get(R.Get(me, "data"), "currentItem") as UnityEngine.Object;
            return it != null;
        }

        int ComboLen() { return hero == HeroKind.Invincible ? 5 : hero == HeroKind.Sukuna ? 4 : 3; }
        float ComboDelay() { switch (hero) { case HeroKind.Invincible: return 0.17f; case HeroKind.Patriot: return 0.4f; case HeroKind.Brute: return 0.5f; default: return 0.32f; } }

        static Color HeroAccent(HeroKind k)
        {
            switch (k)
            {
                case HeroKind.Patriot: return new Color(0.9f, 0.95f, 1f, 0.9f);
                case HeroKind.Invincible: return new Color(1f, 0.88f, 0.2f, 0.9f);
                case HeroKind.Flash: return new Color(1f, 0.95f, 0.3f, 0.9f);
                case HeroKind.Brute: return new Color(0.5f, 1f, 0.3f, 0.9f);
                case HeroKind.Phantom: return new Color(0.2f, 1f, 1f, 0.8f);
                case HeroKind.Gojo: return new Color(0.5f, 0.8f, 1f, 0.9f);
                case HeroKind.Sukuna: return new Color(1f, 0.25f, 0.2f, 0.9f);
                default: return new Color(0.8f, 0.5f, 1f, 0.9f);
            }
        }

        IEnumerator CoHitStop(float dur)
        {
            if (dur >= 0.08f) ImpactFrame();
            if (!hitStop || hsActive) yield break;
            hsActive = true; float prev = Time.timeScale <= 0.2f ? 1f : Time.timeScale;
            Time.timeScale = 0.07f;
            yield return new WaitForSecondsRealtime(dur);
            Time.timeScale = prev; hsActive = false;
        }

        void CombatReset()
        {
            if (hsActive) { Time.timeScale = 1f; hsActive = false; }
            charging = false; strikeBusy = false; atkType = 0; ramUntil = 0f; guardOn = false; ClearTrails();
        }

        // ---------- главный тик ----------
        void TickCombat(Component me, bool typing)
        {
            guardOn = !typing && Keys.Held(keyGuard.Value);
            TickTrails(me);
            if (!combatOn || typing) { charging = false; return; }
            if (ballHeld) { charging = false; return; }
            if (Cursor.lockState != CursorLockMode.Locked) { charging = false; return; }
            if (!combatItems && HoldingItem(me)) { charging = false; return; }
            bool holdArm = grabbed != null && !grabTele;
            if (grabbed != null && grabTele) return;     // телекинез — не бить

            if (Keys.Down(keyDodge.Value) && Time.time >= nextDodge) Dodge();

            if (Keys.MouseDown(0) && Time.time >= nextAtk && !strikeBusy)
            {
                if (flying && flyVel.magnitude > 14f && !holdArm) StartCoroutine(CoRam(me));
                else
                {
                    if (Time.time - comboT > 1.0f) comboN = 0;
                    int idx = comboN; bool last = !holdArm && idx >= ComboLen() - 1;
                    bool big = last || (holdArm && (idx + 1) % 4 == 0);
                    StartCoroutine(CoStrike(me, idx, big, last));
                    comboN = last ? 0 : comboN + 1; comboT = Time.time; nextAtk = Time.time + ComboDelay();
                }
            }

            if (Keys.MouseDown(1) && !strikeBusy)
            {
                if (holdArm) { if (!grabBusy) StartCoroutine(CoGrabSlam(me)); }
                else if (Time.time >= nextAtk) { charging = true; chargeT0 = Time.time; }
            }
            if (charging)
            {
                float c = Time.time - chargeT0;
                if (Time.time % 0.22f < Time.deltaTime) { Vfx.Ring(HandPos(me), CamForward(), HeroAccent(hero), 1.2f + c, 0.25f); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.12f + 0.1f * c); }
                if (!Keys.MouseHeld(1) || c > 2.2f)
                {
                    charging = false;
                    if (c >= 0.35f) StartCoroutine(CoHeavy(me, Mathf.Clamp01((c - 0.35f) / 0.9f)));
                }
            }
        }

        Vector3 HandPos(Component me)
        {
            var pm = PartMap.Of(me); if (pm != null && pm.handR != null) return pm.handR.position;
            return CenterOf(me) + FlatForward() * 0.6f;
        }

        // ---------- уклон ----------
        void Dodge()
        {
            var me = Game.Me(); if (me == null) return;
            nextDodge = Time.time + 0.9f;
            if (hero == HeroKind.Gojo) { nextDodge = Time.time + 1.2f; Blink(me); return; }
            var cam = Camera.main; Vector3 d = Vector3.zero;
            if (cam != null)
            {
                Vector3 f = FlatForward(), r = Vector3.Cross(Vector3.up, f);
                if (Keys.Held(KeyCode.W)) d += f; if (Keys.Held(KeyCode.S)) d -= f; if (Keys.Held(KeyCode.D)) d += r; if (Keys.Held(KeyCode.A)) d -= r;
                if (d.sqrMagnitude < 0.01f) d = -f;
            }
            d.Normalize();
            Dash(d + Vector3.up * 0.1f, 24f, 0.2f); Sfx2.Whoosh(); Shade.Set(ShadeKind.Speed, 0.35f);
            Vfx.Ring(CenterOf(me), d, new Color(1f, 1f, 1f, 0.5f), 3.5f, 0.3f);
        }

        // ---------- обычный удар комбо ----------
        IEnumerator CoStrike(Component me, int idx, bool big, bool last)
        {
            strikeBusy = true;
            var target = grabbed != null && !grabTele ? grabbed : BestTarget(hero == HeroKind.Invincible ? 6.5f : 5.5f);
            Vector3 myC = CenterOf(me);
            Vector3 dir = target != null ? (CenterOf(target) - myC).normalized : CamForward();
            atkDir = dir; armDir = dir;
            atkType = last ? 3 : (idx % 2 == 0 ? 1 : 2); if (grabbed != null && !grabTele) atkType = 1; atkT0 = Time.time;
            float dist = target != null ? Vector3.Distance(myC, CenterOf(target)) : 99f;
            bool lunge = target != null && grabbed == null && dist > 1.9f;
            if (lunge) Dash(dir, Mathf.Clamp(dist * 9f, 14f, 30f), 0.16f);
            Sfx2.Whoosh();
            if (last && hero == HeroKind.Invincible) { StartCine(4, 0.7f); }
            yield return new WaitForSeconds(big ? 0.15f : 0.08f);
            if (lunge) dashUntil = 0f;
            bool done = false;
            if (target != null && Vector3.Distance(CenterOf(me), CenterOf(target)) < (grabbed != null ? 3.2f : 4.4f))
            { HitTarget(me, target, idx, big, dir, 1f + 0.08f * Mathf.Min(idx, 6)); done = true; }
            if (!done) PunchWorld(me, dir, big);
            strikeBusy = false;
        }

        void PunchWorld(Component me, Vector3 dir, bool big)
        {
            Vector3 c = CenterOf(me); RaycastHit h; Vector3 pt = c + dir * 2.2f; bool wall = false;
            if (WorldCast(c, 0.45f, dir, 3.2f, out h)) { pt = h.point; wall = true; }
            float pw = (big ? 1.5f : 0.8f) * heroPower;
            int n = Destruct.Smash(pt, dir, big ? 2.3f : 1.4f, pw);
            if (wall)
            {
                Vfx.Sparks(pt, -dir, big ? 22 : 10); Sfx2.Hit(big ? 0.8f : 0.4f); CamShake.Boost = Mathf.Max(CamShake.Boost, big ? 0.6f : 0.25f);
                Gore.Decal(pt, h.normal, null, big ? 1.2f : 0.7f, 1f, false, true, 70f); Destruct.Chips(pt, h.normal, big ? 1f : 0.5f);
                if (big) Gore.Impact(pt, h.normal, 0.9f);
            }
            else if (n > 0) { Sfx2.Hit(0.5f); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.3f); }
        }

        bool GroundBelowTarget(Component t) { return GroundBelow(t); }

        void HitTarget(Component me, Component t, int idx, bool big, Vector3 dir, float mult)
        {
            bool held = grabbed == t;
            bool air = !held && !GroundBelow(t);
            float roleMul = (hero == HeroKind.Patriot || hero == HeroKind.Brute) ? 1.25f : 1f;
            float pw = (big ? 1.9f : 0.45f + 0.1f * Mathf.Min(idx, 4)) * heroPower * mult * roleMul;
            Vector3 flat = dir; flat.y = 0f; flat = flat.sqrMagnitude < 0.01f ? FlatForward() : flat.normalized;
            Vector3 fd = (flat + Vector3.up * (big ? 0.5f : air ? 0.55f : 0.15f)).normalized;
            Vector3 hp = CenterOf(t) + Vector3.up * 0.25f - dir * 0.2f;
            if (held)
            {
                Game.Rpc(t, "RPCA_AddForceToBodyPart", gIdx, dir * launchForce * (big ? 0.14f : 0.07f) * mult);
                Game.Rpc(t, "RPCA_Fall", 3f); nextFallSend = Time.time + 2.2f;
            }
            else
            {
                Rpc(t, "RPCA_Fall", big ? 4.5f : 1.4f);
                Rpc(t, "RPCA_AddForceToBodyPart", 0, fd * launchForce * (big ? 1.5f : 0.3f + 0.06f * Mathf.Min(idx, 4)) * heroPower * mult * roleMul);
            }
            Gore.Burst(hp, fd, (held ? (big ? 1.5f : 0.9f) : big ? 2.0f : 0.7f + 0.1f * Mathf.Min(idx, 4)) * Mathf.Min(mult, 1.8f));
            Vfx.Sparks(hp, fd, big ? 34 : 14);
            Vfx.Ring(hp, dir, new Color(1f, 1f, 1f, 0.95f), big ? 7f : 3f, big ? 0.38f : 0.22f);
            Sfx2.Hit(big ? 1f : 0.5f);
            CamShake.Boost = Mathf.Max(CamShake.Boost, big ? 1f : 0.35f);
            StartCoroutine(CoHitStop(big ? 0.1f : 0.035f));
            if (!held) Destruct.Smash(hp, fd, big ? 2.2f : 1.0f, pw);
            if (big && !held)
            {
                TrackImpact(t, 3.5f);
                if (hero == HeroKind.Patriot) Shockwave(hp, 6f, 0.5f);
                if (hero == HeroKind.Invincible) Dash(fd, 38f, 0.32f);      // погнался за отлетевшим
            }
            comboCount = (Time.time - lastHitT > 1.6f) ? 1 : comboCount + 1; lastHitT = Time.time; comboShowUntil = Time.time + 1.8f;
            if (air) HeroToast("В воздухе!");
        }

        // ---------- тяжёлый удар ----------
        IEnumerator CoHeavy(Component me, float charge)
        {
            strikeBusy = true; nextAtk = Time.time + 0.9f;
            var target = BestTarget(14f);
            Vector3 dir = target != null ? (CenterOf(target) - CenterOf(me)).normalized : CamForward();
            atkDir = dir; armDir = dir; atkType = 4; atkT0 = Time.time;
            Sfx2.Whoosh(); Shade.Set(ShadeKind.Speed, 0.5f);
            if (hero == HeroKind.Invincible || charge > 0.7f) StartCine(4, 0.85f);
            yield return new WaitForSeconds(0.12f);
            Dash(dir, 44f, 0.45f);
            float end = Time.time + 0.45f; bool near = false;
            while (Time.time < end)
            {
                if (target != null && Vector3.Distance(CenterOf(me), CenterOf(target)) < 2.9f) { near = true; break; }
                RaycastHit wh; if (target == null && WorldCast(CenterOf(me), 0.5f, dir, 2.6f, out wh)) break;
                yield return null;
            }
            dashUntil = 0f;
            Vector3 c = CenterOf(me) + dir * 1.8f;
            float pw = 1.5f + 1.5f * charge;
            if (target != null && (near || Vector3.Distance(CenterOf(me), CenterOf(target)) < 4f)) HitTarget(me, target, 4, true, dir, 1f + charge);
            RaycastHit gh; Vector3 gp = c, gn = Vector3.up;
            if (Physics.Raycast(c + Vector3.up, Vector3.down, out gh, 6f, ~0, QueryTriggerInteraction.Ignore)) { gp = gh.point; gn = gh.normal; }
            Gore.Impact(gp, gn, 1f + charge);
            Shockwave(c, 7f + 4f * charge, 0.5f + 0.4f * charge);
            Destruct.Smash(c, dir, 3.2f + 2f * charge, pw * 1.5f);
            CamShake.Boost = 1f; StartCoroutine(CoHitStop(0.12f));
            HeroToast(charge > 0.7f ? "СОКРУШИТЕЛЬНЫЙ УДАР!" : "Тяжёлый удар!");
            strikeBusy = false;
        }

        // ---------- таран ----------
        IEnumerator CoRam(Component me)
        {
            strikeBusy = true; nextAtk = Time.time + 1.4f;
            Vector3 dir = flyVel.sqrMagnitude > 4f ? flyVel.normalized : CamForward();
            float sp = Mathf.Max(48f, flyVel.magnitude * 1.25f);
            atkDir = dir; armDir = dir; ramUntil = Time.time + 0.75f;
            Dash(dir, sp, 0.7f); Sfx2.Whoosh(); Shade.Set(ShadeKind.Speed, 0.7f);
            var hitSet = new HashSet<Component>(); float end = Time.time + 0.7f, nextSmash = 0f;
            while (Time.time < end)
            {
                Vector3 c = CenterOf(me);
                foreach (var v in Victims())
                    if (!hitSet.Contains(v) && Vector3.Distance(c, CenterOf(v)) < 3f) { hitSet.Add(v); HitTarget(me, v, 4, true, dir, 0.9f); }
                if (Time.time >= nextSmash) { nextSmash = Time.time + 0.07f; Destruct.Smash(c + dir * 1.6f, dir, 1.9f, 1.3f); }
                RaycastHit wh;
                if (WorldCast(c, 0.5f, dir, 3.2f, out wh))
                {
                    Gore.Impact(wh.point, wh.normal, 1.6f); Gore.Decal(wh.point, wh.normal, null, 1.4f, 1f, false, true, 80f);
                    Vfx.Sparks(wh.point, wh.normal, 40); Shockwave(wh.point + wh.normal * 0.5f, 8f, 0.8f); StartCoroutine(CoHitStop(0.1f));
                    break;
                }
                yield return null;
            }
            dashUntil = 0f; ramUntil = Time.time + 0.2f; strikeBusy = false;
        }

        // ---------- след от кулаков ----------
        readonly Dictionary<Rigidbody, TrailRenderer> trails = new Dictionary<Rigidbody, TrailRenderer>();

        void EnsureTrail(Rigidbody hand)
        {
            if (hand == null) return;
            TrailRenderer tr;
            if (trails.TryGetValue(hand, out tr) && tr != null) return;
            if (Vfx.Sh() == null) return;
            var go = new GameObject("PF_Trail"); go.transform.SetParent(hand.transform, false);
            tr = go.AddComponent<TrailRenderer>(); tr.material = Vfx.Mat(null, Color.white);
            tr.time = 0.24f; tr.minVertexDistance = 0.04f; tr.emitting = false; tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(1f, 0f));
            var c = HeroAccent(hero); var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(c, 0.4f), new GradientColorKey(c, 1f) }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            trails[hand] = tr;
        }

        void TickTrails(Component me)
        {
            if (hero == HeroKind.None) { if (trails.Count > 0) ClearTrails(); return; }
            var pm = PartMap.Of(me); if (pm == null || !pm.limbs) return;
            EnsureTrail(pm.handR); EnsureTrail(pm.handL);
            bool want = Time.time - atkT0 < 0.45f || charging || Time.time < ramUntil || (hero == HeroKind.Invincible && flying && flyVel.magnitude > 20f);
            foreach (var kv in trails) if (kv.Value != null) kv.Value.emitting = want;
        }

        void ClearTrails()
        {
            foreach (var kv in trails) if (kv.Value != null) Destroy(kv.Value.gameObject);
            trails.Clear();
        }

        // ---------- счётчик комбо ----------
        void DrawCombatHud()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (hero == HeroKind.None) return;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f);
            if (Time.time < comboShowUntil && comboCount >= 2)
            {
                float a = Mathf.Clamp01((comboShowUntil - Time.time) / 0.5f); float pop = 1f + 0.35f * Mathf.Exp(-(Time.time - lastHitT) * 9f);
                var col = Color.Lerp(Color.white, comboCount >= 5 ? new Color(1f, 0.35f, 0.2f) : Ui.Accent, 0.6f); col.a = a;
                Ui.Text(new Rect(Screen.width - 420 * s, Screen.height * 0.34f, 380 * s, 70 * s), "x" + comboCount, (int)(60 * s * pop), col, TextAnchor.MiddleRight, true);
                Ui.Text(new Rect(Screen.width - 420 * s, Screen.height * 0.34f + 64 * s, 380 * s, 28 * s), comboCount >= 8 ? "НЕУДЕРЖИМЫЙ" : comboCount >= 5 ? "ЖЁСТКО" : "КОМБО", (int)(20 * s), new Color(1, 1, 1, a), TextAnchor.MiddleRight, true);
            }
            if (charging)
            {
                float c = Mathf.Clamp01((Time.time - chargeT0 - 0.35f) / 0.9f); float w = 220 * s, x = Screen.width * 0.5f - w * 0.5f, y = Screen.height * 0.62f;
                Ui.Box(new Rect(x, y, w, 10 * s), new Color(1, 1, 1, 0.15f), 5 * s);
                if (Time.time - chargeT0 > 0.35f) Ui.Box(new Rect(x, y, w * c, 10 * s), c >= 0.99f ? Ui.Good : Ui.Accent, 5 * s);
                else Ui.Box(new Rect(x, y, w * Mathf.Clamp01((Time.time - chargeT0) / 0.35f) * 0.1f, 10 * s), Ui.Muted, 5 * s);
            }
        }
    }
}
