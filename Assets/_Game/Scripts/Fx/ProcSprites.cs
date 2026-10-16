using UnityEngine;

namespace CircuitBreaker.Fx
{
    /// <summary>Procedurally generated sprites and textures so the game needs no imported art.</summary>
    public static class ProcSprites
    {
        private static Sprite _arrowGlyph;
        private static Sprite _circle, _ring, _triangle, _heart, _pixel, _rounded, _lock, _glowDot;
        private static Texture2D _dash;
        private static Material _lineMat;

        /// <summary>One shared unlit material for every line in the game (vertex colours carry the tint), so nothing leaks per level.</summary>
        public static Material LineMaterial => _lineMat != null ? _lineMat : (_lineMat = new Material(Shader.Find("Sprites/Default")));

        /// <summary>A thin straight arrow pointing right (shaft plus head), soft edges. Used for decoration.</summary>
        public static Sprite ArrowGlyph => _arrowGlyph ??= Make(128, (x, y, n) =>
        {
            float u = x / (n - 1f), v = y / (n - 1f);
            float shaft = Mathf.Clamp01((0.022f - Mathf.Abs(v - 0.5f)) * n * 0.6f) * Mathf.Clamp01((u - 0.05f) * n * 0.5f) * Mathf.Clamp01((0.74f - u) * n * 0.5f);
            float head = Mathf.Clamp01(((0.96f - u) * 0.5f - Mathf.Abs(v - 0.5f) * 0.5f) * n * 0.6f) * Mathf.Clamp01((u - 0.70f) * n * 0.5f);
            return Mathf.Max(shaft, head);
        });

        public static Sprite Pixel => _pixel ??= Make(4, (x, y, n) => 1f, 4);
        public static Sprite Circle => _circle ??= Make(64, (x, y, n) => Smooth(Dist(x, y, n), 0.5f));
        public static Sprite GlowDot => _glowDot ??= Make(64, (x, y, n) => Mathf.Pow(Mathf.Clamp01(1f - Dist(x, y, n) / 0.5f), 2f));
        public static Sprite Ring => _ring ??= Make(64, (x, y, n) =>
        {
            float d = Dist(x, y, n);
            return Smooth(d, 0.5f) * (1f - Smooth(d, 0.36f));
        });

        // Chevron-ish head pointing +Y.
        public static Sprite Triangle => _triangle ??= Make(64, (x, y, n) =>
        {
            float u = x / (n - 1f) - 0.5f;   // -0.5..0.5
            float v = y / (n - 1f);          // 0..1
            float halfWidth = (1f - v) * 0.5f;
            float edge = halfWidth - Mathf.Abs(u);
            return Mathf.Clamp01(edge * n * 0.5f) * Mathf.Clamp01(v * n * 0.5f);
        });

        public static Sprite Heart => _heart ??= Make(64, (x, y, n) =>
        {
            float px = (x / (n - 1f) - 0.5f) * 2.5f;
            float py = (y / (n - 1f) - 0.45f) * 2.5f;
            float a = px * px + py * py - 1f;
            float f = a * a * a - px * px * py * py * py;
            return Mathf.Clamp01(0.5f - f * 6f);
        });

        public static Sprite Lock => _lock ??= Make(64, (x, y, n) =>
        {
            float u = x / (n - 1f), v = y / (n - 1f);
            bool body = u > 0.15f && u < 0.85f && v > 0.08f && v < 0.55f;
            float dx = u - 0.5f, dy = v - 0.55f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            bool shackle = r < 0.3f && r > 0.18f && v > 0.5f;
            return (body || shackle) ? 1f : 0f;
        });

        /// <summary>9-sliceable rounded rectangle (border = radius).</summary>
        public static Sprite Rounded => _rounded ??= MakeRounded(64, 22);

        /// <summary>Tiling "packet" texture: one rounded block per unit of line length.</summary>
        public static Texture2D DashTexture
        {
            get
            {
                if (_dash != null) return _dash;
                const int w = 64;
                _dash = new Texture2D(w, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                for (int x = 0; x < w; x++)
                {
                    float u = x / (w - 1f);
                    float a = Mathf.Clamp01(Mathf.Min(u - 0.09f, 0.91f - u) * 40f);
                    for (int y = 0; y < 4; y++) _dash.SetPixel(x, y, new Color(1, 1, 1, a));
                }
                _dash.Apply();
                return _dash;
            }
        }

        private static float Dist(int x, int y, int n)
        {
            float dx = x / (n - 1f) - 0.5f, dy = y / (n - 1f) - 0.5f;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
        private static float Smooth(float d, float r) => Mathf.Clamp01((r - d) * 64f);

        private delegate float Shader2D(int x, int y, int n);

        private static Sprite Make(int n, Shader2D f, int texSize = 0)
        {
            int size = texSize > 0 ? texSize : n;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(f(x, y, size)));
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite MakeRounded(int n, int radius)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x, radius, n - 1 - radius);
                    float cy = Mathf.Clamp(y, radius, n - 1 - radius);
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
