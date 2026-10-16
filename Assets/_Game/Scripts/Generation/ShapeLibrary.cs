using System;
using System.Collections.Generic;
using UnityEngine;

namespace CircuitBreaker.Generation
{
    /// <summary>
    /// Playable-area silhouettes, drawn as unions of simple primitives in a [-1,1] square (v up).
    /// A level is a bitmap of cells inside one of these shapes.
    /// </summary>
    public static class ShapeLibrary
    {
        /// <summary>Levels before this are plain rectangles; from here on boards use the silhouettes.</summary>
        public const int ShapeStartLevel = 21;

        // Order matters: it is the order players meet them. Variety first (animal, object, human, geometry).
        public static readonly string[] Names =
        {
            "Heart", "Cat", "Star", "Fish", "Human", "Moon", "Butterfly", "House", "Rabbit", "Rocket",
            "Circle", "Owl", "Tree", "Crown", "Turtle", "Key", "Bird", "Flower", "Diamond", "Penguin",
            "Sailboat", "Paw", "Sun", "Mushroom", "Plus", "Bolt", "Hexagon", "Ring", "Triangle"
        };

        private delegate bool Shape(float u, float v);
        private static readonly Dictionary<string, Shape> Shapes = BuildShapes();
        private static readonly Dictionary<string, float> Coverage = new Dictionary<string, float>();

        public static bool IsShaped(int level) => level >= ShapeStartLevel;

        public static string NameFor(int level) =>
            level < ShapeStartLevel ? "Rectangle" : Names[(level - ShapeStartLevel) % Names.Length];

        public struct ShapeInfo { public float u0, u1, v0, v1, aspect, coverage; }
        private static readonly Dictionary<string, ShapeInfo> Infos = new Dictionary<string, ShapeInfo>();

        /// <summary>Tight bounding box, aspect ratio (width / height) and how much of that box the shape fills.</summary>
        public static ShapeInfo Info(string name)
        {
            lock (Infos)
            {
                if (Infos.TryGetValue(name, out var cached)) return cached;
                return Infos[name] = ComputeInfo(name);
            }
        }

        private static ShapeInfo ComputeInfo(string name)
        {
            var info = new ShapeInfo { u0 = -1, u1 = 1, v0 = -1, v1 = 1, aspect = 1, coverage = 1 };
            if (name != "Rectangle")
            {
                const int n = 160;
                float minU = 9, maxU = -9, minV = 9, maxV = -9;
                for (int x = 0; x < n; x++)
                    for (int y = 0; y < n; y++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        if (!Inside(name, u, v)) continue;
                        minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
                        minV = Mathf.Min(minV, v); maxV = Mathf.Max(maxV, v);
                    }
                float pad = 2f / n;
                info.u0 = minU - pad; info.u1 = maxU + pad; info.v0 = minV - pad; info.v1 = maxV + pad;
                info.aspect = (info.u1 - info.u0) / (info.v1 - info.v0);
                int inside = 0;
                for (int x = 0; x < n; x++)
                    for (int y = 0; y < n; y++)
                    {
                        float u = Mathf.Lerp(info.u0, info.u1, (x + 0.5f) / n), v = Mathf.Lerp(info.v0, info.v1, (y + 0.5f) / n);
                        if (Inside(name, u, v)) inside++;
                    }
                info.coverage = inside / (float)(n * n);
            }
            return info;
        }

        public static float CoverageOf(string name) => Info(name).coverage;

        /// <summary>Flattened mask, index = x * height + y. The shape is stretched to fill the whole grid.</summary>
        public static bool[] Build(string name, int w, int h)
        {
            var info = Info(name);
            var mask = new bool[w * h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    float u = Mathf.Lerp(info.u0, info.u1, (x + 0.5f) / w);
                    float v = Mathf.Lerp(info.v0, info.v1, (y + 0.5f) / h);
                    mask[x * h + y] = Inside(name, u, v);
                }
            return mask;
        }

