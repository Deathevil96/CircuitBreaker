using System;
using System.Collections.Generic;
using UnityEngine;

namespace CircuitBreaker.UI
{
    /// <summary>
    /// Procedural line icons (one consistent stroke weight). Coordinates are 0..1 with y up.
    /// Every icon is a list of strokes; the sprite is rendered from distance to the nearest stroke.
    /// </summary>
    public static class Icons
    {
        private const int Size = 128;
        private const float Stroke = 0.075f;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private struct Seg { public float x1, y1, x2, y2; }

        public static Sprite Get(string name)
        {
            if (Cache.TryGetValue(name, out var s) && s != null) return s;
            var segs = new List<Seg>();
            Define(name, segs);
            return Cache[name] = Render(segs);
        }

        // ---------- stroke builders ----------
        private static void Line(List<Seg> l, float x1, float y1, float x2, float y2) => l.Add(new Seg { x1 = x1, y1 = y1, x2 = x2, y2 = y2 });

        private static void Poly(List<Seg> l, bool closed, params float[] p)
        {
            int n = p.Length / 2;
            for (int i = 0; i < n - 1; i++) Line(l, p[i * 2], p[i * 2 + 1], p[i * 2 + 2], p[i * 2 + 3]);
            if (closed) Line(l, p[(n - 1) * 2], p[(n - 1) * 2 + 1], p[0], p[1]);
        }

        private static void Arc(List<Seg> l, float cx, float cy, float r, float a0, float a1, int steps = 28)
        {
            float prevX = cx + Mathf.Cos(a0 * Mathf.Deg2Rad) * r, prevY = cy + Mathf.Sin(a0 * Mathf.Deg2Rad) * r;
            for (int i = 1; i <= steps; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad;
                float x = cx + Mathf.Cos(a) * r, y = cy + Mathf.Sin(a) * r;
                Line(l, prevX, prevY, x, y);
                prevX = x; prevY = y;
            }
        }

        private static void Rect(List<Seg> l, float x0, float y0, float x1, float y1) => Poly(l, true, x0, y0, x1, y0, x1, y1, x0, y1);

