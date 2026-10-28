// Герои 1.11: Архимаг Альбус и Владыка Морок — техники, пассивки и «Великий купол».
// Способности только с перезарядками (без полосы энергии). Всё, что можно, идёт через настоящую физику игры (RPC),
// красивости — локально и у друзей с PeakFun.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakFun
{
    // ======================= общие визуальные примитивы =======================
    internal static class AnimVfx
    {
        static void NoShadow(MeshRenderer mr) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; }

        public static GameObject Sph(Color c, float size, Vector3 pos, Transform parent = null)
        {
            if (Vfx.Sh() == null) return null;
            var g = new GameObject("PF_orb");
            if (parent != null) g.transform.SetParent(parent, false);
            g.transform.position = pos; g.transform.localScale = Vector3.one * size;
            g.AddComponent<MeshFilter>().sharedMesh = Vfx.Sphere();
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = Vfx.Mat(null, c); NoShadow(mr);
            return g;
        }

        /// <summary>Светящийся шар: ядро + два полупрозрачных ореола.</summary>
        public static GameObject OrbGo(Color core, Color glow, float size)
        {
            if (Vfx.Sh() == null) return null;
            var root = new GameObject("PF_Orb");
            Sph(core, size, Vector3.zero, root.transform);
            var g1 = glow; g1.a = 0.38f; Sph(g1, size * 1.7f, Vector3.zero, root.transform);
            var g2 = glow; g2.a = 0.16f; Sph(g2, size * 2.7f, Vector3.zero, root.transform);
            return root;
        }

        public static void SetOrbScale(GameObject orb, float k)
        {
            if (orb != null) orb.transform.localScale = Vector3.one * k;
        }

        public static void Flash(Vector3 p, Color c, float maxSize, float dur)
        {
            var g = Sph(c, 0.1f, p); if (g == null) return;
            var m = g.GetComponent<MeshRenderer>().sharedMaterial;
            Vfx.Add(g, dur, (k, go) =>
            {
                float e = 1f - Mathf.Pow(1f - k, 3f); go.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, maxSize, e);
                var cc = c; cc.a = c.a * (1f - k); m.color = cc;
            });
        }

        public static LineRenderer Line(Vector3[] pts, Color c, float w, float dur)
        {
            if (Vfx.Sh() == null || pts == null || pts.Length < 2) return null;
            var go = new GameObject("PF_Line"); var lr = go.AddComponent<LineRenderer>();
            lr.material = Vfx.Mat(null, Color.white); lr.useWorldSpace = true; lr.numCapVertices = 3; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.positionCount = pts.Length; lr.SetPositions(pts); lr.startWidth = lr.endWidth = 1f; lr.widthMultiplier = w;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.25f));
            Vfx.Add(go, dur, (k, g) =>
            {
                var cc = c; cc.a = c.a * (1f - k * k); lr.startColor = lr.endColor = cc; lr.widthMultiplier = w * (1f - 0.75f * k);
            });
            return lr;
        }

        /// <summary>Серповидный клин, летящий вперёд.</summary>
        public static void SlashArc(Vector3 start, Vector3 dir, float speed, float life, float width, float roll, Color col)
        {
            if (Vfx.Sh() == null) return;
            dir = dir.sqrMagnitude < 0.01f ? Vector3.forward : dir.normalized;
            var go = new GameObject("PF_Slash"); go.transform.position = start; go.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, roll);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = Vfx.Mat(null, Color.white); lr.useWorldSpace = false; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            int n = 14; lr.positionCount = n; float half = 1.7f * width;
            for (int i = 0; i < n; i++) { float x = (i / (float)(n - 1) * 2f - 1f); lr.SetPosition(i, new Vector3(x * half, 0f, -x * x * half * 0.55f)); }
            lr.startWidth = lr.endWidth = 1f; lr.widthMultiplier = 0.22f * width;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
            Vfx.Add(go, Mathf.Max(0.1f, life), (k, g) =>
            {
                g.transform.position = start + dir * speed * life * k;
                var cc = col; cc.a = col.a * (1f - k * k * 0.8f); lr.startColor = lr.endColor = cc;
            });
        }

        /// <summary>Вспышка-крест из 3 линий на точке (разрез Морока).</summary>
        public static void CrossSlash(Vector3 p, float size)
        {
            var cam = Camera.main; if (cam == null || Vfx.Sh() == null) return;
            Vector3 r = cam.transform.right, u = cam.transform.up;
            float a0 = UnityEngine.Random.value * Mathf.PI;
            for (int i = 0; i < 3; i++)
            {
                float a = a0 + i * Mathf.PI / 3f + UnityEngine.Random.Range(-0.15f, 0.15f);
                Vector3 d = (r * Mathf.Cos(a) + u * Mathf.Sin(a)) * size;
                Line(new[] { p - d, p - d * 0.3f, p + d * 0.3f, p + d }, i == 0 ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 0.25f, 0.2f, 1f), 0.16f * size, 0.38f);
            }
            Flash(p, new Color(1f, 0.85f, 0.85f, 0.8f), size * 0.8f, 0.22f);
        }

        /// <summary>Длинный клин по земле: линия, прилипающая к рельефу.</summary>
        public static void GroundCut(Vector3 start, Vector3 dir, float len, Color col)
        {
            dir.y = 0f; if (dir.sqrMagnitude < 0.01f) return; dir.Normalize();
            var pts = new List<Vector3>();
            for (float d = 0f; d <= len; d += 1.5f)
            {
                Vector3 p = start + dir * d; RaycastHit h;
                if (Physics.Raycast(p + Vector3.up * 25f, Vector3.down, out h, 60f, ~0, QueryTriggerInteraction.Ignore)) p = h.point;
                pts.Add(p + Vector3.up * 0.25f);
            }
            Line(pts.ToArray(), col, 0.55f, 1.3f);
            Line(pts.ToArray(), new Color(1f, 1f, 1f, 0.9f), 0.18f, 0.8f);
        }
    }

    public partial class PeakFunPlugin
    {
        // ---------- настройки и состояние ----------
        ConfigEntry<KeyCode> keyAb3, keyDomain, keyTest;
        bool soloTest, echoShade = true;

        void SetSoloTest(bool on)
        {
            soloTest = on; Net.Echo = on;
            if (on)
            {
                costumeSync = true;
                if (npc == null) Safe("бот", SpawnCompanion);
                Ui.Toast("Тест в одиночку ВКЛ: бот-спутник — жертва, а купол, шары и лучи приходят и тебе «от друга». [" + keyTest.Value + "] — выключить", Ui.Good);
            }
            else Ui.Toast("Тест в одиночку выкл", Ui.Muted);
        }
        float cdC, cdDom;
        float domR = 26f, domDur = 25f, domCd = 60f, domPow = 1f; bool domBarrier = true, infinityOn = true;
        internal bool domActive, domCasting, purpleCharge;
        HeroKind domKind; Vector3 domC; float domEnd; Coroutine domCo;
        DomainFx domFx;
        float animUntil; int animMode;
        internal static readonly HashSet<int> domActors = new HashSet<int>();
        readonly Dictionary<int, DomainFx> remoteDoms = new Dictionary<int, DomainFx>();
        readonly Dictionary<int, GameObject> remoteOrbs = new Dictionary<int, GameObject>();
        readonly Dictionary<int, float> remoteOrbT = new Dictionary<int, float>();
        static readonly CultureInfo inv2 = CultureInfo.InvariantCulture;

        static bool IsAnime(HeroKind k) { return k == HeroKind.Gojo || k == HeroKind.Sukuna; }

        static string HeroC(HeroKind k) { if (IsHoop(k)) return HoopAbName(k, 2); switch (k) { case HeroKind.Gojo: return "Луч"; case HeroKind.Sukuna: return "Огненная стрела"; } return ""; }
        static string HeroDomainName(HeroKind k) { return k == HeroKind.Gojo ? "Звёздная бездна" : "Багровый чертог"; }

        void AnimPose(int mode, float dur) { animMode = mode; animUntil = Time.time + dur; }

        // ---------- вспомогательное ----------
        void Quiet(Component c, string method, params object[] args)
        {
            if (c == null) return;
            try { Game.Rpc(c, method, args); } catch { }
        }

        void HitV(Component c, Vector3 dir, float force, float fall)
        {
            if (c == null) return;
            if (fall > 0f) Quiet(c, "RPCA_Fall", fall);
            Quiet(c, "RPCA_AddForceToBodyPart", 0, dir.normalized * launchForce * force * heroPower);
        }

        bool CastNoMe(Vector3 o, Vector3 d, float dist, float rad, out RaycastHit best)
        {
            best = default(RaycastHit);
            var me = Game.Me(); var ct = R.FindType("Character");
            RaycastHit[] hits = rad > 0.01f ? Physics.SphereCastAll(o, rad, d, dist, ~0, QueryTriggerInteraction.Ignore) : Physics.RaycastAll(o, d, dist, ~0, QueryTriggerInteraction.Ignore);
            float bd = float.MaxValue; bool got = false;
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                var cc = ct != null ? h.collider.GetComponentInParent(ct) : null;
                if (cc != null && me != null && cc == me) continue;
                if (h.distance < bd) { bd = h.distance; best = h; got = true; }
            }
            if (got && best.distance <= 0.0001f) best.point = o;
            return got;
        }

        Vector3 AimPoint(float maxDist, out Vector3 normal, out bool got)
        {
            normal = Vector3.up; got = false;
            var cam = Camera.main; if (cam == null) return Vector3.zero;
            RaycastHit h;
            if (CastNoMe(cam.transform.position, cam.transform.forward, maxDist, 0f, out h)) { normal = h.normal; got = true; return h.point; }
            return cam.transform.position + cam.transform.forward * maxDist;
        }

        static bool IsChar(Collider c, Type ct, Component me)
        {
            if (c == null || ct == null) return false;
            return c.GetComponentInParent(ct) != null;
        }

        void ForEachProp(Vector3 center, float radius, Action<Rigidbody> f)
        {
            var ct = R.FindType("Character");
            foreach (var col in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = col.attachedRigidbody; if (rb == null || rb.isKinematic) continue;
                if (ct != null && col.GetComponentInParent(ct) != null) continue;
                f(rb);
            }
        }

        static string F(float v) { return v.ToString("F2", inv2); }
        static string V3(Vector3 v) { return F(v.x) + "," + F(v.y) + "," + F(v.z); }

        void SendAfx(int kind, Vector3 p, Vector3 d)
        {
            if (Game.InRoom) Net.Send("afx", kind + "," + V3(p) + "," + V3(d));
        }

        // ======================= управление (из TickHero) =======================
        void TickAnimeKeys(Component me, bool typing)
        {
            if (!IsAnime(hero) && !IsHoop(hero)) return;
            if (IsHoop(hero)) { if (!typing) { if (Keys.Down(keyAb3.Value)) UseC(); if (Keys.Down(keyDomain.Value)) TryDomain(); } return; }
            if (typing) { if (blueOn) BlueStop(); disOn = false; return; }
            if (Keys.Down(keyAb3.Value)) UseC();
            if (Keys.Down(keyDomain.Value)) TryDomain();
        }

        void UseAnimeA(Component me, bool held)
        {
            if (hero == HeroKind.Gojo) BlueTick(me, held);
            else if (hero == HeroKind.Sukuna) DismantleTick(me, held);
        }

        public void UseC()
        {
            if (IsHoop(hero)) { HoopCast(2); return; }
            if (!IsAnime(hero)) { Ui.Toast("Третья техника есть только у Альбус, Морока и баскетболистов", Ui.Muted); return; }
            var me = Game.Me(); if (me == null) return;
            if (Time.time < cdC) { HeroToast(HeroC(hero) + ": перезарядка " + Mathf.CeilToInt(cdC - Time.time) + " с"); return; }
            if (domCasting) return;
            if (hero == HeroKind.Gojo) StartCoroutine(CoPurple(me)); else StartCoroutine(CoFuga(me));
        }

        void StopAnime()
        {
            CancelBuffs(); if (zoneOn || zoneCasting) EndZone();
            if (blueOn) BlueStop(); disOn = false; purpleCharge = false;
            if (domCo != null) { StopCoroutine(domCo); domCo = null; }
            domCasting = false;
            if (domActive) EndDomain();
        }

        // ======================= Альбус: пассивка «Щит» =======================
        GameObject infGo, blueGo; float nextInf;

        void TickInfinity(Component me)
        {
            bool on = hero == HeroKind.Gojo && infinityOn && me != null && !Game.IsDead(me);
            if (!on) { if (infGo != null) infGo.SetActive(false); return; }
            Vector3 c = CenterOf(me); bool near = false;
            if (Time.time >= nextInf)
            {
                nextInf = Time.time + 0.12f;
                foreach (var v in Victims())
                {
                    Vector3 p = CenterOf(v); float d = Vector3.Distance(p, c);
                    if (d > 2.8f) continue;
                    near = true;
                    Vector3 dir = p - c; dir.y = 0f; dir = dir.sqrMagnitude < 0.01f ? Vector3.up : (dir.normalized + Vector3.up * 0.15f).normalized;
                    Quiet(v, "RPCA_AddForceToBodyPart", 0, dir * launchForce * 0.06f * (1f - d / 2.8f + 0.3f));
                }
                ForEachProp(c, 2.6f, rb =>
                {
                    if (rb.velocity.sqrMagnitude > 0.25f) { Vector3 dir = rb.position - c; if (dir.sqrMagnitude > 0.01f) rb.AddForce(dir.normalized * 6f, ForceMode.VelocityChange); near = true; }
                });
                infNear = near ? Time.time + 0.5f : infNear;
            }
            if (infGo == null)
            {
                infGo = AnimVfx.Sph(new Color(0.5f, 0.8f, 1f, 0.05f), 5.2f, c);
                if (infGo != null) UnityEngine.Object.DontDestroyOnLoad(infGo);
            }
            if (infGo != null)
            {
                infGo.SetActive(true); infGo.transform.position = c;
                float pulse = Time.time < infNear ? 0.16f + 0.05f * Mathf.Sin(Time.time * 18f) : 0.045f;
                var m = infGo.GetComponent<MeshRenderer>().sharedMaterial; if (m != null) m.color = new Color(0.5f, 0.8f, 1f, pulse);
                infGo.transform.localScale = Vector3.one * (5.2f + (Time.time < infNear ? 0.25f * Mathf.Sin(Time.time * 18f) : 0f));
            }
        }
        float infNear;

        // ======================= Альбус: Z — «Магнит» (притяжение) =======================
        bool blueOn; float blueDist = 6f, blueStart, nextBlueHit, nextBlueSend, nextBlueRing, nextBlueFall; Vector3 bluePos;

        void BlueTick(Component me, bool held)
        {
            if (!held) { if (blueOn) BlueStop(); return; }
            var cam = Camera.main; if (cam == null) return;
            if (!blueOn)
            {
                if (Time.time < cdA || domCasting) return;
                blueOn = true; blueStart = Time.time; Sfx2.LaserStart();
                Vector3 n; bool g; bluePos = AimPoint(blueDist, out n, out g);
                HeroToast("Приказ: «Магнит» — держи, чтобы притягивать");
            }
            float sc = Input.mouseScrollDelta.y; if (Mathf.Abs(sc) > 0.01f) blueDist = Mathf.Clamp(blueDist + sc * 1.2f, 3f, 14f);
            AnimPose(13, 0.2f);
            Vector3 nn; bool got;
            Vector3 target = AimPoint(blueDist, out nn, out got); if (got) target += nn * 0.6f;
            bluePos = Vector3.Lerp(bluePos, target, 1f - Mathf.Exp(-12f * Time.deltaTime));
            if (blueGo == null) { blueGo = AnimVfx.OrbGo(new Color(0.55f, 0.85f, 1f, 1f), new Color(0.2f, 0.5f, 1f, 1f), 0.9f); }
            if (blueGo != null) { blueGo.transform.position = bluePos; AnimVfx.SetOrbScale(blueGo, 1f + 0.12f * Mathf.Sin(Time.time * 22f)); }

            if (Time.time >= nextBlueRing) { nextBlueRing = Time.time + 0.2f; Vfx.Ring(bluePos, cam.transform.forward, new Color(0.4f, 0.75f, 1f, 0.8f), 6f, 0.4f); Vfx.Sparks(bluePos + UnityEngine.Random.onUnitSphere * 3f, -UnityEngine.Random.onUnitSphere, 6, new Color(0.5f, 0.8f, 1f)); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.22f); }
            if (Time.time >= nextBlueSend) { nextBlueSend = Time.time + 0.1f; SendAfx(1, bluePos, Vector3.zero); }

            if (Time.time >= nextBlueHit)
            {
                nextBlueHit = Time.time + 0.12f; bool fall = Time.time >= nextBlueFall; if (fall) nextBlueFall = Time.time + 0.6f;
                foreach (var v in Victims())
                {
                    Vector3 p = CenterOf(v); float d = Vector3.Distance(p, bluePos);
                    if (d > 12f) continue;
                    Vector3 dir = (bluePos - p).normalized;
                    if (fall) Quiet(v, "RPCA_Fall", 1.6f);
                    Quiet(v, "RPCA_AddForceToBodyPart", 0, dir * launchForce * (0.10f + 0.1f * (1f - d / 12f)) * heroPower);
                    if (d < 2.2f) { Gore.Burst(p, -dir, 0.7f); Quiet(v, "RPCA_Fall", 2.5f); }
                }
                ForEachProp(bluePos, 12f, rb =>
                {
                    Vector3 dir = bluePos - rb.position; float dd = dir.magnitude; if (dd < 0.1f) return;
                    rb.AddForce(dir / dd * Mathf.Lerp(40f, 14f, dd / 12f), ForceMode.Acceleration);
                    if (dd < 1.8f) rb.velocity *= 0.85f;
                });
                if (Time.time % 0.5f < 0.13f) Destruct.Smash(bluePos, (bluePos - cam.transform.position).normalized, 3.2f, 0.9f, true, Mathf.Max(Destruct.MaxSize, 5f));
            }
            if (Time.time - blueStart > 7f) BlueStop();
        }

        void BlueStop()
        {
            if (!blueOn) return;
            blueOn = false; cdA = Time.time + 4f; Sfx2.LaserStop();
            if (blueGo != null) { UnityEngine.Object.Destroy(blueGo); blueGo = null; }
            SendAfx(0, Vector3.zero, Vector3.zero);
            AnimVfx.Flash(bluePos, new Color(0.5f, 0.8f, 1f, 0.7f), 5f, 0.35f);
        }

        // ======================= Альбус: V — «Отброс» (отталкивание) =======================
        IEnumerator CoRed(Component me)
        {
            var cam = Camera.main; if (cam == null) yield break;
            cdB = Time.time + 6f; AnimPose(13, 0.55f);
            Vector3 n; bool g; Vector3 aim = AimPoint(150f, out n, out g);
            Vector3 start = cam.transform.position + cam.transform.forward * 1.3f + cam.transform.right * 0.25f - cam.transform.up * 0.1f;
            Vector3 dir = (aim - start).normalized;
            Sfx2.Whoosh(); SendAfx(2, start, dir); HeroToast("«Отброс»!");
            var orb = AnimVfx.OrbGo(new Color(1f, 0.55f, 0.5f, 1f), new Color(1f, 0.1f, 0.1f, 1f), 0.8f);
            Vector3 pos = start; float life = 0f; bool hit = false; RaycastHit h;
            while (life < 2.8f)
            {
                float dt = Mathf.Max(Time.deltaTime, 0.001f), step = 45f * dt;
                if (CastNoMe(pos, dir, step + 0.2f, 0.4f, out h)) { pos = h.point + h.normal * 0.2f; hit = true; break; }
                pos += dir * step; life += dt;
                if (orb != null) { orb.transform.position = pos; AnimVfx.SetOrbScale(orb, 1f + 0.1f * Mathf.Sin(Time.time * 30f)); }
                yield return null;
            }
            if (orb != null) UnityEngine.Object.Destroy(orb);
            if (!hit) yield break;
            ExplodeRed(pos, dir, true);
        }

        void ExplodeRed(Vector3 pos, Vector3 dir, bool real)
        {
            AnimVfx.Flash(pos, new Color(1f, 0.2f, 0.15f, 0.85f), 20f, 0.55f);
            AnimVfx.Flash(pos, new Color(1f, 0.9f, 0.85f, 0.9f), 8f, 0.3f);
            Vfx.Ring(pos, Vector3.up, new Color(1f, 0.3f, 0.2f, 0.9f), 16f, 0.6f);
            Vfx.Ring(pos, dir, new Color(1f, 0.5f, 0.4f, 0.7f), 12f, 0.5f);
            Vfx.Sparks(pos, Vector3.up, 40, new Color(1f, 0.3f, 0.2f));
            if (!real) { Sfx2.Boom(); return; }
            SendAfx(7, pos, dir);
            Shockwave(pos, 13f, 1.8f);
            Destruct.Smash(pos, dir, 9f, 2.6f, true, Mathf.Max(Destruct.MaxSize, 25f));
            ForEachProp(pos, 13f, rb => rb.AddExplosionForce(16f, pos, 13f, 0.4f, ForceMode.VelocityChange));
            CamShake.Boost = 1.4f;
        }

        // ======================= Альбус: H — «Луч» =======================
        IEnumerator CoPurple(Component me)
        {
            var cam = Camera.main; if (cam == null) yield break;
            cdC = Time.time + 22f; purpleCharge = true; domCasting = false;
            HeroToast("«Луч»: сходятся синий и красный…");
            var blue = AnimVfx.OrbGo(new Color(0.55f, 0.85f, 1f, 1f), new Color(0.2f, 0.5f, 1f, 1f), 0.6f);
            var red = AnimVfx.OrbGo(new Color(1f, 0.55f, 0.5f, 1f), new Color(1f, 0.1f, 0.1f, 1f), 0.6f);
            float t0 = Time.time, dur = 1.1f; Sfx2.LaserStart(); StartCine(3, 1.5f);
            while (Time.time - t0 < dur)
            {
                if (hero != HeroKind.Gojo || me == null) { purpleCharge = false; if (blue != null) UnityEngine.Object.Destroy(blue); if (red != null) UnityEngine.Object.Destroy(red); Sfx2.LaserStop(); yield break; }
                float k = (Time.time - t0) / dur; cam = Camera.main; if (cam == null) break;
                Vector3 center = cam.transform.position + cam.transform.forward * 3.2f - cam.transform.up * 0.1f;
                float sep = Mathf.Lerp(2.4f, 0.05f, k * k), wob = Mathf.Sin(Time.time * 25f) * 0.08f;
                if (blue != null) { blue.transform.position = center - cam.transform.right * sep + cam.transform.up * wob; AnimVfx.SetOrbScale(blue, Mathf.Lerp(0.8f, 1.5f, k)); }
                if (red != null) { red.transform.position = center + cam.transform.right * sep - cam.transform.up * wob; AnimVfx.SetOrbScale(red, Mathf.Lerp(0.8f, 1.5f, k)); }
                AnimPose(13, 0.2f); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.2f + 0.6f * k);
                if (Time.frameCount % 7 == 0) Vfx.Ring(center, cam.transform.forward, new Color(0.7f, 0.45f, 1f, 0.8f), 2.5f + 2f * k, 0.3f);
                yield return null;
            }
            Sfx2.LaserStop();
            if (blue != null) UnityEngine.Object.Destroy(blue); if (red != null) UnityEngine.Object.Destroy(red);
            cam = Camera.main; if (cam == null) { purpleCharge = false; yield break; }
            Vector3 o = cam.transform.position + cam.transform.forward * 1.5f, dir = cam.transform.forward;
            FirePurple(o, dir);
            SendAfx(3, o, dir);
            Dash(-dir, 12f, 0.18f);
            StartCoroutine(CoPurpleBeam(me, o, dir));
            yield return new WaitForSeconds(1.1f);
            purpleCharge = false;
        }

        void FirePurple(Vector3 o, Vector3 dir)
        {
            float len = 110f; Vector3 end = o + dir * len;
            var outer = AnimVfx.Line(new[] { o, end }, new Color(0.65f, 0.25f, 1f, 0.75f), 3.4f, 1.1f);
            var inner = AnimVfx.Line(new[] { o, end }, new Color(1f, 0.92f, 1f, 1f), 1.2f, 0.9f);
            if (outer != null) outer.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            if (inner != null) inner.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            AnimVfx.Flash(o, new Color(0.8f, 0.5f, 1f, 0.9f), 9f, 0.5f);
            Vfx.Ring(o + dir * 2f, dir, new Color(0.75f, 0.4f, 1f, 0.9f), 10f, 0.6f);
            Sfx2.Boom(); Sfx2.Zap();
            Shade.Set(ShadeKind.Power, 1.1f); CamShake.Boost = 1.6f;
        }

        IEnumerator CoPurpleBeam(Component me, Vector3 o, Vector3 dir)
        {
            float t0 = Time.time, prev = 0f; var hitSet = new HashSet<Component>();
            while (Time.time - t0 < 0.95f)
            {
                float reach = Mathf.Min(110f, (Time.time - t0) * 160f + 6f);
                for (float d = prev; d <= reach; d += 7f)
                {
                    Vector3 p = o + dir * d;
                    Destruct.Smash(p, dir, 6f, 3f, true, Mathf.Max(Destruct.MaxSize, 25f));
                    if (Time.frameCount % 2 == 0) Vfx.Dust(p, 3f, 6);
                }
                prev = reach;
                foreach (var v in Victims())
                {
                    if (hitSet.Contains(v)) continue;
                    Vector3 p = CenterOf(v); float t = Vector3.Dot(p - o, dir); if (t < 0f || t > reach) continue;
                    Vector3 perp = (p - o) - dir * t; if (perp.magnitude > 3.8f) continue;
                    hitSet.Add(v);
                    Vector3 fd = (dir + Vector3.up * 0.25f).normalized;
                    HitV(v, fd, 2.2f, 6f); Gore.Burst(p, dir, 2.2f); AnimVfx.Flash(p, new Color(0.8f, 0.5f, 1f, 0.8f), 5f, 0.3f);
                    TrackImpact(v, 2f);
                }
                Vector3 e = o + dir * reach;
                var chT = R.FindType("Character");
                foreach (var col in Physics.OverlapCapsule(o, e, 3.4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var rb = col.attachedRigidbody; if (rb == null || rb.isKinematic) continue;
                    if (chT != null && col.GetComponentInParent(chT) != null) continue;
                    rb.velocity = dir * 34f + Vector3.up * 5f; rb.AddTorque(UnityEngine.Random.onUnitSphere * 8f, ForceMode.VelocityChange);
                }
                CamShake.Boost = Mathf.Max(CamShake.Boost, 1.2f);
                yield return new WaitForSeconds(0.08f);
            }
        }

        // ======================= Альбус: Q — телепорт =======================
        void Blink(Component me)
        {
            var cam = Camera.main; if (cam == null) return;
            Vector3 o = CenterOf(me), f = cam.transform.forward; float dist = 14f; RaycastHit h;
            if (CastNoMe(o, f, dist + 1f, 0.4f, out h)) dist = Mathf.Max(1f, h.distance - 1f);
            Vector3 to = o + f * dist;
            AnimVfx.Flash(o, new Color(0.5f, 0.8f, 1f, 0.7f), 3.5f, 0.3f); Vfx.Ring(o, f, new Color(0.6f, 0.85f, 1f, 0.9f), 3.5f, 0.3f);
            Rpc(me, "WarpPlayerRPC", to, false); Game.HaltBody(me);
            flyVel = f * 6f;
            AnimVfx.Flash(to, new Color(0.5f, 0.8f, 1f, 0.7f), 3.5f, 0.35f); Vfx.Ring(to, f, new Color(0.6f, 0.85f, 1f, 0.9f), 3.5f, 0.3f);
            Sfx2.Whoosh(); Shade.Set(ShadeKind.Speed, 0.3f);
        }

        // ======================= Владыка Морок: Z — «Град клинков» (удержание) =======================
        bool disOn; float nextDis;

        void DismantleTick(Component me, bool held)
        {
            if (!held) { if (disOn) { disOn = false; cdA = Time.time + 0.4f; } return; }
            if (!disOn) { if (Time.time < cdA || domCasting) return; disOn = true; HeroToast("«Град клинков» — зажато"); }
            AnimPose(14, 0.2f);
            if (Time.time < nextDis) return; nextDis = Time.time + 0.09f;
            var cam = Camera.main; if (cam == null) return;
            Vector3 o = cam.transform.position, f = cam.transform.forward;
            Component tv = LookedVictim(40f, 0.975f);
            Vector3 d = (f + cam.transform.right * UnityEngine.Random.Range(-0.07f, 0.07f) + cam.transform.up * UnityEngine.Random.Range(-0.07f, 0.07f)).normalized;
            RaycastHit h; bool got = CastNoMe(o, d, 45f, 0f, out h);
            Vector3 end = got ? h.point : o + d * 45f; Vector3 normal = got ? h.normal : Vector3.up;
            if (tv != null) { end = CenterOf(tv); got = false; }
            Vector3 start = HandPos(me) + d * 0.6f;
            Vector3 sd = (end - start); float dist = sd.magnitude; if (dist < 0.5f) return; sd /= dist;
            float roll = UnityEngine.Random.Range(0f, 180f);
            float life = Mathf.Max(0.08f, dist / 70f);
            AnimVfx.SlashArc(start, sd, 70f, life, UnityEngine.Random.Range(0.9f, 1.5f), roll, new Color(1f, 0.92f, 0.9f, 0.95f));
            SendAfx(4, start, sd);
            Sfx2.Slash(); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.14f);
            StartCoroutine(CoSlashHit(end, sd, normal, life, tv, got && h.collider != null && h.collider.attachedRigidbody == null));
        }

        IEnumerator CoSlashHit(Vector3 end, Vector3 d, Vector3 normal, float delay, Component victim, bool solid)
        {
            yield return new WaitForSeconds(delay);
            AnimVfx.CrossSlash(end, 0.9f);
            if (victim != null) { HitV(victim, (d + Vector3.up * 0.2f), 0.3f, 1.8f); Gore.Burst(end, d, 1.1f); }
            else if (solid)
            {
                Destruct.Smash(end, d, 1.9f, 1.3f, true, Mathf.Max(Destruct.MaxSize, 8f));
                Gore.Impact(end, normal, 0.45f); Vfx.Dust(end + normal * 0.1f, 1f, 4);
            }
            else
            {
                Vfx.Sparks(end, -d, 6, new Color(1f, 0.4f, 0.3f));
            }
        }

        // ======================= Владыка Морок: V — «Клин» =======================
        IEnumerator CoCleave(Component me)
        {
            cdB = Time.time + 3f; AnimPose(14, 0.6f); Sfx2.Slash();
            yield return new WaitForSeconds(0.12f);
            var t = LookedVictim(30f, 0.93f);
            if (t != null)
            {
                Vector3 p = CenterOf(t); Vector3 dir = p - CenterOf(me); dir.y = 0f; dir = (dir.normalized + Vector3.up * 0.25f).normalized;
                for (int i = 0; i < 3; i++)
                {
                    if (t == null) break;
                    p = CenterOf(t);
                    AnimVfx.CrossSlash(p, 1.9f); SendAfx(5, p, Vector3.zero); Sfx2.Slash();
                    Gore.Burst(p, UnityEngine.Random.onUnitSphere, 1.5f); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.5f);
                    yield return new WaitForSeconds(0.07f);
                }
                if (t != null)
                {
                    p = CenterOf(t); HitV(t, dir, 1.4f, 5f); Destruct.Smash(p, dir, 3f, 1.5f, true, Mathf.Max(Destruct.MaxSize, 8f));
                    CamShake.Boost = 1f; Sfx2.Boom(); TrackImpact(t, 1.8f); HeroToast("Клин: " + Game.Name(t));
                }
            }
            else
            {
                Vector3 n; bool got; Vector3 pt = AimPoint(40f, out n, out got);
                Vector3 dir = FlatForward(); Vector3 start = pt;
                AnimVfx.GroundCut(start, dir, 25f, new Color(1f, 0.2f, 0.18f, 0.95f)); SendAfx(10, start, dir);
                CamShake.Boost = 0.9f; HeroToast("Длинный клин");
                for (int i = 0; i <= 12; i++)
                {
                    Vector3 p = start + dir * (i * 2f); RaycastHit h; Vector3 gp = p; Vector3 gn = Vector3.up;
                    if (Physics.Raycast(p + Vector3.up * 25f, Vector3.down, out h, 60f, ~0, QueryTriggerInteraction.Ignore)) { gp = h.point; gn = h.normal; }
                    Destruct.Smash(gp, dir, 2.6f, 1.4f, true, Mathf.Max(Destruct.MaxSize, 10f));
                    Gore.Impact(gp, gn, 0.5f); Vfx.Dust(gp, 1.4f, 4);
                    foreach (var v in Victims())
                    {
                        Vector3 vp = CenterOf(v); Vector3 fp = vp - gp; fp.y = 0f;
                        if (fp.magnitude < 3.2f && Mathf.Abs(vp.y - gp.y) < 4f) { HitV(v, Vector3.up * 0.8f + Vector3.Cross(dir, Vector3.up) * (Vector3.Dot(fp, Vector3.Cross(dir, Vector3.up)) > 0 ? 1f : -1f), 1.0f, 3.5f); Gore.Burst(vp, Vector3.up, 1.2f); }
                    }
                    yield return new WaitForSeconds(0.025f);
                }
            }
        }

        // ======================= Владыка Морок: H — «Огненная стрела» (огненная стрела) =======================
        IEnumerator CoFuga(Component me)
        {
            var cam = Camera.main; if (cam == null) yield break;
            cdC = Time.time + 15f; purpleCharge = true;
            HeroToast("«Огненная стрела»: Раскройся…");
            var orb = AnimVfx.OrbGo(new Color(1f, 0.95f, 0.7f, 1f), new Color(1f, 0.45f, 0.1f, 1f), 0.4f);
            float t0 = Time.time, dur = 1.0f; Sfx2.LaserStart(); StartCine(3, 1.4f);
            while (Time.time - t0 < dur)
            {
                if (hero != HeroKind.Sukuna || me == null) { purpleCharge = false; if (orb != null) UnityEngine.Object.Destroy(orb); Sfx2.LaserStop(); yield break; }
                float k = (Time.time - t0) / dur; cam = Camera.main; if (cam == null) break;
                if (orb != null) { orb.transform.position = cam.transform.position + cam.transform.forward * 1.6f + cam.transform.right * 0.35f; AnimVfx.SetOrbScale(orb, Mathf.Lerp(0.6f, 1.8f, k) * (1f + 0.08f * Mathf.Sin(Time.time * 30f))); }
                AnimPose(15, 0.2f); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.15f + 0.5f * k);
                if (Time.frameCount % 4 == 0) Vfx.Sparks(cam.transform.position + cam.transform.forward * 1.6f, Vector3.up, 4, new Color(1f, 0.5f, 0.1f));
                yield return null;
            }
            Sfx2.LaserStop();
            if (orb != null) UnityEngine.Object.Destroy(orb);
            cam = Camera.main; if (cam == null) { purpleCharge = false; yield break; }
            Vector3 o = cam.transform.position + cam.transform.forward * 1.2f, f = cam.transform.forward;
            RaycastHit h; bool got = CastNoMe(cam.transform.position, f, 220f, 0.3f, out h);
            Vector3 end = got ? h.point : cam.transform.position + f * 220f;
            float travel = Mathf.Max(0.15f, Vector3.Distance(o, end) / 120f);
            SpawnFugaArrow(o, (end - o).normalized, travel);
            SendAfx(6, o, (end - o).normalized);
            Sfx2.Whoosh(); Sfx2.Boom(); CamShake.Boost = 1f;
            yield return new WaitForSeconds(travel);
            ExplodeFire(end, f, true);
            purpleCharge = false;
        }

        void SpawnFugaArrow(Vector3 o, Vector3 dir, float travel)
        {
            var orb = AnimVfx.OrbGo(new Color(1f, 0.97f, 0.8f, 1f), new Color(1f, 0.45f, 0.08f, 1f), 0.7f);
            if (orb == null) return;
            orb.transform.position = o; orb.transform.rotation = Quaternion.LookRotation(dir); orb.transform.localScale = new Vector3(1f, 1f, 3.4f);
            Vector3 end = o + dir * 120f * travel;
            AnimVfx.Line(new[] { o, end }, new Color(1f, 0.55f, 0.15f, 0.9f), 0.9f, travel + 0.3f);
            Vfx.Add(orb, travel, (k, g) => { g.transform.position = Vector3.Lerp(o, end, k); });
        }

        void ExplodeFire(Vector3 pos, Vector3 dir, bool real)
        {
            AnimVfx.Flash(pos, new Color(1f, 0.5f, 0.1f, 0.85f), 30f, 0.8f);
            AnimVfx.Flash(pos, new Color(1f, 0.95f, 0.75f, 0.95f), 13f, 0.4f);
            AnimVfx.Flash(pos, new Color(0.9f, 0.2f, 0.05f, 0.5f), 38f, 1.1f);
            Vfx.Ring(pos, Vector3.up, new Color(1f, 0.6f, 0.2f, 0.9f), 24f, 0.8f);
            Vfx.Ring(pos + Vector3.up, new Vector3(0.3f, 1f, 0.1f), new Color(1f, 0.85f, 0.4f, 0.8f), 18f, 0.6f);
            Vfx.Sparks(pos, Vector3.up, 80, new Color(1f, 0.55f, 0.15f)); Vfx.Dust(pos, 10f, 30);
            if (!real) { Sfx2.Boom(); return; }
            SendAfx(9, pos, dir);
            Shockwave(pos, 14f, 2.0f);
            Destruct.Smash(pos, dir, 11f, 3.5f, true, Mathf.Max(Destruct.MaxSize, 25f));
            ForEachProp(pos, 14f, rb => rb.AddExplosionForce(20f, pos, 14f, 0.5f, ForceMode.VelocityChange));
            foreach (var v in Victims())
            {
                if (Vector3.Distance(CenterOf(v), pos) > 14f) continue;
                int actor = Game.ActorOf(v);
                if (Game.InRoom && !Game.IsMine(v) && actor >= 0) Net.Send("fx", "Fire:3", actor);
                Gore.Burst(CenterOf(v), Vector3.up, 1.6f);
            }
            CamShake.Boost = 1.8f; Shade.Set(ShadeKind.Power, 1.2f);
        }

        // ======================= ВЕЛИКИЙ КУПОЛ =======================
        public void TryDomain()
        {
            if (IsHoop(hero)) { TryZone(); return; }
            if (!IsAnime(hero)) { Ui.Toast("Великий купол — только у Альбус и Морока (у баскетболистов — Горячая рука)", Ui.Muted); return; }
            var me = Game.Me(); if (me == null) return;
            if (domActive || domCasting) return;
            if (Time.time < cdDom) { HeroToast("Великий купол: перезарядка " + Mathf.CeilToInt(cdDom - Time.time) + " с"); return; }
            if (grabbed != null) ReleaseGrab(false);
            domCo = StartCoroutine(CoDomain(me));
        }

        bool DomainValid(HeroKind k, Component me) { return hero == k && me != null && !Game.IsDead(me); }

        IEnumerator CoDomain(Component me)
        {
            HeroKind k = hero; domKind = k; domCasting = true;
            bool gojo = k == HeroKind.Gojo;
            if (blueOn) BlueStop(); disOn = false;
            Vector3 face = FlatForward();
            Vector3 c = CenterOf(me);
            domC = c;
            ShowBanner("ВЕЛИКИЙ КУПОЛ", HeroDomainName(k), gojo ? new Color(0.65f, 0.85f, 1f) : new Color(1f, 0.22f, 0.2f), 3.2f);
            Sfx2.DomainStart(); CamShake.Boost = 1.3f; AnimPose(12, 1.8f);
            StartCine(gojo ? 1 : 2, 1.45f);
            Shade.Set(ShadeKind.Negative, 1.1f);
            if (Game.InRoom) Net.Send("dom", (int)k + "," + V3(c) + "," + F(domR) + "," + F(domDur) + "," + V3(face));
            float t0 = Time.time;
            while (Time.time - t0 < 1.4f)
            {
                if (!DomainValid(k, me)) { domCasting = false; domCo = null; if (Game.InRoom) Net.Send("dom", "0"); yield break; }
                flyVel = Vector3.zero; foreach (var rb in Game.Rigs(me)) Game.SetVelocity(rb, Vector3.zero);
                CamShake.Boost = Mathf.Max(CamShake.Boost, 0.45f + 0.5f * (Time.time - t0));
                AnimPose(12, 0.2f);
                yield return null;
            }
            // ---- активная фаза ----
            domCasting = false; domActive = true; domEnd = Time.time + domDur;
            StartCine(6, 1.5f);
            if (!Net.Echo) domFx = new DomainFx(k, domC, domR, domDur, face);   // в режиме теста вместо него будет «чужая» копия
            Shade.Set(ShadeKind.Negative, 0.5f); CamShake.Boost = 1.8f; Sfx2.Boom();
            Vfx.Ring(domC, Vector3.up, gojo ? new Color(0.6f, 0.85f, 1f, 0.9f) : new Color(1f, 0.2f, 0.2f, 0.9f), domR, 1.0f);
            Vfx.Ring(domC, new Vector3(0.3f, 1f, 0.2f), gojo ? new Color(0.8f, 0.6f, 1f, 0.8f) : new Color(1f, 0.6f, 0.4f, 0.8f), domR * 0.8f, 0.8f);
            Destruct.Smash(domC, Vector3.up, Mathf.Min(domR * 0.6f, 18f), 2.6f * domPow, true, Mathf.Max(Destruct.MaxSize, 25f));
            HeroToast(HeroDomainName(k) + "!");
            var trapped = new HashSet<Component>();
            foreach (var v in Victims()) if (Vector3.Distance(CenterOf(v), domC) < domR) trapped.Add(v);

            float nV = 0f, nSl = 0f, nSm = 0f, nBar = 0f, nProp = 0f, nSh = 0f; bool fallFlip = false;
            while (Time.time < domEnd)
            {
                if (!DomainValid(k, me)) break;
                float now = Time.time;
                float rate = Mathf.Clamp(1f / domPow, 0.4f, 3f);
                var vs = Victims();
                foreach (var v in vs) if (Vector3.Distance(CenterOf(v), domC) < domR) trapped.Add(v);
                if (now >= nSh) { nSh = now + 0.12f; CamShake.Boost = Mathf.Max(CamShake.Boost, gojo ? 0.18f : 0.3f * Mathf.Min(domPow, 1.5f)); }

                if (gojo)
                {
                    if (now >= nV)
                    {
                        nV = now + 0.4f * rate;
                        foreach (var v in vs)
                        {
                            Vector3 p = CenterOf(v); if (Vector3.Distance(p, domC) > domR) continue;
                            Quiet(v, "RPCA_Fall", 2.4f);
                            Quiet(v, "RPCA_AddForceToBodyPart", 0, Vector3.up * launchForce * 0.05f);
                        }
                    }
                    if (now >= nProp)
                    {
                        nProp = now + 0.5f;
                        ForEachProp(domC, domR, rb => { rb.velocity *= 0.55f; rb.AddForce(Vector3.up * 4f, ForceMode.Acceleration); });
                    }
                }
                else
                {
                    if (now >= nSl)
                    {
                        nSl = now + 0.16f * rate; fallFlip = !fallFlip;
                        foreach (var v in vs)
                        {
                            Vector3 p = CenterOf(v); if (Vector3.Distance(p, domC) > domR) continue;
                            AnimVfx.CrossSlash(p + UnityEngine.Random.insideUnitSphere * 0.5f, 1.5f);
                            Gore.Burst(p, UnityEngine.Random.onUnitSphere, 0.9f);
                            if (fallFlip) { Quiet(v, "RPCA_Fall", 2.2f); Quiet(v, "RPCA_AddForceToBodyPart", 0, UnityEngine.Random.onUnitSphere * launchForce * 0.14f); }
                        }
                        Sfx2.Slash();
                    }
                    if (now >= nSm)
                    {
                        nSm = now + 0.22f * rate;
                        float a = UnityEngine.Random.value * Mathf.PI * 2f, r = Mathf.Sqrt(UnityEngine.Random.value) * domR * 0.9f;
                        Vector3 rp = domC + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r; RaycastHit h; Vector3 gp = rp; Vector3 gn = Vector3.up;
                        if (Physics.Raycast(rp + Vector3.up * 40f, Vector3.down, out h, 100f, ~0, QueryTriggerInteraction.Ignore)) { gp = h.point; gn = h.normal; }
                        Vector3 sd = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)).normalized;
                        Destruct.Smash(gp, sd, 3.5f, 1.4f * domPow, true, Mathf.Max(Destruct.MaxSize, 10f));
                        Gore.Impact(gp, gn, 0.6f); Vfx.Dust(gp, 2f, 5);
                        if (UnityEngine.Random.value < 0.5f) AnimVfx.GroundCut(gp - sd * 5f, sd, 10f, new Color(1f, 0.2f, 0.18f, 0.9f));
                    }
                }

                if (domBarrier && now >= nBar)
                {
                    nBar = now + 0.3f;
                    foreach (var v in vs)
                    {
                        if (!trapped.Contains(v)) continue;
                        Vector3 p = CenterOf(v); Vector3 fl = p - domC; float d = fl.magnitude;
                        if (d > domR * 0.96f)
                        {
                            Vector3 inward = -fl.normalized;
                            if (d > domR * 1.12f) { Vector3 np = domC + Vector3.ClampMagnitude(fl, domR * 0.7f); Quiet(v, "WarpPlayerRPC", np, true); }
                            else { Quiet(v, "RPCA_Fall", 1.2f); Quiet(v, "RPCA_AddForceToBodyPart", 0, inward * launchForce * 0.5f); }
                        }
                    }
                }
                yield return null;
            }
            EndDomain();
            domCo = null;
        }

        void EndDomain()
        {
            if (!domActive) return;
            domActive = false; domCd = Mathf.Max(5f, domCd); cdDom = Time.time + domCd;
            if (domFx != null) { domFx.Collapse(); }
            if (Game.InRoom) Net.Send("dom", "0");
            CamShake.Boost = Mathf.Max(CamShake.Boost, 1f); Sfx2.Boom();
            AnimVfx.Flash(domC, domKind == HeroKind.Gojo ? new Color(0.7f, 0.85f, 1f, 0.7f) : new Color(1f, 0.3f, 0.2f, 0.7f), domR * 1.6f, 0.9f);
            HeroToast("Великий купол закончился");
        }

        // ---------- домены друзей (только визуал) ----------
        internal static readonly System.Collections.Generic.Dictionary<int, float> domStart = new System.Collections.Generic.Dictionary<int, float>();

        internal static bool SignOn(Component c)
        {
            var me = Game.Me(); var pl = Instance;
            if (c == me) return pl != null && pl.domCasting && pl.domKind == HeroKind.Gojo;
            float t; return domStart.TryGetValue(Game.ActorOf(c), out t) && Time.time - t < 1.5f;
        }

        void OnDomainNet(string arg, int sender)
        {
            if (!costumeSync) return;
            DomainFx old;
            if (arg == "0" || string.IsNullOrEmpty(arg))
            {
                if (remoteDoms.TryGetValue(sender, out old)) { old.Collapse(); }
                domActors.Remove(sender); return;
            }
            var p = arg.Split(',');
            if (p.Length < 7) return;
            int hk; float x, y, z, r, d;
            if (!int.TryParse(p[0], out hk) || hk < 0 || hk > 15) return;
            if (!float.TryParse(p[1], NumberStyles.Float, inv2, out x) || !float.TryParse(p[2], NumberStyles.Float, inv2, out y) || !float.TryParse(p[3], NumberStyles.Float, inv2, out z)
                || !float.TryParse(p[4], NumberStyles.Float, inv2, out r) || !float.TryParse(p[5], NumberStyles.Float, inv2, out d)) return;
            Vector3 face = Vector3.forward;
            if (p.Length >= 9) { float fx, fy, fz; if (float.TryParse(p[6], NumberStyles.Float, inv2, out fx) && float.TryParse(p[7], NumberStyles.Float, inv2, out fy) && float.TryParse(p[8], NumberStyles.Float, inv2, out fz)) face = new Vector3(fx, fy, fz); }
            r = Mathf.Clamp(r, 8f, 60f); d = Mathf.Clamp(d, 3f, 90f);
            HeroKind k = (HeroKind)hk; if (!IsAnime(k)) return;
            if (remoteDoms.TryGetValue(sender, out old)) old.Destroy();
            domActors.Add(sender); if (k == HeroKind.Gojo) domStart[sender] = Time.time;
            bool echo = sender == Game.LocalActor();
            ShowBanner("ВЕЛИКИЙ КУПОЛ", (echo ? "" : Game.PlayerName(sender) + ": ") + HeroDomainName(k), k == HeroKind.Gojo ? new Color(0.65f, 0.85f, 1f) : new Color(1f, 0.22f, 0.2f), 3.2f);
            if (!echo) { Sfx2.DomainStart(); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.9f); }
            StartCoroutine(CoRemoteDome(sender, k, new Vector3(x, y, z), r, d, face));
        }

        IEnumerator CoRemoteDome(int sender, HeroKind k, Vector3 c, float r, float d, Vector3 face)
        {
            yield return new WaitForSeconds(1.4f);
            if (!domActors.Contains(sender)) yield break;
            var fx = new DomainFx(k, c, r, d, face); fx.owner = sender;
            remoteDoms[sender] = fx;
        }

        void TickAnime()
        {
            var me = Game.Me();
            if (domFx != null) { try { domFx.Tick(); } catch (Exception e) { SafeLog("Domain", e); domFx.Destroy(); } if (domFx != null && domFx.Done) domFx = null; }
            List<int> rm = null;
            foreach (var kv in remoteDoms)
            {
                try { kv.Value.Tick(); } catch (Exception e) { SafeLog("RDomain", e); kv.Value.Destroy(); }
                if (kv.Value.Done) { if (rm == null) rm = new List<int>(); rm.Add(kv.Key); }
            }
            if (rm != null) foreach (var k in rm) { var f = remoteDoms[k]; if (f.shaded) { f.shaded = false; Shade.Off(); } remoteDoms.Remove(k); }
            // искажение экрана, если ты оказался внутри чужого домена
            if (me != null)
                foreach (var kv in remoteDoms)
                {
                    var f = kv.Value; if (f.Done || f.Collapsing) { if (f.shaded) { f.shaded = false; Shade.Off(); } continue; }
                    bool inside = Vector3.Distance(CenterOf(me), f.c) < f.R && (f.owner != Game.LocalActor() || echoShade);
                    if (inside && !f.shaded) { f.shaded = true; Shade.Set(f.kind == HeroKind.Gojo ? ShadeKind.Glitch : ShadeKind.Horror, Mathf.Max(3f, f.dur)); }
                    else if (!inside && f.shaded) { f.shaded = false; Shade.Off(); }
                }
            // чужие шары «Синего»
            List<int> orm = null;
            foreach (var kv in remoteOrbT) if (Time.time - kv.Value > 0.45f) { if (orm == null) orm = new List<int>(); orm.Add(kv.Key); }
            if (orm != null) foreach (var k in orm) { GameObject g; if (remoteOrbs.TryGetValue(k, out g) && g != null) UnityEngine.Object.Destroy(g); remoteOrbs.Remove(k); remoteOrbT.Remove(k); }
            try { TickInfinity(me); } catch (Exception e) { SafeLog("Infinity", e); }
        }

        // ---------- визуал техник друзей ----------
        void OnAfx(string arg, int sender)
        {
            if (!costumeSync) return;
            var p = arg.Split(',');
            if (p.Length < 7) return;
            int kind; float[] f = new float[6];
            if (!int.TryParse(p[0], out kind)) return;
            for (int i = 0; i < 6; i++) if (!float.TryParse(p[i + 1], NumberStyles.Float, inv2, out f[i])) return;
            Vector3 pos = new Vector3(f[0], f[1], f[2]), dir = new Vector3(f[3], f[4], f[5]);
            bool echo = sender == Game.LocalActor();
            switch (kind)
            {
                case 0:
                    { GameObject g; if (remoteOrbs.TryGetValue(sender, out g) && g != null) UnityEngine.Object.Destroy(g); remoteOrbs.Remove(sender); remoteOrbT.Remove(sender); break; }
                case 1:
                    {
                        GameObject g; if (!remoteOrbs.TryGetValue(sender, out g) || g == null) { g = AnimVfx.OrbGo(new Color(0.55f, 0.85f, 1f, 1f), new Color(0.2f, 0.5f, 1f, 1f), 0.9f); remoteOrbs[sender] = g; }
                        if (g != null) { g.transform.position = pos; AnimVfx.SetOrbScale(g, 1f + 0.12f * Mathf.Sin(Time.time * 22f)); }
                        remoteOrbT[sender] = Time.time; break;
                    }
                case 2: StartCoroutine(CoRemoteRed(pos, dir)); break;
                case 3: if (!echo) FirePurple(pos, dir); break;
                case 4: AnimVfx.SlashArc(pos, dir, 70f, 0.5f, 1.2f, UnityEngine.Random.Range(0f, 180f), new Color(1f, 0.92f, 0.9f, 0.95f)); if (!echo) Sfx2.Slash(); break;
                case 5: AnimVfx.CrossSlash(pos, 1.9f); if (!echo) Sfx2.Slash(); break;
                case 6: SpawnFugaArrow(pos, dir.normalized, 1.2f); if (!echo) Sfx2.Whoosh(); break;
                case 7: if (!echo) ExplodeRed(pos, dir, false); break;
                case 9: if (!echo) ExplodeFire(pos, dir, false); break;
                case 10: AnimVfx.GroundCut(pos, dir, 25f, new Color(1f, 0.2f, 0.18f, 0.95f)); break;
            }
        }

        IEnumerator CoRemoteRed(Vector3 start, Vector3 dir)
        {
            var orb = AnimVfx.OrbGo(new Color(1f, 0.55f, 0.5f, 1f), new Color(1f, 0.1f, 0.1f, 1f), 0.8f);
            if (orb == null) yield break;
            Vector3 pos = start; float life = 0f; dir = dir.normalized;
            while (life < 1.4f) { pos += dir * 45f * Time.deltaTime; life += Time.deltaTime; orb.transform.position = pos; yield return null; }
            UnityEngine.Object.Destroy(orb);
        }
    }
}
