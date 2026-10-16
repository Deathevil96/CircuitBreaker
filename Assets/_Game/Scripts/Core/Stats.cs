using System;
using CircuitBreaker.Generation;

namespace CircuitBreaker.Core
{
    /// <summary>Lifetime statistics and personal records (each record keeps the date it was set).</summary>
    public static class Stats
    {
        public static int TotalWins { get => SaveSystem.GetInt("st_wins"); private set => SaveSystem.SetInt("st_wins", value); }
        public static int PerfectWins { get => SaveSystem.GetInt("st_perfect"); private set => SaveSystem.SetInt("st_perfect", value); }
        public static int FirstTryWins { get => SaveSystem.GetInt("st_firstTry"); private set => SaveSystem.SetInt("st_firstTry", value); }
        public static int FirstTryStreak { get => SaveSystem.GetInt("st_ftStreak"); private set => SaveSystem.SetInt("st_ftStreak", value); }
        public static int BestFirstTryStreak { get => SaveSystem.GetInt("st_ftStreakBest"); private set => SaveSystem.SetInt("st_ftStreakBest", value); }
        public static int DailyClears { get => SaveSystem.GetInt("st_daily"); set => SaveSystem.SetInt("st_daily", value); }
        public static int FreeplayWins { get => SaveSystem.GetInt("st_free"); set => SaveSystem.SetInt("st_free", value); }
        public static int MonthlyBadges { get => SaveSystem.GetInt("st_badges"); set => SaveSystem.SetInt("st_badges", value); }
        public static int HighestCleared { get => SaveSystem.GetInt("st_highest"); private set => SaveSystem.SetInt("st_highest", value); }
        public static int TotalLosses { get => SaveSystem.GetInt("st_losses"); private set => SaveSystem.SetInt("st_losses", value); }

        public static int TierWins(Difficulty d) => SaveSystem.GetInt("st_tier" + (int)d);
        public static int TierFirstTryWins(Difficulty d) => SaveSystem.GetInt("st_tierFt" + (int)d);

        // ----- win streak: consecutive wins without running out of hearts -----
        public static int WinStreak { get => SaveSystem.GetInt("st_winStreak"); private set => SaveSystem.SetInt("st_winStreak", value); }
        public static int BestWinStreak => SaveSystem.GetInt("st_winStreakBest");
        public static int BestWinStreakDate => SaveSystem.GetInt("st_winStreakBestDate");

        // ----- day streak: consecutive calendar days the game was opened -----
        public static int DayStreak { get => SaveSystem.GetInt("st_dayStreak"); private set => SaveSystem.SetInt("st_dayStreak", value); }
        public static int BestDayStreak => SaveSystem.GetInt("st_dayStreakBest");
        public static int BestDayStreakEnd => SaveSystem.GetInt("st_dayStreakBestEnd");
        public static int LastPlayDay { get => SaveSystem.GetInt("st_lastDay"); private set => SaveSystem.SetInt("st_lastDay", value); }
        public static int DaysPlayed { get => SaveSystem.GetInt("st_daysPlayed"); private set => SaveSystem.SetInt("st_daysPlayed", value); }

        // ----- most wins in one day -----
        public static int WinsToday => SaveSystem.GetInt("st_winsDay") == Clock.Today ? SaveSystem.GetInt("st_winsDayCount") : 0;
        public static int MostWinsInDay => SaveSystem.GetInt("st_mostDay");
        public static int MostWinsInDayDate => SaveSystem.GetInt("st_mostDayDate");

        // ----- overload (score attack) -----
        public static int OverloadBest => SaveSystem.GetInt("st_ovBest");
        public static int OverloadBestDate => SaveSystem.GetInt("st_ovBestDate");
        public static int OverloadBestRounds => SaveSystem.GetInt("st_ovRounds");

        /// <summary>Call once per app launch (and in tests with a fake clock).</summary>
        public static void RegisterSessionStart()
        {
            int today = Clock.Today;
            int last = LastPlayDay;
            if (last == today) return;

            DayStreak = last > 0 && Clock.DaysBetween(last, today) == 1 ? DayStreak + 1 : 1;
            LastPlayDay = today;
            DaysPlayed++;
            if (DayStreak > BestDayStreak)
            {
                SaveSystem.SetInt("st_dayStreakBest", DayStreak);
                SaveSystem.SetInt("st_dayStreakBestEnd", today);
            }
            else if (DayStreak == BestDayStreak) SaveSystem.SetInt("st_dayStreakBestEnd", today);
        }

        /// <summary>A classic level was cleared. `firstTry` = no restart or failure on it before.</summary>
        public static void RegisterWin(int level, Difficulty difficulty, bool perfect, bool firstTry)
        {
            TotalWins++;
            if (level > HighestCleared) HighestCleared = level;
            if (perfect) PerfectWins++;
            SaveSystem.SetInt("st_tier" + (int)difficulty, TierWins(difficulty) + 1);
            if (firstTry)
            {
                FirstTryWins++;
                FirstTryStreak++;
                if (FirstTryStreak > BestFirstTryStreak) BestFirstTryStreak = FirstTryStreak;
                SaveSystem.SetInt("st_tierFt" + (int)difficulty, TierFirstTryWins(difficulty) + 1);
                SaveSystem.SetInt("ft_" + level, 1);
            }
            else FirstTryStreak = 0;

            WinStreak++;
            if (WinStreak > BestWinStreak)
            {
                SaveSystem.SetInt("st_winStreakBest", WinStreak);
                SaveSystem.SetInt("st_winStreakBestDate", Clock.Today);
            }
            CountWinToday();
        }

        /// <summary>Daily Run and Freeplay wins count toward "wins today" and totals too.</summary>
        public static void RegisterModeWin(bool daily)
        {
            TotalWins++;
            if (daily) DailyClears++; else FreeplayWins++;
            WinStreak++;
            if (WinStreak > BestWinStreak)
            {
                SaveSystem.SetInt("st_winStreakBest", WinStreak);
                SaveSystem.SetInt("st_winStreakBestDate", Clock.Today);
            }
            CountWinToday();
        }

        private static void CountWinToday()
        {
            int today = Clock.Today;
            int count = SaveSystem.GetInt("st_winsDay") == today ? SaveSystem.GetInt("st_winsDayCount") + 1 : 1;
            SaveSystem.SetInt("st_winsDay", today);
            SaveSystem.SetInt("st_winsDayCount", count);
            if (count > MostWinsInDay)
            {
                SaveSystem.SetInt("st_mostDay", count);
                SaveSystem.SetInt("st_mostDayDate", today);
            }
        }

        /// <summary>The player ran out of hearts.</summary>
        public static void RegisterLoss()
        {
            TotalLosses++;
            WinStreak = 0;
            FirstTryStreak = 0;
        }

        public static bool FirstTryWon(int level) => SaveSystem.GetInt("ft_" + level) == 1;

        public static bool RegisterOverload(int score, int rounds)
        {
            bool record = score > OverloadBest;
            if (record)
            {
                SaveSystem.SetInt("st_ovBest", score);
                SaveSystem.SetInt("st_ovBestDate", Clock.Today);
                SaveSystem.SetInt("st_ovRounds", rounds);
            }
            return record;
        }

        // ----- attempts per level (first-attempt tracking) -----
        public static int Attempts(int level) => SaveSystem.GetInt("att_" + level);
        public static void AddAttempt(int level) => SaveSystem.SetInt("att_" + level, Attempts(level) + 1);
        public static void ClearAttempts(int level) => SaveSystem.DeleteKey("att_" + level);
    }
}
