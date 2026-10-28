// Игровой интерфейс: слоты техник, ЦЭ, ульт, полоски врагов и босса, комбо, реплики, баннеры.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakJJK
{
    internal static class Hud
    {
        static string sayWho = "", sayText = ""; static float sayT = -99f; static string banT = "", banS = ""; static float banAt = -99f;
        public static void Say(string who, string text) { sayWho = who; sayText = text; sayT = Time.unscaledTime; }
        public static void Banner(string title, string sub) { banT = title; banS = sub; banAt = Time.unscaledTime; }

        static Color Fade(Color c, float a) { c.a = a; return c; }
        static void Bar(Rect r, float f, Color a, Color b)
        {
            Ui.Box(r, new Color(0f, 0f, 0f, 0.55f), r.height * 0.5f); f = Mathf.Clamp01(f); if (f > 0.001f) { var fr = new Rect(r.x + 2, r.y + 2, Mathf.Max(r.height - 4, (r.width - 4) * f), r.height - 4); Ui.Box(fr, a, (r.height - 4) * 0.5f); Ui.Box(new Rect(fr.x, fr.y, fr.width, fr.height * 0.5f), new Color(1, 1, 1, 0.14f), (r.height - 4) * 0.25f); }
        }

        public static void Draw()
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = Mathf.Clamp(Screen.height / 1080f * Plugin.CfgHudScale.Value, 0.6f, 3f); float vw = Screen.width / s, vh = Screen.height / s; var old = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            try { if (Plugin.CfgHud.Value) { DrawHealthBars(s); DrawBoss(vw); DrawWave(vw); DrawSlots(vw, vh); DrawChar(vh); DrawCombo(vw, vh); DrawSay(vw, vh); DrawBanner(vw, vh); DrawPrompt(vw, vh); } }
            finally { GUI.matrix = old; }
        }

        static void DrawSlots(float vw, float vh)
        {
            var c = Skills.Cur; if (c == null) return; float sz = 74f, gap = 10f, total = sz * 5 + gap * 4 + 18f; float x0 = (vw - total) / 2f, y0 = vh - sz - 46f;
            // ЦЭ
            float ce = Skills.CE / Mathf.Max(1f, Skills.MaxCE); var br = new Rect(x0, y0 - 38f, total, 18f); Bar(br, ce, Color.Lerp(new Color(0.45f, 0.2f, 1f), new Color(0.3f, 0.85f, 1f), ce), Color.white);
            Ui.Text(new Rect(br.x, br.y - 1, br.width, br.height + 2), "ЦЭ  " + Mathf.CeilToInt(Skills.CE) + " / " + Mathf.CeilToInt(Skills.MaxCE) + (Skills.Training ? "  ∞" : ""), 12, Color.white, TextAnchor.MiddleCenter, true);
            var ur = new Rect(x0 + 40f, y0 - 16f, total - 80f, 8f); Bar(ur, Skills.Ult / 100f, Skills.Ult >= 100f ? new Color(1f, 0.85f, 0.3f) : new Color(0.9f, 0.45f, 0.2f), Color.white);
            if (Skills.Ult >= 100f) Ui.Text(new Rect(ur.x, ur.y - 22f, ur.width, 20f), "УЛЬТ ГОТОВ — H", 12, Color.Lerp(new Color(1f, 0.85f, 0.3f), Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)), TextAnchor.MiddleCenter, true);
            string[] keys = { "Z", "X", "V", "G", "H" };
            for (int i = 0; i < 5; i++)
            {
                var sk = c.s[i]; float x = x0 + i * (sz + gap) + (i == 4 ? 18f : 0f); var r = new Rect(x, y0, sz, sz); bool ult = i == 4; bool afford = Skills.Training || Skills.CE >= sk.cost; bool ready = sk.Remaining <= 0f && (!ult || Skills.Ult >= 100f || Skills.Training);
                bool on = sk.toggle && Skills.Infinity;
                Ui.Box(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), on ? Fade(c.col, 0.9f) : (ready && afford ? Fade(c.col, 0.55f) : new Color(0, 0, 0, 0.5f)), 14);
                Ui.Box(r, new Color(0.05f, 0.04f, 0.08f, 0.92f), 12); Ui.Box(new Rect(r.x, r.y + r.height * 0.5f, r.width, r.height * 0.5f), Fade(c.col, 0.12f), 12);
                Ui.Text(new Rect(r.x, r.y + 6, r.width, 26), keys[i], 26, ready && afford ? Color.white : new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter, true);
                Ui.Text(new Rect(r.x + 2, r.y + 38, r.width - 4, 34), sk.name, 11, ready && afford ? new Color(1, 1, 1, 0.9f) : new Color(1, 1, 1, 0.4f), TextAnchor.UpperCenter);
                float fr = sk.Frac; if (ult && Skills.Ult < 100f && !Skills.Training) fr = 1f - Skills.Ult / 100f;
                if (fr > 0.005f) { var cr = new Rect(r.x, r.y + r.height * (1f - fr), r.width, r.height * fr); Ui.Box(cr, new Color(0, 0, 0, 0.6f), 12); if (sk.Remaining > 0.05f) Ui.Text(r, sk.Remaining.ToString(sk.Remaining < 10f ? "0.0" : "0"), 20, Color.white, TextAnchor.MiddleCenter, true); }
                if (!ult) Ui.Text(new Rect(r.x, r.yMax - 14, r.width - 6, 14), sk.cost > 0 ? Mathf.CeilToInt(sk.cost).ToString() : "", 10, afford ? new Color(0.6f, 0.85f, 1f) : new Color(1f, 0.4f, 0.4f), TextAnchor.LowerRight);
            }
            // бафы
            float bx = x0; float by = y0 - 62f;
            if (Skills.Boost) { Ui.Text(new Rect(bx, by, 150, 18), "● Усиление ЦЭ", 12, new Color(1f, 0.6f, 0.3f), TextAnchor.MiddleLeft, true); bx += 130; }
            if (Skills.Overtime) { Ui.Text(new Rect(bx, by, 150, 18), "● Сверхурочная", 12, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleLeft, true); bx += 130; }
            if (Skills.ZoneOn) { Ui.Text(new Rect(bx, by, 100, 18), "● В ЗОНЕ", 12, new Color(1f, 0.2f, 0.2f), TextAnchor.MiddleLeft, true); bx += 100; }
            if (Skills.Infinity) Ui.Text(new Rect(bx, by, 150, 18), "● Бесконечность", 12, new Color(0.5f, 0.8f, 1f), TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(x0, vh - 40f, total, 20f), "B — сменить персонажа  ·  J (держать) — обратная техника: лечение  ·  F4 — меню", 11, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter);
        }

        static void DrawChar(float vh)
        {
            var c = Skills.Cur; if (c == null) return; var r = new Rect(22f, vh - 150f, 300f, 108f);
            Ui.Box(r, new Color(0.03f, 0.02f, 0.06f, 0.72f), 14); Ui.Box(new Rect(r.x, r.y + 10, 4, r.height - 20), c.col, 2);
            Ui.Text(new Rect(r.x + 16, r.y + 6, 270, 28), c.name, 20, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(r.x + 16, r.y + 32, 270, 20), Prog.Grade + " · ур. " + Prog.Level, 13, Fade(c.col2, 1f), TextAnchor.MiddleLeft);
            float xp = Prog.Level >= Prog.MaxLevel ? 1f : Prog.Xp / Prog.Need(Prog.Level); Bar(new Rect(r.x + 16, r.y + 58, 268, 12), xp, new Color(0.5f, 0.85f, 1f), Color.white);
            Ui.Text(new Rect(r.x + 16, r.y + 74, 268, 18), Prog.Level >= Prog.MaxLevel ? "МАКС. уровень" : "ОП " + Mathf.FloorToInt(Prog.Xp) + " / " + Mathf.FloorToInt(Prog.Need(Prog.Level)) + "   ·   урон ×" + (Prog.DmgMul * Plugin.CfgPower).ToString("0.00"), 11, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft);
            if (Skills.HealGlow > 0f) Ui.Text(new Rect(r.x + 16, r.y - 22, 200, 20), "+ обратная техника", 13, Fade(new Color(0.5f, 1f, 0.6f), Skills.HealGlow), TextAnchor.MiddleLeft, true);
        }

        static void DrawCombo(float vw, float vh)
        {
            if (Combo.Count < 3) return; float k = Combo.Left / 3.5f; var r = new Rect(vw - 330f, vh * 0.42f, 300f, 90f);
            Ui.Text(new Rect(r.x, r.y, r.width, 50f), Combo.Count + " ХИТОВ", 36, Color.Lerp(Color.white, new Color(1f, 0.3f, 0.2f), Mathf.Clamp01(Combo.Count / 40f)), TextAnchor.MiddleRight, true);
            Ui.Text(new Rect(r.x, r.y + 44f, r.width, 20f), "урон +" + Mathf.RoundToInt((Combo.Mul - 1f) * 100f) + "%", 14, new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleRight);
            Bar(new Rect(r.x + 90f, r.y + 68f, 210f, 8f), k, new Color(1f, 0.5f, 0.2f), Color.white);
        }

        static void DrawBoss(float vw)
        {
            var b = Enemies.Boss; if (b == null) return; float w = 720f; var r = new Rect((vw - w) / 2f, 24f, w, 22f);
            Ui.Text(new Rect(r.x, r.y - 24f, w, 24f), b.d.name + (b.phase == 1 ? "   ·   ФАЗА 2" : ""), 20, Color.white, TextAnchor.MiddleCenter, true);
            Bar(r, b.hp / Mathf.Max(1f, b.maxHp), Color.Lerp(new Color(1f, 0.15f, 0.15f), b.d.col, 0.4f), Color.white);
            Ui.Text(r, Mathf.CeilToInt(Mathf.Max(0f, b.hp)) + " / " + Mathf.CeilToInt(b.maxHp), 12, Color.white, TextAnchor.MiddleCenter, true);
        }
        static void DrawWave(float vw)
        {
            if (Hunt.Mode == 0) return; float y = Enemies.Boss != null ? 74f : 24f; string t;
            if (Hunt.Mode == 2) t = "ТРЕНИРОВКА · ЦЭ бесконечна"; else if (Hunt.State == 0) t = "Волна " + (Hunt.Wave + 1) + " через " + Mathf.CeilToInt(Mathf.Max(0f, Hunt.NextAt - Time.time)) + " с"; else t = "Волна " + Hunt.Wave + " · осталось " + Hunt.Remaining;
            Ui.Text(new Rect((vw - 500f) / 2f, y, 500f, 22f), t, 15, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter, true);
            if (Hunt.Mode == 1) Ui.Text(new Rect((vw - 500f) / 2f, y + 20f, 500f, 18f), "рекорд: волна " + Prog.BestWave + "  ·  боссов повержено: " + Hunt.BossKills, 11, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter);
        }

        static void DrawHealthBars(float s)
        {
            var cam = Pl.Cam; if (cam == null) return; Vector3 pp = Pl.Pos;
            foreach (var e in World.E)
            {
                if (e.dead || e.d.boss) continue; float dist = Vector3.Distance(pp, e.pos); if (dist > 55f || (e.hp >= e.maxHp * 0.999f && dist > 20f)) continue;
                Vector3 sp = cam.WorldToScreenPoint(e.Center + Vector3.up * (e.Radius + 0.7f)); if (sp.z < 0.3f) continue; float x = sp.x / s, y = (Screen.height - sp.y) / s; float w = 60f;
                Bar(new Rect(x - w / 2f, y, w, 7f), e.hp / e.maxHp, new Color(0.9f, 0.25f, 0.45f), Color.white);
                Ui.Text(new Rect(x - 60f, y - 17f, 120f, 16f), e.dummy ? "Манекен" : "Ранг " + e.d.grade, 10, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter);
                if (e.stun > 0f) Ui.Text(new Rect(x - 60f, y + 8f, 120f, 16f), "оглушён", 10, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter);
            }
        }

        static void DrawSay(float vw, float vh)
        {
            float a = Time.unscaledTime - sayT; if (a > 4.5f) return; float al = Mathf.Clamp01(Mathf.Min(a * 4f, (4.5f - a) * 2f)); var r = new Rect((vw - 760f) / 2f, vh - 290f, 760f, 52f);
            Ui.Text(new Rect(r.x, r.y, r.width, 20f), sayWho, 13, Fade(new Color(1f, 0.55f, 0.45f), al), TextAnchor.MiddleCenter, true); Ui.Text(new Rect(r.x, r.y + 18f, r.width, 30f), "«" + sayText + "»", 20, Fade(Color.white, al), TextAnchor.MiddleCenter, true);
        }
        static void DrawBanner(float vw, float vh)
        {
            float a = Time.unscaledTime - banAt; if (a > 3.2f) return; float al = Mathf.Clamp01(Mathf.Min(a * 5f, (3.2f - a) * 2.5f)); float grow = 0.9f + 0.1f * Ui.EaseOut(a * 3f);
            var r = new Rect((vw - 900f) / 2f, vh * 0.2f, 900f, 120f); Ui.Box(new Rect(0, r.y + 22, vw, 76), new Color(0f, 0f, 0f, 0.5f * al), 0);
            Ui.Text(new Rect(r.x, r.y + 18f, r.width, 56f * grow), banT, (int)(44 * grow), Fade(Color.white, al), TextAnchor.MiddleCenter, true); Ui.Text(new Rect(r.x, r.y + 72f, r.width, 24f), banS, 17, Fade(new Color(1f, 0.6f, 0.5f), al), TextAnchor.MiddleCenter);
        }
        static void DrawPrompt(float vw, float vh)
        {
            if (Skills.BfWindow) { float p = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 30f); Ui.Text(new Rect((vw - 500f) / 2f, vh * 0.55f, 500f, 60f), "ЖМИ  X !", 52, new Color(1f, 0.1f * p, 0.1f * p), TextAnchor.MiddleCenter, true); }
        }
    }
}
