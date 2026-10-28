// Главная вкладка: сводка и плитки быстрых действий — чтобы не лазить по разделам ради частых вещей.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        static readonly Color TileOn = new Color(1f, 0.5f, 0.22f, 0.28f);
        static readonly Color TileRed = new Color(1f, 0.25f, 0.3f, 0.28f);

        bool Tile(Rect r, string title, string sub, bool on, bool danger = false)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current; bool clicked = false;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && r.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); if (r.Contains(e.mousePosition)) clicked = true; }
                    break;
                case EventType.Repaint:
                    float h = Ui.Anim("tile" + title, r.Contains(e.mousePosition) ? 1f : 0f, 14f);
                    var rr = new Rect(r.x, r.y - h * 2f, r.width, r.height);
                    Ui.Box(rr, Color.Lerp(Ui.Card, Ui.CardHi, h), 14);
                    if (on) Ui.Box(rr, danger ? TileRed : TileOn, 14);
                    Ui.Box(new Rect(rr.x + 12, rr.y + 14, 4, rr.height - 28), danger ? Ui.Bad : (on ? Ui.Accent : new Color(1, 1, 1, 0.15f)), 2);
                    Ui.Text(new Rect(rr.x + 26, rr.y + 8, rr.width - 34, 26), title, 16, Ui.Txt, TextAnchor.MiddleLeft, true);
                    Ui.Text(new Rect(rr.x + 26, rr.y + 33, rr.width - 34, 22), sub, 12, on ? Ui.Accent : Ui.Muted, TextAnchor.MiddleLeft);
                    break;
            }
            return clicked;
        }

        void StatCard(Rect r, string label, string value, Color vc)
        {
            if (Event.current.type != EventType.Repaint) return;
            Ui.Box(r, Ui.Card, 12);
            Ui.Text(new Rect(r.x + 14, r.y + 6, r.width - 20, 18), label, 11, Ui.Muted, TextAnchor.MiddleLeft);
            Ui.Text(new Rect(r.x + 14, r.y + 24, r.width - 20, 24), value, 15, vc, TextAnchor.MiddleLeft, true);
        }

        void TabHome()
        {
            var me = Game.Me();
            int friends = Game.Others().Count;
            Ui.Header("Сейчас");
            var row = Ui.Next(54, 10);
            float cw = (row.width - 30) / 4f;
            StatCard(new Rect(row.x, row.y, cw, row.height), "ЛОББИ", Game.InRoom ? (friends + 1) + " игр." + (friends > 0 ? "" : " — ты один") : "ты один", friends > 0 ? Ui.Good : Ui.Txt);
            StatCard(new Rect(row.x + (cw + 10), row.y, cw, row.height), "ШЕЙДЕР", Shade.Active ? Shade.RuName(Shade.Cur) : "нет", Shade.Active ? Ui.Accent : Ui.Muted);
            StatCard(new Rect(row.x + (cw + 10) * 2, row.y, cw, row.height), "БОТ", npc != null ? BotName() : "нет", npc != null ? Ui.Good : Ui.Muted);
            StatCard(new Rect(row.x + (cw + 10) * 3, row.y, cw, row.height), "ХАОС", chaos ? "ВКЛ" : "выкл", chaos ? Ui.Accent2 : Ui.Muted);

            Ui.Header("Цель для пранков");
            TargetChips(true);

            Ui.Header("Быстрые действия");
            float tw = (Ui.W - 24) / 3f, th = 62f;
            var kinds = new List<KeyValuePair<string, string>>();
            bool dead = me != null && IsDeadOrGhost(me);
            string[] titles = { "Режим хаоса", "Бот-спутник", "Случайный шейдер", "Полёт", "Невидимка", "Возродиться", "Фейковый краш", "Пугалка", "СТОП — всё убрать", "Случайный герой", "Схватить друга", "Бросить / отпустить", "Третье лицо", "Играть за друга" };
            string[] subs =
            {
                (chaos ? "включён" : "выключен") + "  ·  " + keyChaos.Value,
                npc != null ? "подключён — нажми, чтобы отключить" : "нажми, чтобы подключить",
                Shade.Active ? "сейчас: " + Shade.RuName(Shade.Cur) : "на 30 секунд на цель",
                (flying ? "летишь" : "выключен") + "  ·  " + keyFly.Value,
                invisible ? "ты невидим для друзей" : "для друзей с PeakFun",
                (dead ? "ты мёртв — вернись" : "работает, когда ты мёртв") + "  ·  " + keyRespawn.Value,
                "«PEAK не отвечает» на цель", "громкий скример на цель", "эффекты, шейдер, пугалки  ·  " + keyPanic.Value,
                hero != HeroKind.None ? "ты — " + HeroName(hero) + "  ·  " + keyHero.Value : "полёт, лазер, молнии  ·  " + keyHero.Value,
                grabbed != null ? "держишь: " + Game.Name(grabbed) : "смотри на друга  ·  " + keyGrab.Value,
                "швырнуть  ·  " + keyThrow.Value,
                thirdOn ? "включено  ·  " + keyThird.Value : "камера за спиной  ·  " + keyThird.Value,
                puppetOn ? "управляешь: " + pupName : "смотри на друга  ·  " + keyPuppet.Value
            };
            bool[] ons = { chaos, npc != null, Shade.Active, flying, invisible, dead, false, false, false, hero != HeroKind.None, grabbed != null, false, thirdOn, puppetOn };
            for (int i = 0; i < titles.Length; i++)
            {
                if (i % 3 == 0) { Ui.Next(th, 12); }
                int col = i % 3; float y = Ui.Y - th - 12;
                var r = new Rect(Ui.X + col * (tw + 12), y, tw, th);
                if (Tile(r, titles[i], subs[i], ons[i], i == 8)) HomeAction(i);
            }
            Ui.Space(4);
            Ui.Para("Остальное — в разделах слева: «Я сам» (твои баффы и предметы), «Друзья» (пранки, жёсткие, монстры, бот), «Визуал» (шейдеры).", 12, Ui.Muted);
        }

        void HomeAction(int i)
        {
            switch (i)
            {
                case 0: SetChaos(!chaos); break;
                case 1: if (npc == null) Safe("бот", SpawnCompanion); else Safe("бот", RemoveCompanion); break;
                case 2:
                    {
                        var all = new List<ShadeKind>();
                        foreach (ShadeKind k in Enum.GetValues(typeof(ShadeKind))) if (k != ShadeKind.None && k < ShadeKind.Speed) all.Add(k);
                        var pick = all[UnityEngine.Random.Range(0, all.Count)];
                        Safe("шейдер", () => ApplyShade(pick));
                        break;
                    }
                case 3: SetFly(!flying); break;
                case 4: SetInvisible(!invisible); break;
                case 5: Safe("возрождение", () => RespawnMe()); break;
                case 6: Safe("краш", () => PrankTo(targetActor, FxKind.FakeCrash, 13f)); break;
                case 7: Safe("пугалка", () => PrankTo(targetActor, FxKind.Scream, 6f)); break;
                case 8: Panic(); break;
                case 9: Safe("герой", () => SetHero(RandomHero(hero))); break;
                case 10: Safe("хват", () => ToggleGrab()); break;
                case 12: SetThird(!thirdOn); break;
                case 13: Safe("кукловод", () => TogglePuppet()); break;
                case 11: Safe("бросок", () => { if (grabbed != null) ThrowGrabbed(); else Ui.Toast("Никого не держишь", Ui.Muted); }); break;
            }
        }
    }
}
