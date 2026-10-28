// Мост к игре через рефлексию: мод не ссылается на Assembly-CSharp при сборке,
// поэтому DLL можно собрать без установленной игры и она подхватывает типы PEAK уже в рантайме.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PeakMods
{
    /// <summary>Всё, что мод знает про PEAK.</summary>
    internal static class Game
    {
        static Type charType, pvType, netType, rpcTargetType, phType;
        static MethodInfo rpcMethod;
        static object rpcAll;
        static PropertyInfo rbVelocity;
        public static bool Ready { get { Init(); return charType != null && pvType != null; } }
        static bool inited;

        public static void Init()
        {
            if (inited && charType != null) return;
            inited = true;
            charType = R.FindType("Character");
            pvType = R.FindType("Photon.Pun.PhotonView");
            netType = R.FindType("Photon.Pun.PhotonNetwork");
            rpcTargetType = R.FindType("Photon.Pun.RpcTarget");
            if (pvType != null && rpcTargetType != null)
            {
                foreach (var m in pvType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name != "RPC") continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 3 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == rpcTargetType && ps[2].ParameterType == typeof(object[]))
                    { rpcMethod = m; break; }
                }
                try { rpcAll = Enum.Parse(rpcTargetType, "All"); } catch { }
            }
        }

        // ---------- игроки ----------
        public static Component Me() { Init(); return R.GetStatic(charType, "localCharacter") as Component; }

        public static List<Component> Everyone(bool aliveOnly = false)
        {
            Init();
            var list = new List<Component>();
            // только настоящие игроки (без зомби и Скаутмастера); PlayerHandler.GetAllPlayerCharacters есть в игре
            IEnumerable src = null;
            try
            {
                if (phType == null) phType = R.FindType("PlayerHandler");
                if (phType != null && R.TryInvokeStatic(phType, "GetAllPlayerCharacters", out var pr)) src = pr as IEnumerable;
            }
            catch { }
            if (src == null) src = R.GetStatic(charType, "AllCharacters") as IEnumerable;
            if (src == null) return list;
            foreach (var o in src)
            {
                var c = o as Component;
                if (c == null) continue;
                if (aliveOnly && IsDead(c)) continue;
                list.Add(c);
            }
            return list;
        }

        public static List<Component> Others()
        {
            var me = Me();
            var l = Everyone();
            l.RemoveAll(c => c == me || IsMine(c));
            return l;
        }

        public static string Name(Component c) { return R.Get(c, "characterName") as string ?? "?"; }
        public static bool IsDead(Component c) { return R.Get<bool>(R.Get(c, "data"), "dead"); }
        public static bool IsMine(Component c) { return R.Get<bool>(R.Get(c, "photonView"), "IsMine"); }
        public static bool IsMaster() { Init(); return R.GetStatic(netType, "IsMasterClient") is bool b && b; }

        // ---------- собственный персонаж ----------
        public static object Movement(Component me) { return R.Get(R.Get(me, "refs"), "movement"); }
        public static object Ragdoll(Component me) { return R.Get(R.Get(me, "refs"), "ragdoll"); }

        public static IEnumerable<Rigidbody> Rigs(Component me)
        {
            var parts = R.Get(Ragdoll(me), "partList") as IEnumerable;
            if (parts == null) yield break;
            foreach (var p in parts)
            {
                var rb = R.Get(p, "Rig") as Rigidbody;
                if (rb != null) yield return rb;
            }
        }

        public static void HaltBody(Component me) { R.CallNoArgs(Ragdoll(me), "HaltBodyVelocity"); }

        public static bool InfiniteStam(Component me) { return R.Get<bool>(me, "infiniteStam"); }
        public static bool StatusesLocked(Component me) { return R.Get<bool>(me, "statusesLocked"); }
        public static void ToggleInfiniteStam() { R.CallStatic(charType, "InfiniteStamina"); }
        public static void ToggleLockStatuses() { R.CallStatic(charType, "LockStatuses"); }

        // ---------- сеть ----------
        /// <summary>Вызвать RPC игры у всех клиентов.</summary>
        public static string Rpc(Component c, string method, params object[] args)
        {
            Init();
            var pv = R.Get(c, "photonView");
            if (pv == null || rpcMethod == null || rpcAll == null) return "PhotonView/RPC не найден";
            try { rpcMethod.Invoke(pv, new object[] { method, rpcAll, args }); return null; }
            catch (Exception e) { return (e.InnerException ?? e).Message; }
        }

        // ---------- расширение 1.2 ----------
        public static Type NetType { get { Init(); return netType; } }
        public static bool InRoom { get { Init(); return R.GetStatic(netType, "InRoom") is bool b && b; } }
        public static bool Connected { get { Init(); return R.GetStatic(netType, "IsConnected") is bool b && b; } }

        public static int LocalActor()
        {
            Init();
            var lp = R.GetStatic(netType, "LocalPlayer");
            return R.Get<int>(lp, "ActorNumber", -1);
        }

        public static int ActorOf(Component c)
        {
            var pv = R.Get(c, "photonView");
            if (pv == null) return -1;
            var v = R.Get(pv, "OwnerActorNr");
            if (v is int i) return i;
            return R.Get<int>(R.Get(pv, "Owner"), "ActorNumber", -1);
        }

        public static Component ByActor(int actor)
        {
            foreach (var c in Everyone()) if (ActorOf(c) == actor) return c;
            return null;
        }

        public static object Afflictions(Component c) { return R.Get(R.Get(c, "refs"), "afflictions") ?? R.Get(c, "afflictions"); }

        public static GameObject PhotonSpawn(string method, string prefab, Vector3 pos, Quaternion rot)
        {
            Init();
            if (R.TryInvokeStatic(netType, method, out var res, prefab, pos, rot)) return res as GameObject;
            throw new MissingMethodException("PhotonNetwork." + method);
        }

        public static string PlayerName(int actor)
        {
            var c = ByActor(actor);
            return c != null ? Name(c) : "игрок " + actor;
        }

        // В Unity 6 свойство называется linearVelocity, в старых — velocity.
        public static void SetVelocity(Rigidbody rb, Vector3 v)
        {
            if (rbVelocity == null)
                rbVelocity = typeof(Rigidbody).GetProperty("linearVelocity") ?? typeof(Rigidbody).GetProperty("velocity");
            if (rbVelocity != null) rbVelocity.SetValue(rb, v, null);
        }
    }
}
