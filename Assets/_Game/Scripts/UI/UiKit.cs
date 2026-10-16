using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CircuitBreaker.Fx;

namespace CircuitBreaker.UI
{
    /// <summary>Design tokens. Monochrome on purpose: the arrows carry the colour, the interface stays out of the way.</summary>
    public static class Theme
    {
        public static readonly Color Bg = Hex("0A0A0C");
        public static readonly Color Surface = Hex("131316");
        public static readonly Color Surface2 = Hex("1B1B1F");
        public static readonly Color Line = Hex("2A2A30");
        public static readonly Color Text = Hex("F4F4F6");
        public static readonly Color Dim = Hex("9A9AA4");
        public static readonly Color Faint = Hex("5A5A64");
        public static readonly Color Accent = Hex("19E6F2");
        public static readonly Color Danger = Hex("D86A6A");
        public static readonly Color Overlay = new Color(0f, 0f, 0f, 0.72f);

        public const int TitleSize = 60, H1 = 46, H2 = 38, Body = 32, Caption = 26, Micro = 22;
        public const float Radius = 18f;

        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        public static Color A(Color c, float a) { c.a = a; return c; }
    }

    public enum ButtonStyle { Primary, Secondary, Ghost }

    /// <summary>Small factory for the UI. Layout is done with layout groups so spacing stays consistent everywhere.</summary>
    public static class UiKit
    {
        // ---------- primitives ----------
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform r, float l = 0, float b = 0, float rt = 0, float t = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -t);
        }

        public static void Anchor(RectTransform r, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.pivot = pivot;
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        public static Image Img(Transform parent, Color color, Sprite sprite = null, bool raycast = false)
        {
            var r = Rect("Image", parent);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : ProcSprites.Pixel;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Rounded(Transform parent, Color color, float radius = Theme.Radius, bool raycast = false)
        {
            var img = Img(parent, color, ProcSprites.Rounded, raycast);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 22f / radius;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false, bool wrap = false)
        {
            var r = Rect("Text", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = UiFont.Get();
            t.fontSize = size;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = anchor;
            t.color = color;
            t.text = text;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Image Icon(Transform parent, string name, float size, Color color)
        {
            var img = Img(parent, color, Icons.Get(name));
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.preserveAspect = true;
            return img;
        }

        // ---------- layout containers ----------
        public static LayoutElement Size(Component c, float w = -1, float h = -1, float minH = -1, float flexW = -1)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; }
            if (minH >= 0) le.minHeight = minH;
            if (flexW >= 0) le.flexibleWidth = flexW;
            return le;
        }

        public static RectTransform Column(Transform parent, float spacing = 20, int padH = 0, int padV = 0, bool fitHeight = true, TextAnchor align = TextAnchor.UpperCenter)
        {
            var r = Rect("Column", parent);
            var g = r.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = new RectOffset(padH, padH, padV, padV);
            g.childControlWidth = true; g.childControlHeight = true;
            g.childForceExpandWidth = true; g.childForceExpandHeight = false;
            g.childAlignment = align;
            if (fitHeight) r.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return r;
        }

        public static RectTransform Row(Transform parent, float spacing = 16, TextAnchor align = TextAnchor.MiddleLeft, bool expand = false)
        {
            var r = Rect("Row", parent);
            var g = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.childControlWidth = true; g.childControlHeight = true;
            g.childForceExpandWidth = expand; g.childForceExpandHeight = false;
            g.childAlignment = align;
            return r;
        }

        public static void Spacer(Transform parent, float height) => Size(Rect("Spacer", parent), h: height);

        public static void Divider(Transform parent)
        {
            var d = Img(parent, Theme.Line);
            Size(d.rectTransform, h: 2);
        }

        // ---------- surfaces ----------
        public class Surface
        {
            public RectTransform root;
            public Image border, fill;
            public RectTransform content;
        }

        /// <summary>A rounded card with a 2px hairline border. `content` is where children go.</summary>
        public static Surface Card(Transform parent, Color? fillColor = null, Color? borderColor = null, float radius = Theme.Radius, bool raycast = false)
        {
            var s = new Surface();
            s.border = Rounded(parent, borderColor ?? Theme.Line, radius, raycast);
            s.root = s.border.rectTransform;
            s.fill = Rounded(s.root, fillColor ?? Theme.Surface, Mathf.Max(2f, radius - 2f));
            Stretch(s.fill.rectTransform, 2, 2, 2, 2);
            s.content = Rect("Content", s.root);
            Stretch(s.content);
            return s;
        }

        // ---------- buttons ----------
        public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            private Vector3 _base = Vector3.one;
            private void Awake() => _base = transform.localScale;
            public void OnPointerDown(PointerEventData e) => transform.localScale = _base * 0.97f;
            public void OnPointerUp(PointerEventData e) => transform.localScale = _base;
            public void OnPointerExit(PointerEventData e) => transform.localScale = _base;
            private void OnDisable() => transform.localScale = _base;
        }

        public class UiButton
        {
            public Button button;
            public Text label;
            public Image icon, border, fill;
            public CanvasGroup group;
            public ButtonStyle style;

            public void SetText(string t) { if (label != null) label.text = t; }
            public void SetEnabled(bool on) { button.interactable = on; group.alpha = on ? 1f : 0.38f; }
            public void SetVisible(bool on) => button.gameObject.SetActive(on);
        }

        public static UiButton MakeButton(Transform parent, string text, ButtonStyle style, Action onClick, string iconName = null, float height = 104, int fontSize = Theme.Body)
        {
            var ub = new UiButton { style = style };
            Color fillC = style == ButtonStyle.Primary ? Theme.Accent : style == ButtonStyle.Secondary ? Theme.Surface : Color.clear;
            Color borderC = style == ButtonStyle.Primary ? Theme.Accent : style == ButtonStyle.Secondary ? Theme.Line : Color.clear;
            Color textC = style == ButtonStyle.Primary ? Theme.Bg : style == ButtonStyle.Secondary ? Theme.Text : Theme.Dim;

            var s = Card(parent, fillC, borderC, 16f, raycast: true);
            ub.border = s.border; ub.fill = s.fill;
            s.root.name = "Btn_" + text;
            Size(s.root, h: height);

            var row = Row(s.content, 14, TextAnchor.MiddleCenter);
            Stretch(row);
            if (iconName != null) { ub.icon = Icon(row, iconName, height * 0.34f, textC); Size(ub.icon.rectTransform, w: height * 0.34f, h: height * 0.34f); }
            if (!string.IsNullOrEmpty(text))
            {
                ub.label = Label(row, text, fontSize, textC, TextAnchor.MiddleCenter, bold: style != ButtonStyle.Ghost);
                ub.label.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            ub.button = s.root.gameObject.AddComponent<Button>();
            ub.button.transition = Selectable.Transition.None;
            ub.button.targetGraphic = s.border;
            s.root.gameObject.AddComponent<PressFeedback>();
            ub.group = s.root.gameObject.AddComponent<CanvasGroup>();
            ub.button.onClick.AddListener(() => { Sfx.Click(); onClick?.Invoke(); });
            return ub;
        }

        /// <summary>Turns any card into a button with press feedback.</summary>
        public static UiButton Tappable(Surface s, Action onClick)
        {
            var ub = new UiButton { style = ButtonStyle.Secondary, border = s.border, fill = s.fill };
            ub.button = s.root.gameObject.AddComponent<Button>();
            ub.button.transition = Selectable.Transition.None;
            ub.button.targetGraphic = s.border;
            s.root.gameObject.AddComponent<PressFeedback>();
            ub.group = s.root.gameObject.AddComponent<CanvasGroup>();
            ub.button.onClick.AddListener(() => { Sfx.Click(); onClick?.Invoke(); });
            return ub;
        }

        /// <summary>Square line-icon button (top bar, power-ups).</summary>
        public static UiButton MakeIconButton(Transform parent, string iconName, Action onClick, float size = 96, bool bordered = true)
        {
            var ub = new UiButton { style = ButtonStyle.Secondary };
            var s = Card(parent, bordered ? Theme.Surface : Color.clear, bordered ? Theme.Line : Color.clear, 16f, raycast: true);
            ub.border = s.border; ub.fill = s.fill;
            s.root.name = "Icon_" + iconName;
            Size(s.root, w: size, h: size);
            ub.icon = Icon(s.content, iconName, size * 0.5f, Theme.Text);
            ub.icon.rectTransform.anchorMin = ub.icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ub.icon.rectTransform.anchoredPosition = Vector2.zero;
            ub.button = s.root.gameObject.AddComponent<Button>();
            ub.button.transition = Selectable.Transition.None;
            ub.button.targetGraphic = s.border;
            s.root.gameObject.AddComponent<PressFeedback>();
            ub.group = s.root.gameObject.AddComponent<CanvasGroup>();
            ub.button.onClick.AddListener(() => { Sfx.Click(); onClick?.Invoke(); });
            return ub;
        }

        // ---------- list rows ----------
        /// <summary>A tappable row: icon, title + subtitle, optional trailing text and chevron.</summary>
        public static UiButton MakeRow(Transform parent, string title, string subtitle, string iconName, Action onClick, string trailing = null, bool chevron = true, float height = 128)
        {
            var ub = new UiButton { style = ButtonStyle.Secondary };
            var s = Card(parent, Theme.Surface, Theme.Line, 16f, raycast: true);
            ub.border = s.border; ub.fill = s.fill;
            s.root.name = "Row_" + title;
            Size(s.root, h: height);

            var row = Row(s.content, 22, TextAnchor.MiddleLeft);
            Stretch(row, 32, 0, 28, 0);
            if (iconName != null)
            {
                var ic = Icon(row, iconName, 52, Theme.Text);
                Size(ic.rectTransform, w: 52, h: 52);
            }
            var col = Column(row, 4, fitHeight: false, align: TextAnchor.MiddleLeft);
            Size(col, flexW: 1);
            ub.label = Label(col, title, Theme.Body, Theme.Text, TextAnchor.MiddleLeft, bold: true);
            if (!string.IsNullOrEmpty(subtitle)) Label(col, subtitle, Theme.Caption, Theme.Dim, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(trailing))
            {
                var tr = Label(row, trailing, Theme.Caption, Theme.Dim, TextAnchor.MiddleRight);
                Size(tr, minH: 0);
            }
            if (chevron) { var ch = Icon(row, "next", 40, Theme.Faint); Size(ch.rectTransform, w: 40, h: 40); }

            ub.button = s.root.gameObject.AddComponent<Button>();
            ub.button.transition = Selectable.Transition.None;
            ub.button.targetGraphic = s.border;
            s.root.gameObject.AddComponent<PressFeedback>();
            ub.group = s.root.gameObject.AddComponent<CanvasGroup>();
            ub.button.onClick.AddListener(() => { Sfx.Click(); onClick?.Invoke(); });
            return ub;
        }

        public class ToggleRow
        {
            public Image knob, track;
            public Text label;
            public bool value;
            public Action<bool> changed;
            public void Set(bool v, bool notify = false)
            {
                value = v;
                track.color = v ? Theme.Text : Theme.Line;
                knob.color = v ? Theme.Bg : Theme.Dim;
                knob.rectTransform.anchoredPosition = new Vector2(v ? 22 : -22, 0);
                if (notify) changed?.Invoke(v);
            }
        }

        public static ToggleRow MakeToggle(Transform parent, string title, string subtitle, bool initial, Action<bool> changed)
        {
            var tr = new ToggleRow { changed = changed };
            var s = Card(parent, Theme.Surface, Theme.Line, 16f, raycast: true);
            s.root.name = "Toggle_" + title;
            Size(s.root, h: 116);
            var row = Row(s.content, 20, TextAnchor.MiddleLeft);
            Stretch(row, 32, 0, 32, 0);
            var col = Column(row, 4, fitHeight: false, align: TextAnchor.MiddleLeft);
            Size(col, flexW: 1);
            tr.label = Label(col, title, Theme.Body, Theme.Text, TextAnchor.MiddleLeft, bold: true);
            if (!string.IsNullOrEmpty(subtitle)) Label(col, subtitle, Theme.Caption, Theme.Dim, TextAnchor.MiddleLeft);

            tr.track = Rounded(row, Theme.Line, 30f);
            Size(tr.track.rectTransform, w: 104, h: 60);
            tr.knob = Rounded(tr.track.rectTransform, Theme.Dim, 24f);
            tr.knob.rectTransform.sizeDelta = new Vector2(48, 48);
            tr.knob.rectTransform.anchorMin = tr.knob.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            var b = s.root.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = s.border;
            b.onClick.AddListener(() => { Sfx.Click(); tr.Set(!tr.value, true); });
            tr.Set(initial);
            return tr;
        }

        public static Image ProgressBar(Transform parent, float fraction, float height = 8)
        {
            var bg = Rounded(parent, Theme.Line, height * 0.5f);
            Size(bg.rectTransform, h: height);
            var fg = Rounded(bg.rectTransform, Theme.Text, height * 0.5f);
            fg.rectTransform.anchorMin = Vector2.zero;
            fg.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            fg.rectTransform.offsetMin = fg.rectTransform.offsetMax = Vector2.zero;
            return fg;
        }

        // ---------- scrolling ----------
        /// <summary>A vertically scrolling column. Returns the content container to add children to.</summary>
        public static RectTransform ScrollColumn(Transform parent, float spacing = 20, int padH = 0, int padV = 0)
        {
            var sv = Rect("Scroll", parent);
            Stretch(sv);
            var hit = Img(sv, Color.clear, null, raycast: true);
            Stretch(hit.rectTransform);
            var viewport = Rect("Viewport", sv);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Column(viewport, spacing, padH, padV);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var sr = sv.gameObject.AddComponent<ScrollRect>();
            sr.viewport = viewport; sr.content = content;
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 40f; sr.decelerationRate = 0.12f;
            return content;
        }
    }
}
