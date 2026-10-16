#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using CircuitBreaker.Core;
using CircuitBreaker.Data;
using CircuitBreaker.Generation;

namespace CircuitBreaker.EditorTools
{
    /// <summary>
    /// Fast logic tests (no scene needed). Run from the menu: CircuitBreaker > Run Self Tests.
    /// They work in a save sandbox, so they can never touch a real save.
    /// </summary>
    public static class SelfTests
    {
        private static int _pass, _fail;
        private static readonly StringBuilder Report = new StringBuilder();

        private static void Check(bool ok, string name, string detail = "")
        {
            if (ok) { _pass++; return; }
            _fail++;
            Report.AppendLine("  FAIL  " + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
        }

        [MenuItem("CircuitBreaker/Run Self Tests")]
        public static void RunFromMenu()
        {
            string r = RunAll();
            if (_fail == 0) Debug.Log("[CircuitBreaker] " + r); else Debug.LogError("[CircuitBreaker] " + r);
        }

        public static string RunAll()
        {
            _pass = _fail = 0;
            Report.Clear();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            SaveSystem.EnterSandbox();
            try
            {
                Run("Shapes", Shapes);
                Run("Difficulty", DifficultyMap);
                Run("Generator", GeneratorChecks);
                Run("Economy", EconomyChecks);
                Run("Stats", StatsChecks);
                Run("Achievements", AchievementChecks);
                Run("RunSave", RunSaveChecks);
                Run("Daily", DailyChecks);
                Run("Board restore", BoardRestore);
            }
            finally { SaveSystem.ExitSandbox(); }
            return $"Self tests: {_pass} passed, {_fail} failed in {clock.ElapsedMilliseconds} ms\n{Report}";
        }

        private static void Run(string name, Action test)
        {
            try { test(); }
            catch (Exception e) { _fail++; Report.AppendLine("  EXCEPTION in " + name + ": " + e.Message); }
        }

        // ------------------------------------------------------------------
        private static void Shapes()
        {
            foreach (var name in ShapeLibrary.Names)
            {
                var info = ShapeLibrary.Info(name);
                Check(info.coverage > 0.15f && info.coverage < 0.95f, "shape coverage " + name, info.coverage.ToString("F2"));
                int w = 24, h = Mathf.Clamp(Mathf.RoundToInt(24 / info.aspect), 8, 48);
                var mask = ShapeLibrary.Build(name, w, h);
                // largest 4-connected component must hold nearly all the cells (no stray islands)
                int total = 0; foreach (bool m in mask) if (m) total++;
                var seen = new bool[w * h]; int best = 0;
                for (int i = 0; i < mask.Length; i++)
                {
                    if (!mask[i] || seen[i]) continue;
                    int size = 0; var stack = new Stack<int>(); stack.Push(i); seen[i] = true;
                    while (stack.Count > 0)
                    {
                        int c = stack.Pop(); size++;
                        int x = c / h, y = c % h;
                        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int ni = nx * h + ny;
                            if (mask[ni] && !seen[ni]) { seen[ni] = true; stack.Push(ni); }
                        }
                    }
                    best = Mathf.Max(best, size);
                }
                Check(best >= total * 0.9f, "shape connected " + name, best + "/" + total);
            }
            Check(ShapeLibrary.NameFor(20) == "Rectangle" && ShapeLibrary.NameFor(21) == "Heart", "rectangles until level 21, heart first");
        }

        private static void DifficultyMap()
        {
            Check(LevelCatalog.DifficultyOf(1) == Difficulty.Easy, "L1 easy");
            Check(LevelCatalog.DifficultyOf(20) == Difficulty.Hard, "L20 hard");
            Check(LevelCatalog.DifficultyOf(21) == Difficulty.Medium, "L21 medium");
            Check(LevelCatalog.DifficultyOf(30) == Difficulty.SuperHard, "L30 super hard");
            Check(LevelCatalog.DifficultyOf(40) == Difficulty.Hard, "L40 hard");
            Check(LevelCatalog.DifficultyOf(50) == Difficulty.UltraHard, "L50 ultra hard");
            Check(LevelCatalog.DifficultyOf(60) == Difficulty.SuperHard, "L60 super hard (30 beats 20)");
            Check(LevelCatalog.DifficultyOf(100) == Difficulty.Nightmare, "L100 nightmare");
            Check(LevelCatalog.DifficultyOf(150) == Difficulty.UltraHard, "L150 ultra hard (50 beats 30)");
            Check(LevelCatalog.DifficultyOf(200) == Difficulty.Nightmare, "L200 nightmare");
            Check(LevelCatalog.For(100).hearts == 2, "nightmare has 2 hearts");
            Check(LevelCatalog.For(3).hearts == 0 && LevelCatalog.For(7).hearts == 3, "tutorial free, then 3 hearts");
        }

