// Эффекты PeakJJK: частицы, кольца, молнии, световые вспышки, тряска камеры, hit-stop, всплывающие числа, процедурные звуки.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakJJK
{
    internal static class Fx
    {
        // ---------------- частицы ----------------
        static ParticleSystem psShrink, psGrow, psSpark; static GameObject host;
        static ParticleSystem MakePS(string name, bool stretch, bool grow, float gravity)
        {
            var go = new GameObject(name); go.transform.SetParent(host.transform, false); var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = stretch ? 1200 : 2400; main.gravityModifier = gravity;
            var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, grow ? AnimationCurve.Linear(0, 0.35f, 1, 1.6f) : AnimationCurve.Linear(0, 1f, 1, 0.1f));
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1) }); col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.material = Gfx.Fade(Gfx.Blob(), Color.white, !grow); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            if (stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 2.4f; }
            ps.Play(); return ps;
        }
        static void EnsureHost()
        {
            if (host != null) return; host = new GameObject("JJK_FX"); UnityEngine.Object.DontDestroyOnLoad(host);
            psShrink = MakePS("shrink", false, false, 0f); psGrow = MakePS("grow", false, true, 0f); psSpark = MakePS("spark", true, false, 0.8f);
        }
        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, Color col, float life)
        {
            var ep = new ParticleSystem.EmitParams(); ep.position = pos; ep.velocity = vel; ep.startSize = size; ep.startLifetime = life; ep.startColor = col; ps.Emit(ep, 1);
        }
        /// <summary>Облачко: grow=true — расширяющийся дым, иначе тающая искра-шар.</summary>
        public static void Puff(Vector3 pos, Vector3 vel, float size, Color col, float life, bool grow = false) { try { EnsureHost(); Emit(grow ? psGrow : psShrink, pos, vel, size, col, life); } catch { } }
        public static void Spark(Vector3 pos, Vector3 vel, float size, Color col, float life) { try { EnsureHost(); Emit(psSpark, pos, vel, size, col, life); } catch { } }
        public static void Burst(Vector3 pos, int count, float speed, float size, Color col, float life, bool sparks = true, float up = 0f)
        {
            try
            {
                EnsureHost();
                for (int i = 0; i < count; i++)
                {
                    Vector3 d = UnityEngine.Random.onUnitSphere; d.y += up; Vector3 vel = d * speed * UnityEngine.Random.Range(0.35f, 1f);
                    if (sparks) Emit(psSpark, pos, vel, size * UnityEngine.Random.Range(0.5f, 1f), col, life * UnityEngine.Random.Range(0.6f, 1f)); else Emit(psGrow, pos, vel * 0.5f, size, col, life);
                }
            }
            catch { }
        }
        /// <summary>Вихрь вокруг точки (аура/заряд): count частиц по кольцу, поднимаются.</summary>
        public static void Swirl(Vector3 center, float radius, int count, Color col, float size, float rise = 2f)
        {
            try { EnsureHost(); for (int i = 0; i < count; i++) { float a = UnityEngine.Random.value * Mathf.PI * 2f; Vector3 off = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * UnityEngine.Random.Range(0.6f, 1f); Emit(psShrink, center + off + Vector3.up * UnityEngine.Random.Range(-0.8f, 0.6f), new Vector3(-off.z, rise, off.x) * 0.6f / Mathf.Max(0.5f, radius), size, col, UnityEngine.Random.Range(0.4f, 0.8f)); } } catch { }
        }

        // ---------------- короткоживущие объекты ----------------
        sealed class Item { public GameObject go; public float t0, dur; public Action<float, GameObject> upd; }
        static readonly List<Item> items = new List<Item>();
        public static void Add(GameObject go, float dur, Action<float, GameObject> upd) { items.Add(new Item { go = go, t0 = Time.time, dur = dur, upd = upd }); }
        public static void Tick()
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i]; if (it.go == null) { items.RemoveAt(i); continue; }
                float k = (Time.time - it.t0) / it.dur; if (k >= 1f) { Kill(it.go); items.RemoveAt(i); continue; }
                try { it.upd(k, it.go); } catch { }
            }
            TickShake(); TickPopups(); TickHitStop();
        }
        public static void Kill(GameObject go) { if (go == null) return; foreach (var r in go.GetComponentsInChildren<Renderer>()) { var m = r.sharedMaterial; if (m != null && m.name == "jjk_own") UnityEngine.Object.Destroy(m); } UnityEngine.Object.Destroy(go); }
        public static void Clear() { foreach (var it in items) if (it.go != null) Kill(it.go); items.Clear(); popups.Clear(); shakeAmp = 0; flashA = 0; ResetTime(); }

        static Mesh sphereMesh;
        public static Mesh Sphere() { if (sphereMesh == null) { var mb = new MB(); mb.Ell(Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity, Color.white, 14, 20); sphereMesh = mb.ToMesh("jjk_sphere"); } return sphereMesh; }

        /// <summary>Расширяющаяся светящаяся сфера (взрыв, ядро техники).</summary>
        public static void Orb(Vector3 pos, float maxR, Color col, float dur, bool hold = false)
        {
            var go = new GameObject("JJK_Orb"); go.transform.position = pos; go.AddComponent<MeshFilter>().sharedMesh = Sphere(); var mr = go.AddComponent<MeshRenderer>();
            var mat = Gfx.Fade(null, col, true); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            Add(go, dur, (k, g) => { float r = maxR * (hold ? Mathf.Sin(Mathf.Min(1f, k * 3f) * 1.5f) : (1f - Mathf.Pow(1f - k, 3f))); g.transform.localScale = Vector3.one * Mathf.Max(0.01f, r * 2f); var c = col; c.a = col.a * (hold ? 1f - Mathf.Pow(k, 4f) : 1f - k); mat.color = c; });
        }
        /// <summary>Ударная волна-кольцо (линия).</summary>
        public static void Ring(Vector3 pos, Vector3 normal, Color col, float maxR, float dur, float width = 0.4f)
        {
            var go = new GameObject("JJK_Ring"); go.transform.position = pos; go.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude < 0.01f ? Vector3.up : normal.normalized);
            var lr = go.AddComponent<LineRenderer>(); lr.material = Gfx.Fade(null, Color.white, true); lr.useWorldSpace = false; lr.loop = true; lr.positionCount = 48; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 48; i++) { float a = i * Mathf.PI * 2f / 48f; lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f)); }
            Add(go, dur, (k, g) => { float r = maxR * (1f - Mathf.Pow(1f - k, 3f)); g.transform.localScale = Vector3.one * Mathf.Max(0.01f, r); var c = col; c.a = (1f - k) * col.a; lr.startColor = c; lr.endColor = c; lr.startWidth = lr.endWidth = Mathf.Lerp(width, 0.04f, k) * Mathf.Clamp(maxR / 6f, 0.6f, 2.5f); });
        }
        /// <summary>Молния (ломаная) от a к b. Для Чёрной вспышки — красная внешняя и чёрная внутренняя.</summary>
        public static void Bolt(Vector3 a, Vector3 b, Color col, float width, float dur, float jitter = 0.5f, int pts = 12)
        {
            var go = new GameObject("JJK_Bolt"); var lr = go.AddComponent<LineRenderer>(); lr.material = Gfx.Fade(null, Color.white, true); lr.useWorldSpace = true; lr.positionCount = pts; lr.numCornerVertices = 2; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Vector3 d = b - a; for (int i = 0; i < pts; i++) { float t = i / (float)(pts - 1); Vector3 p = a + d * t; if (i > 0 && i < pts - 1) p += UnityEngine.Random.insideUnitSphere * jitter * (1f - Mathf.Abs(t * 2f - 1f) * 0.5f); lr.SetPosition(i, p); }
            Add(go, dur, (k, g) => { var c = col; c.a = col.a * (1f - k); lr.startColor = c; lr.endColor = c; lr.startWidth = lr.endWidth = width * (1f - k * 0.7f); });
        }
        public static void Light(Vector3 pos, Color col, float intensity, float range, float dur)
        {
            var go = new GameObject("JJK_Light"); go.transform.position = pos; var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = col; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            Add(go, dur, (k, g) => { l.intensity = intensity * (1f - k) * (1f - k); });
        }
        /// <summary>Взрыв: сфера + кольцо + свет + искры + дым.</summary>
        public static void Explosion(Vector3 pos, float r, Color col, bool dark = false)
        {
            Orb(pos, r * 0.85f, new Color(col.r, col.g, col.b, 0.55f), 0.45f); Orb(pos, r * 0.5f, new Color(1f, 1f, 1f, 0.7f), 0.25f);
            Ring(pos + Vector3.up * 0.3f, Vector3.up, col, r * 1.4f, 0.55f, 0.6f); Light(pos + Vector3.up, col, 6f, r * 2.5f, 0.5f);
            Burst(pos, Mathf.Clamp((int)(r * 6f), 10, 60), r * 3f, 0.25f, col, 0.8f); if (dark) Burst(pos, 14, r, 1.2f, new Color(0.04f, 0.03f, 0.05f, 0.6f), 1.2f, false, 0.4f); else Burst(pos, 10, r * 0.8f, 1.4f, new Color(0.3f, 0.28f, 0.28f, 0.45f), 1.4f, false, 0.5f);
            Shake(Mathf.Clamp(r * 0.05f, 0.04f, 0.4f), 0.35f); Sfx.Boom(pos, Mathf.Clamp01(r / 10f));
        }

        // ---------------- камера: тряска, FOV, наклон ----------------
        static float shakeAmp, shakeUntil, shakeStart; public static float FovKick, FovTarget; static bool hooked; static Vector3 shakeOff; static Quaternion shakeRot = Quaternion.identity; static Camera hookCam; static Vector3 savedPos; static Quaternion savedRot; static float savedFov; static bool applied;
        public static void Shake(float amp, float dur) { if (!Plugin.CfgShake) return; if (amp >= shakeAmp * (shakeUntil > Time.time ? 0.6f : 0f)) { shakeAmp = amp; shakeStart = Time.time; shakeUntil = Time.time + dur; } }
        static void TickShake()
        {
            if (Time.time < shakeUntil) { float k = (shakeUntil - Time.time) / Mathf.Max(0.01f, shakeUntil - shakeStart); float a = shakeAmp * k * k; shakeOff = UnityEngine.Random.insideUnitSphere * a; shakeRot = Quaternion.Euler(UnityEngine.Random.Range(-1f, 1f) * a * 8f, UnityEngine.Random.Range(-1f, 1f) * a * 8f, UnityEngine.Random.Range(-1f, 1f) * a * 14f); }
            else { shakeOff = Vector3.zero; shakeRot = Quaternion.identity; }
            FovKick = Mathf.Lerp(FovKick, FovTarget, 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime)); FovTarget = Mathf.Lerp(FovTarget, 0f, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
        }
        public static string HookInfo { get { return hooked ? "хуки камеры установлены" : "хуки камеры не установлены"; } }
        public static void HookCamera()
        {
            if (hooked) return; hooked = true;
            try { Camera.onPreCull += c => ApplyCam(c); Camera.onPostRender += c => RestoreCam(c); } catch { }
            try { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += (ctx, c) => ApplyCam(c); UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += (ctx, c) => RestoreCam(c); } catch { }
        }
        static void ApplyCam(Camera c)
        {
            if (c != Camera.main || applied) return; if (shakeOff == Vector3.zero && Mathf.Abs(FovKick) < 0.01f && shakeRot == Quaternion.identity) return;
            applied = true; hookCam = c; savedPos = c.transform.position; savedRot = c.transform.rotation; savedFov = c.fieldOfView;
            c.transform.position = savedPos + c.transform.TransformVector(shakeOff); c.transform.rotation = savedRot * shakeRot; c.fieldOfView = Mathf.Clamp(savedFov + FovKick, 20f, 120f);
        }
        static void RestoreCam(Camera c) { if (!applied || c != hookCam) return; applied = false; if (c == null) return; c.transform.position = savedPos; c.transform.rotation = savedRot; c.fieldOfView = savedFov; }

        // ---------------- hit-stop ----------------
        static float stopUntil; static bool stopped;
        public static void HitStop(float dur, float scale = 0.04f) { if (!Plugin.CfgHitStop || Time.timeScale < 0.5f && !stopped) return; stopUntil = Mathf.Max(stopUntil, Time.unscaledTime + dur); Time.timeScale = scale; stopped = true; }
        static void TickHitStop() { if (stopped && Time.unscaledTime >= stopUntil) ResetTime(); }
        public static void Slow(float scale, float dur) { if (Time.timeScale < 0.5f && !stopped) return; stopUntil = Mathf.Max(stopUntil, Time.unscaledTime + dur); Time.timeScale = scale; stopped = true; }
        public static void ResetTime() { if (stopped) { Time.timeScale = 1f; stopped = false; } }

        // ---------------- экранные вспышки ----------------
        static Color flashCol; static float flashA, flashDecay;
        public static void Flash(Color c, float alpha, float decay = 3f) { if (!Plugin.CfgFlash) return; flashCol = c; flashA = Mathf.Max(flashA * 0.5f, alpha); flashDecay = decay; }
        public static void DrawOverlay()
        {
            if (flashA <= 0.003f || Event.current.type != EventType.Repaint) { if (Event.current.type == EventType.Repaint) flashA = 0f; return; }
            var old = GUI.color; GUI.color = new Color(flashCol.r, flashCol.g, flashCol.b, flashA); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = old; flashA = Mathf.MoveTowards(flashA, 0f, flashDecay * Time.unscaledDeltaTime * Mathf.Max(0.3f, flashA));
        }

        // ---------------- всплывающие числа ----------------
        sealed class Popup { public Vector3 pos; public string text; public Color col; public float t0, dur, size; }
        static readonly List<Popup> popups = new List<Popup>(); static GUIStyle pst;
        public static void Popup_(Vector3 pos, string text, Color col, float size = 1f) { if (popups.Count > 40) popups.RemoveAt(0); popups.Add(new Popup { pos = pos + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), 0f, UnityEngine.Random.Range(-0.4f, 0.4f)), text = text, col = col, t0 = Time.time, dur = 1.1f, size = size }); }
        static void TickPopups() { for (int i = popups.Count - 1; i >= 0; i--) if (Time.time - popups[i].t0 > popups[i].dur) popups.RemoveAt(i); }
        public static void DrawPopups()
        {
            if (Event.current.type != EventType.Repaint) return; var cam = Camera.main; if (cam == null) return;
            if (pst == null) { pst = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = false }; }
            foreach (var p in popups)
            {
                float k = (Time.time - p.t0) / p.dur; Vector3 sp = cam.WorldToScreenPoint(p.pos + Vector3.up * (0.5f + k * 1.6f)); if (sp.z <= 0.2f) continue; float y = Screen.height - sp.y;
                int fs = (int)((16f + 14f * p.size) * Mathf.Clamp01(Screen.height / 1080f) * (k < 0.12f ? 0.6f + k * 3.3f : 1f)); pst.fontSize = Mathf.Max(10, fs);
                var rc = new Rect(sp.x - 100, y - 20, 200, 40); GUI.color = new Color(0, 0, 0, (1f - k) * 0.8f); GUI.Label(new Rect(rc.x + 1.5f, rc.y + 1.5f, rc.width, rc.height), p.text, pst);
                GUI.color = new Color(p.col.r, p.col.g, p.col.b, 1f - k * k); GUI.Label(rc, p.text, pst);
            }
            GUI.color = Color.white;
        }
    }

    // ---------------- звуки (генерируются в коде) ----------------
    internal static class Sfx
    {
        static readonly System.Random rn = new System.Random(5); static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(); static AudioSource ui;
        static float Noise() { return (float)rn.NextDouble() * 2f - 1f; }
        static AudioClip Make(string name, float len, Func<float, float, float> f)
        {
            AudioClip c; if (clips.TryGetValue(name, out c) && c != null) return c;
            int rate = 22050, n = (int)(rate * len); var d = new float[n]; for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)rate, i / (float)n), -1f, 1f);
            c = AudioClip.Create(name, n, 1, rate, false); c.SetData(d, 0); clips[name] = c; return c;
        }
        static void Play(AudioClip c, Vector3? pos, float vol, float pitch = 1f)
        {
            if (!Plugin.CfgSound || c == null) return; vol *= Plugin.CfgVolume;
            try
            {
                if (pos.HasValue) { var go = new GameObject("JJK_snd"); go.transform.position = pos.Value; var a = go.AddComponent<AudioSource>(); a.clip = c; a.volume = Mathf.Clamp01(vol); a.pitch = pitch; a.spatialBlend = 0.7f; a.minDistance = 8f; a.maxDistance = 90f; a.Play(); UnityEngine.Object.Destroy(go, c.length / Mathf.Max(0.3f, pitch) + 0.2f); }
                else { if (ui == null) { var go = new GameObject("JJK_ui"); UnityEngine.Object.DontDestroyOnLoad(go); ui = go.AddComponent<AudioSource>(); ui.spatialBlend = 0f; } ui.pitch = pitch; ui.PlayOneShot(c, Mathf.Clamp01(vol)); }
            }
            catch { }
        }
        public static void Whoosh(Vector3? p = null, float v = 0.5f) { Play(Make("whoosh", 0.45f, (t, k) => Noise() * Mathf.Sin(k * Mathf.PI) * 0.6f * (0.3f + k)), p, v, UnityEngine.Random.Range(0.9f, 1.15f)); }
        public static void Boom(Vector3? p = null, float v = 0.6f) { Play(Make("boom", 0.9f, (t, k) => (Mathf.Sin(t * Mathf.PI * 2f * (70f - 45f * k)) * 0.8f + Noise() * 0.5f * (1f - k)) * Mathf.Pow(1f - k, 1.6f)), p, v, UnityEngine.Random.Range(0.85f, 1.05f)); }
        public static void Zap(Vector3? p = null, float v = 0.5f) { Play(Make("zap", 0.35f, (t, k) => (Noise() * 0.5f + Mathf.Sign(Mathf.Sin(t * 2400f * (1f + k))) * 0.3f) * (1f - k)), p, v, UnityEngine.Random.Range(0.9f, 1.2f)); }
        public static void Slash(Vector3? p = null, float v = 0.5f) { Play(Make("slash", 0.3f, (t, k) => Noise() * 0.7f * Mathf.Pow(1f - k, 2f) * (k < 0.05f ? k * 20f : 1f) + Mathf.Sin(t * 3000f * (1f - k)) * 0.15f * (1f - k)), p, v, UnityEngine.Random.Range(0.9f, 1.2f)); }
        public static void Hit(Vector3? p = null, float v = 0.5f) { Play(Make("hit", 0.22f, (t, k) => (Mathf.Sin(t * 2f * Mathf.PI * (180f - 120f * k)) * 0.8f + Noise() * 0.4f) * Mathf.Pow(1f - k, 2.2f)), p, v, UnityEngine.Random.Range(0.85f, 1.2f)); }
        public static void Thud(Vector3? p = null, float v = 0.7f) { Play(Make("thud", 0.5f, (t, k) => Mathf.Sin(t * 2f * Mathf.PI * (55f - 30f * k)) * Mathf.Pow(1f - k, 2f)), p, v, 1f); }
        public static void Clap(Vector3? p = null, float v = 0.8f) { Play(Make("clap", 0.35f, (t, k) => Noise() * Mathf.Pow(1f - k, 4f) * (k < 0.02f ? k * 50f : 1f) + (t > 0.08f && t < 0.2f ? Noise() * 0.25f * (1f - (t - 0.08f) / 0.12f) : 0f)), p, v, 1f); }
        public static void Flash(Vector3? p = null, float v = 0.9f) { Play(Make("bflash", 0.8f, (t, k) => (Mathf.Sin(t * 2f * Mathf.PI * (48f - 20f * k)) * 0.9f + Noise() * 0.7f * Mathf.Pow(1f - k, 6f) + Mathf.Sin(t * 5200f) * 0.2f * Mathf.Pow(1f - k, 8f)) * Mathf.Pow(1f - k, 1.4f)), p, v, 1f); }
        public static void Charge(Vector3? p = null, float v = 0.5f) { Play(Make("charge", 1.2f, (t, k) => Mathf.Sin(t * 2f * Mathf.PI * (180f + 900f * k * k)) * 0.35f * k + Noise() * 0.12f * k), p, v, 1f); }
        public static void Domain(float v = 0.9f) { Play(Make("domain", 2.2f, (t, k) => (Mathf.Sin(t * 2f * Mathf.PI * 46f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * (220f - 150f * k)) * 0.25f + Noise() * 0.2f * Mathf.Pow(1f - k, 3f)) * Mathf.Sin(Mathf.Min(1f, k * 6f) * Mathf.PI * 0.5f) * Mathf.Pow(1f - k, 1.1f)), null, v, 1f); }
        public static void Hurt(float v = 0.6f) { Play(Make("hurt", 0.35f, (t, k) => (Mathf.Sin(t * 2f * Mathf.PI * (130f - 60f * k)) * 0.7f + Noise() * 0.5f) * Mathf.Pow(1f - k, 2f)), null, v, UnityEngine.Random.Range(0.9f, 1.1f)); }
        public static void Heal(float v = 0.4f) { Play(Make("heal", 0.7f, (t, k) => Mathf.Sin(t * 2f * Mathf.PI * (520f + 400f * k)) * 0.25f * Mathf.Sin(k * Mathf.PI)), null, v, 1f); }
        public static void Ding(float v = 0.4f, float pitch = 1f) { Play(Make("ding", 0.6f, (t, k) => Mathf.Sin(t * 2f * Mathf.PI * 880f) * 0.3f * Mathf.Pow(1f - k, 2.5f) + Mathf.Sin(t * 2f * Mathf.PI * 1320f) * 0.15f * Mathf.Pow(1f - k, 3f)), null, v, pitch); }
        public static void Roar(Vector3? p = null, float v = 0.7f) { Play(Make("roar", 1.1f, (t, k) => (Mathf.Sin(t * 2f * Mathf.PI * (75f + 30f * Mathf.Sin(t * 14f))) * 0.6f + Noise() * 0.45f) * Mathf.Sin(k * Mathf.PI) * 0.9f), p, v, UnityEngine.Random.Range(0.8f, 1.0f)); }
        public static void Howl(Vector3? p = null, float v = 0.6f) { Play(Make("howl", 1.0f, (t, k) => Mathf.Sin(t * 2f * Mathf.PI * (320f + 180f * Mathf.Sin(k * Mathf.PI))) * 0.4f * Mathf.Sin(k * Mathf.PI) + Noise() * 0.1f * Mathf.Sin(k * Mathf.PI)), p, v, UnityEngine.Random.Range(0.85f, 1.15f)); }
        public static void Screech(Vector3? p = null, float v = 0.5f) { Play(Make("screech", 0.5f, (t, k) => (Mathf.Sin(t * 2f * Mathf.PI * (900f + 500f * k)) * 0.3f + Noise() * 0.3f) * Mathf.Sin(k * Mathf.PI)), p, v, UnityEngine.Random.Range(0.85f, 1.3f)); }
    }
}
