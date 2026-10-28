// Прокачка (уровень/ранг), комбо и режимы «Охота» / «Тренировка».
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakJJK
{
    internal static class Prog
    {
        public static int Level = 1; public static float Xp; public static int TotalKills, BestWave;
        public const int MaxLevel = 30;
        public static float Need(int lv) { return 80f + lv * lv * 14f; }
        public static float MaxCE { get { return 100f + 10f * (Level - 1); } }
        public static float Regen { get { return 7f + 0.5f * Level; } }
        public static float DmgMul { get { return 1f + 0.05f * (Level - 1); } }
        public static string Grade
        {
            get
            {
                if (Level < 5) return "Маг 4 ранга"; if (Level < 10) return "Маг 3 ранга"; if (Level < 16) return "Маг 2 ранга"; if (Level < 23) return "Маг 1 ранга"; return "Особый ранг";
            }
        }
        public static void Load() { Level = Mathf.Clamp(Plugin.CfgLevel.Value, 1, MaxLevel); Xp = Plugin.CfgXpSaved.Value; TotalKills = Plugin.CfgKills.Value; BestWave = Plugin.CfgBestWave.Value; }
        public static void Save() { Plugin.CfgLevel.Value = Level; Plugin.CfgXpSaved.Value = Xp; Plugin.CfgKills.Value = TotalKills; Plugin.CfgBestWave.Value = BestWave; }
        public static void Reset() { Level = 1; Xp = 0; TotalKills = 0; BestWave = 0; Save(); }
        public static void SetLevel(int l) { Level = Mathf.Clamp(l, 1, MaxLevel); Xp = 0; Save(); }

        public static void AddXp(float amount, EDef d)
        {
            if (amount <= 0f) return; TotalKills++; amount *= Plugin.CfgXp; Xp += amount; Fx.Popup_(Pl.Pos + Vector3.up * 2.4f, "+" + Mathf.RoundToInt(amount) + " ОП", new Color(0.6f, 0.9f, 1f), 0.9f);
            while (Level < MaxLevel && Xp >= Need(Level)) { Xp -= Need(Level); Level++; LevelUp(); }
            if (Level >= MaxLevel) Xp = Mathf.Min(Xp, Need(MaxLevel - 1));
            Save();
        }
        public static void AddBonus(float a) { Xp += a * Plugin.CfgXp; while (Level < MaxLevel && Xp >= Need(Level)) { Xp -= Need(Level); Level++; LevelUp(); } Save(); }
        static void LevelUp()
        {
            Ui.Toast("УРОВЕНЬ " + Level + " — " + Grade); Hud.Banner("УРОВЕНЬ " + Level, Grade); Sfx.Ding(0.6f, 1.2f); Fx.Ring(Pl.Pos, Vector3.up, new Color(0.5f, 0.9f, 1f), 6f, 0.8f, 0.4f); Fx.Burst(Pl.Pos, 30, 5f, 0.3f, new Color(0.6f, 0.9f, 1f), 1f, true, 3f);
            Skills.CE = Skills.MaxCE;
            foreach (var c in Skills.Chars) if (c.unlock == Level && !Plugin.CfgUnlockAll.Value) Ui.Toast("Открыт персонаж: " + c.name);
        }
    }

    internal static class Combo
    {
        public static int Count; static float last;
        public static float Mul { get { return 1f + Mathf.Min(Count, 60) * 0.005f; } }
        public static void Hit() { if (Time.time - last > 3.5f) Count = 0; Count++; last = Time.time; }
        public static float Left { get { return Mathf.Max(0f, 3.5f - (Time.time - last)); } }
        public static void Tick() { if (Count > 0 && Time.time - last > 3.5f) Count = 0; }
    }

    internal static class Hunt
    {
        public static int Mode; // 0 выкл, 1 охота, 2 тренировка
        public static int Wave, BossKills, Kills; public static float NextAt; public static int State; // 0 пауза, 1 бой
        static readonly Queue<string> queue = new Queue<string>(); static float nextSpawn; static bool wasDead; public static int Total;

        public static void Start(int mode)
        {
            Stop(); Mode = mode; Wave = 0; State = 0; NextAt = Time.time + 4f; BossKills = 0; Kills = 0; queue.Clear();
            if (mode == 2) { SpawnDummies(); Ui.Toast("Тренировка: ЦЭ бесконечна, манекены не убивают"); } else Ui.Toast("Охота началась! Волна 1 через 4 секунды");
            Hud.Banner(mode == 1 ? "ОХОТА НА ПРОКЛЯТИЙ" : "ТРЕНИРОВОЧНЫЙ ПОЛИГОН", mode == 1 ? "Выживай и расти в ранге" : "Отрабатывай техники");
        }
        public static void Stop() { if (Mode == 0) return; Mode = 0; queue.Clear(); World.ClearAll(); Prog.Save(); Ui.Toast("Режим остановлен"); }

        static void SpawnDummies()
        {
            Vector3 f = Pl.Fwd; for (int i = 0; i < 3; i++) { Vector3 p = Pl.Pos + Quaternion.Euler(0, (i - 1) * 25f, 0) * f * (10f + i * 2f); Enemies.Spawn("wisp", p, 1f, true); }
        }
        public static void OnKill(Enemy e) { Kills++; Skills.CE = Mathf.Min(Skills.MaxCE, Skills.CE + (e.d.boss ? 40f : 4f)); if (Mode == 2 && e.dummy) World.After(2f, () => { if (Mode == 2) { Vector3 p = Pl.Pos + Quaternion.Euler(0, UnityEngine.Random.Range(-40f, 40f), 0) * Pl.Fwd * UnityEngine.Random.Range(9f, 14f); Enemies.Spawn("wisp", p, 1f, true); } }); }

        static Vector3 SpawnPoint()
        {
            Vector3 pp = Pl.Pos; for (int i = 0; i < 12; i++)
            {
                float a = UnityEngine.Random.value * 6.283f, r = UnityEngine.Random.Range(24f, 38f); Vector3 p = pp + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r; bool f; float gy = World.Ground(p, out f); if (!f) continue; if (Mathf.Abs(gy - pp.y) > 25f) continue; return new Vector3(p.x, gy, p.z);
            }
            return pp + Pl.Fwd * 25f;
        }

        static void Compose(int w)
        {
            queue.Clear(); bool bossWave = w % 5 == 0;
            if (bossWave) { int idx = (w / 5 - 1); string b = Enemies.BossOrder[idx % 4]; if (b == "sukuna" && Skills.Cur != null && Skills.Cur.id == "sukuna") b = "gojo"; queue.Enqueue("boss:" + b); for (int i = 0; i < 3 + Mathf.Min(w / 5, 5); i++) queue.Enqueue("wisp"); Total = queue.Count; return; }
            int n = Mathf.Min(4 + w * 2, 30); var bag = new List<string>();
            for (int i = 0; i < 6; i++) bag.Add("wisp"); if (w >= 2) for (int i = 0; i < 3 + w / 2; i++) bag.Add("hound"); if (w >= 3) for (int i = 0; i < 2 + w / 3; i++) bag.Add("flyhead"); if (w >= 4) for (int i = 0; i < 1 + w / 4; i++) bag.Add("brute");
            for (int i = 0; i < n; i++) queue.Enqueue(bag[UnityEngine.Random.Range(0, bag.Count)]); Total = queue.Count;
        }

        public static int Remaining { get { return queue.Count + Enemies.Alive; } }

        public static void Tick()
        {
            if (Mode == 0) return;
            if (!Pl.Alive) { if (!wasDead) { wasDead = true; World.ClearAll(); queue.Clear(); State = 0; NextAt = Time.time + 8f; if (Wave > 0) Wave--; Ui.Toast("Ты пал… Волна начнётся заново"); } return; }
            if (wasDead) { wasDead = false; NextAt = Time.time + 6f; if (Mode == 2) SpawnDummies(); }
            if (Mode == 2) return;
            if (State == 0)
            {
                if (Time.time >= NextAt) { Wave++; Prog.BestWave = Mathf.Max(Prog.BestWave, Wave); Compose(Wave); State = 1; nextSpawn = Time.time + 1f; Hud.Banner("ВОЛНА " + Wave, Wave % 5 == 0 ? "БОСС!" : Total + " проклятий"); Sfx.Roar(null, 0.4f); }
            }
            else
            {
                if (queue.Count > 0 && Time.time >= nextSpawn && Enemies.Alive < Plugin.CfgMaxAlive.Value)
                {
                    nextSpawn = Time.time + 0.45f; string id = queue.Dequeue(); float loop = Wave / 5 > 0 ? (Wave / 5 - 1) / 4 : 0; float hpMul = 1f + (Wave - 1) * 0.06f;
                    if (id.StartsWith("boss:")) { id = id.Substring(5); hpMul = 1f + loop * 0.4f; var e = Enemies.Spawn(id, SpawnPoint() + Pl.Fwd * 0f, hpMul); if (e != null) e.dmgMul = 1f + loop * 0.2f; }
                    else { var e = Enemies.Spawn(id, SpawnPoint(), hpMul); if (e != null) e.dmgMul = 1f + (Wave - 1) * 0.02f; }
                }
                if (queue.Count == 0 && Enemies.Alive == 0) { State = 0; NextAt = Time.time + 7f; float bonus = 30f + Wave * 12f; Ui.Toast("Волна " + Wave + " зачищена! +" + Mathf.RoundToInt(bonus) + " ОП"); Prog.AddBonus(bonus); Skills.CE = Mathf.Min(Skills.MaxCE, Skills.CE + 30f); Pl.Heal(0.3f, 2f); }
            }
        }
    }
}