        private static void CheckLevel(LevelData d, string label)
        {
            Check(Solver.Solve(d, out int waves), label + " solvable");
            var used = new HashSet<Vector2Int>();
            bool overlap = false, outside = false, badDir = false, badLen = false;
            foreach (var a in d.arrows)
            {
                if (a.cells.Count < 2) badLen = true;
                foreach (var c in a.cells)
                {
                    if (!used.Add(c)) overlap = true;
                    if (!d.InShape(c.x, c.y)) outside = true;
                }
                if (a.Head - a.cells[a.cells.Count - 2] != a.dir) badDir = true;
            }
            Check(!overlap, label + " no overlapping cells");
            Check(!outside, label + " arrows inside the shape");
            Check(!badDir, label + " head points along last segment");
            Check(!badLen, label + " arrows have 2+ cells");
            Check(d.arrows.Count >= 4, label + " has arrows", d.arrows.Count.ToString());
        }

        private static void GeneratorChecks()
        {
            int[] levels = { 1, 2, 5, 6, 12, 20, 21, 22, 30, 36, 40, 50, 60, 100, 150 };
            foreach (int l in levels) CheckLevel(LevelGenerator.Generate(l, 80), "L" + l);
            foreach (var t in DifficultyInfo.All)
            {
                var spec = new LevelSpec { index = DifficultyInfo.ReferenceLevel(t), difficulty = t, shape = "Cat", salt = 3 };
                CheckLevel(LevelGenerator.Generate(spec, 80), "tier " + t);
            }
            // deterministic: same level twice is the same board
            var a = LevelGenerator.Generate(33, 200); var b = LevelGenerator.Generate(33, 200);
            Check(a.arrows.Count == b.arrows.Count && a.seed == b.seed && a.depth == b.depth, "deterministic generation");
            // rolling level window: device sizing, sliding and eviction
            Check(LevelCache.ForDevice(2000, 8) == 10 && LevelCache.ForDevice(4000, 8) == 20 && LevelCache.ForDevice(6000, 8) == 30 && LevelCache.ForDevice(12000, 8) == 50 && LevelCache.ForDevice(12000, 4) == 20, "level window size follows device RAM and cores");
            LevelCache.Clear(); LevelCache.OverrideCapacity(10);
            LevelCache.Focus(1);
            Check(LevelCache.WindowEnd == 11, "window covers capacity levels from the current one");
            LevelCache.Store(1, LevelGenerator.Generate(1)); LevelCache.Store(2, LevelGenerator.Generate(2));
            LevelCache.Focus(10);                      // within 10% of the end: slides
            Check(LevelCache.WindowEnd == 20 && LevelCache.WindowStart == 8, "window slides when the player nears its end");
            Check(!LevelCache.TryGet(1, out _) && !LevelCache.TryGet(2, out _), "levels behind the window are dropped");
            LevelCache.Clear(); LevelCache.OverrideCapacity(0);
            // a level must not depend on how long generation was allowed to take
            var f1 = LevelGenerator.Generate(150, 1); var f2 = LevelGenerator.Generate(150, 5000);
            Check(f1.seed == f2.seed && f1.arrows.Count == f2.arrows.Count && f1.depth == f2.depth, "generation ignores time budgets");
            // different salt -> different board
            var c1 = LevelGenerator.Generate(new LevelSpec { index = 40, difficulty = Difficulty.Medium, shape = "Fish", salt = 1 }, 80);
            var c2 = LevelGenerator.Generate(new LevelSpec { index = 40, difficulty = Difficulty.Medium, shape = "Fish", salt = 2 }, 80);
            Check(c1.seed != c2.seed, "salt changes the board");
        }

