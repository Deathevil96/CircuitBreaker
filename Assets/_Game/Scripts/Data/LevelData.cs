using System;
using System.Collections.Generic;
using UnityEngine;

namespace CircuitBreaker.Data
{
    public enum ArrowKind { Standard = 0, Ice = 1, Key = 2, Locked = 3 }

    [Serializable]
    public class ArrowDef
    {
        public int id;
        public List<Vector2Int> cells = new List<Vector2Int>(); // 0 = tail, last = head
        public Vector2Int dir;                                  // exit direction of the head
        public int colorIndex;
        public ArrowKind kind = ArrowKind.Standard;
        public int counter;                                     // Ice: hits needed. Key/Locked: group id.

        public Vector2Int Head => cells[cells.Count - 1];
        public Vector2Int Tail => cells[0];
    }

    [Serializable]
    public class LevelData
    {
        public int index;       // 1-based
        public int width;
        public int height;
        public int hearts;      // 0 = no heart loss (tutorial)
        public int depth;       // solver wave count
        public int seed;
        public int difficulty;   // Generation.Difficulty as int
        public string shape;     // silhouette name (Heart, Star, ...)
        public bool[] mask;      // flattened x * height + y, true = playable cell
        public List<ArrowDef> arrows = new List<ArrowDef>();

        public bool InShape(int x, int y) => mask == null || mask[x * height + y];
    }
}
