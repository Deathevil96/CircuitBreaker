using System;
using System.Collections.Generic;
using CircuitBreaker.Generation;

namespace CircuitBreaker.Core
{
    public enum AchievementGroup { Progress, FirstAttempt, Streaks, Mastery, Totals, Modes }

    public class Achievement
    {
        public string id, title, description;
        public AchievementGroup group;
        public int target;
        public Func<int> value;
        public int coins, hints;
        public int Progress => Math.Min(target, Math.Max(0, value()));
        public bool Unlocked => SaveSystem.GetInt("ach_" + id) > 0;
        public int UnlockedDate => SaveSystem.GetInt("ach_" + id);
    }

    /// <summary>Data-driven achievements. Call Evaluate after anything that can change a statistic.</summary>
    public static class Achievements
    {
        public static readonly List<Achievement> All = Build();
        public static event Action<Achievement> Unlocked;

        public static string GroupName(AchievementGroup g)
        {
            switch (g)
            {
                case AchievementGroup.Progress: return "Progress";
                case AchievementGroup.FirstAttempt: return "First attempt";
                case AchievementGroup.Streaks: return "Streaks";
                case AchievementGroup.Mastery: return "Mastery";
                case AchievementGroup.Totals: return "Totals";
                default: return "Modes";
            }
        }

        public static int UnlockedCount { get { int n = 0; foreach (var a in All) if (a.Unlocked) n++; return n; } }

        /// <summary>Unlocks anything newly satisfied, grants its reward, raises Unlocked. Returns the newly unlocked ones.</summary>
        public static List<Achievement> Evaluate()
        {
            var fresh = new List<Achievement>();
            foreach (var a in All)
            {
                if (a.Unlocked || a.value() < a.target) continue;
                SaveSystem.SetInt("ach_" + a.id, Clock.Today);
                if (a.hints > 0)
                {
                    int added = SaveSystem.AddStock("hint", a.hints, Economy.HintCap);
                    SaveSystem.Coins += (a.hints - added) * Economy.OverflowCoins("hint");
                }
                SaveSystem.Coins += a.coins;
                fresh.Add(a);
            }
            if (fresh.Count > 0)
            {
                SaveSystem.Save();
                foreach (var a in fresh) Unlocked?.Invoke(a);
            }
            return fresh;
        }

        private static string Roman(int n)
        {
            switch (n) { case 1: return "I"; case 3: return "III"; case 5: return "V"; case 10: return "X"; case 25: return "XXV"; default: return n.ToString(); }
        }

