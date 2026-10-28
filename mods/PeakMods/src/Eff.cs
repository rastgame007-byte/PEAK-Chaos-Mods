// Помощники игры для модификаторов: аффликции, статусы, движение, предметы, карта, свет и туман. Всё через рефлексию (игра не нужна для сборки).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace PeakMods
{
    public partial class PeakModsPlugin
    {
        public static PeakModsPlugin I;
        public static bool Dry;                        // автопроверка: ничего не трогаем в мире, только гоняем код
        public static int DryCalls;                    // сколько «мировых» действий было бы выполнено
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static double Utc() { return (DateTime.UtcNow - Epoch).TotalSeconds; }

        public static Component Me() { return Game.Me(); }
        public static Component MeAlive() { var m = Game.Me(); return m != null && !Game.IsDead(m) ? m : null; }
        public static List<Component> OthersAlive() { var l = Game.Others(); l.RemoveAll(c => c == null || Game.IsDead(c)); return l; }

        // ---------------- аффликции ----------------
        static readonly string[][] AffMap =
        {
            new[] { "energy", "Affliction_FasterBoi" }, new[] { "stamina", "Affliction_InfiniteStamina" }, new[] { "invinc", "Affliction_Invincibility" },
            new[] { "nohunger", "Affliction_NoHunger" }, new[] { "lowgrav", "Affliction_LowGravity" }, new[] { "sunscreen", "Affliction_Sunscreen" },
            new[] { "chalk", "Affliction_ClimbingChalk" }, new[] { "djump", "Affliction_DoubleJumpAmulet" }, new[] { "heal", "Affliction_HealAll" },
            new[] { "blind", "Affliction_Blind" }, new[] { "numb", "Affliction_Numb" }, new[] { "exhaust", "Affliction_Exhaustion" },
            new[] { "chaosaff", "Affliction_Chaos" }, new[] { "bonus", "Affliction_AddBonusStamina" },
        };
        public static string ClsOf(string id) { foreach (var p in AffMap) if (p[0] == id) return p[1]; return null; }
        public static Type AffType(string cls) { return R.FindType("Peak.Afflictions." + cls) ?? R.FindType(cls); }

        public static object MakeAff(string id, float dur)
        {
            string cls = ClsOf(id); var t = cls == null ? null : AffType(cls); if (t == null) return null; object a;
            switch (id)
            {
                case "energy": a = R.New(t); R.Set(a, "moveSpeedMod", 1.4f); R.Set(a, "climbSpeedMod", 1.25f); R.Set(a, "drowsyOnEnd", 0f); R.Set(a, "climbDelay", 0f); break;
                case "stamina": a = R.New(t, dur); R.Set(a, "drowsyAffliction", null); R.Set(a, "climbDelay", 0f); break;
                case "lowgrav": a = R.New(t, 1, dur); break;
                case "sunscreen": a = R.New(t, dur); break;
                case "chalk": a = R.New(t); R.Set(a, "climbStaminaMultiplier", 0.5f); break;
                case "invinc": a = R.New(t); R.Set(a, "isFromMilk", false); R.Set(a, "isFromAmulet", false); break;
                case "exhaust": a = R.New(t); R.Set(a, "drainAmount", 1f); break;
                case "chaosaff": return R.New(t, 0.5f, 0.2f, 0.3f, 0.1f);
                case "heal": a = R.New(t); R.Set(a, "maxHealing", 1f); return a;
                case "bonus": return R.New(t, 1f, 0f);
                default: a = R.New(t); break;
            }
            if (a != null) R.Set(a, "totalTime", dur); return a;
        }

        static bool AddAff(Component me, object aff)
        {
            var affs = Game.Afflictions(me); if (affs == null || aff == null) return false;
            if (R.TryInvoke(affs, "AddAffliction", out var _, aff, false)) return true;
            return R.TryInvoke(affs, "AddAffliction", out var _2, aff);
        }

        static List<object> ActiveAffs(Component me, string cls)
        {
            var res = new List<object>(); var src = R.Get(Game.Afflictions(me), "afflictionList") as IEnumerable; if (src == null) return res;
            foreach (var o in src) if (o != null && o.GetType().Name == cls) res.Add(o); return res;
        }

        static void EnsureBuff(Component me, string id)
        {
            string cls = ClsOf(id); var cur = ActiveAffs(me, cls);
            if (cur.Count > 0)
            {
                foreach (var a in cur) { float tt = R.Get<float>(a, "totalTime"), el = R.Get<float>(a, "timeElapsed"); if (tt - el < 60000f) { R.Set(a, "totalTime", 99999f); R.Set(a, "timeElapsed", 0f); } }
                return;
            }
            AddAff(me, MakeAff(id, 99999f));
        }

        static void ExpireBuff(Component me, string id)
        {
            var affs = Game.Afflictions(me);
            foreach (var a in ActiveAffs(me, ClsOf(id)))
            {
                bool removed = false; try { removed = R.TryInvoke(affs, "RemoveAffliction", out var _, a); } catch { }
                if (!removed) R.Set(a, "timeElapsed", R.Get<float>(a, "totalTime") + 1f);
            }
        }

        static readonly Dictionary<string, int> wantBuff = new Dictionary<string, int>(); static float nextBuffTick;
        public static void Buff(string id, bool on)
        {
            int n; wantBuff.TryGetValue(id, out n); n += on ? 1 : -1; if (n <= 0) { wantBuff.Remove(id); if (!Dry) { var me = Me(); if (me != null) try { ExpireBuff(me, id); } catch { } } } else wantBuff[id] = n;
        }
        static void TickBuffs(Component me)
        {
            if (Time.time < nextBuffTick) return; nextBuffTick = Time.time + 0.5f;
            foreach (var id in wantBuff.Keys.ToList()) { try { if (!Dry) EnsureBuff(me, id); } catch (Exception e) { Log("баф " + id, e); } }
        }
        /// <summary>Разовый эффект (лечение, слепота, усталость…).</summary>
        public static void Pulse(string id, float dur) { if (Dry) { DryCalls++; return; } var me = MeAlive(); if (me == null) return; try { AddAff(me, MakeAff(id, dur)); } catch (Exception e) { Log("эффект " + id, e); } }

        // ---------------- статусы (яд, холод, голод…) ----------------
        public static void Status(string status, float amount)
        {
            if (Dry) { DryCalls++; return; } var me = MeAlive(); if (me == null) return; var affs = Game.Afflictions(me); if (affs == null) return;
            try { R.TryInvoke(affs, "AddStatus", out var _, status, amount); } catch (Exception e) { Log("AddStatus " + status, e); }
        }
        static readonly Dictionary<string, float> drips = new Dictionary<string, float>();
        public static void Drip(string status, float perSec, float dt)
        {
            float b; drips.TryGetValue(status, out b); b += perSec * dt; if (b >= 0.03f) { Status(status, b); b = 0f; } drips[status] = b;
        }
        public static void ClearStatuses() { if (Dry) { DryCalls++; return; } var me = MeAlive(); if (me == null) return; var affs = Game.Afflictions(me); if (affs != null && !R.TryInvoke(affs, "ClearAllStatus", out var _)) R.TryInvoke(affs, "ClearAll", out var _2); }

        // ---------------- движение: общий множитель от всех модификаторов ----------------
        static readonly Dictionary<string, float> mSpd = new Dictionary<string, float>(), mJmp = new Dictionary<string, float>(), mClm = new Dictionary<string, float>(), mGrv = new Dictionary<string, float>(), mLgt = new Dictionary<string, float>();
        public static void MulSet(Dictionary<string, float> d, string key, float v) { d[key] = v; }
        static float Prod(Dictionary<string, float> d) { float p = 1f; foreach (var v in d.Values) p *= v; return p; }
        static float baseForce = -1f, baseJump = -1f; static readonly Dictionary<string, float> baseClimb = new Dictionary<string, float>(); static Vector3 baseGravity; static bool gravKnown;
        static readonly string[] ClimbTypes = { "CharacterClimbing", "CharacterVineClimbing", "CharacterRopeHandling" };
        static bool moveTouched;
        public static string MoveInfo = "";

        static void ApplyMovement(Component me, bool force)
        {
            float s = Prod(mSpd), j = Prod(mJmp), c = Prod(mClm), g = Prod(mGrv);
            bool any = s != 1f || j != 1f || c != 1f || g != 1f; if (!any && !moveTouched && !force) return;
            moveTouched = any;
            var mv = Game.Movement(me);
            if (mv != null)
            {
                if (baseForce < 0f) { baseForce = R.Get<float>(mv, "movementForce"); baseJump = R.Get<float>(mv, "jumpImpulse"); }
                if (baseForce > 0f) R.Set(mv, "movementForce", baseForce * s); if (baseJump > 0f) R.Set(mv, "jumpImpulse", baseJump * j);
            }
            foreach (var n in ClimbTypes)
            {
                var t = R.FindType(n); var comp = t == null ? null : (me.GetComponent(t) ?? me.GetComponentInChildren(t)); if (comp == null) continue;
                if (!baseClimb.ContainsKey(n)) baseClimb[n] = R.Get<float>(comp, "climbSpeedMod", 1f); R.Set(comp, "climbSpeedMod", baseClimb[n] * c);
            }
            if (!gravKnown) { baseGravity = Physics.gravity; gravKnown = true; }
            Physics.gravity = baseGravity * g;
            MoveInfo = "скорость ×" + s.ToString("0.00") + ", прыжок ×" + j.ToString("0.00") + ", лазание ×" + c.ToString("0.00") + ", гравитация ×" + g.ToString("0.00");
        }

        public static void Push(Component me, Vector3 vel)
        {
            if (Dry) { DryCalls++; return; } foreach (var rb in Game.Rigs(me)) rb.AddForce(vel, ForceMode.VelocityChange);
        }
        public static void Trip(Component me) { if (Dry) { DryCalls++; return; } Game.Rpc(me, "RPCA_Fall", 3f); }
        public static void Launch(Component me, float k = 1f)
        {
            if (Dry) { DryCalls++; return; } Game.Rpc(me, "RPCA_Fall", 3f); Vector3 dir = Vector3.up + new Vector3(UnityEngine.Random.Range(-.5f, .5f), 0f, UnityEngine.Random.Range(-.5f, .5f));
            Game.Rpc(me, "RPCA_AddForceToBodyPart", 0, dir.normalized * 6000f * k);
        }

        // ---------------- ввод ----------------
        static Component inputC;
        public static Component InputOf(Component me)
        {
            if (inputC == null) inputC = R.Get(me, "input") as Component ?? R.Get(R.Get(me, "refs"), "input") as Component; return inputC;
        }

        // ---------------- карта и сегменты ----------------
        static Component mapH; static float nextMapFind;
        public static Component FindMap()
        {
            if (mapH != null) return mapH; if (Time.realtimeSinceStartup < nextMapFind) return null; nextMapFind = Time.realtimeSinceStartup + 3f;
            var t = R.FindType("MapHandler"); if (t == null) return null; try { mapH = UnityEngine.Object.FindObjectOfType(t) as Component; } catch { } return mapH;
        }
        public static int SegCur() { var m = FindMap(); return m == null ? -1 : R.Get<int>(m, "currentSegment", -1); }
        public static bool SegAnchor(int seg, out Vector3 p)
        {
            p = Vector3.zero; var m = FindMap(); if (m == null) return false;
            try { var segs = R.Get(m, "segments") as Array; if (segs == null || seg < 0 || seg >= segs.Length) return false; var tr = R.Get(segs.GetValue(seg), "reconnectSpawnPos") as Transform; if (tr == null) return false; p = tr.position; return true; } catch { return false; }
        }
        public static int TerrainMask()
        {
            try
            {
                var hf = R.FindType("HelperFunctions"); var lt = R.FindType("HelperFunctions+LayerType");
                if (hf != null && lt != null && R.TryInvokeStatic(hf, "GetMask", out var m, Enum.ToObject(lt, 1))) { if (m is LayerMask lm) return lm.value; if (m is int i) return i; }
            }
            catch { }
            return ~0;
        }
        /// <summary>Точки на ровной земле вокруг начала биома: одинаковые у всех (seed + биом).</summary>
        public static List<Vector3> FieldPoints(int seg, int count, int salt, float rmin, float rmax, float minGap)
        {
            var res = new List<Vector3>(); Vector3 a; var me = Me(); if (!SegAnchor(seg, out a)) { if (me == null) return res; a = me.transform.position; }
            var r = new System.Random(runSeed * 131 + seg * 17 + salt); int mask = TerrainMask(); if (mask == 0) mask = ~0;
            for (int i = 0; i < count * 14 && res.Count < count; i++)
            {
                float ang = (float)(r.NextDouble() * Math.PI * 2.0), rad = rmin + (float)r.NextDouble() * (rmax - rmin); RaycastHit h;
                if (!Physics.Raycast(a + new Vector3(Mathf.Cos(ang) * rad, 45f, Mathf.Sin(ang) * rad), Vector3.down, out h, 110f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (h.normal.y < 0.8f || Mathf.Abs(h.point.y - a.y) > 30f || h.collider.attachedRigidbody != null) continue;
                bool far = true; foreach (var q in res) if ((q - h.point).sqrMagnitude < minGap * minGap) far = false; if (far) res.Add(h.point);
            }
            return res;
        }

        // ---------------- предметы ----------------
        static readonly List<string> itemList = new List<string>(); static readonly Dictionary<string, string> itemTitle = new Dictionary<string, string>(); static bool itemsLoaded; public static string ItemStatus = "не загружены";
        static readonly string[] FoodKw = { "berry", "apple", "coconut", "banana", "bar", "egg", "honey", "mushroom", "candy", "nut", "jerky", "meat", "orange", "cookie", "lolli", "energy", "pretzel", "fruit", "bread", "cheese", "carrot", "pear" };
        static readonly string[] HealKw = { "bandage", "aid", "med", "heal", "cure", "antidote", "aloe", "pill", "remedy", "potion", "bottle" };
        static readonly string[] ToolKw = { "rope", "anchor", "pick", "axe", "hook", "stake", "chalk", "flare", "torch", "lantern", "glow", "lamp", "wing", "balloon", "shroom", "parasol", "umbrella", "compass", "bell", "scroll" };
        static readonly string[] BadKw = { "bug", "ghost", "npc", "scout", "bot", "test", "debug", "dev", "zombie", "corpse", "statue", "placeholder", "dummy" };

        static string Pretty(string s) { var sb = new System.Text.StringBuilder(); for (int i = 0; i < s.Length; i++) { char ch = s[i]; if (i > 0 && char.IsUpper(ch) && char.IsLower(s[i - 1])) sb.Append(' '); sb.Append(ch == '_' ? ' ' : ch); } return sb.ToString(); }

        public static void LoadItems()
        {
            itemsLoaded = true; itemList.Clear(); itemTitle.Clear();
            try
            {
                var seen = new HashSet<string>(); var dbT = R.FindType("ItemDatabase");
                if (dbT != null) foreach (var db in Resources.FindObjectsOfTypeAll(dbT))
                    {
                        var lk = R.Get(db, "itemLookup") as IDictionary; if (lk != null) foreach (var v in lk.Values) AddItem(v, seen);
                        var objs = R.Get(db, "Objects") as IEnumerable; if (objs != null) foreach (var v in objs) AddItem(v, seen);
                    }
                if (itemList.Count == 0) foreach (var go in Resources.LoadAll("0_Items", typeof(GameObject))) if (seen.Add(go.name)) { itemList.Add(go.name); itemTitle[go.name] = Pretty(go.name); }
                itemList.Sort(StringComparer.Ordinal);
                ItemStatus = itemList.Count > 0 ? "предметов: " + itemList.Count : "список предметов не найден";
            }
            catch (Exception e) { ItemStatus = "ошибка: " + e.Message; Log("LoadItems", e); }
        }
        static void AddItem(object v, HashSet<string> seen)
        {
            var o = v as UnityEngine.Object; if (o == null || string.IsNullOrEmpty(o.name) || !seen.Add(o.name)) return;
            string t = null; try { if (R.TryInvoke(v, "GetName", out var res) && res is string s1 && !string.IsNullOrWhiteSpace(s1)) t = s1; } catch { }
            if (string.IsNullOrWhiteSpace(t)) t = R.Get(R.Get(v, "UIData"), "itemName") as string; itemList.Add(o.name); itemTitle[o.name] = string.IsNullOrWhiteSpace(t) ? Pretty(o.name) : t;
        }
        public static string ItemTitleOf(string p) { string t; return itemTitle.TryGetValue(p, out t) ? t : p; }

        /// <summary>kind: food / heal / tool / any / junk (любое).</summary>
        public static string PickItem(System.Random r, string kind)
        {
            if (!itemsLoaded || itemList.Count == 0) LoadItems(); if (itemList.Count == 0) return null;
            string[] kw = kind == "food" ? FoodKw : kind == "heal" ? HealKw : kind == "tool" ? ToolKw : null; var pool = new List<string>();
            foreach (var n in itemList) { string k = (n + " " + ItemTitleOf(n)).ToLowerInvariant(); bool bad = false; foreach (var b in BadKw) if (k.Contains(b)) bad = true; if (bad) continue; if (kw == null) { pool.Add(n); continue; } foreach (var w in kw) if (k.Contains(w)) { pool.Add(n); break; } }
            if (pool.Count == 0) foreach (var n in itemList) pool.Add(n);
            return pool.Count == 0 ? null : pool[r.Next(pool.Count)];
        }
        static bool SpawnWorldItem(string name, Vector3 pos)
        {
            try { var go = Game.PhotonSpawn("InstantiateItemRoom", name, pos, Quaternion.identity); if (go != null) return true; } catch (Exception e) { Log("InstantiateItemRoom", e); }
            try { var go = Game.PhotonSpawn("Instantiate", "0_Items/" + name, pos, Quaternion.identity); return go != null; } catch (Exception e) { Log("Instantiate item", e); }
            return false;
        }
        /// <summary>Выдать предметы себе: падают прямо перед персонажем.</summary>
        public static int Give(System.Random r, int n, params string[] kinds)
        {
            if (Dry) { DryCalls++; return 0; } var me = MeAlive(); if (me == null) return 0; int ok = 0; string got = "";
            for (int i = 0; i < n; i++)
            {
                string it = PickItem(r, kinds[i % kinds.Length]); if (it == null) continue; Vector3 p = me.transform.position + me.transform.forward * 1.6f + Vector3.up * (1.4f + 0.3f * i) + me.transform.right * ((i % 3) - 1) * 0.5f;
                if (SpawnWorldItem(it, p)) { ok++; got += (got.Length > 0 ? ", " : "") + ItemTitleOf(it); }
            }
            if (ok > 0) Ui.Toast("Припасы: " + got, Ui.Good); return ok;
        }

        // ---------------- свет и туман ----------------
        static readonly Dictionary<Light, float> lightBase = new Dictionary<Light, float>(); static float nextLightScan, baseAmbient = -1f; static bool lightTouched;
        static bool fogSaved, fogWas; static FogMode fogModeWas; static float fogDensWas; static Color fogColWas; static float fogDens = -1f;
        public static void SetFog(float d) { fogDens = d; }
        static void TickLightFog(float dt)
        {
            float k = Prod(mLgt);
            if (k != 1f || lightTouched)
            {
                if (Time.time >= nextLightScan) { nextLightScan = Time.time + 8f; foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>()) if (l != null && l.type == LightType.Directional && !lightBase.ContainsKey(l)) lightBase[l] = l.intensity; }
                foreach (var kv in lightBase.ToList()) { if (kv.Key == null) { lightBase.Remove(kv.Key); continue; } kv.Key.intensity = kv.Value * k; }
                if (baseAmbient < 0f) baseAmbient = RenderSettings.ambientIntensity; RenderSettings.ambientIntensity = baseAmbient * Mathf.Lerp(1f, k, 0.8f); lightTouched = k != 1f;
            }
            if (fogDens > 0f)
            {
                if (!fogSaved) { fogSaved = true; fogWas = RenderSettings.fog; fogModeWas = RenderSettings.fogMode; fogDensWas = RenderSettings.fogDensity; fogColWas = RenderSettings.fogColor; }
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = fogDens; RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, new Color(0.55f, 0.6f, 0.66f), 0.05f);
            }
            else if (fogSaved) { fogSaved = false; RenderSettings.fog = fogWas; RenderSettings.fogMode = fogModeWas; RenderSettings.fogDensity = fogDensWas; RenderSettings.fogColor = fogColWas; }
        }

        // ---------------- прочее ----------------
        static float lastLog;
        public static void Log(string where, Exception e) { if (Time.realtimeSinceStartup - lastLog < 3f) return; lastLog = Time.realtimeSinceStartup; if (I != null) I.Logger.LogWarning(where + ": " + (e.InnerException ?? e).Message); }
        public static void SetFov(float f) { fovWant = f; }
        static float fovWant = -1f;
    }
}
