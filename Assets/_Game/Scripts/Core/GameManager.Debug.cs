#if UNITY_EDITOR || DEVELOPMENT_BUILD || CB_DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using CircuitBreaker.Ads;
using CircuitBreaker.Data;
using CircuitBreaker.Generation;

namespace CircuitBreaker.Core
{
    /// <summary>Debug and test hooks. This whole file is dropped from the release branch.</summary>
    public partial class GameManager
    {
        // =====================================================================
        // Debug
        // =====================================================================
        public string DebugInfo(float fps)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Mode}  {BoardKey}  {DifficultyInfo.Name((Difficulty)Level.difficulty)}{(_debugForced ? "  [DEBUG]" : "")}{(_replay ? "  [REPLAY]" : "")}");
            sb.AppendLine($"level window {LevelCache.WindowStart}-{LevelCache.WindowEnd - 1}, cached {LevelCache.Count}, capacity {LevelCache.Capacity}");
            sb.AppendLine($"{Level.shape} {Level.width}x{Level.height}  arrows {Level.arrows.Count}  left {Board.Model.Remaining}  depth {Level.depth}");
            sb.AppendLine($"seed {Level.seed}  gen {_lastGenMs:F0} ms  hearts {Hearts}/{_maxHearts}  combo {_combo}");
            sb.AppendLine($"taps {_tapCount} valid {_validTaps} bumps {_bumps}  attempts L{LevelIndex}: {Stats.Attempts(LevelIndex)}");
            sb.AppendLine($"progress {SaveSystem.Progress}  coins {SaveSystem.Coins}  highest {Stats.HighestCleared}");
            sb.AppendLine($"stock H{SaveSystem.Stock("hint")}/{Economy.HintCap} R{SaveSystem.Stock("ruler")}/{Economy.RulerCap} E{SaveSystem.Stock("extract")}/{Economy.ExtractCap}");
            Economy.HintAdStatus(out int cd);
            sb.AppendLine($"hint ad {Economy.HintAdStatus(out _)} cd {cd}s  heart-ads {_continueAdsUsed}/{Economy.ContinueAdsPerSession}");
            sb.AppendLine($"restarts {_restartsThisSession} (ad on #{_restartAdTarget}, shown {_restartAdShown})");
            sb.AppendLine($"day streak {Stats.DayStreak} (best {Stats.BestDayStreak})  win streak {Stats.WinStreak} (best {Stats.BestWinStreak})");
            sb.AppendLine($"wins {Stats.TotalWins}  first-try {Stats.FirstTryWins}  achievements {Achievements.UnlockedCount}/{Achievements.All.Count}");
            sb.AppendLine($"clock {Clock.Now:yyyy-MM-dd HH:mm}  banner {AdService.BannerVisible}  locked {InputLocked}  modal {UI.ModalOpen}  fps {fps:F0}");
            sb.AppendLine(BuildInfo.Display);
            return sb.ToString();
        }

        public void DebugOverloadState(out int score, out int round, out float time) { score = _ovScore; round = _ovRound; time = _ovTime; }
        public void DebugSetOverloadTime(float t) => _ovTime = t;
        public int DebugRestartCount => _restartsThisSession;
        public bool DebugRestartAdShown => _restartAdShown;
        public int DebugRestartAdTarget => _restartAdTarget;
        public void DebugForceRestartAdTarget(int n) { _restartAdTarget = n; }
        public void DebugResetSave() => ResetEverything();
        public void DebugReload() => Begin(Mode, _spec, BoardKey, null);
        public void DebugJump(int delta) => StartClassic(Mathf.Max(1, (Mode == GameMode.Classic ? LevelIndex : SaveSystem.Progress) + delta), null, true);
        public void DebugAddCoins(int n) { SaveSystem.Coins += n; SaveSystem.Save(); UI.SetCoins(SaveSystem.Coins); Log("coins +" + n); }

        public void DebugUnlockAll()
        {
            SaveSystem.Progress = 9999;
            SaveSystem.Save();
            Log("all levels unlocked (progress 9999)");
        }

        public void DebugForceWin()
        {
            if (InputLocked) return;
            Log("DEBUG force win");
            foreach (var id in new List<int>(Board.Model.Arrows.Keys)) Board.Extract(id);
        }

        public void DebugForceFail()
        {
            if (InputLocked) return;
            Log("DEBUG force fail");
            Hearts = 1;
            if (Level.hearts == 0) Level.hearts = 3;
            OnCollided(new ArrowDef { id = -1, cells = new List<Vector2Int> { Vector2Int.zero, Vector2Int.up } });
        }

        public void DebugAutoSolve()
        {
            if (InputLocked) return;
            StartCoroutine(AutoSolveRoutine(_levelToken));
        }

        private IEnumerator AutoSolveRoutine(int token)
        {
            Log("AUTO-SOLVE started");
            while (token == _levelToken && Board.Model.Remaining > 0)
            {
                var movable = Board.Model.MovableArrows();
                if (movable.Count == 0) { Log("AUTO-SOLVE stuck: nothing movable"); yield break; }
                var a = movable[0];
                Board.Tap(Board.GridToWorld(a.cells[a.cells.Count / 2]));
                yield return new WaitForSeconds(0.1f);
            }
        }

        public void DebugResetAdCooldown()
        {
            SaveSystem.LastHintAdUtc = DateTime.MinValue;
            _continueAdsUsed = 0;
            _restartsThisSession = 0; _restartAdShown = false;
            SaveSystem.Save();
            Log("ad cooldown, heart-ad limit and restart-ad counter reset");
            RefreshGetMore();
        }

        public void DebugFillStock()
        {
            foreach (var id in new[] { "hint", "extract", "ruler" }) SaveSystem.SetStock(id, Economy.Cap(id));
            SaveSystem.Save(); RefreshBoosters(); Log("stock filled to caps");
        }

        public void DebugEmptyStock()
        {
            foreach (var id in new[] { "hint", "extract", "ruler" }) SaveSystem.SetStock(id, 0);
            SaveSystem.Save(); RefreshBoosters(); Log("stock emptied");
        }

        public void DebugNewSession()
        {
            SaveSystem.LastSessionGiftUtc = DateTime.MinValue;
            _continueAdsUsed = 0;
            string gift = Economy.BeginSession(Clock.Now);
            Log("SIMULATED NEW SESSION -> " + (gift ?? "no gift"));
            RefreshBoosters();
            if (gift != null) UI.ShowNotice("Welcome back", gift);
        }

        public void DebugFakeNextDay()
        {
            Clock.Override = Clock.Now.AddDays(1);
            Stats.RegisterSessionStart();
            Log("FAKE CLOCK -> " + Clock.Now.ToString("yyyy-MM-dd") + ", day streak " + Stats.DayStreak);
            Achievements.Evaluate();
        }

        public void DebugUnlockAchievements()
        {
            int n = 0;
            foreach (var a in Achievements.All)
                if (!a.Unlocked && n++ < 3) { SaveSystem.SetInt("ach_" + a.id, Clock.Today); UI.ShowAchievement(a); }
            Log("unlocked 3 achievements (no rewards)");
        }
    }
}
#endif
