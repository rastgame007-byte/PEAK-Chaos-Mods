// Лут Backrooms (1.23): ящики по карте (удерживай E), предметы мода в «рюкзаке» (клавиши 5–9), риск: шум привлекает монстров.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PeakBR
{
    internal static class Loot
    {
        public static readonly string[] Names = { "Миндальная вода", "Светящаяся палочка", "Радио-приманка", "Карта выходов", "Талисман «второй шанс»" };
        static readonly KeyCode[] KeysUse = { KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9 };
        static readonly string[] Descr = { "лечит, снимает яды, даёт запас сил и успокаивает", "светит на месте до конца уровня", "бросок вперёд: монстры идут на шум", "45 секунд видны все двери", "спасает от одной поимки" };
        public static readonly int[] Inv = new int[5]; public static float MapUntil; public static int Opened, Found;
        static readonly int[] weight = { 22, 20, 14, 10, 5, 17, 12 };   // 5 — пусто, 6 — тревога

        sealed class Crate { public int id; public Vector3 pos; public float yaw; public GameObject go; public Transform lid; public bool open; public float anim; }
        static readonly List<Crate> crates = new List<Crate>(); static readonly List<GameObject> temp = new List<GameObject>(); static readonly List<float> tempDie = new List<float>();
        static int glowCount; static float hold; static Crate target; public static int Count { get { return crates.Count; } }

        public static void Reset() { Array.Clear(Inv, 0, Inv.Length); MapUntil = 0f; Opened = 0; Found = 0; }
        public static void Clear()
        {
            foreach (var c in crates) if (c.go != null) UnityEngine.Object.Destroy(c.go); crates.Clear();
            foreach (var t in temp) if (t != null) UnityEngine.Object.Destroy(t); temp.Clear(); tempDie.Clear(); glowCount = 0; hold = 0f; target = null; MapUntil = 0f;
        }

        // ---------- ящики ----------
        public static void Build(Built b, int seed)
        {
            Clear(); if (b == null || b.root == null) return;
            try
            {
                var g = b.g; var rnd = new System.Random(seed ^ 0x51ed); int want = Mathf.Clamp(b.cells.Count / 85, 6, 42); var used = new List<Vector3>();
                var body = BrGfx.Surf(null, new Color(0.40f, 0.28f, 0.16f), 0.25f); var band = BrGfx.Surf(null, new Color(0.14f, 0.11f, 0.08f), 0.5f, 0.5f); var stripe = BrGfx.Surf(null, new Color(1f, 0.8f, 0.3f), 0.3f, 0f, new Color(2.4f, 1.7f, 0.5f));
                for (int k = 0; k < want * 30 && crates.Count < want; k++)
                {
                    var c = b.cells[rnd.Next(b.cells.Count)]; if (g.dist[c.x, c.y] < 6) continue; Vector3 cc = b.CellC(c.x, c.y), p = cc; float yaw = (float)rnd.NextDouble() * 360f;
                    var walls = new List<int>(); for (int d = 0; d < 4; d++) if (!g.Pass(c.x, c.y, d)) walls.Add(d);
                    if (walls.Count > 0)
                    {
                        int w = walls[rnd.Next(walls.Count)]; Vector3 dir = new Vector3(Grid.DX[w], 0f, Grid.DY[w]); p = cc + dir * (b.C * 0.5f - 0.175f - 0.42f) + new Vector3(-dir.z, 0f, dir.x) * (((float)rnd.NextDouble() - 0.5f) * 1.6f); yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
                    }
                    else p = cc + new Vector3(((float)rnd.NextDouble() - 0.5f) * 2.4f, 0f, ((float)rnd.NextDouble() - 0.5f) * 2.4f);
                    bool ok = Util.Flat(p - b.spawn).magnitude > 12f; foreach (var u in used) if (Util.Flat(u - p).magnitude < 11f) { ok = false; break; } foreach (var ex in b.exitPos) if (Util.Flat(ex - p).magnitude < 3.5f) { ok = false; break; }
                    if (!ok) continue; used.Add(p); p.y = b.baseP.y;
                    var cr = new Crate { id = crates.Count, pos = p, yaw = yaw }; BuildCrate(cr, b, body, band, stripe); crates.Add(cr);
                }
            }
            catch (Exception e) { Plugin.Warn("лут", e); }
        }

        static GameObject Bx(Transform par, Vector3 lp, Vector3 ls, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "BR_loot_part"; var c = g.GetComponent<Collider>(); if (c != null) UnityEngine.Object.DestroyImmediate(c);
            g.transform.SetParent(par, false); g.transform.localPosition = lp; g.transform.localScale = ls; var r = g.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return g;
        }
        static void BuildCrate(Crate c, Built b, Material body, Material band, Material stripe)
        {
            var go = new GameObject("BR_loot_player"); go.transform.SetParent(b.root.transform, false); go.transform.position = c.pos; go.transform.rotation = Quaternion.Euler(0f, c.yaw, 0f); c.go = go; var T = go.transform;
            Bx(T, new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.5f, 0.6f), body); Bx(T, new Vector3(-0.3f, 0.25f, 0), new Vector3(0.06f, 0.52f, 0.62f), band); Bx(T, new Vector3(0.3f, 0.25f, 0), new Vector3(0.06f, 0.52f, 0.62f), band);
            var lid = new GameObject("lid").transform; lid.SetParent(T, false); lid.localPosition = new Vector3(0, 0.5f, -0.3f); c.lid = lid;
            Bx(lid, new Vector3(0, 0.03f, 0.3f), new Vector3(0.94f, 0.06f, 0.64f), body); Bx(lid, new Vector3(0, 0.065f, 0.52f), new Vector3(0.4f, 0.02f, 0.06f), stripe);
            Bx(T, new Vector3(0, 0.3f, 0.305f), new Vector3(0.14f, 0.1f, 0.02f), band);
        }

        // ---------- содержимое (одинаково у всех по seed) ----------
        static void Roll(int id, out int kind, out int qty)
        {
            var r = new System.Random(Play.Seed * 7919 + Play.Level * 131 + id * 17 + 5); int tot = 0; foreach (var w in weight) tot += w; int v = r.Next(tot); kind = 0;
            for (int i = 0; i < weight.Length; i++) { if (v < weight[i]) { kind = i; break; } v -= weight[i]; }
            qty = (kind == 0 || kind == 1) ? 1 + r.Next(2) : 1;
        }

        static void OpenCrate(Crate c, bool mine)
        {
            if (c.open) return; c.open = true; c.anim = 0f; Snd2.At("knock", c.pos + Vector3.up * 0.5f, 0.7f);
            if (!mine) return;
            int kind, qty; Roll(c.id, out kind, out qty); Opened++;
            if (kind <= 4) { Inv[kind] += qty; Found += qty; Ui.Toast("Найдено: " + Names[kind] + (qty > 1 ? " ×" + qty : "") + "  (" + Descr[kind] + ", клавиша " + (5 + kind) + ")", Ui.Good); Snd2.Flat("ding", 0.5f); }
            else if (kind == 5) Ui.Toast("Ящик пуст…", Ui.Muted);
            else { Ui.Toast("Ящик ЗАВОПИЛ — монстры слышат!", Ui.Bad); Snd2.At("boom", c.pos, 1f); Snd2.At("zap", c.pos, 0.9f); CamFx.Boost = Mathf.Max(CamFx.Boost, 1.3f); Lure(c.pos, false); }
            try { Net.Send("crate", c.id.ToString()); } catch { }
        }

        // ---------- шум и приманки ----------
        static string P(Vector3 v) { return v.x.ToString("0.0", CultureInfo.InvariantCulture) + "," + v.y.ToString("0.0", CultureInfo.InvariantCulture) + "," + v.z.ToString("0.0", CultureInfo.InvariantCulture); }
        static bool Parse(string a, out Vector3 v, out bool radio)
        {
            v = Vector3.zero; radio = false; var f = a.Split(','); if (f.Length < 3) return false; float x, y, z;
            if (!float.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) || !float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) || !float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return false;
            v = new Vector3(x, y, z); radio = f.Length > 3 && f[3] == "1"; return true;
        }
        static void Lure(Vector3 p, bool radio)
        {
            ApplyLure(p, radio); try { Net.Send("lure", P(p) + "," + (radio ? "1" : "0")); } catch { }
        }
        static void ApplyLure(Vector3 p, bool radio)
        {
            if (radio) SpawnRadio(p); if (Play.IsHost) Ents.Lure(p, radio ? 15f : 18f);
        }
        static void SpawnRadio(Vector3 p)
        {
            if (Play.Cur == null) return; var go = new GameObject("BR_radio_player"); go.transform.SetParent(Play.Cur.root.transform, false); go.transform.position = p;
            var blk = BrGfx.Surf(null, new Color(0.08f, 0.08f, 0.09f), 0.4f); var led = BrGfx.Surf(null, new Color(1f, 0.2f, 0.1f), 0.3f, 0f, new Color(3f, 0.3f, 0.1f));
            Bx(go.transform, new Vector3(0, 0.12f, 0), new Vector3(0.34f, 0.24f, 0.14f), blk); Bx(go.transform, new Vector3(0.1f, 0.2f, 0.075f), new Vector3(0.05f, 0.05f, 0.02f), led);
            var src = go.AddComponent<AudioSource>(); var clip = Snd2.Get("buzz"); if (clip != null) { src.clip = clip; src.loop = true; src.spatialBlend = 1f; src.maxDistance = 40f; src.rolloffMode = AudioRolloffMode.Linear; src.volume = 0.9f; src.Play(); }
            temp.Add(go); tempDie.Add(Time.time + 15f);
        }
        static void SpawnGlow(Vector3 p)
        {
            if (Play.Cur == null) return; if (glowCount >= 14) return; glowCount++;
            var go = new GameObject("BR_glow_player"); go.transform.SetParent(Play.Cur.root.transform, false); go.transform.position = p + Vector3.up * 0.12f;
            var m = BrGfx.Surf(null, new Color(0.3f, 1f, 0.5f), 0.3f, 0f, new Color(0.6f, 3f, 1.1f)); var s = Bx(go.transform, Vector3.zero, new Vector3(0.05f, 0.05f, 0.3f), m); s.transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.value * 360f, 0f);
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.45f, 1f, 0.6f); l.range = 11f; l.intensity = 2.2f; l.shadows = LightShadows.None; go.transform.position = p + Vector3.up * 0.4f;
        }

        public static void OnNet(string cmd, string arg)
        {
            try
            {
                if (cmd == "crate") { int id; if (int.TryParse(arg, out id) && id >= 0 && id < crates.Count) OpenCrate(crates[id], false); }
                else if (cmd == "glow") { Vector3 v; bool r; if (Parse(arg, out v, out r)) SpawnGlow(v); }
                else if (cmd == "lure") { Vector3 v; bool r; if (Parse(arg, out v, out r)) ApplyLure(v, r); }
            }
            catch (Exception e) { Plugin.Warn("loot net", e); }
        }

        /// <summary>Талисман: гасит поимку. Возвращает true, если поимка отменена.</summary>
        public static bool TryCharm(string kind)
        {
            if (Inv[4] <= 0) return false; Inv[4]--; Ui.Toast("Талисман спас тебя от: " + kind + "!", Ui.Good); Snd2.Flat("ding", 0.8f); Play.Glitch = 1f; CamFx.Boost = Mathf.Max(CamFx.Boost, 1.5f); return true;
        }

        // ---------- каждый кадр ----------
        public static void Tick(float dt)
        {
            if (Play.Cur == null || Play.Busy || Play.Catching) { hold = 0f; return; }
            float now = Time.time;
            for (int i = temp.Count - 1; i >= 0; i--) if (now > tempDie[i]) { if (temp[i] != null) UnityEngine.Object.Destroy(temp[i]); temp.RemoveAt(i); tempDie.RemoveAt(i); }
            foreach (var c in crates) if (c.open && c.anim < 1f) { c.anim = Mathf.MoveTowards(c.anim, 1f, dt * 2.2f); if (c.lid != null) c.lid.localRotation = Quaternion.Euler(-105f * (1f - Mathf.Pow(1f - c.anim, 3f)), 0f, 0f); }
            var me = Game.Me(); var cam = Camera.main; if (me == null || cam == null || Game.IsDead(me)) { hold = 0f; return; }
            Vector3 pc = Util.CenterOf(me);
            // использование предметов
            for (int i = 0; i < 5; i++) if (Inv[i] > 0 && Keys.Down(KeysUse[i])) { Use(i, pc, cam); break; }
            // ближайший ящик
            target = null; float bd = 2.7f; Vector3 fw = cam.transform.forward; fw.y = 0f; fw.Normalize();
            foreach (var c in crates)
            {
                if (c.open) continue; Vector3 d = c.pos - pc; d.y = 0f; float dd = d.magnitude; if (dd > bd || Mathf.Abs(c.pos.y + 0.8f - pc.y) > 2.5f) continue;
                if (dd > 0.9f && Vector3.Dot(fw, d / dd) < 0.2f) continue; bd = dd; target = c;
            }
            if (target != null && Keys.Held(KeyCode.E)) { hold += dt; if (hold >= 1.4f) { hold = 0f; OpenCrate(target, true); target = null; } } else hold = Mathf.Max(0f, hold - dt * 2f);
        }

        static void Use(int i, Vector3 pc, Camera cam)
        {
            switch (i)
            {
                case 0: Inv[0]--; Util.Refresh(); Ui.Toast("Миндальная вода: силы восстановлены, раны затянулись, тревога спала", Ui.Good); Snd2.Flat("splash", 0.5f); break;
                case 1:
                {
                    Inv[1]--; Vector3 p = new Vector3(pc.x, Play.Cur.baseP.y, pc.z) + Util.Flat(cam.transform.forward).normalized * 0.8f; SpawnGlow(p); try { Net.Send("glow", P(p)); } catch { } Ui.Toast("Светящаяся палочка брошена", Ui.Accent); break;
                }
                case 2:
                {
                    Inv[2]--; Vector3 dir = Util.Flat(cam.transform.forward).normalized; if (dir.sqrMagnitude < 0.1f) dir = Vector3.forward; Vector3 from = new Vector3(pc.x, Play.Cur.baseP.y + 1.2f, pc.z); float len = 14f; RaycastHit hit;
                    if (Physics.Raycast(from, dir, out hit, len, 1 << LevelBuilder.Layer(), QueryTriggerInteraction.Ignore)) len = Mathf.Max(1f, hit.distance - 0.4f);
                    Vector3 p = from + dir * len; p.y = Play.Cur.baseP.y; Lure(p, true); Ui.Toast("Радио-приманка включена — монстры идут на шум", Ui.Accent); break;
                }
                case 3: Inv[3]--; MapUntil = Time.time + 45f; Ui.Toast("Карта: все двери отмечены на 45 секунд", Ui.Good); break;
                default: Ui.Toast("Талисман сработает сам при поимке", Ui.Muted); break;
            }
        }

        // ---------- HUD ----------
        static bool Proj(Camera cam, Vector3 w, float W, float H, float m, out Vector2 sp, out bool edge)
        {
            Vector3 v = cam.WorldToScreenPoint(w); bool behind = v.z < 0f; float x = v.x, y = H - v.y; if (behind) { x = W - x; y = H - y; }
            edge = behind || x < m || x > W - m || y < m || y > H - m; x = Mathf.Clamp(x, m, W - m); y = Mathf.Clamp(y, m, H - m); sp = new Vector2(x, y); return true;
        }

        public static void Hud(float W, float H, float s, Camera cam)
        {
            var me = Game.Me(); if (me == null || Play.Cur == null) return; Vector3 pc = Util.CenterOf(me);
            // рюкзак
            float bx = W - 330f * s, by = H - 255f * s; Ui.Box(new Rect(bx - 10f * s, by - 8f * s, 320f * s, 148f * s), new Color(0, 0, 0, 0.35f), 0f);
            for (int i = 0; i < 5; i++)
            {
                bool has = Inv[i] > 0; var col = has ? new Color(1f, 0.95f, 0.7f, 0.95f) : new Color(1f, 1f, 1f, 0.28f);
                Ui.Text(new Rect(bx, by + i * 27f * s, 310f * s, 26f * s), "[" + (5 + i) + "] " + Names[i] + "  ×" + Inv[i], (int)(15f * s), col, TextAnchor.MiddleLeft, true);
            }
            if (Time.time < MapUntil) Ui.Text(new Rect(bx, by + 136f * s - 2f * s, 310f * s, 18f * s), "карта: " + Mathf.CeilToInt(MapUntil - Time.time) + " с", (int)(12f * s), new Color(0.5f, 1f, 0.6f, 0.9f), TextAnchor.MiddleLeft, true);
            // ящики рядом
            foreach (var c in crates)
            {
                if (c.open) continue; float d = Util.Flat(c.pos - pc).magnitude; if (d > 9f || d < 2.7f) continue; Vector2 sp; bool edge; Proj(cam, c.pos + Vector3.up * 0.7f, W, H, 40f * s, out sp, out edge);
                Ui.Text(new Rect(sp.x - 60f * s, sp.y - 14f * s, 120f * s, 26f * s), "▣ " + Mathf.RoundToInt(d) + " м", (int)(14f * s), new Color(1f, 0.85f, 0.35f, edge ? 0.55f : 0.9f), TextAnchor.MiddleCenter, true);
            }
            // карта дверей
            if (Time.time < MapUntil)
                foreach (var ex in Play.Cur.exitPos) { Vector2 sp; bool edge; Proj(cam, ex + Vector3.up * 1.4f, W, H, 50f * s, out sp, out edge); float d = Util.Flat(ex - pc).magnitude; Ui.Text(new Rect(sp.x - 80f * s, sp.y - 16f * s, 160f * s, 30f * s), (edge ? "◆ ВЫХОД " : "▼ ВЫХОД ") + Mathf.RoundToInt(d) + " м", (int)(17f * s), new Color(0.4f, 1f, 0.55f, 0.95f), TextAnchor.MiddleCenter, true); }
            // подсказка у ящика
            if (target != null)
            {
                Ui.Text(new Rect(W * 0.5f - 250f * s, H * 0.58f, 500f * s, 30f * s), "Удерживай [E] — открыть ящик", (int)(20f * s), new Color(1f, 0.95f, 0.7f, 0.95f), TextAnchor.MiddleCenter, true);
                float p = Mathf.Clamp01(hold / 1.4f); Ui.Box(new Rect(W * 0.5f - 100f * s, H * 0.58f + 34f * s, 200f * s, 6f * s), new Color(0, 0, 0, 0.5f), 0f); Ui.Box(new Rect(W * 0.5f - 100f * s, H * 0.58f + 34f * s, 200f * s * p, 6f * s), new Color(1f, 0.85f, 0.3f, 0.95f), 0f);
            }
        }
    }
}