        /// <summary>Text preview (for tests and design review).</summary>
        public static string Ascii(string name, int n = 28)
        {
            var info = Info(name);
            int w = Mathf.Max(6, Mathf.RoundToInt(n * Mathf.Sqrt(info.aspect))), h = Mathf.Max(6, Mathf.RoundToInt(n / Mathf.Sqrt(info.aspect)));
            var mask = Build(name, w, h);
            var sb = new System.Text.StringBuilder();
            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = 0; x < w; x++) sb.Append(mask[x * h + y] ? "#" : ".");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static bool Inside(string name, float u, float v) => Shapes.TryGetValue(name, out var s) ? s(u, v) : true;

        // ---------- primitives ----------
        private static bool Ell(float u, float v, float cx, float cy, float rx, float ry, float rot = 0f)
        {
            float dx = u - cx, dy = v - cy;
            if (rot != 0f)
            {
                float c = Mathf.Cos(rot), s = Mathf.Sin(rot);
                float x = dx * c + dy * s, y = -dx * s + dy * c;
                dx = x; dy = y;
            }
            return (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry) <= 1f;
        }

        private static bool Box(float u, float v, float cx, float cy, float hx, float hy) =>
            Mathf.Abs(u - cx) <= hx && Mathf.Abs(v - cy) <= hy;

        private static bool Cap(float u, float v, float x1, float y1, float x2, float y2, float r)
        {
            float px = u - x1, py = v - y1, dx = x2 - x1, dy = y2 - y1;
            float t = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
            float cx = x1 + dx * t - u, cy = y1 + dy * t - v;
            return cx * cx + cy * cy <= r * r;
        }

