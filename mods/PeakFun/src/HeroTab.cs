using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        void TabHero()
        {
            Ui.Header("Превращение в героя");
            Ui.Para("Выбери героя — и ты сразу им становишься: у каждого свой полёт, свои способности и пассивки. Друзьям мод не нужен: полёт, захват, удары и отбросы идут через настоящую физику игры, и они видят всё это сами.", 13);
            if (!NeedMe()) return;

            Ui.Header("Тест в одиночку (без друга)");
            Ui.Para("Клавиша [" + keyTest.Value + "]. Бот-спутник встаёт рядом как жертва, а то, что ты отправляешь друзьям (купол расширения, шары, лучи, искажение экрана), приходит и тебе — так ты видишь свою технику «глазами друга». Две копии игры на одном ПК запустить нельзя (Steam и Photon считают это одним игроком), поэтому сделано так.", 12);
            bool st = Ui.Toggle("Режим теста в одиночку", soloTest, "Включает бота-жертву и эхо-режим");
            if (st != soloTest) Safe("тест", () => SetSoloTest(st));
            echoShade = Ui.Toggle("В эхо-режиме искажать мой экран внутри своего купола", echoShade, "Показывает, что видят друзья внутри Безграничной пустоты / Святилища. F8 — снять");
            int om = Ui.Buttons(new[] { "Камера обычная", "Спереди  [" + keyObserve.Value + "]", "Глазами бота" }, 3, 1, 36f, true, obsMode, 13);
            if (om >= 0) SetObserve(om);
            if (Ui.Button(puppetOn ? "Вернуться в себя  [" + keyPuppet.Value + "]" : "Перейти в бота и управлять им  [" + keyPuppet.Value + "]", puppetOn ? 2 : 0, npc != null || puppetOn)) TogglePuppet();
            Ui.Para("«Глазами бота» — камера стоит у бота и смотрит на тебя, а ты продолжаешь играть и кастовать. Целиться в этом виде неудобно: смотри, как выглядят техники и купол. «Перейти в бота» — ты управляешь ботом (техники героя в это время недоступны).", 11, Ui.Muted);
            if (Ui.Button(npc == null ? "Создать бота-жертву" : "Убрать бота", npc == null ? 1 : 2)) { if (npc == null) Safe("бот", SpawnCompanion); else Safe("бот", RemoveCompanion); }

            var kinds = new[] { HeroKind.Patriot, HeroKind.Invincible, HeroKind.Flash, HeroKind.Brute, HeroKind.Phantom, HeroKind.Mind, HeroKind.Gojo, HeroKind.Sukuna, HeroKind.Kagami, HeroKind.Aomine, HeroKind.Kise, HeroKind.Midorima, HeroKind.Kuroko, HeroKind.Akashi, HeroKind.Murasakibara };
            var labels = new List<string>();
            int sel = -1;
            for (int i = 0; i < kinds.Length; i++) { labels.Add(HeroName(kinds[i])); if (kinds[i] == hero) sel = i; }
            int c = Ui.Buttons(labels.ToArray(), 3, 1, 46f, true, sel, 17);
            if (c >= 0) Safe("герой", () => SetHero(kinds[c]));

            if (hero != HeroKind.None)
            {
                Ui.Header(HeroName(hero));
                Ui.Para(HeroInfo(hero).Replace("[A]", "[" + keyAb1.Value + "]").Replace("[B]", "[" + keyAb2.Value + "]"), 13, Ui.Accent);
                int a = Ui.Buttons(new[] { HeroA(hero) + "  [" + keyAb1.Value + "]", HeroB(hero) + "  [" + keyAb2.Value + "]" }, 2, 0, 40f, true, -1, 15);
                if (a == 0) { if (hero == HeroKind.Patriot) Ui.Toast("Лазер — зажимай клавишу [" + keyAb1.Value + "] (не кнопку в меню)", Ui.Muted); else UseA(); }
                if (a == 1) UseB();
                if (IsAnime(hero) || IsHoop(hero))
                {
                    int a2 = Ui.Buttons(new[] { HeroC(hero) + "  [" + keyAb3.Value + "]", (IsHoop(hero) ? "ГОРЯЧАЯ РУКА  [" : "ВЕЛИКИЙ КУПОЛ  [") + keyDomain.Value + "]" }, 2, 1, 44f, true, -1, 15);
                    if (a2 == 0) UseC();
                    if (a2 == 1) TryDomain();
                    if (IsHoop(hero)) { zoneDur = Ui.Slider("Длительность горячей руки", zoneDur, 6f, 40f, "0", " с"); zoneCd = Ui.Slider("Перезарядка горячей руки", zoneCd, 10f, 120f, "0", " с"); }
                    Ui.Para(IsHoop(hero) ? "Z/V/H — приёмы героя, G — Горячая рука. R — взять мяч, ЛКМ — бросок, Пробел+ЛКМ — данк, ПКМ — пас, Q — кроссовер. Кольцо и мяч — в разделе «Баскетбол» ниже." : hero == HeroKind.Gojo ? "Z (держать) — «Магнит»: притягивает друзей и предметы к шару перед тобой, колесо мыши — расстояние. Q — телепорт на 14 м. Пассивка «Щит» мягко отталкивает всех, кто подошёл." : "Z (держать) — «Град клинков»: град клинков туда, куда смотришь. V — «Клин»: по другу в прицеле или длинный по земле. Обычный Q — уклон.", 12, Ui.Muted);
                }
                if (Ui.Button("Выйти из героя (всё вернётся как было)", 2)) SetHero(HeroKind.None);
            }
            else Ui.Para("Сейчас ты обычный. Клавиша [" + keyHero.Value + "] — случайный герой.", 12, Ui.Muted);

            Ui.Header("Великий купол (Альбус и Владыка Морок)");
            Ui.Para("Нажми [" + keyDomain.Value + "] — каст 1.4 с с заставкой, потом вокруг вырастает купол. Альбус — «Звёздная бездна»: космос, все внутри роняются и парализованы, у друзей с PeakFun искажается картинка. Владыка Морок — «Багровый чертог»: святилище из костей, непрерывные клинки по всем внутри, всё вокруг крошится. Выйти из купола нельзя — барьер возвращает обратно.", 12);
            domR = Ui.Slider("Радиус купола", domR, 12f, 45f, "0", " м");
            domDur = Ui.Slider("Длительность", domDur, 8f, 60f, "0", " с");
            domCd = Ui.Slider("Перезарядка после окончания", domCd, 10f, 180f, "0", " с");
            domPow = Ui.Slider("Интенсивность (частота ударов, тряска)", domPow, 0.4f, 2.5f, "0.0", "×");
            domBarrier = Ui.Toggle("Барьер не выпускает жертв", domBarrier, "Тех, кто оказался внутри, возвращает обратно: силой, а если улетели далеко — телепортом");
            infinityOn = Ui.Toggle("Альбус: пассивка «Щит»", infinityOn, "Мягко отталкивает друзей и предметы, подошедшие ближе ~2.8 м");
            if (Ui.Button(domActive ? "Завершить «Великий купол» сейчас" : "Великий купол: перезарядка " + (Time.time < cdDom ? Mathf.CeilToInt(cdDom - Time.time) + " с" : "готово"), domActive ? 2 : 0, domActive || (IsAnime(hero) && Time.time >= cdDom))) { if (domActive) EndDomain(); else TryDomain(); }

            Ui.Header("Схватить за горло");
            Ui.Para("Подойди к другу (до ~4.5 м), смотри на него и нажми [" + keyGrab.Value + "]: он повиснет у тебя в руке и будет лететь, куда летишь ты — с любым героем, который умеет летать. [" + keyThrow.Value + "] — швырнуть, повторное [" + keyGrab.Value + "] — отпустить. Работает и на боте-спутнике, если хочешь потренироваться в одиночку.", 13);
            string gl = grabbed != null ? "Отпустить: " + Game.Name(grabbed) : "Схватить того, на кого смотришь  [" + keyGrab.Value + "]";
            if (Ui.Button(gl, grabbed != null ? 2 : 1)) ToggleGrab();
            if (Ui.Button("Швырнуть  [" + keyThrow.Value + "]", 0, grabbed != null)) ThrowGrabbed();

            Ui.Header("Настройки");
            heroPoseOn = Ui.Toggle("Поза полёта (лёжа, как Супермен)", heroPoseOn, "Экспериментально: наклоняет тело по направлению полёта. Если тело дёргается — выключи");
            heroRandom = Ui.Toggle("Менять героя случайно каждые 45 с", heroRandom, "Для веселья: каждые 45 секунд новый герой");
            heroPower = Ui.Slider("Сила ударов", heroPower, 0.3f, 3f, "0.0", "×");
            flySpeed = Mathf.Max(3f, Ui.Slider("Скорость полёта", flySpeed, 3f, 80f, "0", ""));
            Ui.Para("Управление полётом: W/A/S/D — по взгляду камеры, Пробел — вверх, левый Ctrl — вниз, Shift — ускорение.", 12, Ui.Muted);

            BasketTab();
            CineSettings();

            Ui.Header("Боёвка");
            Ui.Para("ЛКМ — комбо (Неуязвимый — 5 быстрых ударов, остальные — 3, последний с мощным отбросом; по воздуху — добивание). Зажми ПКМ и отпусти — тяжёлый удар с рывком, кратером и разрушением. Лети быстро + ЛКМ — таран сквозь врагов. Q — уклон, C (держать) — стойка. Пока держишь за горло: ЛКМ — бить схваченного, ПКМ — удар о землю.", 12);
            combatOn = Ui.Toggle("Боёвка на мышке (ЛКМ / ПКМ)", combatOn, "Выключи, если мешает обычным действиям игры");
            combatItems = Ui.Toggle("Бить, даже когда в руках предмет", combatItems, "Иначе с предметом в руках боёвка молчит, чтобы не использовать предмет случайно");
            hitStop = Ui.Toggle("Замирание кадра при сильном ударе", hitStop, "Короткая пауза на 0.04–0.12 с — удары ощущаются тяжелее");
            heroPower = Ui.Slider("Сила ударов героя", heroPower, 0.3f, 3f, "0.0", "×");

            Ui.Header("Захват за горло");
            grabPhys = Ui.Toggle("Плавный захват (физикой, не телепортом)", grabPhys, "Схваченного ведёт сила с демпфированием, вы летите вместе без рывков. Если не держит — сам переключится на телепорт");
            grabStiff = Ui.Slider("Жёсткость захвата", grabStiff, 8f, 60f, "0", "");
            Ui.Para("X — схватить, ЛКМ — бить, ПКМ или Z — удар о землю, V — взлёт и сброс, B — бросок.", 11, Ui.Muted);

            Ui.Header("Разрушение");
            Ui.Para("Сильные удары ломают объекты карты: брёвна, камни, постройки меньше заданного размера. Они пропадают, а на их месте летят обломки. Земля и скалы побольше не ломаются — на них остаются кратеры и осыпь. Разрушенное само возвращается.", 12);
            Destruct.On = Ui.Toggle("Включить разрушение", Destruct.On);
            Destruct.MaxSize = Ui.Slider("Ломать объекты размером до", Destruct.MaxSize, 1f, 40f, "0.0", " м");
            if (Destruct.MaxSize > 12f) Ui.Para("Много: можно сломать то, на чём держится маршрут, или провалиться. Есть кнопка «Восстановить всё».", 11, Ui.Bad);
            Destruct.Protect = Ui.Toggle("Не ломать то, на чём стою", Destruct.Protect);
            Destruct.RestoreAfter = Ui.Slider("Восстанавливать через", Destruct.RestoreAfter, 0f, 600f, "0", " с");
            if (Destruct.RestoreAfter < 1f) Ui.Para("0 — не восстанавливать совсем (только кнопкой).", 11, Ui.Muted);
            int dc = Ui.Buttons(new[] { "Тест: удар перед собой", "Восстановить всё (" + Destruct.BrokenCount + ")" }, 2, 1);
            if (dc == 0) { var cam = Camera.main; if (cam != null) { Vector3 o = cam.transform.position, f = cam.transform.forward; RaycastHit h; Vector3 pt = o + f * 5f; if (Physics.Raycast(o, f, out h, 20f, ~0, QueryTriggerInteraction.Ignore)) pt = h.point; int n = Destruct.Smash(pt, f, 3f, 1.5f); Gore.Impact(pt, Vector3.up, 1f); Ui.Toast(n > 0 ? "Сломано: " + n : "Ничего не сломалось (слишком большое или нет объектов)", n > 0 ? Ui.Good : Ui.Muted); } }
            if (dc == 1) { Destruct.RestoreAll(); Ui.Toast("Всё восстановлено", Ui.Good); }

            Ui.Header("Костюм героя");
            Ui.Para("Костюм рисуется кодом: эллипсоиды по частям тела, плащ с физикой, герб, причёска, маска, светящиеся глаза. У Патриота — флаг-плащ, золотой орёл и красные глаза при лазере; у Неуязвимого — сине-жёлтый костюм и чёрная маска-очки.", 12);
            costumeOn = Ui.Toggle("Показывать костюм героя", costumeOn, "Выключил — остаётся обычный скаут");
            if (hero == HeroKind.Patriot || hero == HeroKind.Invincible)
            {
                var vn = hero == HeroKind.Patriot ? new[] { "Классический (флаг-плащ)", "Тёмный (чёрный костюм)" } : new[] { "Сине-жёлтый", "Чёрно-красный (Mk2)" };
                int vv = Ui.Buttons(vn, 2, 1, 36f, true, costumeVar, 14); if (vv >= 0) { costumeVar = vv; SendHero(); }
            }
            costumeSync = Ui.Toggle("Видеть костюмы, лучи и кровь друзей с PeakFun", costumeSync, "Друзья с PeakFun видят твой костюм, а ты — их");
            suitThick = Ui.Slider("Толщина костюма", suitThick, 0.6f, 1.8f, "0.00", "×");
            headScale = Ui.Slider("Размер деталей головы (волосы, маска)", headScale, 0.6f, 1.8f, "0.00", "×");
            heroArmsOn = Ui.Toggle("Анимация рук (кулак вперёд, замах, рука к шее)", heroArmsOn, "Экспериментально: двигает звенья рук. Если дёргается — выключи");
            var pmm = PartMap.Of(Game.Me());
            Ui.Para("Части тела: " + (pmm == null ? "—" : (pmm.limbs ? "найдены, костюм по частям работает" : "не распознаны ("+pmm.names+") — будут только плащ, герб, волосы")), 11, pmm != null && pmm.limbs ? Ui.Muted : Ui.Bad);

            Ui.Header("Кровь и эффекты ударов");
            Gore.On = Ui.Toggle("Кровь при ударах", Gore.On, "Брызги, лужи, пятна на скалах и телах, кратеры");
            Gore.Level = Ui.Slider("Густота", Gore.Level, 0.5f, 3f, "0.0", "×");
            int pl = Ui.Buttons(Gore.PaletteNames, 2, 0, 34f, true, Gore.Palette, 13);
            if (pl >= 0) Gore.Palette = pl;
            int gc = Ui.Buttons(new[] { "Тест: брызги перед собой", "Убрать все пятна" }, 2, 1);
            if (gc == 0) { var cam = Camera.main; if (cam != null) Gore.Burst(cam.transform.position + cam.transform.forward * 3f, cam.transform.forward, 1.6f); }
            if (gc == 1) { Gore.Clear(); Vfx.Clear(); }
            Ui.Para("Пятна остаются ~1 минуту и потом тают. Друзья с PeakFun видят брызги и кратеры, у остальных этого нет.", 11, Ui.Muted);

            Ui.Header("Что в игре");
            Ui.Para("Видно всем: полёт, захват, удары, отбросы, телепорты, размер, невидимость Призрака (у друзей с PeakFun). Только у тебя: красный луч, молнии, вспышки экрана и звуки.", 12, Ui.Muted);
            Ui.Para("Осторожно: Призрак проходит сквозь стены — выключай это в воздухе, а не внутри скалы. Клавиши меняются в BepInEx/config/com.friends.peakfun.cfg.", 12, Ui.Muted);
        }
    }
}
