// Костюмы героев 1.9 — целиком рисуются кодом: костюм из эллипсоидов по частям тела, плащ с физикой (верле), герб, причёска, маска,
// светящиеся глаза. Настоящих 3D-моделей у нас нет, поэтому это «по мотивам». Игроки с PeakFun видят костюм друг на друге.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    // ---------- карта частей тела (по именам из ragdoll.partList) ----------
    internal sealed class PartMap
    {
        public Rigidbody hip, torso, head, mid, armL, armR, elbL, elbR, handL, handR, legL, legR, kneeL, kneeR, footL, footR;
        public string names = ""; public bool limbs;
        static readonly Dictionary<int, PartMap> cache = new Dictionary<int, PartMap>(); static bool logged;

        public static PartMap Of(Component c)
        {
            if (c == null) return null;
            int id = c.GetInstanceID(); PartMap m;
            if (cache.TryGetValue(id, out m) && (Time.frameCount % 600 != 0 || m.limbs)) return m;
            m = new PartMap(); cache[id] = m;
            try
            {
                var parts = R.Get(Game.Ragdoll(c), "partList") as System.Collections.IEnumerable;
                if (parts == null) return m;
                var sb = new System.Text.StringBuilder();
                foreach (var p in parts)
                {
                    var rb = R.Get(p, "Rig") as Rigidbody; if (rb == null) continue;
                    var pt = R.Get(p, "partType");
                    string n = (pt != null ? pt.ToString() : (p as Component) != null ? ((Component)p).name : rb.name);
                    sb.Append(n).Append(' ');
                    m.Put(n.ToLowerInvariant().Replace("_", "").Replace(" ", "").Replace(".", ""), rb);
                }
                m.names = sb.ToString();
                m.limbs = m.armL && m.armR && m.handL && m.handR && m.legL && m.legR && m.footL && m.footR && m.hip && m.head;
                if (!logged) { logged = true; Debug.Log("[PeakFun] части тела: " + m.names + " | костюм по частям: " + (m.limbs ? "да" : "нет")); }
            }
            catch (Exception e) { Debug.LogWarning("[PeakFun] части тела: " + e.Message); }
            return m;
        }

        void Put(string n, Rigidbody rb)
        {
            bool left = n.Contains("left") || n.EndsWith("l"), right = n.Contains("right") || n.EndsWith("r");
            if (n.Contains("head")) head = rb;
            else if (n.Contains("torso") || n.Contains("chest") || n.Contains("spine")) torso = rb;
            else if (n.Contains("hip") || n.Contains("pelvis")) hip = rb;
            else if (n.Contains("mid") || n.Contains("waist")) mid = rb;
            else if (n.Contains("hand")) { if (left) handL = rb; else if (right) handR = rb; }
            else if (n.Contains("foot")) { if (left) footL = rb; else if (right) footR = rb; }
            else if (n.Contains("elbow") || n.Contains("forearm")) { if (left) elbL = rb; else if (right) elbR = rb; }
            else if (n.Contains("knee") || n.Contains("calf") || n.Contains("shin")) { if (left) kneeL = rb; else if (right) kneeR = rb; }
            else if (n.Contains("arm")) { if (left) armL = rb; else if (right) armR = rb; }
            else if (n.Contains("leg") || n.Contains("thigh")) { if (left) legL = rb; else if (right) legR = rb; }
        }
    }

    internal sealed class Spec
    {
        public Color suit, accent, glove, boot, hair, eyes, capeCol; public bool mask, hasHair, capeFlag, eyesAlways, hasCape; public int emblem; public bool shorts; public Color trim; public bool belt, collar, shoulders; public float capeLen = 1.15f; public int variant;
        public bool blindfold, spikes, extraArms, extraEyes, markings; public Color mark;
    }

    internal sealed class CostumeInst
    {
        public Component ch; public HeroKind kind; public GameObject root; public bool local;
        Spec spec; PartMap pm;
        sealed class Seg { public Rigidbody a, b; public float r; public Transform t; public bool ball; public Vector3 off; }
        readonly List<Seg> segs = new List<Seg>();
        Transform signP, signA, signB, hairT, maskT, lensL, lensR, eyeL, eyeR, eyeL2, eyeR2, blindT, emblemT, capeT; readonly List<Transform> spikeT = new List<Transform>(); readonly List<Vector3> spikeDir = new List<Vector3>(); Mesh capeMesh; MeshRenderer capeMr;
        Vector3[] cp, cprev, cv; int CW = 8, CH = 12; bool capeInit;
        public bool Alive { get { return ch != null && root != null; } }

        public static Spec SpecFor(HeroKind k, int variant = 0)
        {
            var s = new Spec { eyes = new Color(1f, 0.1f, 0.05f), hasHair = false, variant = variant };
            s.trim = new Color(0.9f, 0.75f, 0.2f);
            switch (k)
            {
                case HeroKind.Patriot:
                    s.suit = new Color(0.07f, 0.13f, 0.42f); s.accent = new Color(0.8f, 0.08f, 0.1f); s.glove = new Color(0.8f, 0.08f, 0.1f); s.boot = new Color(0.8f, 0.08f, 0.1f);
                    s.hasHair = true; s.hair = new Color(0.95f, 0.8f, 0.3f); s.hasCape = true; s.capeFlag = true; s.emblem = 1; s.belt = true; s.collar = true; s.shoulders = true; s.capeLen = 1.35f;
                    if (variant == 1)    // «чёрный» костюм
                    { s.suit = new Color(0.045f, 0.045f, 0.06f); s.accent = new Color(0.85f, 0.7f, 0.15f); s.glove = new Color(0.1f, 0.1f, 0.12f); s.boot = new Color(0.08f, 0.08f, 0.1f); s.capeFlag = false; s.capeCol = new Color(0.1f, 0.02f, 0.03f, 1f); s.trim = new Color(0.85f, 0.7f, 0.15f); }
                    break;
                case HeroKind.Invincible:
                    s.suit = new Color(0.1f, 0.3f, 0.85f); s.accent = new Color(1f, 0.85f, 0.1f); s.glove = new Color(1f, 0.85f, 0.1f); s.boot = new Color(1f, 0.85f, 0.1f);
                    s.hasHair = true; s.hair = new Color(0.05f, 0.05f, 0.07f); s.mask = true; s.emblem = 3; s.belt = true; s.shoulders = true; s.trim = new Color(1f, 0.85f, 0.1f);
                    if (variant == 1)    // чёрно-красный костюм
                    { s.suit = new Color(0.05f, 0.05f, 0.07f); s.accent = new Color(0.85f, 0.1f, 0.1f); s.glove = new Color(0.85f, 0.1f, 0.1f); s.boot = new Color(0.85f, 0.1f, 0.1f); s.trim = new Color(0.95f, 0.95f, 0.95f); }
                    break;
                case HeroKind.Flash:
                    s.suit = new Color(0.85f, 0.08f, 0.08f); s.accent = new Color(1f, 0.85f, 0.1f); s.glove = new Color(1f, 0.85f, 0.1f); s.boot = new Color(1f, 0.85f, 0.1f); s.emblem = 2; s.eyes = new Color(1f, 0.95f, 0.3f); s.belt = true; break;
                case HeroKind.Brute:
                    s.suit = new Color(0.25f, 0.65f, 0.2f); s.accent = new Color(0.45f, 0.2f, 0.55f); s.glove = new Color(0.25f, 0.65f, 0.2f); s.boot = new Color(0.25f, 0.65f, 0.2f); s.shorts = true; s.eyes = new Color(0.7f, 1f, 0.3f); s.shoulders = true; break;
                case HeroKind.Phantom:
                    s.suit = new Color(0.08f, 0.08f, 0.12f); s.accent = new Color(0.1f, 0.9f, 0.9f); s.glove = new Color(0.08f, 0.08f, 0.12f); s.boot = new Color(0.08f, 0.08f, 0.12f);
                    s.hasCape = true; s.capeCol = new Color(0.06f, 0.06f, 0.1f, 0.85f); s.eyesAlways = true; s.eyes = new Color(0.1f, 1f, 1f); s.collar = true; s.trim = new Color(0.1f, 0.9f, 0.9f); break;
                case HeroKind.Gojo:
                    s.suit = new Color(0.04f, 0.05f, 0.1f); s.accent = new Color(0.12f, 0.25f, 0.6f); s.glove = new Color(0.04f, 0.05f, 0.1f); s.boot = new Color(0.03f, 0.03f, 0.06f);
                    s.hasHair = true; s.hair = new Color(0.96f, 0.97f, 1f); s.spikes = true; s.blindfold = true; s.collar = true; s.trim = new Color(0.12f, 0.25f, 0.6f);
                    s.eyes = new Color(0.45f, 0.85f, 1f); s.belt = false; break;
                case HeroKind.Sukuna:
                    s.suit = new Color(0.93f, 0.72f, 0.6f); s.accent = new Color(0.88f, 0.88f, 0.84f); s.glove = new Color(0.93f, 0.72f, 0.6f); s.boot = new Color(0.93f, 0.72f, 0.6f); s.shorts = true;
                    s.hasHair = true; s.hair = new Color(0.98f, 0.55f, 0.68f); s.spikes = true; s.extraArms = true; s.extraEyes = true; s.markings = true; s.mark = new Color(0.05f, 0.03f, 0.04f);
                    s.eyes = new Color(1f, 0.1f, 0.08f); s.eyesAlways = true; s.belt = true; s.trim = new Color(0.55f, 0.1f, 0.12f); break;
                case HeroKind.Kagami:
                    s.suit = new Color(0.93f, 0.93f, 0.95f); s.accent = new Color(0.85f, 0.1f, 0.12f); s.glove = new Color(0.9f, 0.7f, 0.6f); s.boot = new Color(0.85f, 0.1f, 0.12f); s.shorts = true; s.trim = new Color(0.85f, 0.1f, 0.12f);
                    s.hasHair = true; s.hair = new Color(0.75f, 0.1f, 0.08f); s.spikes = true; s.eyes = new Color(1f, 0.2f, 0.1f); s.belt = false; break;
                case HeroKind.Aomine:
                    s.suit = new Color(0.1f, 0.14f, 0.5f); s.accent = new Color(0.1f, 0.14f, 0.5f); s.glove = new Color(0.62f, 0.42f, 0.3f); s.boot = new Color(0.05f, 0.05f, 0.1f); s.shorts = true; s.trim = new Color(0.95f, 0.95f, 1f);
                    s.hasHair = true; s.hair = new Color(0.05f, 0.08f, 0.3f); s.spikes = true; s.eyes = new Color(0.4f, 0.65f, 1f); break;
                case HeroKind.Kise:
                    s.suit = new Color(0.95f, 0.85f, 0.25f); s.accent = new Color(0.1f, 0.15f, 0.45f); s.glove = new Color(0.95f, 0.78f, 0.65f); s.boot = new Color(0.95f, 0.95f, 1f); s.shorts = true; s.trim = new Color(0.1f, 0.15f, 0.45f);
                    s.hasHair = true; s.hair = new Color(1f, 0.85f, 0.2f); s.spikes = true; s.eyes = new Color(1f, 0.9f, 0.3f); break;
                case HeroKind.Midorima:
                    s.suit = new Color(0.12f, 0.5f, 0.3f); s.accent = new Color(0.95f, 0.95f, 1f); s.glove = new Color(0.95f, 0.8f, 0.7f); s.boot = new Color(0.95f, 0.95f, 1f); s.shorts = true; s.trim = new Color(0.95f, 0.95f, 1f);
                    s.hasHair = true; s.hair = new Color(0.2f, 0.7f, 0.35f); s.mask = true; s.eyes = new Color(0.3f, 1f, 0.5f); break;
                case HeroKind.Kuroko:
                    s.suit = new Color(0.93f, 0.95f, 0.98f); s.accent = new Color(0.4f, 0.65f, 0.95f); s.glove = new Color(0.95f, 0.82f, 0.72f); s.boot = new Color(0.4f, 0.65f, 0.95f); s.shorts = true; s.trim = new Color(0.4f, 0.65f, 0.95f);
                    s.hasHair = true; s.hair = new Color(0.55f, 0.8f, 1f); s.eyes = new Color(0.55f, 0.9f, 1f); break;
                case HeroKind.Akashi:
                    s.suit = new Color(0.8f, 0.1f, 0.15f); s.accent = new Color(0.95f, 0.95f, 0.98f); s.glove = new Color(0.95f, 0.8f, 0.7f); s.boot = new Color(0.9f, 0.9f, 0.95f); s.shorts = true; s.trim = new Color(0.95f, 0.95f, 0.98f);
                    s.hasHair = true; s.hair = new Color(0.85f, 0.1f, 0.15f); s.eyes = new Color(1f, 0.2f, 0.1f); s.eyesAlways = true; break;
                case HeroKind.Murasakibara:
                    s.suit = new Color(0.4f, 0.2f, 0.65f); s.accent = new Color(0.12f, 0.08f, 0.2f); s.glove = new Color(0.95f, 0.8f, 0.7f); s.boot = new Color(0.12f, 0.08f, 0.2f); s.shorts = true; s.trim = new Color(0.95f, 0.85f, 0.3f);
                    s.hasHair = true; s.hair = new Color(0.55f, 0.35f, 0.8f); s.shoulders = true; s.eyes = new Color(0.8f, 0.5f, 1f); break;
                case HeroKind.Mind:
                    s.suit = new Color(0.45f, 0.2f, 0.75f); s.accent = new Color(0.95f, 0.95f, 1f); s.glove = new Color(0.95f, 0.95f, 1f); s.boot = new Color(0.95f, 0.95f, 1f);
                    s.hasCape = true; s.capeCol = new Color(0.95f, 0.95f, 1f, 1f); s.eyes = new Color(0.8f, 0.4f, 1f); s.collar = true; s.trim = new Color(0.95f, 0.95f, 1f); break;
            }
            return s;
        }

        sealed class Part { public Rigidbody a; public Transform t; public Vector3 off, scale; }
        readonly List<Part> parts = new List<Part>();
        public int variant;

        void AddPart(Rigidbody a, Color col, Vector3 off, Vector3 scale, Texture tex = null)
        {
            if (a == null) return;
            var g = Prim("part", Vfx.Sphere(), col, tex);
            parts.Add(new Part { a = a, t = g.transform, off = off, scale = scale });
        }

        GameObject Prim(string name, Mesh mesh, Color col, Texture tex = null)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Vfx.Mat(tex, col);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            return go;
        }

        public CostumeInst(Component c, HeroKind k, bool isLocal, int var = 0)
        {
            ch = c; kind = k; local = isLocal; variant = var; spec = SpecFor(k, var); pm = PartMap.Of(c);
            root = new GameObject("PF_Costume_" + k); UnityEngine.Object.DontDestroyOnLoad(root);
            if (Vfx.Sh() == null) return;
            if (pm != null && pm.limbs)
            {
                Rigidbody torsoTop = pm.torso != null ? pm.torso : pm.mid;
                var shortsCol = spec.shorts ? spec.accent : spec.suit;
                Add(pm.hip, torsoTop ?? pm.head, 0.23f, spec.suit, false);
                Add(pm.armL, pm.elbL ?? pm.handL, 0.098f, spec.suit, false); if (pm.elbL != null) Add(pm.elbL, pm.handL, 0.08f, spec.suit, false);
                Add(pm.armR, pm.elbR ?? pm.handR, 0.098f, spec.suit, false); if (pm.elbR != null) Add(pm.elbR, pm.handR, 0.08f, spec.suit, false);
                Add(pm.legL, pm.kneeL ?? pm.footL, 0.105f, shortsCol, false); if (pm.kneeL != null) Add(pm.kneeL, pm.footL, 0.095f, spec.shorts ? spec.suit : spec.suit, false);
                Add(pm.legR, pm.kneeR ?? pm.footR, 0.105f, shortsCol, false); if (pm.kneeR != null) Add(pm.kneeR, pm.footR, 0.095f, spec.shorts ? spec.suit : spec.suit, false);
                Add(pm.handL, null, 0.11f, spec.glove, true); Add(pm.handR, null, 0.11f, spec.glove, true);
                Add(pm.footL, null, 0.13f, spec.boot, true); Add(pm.footR, null, 0.13f, spec.boot, true);
                var tex = (Texture)Vfx.SuitTex(); var chest = pm.torso ?? pm.mid ?? pm.hip;
                AddPart(chest, spec.suit, new Vector3(-0.1f, 0.06f, 0.13f), new Vector3(0.21f, 0.18f, 0.14f), tex);
                AddPart(chest, spec.suit, new Vector3(0.1f, 0.06f, 0.13f), new Vector3(0.21f, 0.18f, 0.14f), tex);
                AddPart(pm.hip, spec.suit, new Vector3(0f, 0.04f, 0.05f), new Vector3(0.44f, 0.22f, 0.34f), tex);
                if (spec.belt) { AddPart(pm.hip, spec.trim, new Vector3(0f, 0.13f, 0.01f), new Vector3(0.5f, 0.075f, 0.4f)); AddPart(pm.hip, spec.accent, new Vector3(0f, 0.13f, 0.2f), new Vector3(0.1f, 0.08f, 0.04f)); }
                if (spec.collar) AddPart(pm.head, spec.suit == spec.trim ? spec.accent : spec.trim, new Vector3(0f, -0.27f, -0.01f), new Vector3(0.37f, 0.1f, 0.31f));
                if (spec.shoulders) { AddPart(pm.armL, spec.suit, Vector3.zero, new Vector3(0.21f, 0.21f, 0.21f), tex); AddPart(pm.armR, spec.suit, Vector3.zero, new Vector3(0.21f, 0.21f, 0.21f), tex); AddPart(pm.armL, spec.trim, new Vector3(-0.02f, 0.06f, 0f), new Vector3(0.12f, 0.06f, 0.2f)); AddPart(pm.armR, spec.trim, new Vector3(0.02f, 0.06f, 0f), new Vector3(0.12f, 0.06f, 0.2f)); }
                AddPart(pm.footL, spec.boot, new Vector3(0f, 0.12f, 0f), new Vector3(0.24f, 0.3f, 0.26f)); AddPart(pm.footR, spec.boot, new Vector3(0f, 0.12f, 0f), new Vector3(0.24f, 0.3f, 0.26f));
            }
            if (spec.hasHair) hairT = Prim("hair", Vfx.Sphere(), spec.hair).transform;
            if (spec.spikes)
                for (int i = 0; i < 11; i++)
                {
                    float a = i / 11f * Mathf.PI * 2f; bool top = i % 2 == 0;
                    spikeDir.Add(new Vector3(Mathf.Cos(a) * (top ? 0.55f : 0.9f), top ? 1f : 0.55f, Mathf.Sin(a) * (top ? 0.55f : 0.9f)).normalized);
                    spikeT.Add(Prim("spike", Vfx.Sphere(), spec.hair).transform);
                }
            if (spec.blindfold) blindT = Prim("blind", Vfx.Sphere(), new Color(0.03f, 0.03f, 0.05f)).transform;
            if (spec.extraArms && pm != null && pm.limbs)
            {
                Vector3 o1 = new Vector3(-0.0f, -0.2f, -0.07f);
                Add(pm.armL, pm.elbL ?? pm.handL, 0.085f, spec.suit, false, o1); if (pm.elbL != null) Add(pm.elbL, pm.handL, 0.07f, spec.suit, false, o1); Add(pm.handL, null, 0.095f, spec.glove, true, o1);
                Add(pm.armR, pm.elbR ?? pm.handR, 0.085f, spec.suit, false, o1); if (pm.elbR != null) Add(pm.elbR, pm.handR, 0.07f, spec.suit, false, o1); Add(pm.handR, null, 0.095f, spec.glove, true, o1);
            }
            if (spec.markings && pm != null && pm.limbs)
            {
                var chest = pm.torso ?? pm.mid ?? pm.hip;
                for (int i = 0; i < 3; i++)
                {
                    AddPart(chest, spec.mark, new Vector3(-0.1f, 0.12f - 0.06f * i, 0.205f), new Vector3(0.17f, 0.022f, 0.05f));
                    AddPart(chest, spec.mark, new Vector3(0.1f, 0.12f - 0.06f * i, 0.205f), new Vector3(0.17f, 0.022f, 0.05f));
                }
                AddPart(pm.armL, spec.mark, Vector3.zero, new Vector3(0.2f, 0.03f, 0.2f)); AddPart(pm.armR, spec.mark, Vector3.zero, new Vector3(0.2f, 0.03f, 0.2f));
                if (pm.elbL != null) AddPart(pm.elbL, spec.mark, Vector3.zero, new Vector3(0.17f, 0.03f, 0.17f)); if (pm.elbR != null) AddPart(pm.elbR, spec.mark, Vector3.zero, new Vector3(0.17f, 0.03f, 0.17f));
                AddPart(pm.head, spec.mark, new Vector3(-0.065f, -0.035f, 0.18f), new Vector3(0.07f, 0.012f, 0.03f)); AddPart(pm.head, spec.mark, new Vector3(0.065f, -0.035f, 0.18f), new Vector3(0.07f, 0.012f, 0.03f));
                AddPart(pm.head, spec.mark, new Vector3(0f, 0.2f, 0.12f), new Vector3(0.05f, 0.02f, 0.05f));
            }
            if (spec.mask)
            {
                maskT = Prim("mask", Vfx.Sphere(), new Color(0.03f, 0.03f, 0.05f)).transform;
                lensL = Prim("lensL", Vfx.Sphere(), Color.white).transform; lensR = Prim("lensR", Vfx.Sphere(), Color.white).transform;
            }
            eyeL = Prim("eyeL", Vfx.Sphere(), spec.eyes).transform; eyeR = Prim("eyeR", Vfx.Sphere(), spec.eyes).transform;
            if (spec.extraEyes) { eyeL2 = Prim("eyeL2", Vfx.Sphere(), spec.eyes).transform; eyeR2 = Prim("eyeR2", Vfx.Sphere(), spec.eyes).transform; }
            if (spec.emblem != 0) emblemT = Prim("emblem", Vfx.Quad(), Color.white, spec.emblem == 1 ? (Texture)Vfx.Eagle() : spec.emblem == 3 ? (Texture)Vfx.Swoosh() : Vfx.Bolt()).transform;
            if (spec.hasCape) BuildCape();
        }

        void Add(Rigidbody a, Rigidbody b, float r, Color col, bool ball, Vector3 off = default(Vector3))
        {
            if (a == null) return;
            var g = Prim("seg", Vfx.Sphere(), col, ball ? null : (Texture)Vfx.SuitTex());
            segs.Add(new Seg { a = a, b = b, r = r, t = g.transform, ball = ball, off = off });
        }

        void BuildCape()
        {
            cp = new Vector3[CW * CH]; cprev = new Vector3[CW * CH]; cv = new Vector3[CW * CH];
            capeMesh = new Mesh { name = "pf_cape" }; capeMesh.MarkDynamic();
            int n = CW * CH; var uv = new Vector2[n * 2]; var cols = new Color[n * 2];
            for (int j = 0; j < CH; j++) for (int i = 0; i < CW; i++)
            {
                int k = j * CW + i; var u = new Vector2(i / (float)(CW - 1), 1f - j / (float)(CH - 1));
                uv[k] = u; uv[k + n] = u; float sh = Mathf.Lerp(1f, 0.72f, j / (float)(CH - 1));
                cols[k] = new Color(sh, sh, sh, 1f); cols[k + n] = new Color(sh * 0.85f, sh * 0.85f, sh * 0.85f, 1f);
            }
            var tri = new List<int>();
            for (int j = 0; j < CH - 1; j++) for (int i = 0; i < CW - 1; i++)
            {
                int a = j * CW + i, b = a + 1, c = a + CW, d = c + 1;
                tri.Add(a); tri.Add(c); tri.Add(b); tri.Add(b); tri.Add(c); tri.Add(d);
                a += n; b += n; c += n; d += n;
                tri.Add(a); tri.Add(b); tri.Add(c); tri.Add(b); tri.Add(d); tri.Add(c);
            }
            capeMesh.vertices = new Vector3[n * 2]; capeMesh.uv = uv; capeMesh.colors = cols; capeMesh.SetTriangles(tri, 0);
            var go = new GameObject("cape"); go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = capeMesh;
            capeMr = go.AddComponent<MeshRenderer>();
            capeMr.sharedMaterial = spec.capeFlag ? Vfx.Mat(Vfx.Flag(), Color.white) : Vfx.Mat(null, spec.capeCol);
            capeMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; capeMr.receiveShadows = false;
            capeMr.allowOcclusionWhenDynamic = false; capeT = go.transform;
        }

        public void Destroy() { if (root != null) UnityEngine.Object.Destroy(root); root = null; }

        static Vector3 Flat(Vector3 v, Vector3 up) { v -= up * Vector3.Dot(v, up); return v.sqrMagnitude < 1e-4f ? Vector3.zero : v.normalized; }

        /// <summary>Система координат тела: вверх, вперёд, вправо.</summary>
        void Frame(out Vector3 up, out Vector3 fwd, out Vector3 right)
        {
            Vector3 head = PeakFunPlugin.HeadOf(ch), center = PeakFunPlugin.CenterOf(ch);
            up = head - center; up = up.sqrMagnitude < 0.01f ? Vector3.up : up.normalized;
            var data = R.Get(ch, "data");
            Vector3 look = R.Get<Vector3>(data, "lookDirection"); if (look.sqrMagnitude < 0.01f) look = R.Get<Vector3>(data, "lookDirection_Flat");
            if (look.sqrMagnitude < 0.01f) look = ch.transform.forward;
            fwd = Flat(look, up); if (fwd == Vector3.zero) fwd = Flat(ch.transform.forward, up); if (fwd == Vector3.zero) fwd = Vector3.forward;
            if (pm != null && pm.armL != null && pm.armR != null)
            {
                var r = pm.armR.position - pm.armL.position; r = Flat(r, up);
                if (r != Vector3.zero)
                {
                    var f2 = Vector3.Cross(r, up);
                    if (Vector3.Dot(f2, fwd) < 0f) { r = -r; f2 = -f2; }
                    if (Vector3.Dot(f2, fwd) > 0.25f) fwd = f2;
                }
            }
            right = Vector3.Cross(up, fwd);
        }

        public void Tick(float thick, float headScale, bool showHead)
        {
            if (!Alive) return;
            Vector3 up, fwd, right; Frame(out up, out fwd, out right);
            float sc = Mathf.Max(0.3f, ch.transform.lossyScale.y);
            foreach (var s in segs)
            {
                if (s.a == null) continue;
                Vector3 p0 = s.a.position; Vector3 so = (right * s.off.x + up * s.off.y + fwd * s.off.z) * sc; p0 += so;
                float r = s.r * thick * sc;
                if (s.ball) { s.t.position = p0; s.t.rotation = Quaternion.identity; s.t.localScale = new Vector3(r * 2f, r * 2f, r * 2.3f); s.t.rotation = Quaternion.LookRotation(fwd, up); continue; }
                Vector3 p1 = s.b != null ? s.b.position + so : p0 + up * 0.2f;
                Vector3 d = p1 - p0; float len = d.magnitude; if (len < 0.01f) continue;
                s.t.position = (p0 + p1) * 0.5f; s.t.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
                s.t.localScale = new Vector3(r * 2f, len + r * 1.3f, r * 2f);
            }
            foreach (var p in parts)
            {
                if (p.a == null) continue;
                p.t.position = p.a.position + (right * p.off.x + up * p.off.y + fwd * p.off.z) * sc * thick;
                p.t.rotation = Quaternion.LookRotation(fwd, up); p.t.localScale = p.scale * sc * thick;
            }
            Vector3 head = PeakFunPlugin.HeadOf(ch);
            float hs = headScale * sc;
            bool ph = showHead;
            if (hairT != null)
            {
                hairT.gameObject.SetActive(ph);
                hairT.position = head + up * 0.1f * hs - fwd * 0.015f * hs; hairT.rotation = Quaternion.LookRotation(fwd, up); hairT.localScale = new Vector3(0.43f, 0.25f, 0.44f) * hs;
            }
            if (maskT != null)
            {
                maskT.gameObject.SetActive(ph); lensL.gameObject.SetActive(ph); lensR.gameObject.SetActive(ph);
                Vector3 c = head + fwd * 0.17f * hs + up * 0.035f * hs; var q = Quaternion.LookRotation(fwd, up);
                maskT.position = c; maskT.rotation = q; maskT.localScale = new Vector3(0.42f, 0.115f, 0.15f) * hs;
                lensL.position = c - right * 0.075f * hs + fwd * 0.045f * hs; lensR.position = c + right * 0.075f * hs + fwd * 0.045f * hs;
                lensL.rotation = lensR.rotation = q; lensL.localScale = lensR.localScale = new Vector3(0.09f, 0.055f, 0.05f) * hs;
            }
            bool glow = spec.eyesAlways || PeakFunPlugin.EyesGlow(ch, kind);
            for (int i = 0; i < spikeT.Count; i++)
            {
                var st = spikeT[i]; st.gameObject.SetActive(ph);
                Vector3 dl = (right * spikeDir[i].x + up * spikeDir[i].y + fwd * spikeDir[i].z).normalized;
                float ln = (kind == HeroKind.Gojo ? 0.2f : 0.15f) * hs * (i % 2 == 0 ? 1f : 0.8f);
                st.position = head + up * 0.1f * hs + dl * 0.17f * hs; st.rotation = Quaternion.FromToRotation(Vector3.up, dl);
                st.localScale = new Vector3(0.07f, ln * 1.6f, 0.07f) * (hs > 0f ? 1f : 1f);
            }
            if (blindT != null)
            {
                bool bf = ph && !glow; blindT.gameObject.SetActive(bf);
                blindT.position = head + up * 0.035f * hs; blindT.rotation = Quaternion.LookRotation(fwd, up); blindT.localScale = new Vector3(0.405f, 0.105f, 0.41f) * hs;
            }
            if (eyeL2 != null)
            {
                eyeL2.gameObject.SetActive(glow && ph); eyeR2.gameObject.SetActive(glow && ph);
                if (glow && ph)
                {
                    Vector3 c2 = head + fwd * 0.18f * hs - up * 0.012f * hs;
                    eyeL2.position = c2 - right * 0.07f * hs; eyeR2.position = c2 + right * 0.07f * hs;
                    eyeL2.localScale = eyeR2.localScale = new Vector3(0.06f, 0.032f, 0.04f) * hs; eyeL2.rotation = eyeR2.rotation = Quaternion.LookRotation(fwd, up);
                }
            }
            eyeL.gameObject.SetActive(glow && ph); eyeR.gameObject.SetActive(glow && ph);
            if (glow && ph)
            {
                Vector3 c = head + fwd * 0.185f * hs + up * 0.035f * hs;
                eyeL.position = c - right * 0.065f * hs; eyeR.position = c + right * 0.065f * hs;
                eyeL.localScale = eyeR.localScale = new Vector3(0.075f, 0.04f, 0.04f) * hs; eyeL.rotation = eyeR.rotation = Quaternion.LookRotation(fwd, up);
            }
            if (kind == HeroKind.Gojo)
            {
                bool sg = ph && PeakFunPlugin.SignOn(ch);
                if (sg && signP == null)
                {
                    var skin = new Color(0.95f, 0.8f, 0.7f);
                    signP = Prim("sg_palm", Vfx.Sphere(), skin).transform; signA = Prim("sg_f1", Vfx.Sphere(), skin).transform; signB = Prim("sg_f2", Vfx.Sphere(), skin).transform;
                }
                if (signP != null)
                {
                    signP.gameObject.SetActive(sg); signA.gameObject.SetActive(sg); signB.gameObject.SetActive(sg);
                    if (sg)
                    {
                        Vector3 b = head + (fwd * 0.36f - up * 0.19f) * hs;
                        signP.position = b; signP.rotation = Quaternion.LookRotation(fwd, up); signP.localScale = new Vector3(0.1f, 0.12f, 0.06f) * hs;
                        Vector3 da = (up + right * 0.5f).normalized, db = (up - right * 0.5f).normalized;
                        signA.position = b + da * 0.1f * hs - fwd * 0.01f * hs; signA.rotation = Quaternion.FromToRotation(Vector3.up, da); signA.localScale = new Vector3(0.036f, 0.2f, 0.036f) * hs;
                        signB.position = b + db * 0.1f * hs + fwd * 0.01f * hs; signB.rotation = Quaternion.FromToRotation(Vector3.up, db); signB.localScale = new Vector3(0.036f, 0.2f, 0.036f) * hs;
                    }
                }
            }
            Vector3 neck = head - up * 0.3f * sc;
            Vector3 chest = pm != null && pm.torso != null ? pm.torso.position : (pm != null && pm.hip != null ? Vector3.Lerp(pm.hip.position, head, 0.55f) : PeakFunPlugin.CenterOf(ch));
            if (emblemT != null)
            {
                emblemT.position = chest + fwd * 0.245f * thick * sc + up * 0.03f * sc; emblemT.rotation = Quaternion.LookRotation(fwd, up);
                emblemT.localScale = Vector3.one * 0.3f * sc;
            }
            if (capeT != null) CapeTick(neck, up, fwd, right, sc, chest);
        }

        void CapeTick(Vector3 neck, Vector3 up, Vector3 fwd, Vector3 right, float sc, Vector3 chest)
        {
            float dt = Mathf.Min(Time.deltaTime, 0.033f); if (dt <= 0f) return;
            float wTop = 0.18f * sc, wBot = 0.36f * sc, len = spec.capeLen * sc, step = len / (CH - 1);
            Vector3 anchor = neck - fwd * 0.17f * sc;
            if (!capeInit)
            {
                for (int j = 0; j < CH; j++) for (int i = 0; i < CW; i++)
                {
                    float u = i / (float)(CW - 1) * 2f - 1f, w = Mathf.Lerp(wTop, wBot, j / (float)(CH - 1));
                    var p = anchor + right * u * w - up * step * j - fwd * 0.05f * j;
                    cp[j * CW + i] = cprev[j * CW + i] = p;
                }
                capeInit = true;
            }
            int sub = 2; float h = dt / sub;
            for (int s = 0; s < sub; s++)
            {
                for (int j = 0; j < CH; j++) for (int i = 0; i < CW; i++)
                {
                    int k = j * CW + i; float u = i / (float)(CW - 1) * 2f - 1f;
                    if (j == 0) { var p0 = anchor + right * u * wTop; cprev[k] = cp[k]; cp[k] = p0; continue; }
                    Vector3 v = (cp[k] - cprev[k]) * 0.965f;
                    cprev[k] = cp[k];
                    Vector3 acc = Vector3.down * 7.5f - fwd * 2.2f + up * 0f;
                    cp[k] += v + acc * h * h;
                }
                for (int it = 0; it < 4; it++)
                {
                    for (int j = 1; j < CH; j++) for (int i = 0; i < CW; i++)
                    {
                        int k = j * CW + i, up1 = k - CW; var d = cp[k] - cp[up1]; float l = d.magnitude; if (l < 1e-5f) continue;
                        var corr = d * ((l - step) / l);
                        if (j - 1 == 0) cp[k] -= corr; else { cp[k] -= corr * 0.5f; cp[up1] += corr * 0.5f; }
                    }
                    for (int j = 1; j < CH; j++) for (int i = 0; i < CW - 1; i++)
                    {
                        int a = j * CW + i, b = a + 1; float w = Mathf.Lerp(wTop, wBot, j / (float)(CH - 1)) * 2f / (CW - 1);
                        var d = cp[b] - cp[a]; float l = d.magnitude; if (l < 1e-5f) continue;
                        var corr = d * ((l - w) / l * 0.5f); cp[a] += corr; cp[b] -= corr;
                    }
                }
                // не пускаем плащ в тело
                Vector3 hipP = pm != null && pm.hip != null ? pm.hip.position : chest - up * 0.4f * sc;
                Vector3 seg = neck - hipP; float segL = Mathf.Max(0.01f, seg.magnitude); Vector3 sd = seg / segL; float rad = 0.27f * sc;
                for (int k = CW; k < cp.Length; k++)
                {
                    float t = Mathf.Clamp(Vector3.Dot(cp[k] - hipP, sd), 0f, segL); var cl = hipP + sd * t; var o = cp[k] - cl; float dd = o.magnitude;
                    if (dd < rad && dd > 1e-4f) cp[k] = cl + o / dd * rad;
                }
            }
            int n = CW * CH; var vs = capeMesh.vertices;
            for (int k = 0; k < n; k++) { vs[k] = cp[k]; vs[k + n] = cp[k]; }
            capeMesh.vertices = vs; capeMesh.RecalculateBounds();
        }
    }

    public partial class PeakFunPlugin
    {
        // ---------- управление костюмами ----------
        bool costumeOn = true, costumeSync = true; int costumeVar; readonly Dictionary<int, int> remoteVar = new Dictionary<int, int>(); float suitThick = 1f, headScale = 1f;
        readonly Dictionary<Component, CostumeInst> costumes = new Dictionary<Component, CostumeInst>();
        readonly Dictionary<int, HeroKind> remoteHero = new Dictionary<int, HeroKind>();
        float nextHeroSend;

        // для глаз: светятся, пока бьёт лазер (Патриот) или держит телекинез (Телепат)
        internal static bool EyesGlow(Component c, HeroKind k)
        {
            var me = Game.Me(); if (c != me) { int ac = Game.ActorOf(c); if (IsHoop(k)) return ZoneOn(c); return (k == HeroKind.Patriot && remoteBeam.ContainsKey(ac)) || (k == HeroKind.Gojo && domActors.Contains(ac)); }
            var pl = Instance; if (pl == null) return false;
            if (IsHoop(k)) return ZoneOn(c);
            if (k == HeroKind.Gojo) return pl.domCasting || pl.domActive || pl.purpleCharge || pl.blueOn;
            return (k == HeroKind.Patriot && pl.laserOn) || (k == HeroKind.Mind && pl.grabbed != null && pl.grabTele);
        }

        void TickCostumes()
        {
            var me = Game.Me();
            // мой костюм
            HeroKind want = costumeOn && hero != HeroKind.None && me != null ? hero : HeroKind.None;
            CostumeInst mine = null; if (me != null) costumes.TryGetValue(me, out mine);
            if (mine != null && (want == HeroKind.None || mine.kind != want || mine.variant != costumeVar || !mine.Alive)) { mine.Destroy(); costumes.Remove(me); mine = null; }
            if (mine == null && want != HeroKind.None) { mine = new CostumeInst(me, want, true, costumeVar); costumes[me] = mine; }
            // чужие
            if (costumeSync)
                foreach (var kv in remoteHero)
                {
                    if (kv.Value == HeroKind.None) continue;
                    var c = Game.ByActor(kv.Key); if (c == null || c == me) continue;
                    CostumeInst ci; costumes.TryGetValue(c, out ci);
                    int rv; remoteVar.TryGetValue(kv.Key, out rv);
                    if (ci != null && (ci.kind != kv.Value || ci.variant != rv || !ci.Alive)) { ci.Destroy(); costumes.Remove(c); ci = null; }
                    if (ci == null) costumes[c] = new CostumeInst(c, kv.Value, false, rv);
                }
            // уборка
            List<Component> dead = null;
            foreach (var kv in costumes)
            {
                bool keep = kv.Key != null && kv.Value.Alive;
                if (keep && kv.Key != me)
                {
                    int a = Game.ActorOf(kv.Key); HeroKind rk;
                    keep = costumeSync && remoteHero.TryGetValue(a, out rk) && rk == kv.Value.kind;
                }
                if (!keep) { if (dead == null) dead = new List<Component>(); dead.Add(kv.Key); }
            }
            if (dead != null) foreach (var k in dead) { CostumeInst ci; if (costumes.TryGetValue(k, out ci)) ci.Destroy(); costumes.Remove(k); }
            bool fp = !(thirdOn || puppetOn || obsMode > 0 || CineActive);
            foreach (var kv in costumes)
            {
                try { kv.Value.Tick(suitThick, headScale, !(kv.Value.local && fp)); }
                catch (Exception e) { SafeLog("Costume", e); kv.Value.Destroy(); }
            }
            // сообщаем друзьям, в кого мы превращены
            if (Time.time >= nextHeroSend && Game.InRoom) { nextHeroSend = Time.time + 8f; Net.Send("hero", ((int)hero).ToString() + ":" + costumeVar); }
        }

        void SendHero() { nextHeroSend = Time.time + 8f; if (Game.InRoom) Net.Send("hero", ((int)(costumeOn ? hero : HeroKind.None)).ToString() + ":" + costumeVar); }

        void ClearCostumes() { foreach (var kv in costumes) kv.Value.Destroy(); costumes.Clear(); }
    }
}
