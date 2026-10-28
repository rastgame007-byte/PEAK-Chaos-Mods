// Блокировка управления персонажем и камерой, пока открыто меню мода.
// Игра сама считает «окно блокирует ввод» (GUIManager.windowBlockingInput) в UpdateWindowStatus — вешаем туда Harmony-постфикс,
// как это делают чат-моды (PeakTextChat). Плюс страховка: выставляем флаг ещё и из Update/LateUpdate.
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace PeakFun
{
    internal static class InputBlock
    {
        public static bool Want;
        public static bool Patched;
        static Type guiT;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Install()
        {
            try
            {
                guiT = R.FindType("GUIManager");
                if (guiT == null) return "GUIManager не найден";
                MethodInfo m = null;
                foreach (var name in new[] { "UpdateWindowStatus", "UpdateWindows" })
                {
                    m = guiT.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    if (m != null) break;
                }
                if (m == null) return "UpdateWindowStatus не найден (работает запасной способ)";
                var h = new Harmony("com.friends.peakfun.input");
                var post = typeof(InputBlock).GetMethod("Post", BindingFlags.Static | BindingFlags.NonPublic);
                h.Patch(m, null, new HarmonyMethod(post));
                Patched = true;
                return "ok";
            }
            catch (Exception e) { return e.GetType().Name + ": " + e.Message; }
        }

        static void Post() { if (Want) Apply(); }

        public static void Apply()
        {
            try
            {
                if (guiT == null) guiT = R.FindType("GUIManager");
                var inst = R.GetStatic(guiT, "instance");
                if (inst != null) R.Set(inst, "windowBlockingInput", true);
            }
            catch { }
        }
    }
}