        private static void EconomyChecks()
        {
            SaveSystem.ResetAll();
            Check(SaveSystem.Stock("hint") == 3 && SaveSystem.Stock("ruler") == 0, "fresh stock: 3 hints");
            Check(SaveSystem.AddStock("hint", 5, Economy.HintCap) == 0, "hint cap holds at 3");
            SaveSystem.SetStock("hint", 1);
            Check(SaveSystem.AddStock("hint", 5, Economy.HintCap) == 2, "cap trims the grant");

            // session gifts
            var d1 = new DateTime(2026, 10, 16, 9, 0, 0);
            SaveSystem.SetStock("hint", 0);
            Check(Economy.BeginSession(d1) == "DAILY BONUS: +1 HINT", "daily bonus");
            SaveSystem.SetStock("hint", 0);
            Check(Economy.BeginSession(d1.AddMinutes(5)) == null, "restart gift guarded for 20 min");
            Check(Economy.BeginSession(d1.AddMinutes(25)) == "WELCOME BACK: +1 HINT", "restart gift after the guard");

            // tier rewards
            Economy.TierReward(Difficulty.Nightmare, out int h, out int r, out int e, out int c);
            Check(h == 3 && r == 2 && e == 1 && c == 150, "nightmare reward");
            SaveSystem.SetStock("hint", 0);
            var win = Economy.ResolveWin(20, true, 3);     // Hard level
            Check(win.hints == 1, "hard level gives a hint", win.hints.ToString());
            SaveSystem.SetStock("hint", 3);
            var win2 = Economy.ResolveWin(40, true, 3);    // Hard again, hints full -> overflow coins
            Check(win2.hints == 0 && win2.coins >= 10 + 6 + 10 + 25 + Economy.OverflowCoins("hint"), "hint overflow becomes coins", win2.coins.ToString());

            Check(Economy.Price("hint", 1) < Economy.Price("hint", 200), "prices rise with level");
            Check(Economy.RulerChance(15) > Economy.RulerChance(300), "ruler drops get rarer");
            Check(Economy.ExtractStreak(300) > Economy.ExtractStreak(8), "extract streak grows");
        }

        private static void StatsChecks()
        {
            SaveSystem.ResetAll();
            Clock.Override = new DateTime(2026, 10, 10, 12, 0, 0);
            Stats.RegisterSessionStart();
            Check(Stats.DayStreak == 1, "day streak starts at 1");
            Clock.Override = new DateTime(2026, 10, 11, 12, 0, 0); Stats.RegisterSessionStart();
            Clock.Override = new DateTime(2026, 10, 12, 8, 0, 0); Stats.RegisterSessionStart();
            Check(Stats.DayStreak == 3 && Stats.BestDayStreak == 3, "consecutive days build a streak");
            Check(Stats.BestDayStreakEnd == 20261012, "best streak keeps its end date");
            Stats.RegisterSessionStart();    // same day again
            Check(Stats.DayStreak == 3, "same day does not double count");
            Clock.Override = new DateTime(2026, 10, 15, 8, 0, 0); Stats.RegisterSessionStart();
            Check(Stats.DayStreak == 1 && Stats.BestDayStreak == 3, "a gap resets the streak but keeps the record");

            // win streak + most wins in a day
            for (int i = 1; i <= 4; i++) Stats.RegisterWin(i, Difficulty.Easy, true, true);
            Check(Stats.WinStreak == 4 && Stats.BestWinStreak == 4, "win streak counts");
            Check(Stats.MostWinsInDay == 4 && Stats.MostWinsInDayDate == 20261015, "most wins in a day with date");
            Stats.RegisterLoss();
            Check(Stats.WinStreak == 0 && Stats.BestWinStreak == 4, "a loss resets the streak, record stays");
            Clock.Override = new DateTime(2026, 10, 16, 8, 0, 0);
            Stats.RegisterWin(5, Difficulty.Easy, false, false);
            Check(Stats.WinsToday == 1 && Stats.MostWinsInDay == 4, "wins today resets daily, record stays");
            Check(Stats.FirstTryWins == 4 && Stats.FirstTryStreak == 0, "first-try tracking");
            Check(Stats.HighestCleared == 5, "highest cleared");

            // attempts
            Check(Stats.Attempts(7) == 0, "no attempts yet");
            Stats.AddAttempt(7); Stats.AddAttempt(7);
            Check(Stats.Attempts(7) == 2, "attempts counted");
            Stats.ClearAttempts(7);
            Check(Stats.Attempts(7) == 0, "attempts cleared on win");
            Clock.Override = null;
        }

        private static void AchievementChecks()
        {
            SaveSystem.ResetAll();
            Clock.Override = new DateTime(2026, 11, 2, 12, 0, 0);
            int coinsBefore = SaveSystem.Coins;
            var unlocked = new List<string>();
            Action<Achievement> handler = a => unlocked.Add(a.id);
            Achievements.Unlocked += handler;
            try
            {
                Check(Achievements.All.Count >= 50, "plenty of achievements", Achievements.All.Count.ToString());
                var ids = new HashSet<string>();
                foreach (var a in Achievements.All) Check(ids.Add(a.id), "unique achievement id " + a.id);

                Stats.RegisterWin(10, Difficulty.Easy, true, true);
                Achievements.Evaluate();
                Check(unlocked.Contains("lvl10"), "clearing level 10 unlocks the level achievement");
                Check(unlocked.Contains("ft10"), "first-attempt win on level 10 unlocks its award");
                Check(SaveSystem.Coins > coinsBefore, "achievement rewards coins");
                unlocked.Clear();
                Achievements.Evaluate();
                Check(unlocked.Count == 0, "achievements unlock only once");

                Stats.RegisterWin(100, Difficulty.Nightmare, true, true);
                Achievements.Evaluate();
                Check(unlocked.Contains("nm1") && unlocked.Contains("nmFt1"), "nightmare clear and first-attempt nightmare");
                Check(unlocked.Contains("lvl100") && unlocked.Contains("ft100"), "level 100 and first-attempt level 100");
                Check(Achievements.All.Find(x => x.id == "nm1").UnlockedDate == 20261102, "unlock date recorded");

                // not first attempt: only the plain level achievement
                unlocked.Clear();
                Stats.RegisterWin(25, Difficulty.Medium, true, false);
                Achievements.Evaluate();
                Check(Achievements.All.Find(x => x.id == "lvl25").Unlocked && !Achievements.All.Find(x => x.id == "ft25").Unlocked, "no first-attempt award when it took several tries");
            }
            finally { Achievements.Unlocked -= handler; Clock.Override = null; }
        }

