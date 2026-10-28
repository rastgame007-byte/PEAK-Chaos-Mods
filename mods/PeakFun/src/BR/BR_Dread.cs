// «Dread» — давящая атмосфера вместо громких скримеров: растущая тревога, тишина, шёпот, шаги за спиной,
// силуэт на краю зрения, дыхание камеры, сужение поля зрения. Всё локально и без обязательных громких звуков.
using System;
using UnityEngine;

namespace PeakBR
{
    internal static class Dread
    {
        public static float Level, SilenceMul = 1f; public static bool Enabled = true;
        static float tgt, nextWhisper, nextBehind, nextEye, nextSilence, silenceEnd, eyeT, eyeSide, eyeLife, lastYaw, turnRate, moveSpd, behindT, behindDist, farNext;
        static float nextOutage, outT; static int outageLeft; static int behindLeft; static Vector3 lastPc; static bool havePc, silenceFired; static AudioSource drone; static Texture2D vig, fig, vigW; static float breathSeed;

        public static void Bump(float v) { Level = Mathf.Clamp01(Level + v); }

        public static void Reset()
        {
            Level = 0f; tgt = 0f; SilenceMul = 1f; behindLeft = 0; eyeT = 0f; havePc = false; silenceEnd = 0f; silenceFired = false; nextOutage = 0f; outageLeft = 0;
            if (drone != null) { UnityEngine.Object.Destroy(drone.gameObject); drone = null; }
            float n = Time.time; nextWhisper = n + UnityEngine.Random.Range(60f, 110f); nextBehind = n + UnityEngine.Random.Range(100f, 180f); nextEye = n + UnityEngine.Random.Range(70f, 130f); nextSilence = n + UnityEngine.Random.Range(80f, 140f); farNext = n + UnityEngine.Random.Range(35f, 70f); breathSeed = UnityEngine.Random.value * 50f;
        }

