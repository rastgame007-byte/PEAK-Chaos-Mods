// Логика мода: движение, эффекты (аффликции), лазание, предметы, монстры, пранки, сеть.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        // ======================= состояние =======================
        // движение
        float speedMul = 1f, jumpMul = 1f, flySpeed = 25f, gravityScale = 0.3f;
        bool lowGravity, markers = true, flying;
        Vector3 flyVel, baseGravity;
        float baseForce = -1f, baseJump = -1f;
        bool quickClimb; float climbMul = 3f; bool climbWasOn;
        readonly Dictionary<string, Component> climbComps = new Dictionary<string, Component>();
        readonly Dictionary<string, float> baseClimb = new Dictionary<string, float>();
        float tpHeight = 50f;

        // эффекты
        float energyMove = 1.5f, energyClimb = 1.3f, effectDur = 15f, statusAmount = 0.5f;
        int lowGravStacks = 1;
        readonly Dictionary<string, bool> buffOn = new Dictionary<string, bool>();
        float nextBuffTick;

        // цели
        int targetActor = -1;                  // -2 — я, -1 — все друзья, иначе номер игрока Photon
        float launchForce = 8000f, skyHeight = 60f;

        // пранки
        float prankDur = 6f; bool burnReal;
        float nextBurn;

        // облик
        float sizeTarget = 1f, curSize = 1f, sizeSent = -1f, nextSizeSend, sizeSendAt;
        bool scoutMode, invisible, sizeWasActive;
        class RemoteSize { public float target = 1f, cur = 1f; }
        readonly Dictionary<int, RemoteSize> remoteSize = new Dictionary<int, RemoteSize>();
        readonly Dictionary<int, bool> haunters = new Dictionary<int, bool>();

        // хаос
        bool chaos;
        float chaosMin = 20f, chaosMax = 60f, nextChaos;
        bool evTrip = true, evLaunch = true, evSwap = true, evSleep, evSummon, evZombie, evScare = true, evGift, evEffect = true;

        // монстры
        int monsterCount = 3;
        readonly List<GameObject> spawned = new List<GameObject>();

        // предметы
        readonly List<string> itemNames = new List<string>();
        string itemFilter = "", itemStatus = "";
        int itemMode, itemCount = 1;
        bool itemsLoaded;

        // ======================= помощники =======================
        Component FindActor(int actor) { return Game.ByActor(actor); }

        List<Component> Targets(bool includeMe = true)
        {
            var l = new List<Component>();
            if (targetActor == -2) { var me = Game.Me(); if (me != null && includeMe) l.Add(me); }
            else if (targetActor == -1) l.AddRange(Game.Others());
            else { var c = FindActor(targetActor); if (c != null && !l.Contains(c)) l.Add(c); }
            return l;
        }

        void Rpc(Component c, string method, params object[] args)
        {
            if (c == null) return;
            string err = Game.Rpc(c, method, args);
            if (err != null) { Logger.LogWarning("RPC " + method + " не сработал: " + err); Ui.Toast("RPC " + method + " не сработал (см. консоль)", Ui.Bad); }
        }

        // ======================= движение =======================
        void ApplyMovement(Component me)
        {
            var mv = Game.Movement(me);
            if (mv == null) return;
            if (baseForce < 0f) { baseForce = R.Get<float>(mv, "movementForce"); baseJump = R.Get<float>(mv, "jumpImpulse"); }
            R.Set(mv, "movementForce", baseForce * speedMul);
            R.Set(mv, "jumpImpulse", baseJump * jumpMul);
        }

        static readonly string[] ClimbTypes = { "CharacterClimbing", "CharacterVineClimbing", "CharacterRopeHandling" };

        void ApplyClimb(Component me)
        {
            if (!quickClimb && !climbWasOn) return;
            foreach (var n in ClimbTypes)
            {
                if (!climbComps.TryGetValue(n, out var comp) || comp == null)
                {
                    var t = R.FindType(n);
                    comp = t == null ? null : (me.GetComponent(t) ?? me.GetComponentInChildren(t));
                    climbComps[n] = comp;
                }
                if (comp == null) continue;
                if (!baseClimb.ContainsKey(n)) baseClimb[n] = R.Get<float>(comp, "climbSpeedMod", 1f);
                R.Set(comp, "climbSpeedMod", quickClimb ? climbMul : baseClimb[n]);
            }
            climbWasOn = quickClimb;
        }

        void SetFly(bool on)
        {
            var me = Game.Me();
            if (me == null) { flying = false; return; }
            flying = on;
            foreach (var rb in Game.Rigs(me)) rb.useGravity = !on;
            if (!on) { flyVel = Vector3.zero; Game.HaltBody(me); }
            Ui.Toast(on ? "Полёт ВКЛ  (WASD, Пробел вверх, Ctrl вниз, Shift ускорение)" : "Полёт ВЫКЛ", on ? Ui.Good : Ui.Muted);
        }

        void Warp(Component c, Vector3 pos) { Rpc(c, "WarpPlayerRPC", pos, true); }

        // ======================= эффекты (аффликции) =======================
        struct Eff { public string id, cls, title, hint; public Eff(string i, string c, string t, string h) { id = i; cls = c; title = t; hint = h; } }

        static readonly Eff[] Buffs =
        {
            new Eff("energy",   "Affliction_FasterBoi",       "Энергетик (бесконечный)",   "Скорость бега и лазания — настраивается ниже"),
            new Eff("stamina",  "Affliction_InfiniteStamina", "Бесконечная выносливость",  "Как леденец, только навсегда"),
            new Eff("invinc",   "Affliction_Invincibility",   "Неуязвимость",               "Урон не проходит"),
            new Eff("nohunger", "Affliction_NoHunger",        "Не хочу есть",               "Голод не растёт"),
            new Eff("lowgrav",  "Affliction_LowGravity",      "Низкая гравитация (игровая)", "Высокие прыжки, плавное падение"),
            new Eff("sunscreen","Affliction_Sunscreen",       "Защита от солнца",           "Не обгораешь"),
            new Eff("chalk",    "Affliction_ClimbingChalk",   "Магнезия",                    "Лазание тратит меньше сил"),
            new Eff("djump",    "Affliction_DoubleJumpAmulet","Двойной прыжок",             "Прыжок в воздухе"),
        };

        static string ClsOf(string id)
        {
            foreach (var b in Buffs) if (b.id == id) return b.cls;
            switch (id)
            {
                case "heal": return "Affliction_HealAll";
                case "blind": return "Affliction_Blind";
                case "numb": return "Affliction_Numb";
                case "exhaust": return "Affliction_Exhaustion";
                case "chaosaff": return "Affliction_Chaos";
                case "bonus": return "Affliction_AddBonusStamina";
            }
            return null;
        }

        static Type AffType(string cls) { return R.FindType("Peak.Afflictions." + cls) ?? R.FindType(cls); }

        object MakeAff(string id, float dur)
        {
            string cls = ClsOf(id);
            var t = cls == null ? null : AffType(cls);
            if (t == null) return null;
            object a;
            switch (id)
            {
                case "energy":
                    a = R.New(t);
                    R.Set(a, "moveSpeedMod", energyMove); R.Set(a, "climbSpeedMod", energyClimb);
                    R.Set(a, "drowsyOnEnd", 0f); R.Set(a, "climbDelay", 0f);
                    break;
                case "stamina":
                    a = R.New(t, dur);
                    R.Set(a, "drowsyAffliction", null); R.Set(a, "climbDelay", 0f);
                    break;
                case "lowgrav": a = R.New(t, lowGravStacks, dur); break;
                case "sunscreen": a = R.New(t, dur); break;
                case "chalk": a = R.New(t); R.Set(a, "climbStaminaMultiplier", 0.5f); break;
                case "invinc": a = R.New(t); R.Set(a, "isFromMilk", false); R.Set(a, "isFromAmulet", false); break;
                case "exhaust": a = R.New(t); R.Set(a, "drainAmount", 1f); break;
                case "chaosaff": return R.New(t, 0.5f, 0.2f, 0.3f, 0.1f);
                case "heal": a = R.New(t); R.Set(a, "maxHealing", 1f); return a;
                case "bonus": return R.New(t, 1f, 0f);
                default: a = R.New(t); break;
            }
            if (a != null) R.Set(a, "totalTime", dur);
            return a;
        }

        bool AddAff(Component me, object aff)
        {
            var affs = Game.Afflictions(me);
            if (affs == null || aff == null) return false;
            if (R.TryInvoke(affs, "AddAffliction", out var _, aff, false)) return true;
            return R.TryInvoke(affs, "AddAffliction", out var _2, aff);
        }

        List<object> ActiveAffs(Component me, string cls)
        {
            var res = new List<object>();
            var src = R.Get(Game.Afflictions(me), "afflictionList") as IEnumerable;
            if (src == null) return res;
            foreach (var o in src) if (o != null && o.GetType().Name == cls) res.Add(o);
            return res;
        }

        void EnsureBuff(Component me, string id)
        {
            string cls = ClsOf(id);
            var cur = ActiveAffs(me, cls);
            if (cur.Count > 0)
            {
                foreach (var a in cur)
                {
                    float tt = R.Get<float>(a, "totalTime");
                    float el = R.Get<float>(a, "timeElapsed");
                    if (tt - el < 60000f) { R.Set(a, "totalTime", 99999f); R.Set(a, "timeElapsed", 0f); }
                    if (id == "energy") { R.Set(a, "moveSpeedMod", energyMove); R.Set(a, "climbSpeedMod", energyClimb); }
                }
                return;
            }
            AddAff(me, MakeAff(id, 99999f));
        }

        void ExpireBuff(Component me, string id)
        {
            var affs = Game.Afflictions(me);
            foreach (var a in ActiveAffs(me, ClsOf(id)))
            {
                bool removed = false;
                try { removed = R.TryInvoke(affs, "RemoveAffliction", out var _, a); } catch { }
                if (!removed) R.Set(a, "timeElapsed", R.Get<float>(a, "totalTime") + 1f);
            }
        }

        bool BuffOn(string id) { return buffOn.TryGetValue(id, out var v) && v; }

        void SetBuff(string id, bool on)
        {
            buffOn[id] = on;
            var me = Game.Me();
            if (me == null) return;
            Safe("эффект " + id, () => { if (on) EnsureBuff(me, id); else ExpireBuff(me, id); });
        }

        void TickBuffs(Component me)
        {
            if (Time.time < nextBuffTick) return;
            nextBuffTick = Time.time + 0.5f;
            foreach (var b in Buffs)
                if (BuffOn(b.id)) { try { EnsureBuff(me, b.id); } catch (Exception e) { SafeLog("баф " + b.id, e); } }
        }

        void ApplyOneShot(string id, float dur)
        {
            var me = Game.Me();
            if (me == null) return;
            var a = MakeAff(id, dur);
            if (a == null || !AddAff(me, a)) Ui.Toast("Эффект «" + id + "» не удалось выдать (см. консоль)", Ui.Bad);
        }

        void AddStatus(Component me, string status, float amount)
        {
            var affs = Game.Afflictions(me);
            if (affs == null || !R.TryInvoke(affs, "AddStatus", out var _, status, amount))
                Ui.Toast("AddStatus не найден (игра обновилась?)", Ui.Bad);
        }

        void ClearStatuses(Component me)
        {
            var affs = Game.Afflictions(me);
            if (affs != null && (R.TryInvoke(affs, "ClearAllStatus", out var _) || R.TryInvoke(affs, "ClearAll", out var _2)))
                Ui.Toast("Статусы очищены", Ui.Good);
            else Ui.Toast("Метод очистки не найден", Ui.Bad);
        }

        // ======================= предметы =======================
        void LoadItemsIfNeeded() { if (!itemsLoaded) LoadItems(); }

        Vector3 InFront(Component me, float dist)
        {
            var cam = Camera.main;
            if (cam != null) return cam.transform.position + cam.transform.forward * dist;
            return me.transform.position + me.transform.forward * dist + Vector3.up * 1.2f;
        }

        bool SpawnWorldItem(string name, Vector3 pos)
        {
            try
            {
                var go = Game.PhotonSpawn("InstantiateItemRoom", name, pos, Quaternion.identity);
                if (go != null) return true;
            }
            catch (Exception e) { SafeLog("InstantiateItemRoom", e); }
            try
            {
                var go = Game.PhotonSpawn("Instantiate", "0_Items/" + name, pos, Quaternion.identity);
                return go != null;
            }
            catch (Exception e) { SafeLog("Instantiate item", e); }
            return false;
        }

        void GiveItem(string name)
        {
            var me = Game.Me();
            if (me == null) { Ui.Toast("Нет персонажа", Ui.Bad); return; }
            int ok = 0;
            for (int i = 0; i < itemCount; i++)
            {
                bool done = false;
                if (itemMode == 0 && i == 0)
                {
                    var items = R.Get(R.Get(me, "refs"), "items");
                    try { done = items != null && R.TryInvoke(items, "SpawnItemInHand", out var _, name); } catch (Exception e) { SafeLog("SpawnItemInHand", e); }
                    if (!done) done = SpawnWorldItem(name, InFront(me, 2f) + Vector3.up * 0.3f);
                }
                else if (itemMode == 2)
                {
                    foreach (var c in Targets(false)) { if (SpawnWorldItem(name, c.transform.position + Vector3.up * 2f + UnityEngine.Random.insideUnitSphere * 0.4f)) done = true; }
                    if (Targets(false).Count == 0) { Ui.Toast("Выбери друга на вкладке «Друзья»/«Пранки»", Ui.Bad); return; }
                }
                else done = SpawnWorldItem(name, InFront(me, 2.2f) + Vector3.up * (0.4f + i * 0.35f) + UnityEngine.Random.insideUnitSphere * 0.3f);
                if (done) ok++;
            }
            Ui.Toast(ok > 0 ? "Выдано: " + ItemTitle(name) + (ok > 1 ? " ×" + ok : "") : "Не вышло выдать «" + name + "» (см. консоль)", ok > 0 ? Ui.Good : Ui.Bad);
        }

        // ======================= монстры =======================
        Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 8f, Vector3.down, out var hit, 40f)) return hit.point + Vector3.up * 0.6f;
            return p + Vector3.up * 0.6f;
        }

        Vector3 RingPos(Vector3 center, float min, float max)
        {
            var d = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(min, max);
            return Ground(center + new Vector3(d.x, 0, d.y));
        }

        void SpawnZombie(Vector3 pos)
        {
            var go = Game.PhotonSpawn("Instantiate", "MushroomZombie", pos, Quaternion.identity);
            if (go == null) throw new Exception("зомби не создан");
            spawned.Add(go);
        }

        void ZombiesAt(Vector3 center, int count, float minR, float maxR)
        {
            int ok = 0;
            for (int i = 0; i < count; i++)
            {
                try { SpawnZombie(RingPos(center, minR, maxR)); ok++; }
                catch (Exception e) { SafeLog("зомби", e); }
            }
            Ui.Toast(ok > 0 ? "Зомби призвано: " + ok : "Зомби не появились (см. консоль)", ok > 0 ? Ui.Good : Ui.Bad);
        }

        void ZombieAtCrosshair()
        {
            var me = Game.Me(); var cam = Camera.main;
            if (me == null || cam == null) return;
            Vector3 pos = cam.transform.position + cam.transform.forward * 6f;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 120f)) pos = hit.point + hit.normal * 0.6f;
            try { SpawnZombie(pos); Ui.Toast("Зомби призван", Ui.Good); }
            catch (Exception e) { SafeLog("зомби", e); Ui.Toast("Зомби не появился (см. консоль)", Ui.Bad); }
        }

        void SpawnScoutmaster(Component target)
        {
            var me = Game.Me();
            if (me == null || target == null) { Ui.Toast("Нет цели", Ui.Bad); return; }
            Vector3 pos = RingPos(target.transform.position, 18f, 26f);
            GameObject go = null;
            if (Game.InRoom)
            {
                if (!Game.IsMaster()) { Ui.Toast("Скаутмастер появляется только у ХОСТА лобби", Ui.Bad); return; }
                go = Game.PhotonSpawn("InstantiateRoomObject", "Character_Scoutmaster", pos, Quaternion.identity);
            }
            else
            {
                var prefab = Resources.Load<GameObject>("Character_Scoutmaster");
                if (prefab != null) go = Instantiate(prefab, pos, Quaternion.identity);
            }
            if (go == null) { Ui.Toast("Скаутмастер не создан (см. консоль)", Ui.Bad); return; }
            spawned.Add(go);
            StartCoroutine(ScoutChase(go, target));
            Ui.Toast("Скаутмастер идёт за " + Game.Name(target) + "!", Ui.Accent2);
        }

        IEnumerator ScoutChase(GameObject go, Component target)
        {
            yield return null;
            yield return new WaitForSeconds(0.4f);
            while (go != null && target != null)
            {
                try
                {
                    var comp = go.GetComponent("Scoutmaster");
                    if (comp != null) R.TryInvoke(comp, "SetCurrentTarget", out var _, target, 90f);
                }
                catch (Exception e) { SafeLog("SetCurrentTarget", e); yield break; }
                yield return new WaitForSeconds(80f);
            }
        }

        void ClearSpawned()
        {
            int n = 0;
            foreach (var go in spawned)
            {
                if (go == null) continue;
                try
                {
                    if (Game.InRoom && R.TryInvokeStatic(Game.NetType, "Destroy", out var _, go)) { n++; continue; }
                    Destroy(go); n++;
                }
                catch (Exception e) { SafeLog("удаление", e); }
            }
            spawned.Clear();
            Ui.Toast("Убрано: " + n, Ui.Good);
        }

        // ======================= друзья =======================
        void Trip(Component c) { Rpc(c, "RPCA_Fall", 3f); }

        void Launch(Component c)
        {
            Rpc(c, "RPCA_Fall", 3f);
            Vector3 dir = Vector3.up + new Vector3(UnityEngine.Random.Range(-.6f, .6f), 0f, UnityEngine.Random.Range(-.6f, .6f));
            Rpc(c, "RPCA_AddForceToBodyPart", 0, dir.normalized * launchForce);
        }

        void PassOut(Component c) { Rpc(c, "RPCA_PassOut"); }
        void WakeUp(Component c) { Rpc(c, "RPCA_UnPassOut"); }
        void Revive(Component c) { if (c != null) Rpc(c, "RPCA_ReviveAtPosition", c.transform.position + Vector3.up * 1.5f, false, -1); }
        void BringToMe(Component c) { var me = Game.Me(); if (me != null && c != null) Warp(c, me.transform.position + Vector3.up * 1.2f); }
        void GoTo(Component c) { var me = Game.Me(); if (me != null && c != null) Warp(me, c.transform.position + Vector3.up * 1.2f); }
        void ToSky(Component c) { if (c != null) Warp(c, c.transform.position + Vector3.up * skyHeight); }

        void SwapRandomPair()
        {
            var all = Game.Everyone(true);
            if (all.Count < 2) return;
            int a = UnityEngine.Random.Range(0, all.Count), b;
            do { b = UnityEngine.Random.Range(0, all.Count); } while (b == a);
            Vector3 pa = all[a].transform.position, pb = all[b].transform.position;
            Warp(all[a], pb + Vector3.up * .5f);
            Warp(all[b], pa + Vector3.up * .5f);
            Ui.Toast("ХАОС: " + Game.Name(all[a]) + " и " + Game.Name(all[b]) + " поменялись местами!");
        }

        // ======================= сетевые команды PeakFun =======================
        /// <summary>Отправить команду выбранным друзьям. Возвращает false, если слать некому/не в комнате.</summary>
        bool SendToTargets(string cmd, string arg)
        {
            if (targetActor == -2) return false;
            if (!Game.InRoom) { Ui.Toast("Ты не в лобби — слать некому", Ui.Bad); return false; }
            bool ok = Net.Send(cmd, arg, targetActor);
            if (!ok) Ui.Toast("Не удалось отправить (канал: " + Net.Status + ")", Ui.Bad);
            return ok;
        }

        static FxKind KindOf(string s)
        {
            try { return (FxKind)Enum.Parse(typeof(FxKind), s, true); } catch { return FxKind.Static; }
        }

        void Prank(FxKind kind) { PrankTo(targetActor, kind, prankDur); }

        void PrankTo(int actor, FxKind kind, float dur)
        {
            bool alone = Game.Others().Count == 0;
            if (actor == -2 || (actor == -1 && alone))
            {
                Fx.Start(kind, dur);
                if (actor == -1) Ui.Toast("Друзей в лобби нет — показал тебе: " + RuName(kind), Ui.Accent2);
                return;
            }
            if (!Game.InRoom) { Ui.Toast("Ты не в лобби — слать некому", Ui.Bad); return; }
            if (Net.Send("fx", kind + ":" + dur.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), actor))
                Ui.Toast("Отправлено: " + RuName(kind) + (actor == -1 ? " (всем)" : " → " + Game.PlayerName(actor)) + "  [у друзей должен быть PeakFun]", Ui.Accent2);
            else Ui.Toast("Не удалось отправить (канал: " + Net.Status + ")", Ui.Bad);
        }

        static string RuName(FxKind k)
        {
            switch (k)
            {
                case FxKind.Fire: return "огонь"; case FxKind.Static: return "помехи"; case FxKind.Death: return "«вы погибли»";
                case FxKind.Kick: return "фейк-кик"; case FxKind.Lag: return "плохая связь"; case FxKind.Black: return "тьма";
                case FxKind.Disco: return "диско"; case FxKind.Freeze: return "фриз игры"; case FxKind.Stutter: return "микрофризы";
                case FxKind.Slideshow: return "слайд-шоу"; case FxKind.Rubber: return "откат назад"; case FxKind.Disconnect: return "потеря соединения";
                case FxKind.Bsod: return "синий экран"; case FxKind.Crash: return "«PEAK перестал работать»"; case FxKind.Glitch: return "артефакты видеокарты";
                case FxKind.SteamDown: return "Steam без сети"; case FxKind.Combo: return "АДСКИЙ ЛАГ";
                case FxKind.FakeCrash: return "вылет на рабочий стол"; case FxKind.WinUpdate: return "обновление Windows"; case FxKind.Hack: return "взлом";
                case FxKind.Flash: return "светошумовая"; case FxKind.Quake: return "землетрясение"; case FxKind.Virus: return "вирусные окна"; case FxKind.Quit: return "НАСТОЯЩИЙ вылет";
                default: return "скример";
            }
        }

        void EffectToTargets(string id)
        {
            if (targetActor == -2)
            {
                var me = Game.Me(); if (me == null) return;
                ApplyEffectLocal(id, effectDur);
                return;
            }
            if (SendToTargets("aff", id + ":" + effectDur.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)))
                Ui.Toast("Отправлено: " + id + "  [у друга должен быть PeakFun]", Ui.Accent2);
        }

        static readonly string[] StatusIds = { "poison", "cold", "hot", "hunger", "curse", "drowsy", "spores", "web" };

        void ApplyEffectLocal(string id, float dur)
        {
            var me = Game.Me();
            if (me == null) return;
            switch (id)
            {
                case "poison": AddStatus(me, "Poison", statusAmount); break;
                case "cold": AddStatus(me, "Cold", statusAmount); break;
                case "hot": AddStatus(me, "Hot", statusAmount); break;
                case "hunger": AddStatus(me, "Hunger", statusAmount); break;
                case "curse": AddStatus(me, "Curse", statusAmount); break;
                case "drowsy": AddStatus(me, "Drowsy", statusAmount); break;
                case "spores": AddStatus(me, "Spores", statusAmount); break;
                case "web": AddStatus(me, "Web", statusAmount); break;
                case "clear": ClearStatuses(me); break;
                default: ApplyOneShot(id, dur); break;
            }
        }

        void OnNetCommand(string cmd, string arg, int sender) { HandleCommand(cmd, arg, sender, false); }

        void HandleCommand(string cmd, string arg, int sender, bool local)
        {
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
#if LITE
                // облегчённая версия: принимает только визуал героев; пранки и эффекты от друзей игнорируются
                if (cmd != "hero" && cmd != "gore" && cmd != "ring" && cmd != "beam" && cmd != "brk" && cmd != "dom" && cmd != "afx") return;
#endif
                if (cmd == "size")
                {
                    if (float.TryParse(arg, System.Globalization.NumberStyles.Float, inv, out var sz))
                    {
                        if (!remoteSize.TryGetValue(sender, out var rs)) remoteSize[sender] = rs = new RemoteSize();
                        rs.target = Mathf.Clamp(sz, 0.2f, 6f);
                    }
                    return;
                }
                if (cmd == "inv") { SetRemoteInv(sender, arg == "1"); return; }
                if (cmd == "hero") { var hp = arg.Split(':'); int hk, hv = 0; if (int.TryParse(hp[0], out hk) && hk >= 0 && hk <= 15) { remoteHero[sender] = (HeroKind)hk; if (hp.Length > 1) int.TryParse(hp[1], out hv); remoteVar[sender] = hv; } return; }
                if (cmd == "gore") { if (costumeSync) Gore.OnNet(arg); return; }
                if (cmd == "ring") { if (costumeSync) Gore.OnNetRing(arg); return; }
                if (cmd == "beam") { OnBeam(arg, sender); return; }
                if (cmd == "dom") { OnDomainNet(arg, sender); return; }
                if (cmd == "bb") { OnBallNet(arg, sender); return; }
                if (cmd == "br") { OnBrNet(arg, sender); return; }
                if (cmd == "wh") { var wp = arg.Split(','); int wi, wx, wa; if (wp.Length >= 3 && int.TryParse(wp[0], out wi) && int.TryParse(wp[1], out wx) && int.TryParse(wp[2], out wa)) WheelBegin(wi, wx, wa); return; }
                if (cmd == "rc") { OnRaceNet(arg, sender); return; }
                if (cmd == "wt") { OnWatcherNet(arg); return; }
                if (cmd == "afx") { OnAfx(arg, sender); return; }
                if (cmd == "brk") { if (costumeSync) Destruct.OnNet(arg); return; }
                if (cmd == "zone" || cmd == "mark" || cmd == "gtoast") { OnGameNet(cmd, arg, sender); return; }
                if (cmd == "scm") { haunters[sender] = arg == "1"; if (arg != "1") haunters.Remove(sender); return; }
                if (!local && !cfgAccept.Value) return;
                var parts = arg.Split(':');
                switch (cmd)
                {
                    case "fx":
                        float d = 6f;
                        if (parts.Length > 1) float.TryParse(parts[1], System.Globalization.NumberStyles.Float, inv, out d);
                        var fk = KindOf(parts[0]);
                        if (fk == FxKind.Quit && !local && !cfgAllowQuit.Value) fk = FxKind.Crash;   // настоящий выход — только с разрешения владельца
                        Fx.Start(fk, Mathf.Clamp(d, 1f, 30f));
                        break;
                    case "shade":
                        {
                            float sd = 30f;
                            if (parts.Length > 1) float.TryParse(parts[1], System.Globalization.NumberStyles.Float, inv, out sd);
                            ShadeKind sk; try { sk = (ShadeKind)Enum.Parse(typeof(ShadeKind), parts[0], true); } catch { sk = ShadeKind.None; }
                            Shade.Set(sk, Mathf.Clamp(sd, 3f, 180f));
                            if (sk != ShadeKind.None) Ui.Toast(Game.PlayerName(sender) + " включил тебе эффект «" + Shade.RuName(sk) + "» (F8 — убрать)", Ui.Accent2);
                            break;
                        }
                    case "aff":
                        float dd = 15f;
                        if (parts.Length > 1) float.TryParse(parts[1], System.Globalization.NumberStyles.Float, inv, out dd);
                        ApplyEffectLocal(parts[0], Mathf.Clamp(dd, 1f, 120f));
                        Ui.Toast(Game.PlayerName(sender) + " подкинул тебе эффект!", Ui.Accent2);
                        break;
                    case "toast":
                        Ui.Toast(arg);
                        break;
                }
            }
            catch (Exception e) { SafeLog("OnNetCommand", e); }
        }

        // ======================= облик / размер =======================
        void SetSize(float s)
        {
            sizeTarget = Mathf.Clamp(s, 0.25f, 5f);
            nextSizeSend = Time.time + 0.25f;
        }

        void TickSize(Component me)
        {
            curSize = Mathf.Lerp(curSize, sizeTarget, 1f - Mathf.Exp(-5f * Time.deltaTime));
            if (Mathf.Abs(curSize - sizeTarget) < 0.003f) curSize = sizeTarget;
            bool active = curSize != 1f || sizeTarget != 1f;
            if (active) me.transform.localScale = Vector3.one * curSize;
            else if (sizeWasActive) me.transform.localScale = Vector3.one;
            sizeWasActive = active;

            // рассылка друзьям с PeakFun
            if (Time.time >= nextSizeSend && sizeSent != sizeTarget && Game.InRoom)
            {
                Net.Send("size", sizeTarget.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), -1);
                sizeSent = sizeTarget; sizeSendAt = Time.time;
            }
            else if (sizeTarget != 1f && Time.time - sizeSendAt > 6f && Game.InRoom)
            {
                Net.Send("size", sizeTarget.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), -1);
                sizeSendAt = Time.time;
            }

            // размеры других игроков (у кого стоит PeakFun)
            List<int> done = null;
            foreach (var kv in remoteSize)
            {
                var c = Game.ByActor(kv.Key);
                if (c == null) { (done ?? (done = new List<int>())).Add(kv.Key); continue; }
                var rs = kv.Value;
                rs.cur = Mathf.Lerp(rs.cur, rs.target, 1f - Mathf.Exp(-5f * Time.deltaTime));
                if (Mathf.Abs(rs.cur - rs.target) < 0.003f) rs.cur = rs.target;
                c.transform.localScale = Vector3.one * rs.cur;
                if (rs.target == 1f && rs.cur == 1f) (done ?? (done = new List<int>())).Add(kv.Key);
            }
            if (done != null) foreach (var k in done) remoteSize.Remove(k);
        }

        void SetScoutMode(bool on)
        {
            scoutMode = on;
            if (on)
            {
                SetSize(2.3f);
                SetBuff("energy", true); SetBuff("invinc", true);
                if (Game.InRoom) Net.Send("scm", "1", -1);
                Ui.Toast("Режим великана-страшилы: большой, быстрый, неуязвимый. Друзья с PeakFun видят помехи рядом", Ui.Accent2);
            }
            else
            {
                SetSize(1f);
                SetBuff("energy", false); SetBuff("invinc", false);
                if (Game.InRoom) Net.Send("scm", "0", -1);
                Ui.Toast("Режим великана выключен");
            }
        }

        void TickProximity(Component me)
        {
            float best = 0f;
            if (cfgAccept.Value && haunters.Count > 0)
            {
                List<int> gone = null;
                foreach (var kv in haunters)
                {
                    var c = Game.ByActor(kv.Key);
                    if (c == null) { (gone ?? (gone = new List<int>())).Add(kv.Key); continue; }
                    float d = Vector3.Distance(me.transform.position, c.transform.position);
                    best = Mathf.Max(best, Mathf.Clamp01((80f - d) / 72f));
                }
                if (gone != null) foreach (var k in gone) haunters.Remove(k);
            }
            Fx.ProximityStatic = Mathf.Lerp(Fx.ProximityStatic, best * 0.85f, 1f - Mathf.Exp(-4f * Time.deltaTime));
        }

        void TickBurn(Component me)
        {
            if (!burnReal || !Fx.FireActive || Time.time < nextBurn) return;
            nextBurn = Time.time + 0.5f;
            AddStatus(me, "Hot", 0.03f);
        }

        // ======================= хаос =======================
        void SetChaos(bool on)
        {
            chaos = on;
            if (on) ScheduleChaos();
            Ui.Toast(on ? "РЕЖИМ ХАОСА ВКЛЮЧЁН" : "Режим хаоса выключен", on ? Ui.Accent2 : Ui.Muted);
        }

        void ScheduleChaos() { nextChaos = Time.time + UnityEngine.Random.Range(chaosMin, Mathf.Max(chaosMin + 1f, chaosMax)); }

        void SendTo(Component c, string cmd, string arg)
        {
            if (c == null) return;
            if (Game.IsMine(c)) { OnNetCommandLocal(cmd, arg); return; }
            Net.Send(cmd, arg, Game.ActorOf(c));
        }

        void OnNetCommandLocal(string cmd, string arg)
        {
            HandleCommand(cmd, arg, -9, true);
        }

        void RunChaosEvent()
        {
            var all = Game.Everyone(true);
            if (all.Count == 0) return;
            var victim = all[UnityEngine.Random.Range(0, all.Count)];
            string vn = Game.Name(victim);
            var pool = new List<Action>();
            if (evTrip) pool.Add(() => { Trip(victim); Ui.Toast("ХАОС: " + vn + " споткнулся"); });
            if (evLaunch) pool.Add(() => { Launch(victim); Ui.Toast("ХАОС: " + vn + " запущен в космос"); });
            if (evSleep) pool.Add(() => { PassOut(victim); Ui.Toast("ХАОС: " + vn + " уснул"); });
            if (evSwap && all.Count >= 2) pool.Add(SwapRandomPair);
            if (evSummon && all.Count >= 2) pool.Add(() => { foreach (var c in Game.Others()) BringToMe(c); Ui.Toast("ХАОС: все сбежались к " + Game.Name(Game.Me())); });
            if (evZombie) pool.Add(() => { ZombiesAt(victim.transform.position, 2, 5f, 9f); Ui.Toast("ХАОС: зомби возле " + vn); });
            if (evScare) pool.Add(() =>
            {
                var kinds = new[] { FxKind.Scream, FxKind.Static, FxKind.Death, FxKind.Lag, FxKind.Black, FxKind.Fire, FxKind.Disco, FxKind.Freeze, FxKind.Stutter, FxKind.Rubber, FxKind.Glitch, FxKind.Disconnect };
                var k = kinds[UnityEngine.Random.Range(0, kinds.Length)];
                SendTo(victim, "fx", k + ":5");
                Ui.Toast("ХАОС: пугалка для " + vn);
            });
            if (evEffect) pool.Add(() =>
            {
                var ids = new[] { "blind", "numb", "exhaust", "chaosaff", "poison", "cold", "hot", "energy", "heal" };
                var id = ids[UnityEngine.Random.Range(0, ids.Length)];
                SendTo(victim, "aff", id + ":12");
                Ui.Toast("ХАОС: эффект «" + id + "» для " + vn);
            });
            if (evGift && itemNames.Count > 0) pool.Add(() =>
            {
                var item = itemNames[UnityEngine.Random.Range(0, itemNames.Count)];
                SpawnWorldItem(item, victim.transform.position + Vector3.up * 2f);
                Ui.Toast("ХАОС: " + vn + " получает «" + ItemTitle(item) + "»");
            });
            if (pool.Count > 0) pool[UnityEngine.Random.Range(0, pool.Count)]();
        }
    }
}
