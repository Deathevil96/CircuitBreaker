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
            int maxAttempts = cells <= 200 ? 24 : cells <= 450 ? 10 : cells <= 800 ? 5 : 3;
            LevelData best = null;
            int bestScore = int.MinValue;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int seed = spec.index * 7919 + attempt * 104729 + 17 + spec.salt * 1000003;
                var candidate = BuildOnce(spec.index, p, seed);
                // Fewest missed targets wins (depth misses count triple); beyond the targets, deeper and more wrapped is a small bonus.
                int score = -(3 * Mathf.Max(0, p.minDepth - candidate.depth) + Mathf.Max(0, p.minWraps - candidate.wraps)) * 100 + candidate.depth + candidate.wraps;
                if (best == null || score > bestScore) { best = candidate; bestScore = score; }
                if (candidate.depth >= p.minDepth && candidate.wraps >= p.minWraps) break;
            }
            best.difficulty = (int)p.difficulty;
            return best;
        }

        private static LevelData BuildOnce(int levelIndex, LevelParams p, int seed)
        {
            // A tiling can occasionally fail (a lone cell that no neighbour can take); a different walk order fixes it.
            for (int retry = 0; retry < 40; retry++)
            {
                var full = BuildTiled(levelIndex, p, unchecked(seed + retry * 15485863));
                if (full != null) return full;
            }
            return BuildInserted(levelIndex, p, seed);      // the old generator (may leave gaps) is only a last-resort safety net
        }

        // =====================================================================
        // Full tiling: every playable cell belongs to an arrow, no gaps at all.
        // =====================================================================

        private static LevelData BuildTiled(int levelIndex, LevelParams p, int seed)
        {
            var rng = new System.Random(seed);
            int w = p.width, h = p.height;
            var level = new LevelData { index = levelIndex, width = w, height = h, hearts = p.hearts, seed = seed };
            level.shape = p.shape;
            level.mask = ShapeLibrary.Build(level.shape, w, h);
            DropIsolatedCells(level);

            var paths = Tile(rng, level, p);
            if (paths == null) return null;
            if (!Orient(level, paths)) return null;

            ColorBalance(level, rng);
            Solver.Solve(level, out level.depth);
            AssignMechanics(level, p, rng);
            Solver.Solve(level, out level.depth);
            level.wraps = CountWraps(level);
            return level;
        }

        /// <summary>A playable cell with no playable neighbour cannot hold an arrow (they need two cells), so it is not part of the board.</summary>
        private static void DropIsolatedCells(LevelData level)
        {
            if (level.mask == null) return;
            int w = level.width, h = level.height;
            var drop = new List<int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (!level.InShape(x, y)) continue;
                    bool any = false;
                    foreach (var d in Dirs)
                    {
                        int nx = x + d.x, ny = y + d.y;
                        if (nx >= 0 && ny >= 0 && nx < w && ny < h && level.InShape(nx, ny)) { any = true; break; }
                    }
                    if (!any) drop.Add(x * h + y);
                }
            foreach (int i in drop) level.mask[i] = false;
        }

        /// <summary>Covers every playable cell with simple paths of at least two cells. Returns null if it cannot.</summary>
        private static List<List<Vector2Int>> Tile(System.Random rng, LevelData level, LevelParams p)
        {
            int w = level.width, h = level.height;
            var cover = new int[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) cover[x, y] = -1;
            var paths = new List<List<Vector2Int>>();

            int Free(Vector2Int c)
            {
                int n = 0;
                foreach (var d in Dirs)
                {
                    var q = c + d;
                    if (q.x >= 0 && q.y >= 0 && q.x < w && q.y < h && level.InShape(q.x, q.y) && cover[q.x, q.y] == -1) n++;
                }
                return n;
            }

            // Walks start at the most cramped uncovered cell and always step to the most cramped neighbour,
            // so corners and dead ends are used up first and arrows end up hugging and coiling round each other.
            var ties = new List<Vector2Int>();
            while (true)
            {
                int bestFree = int.MaxValue;
                ties.Clear();
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        if (!level.InShape(x, y) || cover[x, y] != -1) continue;
                        var c = new Vector2Int(x, y);
                        int f = Free(c);
                        if (f == 0) continue;
                        if (f < bestFree) { bestFree = f; ties.Clear(); }
                        if (f == bestFree) ties.Add(c);
                    }
                if (ties.Count == 0) break;

                var start = ties[rng.Next(ties.Count)];
                int id = paths.Count;
                var path = new List<Vector2Int> { start };
                cover[start.x, start.y] = id;
                int target = rng.Next(p.minLen, p.maxLen + 1);
                int bends = 0;
                Vector2Int heading = Vector2Int.zero;
                var options = new List<Vector2Int>(4);
                while (path.Count < target)
                {
                    var cur = path[path.Count - 1];
                    options.Clear();
                    foreach (var d in Dirs)
                    {
                        var q = cur + d;
                        if (q.x < 0 || q.y < 0 || q.x >= w || q.y >= h || !level.InShape(q.x, q.y) || cover[q.x, q.y] != -1) continue;
                        bool bend = path.Count >= 2 && d != heading;
                        if (bend && bends >= p.maxBends) continue;
                        options.Add(d);
                    }
                    if (options.Count == 0) break;

                    Vector2Int pick;
                    double roll = rng.NextDouble();
                    if (heading != Vector2Int.zero && options.Contains(heading) && roll < p.straight) pick = heading;
                    else if (rng.NextDouble() < p.hug)
                    {
                        int best = int.MaxValue; pick = options[0];
                        foreach (var d in options) { int f = Free(cur + d); if (f < best) { best = f; pick = d; } }
                    }
                    else pick = options[rng.Next(options.Count)];

                    if (path.Count >= 2 && pick != heading) bends++;
                    heading = pick;
                    var next = cur + pick;
                    path.Add(next);
                    cover[next.x, next.y] = id;
                }
                paths.Add(path);
            }

            // Cells nobody could reach (no free neighbour left): join them to a neighbouring arrow.
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (!level.InShape(x, y) || cover[x, y] != -1) continue;
                    if (!AbsorbSingleton(rng, level, paths, cover, new Vector2Int(x, y))) return null;
                }
            return paths;
        }

        private static bool AbsorbSingleton(System.Random rng, LevelData level, List<List<Vector2Int>> paths, int[,] cover, Vector2Int s)
        {
            var order = new List<Vector2Int>(Dirs);
            Shuffle(rng, order);
            foreach (var d in order)
            {
                var n = s + d;
                if (n.x < 0 || n.y < 0 || n.x >= level.width || n.y >= level.height || cover[n.x, n.y] == -1) continue;
                int pi = cover[n.x, n.y];
                var P = paths[pi];
                int idx = P.IndexOf(n);
                if (idx == 0) P.Insert(0, s);
                else if (idx == P.Count - 1) P.Add(s);
                else if (P.Count - 1 - idx >= 2)
                {
                    var tail = P.GetRange(idx + 1, P.Count - idx - 1);
                    P.RemoveRange(idx + 1, tail.Count);
                    P.Add(s);
                    paths.Add(tail);
                }
                else if (idx >= 2)
                {
                    var second = new List<Vector2Int> { s };
                    second.AddRange(P.GetRange(idx, P.Count - idx));
                    P.RemoveRange(idx, P.Count - idx);
                    paths.Add(second);
                }
                else continue;
                for (int i = 0; i < paths.Count; i++) foreach (var c in paths[i]) cover[c.x, c.y] = i;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Chooses each path's head so the board is solvable: repeatedly takes every path that has an end with a clear
        /// ray (exactly the solver's waves), picking the end with the longest ray. If nothing can leave, the longest
        /// remaining path is split in two, which gives it new ends. Returns false only if nothing can be split.
        /// </summary>
        private static bool Orient(LevelData level, List<List<Vector2Int>> paths)
        {
            int w = level.width, h = level.height;
            var occ = new int[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) occ[x, y] = -1;
            for (int i = 0; i < paths.Count; i++) foreach (var c in paths[i]) occ[c.x, c.y] = i;

            var alive = new List<int>();
            for (int i = 0; i < paths.Count; i++) alive.Add(i);
            var removal = new List<ArrowDef>();
            var wave = new List<int>();
            var waveReverse = new List<bool>();

            int guard = 0;
            while (alive.Count > 0 && guard++ < 100000)
            {
                wave.Clear(); waveReverse.Clear();
                foreach (int i in alive)
                {
                    var P = paths[i];
                    int bestLen = -1; bool bestReverse = false;
                    for (int end = 0; end < 2; end++)
                    {
                        bool reversed = end == 1;
                        var head = reversed ? P[0] : P[P.Count - 1];
                        var neck = reversed ? P[1] : P[P.Count - 2];
                        var d = head - neck;
                        int dist = RayDistance(occ, w, h, head, d);
                        if (dist >= 0 && dist > bestLen) { bestLen = dist; bestReverse = reversed; }
                    }
                    if (bestLen >= 0) { wave.Add(i); waveReverse.Add(bestReverse); }
                }

                if (wave.Count == 0)
                {
                    int pick = -1;
                    foreach (int i in alive) if (paths[i].Count >= 4 && (pick == -1 || paths[i].Count > paths[pick].Count)) pick = i;
                    if (pick == -1) return false;
                    var P = paths[pick];
                    int cut = P.Count / 2;
                    var second = P.GetRange(cut, P.Count - cut);
                    P.RemoveRange(cut, P.Count - cut);
                    int ni = paths.Count;
                    paths.Add(second);
                    foreach (var c in second) occ[c.x, c.y] = ni;
                    alive.Add(ni);
                    continue;
                }

                for (int k = 0; k < wave.Count; k++)
                {
                    int i = wave[k];
                    var P = paths[i];
                    if (waveReverse[k]) P.Reverse();
                    foreach (var c in P) occ[c.x, c.y] = -1;
                    alive.Remove(i);
                    removal.Add(new ArrowDef { cells = P, dir = P[P.Count - 1] - P[P.Count - 2] });
                }
            }

            // Ids run in reverse solve order, like the original generator (the last to leave comes first).
            removal.Reverse();
            for (int i = 0; i < removal.Count; i++)
            {
                removal[i].id = i;
                removal[i].colorIndex = i % PaletteSize;
                level.arrows.Add(removal[i]);
            }
            return true;
        }

        /// <summary>Cells from the head to the board edge along d, or -1 if anything still on the board is in the way.</summary>
        private static int RayDistance(int[,] occ, int w, int h, Vector2Int head, Vector2Int d)
        {
            int n = 0;
            var q = head + d;
            while (q.x >= 0 && q.y >= 0 && q.x < w && q.y < h)
            {
                if (occ[q.x, q.y] != -1) return -1;
                n++;
                q += d;
            }
            return n;
        }

        // =====================================================================
        // The original insertion generator (kept as a safety net; may leave gaps).
        // =====================================================================

        private static LevelData BuildInserted(int levelIndex, LevelParams p, int seed)
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
                var path = Walk(rng, occ, level, start, len, p.maxBends, p.hug, p.straight);
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
            level.wraps = CountWraps(level);
            return level;
        }

        private static List<Vector2Int> Walk(System.Random rng, int[,] occ, LevelData level, Vector2Int start, int len, int maxBends, float hug, float straight)
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
                bool preferStraight = rng.NextDouble() < straight;
                if (preferStraight) options.Add(heading);
                var order = new List<Vector2Int>(Dirs);
                Shuffle(rng, order);
                // Hugging: try the cells that touch the most other arrows first, so bodies curl round their neighbours.
                if (rng.NextDouble() < hug)
                {
                    var scored = new List<KeyValuePair<int, Vector2Int>>(4);
                    foreach (var d in order) scored.Add(new KeyValuePair<int, Vector2Int>(Contact(occ, w, h, cur + d, used), d));
                    for (int si = 1; si < scored.Count; si++)     // stable insertion sort (List.Sort is not stable; boards must match on every device)
                    {
                        var item = scored[si]; int j = si - 1;
                        while (j >= 0 && scored[j].Key < item.Key) { scored[j + 1] = scored[j]; j--; }
                        scored[j + 1] = item;
                    }
                    order.Clear();
                    foreach (var kv in scored) order.Add(kv.Value);
                }
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

        /// <summary>
        /// Counts bends whose inner corner is held by a different arrow: that arrow is curled round the other one.
        /// This is what makes boards look tangled, so the generator keeps the candidate with the most.
        /// </summary>
        public static int CountWraps(LevelData level)
        {
            var owner = new int[level.width, level.height];
            for (int x = 0; x < level.width; x++) for (int y = 0; y < level.height; y++) owner[x, y] = -1;
            foreach (var a in level.arrows) foreach (var c in a.cells) owner[c.x, c.y] = a.id;
            int wraps = 0;
            foreach (var a in level.arrows)
                for (int i = 1; i < a.cells.Count - 1; i++)
                {
                    var d1 = a.cells[i] - a.cells[i - 1];
                    var d2 = a.cells[i + 1] - a.cells[i];
                    if (d1 == d2) continue;
                    var inner = a.cells[i - 1] + d2;
                    if (inner.x < 0 || inner.y < 0 || inner.x >= level.width || inner.y >= level.height) continue;
                    int o = owner[inner.x, inner.y];
                    if (o != -1 && o != a.id) wraps++;
                }
            return wraps;
        }

        /// <summary>How many of the cell's four neighbours are occupied by other arrows (the walk's own cells do not count).</summary>
        private static int Contact(int[,] occ, int w, int h, Vector2Int c, HashSet<Vector2Int> own)
        {
            int n = 0;
            foreach (var d in Dirs)
            {
                var q = c + d;
                if (q.x < 0 || q.y < 0 || q.x >= w || q.y >= h || own.Contains(q)) continue;
                if (occ[q.x, q.y] != -1) n++;
            }
            return n;
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
