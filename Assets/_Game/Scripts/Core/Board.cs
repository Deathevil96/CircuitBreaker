using System.Collections.Generic;
using UnityEngine;
using CircuitBreaker.Data;

namespace CircuitBreaker.Core
{
    /// <summary>Pure grid logic: occupancy, exit rays and arrow mechanics. No scene objects.</summary>
    public class Board
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Dictionary<int, ArrowDef> Arrows = new Dictionary<int, ArrowDef>();

        private readonly int[,] _occ;
        private readonly Dictionary<int, int> _iceLeft = new Dictionary<int, int>();
        private readonly HashSet<int> _locked = new HashSet<int>();

        public Board(LevelData level)
        {
            Width = level.width;
            Height = level.height;
            _occ = new int[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++) _occ[x, y] = -1;

            foreach (var a in level.arrows)
            {
                Arrows[a.id] = a;
                foreach (var c in a.cells) _occ[c.x, c.y] = a.id;
                if (a.kind == ArrowKind.Ice) _iceLeft[a.id] = a.counter;
                if (a.kind == ArrowKind.Locked) _locked.Add(a.id);
            }
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;
        public int OccupantAt(Vector2Int c) => InBounds(c) ? _occ[c.x, c.y] : -1;
        public int IceLeft(int id) => _iceLeft.TryGetValue(id, out var n) ? n : 0;
        public bool IsLocked(int id) => _locked.Contains(id);
        public bool IsFrozen(int id) => IceLeft(id) > 0;
        public bool IsInert(int id) => IsFrozen(id) || IsLocked(id);
        public int Remaining => Arrows.Count;

        /// <summary>Casts the exit ray from the arrow's head. Returns true when it reaches the board edge.</summary>
        public bool CastRay(ArrowDef a, out int freeSteps, out Vector2Int blockerCell, out int blockerId)
        {
            freeSteps = 0;
            blockerCell = a.Head;
            blockerId = -1;
            Vector2Int p = a.Head + a.dir;
            while (InBounds(p))
            {
                int occ = _occ[p.x, p.y];
                if (occ != -1)
                {
                    blockerCell = p;
                    blockerId = occ;
                    return false;
                }
                freeSteps++;
                p += a.dir;
            }
            return true;
        }

        public bool RayClear(ArrowDef a) => CastRay(a, out _, out _, out _);
        public bool IsMovable(ArrowDef a) => !IsInert(a.id) && RayClear(a);

        /// <summary>Removes an arrow and applies ice / key side effects. Returns ids whose state changed.</summary>
        public List<int> Remove(int id)
        {
            var changed = new List<int>();
            if (!Arrows.TryGetValue(id, out var a)) return changed;

            foreach (var c in a.cells)
                if (InBounds(c) && _occ[c.x, c.y] == id) _occ[c.x, c.y] = -1;
            Arrows.Remove(id);
            _iceLeft.Remove(id);
            _locked.Remove(id);

            var iceIds = new List<int>(_iceLeft.Keys);
            foreach (int iid in iceIds)
            {
                if (_iceLeft[iid] > 0)
                {
                    _iceLeft[iid]--;
                    changed.Add(iid);
                }
            }

            if (a.kind == ArrowKind.Key)
            {
                foreach (var other in Arrows.Values)
                {
                    if (other.kind == ArrowKind.Locked && other.counter == a.counter && _locked.Remove(other.id))
                        changed.Add(other.id);
                }
            }
            return changed;
        }

        public List<ArrowDef> MovableArrows()
        {
            var list = new List<ArrowDef>();
            foreach (var a in Arrows.Values) if (IsMovable(a)) list.Add(a);
            return list;
        }

        /// <summary>Best next move: the movable arrow whose removal unlocks the most others.</summary>
        public ArrowDef FindBestHint(out int unlockCount)
        {
            unlockCount = 0;
            ArrowDef best = null;
            int bestScore = -1;
            var movable = MovableArrows();
            var movableSet = new HashSet<int>();
            foreach (var m in movable) movableSet.Add(m.id);

            foreach (var cand in movable)
            {
                int score = 0;
                foreach (var other in Arrows.Values)
                {
                    if (other.id == cand.id || movableSet.Contains(other.id)) continue;
                    if (CastRayIgnoring(other, cand.id)) score++;
                }
                if (score > bestScore) { bestScore = score; best = cand; }
            }
            unlockCount = Mathf.Max(0, bestScore);
            return best;
        }

        private bool CastRayIgnoring(ArrowDef a, int ignoreId)
        {
            if (IsInert(a.id)) return false;
            Vector2Int p = a.Head + a.dir;
            while (InBounds(p))
            {
                int occ = _occ[p.x, p.y];
                if (occ != -1 && occ != ignoreId) return false;
                p += a.dir;
            }
            return true;
        }
    }
}
