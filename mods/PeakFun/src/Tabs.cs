// Вкладки меню.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        bool NeedMe()
        {
            if (Game.Me() != null) return true;
            Ui.Para("Персонаж не найден — зайди в забег (в лобби на острове это тоже работает после появления персонажа).", 14, Ui.Bad);
            return false;
        }

        /// <summary>Выбор цели: [Я] [Все друзья] [имена…]</summary>
        void TargetChips(bool withMe)
        {
            var others = Game.Others();
            var labels = new List<string>(); var ids = new List<int>();
            if (withMe) { labels.Add("Я"); ids.Add(-2); }
            labels.Add("Все друзья"); ids.Add(-1);
            foreach (var c in others) { labels.Add((Game.IsDead(c) ? "† " : "") + Game.Name(c)); ids.Add(Game.ActorOf(c)); }
            if (!withMe && targetActor == -2) targetActor = -1;
            if (withMe && others.Count == 0 && targetActor == -1) targetActor = -2;   // один в лобби — пранкаем себя
            if (!ids.Contains(targetActor)) targetActor = ids[0];
            int sel = ids.IndexOf(targetActor);
            int clicked = Ui.Buttons(labels.ToArray(), 4, 0, 34f, true, sel, 14);
            if (clicked >= 0) targetActor = ids[clicked];
            if (others.Count == 0) Ui.Para(withMe ? "Друзей в лобби нет — всё применяется на тебе (выбрано «Я»). Так можно посмотреть, как это выглядит." : "Друзей в лобби нет — действия на друзей ничего не сделают.", 13, withMe ? Ui.Good : Ui.Muted);
        }

        // ========================================= ЭФФЕКТЫ =========================================
        void TabEffects()
        {
            if (!NeedMe()) return;
            var me = Game.Me();

            Ui.Header("Жизнь");
            bool dead = IsDeadOrGhost(me);
            Ui.Para(dead ? "Ты мёртв (призрак). Можно возродиться самому." : "Ты жив. Кнопка работает, когда ты мёртв.", 13, dead ? Ui.Bad : Ui.Good);
            int lc = Ui.Buttons(new[] { "Возродиться  [" + keyRespawn.Value + "]", "Авто-возрождение: " + (autoRespawn ? "ВКЛ" : "выкл") }, 2, 1, 36f, true);
            if (lc == 0) Safe("возрождение", () => RespawnMe());
            if (lc == 1) { autoRespawn = !autoRespawn; Ui.Toast(autoRespawn ? "Авто-возрождение через 3 с после смерти" : "Авто-возрождение выключено"); }

            Ui.Header("Бесконечные баффы — держатся, пока включены");
            foreach (var b in Buffs)
            {
                if (b.id == "stamina")
                {
                    bool nat = Game.InfiniteStam(me);
                    bool both = nat || BuffOn("stamina");
                    bool v = Ui.Toggle(b.title, both, b.hint);
                    if (v != both)
                    {
                        if (nat != v) Game.ToggleInfiniteStam();   // встроенный чит игры
                        SetBuff("stamina", v);
                    }
                    continue;
                }
                bool cur = BuffOn(b.id);
                bool nv = Ui.Toggle(b.title, cur, b.hint);
                if (nv != cur) SetBuff(b.id, nv);
            }

            Ui.Header("Настройка энергетика и гравитации");
            energyMove = Ui.Slider("Скорость бега (энергетик)", energyMove, 1f, 6f, "0.0", "×");
            energyClimb = Ui.Slider("Скорость лазания (энергетик)", energyClimb, 1f, 8f, "0.0", "×");
            lowGravStacks = Mathf.RoundToInt(Ui.Slider("Сила низкой гравитации (стаки)", lowGravStacks, 1, 8, "0"));
            Ui.Para("Изменения применяются к уже действующим эффектам в течение полсекунды.", 12);

            Ui.Header("Разово на себя");
            effectDur = Ui.Slider("Длительность разовых эффектов", effectDur, 3f, 60f, "0", " с");
            int c = Ui.Buttons(new[] { "Лечить всё", "Очистить статусы", "Бонус-выносливость", "Заморозить статусы" }, 2, 1);
            if (c == 0) Safe("лечение", () => ApplyEffectLocal("heal", 1f));
            if (c == 1) Safe("очистка", () => ApplyEffectLocal("clear", 1f));
            if (c == 2) Safe("бонус", () => ApplyEffectLocal("bonus", 1f));
            if (c == 3) { Game.ToggleLockStatuses(); Ui.Toast(Game.StatusesLocked(me) ? "Статусы заморожены" : "Статусы разморожены"); }

            Ui.Header("Вредные — для смеха");
            c = Ui.Buttons(new[] { "Слепота", "Онемение", "Истощение", "Хаос-эффект" }, 4, 2);
            if (c == 0) Safe("слепота", () => ApplyEffectLocal("blind", effectDur));
            if (c == 1) Safe("онемение", () => ApplyEffectLocal("numb", effectDur));
            if (c == 2) Safe("истощение", () => ApplyEffectLocal("exhaust", effectDur));
            if (c == 3) Safe("хаос", () => ApplyEffectLocal("chaosaff", effectDur));

            Ui.Header("Статусы");
            statusAmount = Ui.Slider("Сколько накинуть", statusAmount, 0.05f, 1f, "0.00");
            c = Ui.Buttons(new[] { "Яд", "Холод", "Жара", "Голод", "Проклятие", "Сонливость", "Споры", "Паутина" }, 4, 0);
            if (c >= 0) Safe("статус", () => ApplyEffectLocal(StatusIds[c], effectDur));
        }

        // ========================================= ДВИЖЕНИЕ =========================================
        void TabMove()
        {
            if (!NeedMe()) return;
            var me = Game.Me();

            Ui.Header("Быстрое залезание");
            bool q = Ui.Toggle("Быстро лазать", quickClimb, "Стены, лианы и верёвки — на любой скорости");
            if (q != quickClimb) { quickClimb = q; Ui.Toast(q ? "Быстрое лазание ВКЛ ×" + climbMul.ToString("0.0") : "Быстрое лазание выключено", q ? Ui.Good : Ui.Muted); }
            climbMul = Ui.Slider("Скорость лазания", climbMul, 1f, 10f, "0.0", "×");
            if (quickClimb && !BuffOn("stamina") && !Game.InfiniteStam(me))
                Ui.Para("Совет: включи «Бесконечная выносливость» на вкладке «Эффекты», чтобы не уставать на стене.", 12);

            Ui.Header("Бег и прыжок");
            speedMul = Ui.Slider("Скорость бега", speedMul, 0.5f, 6f, "0.0", "×");
            jumpMul = Ui.Slider("Сила прыжка", jumpMul, 0.5f, 6f, "0.0", "×");
            if (Ui.Button("Сбросить скорость и прыжок")) { speedMul = 1f; jumpMul = 1f; }

            Ui.Header("Полёт и гравитация");
            bool f = Ui.Toggle("Полёт  [" + keyFly.Value + "]", flying, "WASD, Пробел вверх, Ctrl вниз, Shift — ускорение");
            if (f != flying) SetFly(f);
            flySpeed = Ui.Slider("Скорость полёта", flySpeed, 5f, 90f, "0", "");
            lowGravity = Ui.Toggle("Низкая гравитация (физика, только у тебя)", lowGravity);
            gravityScale = Ui.Slider("Гравитация", gravityScale, 0.05f, 1f, "0.00", "×");

            Ui.Header("Телепорт");
            tpHeight = Ui.Slider("Высота подъёма", tpHeight, 10f, 300f, "0", " м");
            int c = Ui.Buttons(new[] { "Подняться вверх", "Метки друзей: " + (markers ? "вкл" : "выкл") }, 2);
            if (c == 0) Warp(me, me.transform.position + Vector3.up * tpHeight);
            if (c == 1) markers = !markers;
        }

        // ========================================= ПРЕДМЕТЫ =========================================
        static readonly string[] ItemModes = { "В руки", "Бросить рядом", "Другу под ноги" };

        void TabItems()
        {
            if (!NeedMe()) return;
            Ui.Header("Куда выдавать");
            int sm = Ui.Buttons(ItemModes, 3, 0, 34f, true, itemMode);
            if (sm >= 0) itemMode = sm;
            if (itemMode == 2) { Ui.Para("Кому: (выбери)", 12); TargetChips(false); }
            itemCount = Mathf.RoundToInt(Ui.Slider("Сколько штук", itemCount, 1, 10, "0"));

            Ui.Header("Поиск");
            itemFilter = Ui.Field(itemFilter, "Начни вводить название…");
            int b = Ui.Buttons(new[] { "Обновить список", "Выдать то, что в поле" }, 2);
            if (b == 0) LoadItems();
            if (b == 1 && !string.IsNullOrWhiteSpace(itemFilter)) { var n = ResolveItem(itemFilter); Safe("выдача", () => GiveItem(n)); }
            Ui.Para(itemStatus, 12);

            string f = (itemFilter ?? "").Trim().ToLowerInvariant();
            var list = new List<ItemInfo>();
            foreach (var it in items) if (f.Length == 0 || it.key.Contains(f)) list.Add(it);
            Ui.Header("Предметы (" + list.Count + ") — нажми, чтобы выдать");
            int cols = 2; float gap = 8f, h = 52f;
            float bw = (Ui.W - gap * (cols - 1)) / cols;
            int rows = (list.Count + cols - 1) / cols;
            float top = Ui.Y;
            float viewTop = scroll.y - 60f, viewBot = scroll.y + WinH;
            bool rep_ = Event.current.type == EventType.Repaint;
            for (int r = 0; r < rows; r++)
            {
                float y = top + r * (h + gap);
                if (y + h < viewTop || y > viewBot) continue;
                for (int cc = 0; cc < cols; cc++)
                {
                    int i = r * cols + cc;
                    if (i >= list.Count) break;
                    var it = list[i];
                    var rc = new Rect(Ui.X + cc * (bw + gap), y, bw, h);
                    if (Ui.ButtonRect(rc, "", 0, true, 13)) { var nm = it.prefab; Safe("выдача", () => GiveItem(nm)); }
                    if (rep_)
                    {
                        var ir = new Rect(rc.x + 8, rc.y + 6, h - 12, h - 12);
                        if (it.tex != null)
                        {
                            GUI.color = new Color(1, 1, 1, Ui.Alpha);
                            GUI.DrawTextureWithTexCoords(ir, it.tex, it.uv);
                            GUI.color = Color.white;
                        }
                        else Ui.Box(ir, new Color(1, 1, 1, 0.08f), 8);
                    }
                    Ui.Text(new Rect(rc.x + h + 2, rc.y, rc.width - h - 8, rc.height), it.title, 15, Ui.Txt, TextAnchor.MiddleLeft);
                }
            }
            Ui.Y = top + rows * (h + gap);
        }

        // ========================================= ДРУЗЬЯ =========================================
        void TabFriends()
        {
            Ui.Header("Кому");
            TargetChips(false);
            var targets = Targets(false);

            Ui.Header("Физика (видят все, мод у друзей НЕ нужен)");
            launchForce = Ui.Slider("Сила катапульты", launchForce, 1000f, 40000f, "0");
            skyHeight = Ui.Slider("Высота «лифта»", skyHeight, 10f, 300f, "0", " м");
            int c = Ui.Buttons(new[] { "Подножка", "Катапульта", "Обморок", "Разбудить", "К себе", "Я к нему", "В небо!", "Воскресить" }, 4, 0, 36f, targets.Count > 0);
            if (c >= 0)
            {
                var list = new List<Component>(targets);
                Safe("действие", () =>
                {
                    foreach (var t in list)
                        switch (c)
                        {
                            case 0: Trip(t); break; case 1: Launch(t); break; case 2: PassOut(t); break; case 3: WakeUp(t); break;
                            case 4: BringToMe(t); break; case 5: GoTo(t); break; case 6: ToSky(t); break; case 7: Revive(t); break;
                        }
                });
            }
            if (Ui.Button("Двое случайных меняются местами", 0, Game.Everyone(true).Count >= 2)) Safe("обмен", SwapRandomPair);

            Ui.Header("Эффекты на друга (нужен PeakFun у друга)");
            effectDur = Ui.Slider("Длительность", effectDur, 3f, 60f, "0", " с");
            statusAmount = Ui.Slider("Сила статусов", statusAmount, 0.05f, 1f, "0.00");
            c = Ui.Buttons(new[] { "Яд", "Холод", "Жара", "Голод", "Слепота", "Онемение", "Истощение", "Хаос-эффект" }, 4, 2, 34f, targets.Count > 0);
            string[] ids = { "poison", "cold", "hot", "hunger", "blind", "numb", "exhaust", "chaosaff" };
            if (c >= 0) EffectToTargets(ids[c]);
            c = Ui.Buttons(new[] { "Подарить энергетик", "Подлечить" }, 2, 1, 34f, targets.Count > 0);
            if (c == 0) EffectToTargets("energy");
            if (c == 1) EffectToTargets("heal");

            Ui.Header("На всех сразу");
            c = Ui.Buttons(new[] { "Подножка всем", "Катапульта всем", "Все ко мне", "Усыпить всех", "Воскресить всех" }, 2);
            if (c >= 0)
                Safe("массовое", () =>
                {
                    foreach (var t in Game.Others())
                        switch (c) { case 0: Trip(t); break; case 1: Launch(t); break; case 2: BringToMe(t); break; case 3: PassOut(t); break; case 4: Revive(t); break; }
                });
        }

        // ========================================= БОТ-СПУТНИК =========================================
        void TabBot()
        {
            if (!NeedMe()) return;
            Ui.Header("Бот-спутник — «живой» игрок, который ходит за вами");
            bool on = npc != null;
            Ui.Para(on ? (npcChar != null ? BotName() + " в игре и идёт за " + (bBuddy != null ? Game.Name(bBuddy) : "кем-то") : "Бот готовится…") : "Бота нет. Нажми «Подключить бота».", 14, on ? Ui.Good : Ui.Muted);
            npcName = Ui.Field(npcName, "Имя бота (покажется в тосте и над головой)");
            int nc = Ui.Buttons(new[] { npc == null ? "Подключить бота" : "Бот уже тут", "Отключить бота" }, 2, 1, 38f, true);
            if (nc == 0 && npc == null) Safe("бот", SpawnCompanion);
            if (nc == 1) Safe("бот", RemoveCompanion);
            botRealPlayer = Ui.Toggle("ЭКСПЕРИМЕНТ: облик настоящего игрока", botRealPlayer, "Может вылететь игра! В 1.5 именно это у одного пользователя закрыло игру. Выключено по умолчанию — тогда бот «зомби-скаут» без ИИ, как в 1.4");
            botFollow = Ui.Toggle("Ходит за игроком", botFollow, "Идёт плавно, держится сбоку-сзади, не заслоняет обзор, лезет за вами по стенам");
            botEye = Ui.Toggle("Живой взгляд", botEye, "В покое смотрит туда же, куда игрок, изредка поглядывает на него и по сторонам. На ваш взгляд НЕ реагирует");
            botJump = Ui.Toggle("Прыгает через преграды", botJump, "Прыгает только когда впереди преграда или он застрял — без прыжков «просто так»");
            botReach = Ui.Toggle("Помогает: протягивает руку", botReach, "Тянется только к тому, кто висит или падает рядом");
            int dc = Ui.Buttons(new[] { "Пусть прыгнет", "Пусть протянет руку" }, 2, 2, 34f, npc != null && npcChar != null);
            if (dc == 0) BotDemoJump();
            if (dc == 1) BotDemoReach();
            Ui.Para("Бот управляется как обычный игрок (ввод персонажа), без телепортов — они только если он безнадёжно отстал или застрял. Смена облика действует при следующем подключении. Друзьям с PeakFun приходит «подключился к игре». Не проверено в живой игре — если что-то не так, «Отключить» и подключи заново; пиши, что именно видишь.", 12);

        }

        // ========================================= МОНСТРЫ =========================================
        void TabMonsters()
        {
            if (!NeedMe()) return;
            var me = Game.Me();
            Ui.Header("Цель");
            TargetChips(true);
            var tg = Targets();
            Component aim = tg.Count > 0 ? tg[UnityEngine.Random.Range(0, tg.Count)] : me;

            Ui.Header("Зомби (грибные)");
            monsterCount = Mathf.RoundToInt(Ui.Slider("Сколько зомби", monsterCount, 1, 12, "0"));
            int c = Ui.Buttons(new[] { "Туда, куда смотрю", "Вокруг цели" }, 2, 1);
            if (c == 0) Safe("зомби", ZombieAtCrosshair);
            if (c == 1) Safe("зомби", () =>
            {
                if (targetActor == -1) foreach (var t in Game.Others()) ZombiesAt(t.transform.position, monsterCount, 6f, 11f);
                else ZombiesAt(aim.transform.position, monsterCount, 6f, 11f);
            });
            Ui.Para("Зомби создаются как объекты игры — их видят и они кусают всех. Могут появляться только у тебя в лобби, если у друга версия игры другая.", 12);

            Ui.Header("Скаутмастер");
            bool host = !Game.InRoom || Game.IsMaster();
            Ui.Para(host ? "Ты хост — Скаутмастер сможет появиться." : "Ты не хост: Скаутмастер появляется только у хоста лобби. Попроси друга-хоста поставить мод.", 13, host ? Ui.Good : Ui.Bad);
            c = Ui.Buttons(new[] { "Натравить на цель", "Натравить на меня" }, 2, 2, 38f, host);
            if (c == 0) Safe("скаутмастер", () =>
            {
                if (targetActor == -1) { var o = Game.Others(); if (o.Count > 0) SpawnScoutmaster(o[UnityEngine.Random.Range(0, o.Count)]); else Ui.Toast("Нет друзей в лобби", Ui.Bad); }
                else SpawnScoutmaster(aim);
            });
            if (c == 1) Safe("скаутмастер", () => SpawnScoutmaster(me));
            Ui.Para("В игре Скаутмастер охотится после сигнала Bugle и не появляется на Tenderfoot, в Kiln и Caldera — здесь он создаётся принудительно.", 12);

            Ui.Header("Уборка");
            if (Ui.Button("Убрать всех, кого я призвал (" + spawned.Count + ")", 0, spawned.Count > 0)) Safe("уборка", ClearSpawned);
        }

        // ========================================= ПРАНКИ =========================================
        static readonly FxKind[] PrankOrder = { FxKind.Scream, FxKind.Fire, FxKind.Static, FxKind.Death, FxKind.Kick, FxKind.Black, FxKind.Disco, FxKind.Lag };
        static readonly string[] PrankLabels = { "СКРИМЕР", "Я ГОРЮ", "Помехи (Скаутмастер)", "«Вы погибли»", "Фейк-кик за читы", "Тьма", "Диско", "Плохая связь (плашка)" };
        static readonly FxKind[] LagOrder = { FxKind.Freeze, FxKind.Stutter, FxKind.Slideshow, FxKind.Rubber, FxKind.Disconnect, FxKind.Glitch, FxKind.Bsod, FxKind.Crash, FxKind.SteamDown, FxKind.Combo };
        static readonly string[] LagLabels = { "Фриз игры + «не отвечает»", "Микро-фризы (рывки кадра)", "Слайд-шоу ~7 FPS", "Откат назад (резинка)", "Потеря соединения", "Артефакты видеокарты", "Синий экран смерти", "«PEAK перестал работать»", "Steam: нет сети", "АДСКИЙ ЛАГ (всё сразу)" };

        void TabPranks()
        {
            Ui.Header("На кого");
            TargetChips(true);
            prankDur = Ui.Slider("Длительность", prankDur, 2f, 20f, "0", " с");

            Ui.Header("Пугалки и визуальные приколы");
            int c = Ui.Buttons(PrankLabels, 2, 0, 42f, true, -1, 15);
            if (c >= 0) { var k = PrankOrder[c]; Safe("пранк", () => Prank(k)); }

            Ui.Header("Реалистичные «лаги» — чтобы друг подумал, что всё сломалось");
            c = Ui.Buttons(LagLabels, 2, 2, 42f, true, -1, 15);
            if (c >= 0) { var k = LagOrder[c]; Safe("лаг", () => Prank(k)); }
            Ui.Para("Фриз и крэш: замороженный кадр + пауза звука. Слайд-шоу и «резинка» работают по-настоящему (падает FPS / персонажа откатывает на пару секунд назад), всё возвращается само. Лучше всего пугает «Адский лаг» на 10–14 с. Ничего не ломается и не сохраняется, F8 у друга убирает всё.", 12);
            Ui.Para("На «Я» эффект включается у тебя (проверить, как выглядит). Друзья увидят его, только если у них тоже стоит PeakFun — без мода у них ничего не происходит.", 12);

            Ui.Header("Настройки");
            burnReal = Ui.Toggle("«Я горю» греет по-настоящему", burnReal, "Пока горит экран — набирается статус «Жара»");
            bool acc = Ui.Toggle("Принимать пранки от друзей", cfgAccept.Value, "Выключи, если не хочешь пугаться сам");
            if (acc != cfgAccept.Value) cfgAccept.Value = acc;
            bool snd = Ui.Toggle("Звук скримера", cfgScreamSound.Value);
            if (snd != cfgScreamSound.Value) cfgScreamSound.Value = snd;
            float vol = Ui.Slider("Громкость скримера", cfgScreamVol.Value, 0.05f, 1f, "0.00");
            if (Mathf.Abs(vol - cfgScreamVol.Value) > 0.0001f) cfgScreamVol.Value = vol;
            if (Ui.Button("СТОП! Убрать всё с экрана  [" + keyPanic.Value + "]", 2)) Panic();
        }

        // ========================================= ЖЁСТКИЕ ПРАНКИ =========================================
        static readonly FxKind[] HardOrder = { FxKind.FakeCrash, FxKind.WinUpdate, FxKind.Hack, FxKind.Flash, FxKind.Quake, FxKind.Virus };
        static readonly string[] HardLabels = { "Вылет на рабочий стол (фейк)", "Обновление Windows", "«Взлом» лобби", "Светошумовая (вспышка + звон)", "Землетрясение", "Вирусные окна" };
        bool quitSure;

        void TargetBanner()
        {
            string who = targetActor == -2 ? "на ТЕБЕ" : targetActor == -1 ? "на ВСЕХ друзей" : "на " + Game.PlayerName(targetActor);
            Ui.Para("Сейчас всё идёт " + who + (targetActor == -2 ? " — безопасно проверить, как это выглядит." : "."), 14, targetActor == -2 ? Ui.Good : Ui.Accent);
        }

        void TabHard()
        {
            Ui.Header("На кого");
            TargetChips(true);
            TargetBanner();

            Ui.Header("Жёсткие пранки — эффект «ну всё, сломалось»");
            int c = Ui.Buttons(HardLabels, 2, 0, 44f, true, -1, 15);
            if (c >= 0) { var k = HardOrder[c]; Safe("жёсткий пранк", () => Prank(k)); }
            Ui.Para("• Вылет: кадр замирает, экран чёрный, появляется рабочий стол с часами, окном «PEAK перестал работать», курсор нажимает «Закрыть» и запускает Steam, потом игра «перезапускается». 13 секунд.\n• Обновление: синий экран «Работа с обновлениями N%».\n• Взлом: матричный дождь, «хакерский» терминал; в конце честная надпись «Шутка».\n• Светошумовая: белая вспышка, звон в ушах, звук игры глохнет.\n• Землетрясение: трясётся камера и слегка тело, гул и пыль.\n• Вирусные окна: десяток «антивирусных» окон по всему экрану.", 12);

            Ui.Header("Всё подряд (цепочка)");
            if (Ui.Button("КУЧА ВСЕГО: взлом → землетрясение → вспышка → вылет", 2)) { int tgt = targetActor; StartCoroutine(CoHardCombo(tgt)); }
            Ui.Para("Самое жёсткое: идёт около 40 секунд. Лучше сначала на себе.", 12);

            Ui.Header("НАСТОЯЩИЙ вылет из игры");
            Ui.Para("Закрывает ТОЛЬКО игру (Application.Quit), компьютер и другие программы не трогает. Сначала показывает «PEAK не отвечает» ~5 секунд. На друге сработает, только если он сам включил у себя «Разрешить друзьям закрывать мне игру» (вкладка «Пранки»); иначе он увидит лишь фейковый крэш.", 12, Ui.Bad);
            quitSure = Ui.Toggle("Я понимаю: игра закроется у цели", quitSure, "Без галочки кнопка не работает. Прогресс забега не сохраняется — осторожно с собой!");
            if (Ui.Button("ЗАКРЫТЬ ИГРУ у цели", 1, quitSure)) { quitSure = false; Safe("выход", () => PrankTo(targetActor, FxKind.Quit, 6f)); }

            Ui.Header("Моя защита");
            bool acc = Ui.Toggle("Принимать пранки от друзей", cfgAccept.Value, "Выключи — и никакие пранки тебя не тронут");
            if (acc != cfgAccept.Value) cfgAccept.Value = acc;
            bool aq = Ui.Toggle("Разрешить друзьям НАСТОЯЩЕ закрывать мне игру", cfgAllowQuit.Value, "По умолчанию выключено: тогда «вылет» от друга — только фейк");
            if (aq != cfgAllowQuit.Value) cfgAllowQuit.Value = aq;
            bool qp = Ui.Toggle("Землетрясение трясёт и тело (не только камеру)", cfgQuakePhysics.Value);
            if (qp != cfgQuakePhysics.Value) cfgQuakePhysics.Value = qp;
            if (Ui.Button("СТОП! Убрать всё с экрана  [" + keyPanic.Value + "]", 2)) Panic();
        }

        IEnumerator CoHardCombo(int actor)
        {
            PrankTo(actor, FxKind.Hack, 11f); yield return new WaitForSeconds(7.5f);
            PrankTo(actor, FxKind.Quake, 7f); yield return new WaitForSeconds(5.5f);
            PrankTo(actor, FxKind.Flash, 5f); yield return new WaitForSeconds(7f);
            PrankTo(actor, FxKind.FakeCrash, 13f);
        }

        // ========================================= ШЕЙДЕРЫ =========================================
        static readonly ShadeKind[] ShadeFilm = { ShadeKind.Film, ShadeKind.Noir, ShadeKind.Sunset, ShadeKind.Cyber, ShadeKind.Dream, ShadeKind.Night, ShadeKind.Thermal, ShadeKind.GameBoy, ShadeKind.Poster, ShadeKind.Negative };
        static readonly ShadeKind[] ShadeWarp = { ShadeKind.Vhs, ShadeKind.Glitch, ShadeKind.Drunk, ShadeKind.Acid, ShadeKind.Fisheye, ShadeKind.Water, ShadeKind.Tunnel };
        static readonly ShadeKind[] ShadeScary = { ShadeKind.Rage, ShadeKind.Radiation, ShadeKind.Horror };
        float shadeDur = 30f; bool shadeForever;

        void ApplyShade(ShadeKind k)
        {
            float d = shadeForever && targetActor == -2 ? 0f : shadeDur;
            if (targetActor == -2 || (targetActor == -1 && Game.Others().Count == 0)) { Shade.Set(k, d); return; }
            if (!Game.InRoom) { Ui.Toast("Ты не в лобби — слать некому", Ui.Bad); return; }
            if (Net.Send("shade", k + ":" + shadeDur.ToString("0", System.Globalization.CultureInfo.InvariantCulture), targetActor))
                Ui.Toast("Отправлено: «" + Shade.RuName(k) + "»  [у друзей должен быть PeakFun]", Ui.Accent2);
        }

        void TabShade()
        {
            Ui.Header("На кого");
            TargetChips(true);
            TargetBanner();
            shadeDur = Ui.Slider("Сколько держится", shadeDur, 5f, 120f, "0", " с");
            shadeForever = Ui.Toggle("На мне — пока сам не выключу", shadeForever, "Для друзей действует ограничение по времени, а F8 у них убирает сразу");
            string curTxt = Shade.Active ? "Сейчас включён: «" + Shade.RuName(Shade.Cur) + "»" : "Сейчас шейдер выключен";
            Ui.Para(curTxt, 14, Shade.Active ? Ui.Good : Ui.Muted);

            Shade.Strength = Ui.Slider("Сила эффекта", Shade.Strength, 0.2f, 1f, "0.00");
            Ui.Para("Состояние: " + Shade.Status, 12, Shade.Ready ? Ui.Good : Ui.Muted);

            Ui.Header("Кино и цвет");
            ShadeGrid(ShadeFilm, 10);
            Ui.Header("Искажения и безумие");
            ShadeGrid(ShadeWarp, 11);
            Ui.Header("Страшное");
            ShadeGrid(ShadeScary, 12);

            if (Ui.Button("ВЫКЛЮЧИТЬ шейдер", 1))
            {
                if (targetActor == -2 || Game.Others().Count == 0) Shade.Off();
                else Net.Send("shade", "None:0", targetActor);
            }
            Ui.Para("Это настоящая пост-обработка на видеокарте: мод включает штатные эффекты URP игры (Bloom, цветокоррекция, хром. аберрация, зерно, виньетка, дисторсия, глубина резкости) через свой Volume, а цветовые «плёнки» (негатив, тепловизор, Game Boy…) считает GPU по таблице цвета (LUT). Поверх экрана ничего не рисуется, родной HUD остаётся. Собственные .shader-файлы без редактора Unity собрать нельзя — для мультяшных контуров/пикселизации поставь мод PeakFilter (Thunderstore), он работает рядом. Строка «Состояние» покажет, какие эффекты URP нашлись в игре.", 12);
        }

        void ShadeGrid(ShadeKind[] kinds, int id)
        {
            var names = new string[kinds.Length]; for (int i = 0; i < names.Length; i++) names[i] = Shade.RuName(kinds[i]);
            int c = Ui.Buttons(names, 3, id, 40f, true, Shade.Active ? Array.IndexOf(kinds, Shade.Cur) : -1, 14);
            if (c >= 0) { var k = kinds[c]; Safe("шейдер", () => ApplyShade(k)); }
        }

        // ========================================= ОБЛИК =========================================
        const string TransformUrl = "https://thunderstore.io/c/peak/p/Thanks/PEAK_Transform/";

        static string FindTransformMod()
        {
            try
            {
                foreach (var kv in BepInEx.Bootstrap.Chainloader.PluginInfos)
                {
                    string key = (kv.Key ?? "") + " " + (kv.Value != null && kv.Value.Metadata != null ? kv.Value.Metadata.Name : "");
                    string kl = key.ToLowerInvariant();
                    if (kl.Contains("transform") || kl.Contains("scoutmaster") || kl.Contains("enemycontrol")) return key.Trim();
                }
            }
            catch { }
            return null;
        }

        void TabLook()
        {
            Ui.Header("Скины и косметика");
            bool sk = Ui.Toggle("Открыть ВСЕ скины", SkinUnlock.On, "Включено — все предметы внешности доступны в паспорте. Выключил — всё снова закрыто, как в обычной игре");
            if (sk != SkinUnlock.On) { SkinUnlock.Set(sk); cfgUnlockSkins.Value = sk; Ui.Toast(sk ? "Скины открыты — перезайди в паспорт (меню внешности)" : "Скины снова закрыты, как было", sk ? Ui.Good : Ui.Muted); }
            Ui.Para("Состояние: " + (SkinUnlock.On ? (SkinUnlock.Patched ? "включено, " + SkinUnlock.Status : "НЕ сработало — " + SkinUnlock.Status) : "выключено (всё как в обычной игре)"), 12, SkinUnlock.On && !SkinUnlock.Patched ? Ui.Bad : Ui.Muted);
            Ui.Para("Это обратимо: в сохранения и достижения Steam ничего не пишется — мод только отвечает «не закрыто» на запрос игры. Откат — выключить тумблер (или удалить мод). Если надел закрытую вещь, а потом выключил тумблер, игра может оставить её надетой до смены внешности — просто выбери другую. Друзья видят твои скины, как обычно.", 12);

            if (!NeedMe()) return;
            Ui.Header("Настоящее превращение в Скаутмастера");
            string tm = FindTransformMod();
            Ui.Para(tm != null ? "Найден мод превращения: " + tm + " — он уже стоит!" : "Мод превращения пока не найден. Он отдельный — ставится за минуту:", 13, tm != null ? Ui.Good : Ui.Accent);
            Ui.Para("Это делает мод PEAK Transform (автор Thanks): ты становишься настоящей моделью Скаутмастера и ходишь, лазаешь, хватаешь и бросаешь, как он (а ещё зомби, лягушка, призрак и ещё 10 форм). В игре зажми F на 1 секунду → меню форм → выбери Скаутмастера, дальше F — превратиться/вернуться. Ссылка и подробности — вкладка «Инфо».", 13);
            if (Ui.Button("Скопировать ссылку на PEAK Transform", 1)) { GUIUtility.systemCopyBuffer = TransformUrl; Ui.Toast("Ссылка скопирована — вставь в браузер", Ui.Good); }
            Ui.Para("Почему не встроено в PeakFun: это сложная штука на ~3000 строк (камера, физика, сетевая синхронизация модели), а чужой код без разрешения автора я в пакет не кладу.", 12);

            Ui.Header("Невидимка (пранк)");
            bool inv = Ui.Toggle("Стать невидимым для друзей", invisible, "Работает у друзей с PeakFun: твоего персонажа они просто не видят");
            if (inv != invisible) Safe("невидимка", () => SetInvisible(inv));
            Ui.Para("Двигаться можно как обычно, друзья слышат шаги и видят предметы, но тебя — нет. У друзей без PeakFun игра не даёт спрятать игрока, там ты останешься виден.", 12);

            Ui.Header("Режим великана-страшилы");
            bool sm = Ui.Toggle("Огромный, быстрый, неуязвимый", scoutMode, "Размер ×2.3 + энергетик + неуязвимость; друзья с PeakFun видят помехи, когда ты рядом");
            if (sm != scoutMode) SetScoutMode(sm);
            float s = Ui.Slider("Размер", sizeTarget, 0.25f, 5f, "0.00", "×");
            if (Mathf.Abs(s - sizeTarget) > 0.0005f) SetSize(s);
            int c = Ui.Buttons(new[] { "Малыш ×0.4", "Обычный ×1", "Гигант ×2.5", "Титан ×4" }, 4);
            if (c == 0) SetSize(0.4f); if (c == 1) SetSize(1f); if (c == 2) SetSize(2.5f); if (c == 3) SetSize(4f);
            Ui.Para("Если физика персонажа ведёт себя странно — жми «Обычный ×1». Размер на персонажах других игроков меняется только на экранах с PeakFun.", 12);
        }

        // ========================================= ХАОС =========================================
        void TabChaos()
        {
            bool cc = Ui.Toggle("РЕЖИМ ХАОСА  [" + keyChaos.Value + "]", chaos, "Каждые N секунд случайное событие со случайным игроком");
            if (cc != chaos) SetChaos(cc);
            chaosMin = Ui.Slider("Минимальная пауза", chaosMin, 5f, 300f, "0", " с");
            chaosMax = Ui.Slider("Максимальная пауза", chaosMax, 10f, 600f, "0", " с");
            if (chaosMax < chaosMin + 1f) chaosMax = chaosMin + 1f;

            Ui.Header("Что может случиться");
            evTrip = Ui.Toggle("Игрок спотыкается", evTrip);
            evLaunch = Ui.Toggle("Игрок летит в космос", evLaunch);
            evSwap = Ui.Toggle("Двое меняются местами", evSwap);
            evSleep = Ui.Toggle("Игрок засыпает", evSleep);
            evSummon = Ui.Toggle("Все телепортируются к тебе", evSummon);
            evZombie = Ui.Toggle("Зомби возле игрока", evZombie);
            evScare = Ui.Toggle("Случайная пугалка (нужен PeakFun)", evScare);
            evEffect = Ui.Toggle("Случайный эффект (нужен PeakFun)", evEffect);
            evGift = Ui.Toggle("Случайный предмет под ноги", evGift);
            if (Ui.Button("Запустить случайное событие сейчас", 1)) Safe("хаос", RunChaosEvent);
            if (chaos) Ui.Para("Следующее событие через ~" + Mathf.Max(0, Mathf.RoundToInt(nextChaos - Time.time)) + " с", 13, Ui.Accent);
        }

        // ========================================= ИНФО =========================================
        void TabInfo()
        {
            ThemePicker();
            Ui.Header("Состояние");
            Ui.Label("Версия: " + Version);
            Ui.Label("Игра распознана: " + (Game.Ready ? "да" : "НЕТ — типы PEAK не найдены"), 15, Game.Ready ? Ui.Good : Ui.Bad);
            Ui.Label("Персонаж: " + (Game.Me() != null ? "найден" : "нет"));
            Ui.Label("Хост лобби: " + (Game.IsMaster() ? "ты" : "не ты") + "   ·   в комнате: " + (Game.InRoom ? "да" : "нет"));
            Ui.Label("Игроков: " + Game.Everyone().Count);
            Ui.Label("Канал PeakFun: " + Net.Status);
            Ui.Label("Предметов в списке: " + itemNames.Count);
            Ui.Label("Шейдеры (URP Volume): " + Shade.Status, 13, Shade.Ready ? Ui.Good : Ui.Muted);
            Ui.Label("Ник бота над головой: " + (NamePatchOk ? "через игру (Harmony)" : "запасной (надпись мода)"), 13, NamePatchOk ? Ui.Good : Ui.Muted);

            Ui.Header("Клавиши");
            Ui.Label(keyMenu.Value + " — меню    " + keyFly.Value + " — полёт    " + keyChaos.Value + " — хаос    " + keyPanic.Value + " — стоп (убрать пугалки)");

            Ui.Label("Блокировка ввода при меню: " + (InputBlock.Patched ? "через игру (Harmony)" : "запасной способ — " + inputBlockInfo), 13, InputBlock.Patched ? Ui.Good : Ui.Accent);

            Ui.Header("Кто что видит");
            Ui.Para("• Мод у друзей НЕ нужен: подножка, катапульта, обморок, телепорты, воскрешение, зомби и Скаутмастер (последний — только если хост ты), бот-спутник.\n• Мод у друзей НУЖЕН: скример, огонь, помехи, все «лаги», «жёсткие» пранки, шейдеры, фейки, эффекты на друга, размер и невидимка.\n• Бафы, лазание, скорость, предметы и самовозрождение — только твои.", 13);

            Ui.Header("Настоящее превращение в Скаутмастера");
            Ui.Para("Ставится отдельным модом PEAK Transform (13 форм: Скаутмастер, зомби, лягушка, призрак…). Скачай .zip со страницы по ссылке ниже (Manual Download), открой, из папки plugins положи Thanks.Transform.dll в BepInEx/plugins рядом с PeakFun.dll. Хост и друзья должны поставить его тоже, чтобы все видели модель.", 13);
            Ui.Label(TransformUrl, 13, Ui.Accent);
            if (Ui.Button("Скопировать ссылку", 1)) { GUIUtility.systemCopyBuffer = TransformUrl; Ui.Toast("Ссылка скопирована", Ui.Good); }

            Ui.Header("Честно");
            Ui.Para("Мод собран без запуска игры: все вызовы идут через рефлексию и защищены от ошибок. Если какая-то кнопка пишет «не вышло» — открой BepInEx/LogOutput.log и пришли строки со словом PeakFun: я подправлю под твою версию игры.", 13);
        }
    }
}