        private static void Define(string name, List<Seg> l)
        {
            switch (name)
            {
                case "back": Poly(l, false, 0.6f, 0.22f, 0.32f, 0.5f, 0.6f, 0.78f); break;
                case "next": Poly(l, false, 0.4f, 0.22f, 0.68f, 0.5f, 0.4f, 0.78f); break;
                case "close": Line(l, 0.26f, 0.26f, 0.74f, 0.74f); Line(l, 0.74f, 0.26f, 0.26f, 0.74f); break;
                case "check": Poly(l, false, 0.22f, 0.5f, 0.42f, 0.3f, 0.78f, 0.7f); break;
                case "plus": Line(l, 0.5f, 0.25f, 0.5f, 0.75f); Line(l, 0.25f, 0.5f, 0.75f, 0.5f); break;
                case "restart":
                {
                    Arc(l, 0.5f, 0.5f, 0.28f, 50f, 340f, 30);
                    float ang = 340f * Mathf.Deg2Rad;
                    float px = 0.5f + Mathf.Cos(ang) * 0.28f, py = 0.5f + Mathf.Sin(ang) * 0.28f;
                    float dx = -Mathf.Sin(ang), dy = Mathf.Cos(ang);      // direction of travel
                    float nx = Mathf.Cos(ang), ny = Mathf.Sin(ang);       // outward
                    float tx = px + dx * 0.05f, ty = py + dy * 0.05f;
                    Line(l, tx, ty, tx - dx * 0.16f + nx * 0.11f, ty - dy * 0.16f + ny * 0.11f);
                    Line(l, tx, ty, tx - dx * 0.16f - nx * 0.11f, ty - dy * 0.16f - ny * 0.11f);
                    break;
                }
                case "home":
                    Poly(l, false, 0.18f, 0.5f, 0.5f, 0.2f, 0.82f, 0.5f);
                    Poly(l, false, 0.27f, 0.44f, 0.27f, 0.8f, 0.73f, 0.8f, 0.73f, 0.44f);
                    break;
                case "menu": Line(l, 0.22f, 0.32f, 0.78f, 0.32f); Line(l, 0.22f, 0.5f, 0.78f, 0.5f); Line(l, 0.22f, 0.68f, 0.78f, 0.68f); break;
                case "play": Poly(l, true, 0.36f, 0.22f, 0.36f, 0.78f, 0.78f, 0.5f); break;
                case "levels":
                    Rect(l, 0.2f, 0.2f, 0.45f, 0.45f); Rect(l, 0.55f, 0.2f, 0.8f, 0.45f);
                    Rect(l, 0.2f, 0.55f, 0.45f, 0.8f); Rect(l, 0.55f, 0.55f, 0.8f, 0.8f);
                    break;
                case "records": Line(l, 0.2f, 0.2f, 0.8f, 0.2f); Line(l, 0.3f, 0.2f, 0.3f, 0.5f); Line(l, 0.5f, 0.2f, 0.5f, 0.78f); Line(l, 0.7f, 0.2f, 0.7f, 0.6f); break;
                case "settings":
                    Arc(l, 0.5f, 0.5f, 0.17f, 0f, 360f, 24);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Line(l, 0.5f + Mathf.Cos(a) * 0.27f, 0.5f + Mathf.Sin(a) * 0.27f, 0.5f + Mathf.Cos(a) * 0.36f, 0.5f + Mathf.Sin(a) * 0.36f);
                    }
                    Arc(l, 0.5f, 0.5f, 0.27f, 0f, 360f, 32);
                    break;
                case "trophy":
                    Poly(l, false, 0.3f, 0.8f, 0.3f, 0.6f, 0.38f, 0.45f, 0.5f, 0.42f, 0.62f, 0.45f, 0.7f, 0.6f, 0.7f, 0.8f);
                    Line(l, 0.3f, 0.8f, 0.7f, 0.8f);
                    Arc(l, 0.28f, 0.68f, 0.09f, 90f, 270f, 10); Arc(l, 0.72f, 0.68f, 0.09f, -90f, 90f, 10);
                    Line(l, 0.5f, 0.42f, 0.5f, 0.3f); Line(l, 0.36f, 0.24f, 0.64f, 0.24f); Line(l, 0.4f, 0.3f, 0.6f, 0.3f);
                    break;
                case "calendar":
                    Rect(l, 0.2f, 0.2f, 0.8f, 0.76f); Line(l, 0.2f, 0.6f, 0.8f, 0.6f);
                    Line(l, 0.36f, 0.68f, 0.36f, 0.84f); Line(l, 0.64f, 0.68f, 0.64f, 0.84f);
                    break;
                case "hint":
                    Arc(l, 0.5f, 0.58f, 0.22f, -55f, 235f, 26);
                    Line(l, 0.36f, 0.4f, 0.4f, 0.32f); Line(l, 0.64f, 0.4f, 0.6f, 0.32f);
                    Line(l, 0.4f, 0.32f, 0.6f, 0.32f); Line(l, 0.42f, 0.24f, 0.58f, 0.24f);
                    break;
                case "ruler":
                    Rect(l, 0.16f, 0.4f, 0.84f, 0.62f);
                    for (int i = 0; i < 5; i++) { float x = 0.28f + i * 0.14f; Line(l, x, 0.62f, x, i % 2 == 0 ? 0.5f : 0.54f); }
                    break;
                case "extract":
                    Poly(l, false, 0.2f, 0.45f, 0.2f, 0.2f, 0.8f, 0.2f, 0.8f, 0.45f);
                    Line(l, 0.5f, 0.3f, 0.5f, 0.82f); Poly(l, false, 0.34f, 0.66f, 0.5f, 0.82f, 0.66f, 0.66f);
                    break;
                case "fit":
                    Poly(l, false, 0.2f, 0.4f, 0.2f, 0.2f, 0.4f, 0.2f); Poly(l, false, 0.6f, 0.2f, 0.8f, 0.2f, 0.8f, 0.4f);
                    Poly(l, false, 0.8f, 0.6f, 0.8f, 0.8f, 0.6f, 0.8f); Poly(l, false, 0.4f, 0.8f, 0.2f, 0.8f, 0.2f, 0.6f);
                    break;
                case "lock":
                    Rect(l, 0.26f, 0.2f, 0.74f, 0.52f); Arc(l, 0.5f, 0.52f, 0.17f, 0f, 180f, 16);
                    break;
                case "coin": Arc(l, 0.5f, 0.5f, 0.3f, 0f, 360f, 32); Arc(l, 0.5f, 0.5f, 0.15f, 0f, 360f, 20); break;
                case "heart":
                {
                    float px = 0, py = 0; bool first = true;
                    for (int i = 0; i <= 40; i++)
                    {
                        float t = i / 40f * Mathf.PI * 2f;
                        float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                        float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2 * t) - 2f * Mathf.Cos(3 * t) - Mathf.Cos(4 * t);
                        x = 0.5f + x / 38f; y = 0.5f + y / 38f + 0.02f;
                        if (!first) Line(l, px, py, x, y);
                        px = x; py = y; first = false;
                    }
                    break;
                }
                case "flame":
                    Poly(l, false, 0.5f, 0.82f, 0.62f, 0.62f, 0.72f, 0.46f, 0.68f, 0.3f, 0.56f, 0.2f, 0.44f, 0.2f, 0.32f, 0.3f, 0.28f, 0.46f, 0.36f, 0.58f, 0.44f, 0.5f, 0.46f, 0.64f, 0.5f, 0.82f);
                    break;
                case "star":
                {
                    float px = 0, py = 0;
                    for (int i = 0; i <= 10; i++)
                    {
                        float r = i % 2 == 0 ? 0.34f : 0.15f, a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                        float x = 0.5f + Mathf.Cos(a) * r, y = 0.5f + Mathf.Sin(a) * r - 0.02f;
                        if (i > 0) Line(l, px, py, x, y);
                        px = x; py = y;
                    }
                    break;
                }
                case "freeplay":
                {
                    float px = 0, py = 0;
                    for (int i = 0; i <= 48; i++)
                    {
                        float t = i / 48f * Mathf.PI * 2f, d = 1f + Mathf.Sin(t) * Mathf.Sin(t);
                        float x = 0.5f + 0.34f * Mathf.Cos(t) / d, y = 0.5f + 0.34f * Mathf.Sin(t) * Mathf.Cos(t) / d * 1.9f;
                        if (i > 0) Line(l, px, py, x, y);
                        px = x; py = y;
                    }
                    break;
                }
                case "overload": Poly(l, false, 0.56f, 0.84f, 0.3f, 0.5f, 0.5f, 0.5f, 0.42f, 0.16f, 0.7f, 0.56f, 0.5f, 0.56f, 0.56f, 0.84f); break;
                case "sound":
                    Poly(l, true, 0.2f, 0.4f, 0.34f, 0.4f, 0.5f, 0.26f, 0.5f, 0.74f, 0.34f, 0.6f, 0.2f, 0.6f);
                    Arc(l, 0.5f, 0.5f, 0.2f, -40f, 40f, 8); Arc(l, 0.5f, 0.5f, 0.32f, -40f, 40f, 8);
                    break;
                case "info": Arc(l, 0.5f, 0.5f, 0.3f, 0f, 360f, 32); Line(l, 0.5f, 0.28f, 0.5f, 0.28f); Line(l, 0.5f, 0.42f, 0.5f, 0.7f); break;
                case "medal": Arc(l, 0.5f, 0.38f, 0.2f, 0f, 360f, 24); Line(l, 0.4f, 0.55f, 0.3f, 0.82f); Line(l, 0.6f, 0.55f, 0.7f, 0.82f); break;
                case "clock": Arc(l, 0.5f, 0.5f, 0.3f, 0f, 360f, 32); Line(l, 0.5f, 0.5f, 0.5f, 0.7f); Line(l, 0.5f, 0.5f, 0.64f, 0.42f); break;
                case "ad": Rect(l, 0.18f, 0.3f, 0.82f, 0.7f); Poly(l, true, 0.44f, 0.4f, 0.44f, 0.6f, 0.62f, 0.5f); break;
                default: Arc(l, 0.5f, 0.5f, 0.3f, 0f, 360f, 24); break;
            }
        }

        private static Sprite Render(List<Seg> segs)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[Size * Size];
            float half = Stroke * 0.5f, aa = 1.2f / Size;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float fx = (x + 0.5f) / Size, fy = (y + 0.5f) / Size;
                    float d = float.MaxValue;
                    foreach (var s in segs)
                    {
                        float dx = s.x2 - s.x1, dy = s.y2 - s.y1;
                        float len2 = dx * dx + dy * dy;
                        float t = len2 < 1e-8f ? 0f : Mathf.Clamp01(((fx - s.x1) * dx + (fy - s.y1) * dy) / len2);
                        float cx = s.x1 + dx * t - fx, cy = s.y1 + dy * t - fy;
                        float dd = cx * cx + cy * cy;
                        if (dd < d) d = dd;
                    }
                    d = Mathf.Sqrt(d);
                    px[y * Size + x] = new Color(1, 1, 1, Mathf.Clamp01((half - d) / aa + 0.5f));
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
