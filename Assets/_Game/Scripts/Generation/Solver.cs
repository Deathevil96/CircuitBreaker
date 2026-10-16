using System.Collections.Generic;
using CircuitBreaker.Core;
using CircuitBreaker.Data;

namespace CircuitBreaker.Generation
{
    public static class Solver
    {
        /// <summary>
        /// Greedy wave simulation. Removing an arrow never hurts, so greedy decides solvability exactly.
        /// waves = dependency depth (how many rounds of tapping the board needs).
        /// </summary>
        public static bool Solve(LevelData level, out int waves)
        {
            var board = new Board(level);
            waves = 0;
            while (board.Remaining > 0)
            {
                var movable = board.MovableArrows();
                if (movable.Count == 0) return false;
                waves++;
                foreach (var a in movable) board.Remove(a.id);
            }
            return true;
        }

        public static List<int> SolveOrder(LevelData level)
        {
            var order = new List<int>();
            var board = new Board(level);
            while (board.Remaining > 0)
            {
                var movable = board.MovableArrows();
                if (movable.Count == 0) return null;
                foreach (var a in movable) { order.Add(a.id); board.Remove(a.id); }
            }
            return order;
        }
    }
}