        private static List<Achievement> Build()
        {
            var l = new List<Achievement>();
            void Add(string id, string title, string desc, AchievementGroup g, int target, Func<int> value, int coins, int hints = 0) =>
                l.Add(new Achievement { id = id, title = title, description = desc, group = g, target = target, value = value, coins = coins, hints = hints });

            // Progress: clear level N
            int[] levels = { 10, 25, 50, 100, 150, 200, 300, 500, 750, 1000 };
            int[] levelCoins = { 25, 50, 75, 150, 200, 250, 400, 750, 1000, 1500 };
            for (int i = 0; i < levels.Length; i++)
            {
                int n = levels[i];
                Add("lvl" + n, "Level " + n, "Clear level " + n + ".", AchievementGroup.Progress, n, () => Stats.HighestCleared, levelCoins[i]);
            }

            // First attempt on specific levels
            int[] ftLevels = { 10, 25, 50, 100, 150, 200, 250, 300, 400, 500 };
            for (int i = 0; i < ftLevels.Length; i++)
            {
                int n = ftLevels[i];
                Add("ft" + n, "First attempt: level " + n, "Clear level " + n + " on your first attempt.", AchievementGroup.FirstAttempt,
                    1, () => Stats.FirstTryWon(n) ? 1 : 0, 40 + 10 * i, i >= 3 ? 1 : 0);
            }
            Add("ftStreak5", "Clean run", "Clear 5 levels in a row on the first attempt.", AchievementGroup.FirstAttempt, 5, () => Stats.BestFirstTryStreak, 60);
            Add("ftStreak10", "Clean run II", "Clear 10 levels in a row on the first attempt.", AchievementGroup.FirstAttempt, 10, () => Stats.BestFirstTryStreak, 150, 1);
            Add("ftStreak25", "Clean run III", "Clear 25 levels in a row on the first attempt.", AchievementGroup.FirstAttempt, 25, () => Stats.BestFirstTryStreak, 400, 2);

            // Mastery: higher tiers
            Add("hard10", "Hard x10", "Clear 10 Hard levels.", AchievementGroup.Mastery, 10, () => Stats.TierWins(Difficulty.Hard), 100);
            Add("super5", "Super Hard x5", "Clear 5 Super Hard levels.", AchievementGroup.Mastery, 5, () => Stats.TierWins(Difficulty.SuperHard), 120);
            Add("ultra3", "Ultra Hard x3", "Clear 3 Ultra Hard levels.", AchievementGroup.Mastery, 3, () => Stats.TierWins(Difficulty.UltraHard), 150);
            int[] nm = { 1, 3, 5, 10, 25 };
            int[] nmCoins = { 150, 250, 400, 800, 2000 };
            for (int i = 0; i < nm.Length; i++)
            {
                int n = nm[i];
                Add("nm" + n, "Nightmare " + Roman(n), n == 1 ? "Clear a Nightmare level." : "Clear " + n + " Nightmare levels.", AchievementGroup.Mastery, n, () => Stats.TierWins(Difficulty.Nightmare), nmCoins[i], i >= 2 ? 1 : 0);
            }
            Add("nmFt1", "Nightmare, first attempt", "Clear a Nightmare level on your first attempt.", AchievementGroup.Mastery, 1, () => Stats.TierFirstTryWins(Difficulty.Nightmare), 500, 2);
            Add("nmFt5", "Nightmare, first attempt x5", "Clear 5 Nightmare levels on your first attempt.", AchievementGroup.Mastery, 5, () => Stats.TierFirstTryWins(Difficulty.Nightmare), 1500, 3);

            // Streaks
            int[] ws = { 5, 10, 20, 50 };
            for (int i = 0; i < ws.Length; i++) { int n = ws[i]; Add("ws" + n, "Win streak " + n, "Win " + n + " levels in a row without running out of hearts.", AchievementGroup.Streaks, n, () => Stats.BestWinStreak, 40 * (i + 1)); }
            int[] ds = { 3, 7, 14, 30, 100 };
            for (int i = 0; i < ds.Length; i++) { int n = ds[i]; Add("ds" + n, n + "-day streak", "Open the game " + n + " days in a row.", AchievementGroup.Streaks, n, () => Stats.BestDayStreak, 30 * (i + 1) * (i + 1), i >= 1 ? 1 : 0); }

            // Totals
            int[] tw = { 10, 50, 100, 250, 500, 1000 };
            for (int i = 0; i < tw.Length; i++) { int n = tw[i]; Add("tw" + n, n + " wins", "Win " + n + " boards in total.", AchievementGroup.Totals, n, () => Stats.TotalWins, 20 + 30 * i * i); }
            int[] pw = { 25, 100, 250 };
            for (int i = 0; i < pw.Length; i++) { int n = pw[i]; Add("pw" + n, "Perfect x" + n, "Clear " + n + " levels without losing a heart.", AchievementGroup.Totals, n, () => Stats.PerfectWins, 50 * (i + 1) * (i + 1)); }

            // Modes
            Add("daily1", "Daily Run", "Clear a Daily Run board.", AchievementGroup.Modes, 1, () => Stats.DailyClears, 30);
            Add("daily7", "Daily Run x7", "Clear 7 Daily Run boards.", AchievementGroup.Modes, 7, () => Stats.DailyClears, 100, 1);
            Add("daily30", "Daily Run x30", "Clear 30 Daily Run boards.", AchievementGroup.Modes, 30, () => Stats.DailyClears, 400, 2);
            Add("badge1", "Monthly badge", "Clear every Daily Run board in a month.", AchievementGroup.Modes, 1, () => Stats.MonthlyBadges, 300, 2);
            Add("badge3", "Monthly badge x3", "Earn 3 monthly badges.", AchievementGroup.Modes, 3, () => Stats.MonthlyBadges, 1000, 3);
            Add("ov2k", "Overload 2,000", "Score 2,000 in Overload.", AchievementGroup.Modes, 2000, () => Stats.OverloadBest, 60);
            Add("ov10k", "Overload 10,000", "Score 10,000 in Overload.", AchievementGroup.Modes, 10000, () => Stats.OverloadBest, 250, 1);
            Add("ov25k", "Overload 25,000", "Score 25,000 in Overload.", AchievementGroup.Modes, 25000, () => Stats.OverloadBest, 800, 2);
            Add("free10", "Freeplay x10", "Clear 10 Freeplay boards.", AchievementGroup.Modes, 10, () => Stats.FreeplayWins, 50);
            Add("free50", "Freeplay x50", "Clear 50 Freeplay boards.", AchievementGroup.Modes, 50, () => Stats.FreeplayWins, 200);
            return l;
        }
    }
}
