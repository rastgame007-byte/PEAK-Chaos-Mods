// Новое в 1.3: невидимка, самовозрождение, откат-«резинка», бот-спутник, каталог предметов с названиями и иконками.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        // ============================== НЕВИДИМКА ==============================
        // Игра не умеет прятать игрока «по сети», поэтому прячем у каждого, у кого стоит PeakFun: гасим рендереры персонажа.
        class InvState { public readonly List<Renderer> rs = new List<Renderer>(); public readonly List<Light> ls = new List<Light>(); }
        readonly Dictionary<int, InvState> remoteInv = new Dictionary<int, InvState>();
        float nextInvSend, nextInvScan;

        void SetInvisible(bool on)
        {
            invisible = on;
            if (Game.InRoom) Net.Send("inv", on ? "1" : "0", -1);
            nextInvSend = Time.time + 4f;
            Ui.Toast(on ? "Ты невидим для друзей с PeakFun" : "Ты снова виден", on ? Ui.Accent2 : Ui.Muted);
        }

        void SetRemoteInv(int actor, bool on)
        {
            if (on) { if (!remoteInv.ContainsKey(actor)) remoteInv[actor] = new InvState(); }
            else RestoreInv(actor);
            nextInvScan = 0f;
        }

        void RestoreInv(int actor)
        {
            if (!remoteInv.TryGetValue(actor, out var st)) return;
            foreach (var r in st.rs) if (r != null) r.enabled = true;
            foreach (var l in st.ls) if (l != null) l.enabled = true;
            remoteInv.Remove(actor);
        }

        void TickInvisible()
        {
            if (invisible && Time.time >= nextInvSend && Game.InRoom)
            {   // повторяем: чтобы увидели вошедшие позже и после смены персонажа
                Net.Send("inv", "1", -1);
                nextInvSend = Time.time + 4f;
            }
            if (remoteInv.Count == 0 || Time.time < nextInvScan) return;
            nextInvScan = Time.time + 0.4f;
            List<int> gone = null;
            foreach (var kv in remoteInv)
            {
                var c = Game.ByActor(kv.Key);
                if (c == null) { (gone ?? (gone = new List<int>())).Add(kv.Key); continue; }
                var st = kv.Value;
                foreach (var r in c.GetComponentsInChildren<Renderer>(true))
                    if (r != null && r.enabled) { r.enabled = false; st.rs.Add(r); }
                foreach (var l in c.GetComponentsInChildren<Light>(true))
                    if (l != null && l.enabled) { l.enabled = false; st.ls.Add(l); }
            }
            if (gone != null) foreach (var a in gone) remoteInv.Remove(a);
        }

        void RestoreAllInv()
        {
            foreach (var a in new List<int>(remoteInv.Keys)) RestoreInv(a);
        }

        // ============================== ВОЗРОЖДЕНИЕ ==============================
        bool autoRespawn; float deadSince = -1f, nextRespawnTry; Vector3 lastAlivePos; bool hasAlivePos;
        float respawnCheckAt;

        bool IsDeadOrGhost(Component c)
        {
            return Game.IsDead(c) || R.Get<bool>(c, "IsGhost");
        }

        void RespawnMe(bool quiet = false)
        {
            var me = Game.Me();
            if (me == null) return;
            Vector3 pos = me.transform.position + Vector3.up * 1.5f;
            Component best = null; float bd = float.MaxValue;
            foreach (var c in Game.Everyone(true))
            {
                if (c == me || IsDeadOrGhost(c)) continue;
                float d = Vector3.Distance(c.transform.position, me.transform.position);
                if (d < bd) { bd = d; best = c; }
            }
            if (best != null) pos = best.transform.position + Vector3.up * 2f;
            else if (hasAlivePos) pos = lastAlivePos + Vector3.up * 1.5f;
            // сигнатура в игре: RPCA_ReviveAtPosition(Vector3 position, bool applyStatus, int ???)
            Rpc(me, "RPCA_ReviveAtPosition", pos, false, -1);
            respawnCheckAt = Time.time + 1.6f;
            if (!quiet) Ui.Toast("Возрождение…", Ui.Good);
        }

        void TickRespawn(Component me)
        {
            bool dead = IsDeadOrGhost(me);
            if (!dead) { lastAlivePos = me.transform.position; hasAlivePos = true; deadSince = -1f; }
            else if (deadSince < 0f) deadSince = Time.time;

            if (respawnCheckAt > 0f && Time.time >= respawnCheckAt)
            {
                respawnCheckAt = 0f;
                if (dead && R.TryInvoke(me, "Revive", out var _)) { /* запасной способ: встроенный метод */ }
            }
            if (autoRespawn && dead && Time.time - deadSince > 3f && Time.time >= nextRespawnTry)
            {
                nextRespawnTry = Time.time + 6f;
                RespawnMe(true);
                Ui.Toast("Авто-возрождение", Ui.Good);
            }
        }

        // ============================== «РЕЗИНКА» (откат позиции) ==============================
        readonly List<KeyValuePair<float, Vector3>> posHist = new List<KeyValuePair<float, Vector3>>();
        float nextHist, rubberUntil, nextRubber;

        void StartRubber(float dur)
        {
            rubberUntil = Time.unscaledTime + dur;
            nextRubber = Time.unscaledTime + UnityEngine.Random.Range(0.4f, 1.0f);
        }

        void TickRubber(Component me)
        {
            float now = Time.unscaledTime;
            if (now >= nextHist)
            {
                nextHist = now + 0.1f;
                if (!IsDeadOrGhost(me)) posHist.Add(new KeyValuePair<float, Vector3>(now, me.transform.position));
                while (posHist.Count > 0 && now - posHist[0].Key > 4f) posHist.RemoveAt(0);
            }
            if (now < rubberUntil && now >= nextRubber && posHist.Count > 4 && !flying)
            {
                nextRubber = now + UnityEngine.Random.Range(0.9f, 2.3f);
                float back = UnityEngine.Random.Range(0.9f, 2.0f);
                Vector3 target = posHist[0].Value;
                for (int i = posHist.Count - 1; i >= 0; i--) if (now - posHist[i].Key >= back) { target = posHist[i].Value; break; }
                if (Vector3.Distance(target, me.transform.position) > 1.2f)
                {
                    Rpc(me, "WarpPlayerRPC", target + Vector3.up * 0.3f, false);
                    Game.HaltBody(me);
                }
            }
        }

        // ============================== КАТАЛОГ ПРЕДМЕТОВ ==============================
        class ItemInfo { public string prefab, title, key; public Texture tex; public Rect uv = new Rect(0, 0, 1, 1); }
        readonly List<ItemInfo> items = new List<ItemInfo>();
        readonly Dictionary<string, ItemInfo> itemByPrefab = new Dictionary<string, ItemInfo>();
        float itemsLoadedAt;

        static string Pretty(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.Replace('_', ' ').Replace('-', ' ').Trim();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (i > 0 && char.IsUpper(ch) && char.IsLower(s[i - 1])) sb.Append(' ');
                sb.Append(ch);
            }
            s = sb.ToString();
            bool allUp = true; foreach (char ch in s) if (char.IsLetter(ch) && !char.IsUpper(ch)) { allUp = false; break; }
            var ti = CultureInfo.CurrentCulture.TextInfo;
            return allUp ? ti.ToTitleCase(s.ToLower()) : s;
        }

        void AddItem(object v, HashSet<string> seen)
        {
            var o = v as UnityEngine.Object;
            if (o == null || string.IsNullOrEmpty(o.name) || !seen.Add(o.name)) return;
            var info = new ItemInfo { prefab = o.name };
            string t = null;
            try
            {
                if (R.TryInvoke(v, "GetName", out var res) && res is string s1 && !string.IsNullOrWhiteSpace(s1)) t = s1;
            }
            catch { }
            var ui = R.Get(v, "UIData");
            if (string.IsNullOrWhiteSpace(t)) t = R.Get(ui, "itemName") as string;
            info.title = Pretty(string.IsNullOrWhiteSpace(t) ? o.name : t);
            if (string.IsNullOrWhiteSpace(info.title)) info.title = o.name;
            info.key = (info.title + " " + o.name).ToLowerInvariant();
            try
            {
                object ic = null;
                if (ui != null && !(R.TryInvoke(ui, "GetIcon", out ic) && ic != null)) ic = R.Get(ui, "icon");
                var sp = ic as Sprite;
                if (sp != null && sp.texture != null)
                {
                    var tr = sp.textureRect; var tx = sp.texture;
                    info.tex = tx; info.uv = new Rect(tr.x / tx.width, tr.y / tx.height, tr.width / tx.width, tr.height / tx.height);
                }
                else if (ic is Texture) info.tex = (Texture)ic;
            }
            catch { }
            items.Add(info);
        }

        void LoadItems()
        {
            itemNames.Clear(); items.Clear(); itemByPrefab.Clear();
            itemsLoaded = true; itemsLoadedAt = Time.realtimeSinceStartup;
            try
            {
                var seen = new HashSet<string>();
                var dbT = R.FindType("ItemDatabase");
                if (dbT != null)
                {
                    foreach (var db in Resources.FindObjectsOfTypeAll(dbT))
                    {
                        var lk = R.Get(db, "itemLookup") as IDictionary;
                        if (lk != null) foreach (var v in lk.Values) AddItem(v, seen);
                        var objs = R.Get(db, "Objects") as IEnumerable;
                        if (objs != null) foreach (var v in objs) AddItem(v, seen);
                    }
                }
                if (items.Count == 0)
                    foreach (var go in Resources.LoadAll("0_Items", typeof(GameObject)))
                    {
                        if (!seen.Add(go.name)) continue;
                        items.Add(new ItemInfo { prefab = go.name, title = Pretty(go.name), key = (Pretty(go.name) + " " + go.name).ToLowerInvariant() });
                    }
                items.Sort((a, b) => string.Compare(a.title, b.title, StringComparison.CurrentCultureIgnoreCase));
                int icons = 0;
                foreach (var it in items) { itemNames.Add(it.prefab); itemByPrefab[it.prefab] = it; if (it.tex != null) icons++; }
                itemStatus = items.Count > 0 ? "Предметов: " + items.Count + ", иконок: " + icons : "Список предметов не найден — введи имя вручную.";
            }
            catch (Exception e) { itemStatus = "Ошибка загрузки списка: " + e.Message; SafeLog("LoadItems", e); }
        }

        string ItemTitle(string prefab) { return itemByPrefab.TryGetValue(prefab, out var i) ? i.title : prefab; }

        /// <summary>Находит предмет по тому, что ввёл человек: название по-русски/по-английски или имя префаба.</summary>
        string ResolveItem(string q)
        {
            q = (q ?? "").Trim();
            if (q.Length == 0) return null;
            string ql = q.ToLowerInvariant();
            foreach (var it in items) if (it.prefab.ToLowerInvariant() == ql || it.title.ToLowerInvariant() == ql) return it.prefab;
            foreach (var it in items) if (it.key.Contains(ql)) return it.prefab;
            return q;   // вдруг это точное имя префаба, которого нет в списке
        }
    }
}
