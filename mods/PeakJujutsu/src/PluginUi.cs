using System.IO;
// Окно меню PeakJujutsu (F4) и самопроверка.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace PeakJJK
{
    public partial class Plugin
    {
        Vector2 winPos = new Vector2(-1, -1), dragOff, scroll; const float WinW = 980f, WinH = 664f; float contentH = 600f; int tab; float tabIndY;
        static readonly string[] TabNames = { "Персонажи", "Охота и бои", "Прокачка", "Настройки", "Инфо и проверка" };
        static List<string> testLog = new List<string>();

        void DrawWindow(float vw, float vh, Matrix4x4 baseMatrix)
        {
            var e = Event.current;
            if (winPos.x < 0) winPos = new Vector2((vw - WinW) / 2f, (vh - WinH) / 2f);
            winPos.x = Mathf.Clamp(winPos.x, -WinW + 120, vw - 120); winPos.y = Mathf.Clamp(winPos.y, 0, vh - 40);
            float eo = Ui.EaseOut(openT); Ui.Alpha = eo; var rect = new Rect(winPos.x, winPos.y, WinW, WinH); var pivot = rect.center;
            if (openT < 0.999f) { float k = 0.9f + 0.1f * eo; GUI.matrix = baseMatrix * Matrix4x4.Translate(new Vector3(pivot.x, pivot.y + (1f - eo) * 24f, 0)) * Matrix4x4.Scale(new Vector3(k, k, 1f)) * Matrix4x4.Translate(new Vector3(-pivot.x, -pivot.y, 0)); }
            Ui.Box(new Rect(rect.x - 10, rect.y - 6, rect.width + 20, rect.height + 24), new Color(0, 0, 0, 0.3f), 26); Ui.Box(rect, Ui.Bg, 18);
            var header = new Rect(rect.x, rect.y, rect.width - 60, 64); int dragId = GUIUtility.GetControlID(FocusType.Passive);
            switch (e.GetTypeForControl(dragId))
            {
                case EventType.MouseDown: if (e.button == 0 && header.Contains(e.mousePosition)) { GUIUtility.hotControl = dragId; dragOff = e.mousePosition - winPos; e.Use(); } break;
                case EventType.MouseDrag: if (GUIUtility.hotControl == dragId) { winPos = e.mousePosition - dragOff; e.Use(); } break;
                case EventType.MouseUp: if (GUIUtility.hotControl == dragId) { GUIUtility.hotControl = 0; e.Use(); } break;
            }
            GUI.BeginGroup(rect); float t = Ui.Time_;
            Ui.Orb(new Vector2(120 + Mathf.Sin(t * 0.5f) * 80, 90 + Mathf.Cos(t * 0.4f) * 40), 420, Ui.Orb1); Ui.Orb(new Vector2(WinW - 140 + Mathf.Cos(t * 0.35f) * 90, 140), 460, Ui.Orb2);
            Ui.Gradient(new Rect(0, 0, WinW, 4), 1f, t * 0.12f);
            Ui.Text(new Rect(26, 10, 360, 44), "呪術廻戦  PeakJujutsu", 28, Ui.Txt, TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(400, 14, 300, 20), "v" + Version + " · Магическая битва", 13, Ui.Muted, TextAnchor.MiddleLeft);
            Ui.Text(new Rect(400, 34, 420, 20), keyMenu.Value + " — закрыть · Z X V G H — техники · B — сменить · J — лечение", 12, Ui.Muted, TextAnchor.MiddleLeft);
            if (Skills.Cur != null) Ui.Text(new Rect(WinW - 460, 20, 400, 26), Skills.Cur.name + " · " + Prog.Grade + " · ур. " + Prog.Level, 15, Skills.Cur.col, TextAnchor.MiddleRight, true);
            if (Ui.ButtonRect(new Rect(WinW - 52, 14, 38, 38), "X", 2, true, 16)) { MenuOpen = false; Prog.Save(); }

            var side = new Rect(12, 72, 196, WinH - 84); Ui.Box(side, Ui.Side, 14);
            float rowH = 42f, y0 = side.y + 10; float selY = y0 + tab * (rowH + 4f); if (tabIndY < 1f) tabIndY = selY; tabIndY = Mathf.Lerp(tabIndY, selY, 1f - Mathf.Exp(-14f * Ui.Dt));
            if (e.type == EventType.Repaint) { Ui.Box(new Rect(side.x + 6, tabIndY, side.width - 12, rowH), new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.22f), 10); Ui.Box(new Rect(side.x + 6, tabIndY + rowH * 0.22f, 4, rowH * 0.56f), Ui.Accent, 2); }
            for (int i = 0; i < TabNames.Length; i++) { var tr = new Rect(side.x + 6, y0 + i * (rowH + 4f), side.width - 12, rowH); if (Ui.ButtonRect(tr, TabNames[i], i == tab ? 3 : 0, true, 15)) { tab = i; scroll = Vector2.zero; } }
            Ui.Text(new Rect(side.x + 8, side.yMax - 56, side.width - 16, 48), "Один игрок · без сети\nнастройки — в конфиге BepInEx", 11, Ui.Muted, TextAnchor.LowerLeft);

            var area = new Rect(220, 72, WinW - 232, WinH - 84); Ui.Box(area, new Color(1, 1, 1, 0.025f), 14);
            float cw = area.width - 26, ch = Mathf.Max(contentH, area.height);
            scroll = GUI.BeginScrollView(new Rect(area.x, area.y + 4, area.width, area.height - 8), scroll, new Rect(0, 0, area.width - 16, ch), false, false, GUIStyle.none, GUIStyle.none);
            Ui.Begin(12, 6, cw);
            try { if (tab == 0) TabChars(); else if (tab == 1) TabHunt(); else if (tab == 2) TabProg(); else if (tab == 3) TabSettings(); else TabInfo(); }
            catch (ArgumentException) { throw; }
            catch (Exception ex) { Log("Tab " + tab, ex); }
            if (e.type == EventType.Repaint || e.type == EventType.Layout) contentH = Ui.Y + 16;
            GUI.EndScrollView();
            if (contentH > area.height && e.type == EventType.Repaint)
            {
                float vis = (area.height - 8) / contentH, barH = Mathf.Max(30, (area.height - 16) * vis), barY = area.y + 8 + (area.height - 16 - barH) * (scroll.y / Mathf.Max(1f, contentH - (area.height - 8)));
                Ui.Box(new Rect(area.xMax - 8, barY, 4, barH), new Color(1, 1, 1, 0.25f), 2);
            }
            GUI.EndGroup(); GUI.matrix = baseMatrix; Ui.Alpha = 1f;
        }

        // ---------------- персонажи ----------------
        void TabChars()
        {
            Ui.Header("Выбор персонажа");
            foreach (var c in Skills.Chars)
            {
                var r = Ui.Next(86f, 8f); bool sel = Skills.Cur == c; bool open = Skills.Unlocked(c);
                Ui.Box(r, sel ? new Color(c.col.r, c.col.g, c.col.b, 0.28f) : Ui.Card, 12); Ui.Box(new Rect(r.x, r.y + 8, 5, r.height - 16), c.col, 2);
                Ui.Text(new Rect(r.x + 18, r.y + 6, 300, 28), c.name, 20, Ui.Txt, TextAnchor.MiddleLeft, true); Ui.Text(new Rect(r.x + 280, r.y + 10, 200, 20), c.jp, 13, c.col2, TextAnchor.MiddleLeft);
                Ui.TextWrap(new Rect(r.x + 18, r.y + 36, r.width - 230, 44), c.desc + "\n" + string.Format("Z {0} · X {1} · V {2} · G {3} · H {4}", c.s[0].name, c.s[1].name, c.s[2].name, c.s[3].name, c.s[4].name), 12, Ui.Muted);
                var br = new Rect(r.xMax - 190, r.y + 22, 176, 42);
                if (Ui.ButtonRect(br, sel ? "ВЫБРАН" : (open ? "Выбрать" : "с ур. " + c.unlock), sel ? 3 : 1, open && !sel, 15)) Skills.Select(c);
            }
            Ui.Space(6); var cur = Skills.Cur; Ui.Header("Техники: " + cur.name);
            string[] k = { "Z", "X", "V", "G", "H (ульт)" };
            for (int i = 0; i < 5; i++) { var s = cur.s[i]; var r = Ui.Next(52f, 6f); Ui.Box(r, Ui.Card, 10); Ui.Text(new Rect(r.x + 12, r.y + 4, 90, 44), k[i], 18, cur.col, TextAnchor.MiddleLeft, true); Ui.Text(new Rect(r.x + 100, r.y + 4, 330, 24), s.name, 16, Ui.Txt, TextAnchor.MiddleLeft, true); Ui.Text(new Rect(r.x + 100, r.y + 26, r.width - 110, 22), s.desc, 12, Ui.Muted, TextAnchor.MiddleLeft); Ui.Text(new Rect(r.xMax - 250, r.y + 4, 240, 24), (i == 4 ? "ульт 100% + " : "") + (s.cost > 0 ? "ЦЭ " + s.cost : "без ЦЭ") + (s.cd > 0 ? " · " + s.cd + " с" : ""), 12, Ui.Accent2, TextAnchor.MiddleRight); }
        }

        // ---------------- охота ----------------
        void TabHunt()
        {
            Ui.Header("Режимы");
            Ui.Para("«Охота» — волны проклятий, каждая 5-я — босс (Дзёго → Ханами → Махито → Сукуна; если ты играешь за Сукуну, вместо него придёт Годзё). «Тренировка» — бесконечная ЦЭ, манекены не бьют в ответ.", 13, Ui.Muted);
            int m = Ui.Buttons(new[] { Hunt.Mode == 1 ? "Охота идёт…" : "▶ Начать «Охоту»", Hunt.Mode == 2 ? "Тренировка идёт…" : "▶ Тренировка", "■ Остановить режим" }, 3, 1, 44f, true, Hunt.Mode == 1 ? 0 : (Hunt.Mode == 2 ? 1 : -1), 15);
            if (m == 0) { MenuOpen = false; Hunt.Start(1); } else if (m == 1) { MenuOpen = false; Hunt.Start(2); } else if (m == 2) Hunt.Stop();
            Ui.Label(Hunt.Mode == 0 ? "Режим не запущен" : (Hunt.Mode == 2 ? "Тренировочный полигон" : "Волна " + Hunt.Wave + " · врагов осталось: " + Hunt.Remaining), 14, Ui.Txt, true);
            Ui.Space(8); Ui.Header("Призвать врага (в 18 м перед тобой)");
            string[] ids = { "wisp", "hound", "flyhead", "brute" }; var l = new List<string>(); foreach (var id in ids) l.Add(Enemies.Defs[id].name);
            int c = Ui.Buttons(l.ToArray(), 2, 0, 38f, Pl.Alive, -1, 13); if (c >= 0) SpawnFront(ids[c]);
            Ui.Label("Боссы", 14, Ui.Muted, true); string[] bs = { "jogo", "hanami", "mahito", "sukuna", "gojo" }; var bl = new List<string>(); foreach (var id in bs) bl.Add(Enemies.Defs[id].name);
            int b = Ui.Buttons(bl.ToArray(), 3, 2, 40f, Pl.Alive, -1, 14); if (b >= 0) SpawnFront(bs[b]);
            Ui.Space(6); int x = Ui.Buttons(new[] { "Убить всех врагов", "Убрать всё (враги, союзники, домены)" }, 2, 0, 38f);
            if (x == 0) { foreach (var e in World.E.ToArray()) World.Kill(e); } else if (x == 1) { World.ClearAll(); Allies.Clear(); Domains.Clear(); Fx.Clear(); }
            Ui.Space(6); Ui.Header("Статистика"); Ui.Label("Убито проклятий всего: " + Prog.TotalKills + " · рекорд волны: " + Prog.BestWave + " · боссов в этом забеге: " + Hunt.BossKills, 13, Ui.Txt);
        }
        void SpawnFront(string id) { if (!Pl.Alive) { Ui.Toast("Ты мёртв"); return; } MenuOpen = false; Enemies.Spawn(id, Pl.Pos + Pl.Fwd * 18f); }

        // ---------------- прокачка ----------------
        void TabProg()
        {
            Ui.Header("Прокачка мага");
            var r = Ui.Next(110f, 8f); Ui.Box(r, Ui.Card, 12);
            Ui.Text(new Rect(r.x + 18, r.y + 8, 400, 34), Prog.Grade, 26, Ui.Accent, TextAnchor.MiddleLeft, true); Ui.Text(new Rect(r.x + 18, r.y + 44, 500, 22), "Уровень " + Prog.Level + " / " + Prog.MaxLevel + (Prog.Level < Prog.MaxLevel ? "  ·  ОП " + Mathf.FloorToInt(Prog.Xp) + " / " + Mathf.FloorToInt(Prog.Need(Prog.Level)) : ""), 15, Ui.Txt, TextAnchor.MiddleLeft);
            Ui.Text(new Rect(r.x + 18, r.y + 70, r.width - 36, 36), "Макс. ЦЭ " + Mathf.RoundToInt(Prog.MaxCE) + " · восстановление " + Prog.Regen.ToString("0.0") + "/с · урон ×" + Prog.DmgMul.ToString("0.00"), 13, Ui.Muted, TextAnchor.MiddleLeft);
            Ui.Para("Опыт даётся за убийство проклятий (боссы — много) и за зачистку волн. Ранг растёт с уровнем: 4 → 3 → 2 → 1 → особый. При каждом уровне растут ЦЭ, регенерация и урон.", 13, Ui.Muted);
            Ui.Header("Открытие персонажей");
            foreach (var c in Skills.Chars) Ui.Label((Prog.Level >= c.unlock ? "✔ " : "🔒 ") + c.name + " — с уровня " + c.unlock, 13, Prog.Level >= c.unlock ? Ui.Good : Ui.Muted);
            Ui.Space(6); bool ua = Ui.Toggle("Все персонажи открыты сразу", CfgUnlockAll.Value, "Выключи, чтобы открывать по уровню"); if (ua != CfgUnlockAll.Value) CfgUnlockAll.Value = ua;
            Ui.Header("Быстрые действия");
            int b = Ui.Buttons(new[] { "+1 уровень", "+5 уровней", "Уровень 30", "Сбросить прогресс" }, 4, 0, 38f); if (b == 0) Prog.SetLevel(Prog.Level + 1); else if (b == 1) Prog.SetLevel(Prog.Level + 5); else if (b == 2) Prog.SetLevel(30); else if (b == 3) Prog.Reset();
        }

        // ---------------- настройки ----------------
        void TabSettings()
        {
            Ui.Header("Эффекты");
            Tg(eShake, "Тряска камеры"); Tg(eHitStop, "Заморозка при мощных ударах"); Tg(eFlash, "Вспышки на весь экран"); Tg(eSound, "Звуки техник");
            eVolume.Value = Ui.Slider("Громкость звуков", eVolume.Value, 0f, 1.5f, "0.00");
            Ui.Header("Интерфейс"); Tg(CfgHud, "Игровой HUD (слоты, ЦЭ, полоски)");
            CfgHudScale.Value = Ui.Slider("Масштаб HUD", CfgHudScale.Value, 0.6f, 1.8f, "0.00"); CfgUiScale.Value = Ui.Slider("Масштаб меню", CfgUiScale.Value, 0.7f, 1.6f, "0.00");
            var tn = new List<string>(); foreach (var th in Themes.All) tn.Add(th.name); int tt = Ui.Buttons(tn.ToArray(), 4, 0, 36f, true, Themes.Cur, 13); if (tt >= 0) { Themes.Apply(tt); CfgTheme.Value = tt; }
            Ui.Header("Баланс");
            ePower.Value = Ui.Slider("Твой урон", ePower.Value, 0.25f, 5f, "0.00", "×"); eDamage.Value = Ui.Slider("Урон по тебе", eDamage.Value, 0f, 4f, "0.00", "×"); eEnemyHp.Value = Ui.Slider("Здоровье врагов", eEnemyHp.Value, 0.25f, 5f, "0.00", "×");
            eXp.Value = Ui.Slider("Опыт", eXp.Value, 0.25f, 10f, "0.00", "×"); eCooldown.Value = Ui.Slider("Перезарядка техник", eCooldown.Value, 0.1f, 2f, "0.00", "×"); CfgMaxAlive.Value = Mathf.RoundToInt(Ui.Slider("Врагов одновременно", CfgMaxAlive.Value, 2, 30, "0"));
            Tg(CfgInfiniteCE, "Бесконечная проклятая энергия и ульт");
            Ui.Para("Урон по тебе = 0 — бессмертие от проклятий (игровые опасности PEAK — падение, холод, голод — это не отключает).", 12, Ui.Muted);
        }
        static void Tg(BepInEx.Configuration.ConfigEntry<bool> e, string label) { bool v = Ui.Toggle(label, e.Value); if (v != e.Value) e.Value = v; }

        // ---------------- инфо ----------------
        void TabInfo()
        {
            Ui.Header("Управление");
            Ui.Para("Z X V G — четыре техники персонажа · H — ульт (домен, когда шкала 100%) · B — следующий персонаж · J (держать) — обратная проклятая техника: лечение за ЦЭ · F4 — меню.\nЧёрная вспышка Юдзи: нажми X, затем ещё раз X в момент удара (подсказка «ЖМИ X!»).", 13, Ui.Txt);
            Ui.Header("Что умеет мод");
            Ui.Para("6 персонажей с настоящими техниками из аниме, 4 вида проклятий и 5 боссов с фазами, домены с куполами, призванные шикигами (псы, Нуэ, Махорага, «лучший друг»), комбо, ульт, уровни и ранги, режимы «Охота» и «Тренировка». Мод работает только у тебя — друзьям ставить не нужно и сеть он не использует.", 13, Ui.Muted);
            Ui.Header("Диагностика");
            Ui.Label("Игра распознана: " + (Game.Ready ? "да" : "нет") + " · персонаж: " + (Game.Me() != null ? "есть" : "нет"), 13, Game.Ready ? Ui.Good : Ui.Bad);
            Ui.Label("Материалы: " + Gfx.Info, 12, Ui.Muted); Ui.Label("Камера: " + Fx.HookInfo, 12, Ui.Muted);
            Ui.Label("Враги: " + World.E.Count + " · снаряды: " + World.P.Count + " · зоны: " + World.Z.Count + " · союзники: " + Allies.L.Count, 12, Ui.Muted);
            if (Ui.Button("Проверить модели, текстуры и техники", 1)) { try { testLog = SelfTest.Run(); } catch (Exception e) { testLog = new List<string> { "Ошибка проверки: " + e.Message }; } }
            foreach (var s in testLog) Ui.Label(s, 12, s.StartsWith("✘") ? Ui.Bad : (s.StartsWith("✔") ? Ui.Good : Ui.Muted));
        }
    }

    internal static class SelfTest
    {
        public static List<string> Run()
        {
            var res = new List<string>(); var sw = new Stopwatch();
            foreach (var d in Enemies.Defs.Values)
            {
                sw.Restart(); try { var m = d.make(); var go = m.Finish(); int mf = 0, vs = 0; foreach (var f in go.GetComponentsInChildren<MeshFilter>()) { mf++; if (f.sharedMesh != null) vs += f.sharedMesh.vertexCount; } UnityEngine.Object.Destroy(go); res.Add("✔ модель «" + d.name + "»: " + mf + " мешей, " + vs + " вершин, " + sw.ElapsedMilliseconds + " мс"); }
                catch (Exception e) { res.Add("✘ модель " + d.id + ": " + e.Message); }
            }
            foreach (var k in new[] { "dog_w", "nue", "mahoraga", "clone" })
            {
                try { var a = Allies.Spawn(k, Pl.Pos + Vector3.up * 200f, 0.1f); res.Add("✔ союзник " + k); } catch (Exception e) { res.Add("✘ союзник " + k + ": " + e.Message); }
            }
            sw.Restart(); try { Domains.Warm(); res.Add("✔ текстуры доменов: " + sw.ElapsedMilliseconds + " мс"); } catch (Exception e) { res.Add("✘ текстуры доменов: " + e.Message); }
            foreach (var c in Skills.Chars) { int n = 0; foreach (var s in c.s) if (s != null && s.cast != null) n++; res.Add((n == 5 ? "✔ " : "✘ ") + c.name + ": техник " + n + "/5"); }
            res.Add("✔ маска земли: " + Pl.Mask + " · материалы: " + Gfx.Info);
            try { File.WriteAllLines(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "PeakJujutsu_check_latest.txt"), res.ToArray()); } catch { }
            return res;
        }
    }
}
