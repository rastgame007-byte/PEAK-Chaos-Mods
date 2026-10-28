// Открыть все скины (косметику) — обратимо.
// Игра спрашивает у каждого предмета внешности (CustomizationOption) свойство IsLocked. Пока тумблер включён, мы отвечаем «нет, не закрыто».
// Ничего не пишется ни в сохранения, ни в достижения Steam: выключил тумблер (или удалил мод) — и всё закрыто ровно так, как было.
// Тот же приём использует мод SimpleUnlocker (там «Non destructive»).
using System;
using System.Reflection;
using HarmonyLib;

namespace PeakFun
{
    internal static class SkinUnlock
    {
        public static bool On;
        public static bool Patched;
        public static string Status = "ещё не включалось";
        static int calls;

        public static void Set(bool on)
        {
            On = on;
            if (on && !Patched) Install();
        }

        static void Install()
        {
            try
            {
                var t = R.FindType("CustomizationOption");
                if (t == null) { Status = "тип CustomizationOption не найден — другая версия игры"; return; }
                const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                MethodInfo m = null;
                for (var c = t; c != null && m == null; c = c.BaseType)
                {
                    var p = c.GetProperty("IsLocked", F);
                    if (p != null) m = p.GetGetMethod(true);
                    if (m == null) m = c.GetMethod("IsLocked", F, null, Type.EmptyTypes, null);
                    if (m == null) m = c.GetMethod("get_IsLocked", F);
                }
                if (m == null || m.ReturnType != typeof(bool)) { Status = "IsLocked не найден — другая версия игры"; return; }
                var h = new Harmony("com.friends.peakfun.skins");
                h.Patch(m, null, new HarmonyMethod(typeof(SkinUnlock).GetMethod("Post", BindingFlags.Static | BindingFlags.NonPublic)));
                Patched = true;
                Status = "готово: перехват " + t.Name + "." + m.Name;
            }
            catch (Exception e) { Status = "ошибка: " + e.GetType().Name + ": " + e.Message; }
        }

        // Postfix: оригинал выполняется как обычно, мы лишь подменяем ответ, пока тумблер включён
        static void Post(ref bool __result)
        {
            if (On) { __result = false; calls++; }
        }

        public static int Calls { get { return calls; } }
    }
}
