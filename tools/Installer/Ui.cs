using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PeakInstaller
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args));
        }
    }

    sealed class MainForm : Form
    {
        static readonly Color Bg = Color.FromArgb(18, 20, 28), Card = Color.FromArgb(28, 32, 44), Fg = Color.FromArgb(235, 238, 245),
            Dim = Color.FromArgb(150, 158, 175), Accent = Color.FromArgb(46, 160, 67), Warn = Color.FromArgb(214, 73, 73), Blue = Color.FromArgb(70, 130, 220);

        readonly TextBox pathBox = new TextBox();
        readonly Label gameState = new Label();
        readonly TextBox log = new TextBox();
        readonly List<CheckBox> checks = new List<CheckBox>();
        readonly List<Label> states = new List<Label>();
        readonly List<Button> buttons = new List<Button>();
        Button btnInstall, btnRemove, btnRemoveAll;
        bool busy;

        public MainForm(string[] args)
        {
            Text = "PEAK Mods — установщик";
            Font = new Font("Segoe UI", 9.5f);
            BackColor = Bg; ForeColor = Fg;
            ClientSize = new Size(660, 700);
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            int y = 14;
            Controls.Add(Lbl("⛰  PEAK Mods", 12, y, 620, 38, new Font("Segoe UI", 20f, FontStyle.Bold), Fg));
            y += 42;
            Controls.Add(Lbl("Автоматическая установка BepInEx и модов. Закрой игру перед установкой.", 14, y, 630, 22, null, Dim));
            y += 36;

            Controls.Add(Lbl("Папка игры (где лежит PEAK.exe):", 14, y, 400, 22, new Font("Segoe UI", 9.5f, FontStyle.Bold), Fg));
            y += 26;
            pathBox.SetBounds(14, y, 470, 28); pathBox.BackColor = Card; pathBox.ForeColor = Fg; pathBox.BorderStyle = BorderStyle.FixedSingle;
            pathBox.TextChanged += (s, e) => Refresh2();
            Controls.Add(pathBox);
            var browse = Btn("Обзор…", 494, y - 1, 70, 30, Card); browse.Click += (s, e) => Browse(); Controls.Add(browse);
            var find = Btn("Найти", 570, y - 1, 76, 30, Card); find.Click += (s, e) => AutoFind(true); Controls.Add(find);
            y += 34;
            gameState.SetBounds(14, y, 630, 22); gameState.ForeColor = Dim; Controls.Add(gameState);
            y += 34;

            Controls.Add(Lbl("Что установить:", 14, y, 400, 22, new Font("Segoe UI", 9.5f, FontStyle.Bold), Fg));
            y += 28;
            string self = Path.GetFileNameWithoutExtension(Application.ExecutablePath) ?? "";
            bool anyByName = Logic.Mods.Any(m => self.IndexOf(m.Id, StringComparison.OrdinalIgnoreCase) >= 0);
            foreach (var m in Logic.Mods)
            {
                var panel = new Panel(); panel.SetBounds(14, y, 632, 70); panel.BackColor = Card;
                var cb = new CheckBox { Text = m.Title, Left = 12, Top = 8, Width = 480, Height = 24, ForeColor = Fg, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Tag = m };
                cb.Checked = anyByName ? self.IndexOf(m.Id, StringComparison.OrdinalIgnoreCase) >= 0 : m.Id == "PeakFun";
                var d = Lbl(m.Desc, 32, 36, 590, 30, new Font("Segoe UI", 9f), Dim);
                var st = new Label { Left = 440, Top = 10, Width = 180, Height = 20, TextAlign = ContentAlignment.MiddleRight, ForeColor = Dim, Font = new Font("Segoe UI", 9f) };
                panel.Controls.Add(cb); panel.Controls.Add(d); panel.Controls.Add(st); panel.Controls.SetChildIndex(st, 0);
                Controls.Add(panel); checks.Add(cb); states.Add(st);
                y += 78;
            }
            y += 4;
            btnInstall = Btn("УСТАНОВИТЬ ВЫБРАННЫЕ", 14, y, 632, 46, Accent); btnInstall.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            btnInstall.Click += async (s, e) => await DoInstall(); Controls.Add(btnInstall);
            y += 54;
            btnRemove = Btn("Удалить выбранные моды", 14, y, 310, 34, Card); btnRemove.Click += async (s, e) => await DoRemove(false); Controls.Add(btnRemove);
            btnRemoveAll = Btn("Удалить всё (моды + BepInEx)", 336, y, 310, 34, Card); btnRemoveAll.Click += async (s, e) => await DoRemove(true); Controls.Add(btnRemoveAll);
            y += 44;
            log.SetBounds(14, y, 632, 112); log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical;
            log.BackColor = Color.FromArgb(10, 12, 18); log.ForeColor = Color.FromArgb(170, 220, 180); log.BorderStyle = BorderStyle.FixedSingle;
            log.Font = new Font("Consolas", 9f); Controls.Add(log);
            y += 118;
            var open = new LinkLabel { Text = "Открыть папку игры", Left = 14, Top = y, Width = 200, Height = 20, LinkColor = Blue, ActiveLinkColor = Fg };
            open.Click += (s, e) => { if (Logic.IsGame(pathBox.Text)) Process.Start("explorer.exe", pathBox.Text); };
            Controls.Add(open);
            var site = new LinkLabel { Text = "github.com/rastgame007-byte/PEAK-Chaos-Mods", Left = 340, Top = y, Width = 306, Height = 20, LinkColor = Blue, ActiveLinkColor = Fg, TextAlign = ContentAlignment.MiddleRight };
            site.Click += (s, e) => { try { Process.Start("https://github.com/rastgame007-byte/PEAK-Chaos-Mods"); } catch { } };
            Controls.Add(site);
            ClientSize = new Size(660, y + 34);

            Shown += (s, e) => { AutoFind(false); };
            Say("Установщик готов. Моды неофициальные; в игре не тестировались.");
        }

        Label Lbl(string t, int x, int y, int w, int h, Font f, Color c)
        {
            var l = new Label { Text = t, Left = x, Top = y, Width = w, Height = h, ForeColor = c, BackColor = Color.Transparent };
            if (f != null) l.Font = f; return l;
        }
        Button Btn(string t, int x, int y, int w, int h, Color c)
        {
            var b = new Button { Text = t, Left = x, Top = y, Width = w, Height = h, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Fg, Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(c, 0.15f); buttons.Add(b); return b;
        }

        void Say(string s)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => Say(s))); return; }
            log.AppendText(s + "\r\n");
        }

        void AutoFind(bool verbose)
        {
            var g = Logic.FindGames();
            if (g.Count > 0) { pathBox.Text = g[0]; Say("Игра найдена: " + g[0]); }
            else
            {
                if (verbose || pathBox.Text.Length == 0) Say("Автоматически игру найти не удалось — нажми «Обзор…» и выбери папку PEAK.");
                Refresh2();
            }
        }

        void Browse()
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "Выбери папку PEAK (в ней лежит PEAK.exe). В Steam: ПКМ по игре → Управление → Просмотреть локальные файлы.";
                if (Logic.IsGame(pathBox.Text)) d.SelectedPath = pathBox.Text;
                if (d.ShowDialog(this) == DialogResult.OK) pathBox.Text = d.SelectedPath;
            }
        }

        void Refresh2()
        {
            var g = pathBox.Text.Trim().Trim('"');
            bool ok = Logic.IsGame(g);
            if (!ok) { gameState.Text = g.Length == 0 ? "Папка игры не выбрана." : "✘ В этой папке нет PEAK.exe."; gameState.ForeColor = g.Length == 0 ? Dim : Warn; }
            else
            {
                gameState.Text = "✔ Игра найдена. BepInEx: " + (Logic.BepInExInstalled(g) ? "установлен" : "не установлен");
                gameState.ForeColor = Accent;
            }
            for (int i = 0; i < Logic.Mods.Length; i++)
            {
                bool inst = ok && Logic.ModInstalled(g, Logic.Mods[i]);
                states[i].Text = !ok ? "" : inst ? "✔ установлен" : "не установлен";
                states[i].ForeColor = inst ? Accent : Dim;
            }
            foreach (var b in buttons) if (b == btnInstall || b == btnRemove || b == btnRemoveAll) b.Enabled = ok && !busy;
        }

        void SetBusy(bool b)
        {
            busy = b; UseWaitCursor = b; Refresh2();
        }

        string GameDir() { return pathBox.Text.Trim().Trim('"'); }
        List<ModInfo> Selected() { return checks.Where(c => c.Checked).Select(c => (ModInfo)c.Tag).ToList(); }

        bool CheckRunning()
        {
            if (!Logic.GameRunning()) return true;
            return MessageBox.Show(this, "PEAK сейчас запущена. Файлы могут быть заняты, а новые моды заработают только после перезапуска игры.\n\nПродолжить всё равно?",
                "PEAK запущена", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        static Stream Payload(string name) { return Assembly.GetExecutingAssembly().GetManifestResourceStream(name); }

        async Task DoInstall()
        {
            var g = GameDir(); var sel = Selected();
            if (!Logic.IsGame(g)) return;
            if (sel.Count == 0) { MessageBox.Show(this, "Отметь хотя бы один мод.", "Нечего ставить", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (!CheckRunning()) return;
            SetBusy(true);
            try
            {
                int n = await Task.Run(() => Logic.Install(g, sel, Payload, Say));
                Say("Готово: скопировано файлов — " + n + ".");
                Say("Запусти PEAK. Меню модов: PeakFun — F5, PeakMods — F1, PeakJujutsu — F4.");
                if (sel.Any(m => m.Id == "PeakFun") && sel.Any(m => m.Id == "PeakJujutsu")) Say("Внимание: клавиша F4 занята у обоих модов (в PeakFun — «играть за друга»). Если мешает — ставь их по отдельности.");
                MessageBox.Show(this, "Установка завершена!\n\nМоды: " + string.Join(", ", sel.Select(m => m.Id)) + "\nЗапусти PEAK через Steam.\n\nПервый запуск с BepInEx может идти дольше обычного.",
                    "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Say("ОШИБКА: " + ex.Message);
                MessageBox.Show(this, "Не удалось установить:\n" + ex.Message + "\n\nЗакрой игру и попробуй ещё раз. Если папка защищена — запусти установщик от имени администратора.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { SetBusy(false); }
        }

        async Task DoRemove(bool all)
        {
            var g = GameDir(); if (!Logic.IsGame(g)) return;
            var sel = all ? Logic.Mods.ToList() : Selected();
            if (sel.Count == 0) { MessageBox.Show(this, "Отметь моды, которые нужно удалить.", "Нечего удалять", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string q = all
                ? "Будут удалены ВСЕ наши моды, настройки и BepInEx вместе с загрузчиком (winhttp.dll). Чужие моды в BepInEx тоже пропадут.\n\nУдалить всё?"
                : "Удалить: " + string.Join(", ", sel.Select(m => m.Id)) + " (и их настройки)?\nBepInEx и другие моды останутся.";
            if (all) { var other = Logic.OtherPlugins(g); if (other.Length > 0) q += "\n\nЧужие плагины, которые тоже будут удалены: " + string.Join(", ", other.Take(8)) + (other.Length > 8 ? "…" : ""); }
            if (MessageBox.Show(this, q, "Подтверди удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (!CheckRunning()) return;
            SetBusy(true);
            try
            {
                int n = await Task.Run(() => Logic.Uninstall(g, sel, all, Say));
                Say("Удалено объектов: " + n + ".");
                MessageBox.Show(this, all ? "Игра очищена от модов. Если что-то странное — Steam → PEAK → Свойства → Установленные файлы → Проверить целостность." : "Готово, выбранные моды удалены.",
                    "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Say("ОШИБКА: " + ex.Message); MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { SetBusy(false); }
        }
    }
}