        public static void Tick(float dt, Vector3 pc, Camera cam, float prox)
        {
            if (!Enabled || Play.L == null || Play.L.rest) { Level = Mathf.MoveTowards(Level, 0f, dt * 0.2f); UpdateDrone(dt); return; }
            float now = Time.time, inT = now - Play.LevelT0; int N = Mathf.Max(1, Levels.All.Length);
            tgt = Mathf.Clamp01(inT / 300f) * 0.34f + (Play.L.dark ? 0.2f : 0f) + (Play.Level / (float)N) * 0.22f + prox * 0.45f + (Play.L.humKind == "dark" ? 0.08f : 0f);
            if (Play.Catching) tgt = 0.9f;
            Level = Mathf.MoveTowards(Level, Mathf.Clamp01(tgt), dt * (tgt > Level ? 0.04f : 0.08f));
            // скорость движения и поворота
            if (havePc) moveSpd = Mathf.Lerp(moveSpd, Util.Flat(pc - lastPc).magnitude / Mathf.Max(0.001f, dt), 0.2f); lastPc = pc; havePc = true;
            if (cam != null) { float y = cam.transform.eulerAngles.y; turnRate = Mathf.Lerp(turnRate, Mathf.Abs(Mathf.DeltaAngle(lastYaw, y)) / Mathf.Max(0.001f, dt), 0.3f); lastYaw = y; }
            // тишина: гул и атмосфера пропадают на несколько секунд, затем — одиночный далёкий звук
            if (Level > 0.22f && now > nextSilence && now > silenceEnd && prox < 0.35f && !Play.Catching) { silenceEnd = now + UnityEngine.Random.Range(5f, 9.5f); silenceFired = false; nextSilence = silenceEnd + UnityEngine.Random.Range(80f, 170f) / Mathf.Lerp(0.8f, 1.6f, Level); }
            bool inSil = now < silenceEnd; SilenceMul = Mathf.MoveTowards(SilenceMul, inSil ? 0f : 1f, dt * (inSil ? 0.7f : 0.45f));
            // «отключения света»: несколько резких провалов освещения подряд (на миг всё погружается во тьму)
            if (Play.Cur != null && Level > 0.25f && !inSil && prox < 0.3f && !Play.Catching && !Play.L.rest && now > nextOutage) { if (nextOutage <= 0f) nextOutage = now + UnityEngine.Random.Range(40f, 90f); else { nextOutage = now + UnityEngine.Random.Range(80f, 170f) / Mathf.Lerp(0.8f, 1.6f, Level); outageLeft = UnityEngine.Random.Range(2, 5); outT = now; } }
            if (outageLeft > 0 && now >= outT && Play.Cur != null) { outageLeft--; outT = now + UnityEngine.Random.Range(0.22f, 0.65f); Play.Cur.rig.Mul = 0f; try { Snd2.At("zap", pc, 0.18f); } catch { } }
            if (!inSil && !silenceFired && silenceEnd > 0f) { silenceFired = true; FarSound(pc, 16f, 28f, 0.5f); }
            // редкие далёкие звуки
            if (now > farNext && !inSil && Level > 0.12f) { farNext = now + UnityEngine.Random.Range(30f, 75f) / Mathf.Lerp(0.8f, 1.7f, Level); FarSound(pc, 14f, 30f, 0.3f + 0.2f * Level); }
            // шёпот за спиной
            if (Level > 0.38f && now > nextWhisper && !inSil && !Play.Catching)
            {
                nextWhisper = now + UnityEngine.Random.Range(45f, 95f) / Mathf.Lerp(0.8f, 1.8f, Level); Vector3 fw = cam != null ? cam.transform.forward : Vector3.forward; fw.y = 0f; fw.Normalize(); Vector3 sd = Vector3.Cross(Vector3.up, fw) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                Snd2.At("whisper", pc - fw * 1.1f + sd * 0.55f + Vector3.up * 0.1f, 0.22f + 0.28f * Level);
            }
            // шаги за спиной: идут, пока идёшь; стоит — замирают
            if (Level > 0.3f && behindLeft <= 0 && now > nextBehind && moveSpd > 1.5f && prox < 0.3f && !inSil) { behindLeft = UnityEngine.Random.Range(5, 9); behindDist = UnityEngine.Random.Range(8f, 11f); behindT = now + 0.5f; nextBehind = now + UnityEngine.Random.Range(120f, 230f) / Mathf.Lerp(0.8f, 1.6f, Level); }
            if (behindLeft > 0 && now > behindT)
            {
                if (moveSpd < 0.4f) { behindLeft = 0; }
                else
                {
                    Vector3 fw = cam != null ? cam.transform.forward : Vector3.forward; fw.y = 0f; fw.Normalize(); float j = (behindLeft % 2 == 0 ? 0.25f : -0.25f); behindT = now + Mathf.Clamp(0.78f - moveSpd * 0.05f, 0.45f, 0.8f);
                    Snd2.At("step", pc - fw * behindDist + Vector3.Cross(Vector3.up, fw) * j + Vector3.down * 1.0f, 0.5f); behindDist = Mathf.Max(4.5f, behindDist - 0.7f); behindLeft--;
                }
            }
            // силуэт на краю зрения
            if (eyeT <= 0f && Level > 0.45f && now > nextEye && prox < 0.25f && !inSil && !Play.Catching) { eyeSide = UnityEngine.Random.value < 0.5f ? -1f : 1f; eyeT = 0.001f; eyeLife = UnityEngine.Random.Range(2.2f, 3.4f); nextEye = now + UnityEngine.Random.Range(70f, 150f) / Mathf.Lerp(0.8f, 1.6f, Level); }
            if (eyeT > 0f) { eyeT += dt; if (eyeT > eyeLife + 1f || (turnRate > 55f && eyeT > 0.9f)) eyeT = -1f; if (eyeT < 0f) eyeT = 0f; }
            UpdateDrone(dt);
        }

        static void FarSound(Vector3 pc, float dmin, float dmax, float vol)
        {
            string[] ks = { "knock", "step", "creak", "pipe", "creak", "step" }; float ang = UnityEngine.Random.value * Mathf.PI * 2f; float d = UnityEngine.Random.Range(dmin, dmax);
            Snd2.At(ks[UnityEngine.Random.Range(0, ks.Length)], pc + new Vector3(Mathf.Cos(ang) * d, 0f, Mathf.Sin(ang) * d), vol);
        }

        static void UpdateDrone(float dt)
        {
            if (Level > 0.03f && Play.On && drone == null) { var c = Snd2.Get("dread"); if (c != null) { var g = new GameObject("BR_dread"); drone = g.AddComponent<AudioSource>(); drone.clip = c; drone.loop = true; drone.spatialBlend = 0f; drone.volume = 0f; drone.Play(); } }
            if (drone != null) { drone.volume = Mathf.Pow(Level, 1.3f) * 0.42f * SilenceMul; if (Level <= 0.001f && !Play.On) { UnityEngine.Object.Destroy(drone.gameObject); drone = null; } }
        }

