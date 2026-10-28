// Камера от третьего лица и «кукловод» (1.8).
//  • Третье лицо: игра — от первого лица. Мы НЕ ломаем её камеру: перед отрисовкой кадра отодвигаем камеру назад (с проверкой стен),
//    после кадра возвращаем. Игровая логика (взаимодействие, лазание) видит обычную камеру. Плюс показываем своё тело (HideTheBody).
//  • Кукловод: ты управляешь персонажем друга. Его тело двигается через RPC игры WarpPlayerRPC (телепорт ~20 раз в секунду по пути,
//    который считает твой мод: ходьба по земле, прыжок, стены, полёт), камера — за его спиной. Свой персонаж стоит на месте
//    (движение отключено, мышь работает). Другу мод не нужен: он видит, как его персонаж сам ходит.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace PeakFun
{
    // ---------- отключаем только движение своего персонажа (мышь остаётся), как это делает AliveSpectator ----------
    internal static class PuppetBlock
    {
        public static bool Want, Patched;
        public static string Status = "не ставилось";
        static Type ciT; static MethodInfo readObj;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Install()
        {
            if (Patched) return Status;
            try
            {
                ciT = R.FindType("CharacterInput");
                if (ciT == null) return Status = "CharacterInput не найден";
                MethodInfo m = null;
                foreach (var mm in ciT.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (mm.Name == "Sample") { m = mm; break; }
                if (m == null) return Status = "CharacterInput.Sample не найден";
                bool hasParam = false; foreach (var p in m.GetParameters()) if (p.Name == "playerMovementActive") hasParam = true;
                var h = new Harmony("com.friends.peakfun.puppet");
                var pre = typeof(PuppetBlock).GetMethod("Pre", BindingFlags.Static | BindingFlags.NonPublic);
                var post = typeof(PuppetBlock).GetMethod("Post", BindingFlags.Static | BindingFlags.NonPublic);
                if (!hasParam) return Status = "у Sample нет параметра playerMovementActive";
                h.Patch(m, new HarmonyMethod(pre), new HarmonyMethod(post));
                Patched = true;
                return Status = "ok";
            }
            catch (Exception e) { return Status = e.GetType().Name + ": " + e.Message; }
        }

        static void Pre(ref bool playerMovementActive) { if (Want) playerMovementActive = false; }

        static void Post(object __instance)
        {
            if (!Want || __instance == null) return;
            try
            {
                R.CallNoArgs(__instance, "ResetInput");
                var al = R.GetStatic(ciT, "action_look");
                if (al != null)
                {
                    if (readObj == null) readObj = al.GetType().GetMethod("ReadValueAsObject", Type.EmptyTypes);
                    if (readObj != null) { var v = readObj.Invoke(al, null); if (v is Vector2) R.Set(__instance, "lookInput", v); }
                }
            }
            catch { }
        }
    }

    // ---------- показать своё тело в третьем лице ----------
    internal static class ViewPatch
    {
        public static bool Want, Patched;
        public static string Status = "не ставилось";
        static Type hideT; static FieldInfo fChar, fBody, fSash, fMedal, fCostumes; static MethodInfo setShowing; static readonly List<MethodInfo> setShowingAll = new List<MethodInfo>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Install()
        {
            if (Patched) return Status;
            try
            {
                hideT = R.FindType("HideTheBody");
                if (hideT == null) return Status = "HideTheBody не найден (тело может быть скрыто)";
                var F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                fChar = hideT.GetField("character", F); fBody = hideT.GetField("body", F); fSash = hideT.GetField("sash", F);
                fMedal = hideT.GetField("medal", F); fCostumes = hideT.GetField("costumes", F);
                foreach (var m in hideT.GetMethods(F)) if (m.Name == "SetShowing" && m.GetParameters().Length == 2) { setShowingAll.Add(m); setShowing = m; }
                var upd = hideT.GetMethod("Update", F | BindingFlags.DeclaredOnly);
                if (upd == null || setShowing == null) return Status = "HideTheBody.Update/SetShowing не найдены";
                var h = new Harmony("com.friends.peakfun.view");
                h.Patch(upd, null, new HarmonyMethod(typeof(ViewPatch).GetMethod("Post", BindingFlags.Static | BindingFlags.NonPublic)));
                Patched = true;
                return Status = "ok";
            }
            catch (Exception e) { return Status = e.GetType().Name + ": " + e.Message; }
        }

        static void Show(object r)
        {
            if (r == null) return;
            var u = r as UnityEngine.Object; if (u != null && !u) return;
            foreach (var m in setShowingAll)
                if (m.GetParameters()[0].ParameterType.IsInstanceOfType(r)) { try { m.Invoke(cur, new object[] { r, 1f }); } catch { } return; }
        }
        static object cur;

        static void Post(object __instance)
        {
            if (!Want || __instance == null) return;
            try
            {
                var ch = fChar != null ? fChar.GetValue(__instance) : null;
                if (ch == null || !R.Get<bool>(ch, "IsLocal")) return;
                cur = __instance;
                if (fBody != null) Show(fBody.GetValue(__instance));
                if (fSash != null) Show(fSash.GetValue(__instance));
                if (fMedal != null) Show(fMedal.GetValue(__instance));
                var cs = fCostumes != null ? fCostumes.GetValue(__instance) as Array : null;
                if (cs != null) foreach (var c in cs) Show(c);
            }
            catch { }
        }
    }

    public partial class PeakFunPlugin
    {
        // ---------- третье лицо ----------
        bool thirdOn; float thirdDist = 3.2f, thirdLift = 0.3f;
        // камера-наблюдатель: 1 — спереди (смотрит на тебя), 2 — глазами бота (как видит тебя «второй игрок»)
        int obsMode; BepInEx.Configuration.ConfigEntry<KeyCode> keyObserve;

        void SetObserve(int m)
        {
            if (m == 2 && npcChar == null) { Ui.Toast("Для вида «глазами бота» нужен бот: нажми F3 или создай его в «Героях»", Ui.Bad); m = 0; }
            obsMode = m;
            if (m > 0) { CamShake.Install(); HookView(); ViewPatch.Install(); ViewPatch.Want = true; }
            else if (!thirdOn) ViewPatch.Want = false;
            Ui.Toast(m == 1 ? "Камера: спереди — ты видишь себя со стороны  [" + keyObserve.Value + "]" : m == 2 ? "Камера: глазами бота — так видит тебя второй игрок  [" + keyObserve.Value + "]" : "Камера: обычная", Ui.Accent);
        }

        void SetThird(bool on)
        {
            thirdOn = on;
            if (on)
            {
                CamShake.Install(); HookView();
                ViewPatch.Install(); ViewPatch.Want = true;
                Ui.Toast("Третье лицо ВКЛ  [" + keyThird.Value + "]" + (ViewPatch.Patched ? "" : "  (тело может быть скрыто: " + ViewPatch.Status + ")"), Ui.Accent);
            }
            else { ViewPatch.Want = false; Ui.Toast("Третье лицо выкл — снова от первого", Ui.Muted); }
        }

        void HookView()
        {
            CamShake.ViewActive = () => thirdOn || obsMode > 0 || (puppetOn && pup != null) || CineActive;
            CamShake.ViewApply = ApplyView;
        }

        static int viewMask = int.MinValue;
        static Type charT;

        /// <summary>Расстояние до ближайшей стены по лучу (персонажи камеру не закрывают).</summary>
        float ClearDist(Vector3 o, Vector3 dir, float dist, float radius)
        {
            if (viewMask == int.MinValue) { viewMask = LayerMask.GetMask("Terrain", "Map"); if (viewMask == 0) viewMask = ~0; }
            if (charT == null) charT = R.FindType("Character");
            float best = dist;
            var hits = Physics.SphereCastAll(o, radius, dir, dist, viewMask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider == null || h.distance <= 0.001f) continue;
                if (charT != null && h.collider.GetComponentInParent(charT) != null) continue;
                if (h.distance < best) best = h.distance;
            }
            return best;
        }

        void ApplyView(Transform tr)
        {
            if (CineApply(tr)) return;
            if (obsMode > 0 && !puppetOn)
            {
                if (obsMode == 1)
                {
                    Vector3 o1 = tr.position, d1 = tr.forward;
                    float dd = ClearDist(o1, d1, thirdDist + 1.5f, 0.22f);
                    tr.position = o1 + d1 * Mathf.Max(0.15f, dd - 0.08f); tr.rotation = Quaternion.LookRotation(-d1, Vector3.up);
                    return;
                }
                var me2 = Game.Me();
                if (obsMode == 2 && npcChar != null && me2 != null)
                {
                    Vector3 bh = HeadOf(npcChar), to = CenterOf(me2) - bh;
                    if (to.sqrMagnitude > 0.04f) { to.Normalize(); tr.position = bh + to * 0.35f; tr.rotation = Quaternion.LookRotation(to, Vector3.up); }
                    return;
                }
            }
            Vector3 back = -tr.forward, origin; float d;
            if (puppetOn && pup != null) { origin = pupPos + Vector3.up * 0.75f; d = puppetDist; }
            else { origin = tr.position + Vector3.up * thirdLift; d = thirdDist; }
            Vector3 dir = (back * d).normalized; if (dir.sqrMagnitude < 0.5f) return;
            float dist = ClearDist(origin, dir, d, 0.22f);
            tr.position = origin + dir * Mathf.Max(0.15f, dist - 0.08f);
        }

        // ---------- кукловод ----------
        bool puppetOn, pupFly; Component pup; Vector3 pupPos, pupVel; float pupVy, pupH = 1f, pupOff, puppetDist = 4.5f, pupSpeedMul = 1f;
        float nextPupWarp, lastPupMove, nextPupCal, pupStart; int pupFails; bool pupPrevInvinc; string pupName = "";

        Component PuppetCandidate()
        {
            var l = Victims();
            var looked = LookedVictim(80f, 0.93f);
            if (looked != null) return looked;
            if (targetActor >= 0) { var t = FindActor(targetActor); if (t != null && l.Contains(t)) return t; }
            var me = Game.Me(); Component best = null; float bd = float.MaxValue;
            foreach (var c in l) { float d = me != null ? Vector3.Distance(c.transform.position, me.transform.position) : 0f; if (d < bd) { bd = d; best = c; } }
            return best;
        }

        void TogglePuppet()
        {
            if (puppetOn) { StopPuppet(true); return; }
            var c = PuppetCandidate();
            if (c == null) { Ui.Toast("Некем управлять: нужен друг в лобби (или бот-спутник). Посмотри на него или выбери в меню «Кукловод»", Ui.Bad); return; }
            StartPuppet(c);
        }

        bool Ground(Vector3 p, out float gy)
        {
            if (charT == null) charT = R.FindType("Character");
            gy = 0f; float bd = float.MaxValue; bool has = false;
            var hits = Physics.RaycastAll(p + Vector3.up * 0.6f, Vector3.down, 7f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider == null || h.distance < 0.001f) continue;
                if (charT != null && h.collider.GetComponentInParent(charT) != null) continue;
                if (h.normal.y < 0.35f) continue;
                if (h.distance < bd) { bd = h.distance; gy = h.point.y + pupH; has = true; }
            }
            return has;
        }

        void StartPuppet(Component c)
        {
            var me = Game.Me(); if (me == null) { Ui.Toast("Нет персонажа", Ui.Bad); return; }
            if (hero != HeroKind.None && grabbed != null) ReleaseGrab(false);
            ReleaseGrab(false);
            string st = PuppetBlock.Install();
            if (!PuppetBlock.Patched) { Ui.Toast("Не вышло отключить своё управление: " + st, Ui.Bad); return; }
            CamShake.Install(); HookView();
            pup = c; pupName = Game.Name(c); pupPos = CenterOf(c); pupVel = Vector3.zero; pupVy = 0f; pupOff = 0f; pupFails = 0;
            pupH = 1f;
            { float gy; pupH = 1f; var p0 = pupPos; if (Ground(p0, out gy)) pupH = Mathf.Clamp(pupPos.y - (gy - 1f), 0.5f, 1.8f); }
            pupFly = false; pupStart = Time.time; lastPupMove = Time.time; nextPupWarp = 0f; nextPupCal = Time.time + 0.8f;
            pupPrevInvinc = BuffOn("invinc"); SetBuff("invinc", true);
            if (flying) SetFly(false);
            PuppetBlock.Want = true; puppetOn = true;
            Ui.Toast("Ты управляешь: " + pupName + "  —  WASD ходить, Пробел прыжок, Shift бежать, [" + keyAb1.Value + "] уронить, [" + keyAb2.Value + "] пнуть, [" + keyPuppet.Value + "] выйти", Ui.Accent);
        }

        void StopPuppet(bool toast)
        {
            if (!puppetOn) { PuppetBlock.Want = false; return; }
            puppetOn = false; PuppetBlock.Want = false;
            if (!pupPrevInvinc) SetBuff("invinc", false);
            if (toast) Ui.Toast("Управление " + pupName + " отпущено", Ui.Muted);
            pup = null;
        }

        void PuppetReset() { puppetOn = false; PuppetBlock.Want = false; pup = null; }

        void TickView()
        {
            bool typing = showMenu && cfgFreeze.Value;
#if !LITE
            if (!typing && Keys.Down(keyThird.Value)) SetThird(!thirdOn);
            if (!typing && Keys.Down(keyObserve.Value)) SetObserve((obsMode + 1) % 3);
            if (obsMode == 2 && npcChar == null) SetObserve(0);
            if (!typing && Keys.Down(keyPuppet.Value)) TogglePuppet();
#endif
            if (puppetOn) { try { PuppetTick(); } catch (Exception e) { SafeLog("Puppet", e); pupFails++; if (pupFails > 8) StopPuppet(true); } }
            else if (PuppetBlock.Want) PuppetBlock.Want = false;
        }

        void PuppetTick()
        {
            var me = Game.Me();
            if (me == null || pup == null || Game.IsDead(me)) { StopPuppet(true); return; }
            if (Game.IsDead(pup)) { Ui.Toast(pupName + " погиб — управление окончено", Ui.Bad); StopPuppet(false); return; }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            bool typing = showMenu && cfgFreeze.Value;
            var cam = Camera.main; if (cam == null) return;
            Vector3 f = cam.transform.forward; f.y = 0f; f = f.sqrMagnitude < 0.01f ? Vector3.forward : f.normalized;
            Vector3 r = new Vector3(f.z, 0f, -f.x);
            Vector3 mv = Vector3.zero; bool jump = false, down = false;
            if (!typing)
            {
                if (Keys.Held(KeyCode.W)) mv += f; if (Keys.Held(KeyCode.S)) mv -= f;
                if (Keys.Held(KeyCode.D)) mv += r; if (Keys.Held(KeyCode.A)) mv -= r;
                jump = Keys.Held(KeyCode.Space); down = Keys.Held(KeyCode.LeftControl);
                if (Keys.Down(keyAb1.Value)) { Game.Rpc(pup, "RPCA_Fall", 3f); HeroToast("Уронил " + pupName); }
                if (Keys.Down(keyAb2.Value)) { Game.Rpc(pup, "RPCA_Fall", 3f); Game.Rpc(pup, "RPCA_AddForceToBodyPart", 0, (f + Vector3.up * 0.35f).normalized * launchForce * 0.8f); HeroToast("Пнул " + pupName); }
                if (Keys.Down(KeyCode.F)) { pupFly = !pupFly; pupVy = 0f; HeroToast(pupFly ? "Левитация: ВКЛ (Пробел вверх, Ctrl вниз)" : "Левитация выкл"); }
            }
            float speed = 4.3f * pupSpeedMul * (Keys.Held(KeyCode.LeftShift) && !typing ? 1.8f : 1f);
            Vector3 desired = mv.sqrMagnitude > 0.01f ? mv.normalized * speed : Vector3.zero;
            pupVel = Vector3.MoveTowards(pupVel, desired, 30f * dt);
            if (pupVel.sqrMagnitude > 0.05f || jump) lastPupMove = Time.time;

            Vector3 step = pupVel * dt;
            if (step.sqrMagnitude > 1e-8f)
            {
                // стены: скользим вдоль них
                if (charT == null) charT = R.FindType("Character");
                float len = step.magnitude; Vector3 sd = step / len;
                var hits = Physics.SphereCastAll(pupPos, 0.3f, sd, len + 0.05f, ~0, QueryTriggerInteraction.Ignore);
                float bestD = float.MaxValue; Vector3 bn = Vector3.zero;
                foreach (var h in hits)
                {
                    if (h.collider == null || h.distance < 0.001f || h.normal.sqrMagnitude < 0.01f) continue;
                    if (charT != null && h.collider.GetComponentInParent(charT) != null) continue;
                    if (h.normal.y > 0.6f) continue;     // пол/пологий склон — не стена
                    if (h.distance < bestD) { bestD = h.distance; bn = h.normal; }
                }
                if (bestD < float.MaxValue)
                {
                    bn.y = 0f; bn.Normalize();
                    float into = Vector3.Dot(step, bn);
                    if (into < 0f) step -= bn * into;
                    if (bestD < len) step *= 0.9f;
                }
            }

            if (pupFly)
            {
                pupPos += step; pupPos.y += ((jump ? 1f : 0f) - (down ? 1f : 0f)) * speed * 0.9f * dt;
            }
            else
            {
                Vector3 np = pupPos + step; float gy;
                bool has = Ground(np, out gy);
                if (has && gy - pupPos.y > 0.9f && pupVy <= 0.1f) { np = pupPos; has = Ground(np, out gy); }   // слишком высокий уступ — стена
                pupPos.x = np.x; pupPos.z = np.z;
                bool grounded = false;
                if (has && pupVy <= 0f && pupPos.y <= gy + 0.35f)
                {
                    pupPos.y = Mathf.MoveTowards(pupPos.y, gy, 14f * dt); pupVy = 0f; grounded = true;
                }
                if (!grounded)
                {
                    pupVy = Mathf.Max(pupVy - 22f * dt, -35f); pupPos.y += pupVy * dt;
                    if (has && pupPos.y <= gy) { pupPos.y = gy; pupVy = 0f; grounded = true; }
                }
                if (grounded && jump && !typing) { pupVy = 8f; pupPos.y += 0.06f; }
            }
            if (pupPos.y < -900f) { StopPuppet(true); return; }

            // самокалибровка высоты: стоим — смотрим, где реально оказался друг
            if (Time.time >= nextPupCal)
            {
                nextPupCal = Time.time + 0.6f;
                if (Time.time - lastPupMove > 0.6f && pupVy == 0f)
                {
                    float err = CenterOf(pup).y - pupPos.y;
                    if (Mathf.Abs(err) < 1.2f) pupOff = Mathf.Clamp(pupOff - err * 0.6f, -1.2f, 1.2f);
                }
                // друга унесло силой игры далеко — догоняем его позицию
                if (Vector3.Distance(CenterOf(pup), pupPos) > 25f && Time.time - pupStart > 2f) { pupPos = CenterOf(pup); pupVy = 0f; }
            }

            if (Time.time >= nextPupWarp)
            {
                nextPupWarp = Time.time + 0.05f;
                string err = Game.Rpc(pup, "WarpPlayerRPC", pupPos + Vector3.up * pupOff, false);
                if (err != null) { pupFails++; if (pupFails > 6) { Ui.Toast("RPC телепорта не работает: " + err, Ui.Bad); StopPuppet(false); } }
                else pupFails = 0;
            }
        }

        void DrawPuppetHud()
        {
            if (!puppetOn) return;
            if (Event.current.type != EventType.Repaint) return;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f);
            float w = 560f * s, h = 76f * s, x = Screen.width * 0.5f - w * 0.5f, y = 18f * s;
            Ui.Box(new Rect(x, y, w, h), new Color(0.04f, 0.05f, 0.1f, 0.8f), 16 * s);
            Ui.Text(new Rect(x + 16 * s, y + 6 * s, w - 32 * s, 28 * s), "ТЫ УПРАВЛЯЕШЬ: " + pupName.ToUpper(), (int)(20 * s), Ui.Accent, TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(x + 16 * s, y + 36 * s, w - 32 * s, 34 * s), "WASD идти · Пробел прыжок · Shift бежать · F левитация " + (pupFly ? "[ВКЛ]" : "") + " · [" + keyAb1.Value + "] уронить · [" + keyAb2.Value + "] пнуть · [" + keyPuppet.Value + "] выйти", (int)(13 * s), Ui.Txt, TextAnchor.MiddleLeft, false);
        }

        // ---------- вкладка «Кукловод» ----------
        void TabPuppet()
        {
            Ui.Header("Третье лицо");
            bool t = Ui.Toggle("Камера от третьего лица  [" + keyThird.Value + "]", thirdOn, "Камера отъезжает назад и показывает твоего персонажа. Игра остаётся обычной — просто другая точка обзора");
            if (t != thirdOn) SetThird(t);
            thirdDist = Ui.Slider("Дальность камеры", thirdDist, 1.2f, 8f, "0.0", " м");
            thirdLift = Ui.Slider("Высота камеры", thirdLift, -0.5f, 1.5f, "0.00", " м");
            if (thirdOn && !ViewPatch.Patched) Ui.Para("Тело не получилось показать: " + ViewPatch.Status, 12, Ui.Bad);
            Ui.Para("Прицел и взаимодействие по-прежнему идут «из глаз» персонажа, а не из камеры — так игре привычнее.", 12, Ui.Muted);

            Ui.Header("Кукловод: играть за друга");
            Ui.Para("Ты управляешь ПЕРСОНАЖЕМ ДРУГА: WASD ходишь, Пробел — прыжок, Shift — бег, F — левитация. Камера идёт за ним. Твой собственный персонаж стоит на месте. Друг видит, как его персонаж сам куда-то идёт. Мод у друга не нужен.", 13);
            if (!NeedMe()) return;
            if (puppetOn)
            {
                Ui.Para("Сейчас управляешь: " + pupName, 14, Ui.Accent);
                if (Ui.Button("Отпустить  [" + keyPuppet.Value + "]", 2)) StopPuppet(true);
                puppetDist = Ui.Slider("Дальность камеры за другом", puppetDist, 1.5f, 12f, "0.0", " м");
                pupSpeedMul = Ui.Slider("Скорость ходьбы", pupSpeedMul, 0.3f, 4f, "0.0", "×");
            }
            else
            {
                var l = Victims();
                if (l.Count == 0) Ui.Para("Друзей в лобби нет. Для проверки в одиночку включи бота-спутника (вкладка «Бот-спутник») — им тоже можно управлять.", 13, Ui.Muted);
                else
                {
                    var labels = new List<string>();
                    foreach (var c in l) labels.Add(Game.Name(c));
                    int pick = Ui.Buttons(labels.ToArray(), 3, 1, 40f, true, -1, 15);
                    if (pick >= 0) StartPuppet(l[pick]);
                }
                Ui.Para("Или просто посмотри на друга и нажми [" + keyPuppet.Value + "].", 12, Ui.Muted);
            }
            Ui.Para("Честно об ограничениях: друг может крутить камеру и жать клавиши — ты его перебиваешь телепортами (он видит лёгкие подёргивания); его персонаж не переставляет ноги по-настоящему, а «скользит» (позу и анимацию ведёт игра друга). Зато идёт туда, куда ты хочешь, прыгает и взлетает.", 12, Ui.Muted);
        }
    }
}
