// Автоприманки: по одной в каждом биоме (сегменте карты PEAK) — внизу, рядом с началом биома, на ровной земле, а не на подъёме в гору.
// Сегменты берутся из MapHandler.segments[i].reconnectSpawnPos (точка, куда игроков переносит в начало биома). Всё через рефлексию.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    public partial class PeakFunPlugin
    {
        string autoInfo = "ещё не искал"; int autoSeg = -99; float autoSegT, autoNext; bool autoPlaced; Component autoMap; Vector3 autoFallback; bool autoHaveFallback;

        static Component FindMapHandler()
        {
            var t = R.FindType("MapHandler"); if (t == null) return null;
            try { return UnityEngine.Object.FindObjectOfType(t) as Component; } catch { return null; }
        }

        void TickAutoCases()
        {
            if (!cfgAutoCases.Value || Time.time < autoNext) return; autoNext = Time.time + 1.5f;
            if (brInside || brBusy) return; var me = Game.Me(); if (me == null || Game.IsDead(me)) return;
            if (autoMap == null) autoMap = FindMapHandler(); if (autoMap == null) { autoInfo = "MapHandler не найден (карта ещё не загружена)"; return; }
            int cur = R.Get<int>(autoMap, "currentSegment", -1); if (cur < 0) { autoInfo = "currentSegment не прочитан"; return; }
            if (!autoHaveFallback) { autoFallback = me.transform.position; autoHaveFallback = true; }
            if (cur != autoSeg) { autoSeg = cur; autoSegT = Time.time; autoPlaced = false; AutoRemoveOld(); }
            if (autoPlaced) return; if (Time.time - autoSegT < 4f) return;
            if (cur >= 5) { autoInfo = "вершина — приманку не ставлю"; autoPlaced = true; return; }
            AutoPlace(me, cur, false);
        }

        void AutoRemoveOld()
        {
            for (int i = brCrates.Count - 1; i >= 0; i--)
            {
                var c = brCrates[i]; if (c.id > -100 || c.id <= -200) continue;
                if (c.go != null) UnityEngine.Object.Destroy(c.go); if (c.glow != null) UnityEngine.Object.Destroy(c.glow); brCrates.RemoveAt(i);
            }
        }

        void AutoCaseNow()
        {
            var me = Game.Me(); if (me == null) { Ui.Toast("Нет персонажа", Ui.Bad); return; }
            if (autoMap == null) autoMap = FindMapHandler(); int cur = autoMap != null ? R.Get<int>(autoMap, "currentSegment", 0) : 0; if (cur < 0) cur = 0;
            AutoRemoveOld(); bool ok = AutoPlace(me, Mathf.Min(cur, 4), true); Ui.Toast(ok ? "Автоприманка поставлена, см. «Авто:» в этой вкладке" : "Подходящее место не найдено: " + autoInfo, ok ? Ui.Good : Ui.Bad);
        }

        bool AutoPlace(Component me, int seg, bool force)
        {
            Vector3 anchor = autoFallback; string src = "место старта";
            try
            {
                var segs = R.Get(autoMap, "segments") as Array;
                if (segs != null && seg < segs.Length)
                {
                    var sg = segs.GetValue(seg); var tr = R.Get(sg, "reconnectSpawnPos") as Transform; if (tr != null) { anchor = tr.position; src = "начало биома #" + seg; }
                }
            }
            catch (Exception e) { SafeLog("auto-seg", e); }
            if (seg > 0 && src.StartsWith("место")) { anchor = me.transform.position; src = "твоя позиция"; }
            if (force && seg == 0 && src.StartsWith("место")) anchor = me.transform.position;
            var rnd = new System.Random(seg * 7919 + DateTime.UtcNow.DayOfYear * 131 + DateTime.UtcNow.Year);
            int mask = 0; try { mask = TerrainMask(); } catch { } if (mask == 0) mask = ~0;
            int tries = 0, rej = 0; Vector3 pick = Vector3.zero; bool found = false;
            for (int i = 0; i < 90 && !found; i++)
            {
                tries++; float ang = (float)(rnd.NextDouble() * Math.PI * 2.0), rad = 9f + (float)rnd.NextDouble() * (i < 45 ? 34f : 70f);
                Vector3 o = anchor + new Vector3(Mathf.Cos(ang) * rad, 40f, Mathf.Sin(ang) * rad); RaycastHit h;
                if (!Physics.Raycast(o, Vector3.down, out h, 120f, mask, QueryTriggerInteraction.Ignore)) { rej++; continue; }
                if (h.collider != null && (h.collider.name.StartsWith("PF_") || h.collider.attachedRigidbody != null)) { rej++; continue; }
                if (h.normal.y < 0.93f) { rej++; continue; }                         // не склон
                if (Mathf.Abs(h.point.y - anchor.y) > 9f) { rej++; continue; }       // не уходим вверх по горе
                if (Physics.OverlapSphere(h.point + Vector3.up * 1.2f, 0.9f, ~0, QueryTriggerInteraction.Ignore).Length > 0) { rej++; continue; }   // место свободно
                if (Physics.OverlapSphere(h.point + Vector3.up * 0.2f, 0.55f, ~0, QueryTriggerInteraction.Ignore).Length > 1) { rej++; continue; }
                pick = h.point; found = true;
            }
            if (!found) { autoInfo = "биом #" + seg + ": не нашёл ровное место за " + tries + " попыток (" + src + ")"; if (!force) autoPlaced = true; return false; }
            var r2 = new System.Random(seg * 31 + 5); int model = r2.Next(4), trap = PickTrap(r2);
            MakeCrate(-100 - seg, pick, cfgBrHeight.Value, brSize, brEntity ? 1 : 0, model, trap);
            autoInfo = "биом #" + seg + ": «" + BaitNames[model] + "» в " + Vector3.Distance(pick, anchor).ToString("0") + " м от точки (" + src + "), ловушка «" + TrapNames[trap] + "», отклонено мест: " + rej;
            BrDiag("автоприманка: " + autoInfo); autoPlaced = true; return true;
        }
    }
}
