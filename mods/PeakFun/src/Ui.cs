// Мини-библиотека красивого IMGUI: скруглённые панели, плавные анимации, переключатели, слайдеры, тосты.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakFun
{
    internal static class Ui
    {
        // ---------- палитра ----------
        public static Color Bg = new Color(0.055f, 0.07f, 0.125f, 0.97f);
        public static Color Side = new Color(0.075f, 0.095f, 0.17f, 1f);
        public static Color Card = new Color(0.105f, 0.13f, 0.225f, 1f);
        public static Color CardHi = new Color(0.16f, 0.19f, 0.31f, 1f);
        public static Color Accent = new Color(1f, 0.55f, 0.24f, 1f);
        public static Color Accent2 = new Color(1f, 0.24f, 0.5f, 1f);
        public static Color Txt = new Color(0.92f, 0.94f, 1f, 1f);
        public static Color Muted = new Color(0.56f, 0.62f, 0.78f, 1f);
        public static Color Good = new Color(0.3f, 0.87f, 0.5f, 1f);
        public static Color Bad = new Color(1f, 0.32f, 0.36f, 1f);

        public static Color GradC = new Color(0.55f, 0.36f, 0.96f);
        public static Color Orb1 = new Color(1f, 0.45f, 0.2f, 0.10f), Orb2 = new Color(0.5f, 0.3f, 1f, 0.12f), Orb3 = new Color(1f, 0.2f, 0.45f, 0.08f);
        public static bool Dark = true;
        public static Texture2D GradH, Glow;

        public static void RebuildGradient()
        {
            if (GradH == null) return;
            Color a = Accent, b = Accent2, c = GradC;
            for (int i = 0; i < 256; i++)
            {
                float t = i / 256f * 3f;
                GradH.SetPixel(i, 0, t < 1 ? Color.Lerp(a, b, t) : t < 2 ? Color.Lerp(b, c, t - 1) : Color.Lerp(c, a, t - 2));
            }
            GradH.Apply();
            if (sLabel != null) sLabel.normal.textColor = Txt;
            if (sField != null) { sField.normal.textColor = Txt; sField.focused.textColor = Txt; sField.hover.textColor = Txt; sField.active.textColor = Txt; }
        }
        static GUIStyle sLabel, sField;
        static bool inited;

        // ---------- раскладка ----------
        public static float X, Y, W;
        public static float Dt = 0.016f;
        public static float Time_;
        public static float Alpha = 1f;   // общая прозрачность (анимация открытия/уведомления)

        public static void Init()
        {
            if (inited && GradH != null) return;
            inited = true;

            // горизонтальный градиент (бесшовный): оранжевый -> розовый -> фиолетовый -> оранжевый
            GradH = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            Color a = Accent, b = Accent2, c = GradC;
            for (int i = 0; i < 256; i++)
            {
                float t = i / 256f * 3f;
                Color col = t < 1 ? Color.Lerp(a, b, t) : t < 2 ? Color.Lerp(b, c, t - 1) : Color.Lerp(c, a, t - 2);
                GradH.SetPixel(i, 0, col);
            }
            GradH.Apply();

            // мягкое радиальное свечение
            Glow = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f);
                    float al = Mathf.Pow(1f - d, 2.2f);
                    Glow.SetPixel(x, y, new Color(1, 1, 1, al));
                }
            Glow.Apply();

            sLabel = new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Clip, richText = true, alignment = TextAnchor.MiddleLeft };
            sLabel.normal.textColor = Txt;
            sField = new GUIStyle(GUI.skin.textField) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 4, 4) };
            sField.normal.background = null; sField.focused.background = null; sField.hover.background = null; sField.active.background = null;
            sField.normal.textColor = Txt; sField.focused.textColor = Txt; sField.hover.textColor = Txt; sField.active.textColor = Txt;
        }

        // ---------- анимации ----------
        static readonly Dictionary<string, float> anim = new Dictionary<string, float>();

        public static float Anim(string key, float target, float speed = 12f)
        {
            if (!anim.TryGetValue(key, out var v)) v = target;
            if (Event.current != null && Event.current.type == EventType.Repaint)
            {
                v += (target - v) * (1f - Mathf.Exp(-speed * Dt));
                if (Mathf.Abs(target - v) < 0.002f) v = target;
                anim[key] = v;
            }
            return v;
        }

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - Mathf.Pow(1f - t, 3f); }


        // ---------- «Noir»-оформление (1.25): чёткие рамки, угловые скобки, строчная развёртка ----------
        public static bool Sharp = true;
        static Texture2D scanTex;
        public static void Border(Rect r, Color c, float th = 1f)
        {
            if (Event.current.type != EventType.Repaint) return; c.a *= Alpha; if (c.a <= 0.001f) return;
            GUI.color = c; var w = Texture2D.whiteTexture; GUI.DrawTexture(new Rect(r.x, r.y, r.width, th), w); GUI.DrawTexture(new Rect(r.x, r.yMax - th, r.width, th), w);
            GUI.DrawTexture(new Rect(r.x, r.y + th, th, r.height - 2f * th), w); GUI.DrawTexture(new Rect(r.xMax - th, r.y + th, th, r.height - 2f * th), w); GUI.color = Color.white;
        }
        public static void Brackets(Rect r, Color c, float len = 14f, float th = 2f)
        {
            if (Event.current.type != EventType.Repaint) return; c.a *= Alpha; if (c.a <= 0.001f) return;
            GUI.color = c; var w = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(r.x, r.y, len, th), w); GUI.DrawTexture(new Rect(r.x, r.y, th, len), w); GUI.DrawTexture(new Rect(r.xMax - len, r.y, len, th), w); GUI.DrawTexture(new Rect(r.xMax - th, r.y, th, len), w);
            GUI.DrawTexture(new Rect(r.x, r.yMax - th, len, th), w); GUI.DrawTexture(new Rect(r.x, r.yMax - len, th, len), w); GUI.DrawTexture(new Rect(r.xMax - len, r.yMax - th, len, th), w); GUI.DrawTexture(new Rect(r.xMax - th, r.yMax - len, th, len), w); GUI.color = Color.white;
        }
        public static void Scan(Rect r, float alpha)
        {
            if (Event.current.type != EventType.Repaint || alpha <= 0.001f) return;
            if (scanTex == null) { scanTex = new Texture2D(1, 3, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point }; scanTex.SetPixels(new[] { new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), new Color(0, 0, 0, 1) }); scanTex.Apply(); }
            GUI.color = new Color(1, 1, 1, alpha * Alpha); GUI.DrawTextureWithTexCoords(r, scanTex, new Rect(0, 0, 1, r.height / 3f)); GUI.color = Color.white;
        }
        public static bool LightAccent { get { return Accent.r * 0.3f + Accent.g * 0.59f + Accent.b * 0.11f > 0.55f; } }

        // ---------- примитивы ----------
        public static void Box(Rect r, Color c, float radius = 0f)
        {
            c.a *= Alpha;
            if (Event.current.type != EventType.Repaint || c.a <= 0.001f) return;
            if (Sharp && radius > 0.5f) radius = Mathf.Min(radius, radius > 12f ? 5f : 3f);
            if (radius <= 0.5f) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; return; }
            int rad = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f)), 1, 48);
            var st = RoundStyle(rad);
            GUI.color = c;
            st.Draw(r, GUIContent.none, false, false, false, false);
            GUI.color = Color.white;
        }

        // Скруглённые прямоугольники: 9-slice текстура с антиалиасингом, по одной на радиус.
        static readonly Dictionary<int, GUIStyle> roundStyles = new Dictionary<int, GUIStyle>();

        static GUIStyle RoundStyle(int rad)
        {
            if (roundStyles.TryGetValue(rad, out var st) && st.normal.background != null) return st;
            int size = rad * 2 + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // расстояние до скруглённого прямоугольника (знаковое)
                    float dx = Mathf.Max(Mathf.Abs(x - c) - (c - rad), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y - c) - (c - rad), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - rad + 0.5f;
                    px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                }
            tex.SetPixels(px); tex.Apply();
            st = new GUIStyle();
            st.normal.background = tex;
            st.border = new RectOffset(rad + 1, rad + 1, rad + 1, rad + 1);
            roundStyles[rad] = st;
            return st;
        }

        public static void Gradient(Rect r, float alpha, float scroll)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.color = new Color(1, 1, 1, alpha * Alpha);
            GUI.DrawTextureWithTexCoords(r, GradH, new Rect(scroll, 0, 1, 1));
            GUI.color = Color.white;
        }

        public static void Orb(Vector2 center, float size, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            c.a *= Alpha;
            GUI.color = c;
            GUI.DrawTexture(new Rect(center.x - size / 2, center.y - size / 2, size, size), Glow);
            GUI.color = Color.white;
        }

        public static void Text(Rect r, string s, int size, Color col, TextAnchor al = TextAnchor.MiddleLeft, bool bold = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            sLabel.fontSize = size; sLabel.alignment = al; sLabel.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            col.a *= Alpha;
            sLabel.normal.textColor = col;
            GUI.Label(r, s, sLabel);
        }

        public static void TextWrap(Rect r, string s, int size, Color col)
        {
            if (Event.current.type != EventType.Repaint) return;
            sLabel.fontSize = size; sLabel.alignment = TextAnchor.UpperLeft; sLabel.fontStyle = FontStyle.Normal;
            sLabel.wordWrap = true; col.a *= Alpha; sLabel.normal.textColor = col;
            GUI.Label(r, s, sLabel);
            sLabel.wordWrap = false;
        }

        public static float TextHeight(string s, int size, float width)
        {
            sLabel.fontSize = size; sLabel.wordWrap = true; sLabel.fontStyle = FontStyle.Normal;
            float h = sLabel.CalcHeight(new GUIContent(s), width);
            sLabel.wordWrap = false;
            return h;
        }

        // ---------- раскладка ----------
        public static void Begin(float x, float y, float w) { X = x; Y = y; W = w; }
        public static Rect Next(float h, float gap = 6f) { var r = new Rect(X, Y, W, h); Y += h + gap; return r; }
        public static void Space(float h) { Y += h; }

        public static void Label(string s, int size = 15, Color? col = null, bool bold = false)
        {
            var r = Next(size + 9, 2);
            Text(r, s, size, col ?? Txt, TextAnchor.MiddleLeft, bold);
        }

        public static void Para(string s, int size = 14, Color? col = null)
        {
            float h = TextHeight(s, size, W) + 4;
            var r = Next(h, 4);
            TextWrap(r, s, size, col ?? Muted);
        }

        public static void Header(string s)
        {
            Space(6);
            var r = Next(24, 6);
            Box(new Rect(r.x, r.y + 4, 3, 16), Accent, 1);
            Text(new Rect(r.x + 12, r.y, r.width - 12, r.height), s.ToUpperInvariant(), 13, Muted, TextAnchor.MiddleLeft, true);
            Box(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(Accent.r, Accent.g, Accent.b, 0.22f), 0f);
        }

        // ---------- кнопка ----------
        // style: 0 обычная, 1 акцентная, 2 опасная, 3 «выбрана»
        public static bool ButtonRect(Rect r, string label, int style = 0, bool enabled = true, int fontSize = 15)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            bool clicked = false;
            enabled = enabled && GUI.enabled;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (enabled && e.button == 0 && r.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0; e.Use();
                        if (r.Contains(e.mousePosition)) clicked = true;
                    }
                    break;
                case EventType.Repaint:
                    {
                        bool hover = enabled && r.Contains(e.mousePosition) && (GUIUtility.hotControl == 0 || GUIUtility.hotControl == id);
                        bool press = GUIUtility.hotControl == id;
                        string key = "b" + id + label;
                        float h = Anim(key + "h", hover ? 1f : 0f, 16f);
                        float p = Anim(key + "p", press ? 1f : 0f, 30f);
                        var rr = new Rect(r.x + p * 1.5f, r.y + p * 1.5f, r.width - p * 3f, r.height - p * 3f);
                        Color baseC = style == 1 ? Accent : style == 2 ? new Color(0.8f, 0.2f, 0.28f) : style == 3 ? Color.Lerp(CardHi, Accent, 0.28f) : Card;
                        Color hiC = style == 1 ? Color.Lerp(Accent, Accent2, 0.55f) : style == 2 ? Bad : CardHi;
                        if (style == 3) hiC = Color.Lerp(CardHi, Accent, 0.45f);
                        if (h > 0.01f) Box(new Rect(rr.x - 2, rr.y - 2, rr.width + 4, rr.height + 4), new Color(hiC.r, hiC.g, hiC.b, 0.35f * h), 10);
                        Box(rr, Color.Lerp(baseC, hiC, h), 8);
                        Border(rr, style == 1 ? Color.Lerp(Accent, Color.white, 0.25f * h) : style == 2 ? new Color(Bad.r, Bad.g, Bad.b, 0.7f) : Color.Lerp(new Color(Accent.r, Accent.g, Accent.b, style == 3 ? 0.6f : 0.16f), Accent, h * 0.9f), 1f);
                        if (style == 3) Box(new Rect(rr.x + 8, rr.yMax - 4, rr.width - 16, 2), Accent, 1);
                        Color tc = enabled ? (style == 1 ? (LightAccent ? new Color(0.06f, 0.05f, 0.02f) : Color.white) : style == 2 ? Color.white : Txt) : new Color(Muted.r, Muted.g, Muted.b, 0.5f);
                        Text(new Rect(rr.x + 6, rr.y, rr.width - 12, rr.height), label, fontSize, tc, TextAnchor.MiddleCenter, style == 1 || style == 3);
                        break;
                    }
            }
            return clicked;
        }

        public static bool Button(string label, int style = 0, bool enabled = true)
        {
            return ButtonRect(Next(36, 6), label, style, enabled);
        }

        /// <summary>Ряд кнопок в несколько колонок. Возвращает индекс нажатой или -1.</summary>
        public static int Buttons(string[] labels, int cols, int style = 0, float h = 34f, bool enabled = true, int selected = -1, int fontSize = 14)
        {
            int clicked = -1;
            float gap = 6f;
            float bw = (W - gap * (cols - 1)) / cols;
            for (int i = 0; i < labels.Length; i++)
            {
                int col = i % cols;
                var r = new Rect(X + col * (bw + gap), Y, bw, h);
                int st = i == selected ? 3 : style;
                if (ButtonRect(r, labels[i], st, enabled, fontSize)) clicked = i;
                if (col == cols - 1 || i == labels.Length - 1) Y += h + gap;
            }
            return clicked;
        }

        // ---------- переключатель ----------
        public static bool Toggle(string label, bool v, string hint = null)
        {
            float h = hint == null ? 36f : 48f;
            var r = Next(h, 6);
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (GUI.enabled && e.button == 0 && r.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0; e.Use();
                        if (r.Contains(e.mousePosition)) v = !v;
                    }
                    break;
                case EventType.Repaint:
                    {
                        string key = "t" + id + label;
                        bool hover = r.Contains(e.mousePosition) && GUI.enabled;
                        float hv = Anim(key + "h", hover ? 1f : 0f, 14f);
                        float t = Anim(key, v ? 1f : 0f, 14f);
                        Box(r, Color.Lerp(Card, CardHi, hv), 8);
                        Text(new Rect(r.x + 12, r.y + (hint == null ? 0 : 3), r.width - 80, hint == null ? r.height : 24), label, 15, Txt, TextAnchor.MiddleLeft);
                        if (hint != null) Text(new Rect(r.x + 12, r.y + 24, r.width - 80, 20), hint, 12, Muted, TextAnchor.MiddleLeft);
                        var tr = new Rect(r.xMax - 58, r.y + r.height / 2 - 12, 46, 24);
                        Border(r, new Color(Accent.r, Accent.g, Accent.b, 0.1f + 0.2f * hv), 1f);
                        Box(tr, Color.Lerp(new Color(Muted.r * 0.35f, Muted.g * 0.35f, Muted.b * 0.35f), Accent, t), 12);
                        if (t > 0.02f) Gradient(tr, t, Time_ * 0.15f);
                        float kx = Mathf.Lerp(tr.x + 3, tr.xMax - 21, t);
                        Box(new Rect(kx, tr.y + 3, 18, 18), t > 0.5f && LightAccent ? new Color(0.08f, 0.07f, 0.03f) : Color.white, 9);
                        break;
                    }
            }
            return v;
        }

        // ---------- слайдер ----------
        public static float Slider(string label, float v, float min, float max, string fmt = "0.0", string suffix = "")
        {
            var r = Next(46, 6);
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            var track = new Rect(r.x + 14, r.y + 31, r.width - 28, 6);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (GUI.enabled && e.button == 0 && new Rect(r.x, r.y + 18, r.width, r.height - 18).Contains(e.mousePosition))
                    { GUIUtility.hotControl = id; v = FromMouse(track, e.mousePosition.x, min, max); e.Use(); }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id) { v = FromMouse(track, e.mousePosition.x, min, max); e.Use(); }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                    break;
                case EventType.Repaint:
                    {
                        string key = "s" + id + label;
                        bool active = GUIUtility.hotControl == id;
                        bool hover = r.Contains(e.mousePosition);
                        float hv = Anim(key + "h", active ? 1f : hover ? 0.5f : 0f, 14f);
                        float t = Anim(key, Mathf.InverseLerp(min, max, v), 20f);
                        Box(r, Color.Lerp(Card, CardHi, hv * 0.8f), 8);
                        Text(new Rect(r.x + 12, r.y + 2, r.width - 100, 22), label, 14, Txt, TextAnchor.MiddleLeft);
                        Text(new Rect(r.xMax - 112, r.y + 2, 100, 22), v.ToString(fmt) + suffix, 14, Accent, TextAnchor.MiddleRight, true);
                        Box(track, new Color(Muted.r * 0.35f, Muted.g * 0.35f, Muted.b * 0.35f), 3);
                        var fill = new Rect(track.x, track.y, track.width * t, track.height);
                        Box(fill, Accent, 3);
                        if (fill.width > 2) Gradient(fill, 1f, Time_ * 0.2f);
                        float ks = 14 + hv * 4;
                        Box(new Rect(track.x + track.width * t - ks / 2, track.y + 3 - ks / 2, ks, ks), Color.white, ks / 2);
                        break;
                    }
            }
            return v;
        }

        static float FromMouse(Rect track, float mx, float min, float max)
        {
            return Mathf.Lerp(min, max, Mathf.Clamp01((mx - track.x) / track.width));
        }

        // ---------- поле ввода ----------
        public static string Field(string value, string placeholder)
        {
            var r = Next(36, 6);
            Box(r, Card, 8); Border(r, new Color(Accent.r, Accent.g, Accent.b, 0.25f), 1f);
            if (Event.current.type == EventType.Repaint && string.IsNullOrEmpty(value))
                Text(new Rect(r.x + 12, r.y, r.width - 24, r.height), placeholder, 14, Muted, TextAnchor.MiddleLeft);
            return GUI.TextField(r, value ?? "", 40, sField);
        }

        // ---------- уведомления ----------
        class ToastItem { public string text; public float born; public Color col; }
        static readonly List<ToastItem> toasts = new List<ToastItem>();

        public static void Toast(string text, Color? col = null)
        {
            toasts.Add(new ToastItem { text = text, born = UnityEngine.Time.unscaledTime, col = col ?? Accent });
            if (toasts.Count > 6) toasts.RemoveAt(0);
        }

        public static void DrawToasts(float screenW)
        {
            if (toasts.Count == 0) return;
            float now = UnityEngine.Time.unscaledTime;
            toasts.RemoveAll(t => now - t.born > 4.2f);
            float y = 18f;
            foreach (var t in toasts)
            {
                float age = now - t.born;
                float inT = EaseOutBack(age / 0.45f);
                float outT = 1f - Mathf.Clamp01((age - 3.6f) / 0.6f);
                float w = 420f, h = 40f;
                float x = Mathf.Lerp(screenW + 10f, screenW - 18f - w, Mathf.Clamp01(inT));
                var r = new Rect(x, y, w, h);
                float oldA = Alpha; Alpha = oldA * outT;
                Box(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(t.col.r, t.col.g, t.col.b, 0.25f), 12);
                Box(r, new Color(Bg.r * 0.9f, Bg.g * 0.9f, Bg.b * 0.9f, 0.96f), 10); Border(r, new Color(t.col.r, t.col.g, t.col.b, 0.5f), 1f);
                Box(new Rect(r.x, r.y + 6, 4, r.height - 12), t.col, 2);
                Text(new Rect(r.x + 16, r.y, r.width - 24, r.height), t.text, 15, Txt, TextAnchor.MiddleLeft, true);
                Alpha = oldA;
                y += (h + 8f) * Mathf.Clamp01(outT + 0.0001f);
            }
        }
    }
}
