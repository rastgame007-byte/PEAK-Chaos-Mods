// Баскетбол 1.14: приёмы дриблинга, ведение мяча (держит двумя руками, подбор), тень и вид мяча.
using System;
using System.Collections;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        int dribMove; float dribMoveT0, dribMoveDur, dribMoveEnd, dribMoveSide = 1f; bool dribMoveHit, dribArmR = true;
        float holdBlend, idleT, pickT0 = -9f, lastSideSwitch, lastCrossT; Vector3 pickFrom;
        ConfigEntry<KeyCode> keyMove1, keyMove2, keyMove3, keyMove4;
        GameObject ballShadow; Material ballShadowMat; int streak;
        static readonly string[] MoveNames = { "", "Между ног!", "За спиной!", "Разворот!", "Финт!" };

        void InitMoves()
        {
            keyMove1 = Config.Bind("Keys", "DribbleBetweenLegs", KeyCode.T, "Дриблинг: мяч между ног");
            keyMove2 = Config.Bind("Keys", "DribbleBehindBack", KeyCode.Y, "Дриблинг: мяч за спиной");
            keyMove3 = Config.Bind("Keys", "DribbleSpin", KeyCode.U, "Дриблинг: разворот на 360° с мячом");
            keyMove4 = Config.Bind("Keys", "DribbleHesitate", KeyCode.J, "Дриблинг: финт с задержкой и рывком");
        }

        void StartMove(Component me, int kind)
        {
            if (dribMove != 0 || dunkBusy || ballGo == null || !ballHeld) return;
            dribMove = kind; dribMoveT0 = Time.time; dribMoveHit = false;
            dribMoveDur = kind == 1 ? 0.46f : kind == 2 ? 0.5f : kind == 3 ? 0.62f : 0.56f; dribMoveEnd = dribMoveT0 + dribMoveDur;
            dribMoveSide = dribSideCur >= 0f ? 1f : -1f; shootCharging = false;
            Vector3 fw = FlatForward(), side = Vector3.Cross(Vector3.up, fw).normalized;
            Sfx2.Whoosh();
            switch (kind)
            {
                case 1: Dash(fw * 0.8f - side * dribMoveSide * 0.5f, 8.5f, 0.4f); dribSide = -dribMoveSide; break;
                case 2: Dash(fw * 0.6f - side * dribMoveSide * 0.6f, 8f, 0.45f); dribSide = -dribMoveSide; break;
                case 3: Dash(fw * 0.9f + side * dribMoveSide * 0.3f, 9.5f, 0.55f); StartCoroutine(CoSpinMove(me, dribMoveDur, dribMoveSide)); break;
                default: Dash(-fw * 0.5f, 5f, 0.2f); break;
            }
            HeroToast(MoveNames[kind]);
            AiOnPlayerMove(kind);
        }

        IEnumerator CoDoubleCross(Component me)
        {
            Crossover(me); yield return new WaitForSeconds(0.24f);
            if (me != null && ballHeld) { Crossover(me); HeroToast("Двойной кроссовер!"); AiOnPlayerMove(5); }
        }

        IEnumerator CoSpinMove(Component me, float dur, float sd)
        {
            float t = 0f, prev = 0f;
            while (t < dur && me != null)
            {
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime; float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                float step = (k - prev) * 360f * sd; prev = k;
                var q = Quaternion.AngleAxis(step, Vector3.up); Vector3 c = CenterOf(me);
                foreach (var rb in Game.Rigs(me)) { rb.position = c + q * (rb.position - c); rb.rotation = q * rb.rotation; }
            }
        }

        Vector3 MoveBallPos(Component me, int kind, float u, float sd, Vector3 c0, Vector3 fw, Vector3 side, float sc, out float latS)
        {
            float r = 0.15f * hoopScale, gy = GroundY(c0 + fw * 0.2f * sc, me.transform.position.y);
            float top = c0.y - 0.05f * sc, A = Mathf.Max(0.25f, top - gy - r);
            u = Mathf.Clamp01(u); float cu = Mathf.Cos(Mathf.PI * u), su = Mathf.Sin(Mathf.PI * u); Vector3 pos;
            switch (kind)
            {
                case 1:
                    pos = c0 + side * (0.36f * sc * sd * cu) + fw * Mathf.Lerp(0.34f * sc, 0.05f * sc, su); pos.y = gy + r + A * Mathf.Abs(cu); break;
                case 2:
                    pos = c0 + side * (0.36f * sc * sd * cu) + fw * Mathf.Lerp(0.34f * sc, -0.42f * sc, su); pos.y = Mathf.Lerp(top, c0.y - 0.42f * sc, su); break;
                case 3:
                    {
                        float ang = Mathf.SmoothStep(0f, 1f, u) * 360f * sd;
                        Vector3 b = side * 0.36f * sd * sc + fw * 0.34f * sc; pos = c0 + Quaternion.AngleAxis(ang, Vector3.up) * b;
                        pos.y = gy + r + A * Mathf.Abs(Mathf.Cos(2f * Mathf.PI * u)); break;
                    }
                default:
                    {
                        pos = c0 + side * 0.30f * sd * sc + fw * 0.36f * sc * (1f + 0.5f * Mathf.Max(0f, u - 0.5f) * 2f);
                        float f = u < 0.5f ? 0.55f * Mathf.Abs(Mathf.Cos(4f * Mathf.PI * u)) : Mathf.Lerp(0.55f, 1f, (u - 0.5f) * 2f) * Mathf.Abs(Mathf.Cos(2f * Mathf.PI * (u - 0.5f)));
                        pos.y = gy + r + A * f; break;
                    }
            }
            latS = Vector3.Dot(pos - c0, side);
            return pos;
        }

        // ---------- мяч: тень на земле и огонь ----------
        void EnsureBallShadow()
        {
            if (ballShadow != null) return;
            ballShadow = new GameObject("PF_ballshadow_player");
            var mf = ballShadow.AddComponent<MeshFilter>(); mf.sharedMesh = Vfx.Quad();
            var mr = ballShadow.AddComponent<MeshRenderer>(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            ballShadowMat = Vfx.Mat(Vfx.Blob(), new Color(0f, 0f, 0f, 0.6f)); if (ballShadowMat != null) mr.sharedMaterial = ballShadowMat;
            ballShadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        void TickBallShadow()
        {
            if (ballGo == null) { if (ballShadow != null) ballShadow.SetActive(false); return; }
            EnsureBallShadow();
            bool vis = ballRend != null && ballRend.enabled && ballGo.activeSelf; ballShadow.SetActive(vis);
            if (!vis) return;
            Vector3 p = ballGo.transform.position; float gy = GroundY(p, p.y - 2.5f), hgt = Mathf.Max(0f, p.y - gy);
            float sz = 0.62f * hoopScale * (1f - 0.55f * Mathf.Clamp01(hgt / 9f));
            ballShadow.transform.position = new Vector3(p.x, gy + 0.035f, p.z); ballShadow.transform.localScale = new Vector3(sz, sz, 1f);
            if (ballShadowMat != null) ballShadowMat.color = new Color(0f, 0f, 0f, 0.62f * (1f - Mathf.Clamp01(hgt / 11f)));
        }

        bool ballFireOn;
        void TickBallFire()
        {
            if (ballGo == null || ballTrail == null) return;
            bool fire = streak >= 3 || zoneOn;
            if (fire != ballFireOn)
            {
                ballFireOn = fire;
                if (fire) { ballTrail.startColor = zoneOn ? new Color(0.5f, 0.9f, 1f, 0.9f) : new Color(1f, 0.55f, 0.1f, 0.95f); ballTrail.endColor = zoneOn ? new Color(0.2f, 0.5f, 1f, 0f) : new Color(1f, 0.15f, 0f, 0f); ballTrail.time = 0.4f; ballTrail.startWidth = 0.2f; }
                else { ballTrail.startColor = new Color(1f, 0.7f, 0.3f, 0.8f); ballTrail.endColor = new Color(1f, 0.5f, 0.1f, 0f); ballTrail.time = 0.28f; ballTrail.startWidth = 0.12f; }
            }
            if (fire) { ballTrail.emitting = true; if (Time.frameCount % 4 == 0) Vfx.Sparks(ballGo.transform.position, Vector3.up, 1, zoneOn ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.5f, 0.1f)); }
        }
    }
}
