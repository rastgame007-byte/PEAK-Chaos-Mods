// Вкладка «Проверка»: проба хуков игры и «сухой» прогон всех модификаторов без изменения мира.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PeakMods
{
    public partial class PeakModsPlugin
    {
        string testReport = "";

        void TabTest()
        {
            Ui.Header("Проверка");
            Ui.Para("Проверяет, что мод видит игру (персонаж, сеть, карта, предметы), и гоняет каждый модификатор вхолостую: ничего в мире не меняется. Запускай в игре, лучше в лагере.", 13, Ui.Muted);
            if (Ui.Button("Запустить проверку", 0, !runOn)) { try { testReport = RunCheck(); } catch (Exception e) { testReport = "Проверка упала: " + e; } try { System.IO.File.WriteAllText(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "PeakMods_check_latest.txt"), testReport); } catch { } }
            if (runOn) Ui.Label("Во время забега проверка недоступна.", 13, Ui.Muted);
            if (testReport != "") { Ui.Para(testReport, 12, Ui.Txt); Ui.Label("Отчёт сохранён: BepInEx\\PeakMods_check_latest.txt", 12, Ui.Muted); }
        }

        string RunCheck()
        {
            var sb = new StringBuilder(); int bad = 0;
            Action<string, bool, string> row = (n, ok, d) => { sb.AppendLine((ok ? "[ОК]   " : "[ОШИБКА] ") + n + (string.IsNullOrEmpty(d) ? "" : " — " + d)); if (!ok) bad++; };
            sb.AppendLine("PeakMods " + Version + " · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            row("Игра распознана", Game.Ready, "");
            var me = Game.Me(); row("Персонаж найден", me != null, me != null ? Game.Name(me) : "зайди в лагерь");
            row("Сеть Photon", Net.Status.Contains("активен"), Net.Status);
            row("Блокировка ввода", InputBlock.Patched, InputBlock.Patched ? "" : "не критично");
            row("Карта (MapHandler)", FindMap() != null, "биом " + SegCur());
            Vector3 an; row("Якорь биома", SegAnchor(Math.Max(0, SegCur()), out an), "");
            try { if (!itemsLoaded) LoadItems(); } catch { }
            row("Каталог предметов", itemList.Count > 0, itemList.Count + " шт. " + ItemStatus);
            if (me != null)
            {
                row("Аффликции", Game.Afflictions(me) != null, "");
                row("Движение (Movement)", Game.Movement(me) != null, "");
                row("Тела (Rigidbody)", Game.Rigs(me).Any(), "");
                row("Кандидаты Terrain/маска", TerrainMask() != 0, "");
            }
            sb.AppendLine("--- Сухой прогон модификаторов ---");
            Dry = true; DryCalls = 0; int dryOk = 0; var savedSeed = runSeed; runSeed = 4242; var wasSeg = Seg; Seg = Math.Max(0, SegCur());
            foreach (var m in All)
            {
                string stage = "start"; float savedCur = m.cur;
                try
                {
                    m.cur = m.def; m.t = m.t2 = 0f; m.n = 0; m.o = null; m.errors = 0; m.broken = false;
                    if (m.start != null) m.start(m); stage = "biome"; if (m.biome != null) m.biome(m, Seg);
                    stage = "tick"; for (int i = 0; i < 40; i++) { SegT = i * 0.5f; if (m.tick != null) m.tick(m, 0.5f); }
                    stage = "hud"; if (m.hud != null) m.hud(m); stage = "stop"; if (m.stop != null) m.stop(m); dryOk++;
                }
                catch (Exception e) { sb.AppendLine("[ОШИБКА] " + m.name + " (" + m.id + ") на этапе " + stage + ": " + (e.InnerException ?? e).Message); bad++; }
                finally { Clean(m); m.cur = savedCur; m.o = null; }
            }
            Dry = false; runSeed = savedSeed; Seg = wasSeg; mSpd.Clear(); mJmp.Clear(); mClm.Clear(); mGrv.Clear(); mLgt.Clear(); wantBuff.Clear(); SetFog(-1f); SetFov(-1f); Shade.Off();
            sb.AppendLine("Модификаторов прогнано без ошибок: " + dryOk + " из " + All.Count + " (мировых действий пропущено: " + DryCalls + ")");
            // взаимные исключения
            var bySet = new HashSet<string>(); int dup = 0; foreach (var m in All) if (!bySet.Add(m.id)) dup++; row("Уникальные id", dup == 0, "");
            for (int pi = 0; pi < Presets.Length; pi++) { var p = Presets[pi]; bool ok = true; for (int a = 2; a < p.Length; a++) { if (!ById.ContainsKey(p[a])) ok = false; for (int b = a + 1; b < p.Length; b++) if (Conflicts(p[a], p[b])) ok = false; } row("Пресет «" + p[0] + "»", ok, "очки " + Points(p.Skip(2)) + " — " + DiffLabel(Points(p.Skip(2)))); }
            sb.AppendLine(bad == 0 ? "ИТОГ: всё в порядке." : "ИТОГ: проблем — " + bad + ". Пришлите этот отчёт и BepInEx\\LogOutput.log.");
            return sb.ToString();
        }
    }
}
