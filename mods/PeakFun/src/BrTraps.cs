// Приманки и ловушки 1.17.1: четыре разных предмета (чемодан из самолёта PEAK, сундук, забытый рюкзак, аптечка) и семь разных ловушек.
// Вид предмета НЕ говорит, что внутри: Backrooms может оказаться и в сундуке, и в рюкзаке, а чемодан может быть пустышкой.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        internal static readonly string[] BaitNames = { "Чемодан", "Сундук", "Рюкзак", "Аптечка" };
        internal static readonly string[] TrapNames = { "Backrooms", "Катапульта", "Скример", "Споры", "Сон", "Холод", "Пустышка" };
        static readonly int[] TrapW = { 36, 11, 13, 9, 8, 8, 15 };
        int brPickModel = -1, brPickTrap = -1;

        internal static int PickTrap(System.Random r)
        {
            int tot = 0; foreach (var w in TrapW) tot += w; int x = r.Next(tot);
            for (int i = 0; i < TrapW.Length; i++) { x -= TrapW[i]; if (x < 0) return i; }
            return 0;
        }

        // ======================= текстуры =======================
        static Texture2D texWood, texCanvas, texChestWood;
        static Texture2D WoodTex()
        {
            if (texWood == null) texWood = BrGfx.MakeTex(256, 256, true, (u, v) =>
            {
                int pl = (int)(u * 5f); float pu = Mathf.Repeat(u * 5f, 1f);
                float g = 0.7f + 0.3f * Mathf.PerlinNoise(pu * 6f + pl * 9f, v * 70f + pl * 13f) + 0.08f * Mathf.PerlinNoise(pu * 40f, v * 400f);
                float knot = Mathf.PerlinNoise(pu * 3f + pl * 5f, v * 3f + 11f); if (knot > 0.78f) g *= 0.62f;
                float gap = Mathf.Min(pu, 1f - pu); if (gap < 0.025f) g *= 0.35f; else if (gap < 0.05f) g *= 0.8f;
                Color c = new Color(0.5f, 0.31f, 0.15f) * g * (0.9f + 0.1f * ((pl * 37) % 5) / 4f); c.a = 1f;
                float nv = Mathf.Min(v, 1f - v); if (nv < 0.07f && Mathf.Abs(pu - 0.5f) < 0.2f && Vector2.Distance(new Vector2(pu, nv), new Vector2(0.28f, 0.035f)) < 0.025f) c = new Color(0.12f, 0.1f, 0.09f);
                return c;
            });
            return texWood;
        }
        static Texture2D CanvasTex()
        {
            if (texCanvas == null) texCanvas = BrGfx.MakeTex(128, 128, true, (u, v) =>
            {
                float wv = 0.9f + 0.1f * Mathf.Sin(u * 380f) * Mathf.Sin(v * 380f); float n = 0.85f + 0.2f * Mathf.PerlinNoise(u * 14f, v * 14f); float dirt = Mathf.PerlinNoise(u * 4f + 3f, v * 4f) > 0.7f ? 0.82f : 1f;
                float s = wv * n * dirt; return new Color(s, s, s, 1f);
            });
            return texCanvas;
        }

        // ======================= модели =======================
        void BuildBait(Transform T, int model, int variant)
        {
            const string N = "PF_crate_player_part";
            var rnd = new System.Random(variant * 7 + 3);
            if (model == 1)            // ---- сундук с сокровищами ----
            {
                var wood = BrGfx.Surf(WoodTex(), new Color(1f, 0.92f, 0.82f), 0.12f); var iron = BrGfx.Surf(null, new Color(0.17f, 0.17f, 0.19f), 0.45f, 0.8f); var gold = BrGfx.Surf(null, new Color(0.95f, 0.72f, 0.2f), 0.7f, 0.9f);
                var glow = BrGfx.Surf(null, new Color(1f, 0.8f, 0.3f), 0.5f, 0.5f, new Color(2.4f, 1.6f, 0.45f));
                BrGfx.RBox(T, N, new Vector3(0f, 0.27f, 0f), new Vector3(1.02f, 0.54f, 0.62f), 0.03f, wood);
                BrGfx.RBox(T, N, new Vector3(0f, 0.5f, -0.02f), new Vector3(0.9f, 0.1f, 0.5f), 0.02f, glow);            // золото внутри
                var lid = new GameObject("lid"); lid.transform.SetParent(T, false); lid.transform.localPosition = new Vector3(0f, 0.54f, -0.31f); lid.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
                BrGfx.RBox(lid.transform, N, new Vector3(0f, 0.1f, 0.31f), new Vector3(1.04f, 0.22f, 0.64f), 0.1f, wood);
                foreach (float sx in new[] { -0.38f, 0.38f })
                {
                    BrGfx.RBox(T, N, new Vector3(sx, 0.27f, 0f), new Vector3(0.075f, 0.56f, 0.645f), 0.012f, iron);
                    BrGfx.RBox(lid.transform, N, new Vector3(sx, 0.1f, 0.31f), new Vector3(0.075f, 0.235f, 0.665f), 0.04f, iron);
                }
                foreach (float sx in new[] { -0.5f, 0.5f }) foreach (float sz in new[] { -0.3f, 0.3f }) BrGfx.RBox(T, N, new Vector3(sx, 0.05f, sz), new Vector3(0.07f, 0.1f, 0.07f), 0.015f, iron);
                BrGfx.RBox(T, N, new Vector3(0f, 0.43f, 0.323f), new Vector3(0.14f, 0.16f, 0.03f), 0.012f, gold);                 // замок
                BrGfx.RBox(T, N, new Vector3(0f, 0.41f, 0.34f), new Vector3(0.04f, 0.06f, 0.02f), 0.008f, iron);
                var l = new GameObject("PF_crate_player_light"); l.transform.SetParent(T, false); l.transform.localPosition = new Vector3(0f, 0.8f, 0f); var li = l.AddComponent<Light>(); li.type = LightType.Point; li.color = new Color(1f, 0.8f, 0.4f); li.range = 4f; li.intensity = 0.9f; li.shadows = LightShadows.None;
            }
            else if (model == 2)       // ---- забытый рюкзак ----
            {
                Color[] cols = { new Color(0.85f, 0.38f, 0.1f), new Color(0.2f, 0.45f, 0.3f), new Color(0.2f, 0.32f, 0.62f), new Color(0.65f, 0.15f, 0.15f), new Color(0.8f, 0.65f, 0.2f) }; Color c1 = cols[rnd.Next(cols.Length)], c2 = c1 * 0.62f; c2.a = 1f;
                var body = BrGfx.Surf(CanvasTex(), c1, 0.18f); var dark = BrGfx.Surf(CanvasTex(), c2, 0.15f); var strap = BrGfx.Surf(null, new Color(0.12f, 0.12f, 0.13f), 0.2f); var metal = BrGfx.Surf(null, new Color(0.75f, 0.77f, 0.8f), 0.6f, 0.8f);
                var g = new GameObject("pack"); g.transform.SetParent(T, false); g.transform.localPosition = new Vector3(0f, 0.3f, 0f); g.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
                var P = g.transform;
                BrGfx.RBox(P, N, new Vector3(0f, 0f, 0f), new Vector3(0.52f, 0.62f, 0.3f), 0.12f, body);
                BrGfx.RBox(P, N, new Vector3(0f, 0.29f, 0.01f), new Vector3(0.5f, 0.2f, 0.33f), 0.08f, dark);
                BrGfx.RBox(P, N, new Vector3(0f, -0.1f, 0.19f), new Vector3(0.38f, 0.3f, 0.1f), 0.045f, dark);
                BrGfx.RBox(P, N, new Vector3(0f, -0.05f, 0.245f), new Vector3(0.3f, 0.015f, 0.01f), 0.006f, metal);
                foreach (float sx in new[] { -0.29f, 0.29f }) BrGfx.RBox(P, N, new Vector3(sx, -0.14f, 0.02f), new Vector3(0.1f, 0.28f, 0.2f), 0.04f, dark);
                foreach (float sx in new[] { -0.14f, 0.14f }) BrGfx.RBox(P, N, new Vector3(sx, 0.07f, -0.158f), new Vector3(0.07f, 0.5f, 0.03f), 0.012f, strap);
                BrGfx.RBox(P, N, new Vector3(0f, 0.2f, 0.19f), new Vector3(0.07f, 0.12f, 0.03f), 0.01f, strap); BrGfx.RBox(P, N, new Vector3(0f, 0.15f, 0.2f), new Vector3(0.06f, 0.04f, 0.02f), 0.008f, metal);
                BrGfx.RBox(P, N, new Vector3(0f, 0.42f, -0.05f), new Vector3(0.14f, 0.05f, 0.04f), 0.015f, strap);
                var pad = new GameObject("PF_crate_player_pad"); pad.transform.SetParent(T, false);
            }
            else if (model == 3)       // ---- аптечка ----
            {
                var white = BrGfx.Surf(null, new Color(0.9f, 0.9f, 0.88f), 0.35f); var red = BrGfx.Surf(null, new Color(0.82f, 0.08f, 0.08f), 0.35f); var grey = BrGfx.Surf(null, new Color(0.45f, 0.47f, 0.5f), 0.5f, 0.6f);
                BrGfx.RBox(T, N, new Vector3(0f, 0.17f, 0f), new Vector3(0.62f, 0.34f, 0.26f), 0.05f, white);
                BrGfx.RBox(T, N, new Vector3(0f, 0.34f, 0f), new Vector3(0.28f, 0.04f, 0.05f), 0.015f, grey);
                BrGfx.RBox(T, N, new Vector3(0f, 0.18f, 0.132f), new Vector3(0.06f, 0.18f, 0.012f), 0.004f, red); BrGfx.RBox(T, N, new Vector3(0f, 0.18f, 0.132f), new Vector3(0.18f, 0.06f, 0.012f), 0.004f, red);
                BrGfx.RBox(T, N, new Vector3(0f, 0.18f, -0.132f), new Vector3(0.06f, 0.18f, 0.012f), 0.004f, red); BrGfx.RBox(T, N, new Vector3(0f, 0.18f, -0.132f), new Vector3(0.18f, 0.06f, 0.012f), 0.004f, red);
                foreach (float sx in new[] { -0.2f, 0.2f }) BrGfx.RBox(T, N, new Vector3(sx, 0.2f, 0.135f), new Vector3(0.05f, 0.05f, 0.02f), 0.006f, grey);
                BrGfx.RBox(T, N, new Vector3(0f, 0.17f, 0f), new Vector3(0.64f, 0.012f, 0.27f), 0.004f, grey);
            }
            else                       // ---- чемодан из самолёта PEAK: кожа красно-коричневая, оранжевые ремни с заклёпками, кремовые уголки, золотые замки, красная ручка ----
            {
                var leather = BrGfx.Surf(BrGfx.CaseAtlas(), Color.white, 0.4f); var strap = BrGfx.Surf(BrGfx.StrapTex(), Color.white, 0.3f);
                var cream = BrGfx.Surf(null, new Color(0.93f, 0.87f, 0.72f), 0.35f); var gold = BrGfx.Surf(null, new Color(0.97f, 0.76f, 0.25f), 0.7f, 0.85f); var red = BrGfx.Surf(null, new Color(0.78f, 0.1f, 0.08f), 0.4f);
                BrGfx.RBox(T, N, new Vector3(0f, 0.28f, 0f), new Vector3(1.3f, 0.56f, 0.8f), 0.07f, leather, Vector3.zero, 3, 2);
                foreach (float sx in new[] { -0.46f, 0.46f }) BrGfx.RBox(T, N, new Vector3(sx, 0.28f, 0f), new Vector3(0.12f, 0.58f, 0.83f), 0.03f, strap);
                foreach (float sx in new[] { -0.63f, 0.63f }) foreach (float sy in new[] { 0.06f, 0.5f }) foreach (float sz in new[] { -0.37f, 0.37f })
                            BrGfx.RBox(T, N, new Vector3(sx, sy, sz), new Vector3(0.12f, 0.12f, 0.12f), 0.035f, cream);
                foreach (float sx in new[] { -0.22f, 0.22f }) { BrGfx.RBox(T, N, new Vector3(sx, 0.3f, 0.41f), new Vector3(0.1f, 0.08f, 0.04f), 0.014f, gold); BrGfx.RBox(T, N, new Vector3(sx, 0.3f, -0.41f), new Vector3(0.1f, 0.08f, 0.04f), 0.014f, gold); }
                // красная ручка-петля спереди (как у чемодана из референса), золотые наконечники
                foreach (float sx in new[] { -0.11f, 0.11f }) BrGfx.RBox(T, N, new Vector3(sx, 0.16f, 0.43f), new Vector3(0.05f, 0.05f, 0.06f), 0.015f, gold);
                BrGfx.RBox(T, N, new Vector3(0f, 0.16f, 0.465f), new Vector3(0.26f, 0.035f, 0.035f), 0.012f, red);
                foreach (float sx in new[] { -0.115f, 0.115f }) BrGfx.RBox(T, N, new Vector3(sx, 0.185f, 0.455f), new Vector3(0.03f, 0.08f, 0.03f), 0.01f, red);
                BrGfx.RBox(T, N, new Vector3(0f, 0.6f, 0f), new Vector3(0.28f, 0.03f, 0.07f), 0.012f, red);
            }
        }

        static readonly float[] BaitScale = { 1.25f, 1.2f, 1.3f, 1.4f };

        BrCrate MakeCrate(int id, Vector3 p, float up, int n, int ent, int model = 0, int trap = 0)
        {
            foreach (var e in brCrates) if (e.id == id) return e;
            model = Mathf.Clamp(model, 0, 3); trap = Mathf.Clamp(trap, 0, TrapNames.Length - 1);
            var c = new BrCrate { id = id, pos = p, up = Mathf.Clamp(up, 40f, 1500f), n = Mathf.Clamp(n, 7, 80), ent = ent, model = model, trap = trap };
            var root = new GameObject("PF_crate_player"); root.transform.position = p + Vector3.down * 0.02f;
            root.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-2f, 2f), (id * 47 % 360), UnityEngine.Random.Range(-2f, 2f)); root.transform.localScale = Vector3.one * BaitScale[model]; c.go = root;
            var T = root.transform;
            var sh = new GameObject("PF_crate_player_shadow"); sh.transform.SetParent(T, false); sh.transform.localPosition = new Vector3(0f, 0.035f, 0f); sh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); sh.transform.localScale = new Vector3(1.7f, 1.2f, 1f);
            sh.AddComponent<MeshFilter>().sharedMesh = Vfx.Quad(); var smr = sh.AddComponent<MeshRenderer>(); smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; var smat = Vfx.Mat(Vfx.Blob(), new Color(0f, 0f, 0f, 0.5f)); if (smat != null) smr.sharedMaterial = smat;
            BuildBait(T, model, id);
            brCrates.Add(c); return c;
        }

        // ======================= срабатывание =======================
        IEnumerator CoTrap(BrCrate c)
        {
            if (c.trap == 0) { yield return StartCoroutine(CoBrFall(c)); yield break; }
            brBusy = true; var me = Game.Me(); Vector3 p = c.pos + Vector3.up * 0.5f; Sfx2.Clank(0.6f);
            float t = 0f; while (t < 0.55f) { t += Time.deltaTime; if (c.go != null) c.go.transform.position = c.pos + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)) * 0.03f; CamShake.Boost = Mathf.Max(CamShake.Boost, 0.2f); yield return null; }
            Color[] pal = { new Color(1f, 0.85f, 0.3f), new Color(0.4f, 1f, 0.5f), new Color(0.4f, 0.8f, 1f), new Color(1f, 0.4f, 0.6f) };
            switch (c.trap)
            {
                case 1: Vfx.Sparks(p, Vector3.up, 24, new Color(1f, 0.7f, 0.2f)); Sfx2.Whoosh(); HeroToast("Пружина!"); if (me != null) Launch(me); break;
                case 2: Fx.Start(FxKind.Scream, 2.6f); CamShake.Boost = 2f; HeroToast("…"); break;
                case 3: Vfx.Sparks(p, Vector3.up, 30, new Color(0.55f, 1f, 0.4f)); HeroToast("Облако спор!"); ApplyEffectLocal("spores", 12f); try { Shade.Set(ShadeKind.Vhs, 9f); } catch { } break;
                case 4: HeroToast("Так хочется спать…"); ApplyEffectLocal("drowsy", 12f); try { Shade.Set(ShadeKind.Glitch, 6f); } catch { } break;
                case 5: Vfx.Sparks(p, Vector3.up, 24, new Color(0.6f, 0.9f, 1f)); HeroToast("Ледяной сквозняк"); ApplyEffectLocal("cold", 12f); try { Shade.Set(ShadeKind.Negative, 3f); } catch { } break;
                default: foreach (var col in pal) Vfx.Sparks(p, Vector3.up, 16, col); Sfx2.Whoosh(); HeroToast("Пусто. Повезло!"); break;
            }
            BrDiag("ловушка «" + TrapNames[c.trap] + "» в предмете «" + BaitNames[c.model] + "»");
            if (c.go != null) UnityEngine.Object.Destroy(c.go, 0.4f); c.go = null; if (c.glow != null) c.glow.SetActive(false);
            brBusy = false;
        }
    }
}