        /// <summary>Лёгкое «дыхание» камеры: медленный крен и покачивание, растущие вместе с тревогой.</summary>
        public static void Sway(Transform tr)
        {
            float t = Time.time + breathSeed, l = Level; float roll = (Mathf.Sin(t * 0.37f) * 0.7f + Mathf.Sin(t * 0.91f) * 0.3f) * 0.9f * l; float pitch = Mathf.Sin(t * 1.1f) * 0.28f * l * (1f + Mathf.Clamp01(Play.Prox) * 1.5f);
            tr.rotation = tr.rotation * Quaternion.Euler(pitch, Mathf.Sin(t * 0.53f) * 0.35f * l, roll);
        }

        /// <summary>Приглушённый «удар» при поимке — вместо визга.</summary>
        public static void CatchSting() { Snd2.Flat("boom", 0.6f); }

        static void Init()
        {
            if (vig == null) vig = Vfx.Tex(128, 128, (u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f; float a = Mathf.Clamp01((d - 0.5f) / 0.75f); return new Color(0f, 0f, 0.012f, a * a * (3f - 2f * a)); });
            if (vigW == null) vigW = Vfx.Tex(96, 96, (u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f; float a = Mathf.Clamp01((d - 0.45f) / 0.8f); return new Color(1f, 1f, 1f, a * a); });
            if (fig == null) fig = Vfx.Tex(48, 160, (u, v) =>
            {   // высокий узкий силуэт: вытянутая голова, длинная шея, покатые плечи, тело-колонна
                float x = (u - 0.5f) * 2f, y = v; float a = 0f;
                float hd = Mathf.Sqrt(Mathf.Pow(x / 0.28f, 2f) + Mathf.Pow((y - 0.9f) / 0.085f, 2f)); a = Mathf.Max(a, 1f - Mathf.Clamp01((hd - 0.85f) / 0.3f));
                if (y < 0.82f && y > 0.7f) a = Mathf.Max(a, 1f - Mathf.Clamp01((Mathf.Abs(x) - 0.1f) / 0.12f));
                if (y <= 0.7f) { float w = Mathf.Lerp(0.2f, 0.62f, Mathf.Clamp01((0.7f - y) / 0.06f)) * Mathf.Lerp(1f, 0.75f, Mathf.Clamp01((0.6f - y) / 0.6f)); a = Mathf.Max(a, 1f - Mathf.Clamp01((Mathf.Abs(x) - w) / 0.2f)); }
                return new Color(0f, 0f, 0f, Mathf.Clamp01(a) * Mathf.Clamp01(y * 10f));
            });
        }

        /// <summary>Цветная кайма по краям экрана (например, красная при погоне).</summary>
        public static void Vignette(float W, float H, Color c, float a) { Init(); GUI.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a)); GUI.DrawTexture(new Rect(0, 0, W, H), vigW); GUI.color = Color.white; }

        public static void Draw(float W, float H, float s)
        {
            if (!Enabled || Level < 0.02f) return; Init(); float l = Level, t = Time.time;
            // сужение поля зрения и «дыхание»: чем выше тревога, тем плотнее тьма по краям
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(0.7f, 1.6f, l) + breathSeed); float grow = 1.25f - 0.32f * l - 0.025f * pulse * l; float a = Mathf.Clamp01(0.18f + 0.78f * l);
            float w = W * grow, h = H * grow; GUI.color = new Color(1f, 1f, 1f, a); GUI.DrawTexture(new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h), vig);
            GUI.color = new Color(0f, 0.01f, 0.03f, 0.1f * l * l); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            // фигура на краю зрения
            if (eyeT > 0f)
            {
                float fadeIn = Mathf.Clamp01(eyeT / 1.3f), fadeOut = Mathf.Clamp01((eyeLife + 1f - eyeT) / 0.8f), al = fadeIn * fadeOut * 0.62f * Mathf.Clamp01((turnRate > 40f) ? 0.4f : 1f);
                float fh = H * 0.62f, fw = fh * 0.3f, x = eyeSide < 0f ? W * 0.015f : W - fw - W * 0.015f; float sway = Mathf.Sin(t * 1.3f) * 2f * s; GUI.color = new Color(1f, 1f, 1f, al); GUI.DrawTexture(new Rect(x + sway, H * 0.5f - fh * 0.42f, fw, fh), fig);
            }
            GUI.color = Color.white;
        }
    }
}