        private static void RunSaveChecks()
        {
            SaveSystem.ResetAll(); RunSave.ClearAll();
            var s = new RunState { key = "classic:24", hearts = 2, heartsLost = 1, bumps = 3, bestCombo = 5, removed = new List<int> { 4, 1, 9 } };
            RunSave.Save(s);
            var l = RunSave.Load();
            Check(l != null && l.key == "classic:24" && l.hearts == 2 && l.heartsLost == 1 && l.bumps == 3 && l.bestCombo == 5, "run roundtrip");
            Check(l != null && l.removed.Count == 3 && l.removed[0] == 4 && l.removed[2] == 9, "removed order kept");

            // separate slot for the daily run, most recent is "last"
            RunSave.Save(new RunState { key = "daily:20261016", hearts = 3, removed = new List<int> { 2 } });
            Check(RunSave.Load().key == "daily:20261016", "latest saved family is continued");
            Check(RunSave.LoadFamily("classic").key == "classic:24", "classic slot untouched by a daily run");
            Check(RunSave.Has("classic:24") && !RunSave.Has("classic:25"), "Has() matches the exact board");
            RunSave.ClearFamily("daily");
            Check(RunSave.Load().key == "classic:24", "falls back to the other slot");

            // a run from another generator version is ignored
            SaveSystem.SetString("run_classic", (BuildInfo.GeneratorVersion + 1) + "|classic:24|3|0|0|0|1,2");
            Check(RunSave.LoadFamily("classic") == null, "stale generator version is discarded");
            RunSave.ClearAll();
        }

        private static void DailyChecks()
        {
            SaveSystem.ResetAll();
            Clock.Override = new DateTime(2026, 2, 10, 10, 0, 0);       // February 2026 has 28 days
            Check(Daily.DaysInMonth(2026, 2) == 28, "28 days in Feb 2026");
            Check(Daily.IsPlayable(20260210) && Daily.IsPlayable(20260203), "today and earlier days of the month are playable");
            Check(!Daily.IsPlayable(20260211), "future days are locked");
            Check(!Daily.IsPlayable(20260131), "last month is closed");
            Check(Daily.SpecFor(20260210).shape == Daily.SpecFor(20260210).shape, "daily board is stable");
            Check(LevelGenerator.Generate(Daily.SpecFor(20260210), 80).seed == LevelGenerator.Generate(Daily.SpecFor(20260210), 80).seed, "same day, same board");

            bool badge = false;
            for (int d = 1; d <= 28; d++) badge = Daily.MarkCleared(20260200 + d);
            Check(badge, "clearing the last day completes the month");
            Check(Stats.MonthlyBadges == 1 && Daily.BadgeEarned(2026, 2), "monthly badge recorded");
            Check(!Daily.MarkCleared(20260228), "re-clearing gives no second badge");
            Clock.Override = null;
        }

        private static void BoardRestore()
        {
            var level = LevelGenerator.Generate(45, 80);
            var order = Solver.SolveOrder(level);
            Check(order != null, "solve order exists");
            var live = new Board(level);
            var restored = new Board(level);
            int half = order.Count / 2;
            for (int i = 0; i < half; i++) live.Remove(order[i]);
            foreach (int id in order.GetRange(0, half)) restored.Remove(id);
            Check(live.Remaining == restored.Remaining && live.Remaining == order.Count - half, "replaying removals restores the same board");
            bool sameMovable = live.MovableArrows().Count == restored.MovableArrows().Count;
            Check(sameMovable, "restored board has the same movable arrows");
            // the rest is still solvable from the restored state
            int guard = 0;
            while (restored.Remaining > 0 && guard++ < 500)
            {
                var m = restored.MovableArrows();
                if (m.Count == 0) break;
                foreach (var a in m) restored.Remove(a.id);
            }
            Check(restored.Remaining == 0, "restored board can still be finished");
        }
    }
}
#endif