        private static bool Poly(float u, float v, params float[] xy)
        {
            bool inside = false;
            int n = xy.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                if ((yi > v) != (yj > v) && u < (xj - xi) * (v - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        private static float[] Regular(int n, float radius, float rot = 0f)
        {
            var p = new float[n * 2];
            for (int i = 0; i < n; i++)
            {
                float a = rot + i * 2f * Mathf.PI / n;
                p[i * 2] = Mathf.Cos(a) * radius; p[i * 2 + 1] = Mathf.Sin(a) * radius;
            }
            return p;
        }

        private static float[] StarPts(int points, float outer, float inner, float yShift)
        {
            var p = new float[points * 4];
            for (int i = 0; i < points * 2; i++)
            {
                float r = i % 2 == 0 ? outer : inner;
                float a = Mathf.PI / 2f + i * Mathf.PI / points;
                p[i * 2] = Mathf.Cos(a) * r; p[i * 2 + 1] = Mathf.Sin(a) * r + yShift;
            }
            return p;
        }

        private static readonly float[] StarPoly = StarPts(5, 1f, 0.5f, -0.05f);
        private static readonly float[] HexPoly = Regular(6, 1f);

        private static Dictionary<string, Shape> BuildShapes()
        {
            var d = new Dictionary<string, Shape>();

            // ----- geometry -----
            d["Heart"] = (u, v) => { float x = u * 1.2f, y = v * 1.2f + 0.12f, a = x * x + y * y - 1f; return a * a * a - x * x * y * y * y <= 0f; };
            d["Circle"] = (u, v) => u * u + v * v <= 0.98f;
            d["Ring"] = (u, v) => { float r2 = u * u + v * v; return r2 <= 0.98f && r2 >= 0.22f; };
            d["Diamond"] = (u, v) => Mathf.Abs(u) + Mathf.Abs(v) <= 1.02f;
            d["Plus"] = (u, v) => Mathf.Abs(u) <= 0.36f || Mathf.Abs(v) <= 0.36f;
            d["Star"] = (u, v) => Poly(u, v, StarPoly);
            d["Hexagon"] = (u, v) => Poly(u, v, HexPoly);
            d["Triangle"] = (u, v) => Poly(u, v, 0f, 1f, 1f, -0.9f, -1f, -0.9f);
            d["House"] = (u, v) => Poly(u, v, -0.9f, -0.95f, 0.9f, -0.95f, 0.9f, 0.1f, 0f, 1f, -0.9f, 0.1f);
            d["Bolt"] = (u, v) => Poly(u, v, 0.2f, 1f, -0.55f, -0.05f, -0.05f, -0.05f, -0.3f, -1f, 0.6f, 0.2f, 0.08f, 0.2f, 0.55f, 1f);

            // ----- animals -----
            d["Cat"] = (u, v) => Ell(u, v, 0, -0.12f, 0.85f, 0.72f)
                              || Poly(u, v, -0.88f, 0.15f, -0.72f, 0.98f, -0.15f, 0.55f)
                              || Poly(u, v, 0.88f, 0.15f, 0.72f, 0.98f, 0.15f, 0.55f);
            d["Fish"] = (u, v) => Ell(u, v, -0.15f, 0, 0.8f, 0.5f)
                               || Poly(u, v, 0.5f, 0f, 1f, 0.55f, 0.9f, 0f, 1f, -0.55f)
                               || Poly(u, v, -0.15f, 0.4f, 0.05f, 0.8f, 0.2f, 0.38f);
            d["Bird"] = (u, v) => Ell(u, v, -0.05f, -0.1f, 0.65f, 0.42f)
                               || Ell(u, v, 0.55f, 0.35f, 0.3f, 0.3f)
                               || Poly(u, v, 0.8f, 0.4f, 1f, 0.3f, 0.8f, 0.2f)
                               || Poly(u, v, -0.55f, -0.05f, -1f, 0.3f, -0.95f, -0.35f)
                               || Cap(u, v, 0.0f, -0.45f, -0.1f, -0.9f, 0.07f) || Cap(u, v, 0.25f, -0.4f, 0.3f, -0.9f, 0.07f);
            d["Butterfly"] = (u, v) => Ell(u, v, -0.5f, 0.38f, 0.5f, 0.5f, 0.3f) || Ell(u, v, 0.5f, 0.38f, 0.5f, 0.5f, -0.3f)
                                    || Ell(u, v, -0.4f, -0.45f, 0.36f, 0.42f, -0.3f) || Ell(u, v, 0.4f, -0.45f, 0.36f, 0.42f, 0.3f)
                                    || Box(u, v, 0, -0.05f, 0.1f, 0.75f);
            d["Rabbit"] = (u, v) => Ell(u, v, 0, -0.4f, 0.55f, 0.55f) || Ell(u, v, -0.25f, 0.5f, 0.17f, 0.5f, 0.12f)
                                 || Ell(u, v, 0.25f, 0.5f, 0.17f, 0.5f, -0.12f);
            d["Owl"] = (u, v) => Ell(u, v, 0, -0.08f, 0.7f, 0.85f) || Poly(u, v, -0.7f, 0.5f, -0.6f, 1f, -0.2f, 0.78f)
                              || Poly(u, v, 0.7f, 0.5f, 0.6f, 1f, 0.2f, 0.78f);
            d["Turtle"] = (u, v) => Ell(u, v, 0, -0.05f, 0.62f, 0.55f) || Ell(u, v, 0, 0.78f, 0.2f, 0.2f)
                                 || Ell(u, v, -0.62f, 0.42f, 0.2f, 0.2f) || Ell(u, v, 0.62f, 0.42f, 0.2f, 0.2f)
                                 || Ell(u, v, -0.55f, -0.58f, 0.2f, 0.2f) || Ell(u, v, 0.55f, -0.58f, 0.2f, 0.2f)
                                 || Poly(u, v, -0.12f, -0.55f, 0.12f, -0.55f, 0f, -0.95f);
            d["Penguin"] = (u, v) => Ell(u, v, 0, -0.1f, 0.55f, 0.85f) || Ell(u, v, 0, 0.62f, 0.34f, 0.34f)
                                  || Ell(u, v, -0.62f, -0.1f, 0.14f, 0.4f, 0.3f) || Ell(u, v, 0.62f, -0.1f, 0.14f, 0.4f, -0.3f);
            d["Paw"] = (u, v) => Ell(u, v, 0, -0.28f, 0.56f, 0.5f) || Ell(u, v, -0.72f, 0.06f, 0.22f, 0.3f, -0.3f)
                              || Ell(u, v, -0.28f, 0.44f, 0.2f, 0.3f, -0.1f) || Ell(u, v, 0.28f, 0.44f, 0.2f, 0.3f, 0.1f)
                              || Ell(u, v, 0.72f, 0.06f, 0.22f, 0.3f, 0.3f);

            // ----- people -----
            d["Human"] = (u, v) => Ell(u, v, 0, 0.8f, 0.2f, 0.2f)
                                || Box(u, v, 0, 0.22f, 0.27f, 0.4f)
                                || Cap(u, v, -0.3f, 0.5f, -0.72f, -0.15f, 0.11f) || Cap(u, v, 0.3f, 0.5f, 0.72f, -0.15f, 0.11f)
                                || Cap(u, v, -0.13f, -0.15f, -0.22f, -0.92f, 0.13f) || Cap(u, v, 0.13f, -0.15f, 0.22f, -0.92f, 0.13f);

            // ----- objects / nature -----
            d["Moon"] = (u, v) => Ell(u, v, 0, 0, 0.92f, 0.92f) && !Ell(u, v, 0.42f, 0.14f, 0.76f, 0.76f);
            d["Rocket"] = (u, v) => Ell(u, v, 0, 0.12f, 0.36f, 0.82f)
                                 || Poly(u, v, -0.34f, -0.25f, -0.85f, -0.85f, -0.2f, -0.55f)
                                 || Poly(u, v, 0.34f, -0.25f, 0.85f, -0.85f, 0.2f, -0.55f) || Box(u, v, 0, -0.78f, 0.16f, 0.14f);
            d["Tree"] = (u, v) => Ell(u, v, 0, 0.32f, 0.68f, 0.62f) || Ell(u, v, -0.4f, 0.0f, 0.4f, 0.36f) || Ell(u, v, 0.4f, 0.0f, 0.4f, 0.36f)
                               || Box(u, v, 0, -0.62f, 0.14f, 0.4f);
            d["Crown"] = (u, v) => Poly(u, v, -0.9f, -0.6f, 0.9f, -0.6f, 0.9f, 0.25f, 0.45f, -0.15f, 0f, 0.75f, -0.45f, -0.15f, -0.9f, 0.25f);
            d["Key"] = (u, v) => (Ell(u, v, -0.55f, 0, 0.42f, 0.42f) && !Ell(u, v, -0.55f, 0, 0.17f, 0.17f))
                              || Box(u, v, 0.22f, 0, 0.62f, 0.1f) || Box(u, v, 0.7f, -0.22f, 0.09f, 0.2f) || Box(u, v, 0.45f, -0.18f, 0.09f, 0.15f);
            d["Flower"] = (u, v) =>
            {
                if (Ell(u, v, 0, 0, 0.26f, 0.26f)) return true;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f + 0.5f;
                    if (Ell(u, v, Mathf.Cos(a) * 0.55f, Mathf.Sin(a) * 0.55f, 0.32f, 0.32f)) return true;
                }
                return false;
            };
            d["Sun"] = (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                float ray = Mathf.Cos(a * 8f) > 0.35f ? 0.34f : 0f;
                return r <= 0.52f + ray;
            };
            d["Mushroom"] = (u, v) => (v >= -0.12f && Ell(u, v, 0, -0.12f, 0.95f, 0.85f)) || Box(u, v, 0, -0.56f, 0.28f, 0.4f);
            d["Sailboat"] = (u, v) => Poly(u, v, -0.95f, -0.28f, 0.95f, -0.28f, 0.62f, -0.9f, -0.62f, -0.9f)
                                   || Poly(u, v, -0.05f, -0.34f, -0.05f, 0.95f, 0.78f, -0.34f)
                                   || Poly(u, v, -0.15f, -0.34f, -0.15f, 0.55f, -0.8f, -0.34f);
            return d;
        }
    }
}
