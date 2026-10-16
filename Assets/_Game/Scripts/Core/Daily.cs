using System;
using CircuitBreaker.Generation;

namespace CircuitBreaker.Core
{
    /// <summary>Daily Run: one board per calendar day, the same for everyone. Clearing every day of a month earns the monthly badge.</summary>
    public static class Daily
    {
        public static Difficulty TierFor(int date)
        {
            int day = date % 100;
            // A steady mix: mostly Medium, regular Hard days, and one Super Hard day a week.
            if (day % 7 == 0) return Difficulty.SuperHard;
            return day % 3 == 0 ? Difficulty.Hard : Difficulty.Medium;
        }

        public static LevelSpec SpecFor(int date)
        {
            var tier = TierFor(date);
            int day = date % 100;
            var names = ShapeLibrary.Names;
            int hash = Math.Abs(unchecked(date * 31 + 7) ^ (date >> 3));
            return new LevelSpec
            {
                index = tier == Difficulty.Medium ? 28 + day : tier == Difficulty.Hard ? 64 + day : 125 + day,
                difficulty = tier,
                shape = names[hash % names.Length],
                salt = date
            };
        }

        public static string KeyFor(int date) => "daily:" + date;
        public static bool IsCleared(int date) => SaveSystem.GetInt("daily_" + date) == 1;

        /// <summary>Marks a day cleared. Returns true when that completes the month (monthly badge earned now).</summary>
        public static bool MarkCleared(int date)
        {
            if (IsCleared(date)) return false;
            SaveSystem.SetInt("daily_" + date, 1);
            int year = date / 10000, month = (date / 100) % 100;
            if (!MonthComplete(year, month) || SaveSystem.GetInt("badge_" + year * 100 + month) == 1) return false;
            SaveSystem.SetInt("badge_" + year * 100 + month, 1);
            Stats.MonthlyBadges++;
            return true;
        }

        public static int DaysInMonth(int year, int month) => DateTime.DaysInMonth(year, month);

        public static int ClearedInMonth(int year, int month)
        {
            int n = 0;
            for (int d = 1; d <= DaysInMonth(year, month); d++) if (IsCleared(year * 10000 + month * 100 + d)) n++;
            return n;
        }

        public static bool MonthComplete(int year, int month) => ClearedInMonth(year, month) == DaysInMonth(year, month);
        public static bool BadgeEarned(int year, int month) => SaveSystem.GetInt("badge_" + year * 100 + month) == 1;

        /// <summary>Days in the past can be played to catch up within the same month; future days stay locked.</summary>
        public static bool IsPlayable(int date) => date <= Clock.Today && date / 100 == Clock.Today / 100;
    }
}
