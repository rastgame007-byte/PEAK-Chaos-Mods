using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace PeakInstaller
{
    public sealed class ModInfo
    {
        public string Id, Title, Desc, Dll, Cfg, Pattern;
        public ModInfo(string id, string title, string desc, string dll, string cfg, string pattern)
        { Id = id; Title = title; Desc = desc; Dll = dll; Cfg = cfg; Pattern = pattern; }
    }

    public static class Logic
    {
        public const string GameExe = "PEAK.exe";

        public static readonly ModInfo[] Mods =
        {
            new ModInfo("PeakFun", "PeakFun — главный мод",
                "Способности героев, баскетбол, мини-игры, пранки, режим BACKROOMS: PEAK. Нужен у всех друзей.",
                "PeakFun.dll", "com.friends.peakfun*.cfg", "PeakFun*"),
            new ModInfo("PeakMods", "PeakMods — модификаторы забега",
                "Правила легче/сложнее/веселее для всего отряда. Без читов. Нужен у всех друзей.",
                "PeakMods.dll", "com.friends.peakmods*.cfg", "PeakMods*"),
            new ModInfo("PeakJujutsu", "PeakJujutsu — «Магическая битва»",
                "Для игры в одиночку: 6 персонажей, техники, расширения территорий, охота на проклятия.",
                "PeakJujutsu.dll", "com.friends.peakjujutsu*.cfg", "PeakJujutsu*"),
        };

        // ---------- поиск игры ----------
        public static List<string> FindGames(Action<string> log = null)
        {
            var res = new List<string>();
            Action<string> add = p =>
            {
                try
                {
                    if (string.IsNullOrEmpty(p)) return;
                    p = p.Replace('/', '\\').TrimEnd('\\');
                    if (File.Exists(Path.Combine(p, GameExe)) && !res.Contains(p, StringComparer.OrdinalIgnoreCase)) res.Add(p);
                }
                catch { }
            };
            var libs = new List<string>();
            try
            {
                foreach (var root in new[] { "HKEY_CURRENT_USER\\Software\\Valve\\Steam", "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Valve\\Steam", "HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam" })
                {
                    foreach (var val in new[] { "SteamPath", "InstallPath" })
                    {
                        string s = null;
                        try { s = Microsoft.Win32.Registry.GetValue(root, val, null) as string; } catch { }
                        if (!string.IsNullOrEmpty(s)) libs.Add(s.Replace('/', '\\'));
                    }
                }
            }
            catch { }
            foreach (var steam in libs.Distinct(StringComparer.OrdinalIgnoreCase).ToList())
            {
                try
                {
                    var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdf))
                        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                            libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
                }
                catch { }
            }
            foreach (var l in libs.Distinct(StringComparer.OrdinalIgnoreCase)) add(Path.Combine(l, "steamapps", "common", "PEAK"));
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable) continue;
                    var r = d.Name;
                    add(Path.Combine(r, "Program Files (x86)", "Steam", "steamapps", "common", "PEAK"));
                    add(Path.Combine(r, "Program Files", "Steam", "steamapps", "common", "PEAK"));
                    add(Path.Combine(r, "SteamLibrary", "steamapps", "common", "PEAK"));
                    add(Path.Combine(r, "Steam", "steamapps", "common", "PEAK"));
                    add(Path.Combine(r, "Games", "Steam", "steamapps", "common", "PEAK"));
                }
            }
            catch { }
            return res;
        }

        public static bool IsGame(string dir)
        {
            try { return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, GameExe)); } catch { return false; }
        }

        public static bool GameRunning()
        {
            try { return Process.GetProcessesByName("PEAK").Length > 0; } catch { return false; }
        }

        // ---------- состояние ----------
        public static bool BepInExInstalled(string game)
        {
            return File.Exists(Path.Combine(game, "winhttp.dll")) && File.Exists(Path.Combine(game, "BepInEx", "core", "BepInEx.dll"));
        }
        public static bool ModInstalled(string game, ModInfo m)
        {
            return File.Exists(Path.Combine(game, "BepInEx", "plugins", m.Dll));
        }
        public static bool ModInstalled(string game, string dll)
        {
            return File.Exists(Path.Combine(game, "BepInEx", "plugins", dll));
        }

        // ---------- установка ----------
        public static int Install(string game, IEnumerable<ModInfo> mods, Func<string, Stream> payload, Action<string> log)
        {
            int files = 0;
            foreach (var m in mods)
            {
                log("Устанавливаю " + m.Id + "…");
                using (var s = payload("payload." + m.Id + ".zip"))
                {
                    if (s == null) throw new InvalidOperationException("В установщике нет пакета " + m.Id);
                    using (var zip = new ZipArchive(s, ZipArchiveMode.Read))
                    {
                        foreach (var e in zip.Entries)
                        {
                            if (string.IsNullOrEmpty(e.Name)) continue; // папка
                            var rel = e.FullName.Replace('/', Path.DirectorySeparatorChar);
                            if (rel.Contains("..")) continue;
                            var dst = Path.Combine(game, rel);
                            Directory.CreateDirectory(Path.GetDirectoryName(dst));
                            ExtractTo(e, dst);
                            files++;
                        }
                    }
                }
                log("  готово: " + m.Id);
            }
            try
            {
                File.WriteAllText(Path.Combine(game, "BepInEx", "PEAK_Mods_Installer.txt"),
                    "Установлено установщиком PEAK-Mods-Installer " + DateTime.Now + "\r\nМоды: " + string.Join(", ", mods.Select(x => x.Id)) + "\r\n");
            }
            catch { }
            return files;
        }

        static void ExtractTo(ZipArchiveEntry e, string dst)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(dst)) { try { File.SetAttributes(dst, FileAttributes.Normal); } catch { } }
                    using (var inp = e.Open())
                    using (var outp = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None))
                        inp.CopyTo(outp);
                    return;
                }
                catch (IOException) when (attempt < 2) { System.Threading.Thread.Sleep(400); }
            }
        }

        // ---------- удаление ----------
        public static int Uninstall(string game, IEnumerable<ModInfo> mods, bool removeBepInEx, Action<string> log)
        {
            int n = 0;
            var bep = Path.Combine(game, "BepInEx");
            foreach (var m in mods)
            {
                log("Удаляю " + m.Id + "…");
                n += Del(Path.Combine(bep, "plugins", m.Dll));
                try
                {
                    var cd = Path.Combine(bep, "config");
                    if (Directory.Exists(cd)) foreach (var f in Directory.GetFiles(cd, m.Cfg)) n += Del(f);
                }
                catch { }
                try
                {
                    if (Directory.Exists(bep))
                        foreach (var f in Directory.GetFiles(bep, m.Pattern, SearchOption.TopDirectoryOnly)) n += Del(f);
                }
                catch { }
            }
            if (removeBepInEx)
            {
                log("Удаляю BepInEx и загрузчик…");
                try { if (Directory.Exists(bep)) { Directory.Delete(bep, true); n++; } } catch (Exception ex) { log("  не удалось удалить папку BepInEx: " + ex.Message); }
                n += Del(Path.Combine(game, "winhttp.dll"));
                n += Del(Path.Combine(game, "doorstop_config.ini"));
                n += Del(Path.Combine(game, ".doorstop_version"));
                n += Del(Path.Combine(game, "changelog.txt"));
            }
            return n;
        }

        static int Del(string f)
        {
            try { if (File.Exists(f)) { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); return 1; } } catch { }
            return 0;
        }

        // остальные плагины в BepInEx\plugins (чужие моды)
        public static string[] OtherPlugins(string game)
        {
            try
            {
                var dir = Path.Combine(game, "BepInEx", "plugins");
                if (!Directory.Exists(dir)) return new string[0];
                var ours = Mods.Select(m => m.Dll).ToArray();
                return Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories).Select(Path.GetFileName)
                    .Where(x => !ours.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
            }
            catch { return new string[0]; }
        }
    }
}
