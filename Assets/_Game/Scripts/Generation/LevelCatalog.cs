using UnityEngine;

namespace CircuitBreaker.Generation
{
    public enum Difficulty { Easy = 0, Medium = 1, Hard = 2, SuperHard = 3, UltraHard = 4, Nightmare = 5 }

    public static class DifficultyInfo
    {
        public static readonly Difficulty[] All = { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard, Difficulty.SuperHard, Difficulty.UltraHard, Difficulty.Nightmare };

        public static string Name(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Easy: return "Easy";
                case Difficulty.Medium: return "Medium";
                case Difficulty.Hard: return "Hard";
                case Difficulty.SuperHard: return "Super Hard";
                case Difficulty.UltraHard: return "Ultra Hard";
                default: return "Nightmare";
            }
        }

        public static int Pips(Difficulty d) => (int)d + 1;      // shown as 1..6 dots in the HUD
        public static bool IsMilestone(Difficulty d) => d >= Difficulty.Hard;

        /// <summary>A representative level number used when a mode asks for a tier directly (Freeplay, Daily Run).</summary>
        public static int ReferenceLevel(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Easy: return 12;
                case Difficulty.Medium: return 40;
                case Difficulty.Hard: return 80;
                case Difficulty.SuperHard: return 130;
                case Difficulty.UltraHard: return 200;
                default: return 300;
            }
        }
    }

    /// <summary>Everything the generator needs to know about one board.</summary>
    public struct LevelParams
    {
        public int width, height;
        public int minLen, maxLen, maxBends;
        public int minDepth;
        public float fill;
        public float iceChance;
        public int keyPairs;
        public int hearts;               // 0 = no heart loss
        public Difficulty difficulty;
        public string shape;
        public string tier => DifficultyInfo.Name(difficulty);
    }

    /// <summary>Describes which board to build: a classic level, or a tier/shape chosen by a mode.</summary>
    public class LevelSpec
    {
        public int index = 1;                    // drives size growth and mechanic unlocks
        public Difficulty? difficulty;           // null = derived from the level number
        public string shape;                     // null = derived from the level number
        public int salt;                         // different salt = different board for the same level/tier
        public static LevelSpec Classic(int level) => new LevelSpec { index = level };
    }

    /// <summary>Maps levels to generation parameters (GDD sections 4 and 13).</summary>
    public static class LevelCatalog
    {
        public const int MaxSize = 26;

        /// <summary>Easy to Nightmare. Milestone levels recur: Hard every 20, Super Hard every 30, Ultra Hard every 50, Nightmare every 100.</summary>
        public static Difficulty DifficultyOf(int level)
        {
            level = Mathf.Max(1, level);
            if (level % 100 == 0) return Difficulty.Nightmare;
            if (level % 50 == 0) return Difficulty.UltraHard;
            if (level % 30 == 0) return Difficulty.SuperHard;
            if (level % 20 == 0) return Difficulty.Hard;
            if (level > 100 && level % 10 == 0) return Difficulty.Hard;     // more spikes once the player is experienced
            return level <= 20 ? Difficulty.Easy : Difficulty.Medium;
        }

        public static LevelParams For(int level) => For(LevelSpec.Classic(level));

        public static LevelParams For(LevelSpec spec)
        {
            int level = Mathf.Max(1, spec.index);
            var difficulty = spec.difficulty ?? DifficultyOf(level);
            var p = new LevelParams { difficulty = difficulty, hearts = level <= 5 ? 0 : 3 };
            int size;

            if (level <= 5)
            {
                size = 5 + (level - 1) / 2;       // 5,5,6,6,6
                p.minLen = 2; p.maxLen = 3; p.maxBends = level < 3 ? 0 : 1;
                p.minDepth = level <= 2 ? 2 : 3; p.fill = 0.55f;
            }
            else if (level <= 20)
            {
                size = 7 + Mathf.FloorToInt((level - 6) * 2.99f / 14f);   // 7..9
                p.minLen = 2; p.maxLen = 4; p.maxBends = level < 12 ? 1 : 2;
                p.minDepth = 4 + (level - 6) / 7; p.fill = 0.60f;
            }
            else if (level <= 60)
            {
                size = 11 + Mathf.FloorToInt((level - 21) * 3.99f / 39f); // 11..14
                p.minLen = 2; p.maxLen = 5; p.maxBends = 3;
                p.minDepth = 7 + (level - 21) / 11; p.fill = 0.67f;
            }
            else if (level <= 150)
            {
                size = 15 + Mathf.FloorToInt((level - 61) * 4.99f / 89f); // 15..19
                p.minLen = 2; p.maxLen = 6; p.maxBends = 4;
                p.minDepth = 11 + (level - 61) / 12; p.fill = 0.72f;
            }
            else
            {
                // Keeps climbing to level 5000 instead of plateauing: board size, chain depth, arrow length, bends and density.
                size = Mathf.Min(MaxSize, 21 + (level - 151) / 100);                  // 21 at 151, 26 by ~650
                p.minLen = 2; p.maxLen = level < 600 ? 7 : 8;
                p.maxBends = level < 1000 ? 4 : 5;
                p.minDepth = Mathf.Min(34, 19 + (level - 151) / 90);                  // 19 -> 34 around level 1500
                p.fill = Mathf.Min(0.78f, 0.74f + (level - 151) / 20000f);
            }

            // Tougher tiers: bigger board, longer and more bent arrows, deeper dependency chains.
            switch (difficulty)
            {
                case Difficulty.Hard: Bump(ref p, ref size, 2, 2, 1, 1, 0.04f); break;
                case Difficulty.SuperHard: Bump(ref p, ref size, 3, 4, 1, 1, 0.05f); if (level > 5) p.hearts = 2; break;
                case Difficulty.UltraHard: Bump(ref p, ref size, 4, 6, 2, 1, 0.06f); if (level > 5) p.hearts = 2; break;
                case Difficulty.Nightmare: Bump(ref p, ref size, 5, 8, 2, 2, 0.08f); p.hearts = level <= 5 ? 0 : 1; break;
            }

            string shape = spec.shape ?? ShapeLibrary.NameFor(level);
            p.shape = shape;

            if (shape == "Rectangle")
            {
                p.width = size;
                p.height = size + size / 4;       // a bit taller than wide to use the portrait screen
            }
            else
            {
                // Shapes cover only part of their box: aim for a comparable number of playable cells, with a finer grid.
                var info = ShapeLibrary.Info(shape);
                float cells = 1.9f * size * size;
                float area = cells / Mathf.Max(0.3f, info.coverage);
                p.width = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(area * info.aspect)), 8, 36);
                p.height = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(area / info.aspect)), 8, 36);
                p.fill = Mathf.Min(0.88f, p.fill + 0.15f);    // denser, so the silhouette reads
            }

            float iceCap = level > 1000 ? 0.28f : 0.2f;
            float tierMul = difficulty == Difficulty.Nightmare ? 1.8f : difficulty >= Difficulty.UltraHard ? 1.3f : 1f;
            p.iceChance = level >= 20 ? Mathf.Min(iceCap, (0.08f + (level - 20) * 0.002f) * tierMul) : 0f;
            int keyCap = level > 1000 ? 8 : 5;
            p.keyPairs = level >= 35 ? Mathf.Min(keyCap, 1 + (level - 35) / 30 + (difficulty == Difficulty.Nightmare ? 1 : 0)) : 0;
            return p;
        }

        private static void Bump(ref LevelParams p, ref int size, int sizeAdd, int depthAdd, int lenAdd, int bendAdd, float fillAdd)
        {
            size = Mathf.Min(MaxSize, size + sizeAdd);
            p.minDepth += depthAdd;
            p.maxLen = Mathf.Min(8, p.maxLen + lenAdd);
            p.maxBends = Mathf.Min(5, p.maxBends + bendAdd);
            p.fill = Mathf.Min(0.78f, p.fill + fillAdd);
        }
    }
}
