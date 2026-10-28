// Баскетбол 1.13 («Баскетбол Крис Пол»): кольцо, мяч с настоящей физикой, ведение рукой, бросок по таймингу, трёхочковый, данк,
// пас, перехват, счёт, синхронизация с друзьями (канал "bb"). Герои и «Горячая рука» (Зона) — в HoopHeroes.cs.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakFun
{
    internal sealed class HoopObj
    {
        public GameObject root; public Vector3 baseP, rimC, fwd; public float rimH, scale, rimR, shake, tilt; public int id;
        public readonly List<Collider> cols = new List<Collider>(); public LineRenderer[] net; public LineRenderer arc;
        public Vector3 rimLocal; public float arcR;
    }

    internal sealed class BallProbe : MonoBehaviour
    {
        public static Action<Collision> Hit;
        void OnCollisionEnter(Collision c) { if (Hit != null) Hit(c); }
    }

    public partial class PeakFunPlugin
    {
        // ---------- состояние ----------
        readonly List<HoopObj> hoops = new List<HoopObj>();
        GameObject ballGo; Rigidbody ballRb; SphereCollider ballCol; TrailRenderer ballTrail; Renderer ballRend;
        float hoopScale = 1f, rimHeight = 3.05f; bool aimAssist = true;
        bool ballHeld, ballRemoteHeld, dunkBusy, shootCharging; int ballOwner = -1;
        float shootT0, dribPhase = 0.5f, dribSide = 1f, dribSideCur = 1f, crossUntil, followUntil, ballHideUntil, nextBallSend, lastRecv, lastGoalT, lastBounceSfx;
        Vector3 ballTarget, ballTargetVel, ballPrev; float shotUntil; int shotPts = 2; bool shotLive, shotDunk, rimTouched; Vector3 shotFootFrom;
        int ballArm; float perfectUntil, magnetUntil;
        readonly Dictionary<int, int> scores = new Dictionary<int, int>();
        string bigText = ""; float bigUntil; Color bigCol = Color.white;
        ConfigEntry<KeyCode> keyBall;
        static object ballMat; static Texture2D ballTexC;
        const float ThreeDist = 6.75f;
        int hoopSeq;

        void InitHoops()
        {
            keyBall = Config.Bind("Keys", "Ball", KeyCode.R, "Баскетбол: взять мяч / бросить мяч из рук / перехват");
            BallProbe.Hit = BallCollide; InitMoves();
        }

        // ======================= физика-материал =======================
        static void SetBounce(Collider c, float bounce)
        {
            if (c == null) return;
            try
            {
                if (ballMat == null)
                {
                    var t = R.FindType("UnityEngine.PhysicsMaterial") ?? R.FindType("UnityEngine.PhysicMaterial");
                    if (t == null) return;
                    ballMat = Activator.CreateInstance(t);
                    var p = t.GetProperty("bounciness"); if (p != null) p.SetValue(ballMat, 0.82f, null);
                    p = t.GetProperty("dynamicFriction"); if (p != null) p.SetValue(ballMat, 0.55f, null);
                    p = t.GetProperty("staticFriction"); if (p != null) p.SetValue(ballMat, 0.55f, null);
                    p = t.GetProperty("bounceCombine"); if (p != null) p.SetValue(ballMat, Enum.ToObject(p.PropertyType, 3), null);
                    p = t.GetProperty("frictionCombine"); if (p != null) p.SetValue(ballMat, Enum.ToObject(p.PropertyType, 1), null);
                }
                var sp = typeof(Collider).GetProperty("sharedMaterial"); if (sp != null) sp.SetValue(c, ballMat, null);
            }
            catch { }
        }

        static Texture2D BallTex()
        {
            if (ballTexC != null) return ballTexC;
            ballTexC = Vfx.Tex(512, 256, (u, v) =>
            {
                float sv = Mathf.Max(0.18f, Mathf.Sin(v * Mathf.PI)), lw = 0.0075f;
                float eq = Mathf.Abs(v - 0.5f);
                float m1 = Mathf.Min(Mathf.Abs(u - 0.5f), Mathf.Min(u, 1f - u)) * sv;
                float cu = Mathf.Min(Mathf.Abs(u - (0.25f + 0.07f * Mathf.Sin(v * Mathf.PI * 2f))), Mathf.Abs(u - (0.75f - 0.07f * Mathf.Sin(v * Mathf.PI * 2f)))) * sv;
                bool line = eq < lw || m1 < lw * 0.6f || cu < lw * 0.6f;
                int ix = (int)(u * 256f), iy = (int)(v * 128f); float n = ((ix * 73856093) ^ (iy * 19349663)) & 255; n = n / 255f - 0.5f;
                float shade = (0.8f + 0.2f * Mathf.Sin(v * Mathf.PI)) + n * 0.12f;
                return line ? new Color(0.06f, 0.035f, 0.02f, 1f) : new Color(0.93f * shade, 0.42f * shade, 0.1f * shade, 1f);
            });
            ballTexC.wrapMode = TextureWrapMode.Repeat;
            return ballTexC;
        }

        // ======================= мяч =======================
        void EnsureBall(Vector3 pos)
        {
            if (ballGo == null)
            {
                ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere); ballGo.name = "PF_ball_player";
                ballCol = ballGo.GetComponent<SphereCollider>();
                ballRend = ballGo.GetComponent<MeshRenderer>();
                var m = Vfx.Mat(BallTex(), Color.white); if (m != null) ballRend.sharedMaterial = m;
                ballRb = ballGo.AddComponent<Rigidbody>(); ballRb.mass = 0.6f; ballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                ballRb.interpolation = RigidbodyInterpolation.Interpolate; ballRb.maxAngularVelocity = 40f; ballRb.drag = 0.03f; ballRb.angularDrag = 0.5f;
                SetBounce(ballCol, 0.82f);
                ballGo.AddComponent<BallProbe>();
                ballTrail = ballGo.AddComponent<TrailRenderer>(); ballTrail.time = 0.28f; ballTrail.startWidth = 0.12f; ballTrail.endWidth = 0f; ballTrail.emitting = false;
                var tm = Vfx.Mat(null, Color.white); if (tm != null) ballTrail.material = tm;
                ballTrail.startColor = new Color(1f, 0.7f, 0.3f, 0.8f); ballTrail.endColor = new Color(1f, 0.5f, 0.1f, 0f);
            }
            ballGo.SetActive(true);
            ballGo.transform.localScale = Vector3.one * 0.3f * hoopScale;
            ballGo.transform.position = pos; ballRb.isKinematic = false; ballCol.enabled = true;
            Game.SetVelocity(ballRb, Vector3.zero); ballRb.angularVelocity = Vector3.zero;
            ballTrail.Clear(); ballPrev = pos;
        }

        void RemoveBall() { aiHasBall = false; aiShotLive = false; if (ballGo != null) UnityEngine.Object.Destroy(ballGo); ballGo = null; ballHeld = false; ballRemoteHeld = false; ballOwner = -1; shootCharging = false; ballArm = 0; }

        float GroundY(Vector3 p, float fallback)
        {
            if (charType2 == null) charType2 = R.FindType("Character");
            var hits = Physics.RaycastAll(p + Vector3.up * 0.3f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool got = false;
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (ballGo != null && h.collider.gameObject == ballGo) continue;
                if (aiGo != null && h.collider.gameObject == aiGo) continue;
                if (charType2 != null && h.collider.GetComponentInParent(charType2) != null) continue;
                if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue;
                if (h.distance < best) { best = h.distance; got = true; }
            }
            return got ? p.y + 0.3f - best : fallback;
        }

        float ShotG() { return Mathf.Max(3f, -Physics.gravity.y); }

        static Vector3 SolveVel(Vector3 s, Vector3 t, float rise, float g, out float T)
        {
            float top = Mathf.Max(s.y, t.y) + Mathf.Max(0.5f, rise);
            float vy = Mathf.Sqrt(2f * g * (top - s.y));
            float tUp = vy / g, tDown = Mathf.Sqrt(2f * Mathf.Max(0.01f, top - t.y) / g); T = tUp + tDown;
            Vector3 flat = t - s; flat.y = 0f; Vector3 v = flat / T; v.y = vy; return v;
        }

        // ---------- взять / бросить ----------
        void TakeBall(Component me)
        {
            if (ballGo == null) EnsureBall(CenterOf(me) + FlatForward());
            pickFrom = ballGo.transform.position; pickT0 = Time.time; holdBlend = 0f; idleT = 0f; dribMove = 0; aiHasBall = false; aiShotLive = false;
            ballHeld = true; ballOwner = Game.LocalActor(); ballRemoteHeld = false; shotLive = false; dunkBusy = false;
            ballRb.isKinematic = true; ballCol.enabled = false; ballTrail.emitting = false; ballTrail.Clear();
            dribPhase = 0.5f; ballRend.enabled = true;
            SendBall(true);
            Sfx2.Bounce(0.5f);
        }

        void DropBall(Component me, float spd)
        {
            if (!ballHeld) return;
            Vector3 v = FlatForward() * spd + Vector3.up * 1.5f;
            ReleaseBall(ballGo.transform.position, v, 0, false);
        }

        void ReleaseBall(Vector3 pos, Vector3 vel, int pts, bool dunk)
        {
            ballHeld = false; shootCharging = false;
            ballGo.transform.position = pos; ballCol.enabled = true; ballRb.isKinematic = false;
            Game.SetVelocity(ballRb, vel); ballRb.angularVelocity = UnityEngine.Random.onUnitSphere * 6f;
            shotLive = pts > 0; shotPts = pts; shotUntil = Time.time + 7f; shotDunk = dunk; rimTouched = false;
            var me = Game.Me(); if (me != null) { var cm = me.transform.position; shotFootFrom = CenterOf(me); StartCoroutine(CoIgnoreBody(0.35f)); }
            ballOwner = Game.LocalActor(); followUntil = Time.time + 0.45f;
            SendBall(false);
        }

        IEnumerator CoIgnoreBody(float t)
        {
            var me = Game.Me(); if (me == null || ballCol == null) yield break;
            var cs = me.GetComponentsInChildren<Collider>(); var bc = ballCol;
            foreach (var c in cs) if (c != null && bc != null) Physics.IgnoreCollision(bc, c, true);
            yield return new WaitForSeconds(t);
            foreach (var c in cs) if (c != null && bc != null) Physics.IgnoreCollision(bc, c, false);
        }

        // ---------- ведение ----------
        void DribbleTick(Component me)
        {
            var pm = PartMap.Of(me);
            Vector3 up = Vector3.up, fw = FlatForward(), side = Vector3.Cross(up, fw).normalized;
            float dt = Time.deltaTime, sc = Mathf.Max(0.3f, me.transform.lossyScale.y), now = Time.time;
            Vector3 vel = pm != null && pm.hip != null ? pm.hip.velocity : Vector3.zero; vel.y = 0f;
            float spd = vel.magnitude, mul = zoneOn ? 0.78f : 1f, r = 0.15f * hoopScale;
            Vector3 c0 = CenterOf(me); float lat = Vector3.Dot(vel, side);
            bool inMove = dribMove != 0 && now < dribMoveEnd;
            if (dribMove != 0 && !inMove) { dribMove = 0; dribPhase = 0.5f; }
            if (!inMove && now >= crossUntil && Mathf.Abs(lat) > 3f && now - lastSideSwitch > 0.8f) { float ns = lat > 0f ? 1f : -1f; if (ns != dribSide) { dribSide = ns; lastSideSwitch = now; } }
            dribSideCur = Mathf.MoveTowards(dribSideCur, dribSide, dt * 6.5f);
            Vector3 pos;
            if (inMove)
            {
                float u = (now - dribMoveT0) / dribMoveDur, latS;
                pos = MoveBallPos(me, dribMove, u, dribMoveSide, c0, fw, side, sc, out latS);
                if (dribMove == 4 && !dribMoveHit && u > 0.5f) { dribMoveHit = true; Dash(fw, 17f, 0.3f); Sfx2.Whoosh(); }
                if (dribMove == 1 && !dribMoveHit && u > 0.5f) { dribMoveHit = true; Sfx2.Bounce(0.7f); }
                if (Mathf.Abs(latS) > 0.03f) dribArmR = latS > 0f;
                float bl = Mathf.Clamp01((now - dribMoveT0) / 0.1f); if (bl < 1f) pos = Vector3.Lerp(ballGo.transform.position, pos, bl);
                holdBlend = 0f; idleT = 0f;
            }
            else
            {
                bool idle = spd < 0.7f && !shootCharging && now >= crossUntil;
                idleT = idle ? idleT + dt : 0f;
                float ht = idleT > 0.9f ? 1f : 0f; holdBlend = Mathf.MoveTowards(holdBlend, ht, dt * (ht > holdBlend ? 3f : 6f));
                float period = Mathf.Lerp(0.52f, 0.34f, Mathf.Clamp01(spd / 8f)) * mul;
                dribPhase += dt / period;
                float push = Mathf.Clamp(spd * 0.045f, 0f, 0.3f);
                Vector3 hc = c0 + side * 0.36f * dribSideCur * sc + fw * (0.34f + push) * sc + vel * 0.05f;
                float gy = GroundY(hc, me.transform.position.y);
                float top = c0.y - 0.05f * sc;
                float A = top - gy - r; bool air = A > 1.5f * sc;
                A *= Mathf.Lerp(1f, 0.62f, Mathf.Clamp01(spd / 8f));
                if (dribPhase >= 1f) { dribPhase -= 1f; if (!air && holdBlend < 0.5f) { Sfx2.Bounce(0.35f + 0.04f * spd); Vfx.Dust(new Vector3(hc.x, gy, hc.z), 0.15f, 2); } }
                float y;
                if (air) { y = top - 0.02f; dribPhase = 0.5f; }
                else
                {
                    if (now < crossUntil) A *= 0.4f;
                    float hh = 4f * dribPhase * (1f - dribPhase); y = gy + r + Mathf.Max(0.1f, A) * hh;
                }
                pos = new Vector3(hc.x, y, hc.z); dribArmR = dribSideCur >= 0f;
                if (holdBlend > 0.01f)
                {
                    Vector3 chest = c0 + fw * 0.4f * sc + up * (-0.14f * sc + 0.012f * Mathf.Sin(now * 2.2f));
                    float hb = holdBlend * holdBlend * (3f - 2f * holdBlend); pos = Vector3.Lerp(pos, chest, hb);
                }
            }
            float pk = (now - pickT0) / 0.3f;
            if (pk < 1f) pos = Vector3.Lerp(pickFrom, pos, pk * pk * (3f - 2f * pk));
            ballGo.transform.position = pos;
            ballGo.transform.Rotate(spd * 40f * dt, 0f, 0f, Space.World);
        }

        void Crossover(Component me)
        {
            dribSide = -dribSide; crossUntil = Time.time + 0.3f; Sfx2.Whoosh();
            Vector3 side = Vector3.Cross(Vector3.up, FlatForward()).normalized;
            Dash(side * dribSide * 1f + FlatForward() * 0.3f, 13f, 0.16f);
            HeroToast("Кроссовер!");
        }

        // ---------- бросок ----------
        HoopObj AimHoop(Component me, float max)
        {
            if (testAim != null && testAim.root != null) return testAim;
            var cam = Camera.main; if (cam == null) return null;
            HoopObj best = null; float bd = 0.45f; Vector3 o = cam.transform.position, f = cam.transform.forward;
            foreach (var h in hoops)
            {
                if (h.root == null) continue; Vector3 to = h.rimC - o; float d = to.magnitude; if (d > max) continue;
                float dot = Vector3.Dot(f, to / d); if (dot > bd) { bd = dot; best = h; }
            }
            return best;
        }

        HoopObj NearestHoop(Vector3 p, float max)
        {
            HoopObj best = null; float bd = max;
            foreach (var h in hoops) { if (h.root == null) continue; float d = Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(h.rimC.x, 0, h.rimC.z)); if (d < bd) { bd = d; best = h; } }
            return best;
        }

        Vector3 ShotOrigin(Component me)
        {
            float sc = Mathf.Max(0.3f, me.transform.lossyScale.y);
            return HeadOf(me) + Vector3.up * 0.3f * sc + FlatForward() * 0.2f * sc;
        }

        float ShotErr(float charge)
        {
            if (zoneOn || Time.time < perfectUntil) return 0.02f;
            float dc = Mathf.Max(0f, Mathf.Abs(charge - 0.82f) - 0.1f);
            return Mathf.Lerp(0.03f, 0.95f, Mathf.Clamp01(dc / 0.5f));
        }

        void ReleaseShotCharge(Component me, float charge)
        {
            if (!ballHeld) return;
            float e = ShotErr(charge);
            bool air = !GroundBelow(me);
            if (air) e *= 0.6f;
            if (aimAssist) e *= 0.65f;
            float cn = AiContest(me); if (cn > 0f) { e = e * (1f + cn) + 0.12f * cn; if (cn > 0.5f) HeroToast("Защитник мешает!"); }
            Shoot(me, e, 0f, false, 0);
            if (e < 0.08f) { HeroToast("Идеально!"); }
        }

        /// <summary>Бросок: err — радиус промаха в метрах, rise — добавка к высоте дуги (0 = авто), forcePts 0 = авто.</summary>
        float Shoot(Component me, float err, float rise, bool silent, int forcePts)
        {
            if (!ballHeld) return 0f;
            Vector3 s = ShotOrigin(me), v; float T = 1f; int pts = 2; Vector3 guideTgt = Vector3.zero; bool guide = false;
            var h = AimHoop(me, 70f);
            if (h != null)
            {
                Vector3 flat = h.rimC - s; flat.y = 0f; float d = flat.magnitude; Vector3 fd = flat.normalized, right = Vector3.Cross(Vector3.up, fd);
                float e = err * h.scale * Mathf.Clamp(d / 6f, 0.6f, 2.5f);
                Vector2 off = UnityEngine.Random.insideUnitCircle * e;
                Vector3 tgt = h.rimC + right * off.x + fd * off.y;
                float rs = rise > 0f ? rise : Mathf.Clamp(1.1f + 0.13f * d, 1.3f, 9f);
                v = SolveVel(s, tgt, rs, ShotG(), out T); guideTgt = tgt; guide = true;
                Logger.LogInfo("[PeakFun] бросок: дистанция " + d.ToString("0.0") + " м, ошибка " + e.ToString("0.00") + " м, время " + T.ToString("0.00") + " с, g=" + ShotG().ToString("0.0"));
                float dist = Vector2.Distance(new Vector2(CenterOf(me).x, CenterOf(me).z), new Vector2(h.rimC.x, h.rimC.z));
                pts = dist > ThreeDist * h.scale ? 3 : 2;
            }
            else
            {
                v = CamForward() * 11f + Vector3.up * 4f; T = 1.2f;
            }
            if (forcePts > 0) pts = forcePts;
            ReleaseBall(s, v, pts, false);
            ballTrail.emitting = true; Sfx2.Whoosh(); AnimPose(18, 0.4f);
            if (guide) StartCoroutine(CoGuide(guideTgt, T, h));
            return T;
        }

        int shotSeq; bool guideOn;

        /// <summary>Подруливание: мяч каждый физ-кадр корректируется так, чтобы прийти ровно в точку прицеливания (с учётом ошибки тайминга).</summary>
        IEnumerator CoGuide(Vector3 tgt, float T, HoopObj h)
        {
            int id = ++shotSeq; guideOn = true; float t0 = Time.time, minD = 99f; float g = ShotG();
            while (ballGo != null && !ballHeld && guideOn && id == shotSeq && Time.time - t0 < T + 0.3f)
            {
                yield return new WaitForFixedUpdate();
                if (ballGo == null || ballRb == null || ballRb.isKinematic) break;
                float rem = T - (Time.time - t0); Vector3 p = ballGo.transform.position;
                minD = Mathf.Min(minD, Vector3.Distance(p, h.rimC));
                if (rem > 0.1f)
                {
                    Vector3 need = (tgt - p) / rem + Vector3.up * (0.5f * g * rem);
                    Game.SetVelocity(ballRb, Vector3.Lerp(ballRb.velocity, need, 0.55f));
                }
            }
            if (id == shotSeq) Logger.LogInfo("[PeakFun] бросок: ближайшее расстояние до центра кольца " + minD.ToString("0.00") + " м, " + (guideOn ? "без столкновений" : "было столкновение"));
        }

        void Pass(Component me, bool fast)
        {
            if (!ballHeld) return;
            Vector3 s = ShotOrigin(me) - Vector3.up * 0.25f; Vector3 v; var cam = Camera.main;
            Component best = null; float bd = fast ? 0.93f : 0.88f;
            if (cam != null)
                foreach (var c in Victims())
                {
                    Vector3 to = CenterOf(c) - cam.transform.position; float d = to.magnitude; if (d > 70f || d < 1f) continue;
                    float dot = Vector3.Dot(cam.transform.forward, to / d); if (dot > bd) { bd = dot; best = c; }
                }
            if (best != null)
            {
                Vector3 tgt = CenterOf(best) + Vector3.up * 0.15f; float d = Vector3.Distance(s, tgt);
                float T = Mathf.Clamp(d / (fast ? 55f : 20f), 0.12f, 1.8f); float g = ShotG();
                v = (tgt - s) / T; if (!fast) v.y += 0.5f * g * T;
            }
            else v = CamForward() * (fast ? 55f : 18f) + (fast ? Vector3.zero : Vector3.up * 2.5f);
            ReleaseBall(s, v, 0, false);
            ballTrail.emitting = true; Sfx2.Whoosh();
            if (fast) StartCoroutine(CoStraight(v.normalized, v.magnitude));
            HeroToast("Пас!");
        }

        IEnumerator CoStraight(Vector3 dir, float speed)
        {
            if (ballRb == null) yield break;
            ballRb.useGravity = false; float end = Time.time + 1.2f; var me = Game.Me();
            var hit = new HashSet<Component>();
            while (Time.time < end && ballGo != null && !ballHeld)
            {
                Vector3 p = ballGo.transform.position;
                foreach (var v in Victims())
                {
                    if (hit.Contains(v) || Vector3.Distance(CenterOf(v), p) > 1.3f) continue;
                    hit.Add(v); Rpc(v, "RPCA_Fall", 2.2f); Rpc(v, "RPCA_AddForceToBodyPart", 0, dir * launchForce * 0.35f);
                    Vfx.Sparks(p, dir, 20); Sfx2.Hit(0.7f); CamShake.Boost = Mathf.Max(CamShake.Boost, 0.4f);
                    Game.SetVelocity(ballRb, dir * 4f); end = 0f;
                }
                yield return null;
            }
            if (ballRb != null) ballRb.useGravity = true;
        }

        // ======================= данк =======================
        HoopObj DunkHoop(Component me, float range)
        {
            var h = NearestHoop(CenterOf(me), range); if (h == null) return null;
            Vector3 to = h.rimC - CenterOf(me); to.y = 0f;
            return Vector3.Dot(FlatForward(), to.normalized) > 0.2f ? h : null;
        }

        int dunkStyle = 11, curDunk; float dunkPhase;
        static readonly string[] DunkNames = { "ДАНК!", "ДАНК ДВУМЯ РУКАМИ!", "РАЗВОРОТ 360°!", "САЛЬТО-ДАНК!", "МЕЛЬНИЦА!", "ПОВИС НА КОЛЬЦЕ!", "ТОМАГАВК!", "БЭКФЛИП!", "ОБРАТНЫЙ ДАНК!", "УРАГАН 720°!", "ПОЛЁТ!" };

        IEnumerator CoDunk(HoopObj h, bool meteor, Component me)
        {
            if (dunkBusy || h == null || h.root == null) yield break;
            dunkBusy = true; shootCharging = false; guideOn = false; shotSeq++;
            int ds = meteor ? new[] { 0, 2, 3, 9, 6 }[UnityEngine.Random.Range(0, 5)] : (dunkStyle >= 11 ? UnityEngine.Random.Range(0, 11) : dunkStyle); curDunk = ds;
            float spinDeg = ds == 2 ? 360f : ds == 9 ? 720f : ds == 3 ? 360f : ds == 7 ? -360f : ds == 8 ? 180f : 0f; bool spin = spinDeg != 0f;
            var myCols = me.GetComponentsInChildren<Collider>();
            foreach (var hc in h.cols) foreach (var mc in myCols) if (hc != null && mc != null) Physics.IgnoreCollision(hc, mc, true);
            Vector3 start = CenterOf(me); Vector3 flat = h.rimC - start; flat.y = 0f; float dist = flat.magnitude; Vector3 fd = dist > 0.1f ? flat / dist : FlatForward();
            float sc = Mathf.Max(0.3f, me.transform.lossyScale.y);
            Vector3 tgt = h.rimC - fd * 0.55f * sc + Vector3.up * (-0.3f * sc);
            float T = Mathf.Clamp(0.55f + dist * 0.05f, 0.6f, 1.2f) + (meteor ? 0.3f : 0f) + (spin || ds == 4 || ds == 6 ? 0.2f : 0f) + (ds == 9 ? 0.2f : 0f);
            float arcH = Mathf.Max(0.5f, (tgt.y - start.y) * 0.3f) + (meteor ? 3.2f : (ds == 3 || ds == 7 ? 1.7f : ds == 9 ? 1.3f : ds == 10 ? 1.2f : 0.9f));
            StartCine(ds == 2 || ds == 5 || ds == 9 ? 2 : 4, T + 0.9f); AnimPose(ds == 4 ? 21 : (ds == 1 || ds == 7) ? 22 : ds == 6 ? 23 : ds == 10 ? 24 : 20, T + 1.4f);
            Vfx.Dust(new Vector3(start.x, GroundY(start, start.y - 1f), start.z), 0.8f, 10);
            Sfx2.Whoosh(); Shade.Set(ShadeKind.Speed, 0.5f); if (meteor) { CamShake.Boost = 1f; Sfx2.Boom(); }
            Vector3 axis = (ds == 2 || ds == 9 || ds == 8) ? Vector3.up : Vector3.Cross(Vector3.up, fd);
            float t = 0f, prevKK = 0f;
            while (t < T)
            {
                yield return new WaitForFixedUpdate();
                if (me == null) break;
                float dt = Time.fixedDeltaTime; t += dt; float k = Mathf.Clamp01(t / T), e = Mathf.SmoothStep(0f, 1f, k); dunkPhase = k;
                if (spin)
                {
                    float kk = Mathf.InverseLerp(0.1f, 0.82f, k), step = (kk - prevKK) * spinDeg; prevKK = kk;
                    if (Mathf.Abs(step) > 0.01f)
                    {
                        var q = Quaternion.AngleAxis(step, axis); Vector3 c = CenterOf(me);
                        foreach (var rb in Game.Rigs(me)) { rb.position = c + q * (rb.position - c); rb.rotation = q * rb.rotation; }
                    }
                }
                Vector3 want = Vector3.Lerp(start, tgt, e) + Vector3.up * (arcH * Mathf.Sin(Mathf.PI * k));
                Vector3 v = (want - CenterOf(me)) / dt; if (v.magnitude > 70f) v = v.normalized * 70f;
                flyVel = v; foreach (var rb in Game.Rigs(me)) Game.SetVelocity(rb, v);
                if (ballGo != null && ballHeld)
                {
                    Vector3 hp;
                    var pm = PartMap.Of(me);
                    if ((ds == 1 || ds == 7 || ds == 6) && pm != null && pm.handL != null && pm.handR != null) hp = (pm.handL.position + pm.handR.position) * 0.5f + Vector3.up * 0.1f * sc;
                    else hp = HandPos(me) + Vector3.up * 0.12f * sc;
                    if (k > 0.85f) hp = Vector3.Lerp(hp, h.rimC + Vector3.up * 0.28f * sc, Mathf.InverseLerp(0.85f, 1f, k));
                    ballGo.transform.position = hp;
                }
                if ((meteor || ds >= 2) && Time.time % 0.08f < dt) Vfx.Sparks(CenterOf(me), -v.normalized, 3, meteor ? new Color(1f, 0.4f, 0.1f) : new Color(1f, 0.9f, 0.5f));
            }
            bool released = false;
            if (ballGo != null && ballHeld)
            {
                Vector3 bp = new Vector3(h.rimC.x, h.rimC.y + 0.26f * sc, h.rimC.z);
                foreach (var hc in h.cols) if (hc != null && ballCol != null) Physics.IgnoreCollision(ballCol, hc, true);
                ReleaseBall(bp, Vector3.down * 8f, 2, true); released = true;
                StartCoroutine(CoRestoreRim(h));
            }
            h.shake = meteor ? 3f : (ds == 1 ? 2.2f : 1.5f); Sfx2.Board(); CamShake.Boost = Mathf.Max(CamShake.Boost, meteor ? 1.4f : 0.9f);
            Vfx.Ring(h.rimC, Vector3.up, new Color(1f, 0.8f, 0.4f, 0.9f), meteor ? 8f : 3f, 0.5f);
            Vfx.Sparks(h.rimC, Vector3.down, meteor ? 40 : 16);
            StartCoroutine(CoHitStop(0.1f));
            if (meteor) { Shockwave(h.rimC - Vector3.up * 1.2f, 10f, 1.2f); BigText("ДАНК С ВЫСОТЫ!", new Color(1f, 0.4f, 0.15f)); }
            else BigText(DunkNames[ds], spin ? new Color(0.5f, 0.9f, 1f) : new Color(1f, 0.85f, 0.2f)); if (ds == 9 || ds == 6) Vfx.Ring(h.rimC, Vector3.up, new Color(1f, 0.5f, 0.2f, 0.9f), 6f, 0.6f);
            if (ds == 5)
            {
                float hold = 0.9f, t2 = 0f; AnimPose(20, hold + 0.2f);
                while (t2 < hold)
                {
                    yield return new WaitForFixedUpdate(); if (me == null) break;
                    float dt = Time.fixedDeltaTime; t2 += dt; h.shake = Mathf.Max(h.shake, 1.6f);
                    Vector3 v = (tgt + Vector3.down * 0.08f * Mathf.Sin(t2 * 12f) - CenterOf(me)) / dt; if (v.magnitude > 40f) v = v.normalized * 40f;
                    flyVel = v; foreach (var rb in Game.Rigs(me)) Game.SetVelocity(rb, v);
                }
            }
            flyVel = Vector3.zero; if (me != null) foreach (var rb in Game.Rigs(me)) Game.SetVelocity(rb, Vector3.down * 3f);
            yield return new WaitForSeconds(0.6f);
            dunkBusy = false;
            foreach (var hc in h.cols) foreach (var mc in myCols) if (hc != null && mc != null) Physics.IgnoreCollision(hc, mc, false);
            if (!released) { }
        }

        IEnumerator CoRestoreRim(HoopObj h)
        {
            yield return new WaitForSeconds(0.7f);
            if (ballCol != null) foreach (var hc in h.cols) if (hc != null) Physics.IgnoreCollision(ballCol, hc, false);
        }

        // ======================= столкновения мяча =======================
        void BallCollide(Collision c)
        {
            float sp = c.relativeVelocity.magnitude; if (sp < 1.2f) return;
            string n = c.collider != null ? c.collider.name : "";
            if (shotLive || aiShotLive) guideOn = false;
            if (Time.time - lastBounceSfx < 0.06f) return; lastBounceSfx = Time.time;
            if (n.Contains("hoop_rim")) { Sfx2.Clank(Mathf.Clamp01(sp / 8f)); rimTouched = true; foreach (var h in hoops) if (h.root != null && c.collider.transform.IsChildOf(h.root.transform)) h.shake = Mathf.Max(h.shake, 0.6f); }
            else if (n.Contains("hoop_board")) { Sfx2.Board(); foreach (var h in hoops) if (h.root != null && c.collider.transform.IsChildOf(h.root.transform)) h.shake = Mathf.Max(h.shake, 0.4f); }
            else Sfx2.Bounce(Mathf.Clamp01(sp / 9f));
        }

        // ======================= гол =======================
        void CheckGoal()
        {
            if (ballGo == null || ballHeld || ballRb == null || ballRb.isKinematic || ballOwner != Game.LocalActor()) { if (ballGo != null) ballPrev = ballGo.transform.position; return; }
            Vector3 p = ballGo.transform.position;
            foreach (var h in hoops)
            {
                if (h.root == null) continue;
                if (ballPrev.y > h.rimC.y && p.y <= h.rimC.y && Time.time - lastGoalT > 1.2f)
                {
                    float f = (ballPrev.y - h.rimC.y) / Mathf.Max(0.0001f, ballPrev.y - p.y); Vector3 pc = Vector3.Lerp(ballPrev, p, f);
                    float d = Vector2.Distance(new Vector2(pc.x, pc.z), new Vector2(h.rimC.x, h.rimC.z));
                    if (d < h.rimR - 0.03f * h.scale && ballRb.velocity.y < 0f) { lastGoalT = Time.time; OnGoal(h); }
                }
            }
            ballPrev = p;
        }

        void OnGoal(HoopObj h)
        {
            int pts = shotLive && Time.time < shotUntil ? shotPts : 2; if (pts <= 0) pts = 2;
            bool clean = !rimTouched && !shotDunk; shotLive = false;
            int me = Game.LocalActor();
            if (shotLive == false && pts == 0) pts = 2;
            AddScore(me, pts);
            if (Game.InRoom) Net.Send("bb", "g;" + me + "," + pts + "," + h.id + "," + (clean ? 1 : 0));
            GoalFx(h, me, pts, clean, true, shotDunk);
            streak++; if (streak == 3) { BigText("ПЫЛАЕТ!  мяч в огне", new Color(1f, 0.5f, 0.1f)); Sfx2.Boom(); }
            AiOnPlayerScored();
            var v = ballRb.velocity; Game.SetVelocity(ballRb, new Vector3(v.x * 0.3f, v.y * 0.5f, v.z * 0.3f));
        }

        void GoalFx(HoopObj h, int actor, int pts, bool clean, bool mine, bool dunk = false)
        {
            h.shake = Mathf.Max(h.shake, 1f); Sfx2.Swish();
            Vfx.Ring(h.rimC, Vector3.up, new Color(1f, 0.85f, 0.4f, 0.8f), 2.2f, 0.45f);
            string who = mine ? "" : ActorName(actor) + ": ";
            if (pts >= 3) BigText(who + "ТРЁХОЧКОВЫЙ! +" + pts + (clean ? "  (чисто)" : ""), new Color(0.4f, 0.9f, 1f));
            else if (!dunk) BigText(who + (clean ? "ЧИСТО! +" : "ПОПАЛ! +") + pts, new Color(1f, 0.85f, 0.3f));
        }
        bool shotDunkRemote;

        void AddScore(int actor, int pts) { int v; scores.TryGetValue(actor, out v); scores[actor] = v + pts; }

        void BigText(string s, Color c) { bigText = s; bigUntil = Time.unscaledTime + 1.8f; bigCol = c; }

        // ======================= сеть =======================
        void SendBall(bool held)
        {
            nextBallSend = Time.time + 0.08f;
            if (!Game.InRoom || ballGo == null) return;
            Vector3 v = ballRb != null && !ballRb.isKinematic ? ballRb.velocity : Vector3.zero;
            Net.Send("bb", "p;" + V3(ballGo.transform.position) + "," + V3(v) + "," + (ballHeld ? 1 : 0));
        }

        static float[] Nums(string s)
        {
            var p = s.Split(','); var r = new float[p.Length];
            for (int i = 0; i < p.Length; i++) float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out r[i]);
            return r;
        }

        void OnBallNet(string arg, int sender)
        {
            if (string.IsNullOrEmpty(arg) || arg.Length < 2) return;
            string op = arg.Substring(0, arg.IndexOf(';') < 0 ? 1 : arg.IndexOf(';')); string rest = arg.IndexOf(';') < 0 ? "" : arg.Substring(arg.IndexOf(';') + 1);
            var n = Nums(rest.Length == 0 ? "0" : rest);
            try
            {
                switch (op)
                {
                    case "p":
                        if (n.Length < 7) return;
                        Vector3 pos = new Vector3(n[0], n[1], n[2]), vel = new Vector3(n[3], n[4], n[5]);
                        if (ballGo == null) EnsureBall(pos);
                        if (ballHeld) { ballHeld = false; shootCharging = false; ballArm = 0; }
                        aiHasBall = false; aiShotLive = false;
                        ballOwner = sender; ballRemoteHeld = n[6] > 0.5f; ballTarget = pos; ballTargetVel = vel; lastRecv = Time.time;
                        ballRb.isKinematic = true; ballCol.enabled = false; ballTrail.emitting = vel.magnitude > 14f; break;
                    case "h":
                        if (n.Length < 7) return;
                        foreach (var hh in hoops) if (hh.id == (int)n[0]) return;
                        { var hobj = BuildHoop(new Vector3(n[1], n[2], n[3]), Quaternion.Euler(0f, n[4], 0f) * Vector3.forward, n[5], n[6], (int)n[0]); hoops.Add(hobj); }
                        break;
                    case "x": if (rest == "h") ClearHoops(); else if (rest == "b") RemoveBall(); break;
                    case "g":
                        if (n.Length < 3) return;
                        AddScore((int)n[0], (int)n[1]);
                        foreach (var hh in hoops) if (hh.id == (int)n[2]) { shotDunkRemote = false; GoalFx(hh, (int)n[0], (int)n[1], n.Length > 3 && n[3] > 0.5f, false); }
                        break;
                    case "s":
                        if ((int)n[0] == Game.LocalActor() && ballHeld) StolenFrom(sender, n.Length > 1 && n[1] > 0.5f);
                        break;
                    case "z": OnRemoteZone(sender, n); break;
                }
            }
            catch (Exception e) { SafeLog("bb", e); }
        }

        void StolenFrom(int thief, bool force)
        {
            if (!(force || UnityEngine.Random.value < (zoneOn ? 0.15f : 0.55f))) { HeroToast("Отбил попытку перехвата"); return; }
            var me = Game.Me(); var t = Game.ByActor(thief);
            Vector3 dir = t != null ? (CenterOf(t) - CenterOf(me)).normalized : -FlatForward();
            ReleaseBall(ballGo.transform.position, dir * 4f + Vector3.up * 3f, 0, false);
            HeroToast("У тебя выбили мяч!"); Sfx2.Hit(0.4f);
        }

        // ======================= тик =======================
        void TickRemoteBall()
        {
            if (ballGo == null) return;
            if (ballOwner != Game.LocalActor() && ballOwner >= 0 && !ballHeld)
            {
                if (Time.time - lastRecv > 1.6f && !ballRemoteHeld) { ballOwner = -1; ballRb.isKinematic = false; ballCol.enabled = true; Game.SetVelocity(ballRb, Vector3.zero); }
                else
                {
                    Vector3 want = ballTarget + ballTargetVel * Mathf.Min(Time.time - lastRecv, 0.3f);
                    ballGo.transform.position = Vector3.Lerp(ballGo.transform.position, want, 1f - Mathf.Exp(-18f * Time.deltaTime));
                    ballGo.transform.Rotate(ballTargetVel.magnitude * 50f * Time.deltaTime, 0f, 0f, Space.World);
                }
            }
        }

        void TickHoopVisuals()
        {
            for (int i = hoops.Count - 1; i >= 0; i--)
            {
                var h = hoops[i]; if (h.root == null) { hoops.RemoveAt(i); continue; }
                h.shake = Mathf.MoveTowards(h.shake, 0f, Time.deltaTime * 1.4f);
                float sw = h.shake * Mathf.Sin(Time.time * 26f);
                if (h.shake > 0.001f || h.tilt != 0f) { h.root.transform.rotation = Quaternion.LookRotation(h.fwd, Vector3.up) * Quaternion.Euler(sw * 0.9f, 0f, sw * 0.5f); h.tilt = h.shake > 0.001f ? 1f : 0f; }
                if (h.net == null) continue;
                var T = h.root.transform; float sc = h.scale;
                for (int k = 0; k < h.net.Length; k++)
                {
                    var lr = h.net[k]; if (lr == null) continue;
                    float a = (k % 12) / 12f * Mathf.PI * 2f, d = Mathf.PI / 12f * (k < 12 ? 1f : -1f);
                    float flick = h.shake > 0.02f ? h.shake * 0.5f : 0f;
                    Vector3 p0 = Ring(h, a, 0.98f, -0.02f), p1 = Ring(h, a + d, 0.82f, -0.22f + 0.06f * flick * Mathf.Sin(Time.time * 18f + k)), p2 = Ring(h, a + 2f * d, 0.58f, -0.44f + 0.12f * flick * Mathf.Sin(Time.time * 15f + k));
                    lr.SetPosition(0, T.TransformPoint(p0)); lr.SetPosition(1, T.TransformPoint(p1)); lr.SetPosition(2, T.TransformPoint(p2));
                }
            }
        }

        static Vector3 Ring(HoopObj h, float a, float rk, float dy)
        {
            float R = h.rimR * rk; return h.rimLocal + new Vector3(Mathf.Cos(a) * R, dy * h.scale, Mathf.Sin(a) * R);
        }

        void TickHoops()
        {
            TickTestKey(); TickBackrooms(); TickAutoCases(); TickRemoteBall(); TickHoopVisuals(); TickZoneFx(); TickBallShadow(); TickBallFire();
            var me = Game.Me();
            if (ballGo == null) { ballHeld = false; ballArm = 0; }
            if (me == null) return;
            bool ctl = Cursor.lockState == CursorLockMode.Locked && !(showMenu && cfgFreeze.Value) && !puppetOn && !tRunning;
            bool alive = !Game.IsDead(me);
            if (ballGo != null && Time.time < ballHideUntil != !ballRend.enabled) ballRend.enabled = !(Time.time < ballHideUntil);
            if (ballGo != null && ballRb != null && !ballRb.isKinematic && ballRb.velocity.magnitude < 12f && ballTrail != null && ballTrail.emitting && !ballHeld) ballTrail.emitting = false;

            // магнит мяча (Зона / Лаки / Видение площадки)
            if (ballGo != null && !ballHeld && !aiHasBall && (zoneOn || Time.time < magnetUntil) && !ballRemoteHeld && alive)
            {
                Vector3 to = CenterOf(me) - ballGo.transform.position; float d = to.magnitude;
                if (d < 14f && d > 0.01f && ballOwner != -2)
                {
                    if (ballRb.isKinematic) { ballOwner = Game.LocalActor(); ballRb.isKinematic = false; ballCol.enabled = true; }
                    Game.SetVelocity(ballRb, Vector3.Lerp(ballRb.velocity, to.normalized * Mathf.Min(16f, 4f + d), 0.15f));
                    if (d < 1.6f && !dunkBusy) TakeBall(me);
                }
            }

            if (ballHeld && alive)
            {
                if (!dunkBusy) DribbleTick(me);
                if (ctl)
                {
                    if (Keys.Down(keyDodge.Value) && !dunkBusy && dribMove == 0) { if (Time.time - lastCrossT < 0.55f) StartCoroutine(CoDoubleCross(me)); else { Crossover(me); AiOnPlayerMove(5); } lastCrossT = Time.time; }
                    if (!dunkBusy && dribMove == 0 && !shootCharging)
                    {
                        if (Keys.Down(keyMove1.Value)) StartMove(me, 1); else if (Keys.Down(keyMove2.Value)) StartMove(me, 2);
                        else if (Keys.Down(keyMove3.Value)) StartMove(me, 3); else if (Keys.Down(keyMove4.Value)) StartMove(me, 4);
                    }
                    if (Keys.MouseDown(0) && !dunkBusy && !shootCharging)
                    {
                        bool jumpish = Input.GetKey(KeyCode.Space) || !GroundBelow(me);
                        var dh = jumpish ? DunkHoop(me, zoneOn ? 14f : 5.5f) : null;
                        if (dh != null) StartCoroutine(CoDunk(dh, false, me)); else { shootCharging = true; shootT0 = Time.time; }
                    }
                    if (shootCharging)
                    {
                        float c = (Time.time - shootT0) / 0.95f;
                        if (!Keys.MouseHeld(0) || c > 1.15f) { shootCharging = false; ReleaseShotCharge(me, c); }
                    }
                    if (Keys.MouseDown(1) && !dunkBusy) { if (shootCharging && aiGo != null) { shootCharging = false; HeroToast("Финт броском!"); AiOnPlayerFake(); } else { shootCharging = false; Pass(me, false); } }
                }
                else shootCharging = false;
                ballArm = dunkBusy ? 0 : (shootCharging ? 17 : (holdBlend > 0.6f ? 25 : 16));
                if (shootCharging)
                {
                    float c = Mathf.Clamp01((Time.time - shootT0) / 0.95f); Vector3 fw = FlatForward(); float sc = Mathf.Max(0.3f, me.transform.lossyScale.y);
                    Vector3 hp = HeadOf(me) + fw * 0.3f * sc + Vector3.up * (0.05f + 0.3f * c) * sc;
                    ballGo.transform.position = Vector3.Lerp(ballGo.transform.position, hp, 0.4f);
                }
            }
            else ballArm = Time.time < followUntil ? 18 : 0;

            if (ctl && alive && Keys.Down(keyBall.Value)) { if (ballHeld) DropBall(me, 3f); else TryTakeBall(me); }
            CheckGoal(); TickAi();
            if (ballGo != null && ballOwner == Game.LocalActor() && Time.time >= nextBallSend && !aiHasBall)
            {
                bool moving = ballHeld || (ballRb != null && !ballRb.isKinematic && ballRb.velocity.sqrMagnitude > 0.05f);
                if (moving || Time.time - nextBallSend > 1.2f) SendBall(ballHeld);
                else nextBallSend = Time.time + 1.2f;
            }
        }

        void TryTakeBall(Component me)
        {
            if (ballGo == null) { EnsureBall(CenterOf(me) + FlatForward() * 1.2f); Game.SetVelocity(ballRb, Vector3.zero); }
            if (aiHasBall) { PlayerStealFromAi(me); return; }
            float reach = 3.6f * Mathf.Max(1f, me.transform.lossyScale.y);
            float d = Vector3.Distance(ballGo.transform.position, CenterOf(me));
            if (ballRemoteHeld && d < reach) { TryStealAt(me, ballOwner, false); return; }
            if (d < reach) { TakeBall(me); return; }
            HeroToast("Мяч далеко (" + Mathf.RoundToInt(d) + " м): подойди ближе и нажми [" + keyBall.Value + "]");
        }

        void TryStealAt(Component me, int actor, bool force)
        {
            if (Game.InRoom) Net.Send("bb", "s;" + actor + "," + (force ? 1 : 0), actor);
            HeroToast("Попытка перехвата у " + Game.PlayerName(actor) + "…"); Sfx2.Whoosh();
        }

        // ======================= постройка кольца =======================
        void SpawnHoopAhead()
        {
            var me = Game.Me(); var cam = Camera.main; if (me == null || cam == null) { Ui.Toast("Нет персонажа", Ui.Bad); return; }
            if (hoops.Count >= 4) { Ui.Toast("Максимум 4 кольца — сначала убери лишние", Ui.Muted); return; }
            Vector3 o = cam.transform.position, f = FlatForward(); Vector3 gp = CenterOf(me) + f * 7f;
            RaycastHit rh; Vector3 target = o + cam.transform.forward * 8f;
            if (Physics.Raycast(o, cam.transform.forward, out rh, 14f, ~0, QueryTriggerInteraction.Ignore)) target = rh.point;
            gp = target; float gy = GroundY(gp, me.transform.position.y - 0.9f); gp.y = gy;
            Vector3 face = CenterOf(me) - gp; face.y = 0f; if (face.sqrMagnitude < 0.1f) face = -f; face.Normalize();
            gp -= face * 0.0f;
            int id = Game.LocalActor() * 100 + (++hoopSeq);
            float yaw = Quaternion.LookRotation(face).eulerAngles.y;
            var h = BuildHoop(gp, face, rimHeight, hoopScale, id); hoops.Add(h);
            if (Game.InRoom) Net.Send("bb", "h;" + id + "," + V3(gp) + "," + F(yaw) + "," + F(rimHeight) + "," + F(hoopScale));
            Sfx2.Board(); Ui.Toast("Кольцо поставлено. [" + keyBall.Value + "] — взять мяч, ЛКМ — бросок", Ui.Good);
            if (ballGo == null) { EnsureBall(CenterOf(me) + f * 1.3f + Vector3.up * 0.5f); if (Game.InRoom) SendBall(false); ballOwner = Game.LocalActor(); }
        }

        void ClearHoops()
        {
            foreach (var h in hoops) { if (h.root != null) UnityEngine.Object.Destroy(h.root); if (h.net != null) foreach (var l in h.net) if (l != null) UnityEngine.Object.Destroy(l.gameObject); if (h.arc != null) UnityEngine.Object.Destroy(h.arc.gameObject); }
            hoops.Clear();
        }

        GameObject PrimGO(PrimitiveType t, Transform parent, Vector3 lp, Vector3 ls, Color col, bool keepCol = false, string name = "PF_hoop_player")
        {
            var g = GameObject.CreatePrimitive(t); g.name = name; g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = ls;
            var mr = g.GetComponent<MeshRenderer>(); var m = Vfx.Mat(null, col); if (m != null) mr.sharedMaterial = m;
            if (!keepCol) { var c = g.GetComponent<Collider>(); if (c != null) UnityEngine.Object.DestroyImmediate(c); }
            return g;
        }

        HoopObj BuildHoop(Vector3 baseP, Vector3 f, float rimH, float sc, int id)
        {
            var h = new HoopObj { id = id, baseP = baseP, fwd = f, rimH = rimH, scale = sc };
            var root = new GameObject("PF_hoop_player"); root.transform.position = baseP; root.transform.rotation = Quaternion.LookRotation(f, Vector3.up); h.root = root;
            var T = root.transform; var gray = new Color(0.35f, 0.37f, 0.42f); var orange = new Color(1f, 0.38f, 0.08f);
            float tall = rimH + 0.9f * sc;
            var pole = PrimGO(PrimitiveType.Cylinder, T, new Vector3(0, tall * 0.5f, 0), new Vector3(0.14f * sc, tall * 0.5f, 0.14f * sc), gray, true); h.cols.Add(pole.GetComponent<Collider>());
            PrimGO(PrimitiveType.Cube, T, new Vector3(0, rimH - 0.1f * sc, 0.27f * sc), new Vector3(0.12f * sc, 0.1f * sc, 0.55f * sc), gray);
            PrimGO(PrimitiveType.Cube, T, new Vector3(0, 0.2f * sc, 0), new Vector3(0.7f * sc, 0.4f * sc, 0.7f * sc), new Color(0.12f, 0.14f, 0.3f));
            var board = PrimGO(PrimitiveType.Cube, T, new Vector3(0, rimH + 0.3f * sc, 0.5f * sc), new Vector3(1.8f * sc, 1.05f * sc, 0.05f * sc), new Color(0.96f, 0.97f, 1f), true, "PF_hoop_board_player");
            var bc = board.GetComponent<Collider>(); SetBounce(bc, 0.7f); h.cols.Add(bc);
            PrimGO(PrimitiveType.Cube, T, new Vector3(0, rimH + 0.17f * sc, 0.5f * sc + 0.03f * sc), new Vector3(0.6f * sc, 0.45f * sc, 0.012f * sc), new Color(0.9f, 0.15f, 0.1f));
            PrimGO(PrimitiveType.Cube, T, new Vector3(0, rimH + 0.17f * sc, 0.5f * sc + 0.034f * sc), new Vector3(0.54f * sc, 0.39f * sc, 0.012f * sc), new Color(0.96f, 0.97f, 1f));
            float R = 0.38f * sc; var rc = new Vector3(0, rimH, 0.95f * sc); h.rimLocal = rc; h.rimR = R;
            PrimGO(PrimitiveType.Cube, T, new Vector3(0, rimH, 0.5f * sc + 0.06f * sc + (0.62f * sc - 0.5f * sc) * 0.0f), new Vector3(0.05f * sc, 0.04f * sc, 0.2f * sc), orange);
            int seg = 24;
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f; Vector3 lp = rc + new Vector3(Mathf.Cos(a) * R, 0, Mathf.Sin(a) * R); Vector3 tan = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                var s = PrimGO(PrimitiveType.Cube, T, lp, new Vector3(0.035f * sc, 0.035f * sc, 2f * Mathf.PI * R / seg * 1.15f), orange);
                s.transform.localRotation = Quaternion.LookRotation(tan, Vector3.up);
            }
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f; var go = new GameObject("PF_hoop_rim_player"); go.transform.SetParent(T, false);
                go.transform.localPosition = rc + new Vector3(Mathf.Cos(a) * R, 0, Mathf.Sin(a) * R);
                var sc2 = go.AddComponent<SphereCollider>(); sc2.radius = 0.04f * sc; SetBounce(sc2, 0.6f); h.cols.Add(sc2);
            }
            h.rimC = T.TransformPoint(rc);
            var wm = Vfx.Mat(null, new Color(1f, 1f, 1f, 0.9f)); h.net = new LineRenderer[24];
            for (int i = 0; i < 24; i++)
            {
                var go = new GameObject("PF_hoop_net_player"); go.transform.SetParent(T, false);
                var lr = go.AddComponent<LineRenderer>(); lr.positionCount = 3; lr.useWorldSpace = true; lr.startWidth = lr.endWidth = 0.014f * sc; if (wm != null) lr.sharedMaterial = wm;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; h.net[i] = lr;
            }
            // трёхочковая линия по земле
            var al = new GameObject("PF_hoop_arc_player"); var arc = al.AddComponent<LineRenderer>(); arc.useWorldSpace = true; arc.startWidth = arc.endWidth = 0.07f;
            var am = Vfx.Mat(null, new Color(1f, 1f, 1f, 0.85f)); if (am != null) arc.sharedMaterial = am; arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            int np = 48; arc.positionCount = np; float ar = ThreeDist * sc; Vector3 rcW = h.rimC;
            for (int i = 0; i < np; i++)
            {
                float a = (i / (float)(np - 1) - 0.5f) * Mathf.PI * 1.15f; Vector3 dir = Quaternion.AngleAxis(a * Mathf.Rad2Deg, Vector3.up) * f;
                Vector3 p = new Vector3(rcW.x, 0, rcW.z) + dir * ar; float gy = GroundY(new Vector3(p.x, rcW.y, p.z), baseP.y); p.y = gy + 0.05f; arc.SetPosition(i, p);
            }
            h.arc = arc;
            return h;
        }

        // ======================= HUD =======================
        void DrawHoopHud()
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f), W = Screen.width, H = Screen.height; var th = Themes.Now;
            if (hoops.Count > 0 || scores.Count > 0)
            {
                var keys = new List<int>(scores.Keys); keys.Sort((a, b) => scores[b].CompareTo(scores[a]));
                int n = Mathf.Min(keys.Count, 4); float w = 320f * s, x = W * 0.5f - w * 0.5f, y = 12f * s;
                Ui.Box(new Rect(x, y, w, (36f + 24f * Mathf.Max(1, n)) * s), new Color(0, 0, 0, 0.5f), 14f * s);
                Ui.Text(new Rect(x, y + 2f * s, w, 30f * s), "БАСКЕТБОЛ", (int)(18f * s), th.accent, TextAnchor.MiddleCenter, true);
                if (n == 0) Ui.Text(new Rect(x, y + 34f * s, w, 24f * s), "счёт 0 : 0", (int)(15f * s), Color.white, TextAnchor.MiddleCenter, false);
                for (int i = 0; i < n; i++)
                {
                    int a = keys[i]; string nm = ActorName(a);
                    Ui.Text(new Rect(x + 16f * s, y + (34f + 24f * i) * s, w - 100f * s, 24f * s), nm, (int)(15f * s), Color.white, TextAnchor.MiddleLeft, false);
                    Ui.Text(new Rect(x + w - 90f * s, y + (34f + 24f * i) * s, 74f * s, 24f * s), scores[a].ToString(), (int)(17f * s), th.accent2, TextAnchor.MiddleRight, true);
                }
            }
            DrawAiTag(); DrawTestHud(); DrawBackrooms();
            if (shootCharging)
            {
                float c = Mathf.Clamp((Time.time - shootT0) / 0.95f, 0f, 1.15f), bw = 280f * s, bh = 16f * s, bx = W * 0.5f - bw * 0.5f, by = H * 0.6f;
                Ui.Box(new Rect(bx, by, bw, bh), new Color(0, 0, 0, 0.6f), bh * 0.5f);
                Ui.Box(new Rect(bx + bw * 0.72f / 1.15f, by, bw * 0.2f / 1.15f, bh), new Color(0.3f, 1f, 0.5f, 0.85f), 0f);
                Ui.Box(new Rect(bx + bw * c / 1.15f - 3f * s, by - 4f * s, 6f * s, bh + 8f * s), Color.white, 3f * s);
                Ui.Text(new Rect(bx, by + bh + 2f * s, bw, 20f * s), "отпусти в зелёной зоне", (int)(13f * s), new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, true);
            }
            if (ballHeld)
                Ui.Text(new Rect(W * 0.5f - 500f * s, H * 0.66f, 1000f * s, 26f * s), "ЛКМ — бросок · Пробел+ЛКМ у кольца — ДАНК · ПКМ — пас · [" + keyDodge.Value + "] кроссовер (×2 — двойной) · [" + keyMove1.Value + "] между ног · [" + keyMove2.Value + "] за спиной · [" + keyMove3.Value + "] разворот · [" + keyMove4.Value + "] финт · [" + keyBall.Value + "] бросить" + keyDodge.Value + "] — кроссовер   ·   [" + keyBall.Value + "] — бросить мяч", (int)(14f * s), new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, true);
            else if (ballGo != null && Game.Me() != null && Vector3.Distance(ballGo.transform.position, CenterOf(Game.Me())) < 4f)
                Ui.Text(new Rect(W * 0.5f - 300f * s, H * 0.62f, 600f * s, 28f * s), "[" + keyBall.Value + "] — взять мяч" + (ballRemoteHeld || aiHasBall ? " (перехват)" : ""), (int)(18f * s), Color.white, TextAnchor.MiddleCenter, true);
            if (Time.unscaledTime < bigUntil)
            {
                float k = 1f - (bigUntil - Time.unscaledTime) / 1.8f, sc = Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(k * 6f));
                var c = bigCol; c.a = Mathf.Clamp01((1f - k) * 3f);
                Ui.Text(new Rect(0, H * 0.2f, W, 90f * s), bigText, (int)(60f * s * sc), c, TextAnchor.MiddleCenter, true);
            }
        }

        // ======================= вкладка меню =======================
        void BasketTab()
        {
            AiTab();
            Ui.Header("Баскетбол");
            Ui.Para("Поставь кольцо, возьми мяч [" + keyBall.Value + "] и играй: мяч ведётся рукой и отскакивает от земли, бросок — зажми ЛКМ и отпусти в зелёной зоне (как в аркаде). За дугой — 3 очка. Данк — Пробел+ЛКМ рядом с кольцом (6 стилей: классика, двумя руками, разворот 360°, сальто, мельница, повисеть на кольце). ПКМ — пас, Q — кроссовер. Дриблинг: Q — кроссовер (дважды — двойной), T — между ног, Y — за спиной, U — разворот, J — финт. Если постоять, мяч берётся двумя руками к груди. Данков 11 стилей. У друзей с PeakFun кольцо, мяч и счёт видны, мяч можно перехватывать. Герои — Винс Картер, Аллен Айверсон, Коби Брайант, Стивен Карри, Крис Пол, Мэджик Джонсон, Шакил О'Нил (вкладка выше).", 12);
            if (Ui.Button("Поставить кольцо перед собой", 1)) Safe("кольцо", SpawnHoopAhead);
            if (Ui.Button("Убрать кольца")) { ClearHoops(); if (Game.InRoom) Net.Send("bb", "x;h"); }
            var me = Game.Me();
            if (Ui.Button(ballGo == null ? "Создать мяч и взять в руки" : "Взять мяч в руки (телепортировать)", 0, me != null)) Safe("мяч", () => { if (ballGo == null) EnsureBall(CenterOf(me) + FlatForward()); else { ballGo.transform.position = CenterOf(me) + FlatForward(); Game.SetVelocity(ballRb, Vector3.zero); } TakeBall(me); });
            if (Ui.Button("Убрать мяч", 2, ballGo != null)) { RemoveBall(); if (Game.InRoom) Net.Send("bb", "x;b"); }
            if (Ui.Button("Сбросить счёт")) scores.Clear();
            rimHeight = Ui.Slider("Высота кольца (для новых колец)", rimHeight, 2f, 5f, "0.00", " м");
            hoopScale = Ui.Slider("Масштаб кольца и мяча", hoopScale, 0.7f, 3f, "0.0", "×");
            string[] dn = { "Случайный", "Классика", "Двумя руками", "Разворот 360°", "Сальто", "Мельница", "Повиснуть", "Томагавк", "Бэкфлип", "Обратный", "Ураган 720°", "Полёт" };
            int dsel = Ui.Buttons(dn, 4, 1, 34f, true, dunkStyle >= 11 ? 0 : dunkStyle + 1, 13); if (dsel >= 0) dunkStyle = dsel == 0 ? 11 : dsel - 1;
            aimAssist = Ui.Toggle("Помощь прицела (меньше промахов)", aimAssist, "Бросок летит в кольцо, если смотришь примерно на него; ошибка зависит от тайминга");
        }
    }
}
