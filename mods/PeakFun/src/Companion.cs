// Бот-спутник 1.5: «нормальный игрок», а не зомби и не робот.
//  • появляется настоящим персонажем игрока (префаб Character) с именем и ником над головой; если не вышло — запасной «зомби-скаут» с выключенным ИИ;
//  • идёт за игроком плавно: разгоняется и тормозит, не липнет, держится сбоку-сзади и не заслоняет обзор;
//  • НИКАК не реагирует на то, что на него смотрят: ни прыжков, ни резких поворотов, ни «приветствий» — просто занят своими делами;
//  • в покое смотрит туда же, куда смотрит игрок, изредка поглядывает на него и по сторонам (медленно, без дрожи);
//  • прыгает только когда это нужно (преграда), лезет по стене за игроком, а руку тянет только тому, кто висит или падает рядом.
// Управление идёт через ввод самого персонажа (Character.input), без «рывков» и телепортов, кроме случая «потерялся/застрял надолго».
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        GameObject npc; Component npcChar; string npcName = "Толик";
        Component bMe;                      // настоящий локальный персонаж (чтобы игра не «перехватила» управление ботом)

        // настройки поведения
        bool botFollow = true, botJump = true, botReach = true, botEye = true, botRealPlayer = false;   // «настоящий Character» — экспериментально, по умолчанию выключено

        // «мозг»
        Component bBuddy; float bBuddyUntil;
        float bStay = 3.2f;
        Vector3 bSlot; float bSlotUntil;
        float bYaw, bPitch, bYawV, bPitchV;
        bool bLookInit;
        float bGlanceUntil, bNextGlance; Vector3 bGlancePos; bool bGlanceHas; float bGlanceYaw, bGlancePitch;
        float bIdleYaw;
        float bJumpHold; bool bJumpFire; float bJumpCheckAt = -1f;
        float bReachUntil, bReachCool; bool bReachActive, bReachFirst;
        float bMoveF;
        Vector3 bStuckPos; float bStuckT, bStuckTotal, bSidestepUntil; float bSidestepDir = 1f;
        float bClimbCool, bLastJump, bClimbJumpAt;
        bool bMoving;

        // ---------------- ник над головой (Harmony) ----------------
        static Component patchBot; static string patchName = "Толик";
        static bool namePatched; public static bool NamePatchOk;

        void InstallNamePatch()
        {
            if (namePatched) return; namePatched = true;
            try
            {
                var h = new Harmony("com.friends.peakfun.botname");
                var ct = R.FindType("Character");
                var get = ct != null ? ct.GetMethod("get_characterName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly) : null;
                if (get != null)
                {
                    h.Patch(get, null, new HarmonyMethod(typeof(PeakFunPlugin).GetMethod("NamePost", BindingFlags.Static | BindingFlags.NonPublic)));
                    NamePatchOk = true;
                }
                var ut = R.FindType("UIPlayerNames");
                var upd = ut != null ? ut.GetMethod("UpdateName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly) : null;
                if (upd != null)
                    h.Patch(upd, null, new HarmonyMethod(typeof(PeakFunPlugin).GetMethod("TagPost", BindingFlags.Static | BindingFlags.NonPublic)));
            }
            catch (Exception e) { NamePatchOk = false; Debug.LogWarning("[PeakFun] ник бота: " + e.Message); }
        }

        static void NamePost(object __instance, ref string __result)
        {
            try { if (patchBot != null && ReferenceEquals(__instance, patchBot)) __result = patchName; } catch { }
        }

        static void TagPost(object __instance, int index)
        {
            try
            {
                if (patchBot == null) return;
                var arr = R.Get(__instance, "playerNameText") as Array;
                if (arr == null || index < 0 || index >= arr.Length) return;
                var pn = arr.GetValue(index); if (pn == null) return;
                var ci = R.Get(pn, "characterInteractable"); if (ci == null) return;
                var chr = R.Get(ci, "character");
                if (chr == null || !ReferenceEquals(chr, patchBot)) return;
                var txt = R.Get(pn, "text");
                if (txt != null && !Equals(R.Get(txt, "text"), patchName)) R.Set(txt, "text", patchName);
                var star = R.Get(pn, "hostStar") as GameObject; if (star != null && star.activeSelf) star.SetActive(false);
            }
            catch { }
        }

        // ---------------- создание / удаление ----------------
        void SpawnCompanion()
        {
            var me = Game.Me();
            if (me == null) { Ui.Toast("Нет персонажа", Ui.Bad); return; }
            if (npc != null) { Ui.Toast("Бот уже подключён — сначала отключи", Ui.Bad); return; }
            Vector3 pos = Ground(me.transform.position - me.transform.forward * 3.5f);
            GameObject go = null; string used = null;
            bool master = !Game.InRoom || Game.IsMaster();
            var tries = new List<KeyValuePair<string, string>>();
            if (botRealPlayer) tries.Add(new KeyValuePair<string, string>("Instantiate", "Character"));
            if (master) tries.Add(new KeyValuePair<string, string>("InstantiateRoomObject", "MushroomZombie_Player"));
            tries.Add(new KeyValuePair<string, string>("Instantiate", "MushroomZombie_Player"));
            tries.Add(new KeyValuePair<string, string>("Instantiate", "MushroomZombie"));
            bMe = me;
            patchName = BotName();
            InstallNamePatch();
            foreach (var t in tries)
            {
                Logger.LogInfo("[PeakFun] Бот: пробую PhotonNetwork." + t.Key + "(\"" + t.Value + "\")");
                try { go = Game.PhotonSpawn(t.Key, t.Value, pos, Quaternion.identity); }
                catch (Exception e) { SafeLog("бот " + t.Value, e); go = null; }
                if (go != null) { used = t.Key + "/" + t.Value; break; }
            }
            if (go == null) { Ui.Toast("Бот не появился (см. консоль)", Ui.Bad); return; }
            npc = go; npcChar = null;
            spawned.Add(go);
            SetupBot(go, me);
            StartCoroutine(PrepareCompanion(go, me, used));
            bStay = UnityEngine.Random.Range(2.8f, 4.0f);
            bLookInit = false; bBuddy = null; bMoveF = 0f; bSlotUntil = 0f;
            bNextGlance = Time.time + UnityEngine.Random.Range(3f, 6f);
            bReachCool = Time.time + 4f;
            string nm = BotName();
            Ui.Toast(nm + " подключился к игре" + (used != null && used.EndsWith("/Character") ? "" : " (запасной облик)"), Ui.Good);
            if (Game.InRoom) Net.Send("toast", nm + " подключился к игре", -1);
        }

        string BotName() { return string.IsNullOrWhiteSpace(npcName) ? "Толик" : npcName.Trim(); }

        void RestoreMe(Component me)
        {
            if (me == null) return;
            try
            {
                var ct = R.FindType("Character");
                me.gameObject.SetActive(true);
                if (ct != null && !ReferenceEquals(R.GetStatic(ct, "localCharacter"), me)) R.SetStatic(ct, "localCharacter", me);
                var pt = R.FindType("Player");
                var lp = pt != null ? R.GetStatic(pt, "localPlayer") : null;
                if (lp != null && !ReferenceEquals(R.Get(lp, "character"), me)) R.Set(lp, "character", me);
            }
            catch { }
        }

        /// <summary>Синхронная подготовка сразу после создания: выключаем ИИ зомби и укусы, возвращаем управление и голос себе.</summary>
        void SetupBot(GameObject go, Component me)
        {
            var ct = R.FindType("Character");
            Component ch = null;
            try { if (ct != null) ch = go.GetComponent(ct) ?? go.GetComponentInChildren(ct); } catch { }
            RestoreMe(me);
            try
            {
                var recT = R.FindType("Photon.Voice.Unity.Recorder");
                if (recT != null)
                {
                    foreach (var rc in go.GetComponentsInChildren(recT, true)) if (rc != null) Destroy(rc);
                    var myRec = me != null ? me.GetComponentInChildren(recT) : null;
                    var vch = R.FindType("VoiceClientHandler");
                    if (myRec != null && vch != null) R.TryInvokeStatic(vch, "LocalPlayerAssigned", out var _, myRec);
                }
            }
            catch (Exception e) { SafeLog("голос бота", e); }
            int off = 0;
            try
            {
                foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (b == null) continue;
                    string n = b.GetType().Name;
                    if (n == "MushroomZombieBiteCollider") { Destroy(b); off++; }
                    else if (n == "CharacterVoiceHandler") { b.enabled = false; off++; }
                    else if (n == "MushroomZombie")
                    {
                        R.Set(b, "isNPCZombie", false);
                        R.Set(b, "currentState", 2);
                        R.Set(b, "currentTarget", null);
                        var bc = R.Get(b, "biteColliderObject") as GameObject;
                        if (bc != null) { bc.SetActive(false); Destroy(bc); }
                        b.enabled = false; off++;
                    }
                    else if (n == "Mob" || n == "MushroomZombieAI") { b.enabled = false; off++; }
                }
            }
            catch (Exception e) { SafeLog("ИИ бота", e); }
            try
            {
                if (ch != null)
                {
                    R.Set(ch, "isBot", true);
                    foreach (var listName in new[] { "AllBotCharacters", "AllCharacters" })
                    {
                        var lst = R.GetStatic(ct, listName) as IList;
                        if (lst != null && !lst.Contains(ch)) lst.Add(ch);
                    }
                    if (me != null)
                    {
                        var all = R.GetStatic(ct, "AllCharacters") as IList;
                        if (all != null && !all.Contains(me)) all.Add(me);
                    }
                    var inp = R.Get(ch, "input") as Behaviour;
                    if (inp != null) inp.enabled = false;
                    R.Set(ch, "infiniteStam", true);
                    var data = R.Get(ch, "data");
                    R.Set(data, "currentStamina", 1f); R.Set(data, "extraStamina", 1f);
                    var cust = ch.GetComponent("CharacterCustomization");
                    if (cust != null) R.CallNoArgs(cust, "ShowAllRenderers");
                    npcChar = ch; patchBot = ch; patchName = BotName();
                }
            }
            catch (Exception e) { SafeLog("настройка бота", e); }
            Logger.LogInfo("Бот-спутник: Character=" + (ch != null) + ", отключено компонентов: " + off + ", ник-патч=" + NamePatchOk);
        }

        IEnumerator PrepareCompanion(GameObject go, Component me, string used)
        {
            yield return null; yield return null; yield return new WaitForSeconds(0.5f);
            if (go == null) yield break;
            try
            {
                if (npcChar == null) SetupBot(go, me);
                else
                {
                    foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (b == null) continue;
                        string n = b.GetType().Name;
                        if (n == "MushroomZombieBiteCollider") Destroy(b);
                        else if (n == "MushroomZombie" && b.enabled) b.enabled = false;
                    }
                    RestoreMe(me);
                }
                Logger.LogInfo("Бот-спутник готов: " + used);
            }
            catch (Exception e) { SafeLog("подготовка бота", e); }
        }

        void RemoveCompanion()
        {
            if (npc == null) return;
            string nm = BotName();
            patchBot = null;
            try
            {
                var ct = R.FindType("Character");
                foreach (var listName in new[] { "AllBotCharacters", "AllCharacters" })
                {
                    var lst = R.GetStatic(ct, listName) as IList;
                    if (lst != null && npcChar != null && lst.Contains(npcChar)) lst.Remove(npcChar);
                }
            }
            catch { }
            try
            {
                if (!(Game.InRoom && R.TryInvokeStatic(Game.NetType, "Destroy", out var _, npc))) Destroy(npc);
            }
            catch (Exception e) { SafeLog("удаление бота", e); }
            spawned.Remove(npc);
            npc = null; npcChar = null; bBuddy = null; bLookInit = false;
            RestoreMe(bMe);
            Ui.Toast(nm + " отключился", Ui.Muted);
            if (Game.InRoom) Net.Send("toast", nm + " отключился от игры", -1);
        }

        // ---------------- вспомогательное ----------------
        Vector3 NpcPos()
        {
            if (npcChar != null) { var c = R.Get(npcChar, "Center"); if (c is Vector3 v) return v; return npcChar.transform.position; }
            return npc != null ? npc.transform.position : Vector3.zero;
        }

        internal static Vector3 CenterOf(Component c) { var v = R.Get(c, "Center"); return v is Vector3 p ? p : c.transform.position; }
        internal static Vector3 HeadOf(Component c) { var v = R.Get(c, "Head"); return v is Vector3 p ? p : c.transform.position + Vector3.up * 1.5f; }
        static float Yaw(Vector3 d) { return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
        static float PitchTo(Vector3 from, Vector3 to)
        {
            float dd = Mathf.Max(0.5f, Vector2.Distance(new Vector2(to.x, to.z), new Vector2(from.x, from.z)));
            return -Mathf.Atan2(to.y - from.y, dd) * Mathf.Rad2Deg;
        }

        Vector3 GroundIgnoring(Vector3 p, Transform ignore)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * 4f, Vector3.down, 14f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            var ct = R.FindType("Character");
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.isTrigger) continue;
                if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
                if (ct != null && h.collider.GetComponentInParent(ct) != null) continue;
                return h.point + Vector3.up * 1.0f;
            }
            return p;
        }

        static int TerrainMask()
        {
            try
            {
                var hf = R.FindType("HelperFunctions");
                var lt = R.FindType("HelperFunctions+LayerType");
                if (hf != null && lt != null && R.TryInvokeStatic(hf, "GetMask", out var m, Enum.ToObject(lt, 1)))
                {
                    if (m is LayerMask lm) return lm.value;
                    if (m is int i) return i;
                }
            }
            catch { }
            return ~0;
        }

        void BotLook(float yaw, float pitch)
        {
            var data = R.Get(npcChar, "data"); if (data == null) return;
            R.Set(data, "lookValues", new Vector2(yaw, pitch));
            R.Set(data, "lookDirection", Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward);
            R.Set(data, "lookDirection_Right", Quaternion.Euler(0f, yaw + 90f, 0f) * Vector3.forward);
            R.Set(data, "lookDirection_Flat", Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
            try { R.CallNoArgs(npcChar, "RecalculateLookDirections"); } catch { }
            // корпус поворачиваем мягко (а не «щёлкаем» каждый кадр) — иначе его трясёт
            var want = Quaternion.Euler(0f, yaw, 0f);
            npcChar.transform.rotation = Quaternion.Slerp(npcChar.transform.rotation, want, Mathf.Clamp01(Time.deltaTime * 9f));
        }

        /// <summary>Один честный прыжок: нажатие кнопки; RPC — только если игра сама прыжок не начала.</summary>
        void BotJump()
        {
            bJumpFire = true; bJumpHold = Time.time + 0.16f;
            bLastJump = Time.time; bJumpCheckAt = Time.time + 0.3f;
        }

        void BotReach(float seconds)
        {
            if (!botReach) return;
            if (Time.time < bReachUntil) { bReachUntil = Mathf.Max(bReachUntil, Time.time + seconds); return; }
            bReachUntil = Time.time + seconds; bReachFirst = true;
        }

        void BotJumpTick(object input, object data, float now)
        {
            bool fire = bJumpFire; bJumpFire = false;
            R.Set(input, "jumpWasPressed", fire);
            R.Set(input, "jumpIsPressed", now < bJumpHold);
            if (bJumpCheckAt > 0f && now >= bJumpCheckAt)
            {
                bJumpCheckAt = -1f;
                if (R.Get<float>(data, "sinceJump", 9f) > 0.35f) { try { Game.Rpc(npcChar, "JumpRpc", false); } catch { } }
            }
        }

        // ---------------- главный цикл (каждый кадр) ----------------
        void TickCompanion()
        {
            if (npc == null) { npcChar = null; return; }
            // игра может отдать «локального персонажа» боту (он создан нашим клиентом) — возвращаем себе
            if (bMe != null && Time.frameCount % 15 == 0)
            {
                var ct0 = R.FindType("Character");
                if (ct0 != null && !ReferenceEquals(R.GetStatic(ct0, "localCharacter"), bMe)) RestoreMe(bMe);
            }
            if (npcChar == null) return;
            var data = R.Get(npcChar, "data");
            var input = R.Get(npcChar, "input");
            if (data == null || input == null) return;
            float now = Time.time, dt = Mathf.Min(Time.deltaTime, 0.05f);

            if (R.Get<bool>(data, "dead") || R.Get<bool>(data, "passedOut") || R.Get<bool>(data, "fullyPassedOut"))
            {
                R.Set(input, "movementInput", Vector2.zero); R.Set(input, "sprintIsPressed", false);
                R.Set(input, "useSecondaryIsPressed", false); bMoveF = 0f;
                return;
            }
            R.Set(npcChar, "infiniteStam", true);
            R.Set(data, "currentStamina", 1f);

            Vector3 pos = NpcPos(), head = HeadOf(npcChar);
            if (!bLookInit)
            {
                var lv = R.Get<Vector2>(data, "lookValues");
                bYaw = lv.x != 0f ? lv.x : npcChar.transform.eulerAngles.y; bPitch = lv.y; bIdleYaw = bYaw; bLookInit = true;
            }
            BotJumpTick(input, data, now);

            // ----- 1. за кем идти: держимся одного игрока, меняем редко -----
            var players = new List<Component>();
            foreach (var c in Game.Everyone(true)) if (c != npcChar && !IsDeadOrGhost(c)) players.Add(c);
            if (bBuddy != null && !players.Contains(bBuddy)) bBuddy = null;
            if (bBuddy == null || now > bBuddyUntil)
            {
                Component pick = null; float bd = float.MaxValue;
                foreach (var c in players)
                {
                    float d = Vector3.Distance(CenterOf(c), pos);
                    if (c == bBuddy) d *= 0.6f;      // предпочитаем текущего
                    if (d < bd) { bd = d; pick = c; }
                }
                bBuddy = pick; bBuddyUntil = now + UnityEngine.Random.Range(50f, 110f);
            }

            bool grounded = R.Get<bool>(data, "isGrounded");
            bool climbing = R.Get<bool>(data, "isClimbing");
            Vector2 move = Vector2.zero; bool sprint = false;
            float lookYawT = bYaw, lookPitchT = 0f; float smooth = 0.28f;

            if (bBuddy == null)
            {
                bMoveF = Mathf.MoveTowards(bMoveF, 0f, dt * 3f);
                R.Set(input, "movementInput", new Vector2(0f, bMoveF)); R.Set(input, "sprintIsPressed", false);
                IdleLook(now, head, null, ref lookYawT, ref lookPitchT);
                ApplyLook(lookYawT, lookPitchT, dt, 0.35f);
                UpdateReach(input, data);
                return;
            }

            Vector3 bc = CenterOf(bBuddy), bh = HeadOf(bBuddy);
            Vector3 toB = bc - pos; Vector3 flatToB = new Vector3(toB.x, 0, toB.z);
            float dist = flatToB.magnitude, dy = bc.y - pos.y;

            // ----- 2. где стоять: сбоку-сзади от игрока, чтобы не маячить перед камерой -----
            if (now > bSlotUntil)
            {
                bSlotUntil = now + UnityEngine.Random.Range(10f, 18f);
                Vector3 bf = R.Get<Vector3>(R.Get(bBuddy, "data"), "lookDirection_Flat");
                if (bf.sqrMagnitude < 0.01f) bf = bBuddy.transform.forward; bf.y = 0f; bf.Normalize();
                Vector3 sideV = Vector3.Cross(Vector3.up, bf) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                bSlot = -bf * UnityEngine.Random.Range(0.4f, 1.4f) + sideV * UnityEngine.Random.Range(1.0f, 2.0f);
            }
            Vector3 goal = bc + (dist < 9f ? bSlot : Vector3.zero);
            Vector3 toGoal = goal - pos; toGoal.y = 0;
            float gd = toGoal.magnitude;
            bool wantMove = botFollow && (dist > bStay + 1.6f || (bMoving && (dist > bStay - 0.3f || gd > 2.6f) && dist > 1.8f));
            bMoving = wantMove;

            // ----- 3. лазание -----
            Vector3 fwdDir = gd > 0.05f ? toGoal / gd : npcChar.transform.forward;
            int mask = TerrainMask();
            if (climbing)
            {
                bool wall = Physics.Raycast(pos + Vector3.up * 0.8f, npcChar.transform.forward, 1.8f, mask);
                float side = Mathf.Clamp(npcChar.transform.InverseTransformPoint(bh).x * 0.35f, -1f, 1f);
                if (wall)
                {
                    if (Mathf.Abs(side) < 0.2f) side = (now % 2f < 1f) ? 0.7f : -0.7f;
                    if (R.Get<float>(data, "sinceClimbJump", 9f) > 0.8f && now > bClimbJumpAt)
                    {
                        try { Game.Rpc(npcChar, "RPCA_ClimbJump"); } catch { }
                        R.Set(data, "sinceClimbJump", 0f);
                        R.Set(input, "jumpWasPressed", true); bClimbJumpAt = now + 0.9f;
                        try { var rag = Game.Ragdoll(npcChar); R.TryInvoke(rag, "MoveAllRigsInDirection", out var _, Vector3.up * 0.5f + npcChar.transform.forward * 0.4f); } catch { }
                    }
                }
                move = new Vector2(side, 1f); sprint = true;
                lookYawT = Yaw(flatToB.sqrMagnitude > 0.01f ? flatToB : npcChar.transform.forward); lookPitchT = -15f;
                var anim = R.Get(R.Get(npcChar, "refs"), "animator");
                if (anim != null)
                {
                    try { R.TryInvoke(anim, "SetBool", out var _, "Climb Surface", true); R.TryInvoke(anim, "SetBool", out var _, "Is Grounded", false); R.TryInvoke(anim, "SetFloat", out var _, "Input Y", 1f); } catch { }
                }
                if (dy <= 0.2f)
                {   // наверху — слезаем вперёд
                    try { R.CallNoArgs(R.Get(R.Get(npcChar, "refs"), "climbing"), "StopClimbing"); } catch { }
                    try { Game.Rpc(npcChar, "StopClimbingRpc", 0f); } catch { }
                    try { var rag = Game.Ragdoll(npcChar); R.TryInvoke(rag, "MoveAllRigsInDirection", out var _, npcChar.transform.forward * 0.9f + Vector3.up * 0.35f); } catch { }
                    R.Set(data, "hasClimbedSinceGrounded", false); R.Set(data, "isClimbing", false);
                    bClimbCool = now + 1f; bLastJump = now + 2f;
                    var an2 = R.Get(R.Get(npcChar, "refs"), "animator");
                    try { R.TryInvoke(an2, "SetBool", out var _, "Climb Surface", false); R.TryInvoke(an2, "SetBool", out var _, "Is Grounded", true); } catch { }
                }
                bMoveF = 1f;
                R.Set(input, "movementInput", move); R.Set(input, "sprintIsPressed", sprint);
                ApplyLook(lookYawT, lookPitchT, dt, 0.15f);
                UpdateReach(input, data);
                return;
            }

            bool wallAhead = false; RaycastHit wh = default(RaycastHit);
            if (wantMove)
            {
                wallAhead = Physics.Raycast(pos, fwdDir, out wh, 2.2f, mask)
                         || Physics.Raycast(pos + Vector3.up * 0.6f, fwdDir, out wh, 2.2f, mask);
                if (!wallAhead) { wallAhead = Physics.Raycast(pos - Vector3.up * 0.5f, fwdDir, out wh, 2.2f, mask); }
            }
            if (wantMove && wallAhead && now > bClimbCool && R.Get<float>(data, "sinceClimb", 9f) > 0.5f && (dy > 0.9f || (dist < 3f && dy > 0.5f)))
            {
                try
                {
                    Game.Rpc(npcChar, "StartClimbRpc", wh.point, wh.normal);
                    R.Set(data, "isClimbing", true); R.Set(data, "hasClimbedSinceGrounded", false);
                    R.Set(input, "jumpWasPressed", false); R.Set(input, "jumpIsPressed", false);
                }
                catch (Exception e) { SafeLog("лазание бота", e); }
                bClimbCool = now + 1.5f;
            }

            // ----- 4. движение -----
            float target = 0f;
            if (wantMove)
            {
                float moveYaw = Yaw(fwdDir);
                bStuckT += dt;
                if (bStuckT > 1.2f)
                {
                    float prog = Vector3.Distance(new Vector3(pos.x, 0, pos.z), new Vector3(bStuckPos.x, 0, bStuckPos.z));
                    if (prog < 0.5f)
                    {
                        bStuckTotal += bStuckT;
                        if (grounded && botJump && now > bLastJump + 0.9f) BotJump();
                        if (bStuckTotal > 3f) { bSidestepUntil = now + 1.1f; bSidestepDir = UnityEngine.Random.value < 0.5f ? -1f : 1f; bStuckTotal = Mathf.Min(bStuckTotal, 5f); }
                    }
                    else bStuckTotal = 0f;
                    bStuckPos = pos; bStuckT = 0f;
                }
                bool lost = dist > 60f || Mathf.Abs(dy) > 28f || bStuckTotal > 9f;
                if (lost) { CatchUp(bc, pos); bStuckTotal = 0f; }

                // сначала довернуться, потом идти: так ходят люди, и тело не «ломается» на ходу
                lookYawT = moveYaw; smooth = 0.22f; bIdleYaw = bYaw;
                float delta = Mathf.Abs(Mathf.DeltaAngle(bYaw, moveYaw));
                float aligned = delta < 40f ? 1f : (delta < 90f ? 0.3f : 0f);
                float speed = dist > 10f ? 1f : Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(bStay, 8f, dist));
                target = aligned * speed;
                sprint = dist > 13f || dy > 2.4f;
                lookPitchT = Mathf.Clamp(PitchTo(head, bh) * 0.4f, -12f, 12f);
                // низкая преграда впереди — перешагнуть прыжком (единственный «прыжок без причины» — причина есть)
                if (grounded && botJump && now > bLastJump + 1.2f && now > bClimbCool)
                {
                    bool low = Physics.Raycast(pos - Vector3.up * 0.5f, fwdDir, 1.2f, mask) && !Physics.Raycast(pos + Vector3.up * 0.4f, fwdDir, 1.5f, mask);
                    if (low) BotJump();
                }
            }
            else
            {
                bStuckT = 0f; bStuckTotal = 0f; bStuckPos = pos;
                IdleLook(now, head, bBuddy, ref lookYawT, ref lookPitchT);
                smooth = 0.4f;
            }
            bMoveF = Mathf.MoveTowards(bMoveF, target, dt * (target > bMoveF ? 2.6f : 4.5f));
            move = new Vector2(0f, bMoveF);
            if (now < bSidestepUntil && wantMove) move = new Vector2(bSidestepDir * 0.8f, 0.6f);

            // ----- 5. помощь: рука тому, кто висит или падает рядом (и больше никогда без причины) -----
            if (botReach && dist < 4.5f && now > bReachCool && now > bReachUntil)
            {
                var bd = R.Get(bBuddy, "data");
                bool bClimb = R.Get<bool>(bd, "isClimbing");
                bool bFalling = !R.Get<bool>(bd, "isGrounded") && R.Get<Vector3>(bd, "avarageVelocity").y < -4f;
                if (bClimb || bFalling) { BotReach(2.4f); bReachCool = now + 7f; }
            }
            if (now < bReachUntil && !wantMove) { lookYawT = Yaw(bh - head); lookPitchT = PitchTo(head, bh); smooth = 0.3f; }

            R.Set(input, "movementInput", move);
            R.Set(input, "sprintIsPressed", sprint);
            ApplyLook(lookYawT, lookPitchT, dt, smooth);
            UpdateReach(input, data);
        }

        void ApplyLook(float yawT, float pitchT, float dt, float smooth)
        {
            // мёртвая зона: мелкие отклонения игнорируем — иначе голову «трясёт»
            if (Mathf.Abs(Mathf.DeltaAngle(bYaw, yawT)) < 1.2f && Mathf.Abs(bYawV) < 8f) yawT = bYaw;
            bYaw = Mathf.SmoothDampAngle(bYaw, yawT, ref bYawV, smooth, 200f, dt);
            bPitch = Mathf.SmoothDamp(bPitch, Mathf.Clamp(pitchT, -60f, 60f), ref bPitchV, smooth + 0.1f, 110f, dt);
            BotLook(bYaw, bPitch);
        }

        void UpdateReach(object input, object data)
        {
            bool want = Time.time < bReachUntil && botReach;
            if (want)
            {
                R.Set(input, "useSecondaryIsPressed", true);
                R.Set(input, "useSecondaryWasPressed", bReachFirst);
                R.Set(input, "useSecondaryWasReleased", false);
                R.Set(data, "sincePressReach", 0f);
                bReachFirst = false; bReachActive = true;
            }
            else if (bReachActive)
            {
                R.Set(input, "useSecondaryIsPressed", false);
                R.Set(input, "useSecondaryWasPressed", false);
                R.Set(input, "useSecondaryWasReleased", true);
                R.Set(data, "sincePressReach", 10f);
                bReachActive = false;
            }
            else
            {
                R.Set(input, "useSecondaryWasPressed", false);
                R.Set(input, "useSecondaryWasReleased", false);
            }
        }

        /// <summary>Взгляд в покое: по умолчанию туда же, куда смотрит игрок; редкие неторопливые взгляды на него и по сторонам.</summary>
        void IdleLook(float now, Vector3 head, Component buddy, ref float yawT, ref float pitchT)
        {
            if (now > bNextGlance && botEye)
            {
                float r = UnityEngine.Random.value;
                bGlanceHas = false;
                if (r < 0.40f && buddy != null) { bGlancePos = HeadOf(buddy); bGlanceHas = true; }
                else
                {
                    bGlanceYaw = bIdleYaw + UnityEngine.Random.Range(-55f, 55f);
                    bGlancePitch = UnityEngine.Random.Range(-6f, 10f);
                }
                bGlanceUntil = now + UnityEngine.Random.Range(1.2f, 2.6f);
                bNextGlance = bGlanceUntil + UnityEngine.Random.Range(5f, 14f);
            }
            if (now < bGlanceUntil && botEye)
            {
                if (bGlanceHas) { yawT = Yaw(bGlancePos - head); pitchT = PitchTo(head, bGlancePos); }
                else { yawT = bGlanceYaw; pitchT = bGlancePitch; }
                return;
            }
            // «куда смотрит группа»: поворачиваемся вслед за игроком только если он ушёл далеко от нашего направления
            if (buddy != null && botEye)
            {
                var ld = R.Get<Vector3>(R.Get(buddy, "data"), "lookDirection_Flat");
                if (ld.sqrMagnitude > 0.01f)
                {
                    float by = Yaw(ld);
                    if (Mathf.Abs(Mathf.DeltaAngle(bIdleYaw, by)) > 45f) bIdleYaw = by;
                }
            }
            yawT = bIdleYaw; pitchT = Mathf.Lerp(bPitch, 0f, 0.03f);
        }

        /// <summary>Крайний случай: бот сильно отстал или застрял — «подбегает из-за угла» позади игрока.</summary>
        void CatchUp(Vector3 buddyPos, Vector3 cur)
        {
            var dir = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(5f, 8f);
            Vector3 next = GroundIgnoring(buddyPos + new Vector3(dir.x, 0, dir.y), npc != null ? npc.transform : null);
            Rpc(npcChar, "WarpPlayerRPC", next, false);
            Game.HaltBody(npcChar);
        }

        // ---------------- кнопки «сделай сейчас» ----------------
        void BotDemoJump() { if (npcChar != null) BotJump(); }
        void BotDemoReach() { if (npcChar != null) { bool old = botReach; botReach = true; BotReach(2.5f); botReach = old; } }

        void DrawNpcMarker()
        {
            if (npc == null || NamePatchOk || Camera.main == null || Event.current.type != EventType.Repaint) return;
            var cam = Camera.main;
            Vector3 sp = cam.WorldToScreenPoint(NpcPos() + Vector3.up * 1.4f);
            if (sp.z <= 0f) return;
            var r = new Rect(sp.x - 110, Screen.height - sp.y - 14, 220, 28);
            Ui.Text(r, BotName(), 16, new Color(1f, 0.95f, 0.5f), TextAnchor.MiddleCenter, true);
        }
    }
}
