using System.Collections.Generic;
using UnityEngine;
using CircuitBreaker.Data;

namespace CircuitBreaker.Generation
{
    /// <summary>
    /// Reverse-insertion generator. An arrow is only placed if its exit ray crosses no arrow that is
    /// already on the board, so insertion order reversed is a valid solve order (solvable by construction).
    /// </summary>
    public static class LevelGenerator
    {
        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private const int PaletteSize = 5;

        /// <summary>`budgetMs` is ignored; it stays only so older callers compile. Generation must never depend on the clock.</summary>
        public static LevelData Generate(int levelIndex, int budgetMs = 0) => Generate(LevelSpec.Classic(levelIndex), budgetMs);

        /// <summary>
        /// Best-of-N with a FIXED N per board size, stopping early once the tier's depth target is met.
        /// The result depends only on the spec, never on device speed, so a level number is the same board on every phone
        /// (resume, Daily Run and achievements rely on that).
        /// </summary>
        public static LevelData Generate(LevelSpec spec, int budgetMs = 0)
        {
            var p = LevelCatalog.For(spec);
            int cells = p.width * p.height;
            int maxAttempts = cells <= 200 ? 14 : cells <= 450 ? 8 : cells <= 800 ? 5 : 3;
            LevelData best = null;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int seed = spec.index * 7919 + attempt * 104729 + 17 + spec.salt * 1000003;
                var candidate = BuildOnce(spec.index, p, seed);
                if (best == null || candidate.depth > best.depth) best = candidate;
                if (candidate.depth >= p.minDepth) break;
            }
            best.difficulty = (int)p.difficulty;
            return best;
        }

        private static LevelData BuildOnce(int levelIndex, LevelParams p, int seed)
        {
            var rng = new System.Random(seed);
            int w = p.width, h = p.height;
            var level = new LevelData { index = levelIndex, width = w, height = h, hearts = p.hearts, seed = seed };
            level.shape = p.shape;
            level.mask = ShapeLibrary.Build(level.shape, w, h);
            int maskCells = 0;
            foreach (bool m in level.mask) if (m) maskCells++;

            var occ = new int[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) occ[x, y] = -1;

            int target = Mathf.RoundToInt(maskCells * p.fill);
            int filled = 0;
            int maxAttempts = maskCells * 90;
            int nextId = 0;
            int stall = 0, relax = 0;

            for (int attempt = 0; attempt < maxAttempts && filled < target; attempt++)
            {
                // Inward-pointing arrows go in first (they end up deepest in the dependency chain);
                // the required corridor shrinks as the board fills or when placement stalls.
                float progress = filled / (float)target;
                int minRay = Mathf.Max(0, Mathf.RoundToInt((1f - progress) * (Mathf.Max(w, h) * 0.5f)) - relax);

                var start = new Vector2Int(rng.Next(w), rng.Next(h));
                if (!level.InShape(start.x, start.y) || occ[start.x, start.y] != -1) { stall++; continue; }

                int len = rng.Next(p.minLen, p.maxLen + 1);
                var path = Walk(rng, occ, level, start, len, p.maxBends);
                if (path == null) { stall++; continue; }

                if (!TryOrient(rng, occ, w, h, path, minRay, out Vector2Int dir))
                {
                    if (++stall % 250 == 0) relax++;
                    continue;
                }
                stall = 0;

                var def = new ArrowDef { id = nextId, cells = path, dir = dir, colorIndex = nextId % PaletteSize };
                foreach (var c in path) occ[c.x, c.y] = nextId;
                filled += path.Count;
                level.arrows.Add(def);
                nextId++;
            }

            // Re-id colours so neighbours rarely match.
            ColorBalance(level, rng);

            Solver.Solve(level, out level.depth);
            AssignMechanics(level, p, rng);
            Solver.Solve(level, out level.depth);
            return level;
        }

