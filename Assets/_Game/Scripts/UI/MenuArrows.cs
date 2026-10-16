using UnityEngine;
using UnityEngine.UI;
using CircuitBreaker.Fx;

namespace CircuitBreaker.UI
{
    /// <summary>Faint neon arrows drifting across the menu background: the game's motif, kept low-contrast so it never fights the content.</summary>
    public class MenuArrows : MonoBehaviour
    {
        private struct Glyph { public RectTransform rt; public Vector2 dir; public float speed; }
        private readonly System.Collections.Generic.List<Glyph> _glyphs = new System.Collections.Generic.List<Glyph>();
        private RectTransform _area;

        public static MenuArrows Create(RectTransform area, int count = 11)
        {
            var host = UiKit.Rect("Arrows", area);
            UiKit.Stretch(host);
            var m = host.gameObject.AddComponent<MenuArrows>();
            m._area = host;
            var rng = new System.Random(7);
            Vector2[] dirs = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
            for (int i = 0; i < count; i++)
            {
                var img = UiKit.Img(host, Theme.A(CyberTheme.Arrow(i), 0.07f + (float)rng.NextDouble() * 0.10f), ProcSprites.ArrowGlyph);
                img.raycastTarget = false;
                float len = 150 + (float)rng.NextDouble() * 220;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(len, len);
                var dir = dirs[rng.Next(dirs.Length)];
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                rt.anchoredPosition = new Vector2((float)rng.NextDouble() * 1080f, (float)rng.NextDouble() * 1920f);
                m._glyphs.Add(new Glyph { rt = rt, dir = dir, speed = 14 + (float)rng.NextDouble() * 26 });
            }
            return m;
        }

        private void Update()
        {
            var r = _area.rect;
            float w = Mathf.Max(1f, r.width), h = Mathf.Max(1f, r.height);
            for (int i = 0; i < _glyphs.Count; i++)
            {
                var g = _glyphs[i];
                var p = g.rt.anchoredPosition + g.dir * g.speed * Time.unscaledDeltaTime;
                float pad = g.rt.sizeDelta.x;
                if (p.x > w + pad) p.x = -pad; else if (p.x < -pad) p.x = w + pad;
                if (p.y > h + pad) p.y = -pad; else if (p.y < -pad) p.y = h + pad;
                g.rt.anchoredPosition = p;
            }
        }
    }
}
