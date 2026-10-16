using System.Collections.Generic;
using UnityEngine;
using CircuitBreaker.Core;
using CircuitBreaker.Data;

namespace CircuitBreaker.Generation
{
    /// <summary>
    /// Generates a level and plays it to the end the way the game does (repeatedly sending any arrow with a clear path),
    /// then checks the board invariants. Pure logic, safe on any thread.
    /// </summary>
    public static class LevelValidator
    {
        public struct Result
        {
            public int level, arrows, depth;
            public long generationMs;
            public string problem;       // null = the level is fine
            public bool Ok => problem == null;
        }

        public static Result Validate(int level, int budgetMs = 300)
        {
            var r = new Result { level = level };
            try
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var d = LevelGenerator.Generate(level, budgetMs);
                r.generationMs = clock.ElapsedMilliseconds;
                r.arrows = d.arrows.Count;
                r.depth = d.depth;

                var used = new HashSet<Vector2Int>();
                foreach (var a in d.arrows)
                {
                    if (a.cells.Count < 2) r.problem = r.problem ?? "arrow shorter than 2 cells";
                    if (a.Head - a.cells[a.cells.Count - 2] != a.dir) r.problem = r.problem ?? "head does not point along its last segment";
                    foreach (var c in a.cells)
                    {
                        if (!used.Add(c)) r.problem = r.problem ?? "two arrows share a cell";
                        if (!d.InShape(c.x, c.y)) r.problem = r.problem ?? "arrow outside the shape";
                    }
                }
                if (d.arrows.Count < 3) r.problem = r.problem ?? "too few arrows";

                var board = new Board(d);
                int guard = 0;
                while (board.Remaining > 0 && guard++ < 100000)
                {
                    ArrowDef pick = null;
                    foreach (var a in board.Arrows.Values) if (board.IsMovable(a)) { pick = a; break; }
                    if (pick == null) { r.problem = r.problem ?? ("stuck with " + board.Remaining + " arrows left"); break; }
                    if (!board.CastRay(pick, out _, out _, out _)) { r.problem = r.problem ?? "movable arrow reported blocked"; break; }
                    board.Remove(pick.id);
                }
            }
            catch (System.Exception e) { r.problem = "exception: " + e.Message; }
            return r;
        }
    }
}