        private static List<Vector2Int> Walk(System.Random rng, int[,] occ, LevelData level, Vector2Int start, int len, int maxBends)
        {
            int w = level.width, h = level.height;
            var path = new List<Vector2Int> { start };
            var used = new HashSet<Vector2Int> { start };
            Vector2Int cur = start;
            Vector2Int heading = Dirs[rng.Next(4)];
            int bends = 0;

            for (int i = 1; i < len; i++)
            {
                var options = new List<Vector2Int>(4);
                // Straight-biased: try the current heading first most of the time.
                bool preferStraight = rng.NextDouble() < 0.62;
                if (preferStraight) options.Add(heading);
                var order = new List<Vector2Int>(Dirs);
                Shuffle(rng, order);
                foreach (var d in order) if (!options.Contains(d)) options.Add(d);

                bool moved = false;
                foreach (var d in options)
                {
                    var n = cur + d;
                    if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h) continue;
                    if (!level.InShape(n.x, n.y) || occ[n.x, n.y] != -1 || used.Contains(n)) continue;
                    bool isBend = i > 1 && d != heading;
                    if (isBend && bends >= maxBends) continue;
                    if (isBend) bends++;
                    path.Add(n); used.Add(n);
                    cur = n; heading = d; moved = true;
                    break;
                }
                if (!moved) break;
            }
            return path.Count >= 2 ? path : null;
        }

        private static bool TryOrient(System.Random rng, int[,] occ, int w, int h, List<Vector2Int> path, int minRay, out Vector2Int dir)
        {
            // Try both ends as the head, in random order. On success `path` is left oriented tail -> head.
            bool reverseFirst = rng.Next(2) == 0;
            for (int pass = 0; pass < 2; pass++)
            {
                bool reversed = (pass == 0) == reverseFirst;
                if (reversed) path.Reverse();

                var head = path[path.Count - 1];
                var neck = path[path.Count - 2];
                var forward = head - neck;

                // The arrowhead always points along its last body segment, so what you see is where it goes.
                if (RayLength(occ, w, h, path, head, forward) >= minRay) { dir = forward; return true; }
                if (reversed) path.Reverse(); // undo before trying the other end
            }
            dir = Vector2Int.zero;
            return false;
        }

        /// <summary>Cells between the head and the board edge, or -1 if the ray hits anything.</summary>
        private static int RayLength(int[,] occ, int w, int h, List<Vector2Int> own, Vector2Int head, Vector2Int d)
        {
            int len = 0;
            var p = head + d;
            while (p.x >= 0 && p.y >= 0 && p.x < w && p.y < h)
            {
                if (occ[p.x, p.y] != -1 || own.Contains(p)) return -1;
                len++;
                p += d;
            }
            return len;
        }

        private static void AssignMechanics(LevelData level, LevelParams p, System.Random rng)
        {
            if (level.arrows.Count < 6) return;

            // Ice: each candidate is kept only if the board stays solvable.
            if (p.iceChance > 0f)
            {
                foreach (var a in level.arrows)
                {
                    if (rng.NextDouble() > p.iceChance) continue;
                    a.kind = ArrowKind.Ice;
                    a.counter = rng.Next(1, 4);
                    if (!Solver.Solve(level, out _)) { a.kind = ArrowKind.Standard; a.counter = 0; }
                }
            }

            // Key / Lock pairs.
            for (int g = 1; g <= p.keyPairs; g++)
            {
                var standard = level.arrows.FindAll(a => a.kind == ArrowKind.Standard);
                if (standard.Count < 2) break;
                for (int tries = 0; tries < 12; tries++)
                {
                    var key = standard[rng.Next(standard.Count)];
                    var lck = standard[rng.Next(standard.Count)];
                    if (key == lck || key.kind != ArrowKind.Standard || lck.kind != ArrowKind.Standard) continue;
                    key.kind = ArrowKind.Key; key.counter = g;
                    lck.kind = ArrowKind.Locked; lck.counter = g;
                    if (Solver.Solve(level, out _)) break;
                    key.kind = ArrowKind.Standard; key.counter = 0;
                    lck.kind = ArrowKind.Standard; lck.counter = 0;
                }
            }
        }

        private static void ColorBalance(LevelData level, System.Random rng)
        {
            // Greedy: pick the palette colour least used among already-coloured neighbouring arrows.
            var colorAt = new Dictionary<Vector2Int, int>();
            foreach (var a in level.arrows)
            {
                var score = new int[PaletteSize];
                foreach (var c in a.cells)
                    foreach (var d in Dirs)
                        if (colorAt.TryGetValue(c + d, out int col)) score[col]++;
                int bestCol = 0, bestScore = int.MaxValue;
                int offset = rng.Next(PaletteSize);
                for (int i = 0; i < PaletteSize; i++)
                {
                    int col = (i + offset) % PaletteSize;
                    if (score[col] < bestScore) { bestScore = score[col]; bestCol = col; }
                }
                a.colorIndex = bestCol;
                foreach (var c in a.cells) colorAt[c] = bestCol;
            }
        }

        private static void Shuffle<T>(System.Random rng, List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
