// 1.31.1: страховка от ClientTimeout. Генерация уровня (текстуры, меши) может подвесить игру на несколько секунд, а Photon по умолчанию
// отключает клиента после ~10 с без ответа. На время Backrooms увеличиваем тайм-аут клиента (через рефлексию), потом возвращаем.
using System;
using System.Reflection;

namespace PeakBR
{
    internal static class NetGuard
    {
        static int old = -1; const int Ms = 90000;
        static PropertyInfo Prop(out object peer)
        {
            peer = null; var pn = R.FindType("Photon.Pun.PhotonNetwork"); if (pn == null) return null;
            var f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            var cp = pn.GetProperty("NetworkingClient", f); object cl = cp != null ? cp.GetValue(null, null) : null; if (cl == null) return null;
            var lp = cl.GetType().GetProperty("LoadBalancingPeer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); peer = lp != null ? lp.GetValue(cl, null) : null; if (peer == null) return null;
            return peer.GetType().GetProperty("DisconnectTimeout", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }
        public static void On()
        {
            try
            {
                object peer; var p = Prop(out peer); if (p == null) { Plugin.Log("NetGuard: DisconnectTimeout не найден"); return; }
                int cur = Convert.ToInt32(p.GetValue(peer, null)); if (old < 0 && cur < Ms) old = cur; if (cur < Ms) { p.SetValue(peer, Ms, null); Plugin.Log("NetGuard: тайм-аут Photon " + cur + " → " + Ms + " мс"); }
            }
            catch (Exception e) { Plugin.Warn("NetGuard", e); }
        }
        public static void Off()
        {
            try { if (old < 0) return; object peer; var p = Prop(out peer); if (p != null) p.SetValue(peer, old, null); old = -1; } catch { old = -1; }
        }
    }
}
