// Свой канал связи между игроками, у которых стоит PeakFun (Photon RaiseEvent).
// У друзей без мода ничего не происходит — они просто не получают эти события.
using System;
using System.Reflection;
using UnityEngine;

namespace PeakBR
{
    internal static class Net
    {
        public const byte EventCode = 113;
        const string Magic = "BR1";

        static bool subscribed;
        static float nextTry;
        static object client;
        static Type raiseOptType, sendOptType, receiverType;
        static object sendReliable, receiversOthers;
        public static string Status = "ещё не подключено";

        /// <summary>Вызывается из команды (cmd, arg, отправитель).</summary>
        public static Action<string, string, int> OnCommand;

        public static void Tick()
        {
            if (subscribed || Time.realtimeSinceStartup < nextTry) return;
            nextTry = Time.realtimeSinceStartup + 3f;
            try { Subscribe(); }
            catch (Exception e) { Status = "ошибка: " + (e.InnerException ?? e).Message; }
        }

        static void Subscribe()
        {
            var netType = Game.NetType;
            if (netType == null) { Status = "Photon не найден"; return; }
            client = R.GetStatic(netType, "NetworkingClient");
            if (client == null) { Status = "NetworkingClient не найден"; return; }

            raiseOptType = R.FindType("Photon.Realtime.RaiseEventOptions");
            receiverType = R.FindType("Photon.Realtime.ReceiverGroup");
            sendOptType = R.FindType("ExitGames.Client.Photon.SendOptions");
            if (raiseOptType == null || sendOptType == null || receiverType == null) { Status = "типы Photon.Realtime не найдены"; return; }
            sendReliable = R.GetStatic(sendOptType, "SendReliable");
            receiversOthers = Enum.Parse(receiverType, "Others");

            var ev = client.GetType().GetEvent("EventReceived", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (ev == null) { Status = "событие EventReceived не найдено"; return; }
            var evArg = ev.EventHandlerType.GetGenericArguments()[0];
            var mi = typeof(Net).GetMethod("Handler", BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(evArg);
            ev.AddEventHandler(client, Delegate.CreateDelegate(ev.EventHandlerType, mi));
            subscribed = true;
            Status = "канал PeakMods активен";
        }

        static void Handler<T>(T data)
        {
            try
            {
                if (!(R.Get(data, "Code") is byte code) || code != EventCode) return;
                int sender = R.Get<int>(data, "Sender", -1);
                if (sender == Game.LocalActor()) return;
                var s = R.Get(data, "CustomData") as string;
                if (s == null) return;
                var p = s.Split('|');
                if (p.Length < 4 || p[0] != Magic) return;
                int target = -1; int.TryParse(p[3], out target);
                if (target != -1 && target != Game.LocalActor()) return;
                OnCommand?.Invoke(p[1], p[2], sender);
            }
            catch (Exception e) { Debug.LogWarning("[PeakMods] приём события: " + e.Message); }
        }

        /// <summary>Послать команду. target = -1 — всем остальным.</summary>
        /// <summary>Режим «тест в одиночку»: команды dom/afx приходят и самому себе, как будто их прислал друг.</summary>
        public static bool Echo;

        public static bool Send(string cmd, string arg, int target = -1)
        {
            try
            {
                if (Echo && target == -1)
                { try { OnCommand?.Invoke(cmd, arg, Game.LocalActor()); } catch (Exception ee) { Debug.LogWarning("[PeakMods] эхо: " + ee.Message); } }
                Tick();
                if (!subscribed || !Game.InRoom) return false;
                var opts = Activator.CreateInstance(raiseOptType);
                R.Set(opts, "Receivers", receiversOthers);
                string payload = Magic + "|" + cmd + "|" + arg + "|" + target;
                R.TryInvokeStatic(Game.NetType, "RaiseEvent", out var _, EventCode, payload, opts, sendReliable);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[PeakMods] отправка: " + (e.InnerException ?? e).Message); return false; }
        }
    }
}
