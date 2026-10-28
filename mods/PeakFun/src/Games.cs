// Мини-игры 1.9: «Горячая картошка», «Король горы», «Прятки». Судья — ты (у тебя стоит мод), друзьям мод не нужен:
// всё, что нужно по правилам, делается через игровую физику (подножка, отброс), а подсказки приходят тем, у кого есть PeakFun.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PeakFun
{
    internal enum GameKind { None, Potato, Koth, Hide }

    public partial class PeakFunPlugin
    {
        GameKind game = GameKind.None; float gameEnd, gameStart; bool gameIncludeMe = true;
        readonly Dictionary<Component, int> gScore = new Dictionary<Component, int>();
        string gameMsg = ""; float gameMsgUntil;
        // картошка
        Component potato, lastHolder; float potatoEnd, passUntil, nextBeep, nextMarkSend, potatoNewAt; float potatoMin = 18f, potatoMax = 34f;
        // король горы
        Vector3 zonePos; float zoneR = 7f; int kothTarget = 45; float nextKothTick;
        // прятки
        int hidePhase; float hideTime = 30f, seekTime = 180f; bool hideBlind; readonly Dictionary<Component, float> seenT = new Dictionary<Component, float>(); readonly HashSet<Component> found = new HashSet<Component>(); List<Component> hiders = new List<Component>();
        // маркеры/маяк (локальные и от друзей)
        GameObject markGo, beaconGo; LineRenderer beaconPillar, beaconRing; int remoteMarkActor = -1; float remoteMarkT; Vector3 remoteZone; float remoteZoneR; bool remoteZoneOn; float remoteZoneT;

        List<Component> GamePlayers()
        {
            var l = Victims(); var me = Game.Me();
            if (gameIncludeMe && me != null && !Game.IsDead(me)) l.Add(me);
            return l;
        }

        void GameToast(string s, bool net = true)
        {
            gameMsg = s; gameMsgUntil = Time.time + 4f; Ui.Toast(s, Ui.Accent2);
            if (net && Game.InRoom) Net.Send("gtoast", s);
        }

        void StopGame(bool toast)
        {
            if (game == GameKind.None) return;
            game = GameKind.None; hideBlind = false; hidePhase = 0; potato = null; found.Clear(); seenT.Clear();
            SetBeacon(Vector3.zero, 0f, false); if (Game.InRoom) { Net.Send("zone", "0,0,0,0,0"); Net.Send("mark", "-1"); }
            if (toast) Ui.Toast("Мини-игра остановлена", Ui.Muted);
        }

        void StartGame(GameKind k)
        {
            var me = Game.Me(); if (me == null) { Ui.Toast("Нет персонажа", Ui.Bad); return; }
            StopGame(false);
            gScore.Clear(); game = k; gameStart = Time.time;
            switch (k)
            {
                case GameKind.Potato:
                    {
                        var pl = GamePlayers(); if (pl.Count < 2) { Ui.Toast("Для картошки нужно хотя бы двое (включи бота-спутника для проверки)", Ui.Bad); game = GameKind.None; return; }
                        PickPotato(null);
                        GameToast("ГОРЯЧАЯ КАРТОШКА! Картошка у " + Game.Name(potato) + " — передавай прикосновением, не то взорвётся", true);
                        break;
                    }
                case GameKind.Koth:
                    {
                        var cam = Camera.main; Vector3 p = me.transform.position; RaycastHit h;
                        if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out h, 150f, ~0, QueryTriggerInteraction.Ignore)) p = h.point;
                        zonePos = p; SetBeacon(zonePos, zoneR, true); SendZone(true);
                        GameToast("КОРОЛЬ ГОРЫ! Стой в золотой зоне — +1 очко в секунду. До " + kothTarget + " очков.", true);
                        nextKothTick = Time.time + 1f; break;
                    }
                case GameKind.Hide:
                    {
                        hiders = Victims();
                        if (hiders.Count == 0) { Ui.Toast("Прятаться некому — нужны друзья (или бот-спутник)", Ui.Bad); game = GameKind.None; return; }
                        hidePhase = 0; hideBlind = true; gameEnd = Time.time + hideTime; found.Clear(); seenT.Clear();
                        GameToast("ПРЯТКИ! " + Game.Name(me) + " водит. Прячьтесь — " + (int)hideTime + " секунд, потом начну искать", true);
                        break;
                    }
            }
        }

        // ---------- картошка ----------
        void PickPotato(Component except)
        {
            var pl = GamePlayers(); pl.Remove(except); if (pl.Count == 0) { StopGame(true); return; }
            potato = pl[UnityEngine.Random.Range(0, pl.Count)];
            potatoEnd = Time.time + UnityEngine.Random.Range(potatoMin, potatoMax); passUntil = Time.time + 1.5f; lastHolder = null; nextBeep = 0f; nextMarkSend = 0f;
        }

        void TickPotato()
        {
            if (Time.time < potatoNewAt) return;
            if (potato == null || Game.IsDead(potato)) { PickPotato(potato); if (potato == null) return; }
            float left = potatoEnd - Time.time;
            // передача прикосновением
            if (Time.time >= passUntil)
                foreach (var p in GamePlayers())
                {
                    if (p == potato || p == lastHolder && Time.time < passUntil + 1.5f) continue;
                    if (Vector3.Distance(CenterOf(p), CenterOf(potato)) < 2.3f)
                    {
                        lastHolder = potato; potato = p; passUntil = Time.time + 1.2f; nextMarkSend = 0f;
                        Sfx2.Beep(0.7f); GameToast("Картошка у " + Game.Name(p) + "!", true); break;
                    }
                }
            if (Time.time >= nextBeep)
            {
                nextBeep = Time.time + Mathf.Lerp(0.18f, 1.1f, Mathf.Clamp01(left / 12f)); Sfx2.Beep(left < 4f ? 0.9f : 0.45f);
            }
            if (Time.time >= nextMarkSend) { nextMarkSend = Time.time + 1f; if (Game.InRoom) Net.Send("mark", Game.ActorOf(potato).ToString()); }
            if (left <= 0f)
            {
                var c = potato; Vector3 p = CenterOf(c);
                Gore.Burst(p, Vector3.up, 2.6f); Gore.Burst(p, Vector3.up + Vector3.right, 1.6f);
                Vfx.Ring(p, Vector3.up, new Color(1f, 0.5f, 0.1f, 0.95f), 10f, 0.7f); Sfx2.Boom(); CamShake.Boost = 1f;
                if (!Game.IsMine(c)) { Rpc(c, "RPCA_Fall", 5f); Rpc(c, "RPCA_AddForceToBodyPart", 0, Vector3.up * launchForce * 1.2f); }
                Shockwave(p, 8f, 0.6f);
                int v; gScore.TryGetValue(c, out v); gScore[c] = v + 1;
                GameToast("БАХ! " + Game.Name(c) + " взорвался (" + (v + 1) + ")", true);
                potatoNewAt = Time.time + 2.5f; potato = null; potatoPending = true;
            }
        }
        bool potatoPending;

        // ---------- король горы ----------
        void SendZone(bool on) { if (Game.InRoom) Net.Send("zone", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F1},{1:F1},{2:F1},{3:F1},{4}", zonePos.x, zonePos.y, zonePos.z, zoneR, on ? 1 : 0)); }

        void TickKoth()
        {
            if (Time.time % 5f < Time.deltaTime) SendZone(true);
            if (Time.time < nextKothTick) return;
            nextKothTick = Time.time + 1f;
            foreach (var p in GamePlayers())
            {
                Vector3 c = CenterOf(p); Vector2 a = new Vector2(c.x - zonePos.x, c.z - zonePos.z);
                if (a.magnitude < zoneR && Mathf.Abs(c.y - zonePos.y) < 7f)
                {
                    int v; gScore.TryGetValue(p, out v); v++; gScore[p] = v;
                    if (v >= kothTarget) { KothWin(p); return; }
                }
            }
        }

        void KothWin(Component p)
        {
            Sfx2.Fanfare(); int pal = Gore.Palette; Gore.Palette = 4;
            for (int i = 0; i < 6; i++) Gore.Burst(CenterOf(p) + UnityEngine.Random.insideUnitSphere, Vector3.up + UnityEngine.Random.insideUnitSphere * 0.5f, 1.2f);
            Gore.Palette = pal;
            Vfx.Ring(CenterOf(p), Vector3.up, new Color(1f, 0.85f, 0.2f, 1f), 12f, 1f);
            GameToast("КОРОЛЬ ГОРЫ — " + Game.Name(p) + "!", true);
            StopGame(false);
        }

        // ---------- прятки ----------
        void TickHide()
        {
            var me = Game.Me(); if (me == null) return;
            float left = gameEnd - Time.time;
            if (hidePhase == 0)
            {
                hideBlind = true;
                if (left <= 0f) { hidePhase = 1; hideBlind = false; gameEnd = Time.time + seekTime; Sfx2.Boom(); GameToast("ИЩУ! Время пошло: " + (int)seekTime + " секунд", true); }
                return;
            }
            var cam = Camera.main; if (cam == null) return;
            foreach (var c in hiders)
            {
                if (c == null || found.Contains(c) || Game.IsDead(c)) continue;
                Vector3 head = HeadOf(c), o = cam.transform.position, to = head - o; float d = to.magnitude;
                bool vis = d < 45f && d > 0.5f && Vector3.Angle(cam.transform.forward, to) < 22f;
                if (vis)
                {
                    RaycastHit h;
                    if (WorldCast(o, 0.12f, to / d, d - 0.3f, out h)) vis = false;
                }
                float t; seenT.TryGetValue(c, out t);
                t = vis ? t + Time.deltaTime : Mathf.Max(0f, t - Time.deltaTime * 0.6f); seenT[c] = t;
                if (t >= 1.0f)
                {
                    found.Add(c);
                    Rpc(c, "RPCA_Fall", 3f); Gore.Burst(CenterOf(c), Vector3.up, 0.5f); Sfx2.Beep(0.8f);
                    GameToast("НАЙДЕН: " + Game.Name(c) + " (" + found.Count + "/" + hiders.Count + ")", true);
                }
            }
            bool all = hiders.All(h => h == null || found.Contains(h) || Game.IsDead(h));
            if (all) { Sfx2.Fanfare(); GameToast("ВОДЯЩИЙ ПОБЕДИЛ — все найдены!", true); StopGame(false); return; }
            if (left <= 0f)
            {
                var left2 = hiders.Where(h => h != null && !found.Contains(h)).Select(h => Game.Name(h)).ToArray();
                Sfx2.Fanfare(); GameToast("ПРЯТАВШИЕСЯ ПОБЕДИЛИ! Не нашёл: " + string.Join(", ", left2), true); StopGame(false);
            }
        }

        // ---------- маркеры и маяк ----------
        void SetBeacon(Vector3 pos, float r, bool on)
        {
            if (!on) { if (beaconGo != null) beaconGo.SetActive(false); return; }
            if (Vfx.Sh() == null) return;
            if (beaconGo == null)
            {
                beaconGo = new GameObject("PF_Beacon"); DontDestroyOnLoad(beaconGo);
                var p = new GameObject("pillar"); p.transform.SetParent(beaconGo.transform, false); beaconPillar = p.AddComponent<LineRenderer>();
                beaconPillar.material = Vfx.Mat(null, Color.white); beaconPillar.positionCount = 2; beaconPillar.useWorldSpace = true; beaconPillar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var rg = new GameObject("ring"); rg.transform.SetParent(beaconGo.transform, false); beaconRing = rg.AddComponent<LineRenderer>();
                beaconRing.material = Vfx.Mat(null, Color.white); beaconRing.loop = true; beaconRing.positionCount = 64; beaconRing.useWorldSpace = true; beaconRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            beaconGo.SetActive(true);
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 3f);
            var col = new Color(1f, 0.82f, 0.15f, 0.55f * pulse + 0.2f);
            beaconPillar.startColor = beaconPillar.endColor = new Color(col.r, col.g, col.b, col.a * 0.8f);
            beaconPillar.startWidth = beaconPillar.endWidth = 0.5f;
            beaconPillar.SetPosition(0, pos); beaconPillar.SetPosition(1, pos + Vector3.up * 60f);
            beaconRing.startColor = beaconRing.endColor = col; beaconRing.startWidth = beaconRing.endWidth = 0.18f;
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI * 2f / 64f; Vector3 q = pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                RaycastHit h; if (Physics.Raycast(q + Vector3.up * 6f, Vector3.down, out h, 14f, ~0, QueryTriggerInteraction.Ignore)) q.y = h.point.y + 0.15f; else q.y = pos.y + 0.15f;
                beaconRing.SetPosition(i, q);
            }
        }

        void TickMarks()
        {
            // маяк: свой или от друга
            if (game == GameKind.Koth) SetBeacon(zonePos, zoneR, true);
            else if (raceOn) SetBeacon(racePos, raceR, true);
            else if (remoteZoneOn && Time.time - remoteZoneT < 12f) SetBeacon(remoteZone, remoteZoneR, true);
            else SetBeacon(Vector3.zero, 0f, false);
            // метка над картошкой
            Component target = null;
            if (game == GameKind.Potato && potato != null) target = potato;
            else if (remoteMarkActor >= 0 && Time.time - remoteMarkT < 3f) target = Game.ByActor(remoteMarkActor);
            if (target == null) { if (markGo != null) markGo.SetActive(false); return; }
            if (markGo == null && Vfx.Sh() != null)
            {
                markGo = new GameObject("PF_Mark"); DontDestroyOnLoad(markGo);
                markGo.AddComponent<MeshFilter>().sharedMesh = Vfx.Sphere(); var mr = markGo.AddComponent<MeshRenderer>(); mr.sharedMaterial = Vfx.Mat(null, new Color(1f, 0.25f, 0.1f));
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (markGo == null) return;
            markGo.SetActive(true);
            float left = game == GameKind.Potato ? potatoEnd - Time.time : 10f;
            float pulse = 1f + 0.25f * Mathf.Sin(Time.time * Mathf.Lerp(4f, 22f, 1f - Mathf.Clamp01(left / 10f)));
            markGo.transform.position = HeadOf(target) + Vector3.up * (0.9f + 0.1f * Mathf.Sin(Time.time * 3f)); markGo.transform.localScale = Vector3.one * 0.45f * pulse;
            markGo.GetComponent<MeshRenderer>().sharedMaterial.color = Color.Lerp(new Color(1f, 0.85f, 0.1f), new Color(1f, 0.1f, 0.05f), 1f - Mathf.Clamp01(left / 10f));
        }

        void OnGameNet(string cmd, string arg, int sender)
        {
            try
            {
                if (cmd == "gtoast") { Ui.Toast(Game.PlayerName(sender) + ": " + arg, Ui.Accent2); return; }
                if (cmd == "mark") { int a; if (int.TryParse(arg, out a)) { remoteMarkActor = a; remoteMarkT = Time.time; } return; }
                if (cmd == "zone")
                {
                    var p = arg.Split(','); var inv = System.Globalization.CultureInfo.InvariantCulture;
                    remoteZoneOn = p[4] == "1"; remoteZone = new Vector3(float.Parse(p[0], inv), float.Parse(p[1], inv), float.Parse(p[2], inv)); remoteZoneR = float.Parse(p[3], inv); remoteZoneT = Time.time;
                }
            }
            catch { }
        }

        void TickGames()
        {
            try
            {
                if (potatoPending && Time.time >= potatoNewAt && game == GameKind.Potato) { potatoPending = false; PickPotato(null); GameToast("Новая картошка у " + Game.Name(potato) + "!", true); }
                switch (game)
                {
                    case GameKind.Potato: if (!potatoPending) TickPotato(); break;
                    case GameKind.Koth: TickKoth(); break;
                    case GameKind.Hide: TickHide(); break;
                }
                TickMarks();
            }
            catch (Exception e) { SafeLog("Games", e); StopGame(false); }
        }

        void DrawGamesHud()
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.7f, 2f);
            if (hideBlind && game == GameKind.Hide)
            {
                Ui.Box(new Rect(0, 0, Screen.width, Screen.height), Color.black, 0f);
                Ui.Text(new Rect(0, Screen.height * 0.4f, Screen.width, 80 * s), "ЖМУРЮСЬ… " + Mathf.CeilToInt(Mathf.Max(0f, gameEnd - Time.time)), (int)(54 * s), Color.white, TextAnchor.MiddleCenter, true);
                Ui.Text(new Rect(0, Screen.height * 0.4f + 80 * s, Screen.width, 40 * s), "друзья прячутся, не подглядывай", (int)(20 * s), Ui.Muted, TextAnchor.MiddleCenter, false);
                return;
            }
            if (game == GameKind.None) return;
            var lines = new List<string>(); string title = ""; string timer = "";
            switch (game)
            {
                case GameKind.Potato: title = "ГОРЯЧАЯ КАРТОШКА"; timer = potato != null ? "у " + Game.Name(potato) : "…"; lines.Add("чаще всех взорвался — проигрывает"); break;
                case GameKind.Koth: title = "КОРОЛЬ ГОРЫ — до " + kothTarget; timer = ""; break;
                case GameKind.Hide: title = hidePhase == 0 ? "ПРЯТКИ: прячутся" : "ПРЯТКИ: поиск"; timer = Mathf.CeilToInt(Mathf.Max(0f, gameEnd - Time.time)) + " с"; lines.Add("найдено " + found.Count + " из " + hiders.Count); break;
            }
            if (game != GameKind.Hide) foreach (var kv in gScore.OrderByDescending(k => k.Value).Take(6)) if (kv.Key != null) lines.Add(Game.Name(kv.Key) + ": " + kv.Value);
            float w = 340 * s, h = (46 + lines.Count * 22) * s, x = 24 * s, y = 24 * s;
            Ui.Box(new Rect(x, y, w, h), new Color(0.04f, 0.05f, 0.1f, 0.78f), 14 * s);
            Ui.Text(new Rect(x + 14 * s, y + 6 * s, w - 28 * s, 28 * s), title, (int)(18 * s), Ui.Accent, TextAnchor.MiddleLeft, true);
            if (timer != "") Ui.Text(new Rect(x + 14 * s, y + 6 * s, w - 28 * s, 28 * s), timer, (int)(16 * s), Ui.Txt, TextAnchor.MiddleRight, true);
            for (int i = 0; i < lines.Count; i++) Ui.Text(new Rect(x + 14 * s, y + 38 * s + i * 22 * s, w - 28 * s, 22 * s), lines[i], (int)(14 * s), Ui.Txt, TextAnchor.MiddleLeft, false);
            if (Time.time < gameMsgUntil) Ui.Text(new Rect(Screen.width * 0.5f - 400 * s, Screen.height * 0.18f, 800 * s, 40 * s), gameMsg, (int)(24 * s), Color.white, TextAnchor.MiddleCenter, true);
        }

        void TabGames()
        {
            Ui.Header("Мини-игры");
            Ui.Para("Судья — ты. У друзей мод не нужен: проигравшие падают и отлетают через игру. Подсказки (картошка над головой, золотая зона, сообщения) видят те, у кого стоит PeakFun, остальным говори голосом.", 13);
            if (!NeedMe()) return;
            gameIncludeMe = Ui.Toggle("Я тоже играю (картошка, король горы)", gameIncludeMe);
            if (game != GameKind.None && Ui.Button("СТОП мини-игру", 2)) StopGame(true);
            if (game != GameKind.None) Ui.Para("Идёт: " + game, 13, Ui.Accent);

            Ui.Header("Горячая картошка");
            Ui.Para("Случайный игрок — «картошка». Над головой горит шар и пищит всё чаще. Передай картошку, коснувшись другого (ближе 2.3 м). Не успел — взрыв: подножка, отброс, брызги, +1 к счёту проигравших. С героями можно хватать и швырять картошку на других!", 12);
            potatoMin = Ui.Slider("Минимум до взрыва", potatoMin, 8f, 40f, "0", " с"); potatoMax = Mathf.Max(potatoMin + 2f, Ui.Slider("Максимум до взрыва", potatoMax, 10f, 70f, "0", " с"));
            if (Ui.Button("Начать «Горячая картошка»", 1)) StartGame(GameKind.Potato);

            Ui.Header("Король горы");
            Ui.Para("Золотая зона там, куда ты смотришь. Кто стоит внутри — получает +1 очко в секунду. Столкни соперников силой героя! Кто первый набрал нужное число — король.", 12);
            kothTarget = Mathf.RoundToInt(Ui.Slider("Очков для победы", kothTarget, 10f, 200f, "0", ""));
            zoneR = Ui.Slider("Радиус зоны", zoneR, 3f, 20f, "0", " м");
            if (Ui.Button("Поставить зону туда, куда смотрю, и начать", 1)) StartGame(GameKind.Koth);

            Ui.Header("Прятки");
            Ui.Para("Ты водишь: экран чёрный, пока друзья прячутся. Потом ищешь: надо держать друга в центре экрана ~1 секунду без стены между вами. Найденного роняет на 3 секунды. Нашёл всех — победил, не успел — победили спрятавшиеся. Лазер Патриота и полёт в помощь.", 12);
            hideTime = Mathf.Round(Ui.Slider("Время на прятки", hideTime, 10f, 120f, "0", " с")); seekTime = Mathf.Round(Ui.Slider("Время на поиск", seekTime, 60f, 600f, "0", " с"));
            if (Ui.Button("Начать «Прятки»", 1)) StartGame(GameKind.Hide);

            if (gScore.Count > 0)
            {
                Ui.Header("Счёт");
                foreach (var kv in gScore.OrderByDescending(k => k.Value)) if (kv.Key != null) Ui.Para(Game.Name(kv.Key) + ": " + kv.Value, 14);
            }
        }
    }
}
