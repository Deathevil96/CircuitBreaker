using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using CircuitBreaker.Core;
using CircuitBreaker.Fx;
using CircuitBreaker.Generation;

namespace CircuitBreaker.UI
{
    public partial class GameUI
    {
        private class Page
        {
            public GameObject root;
            public RectTransform content;
            public Text title;
        }

        private Page _levelsPage, _recordsPage, _settingsPage, _dailyPage;
        private int _dailySelected;
        private UiKit.ToggleRow _soundToggle, _vibToggle, _glowToggle;

        private Page MakePage(string title, Action onBack)
        {
            var p = new Page();
            var bg = UiKit.Img(_overlay, Theme.Bg, null, raycast: true);
            bg.name = "Page_" + title;
            p.root = bg.gameObject;
            UiKit.Stretch(bg.rectTransform);

            var head = UiKit.Rect("Header", p.root.transform);
            UiKit.Anchor(head, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 140));
            var back = UiKit.MakeIconButton(head, "back", onBack, 96);
            UiKit.Anchor((RectTransform)back.border.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(32, 0), new Vector2(96, 96));
            p.title = UiKit.Label(head, title, Theme.H2, Theme.Text, TextAnchor.MiddleCenter, bold: true);
            UiKit.Stretch(p.title.rectTransform);
            var line = UiKit.Img(head, Theme.Line);
            UiKit.Anchor(line.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 2));

            var area = UiKit.Rect("Area", p.root.transform);
            UiKit.Stretch(area, 0, 0, 0, 142);
            p.content = UiKit.ScrollColumn(area, 18, 40, 28);
            p.root.SetActive(false);
            return p;
        }

        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i).gameObject;
                child.SetActive(false);                 // leaves the layout immediately
                UnityEngine.Object.Destroy(child);
            }
        }

        private void BuildPages()
        {
            _levelsPage = MakePage("Levels", () => SetLayer(_levelsPage.root, false));
            _recordsPage = MakePage("Records", () => SetLayer(_recordsPage.root, false));
            _settingsPage = MakePage("Settings", () => SetLayer(_settingsPage.root, false));
            _dailyPage = MakePage("Daily Run", () => SetLayer(_dailyPage.root, false));
        }

        // =====================================================================
        // Levels
        // =====================================================================
        public void ShowLevels(int current, int progress)
        {
            ClearChildren(_levelsPage.content);
            var c = _levelsPage.content;
            int last = Mathf.Min(progress + 9, 400);

            var hint = UiKit.Label(c, "Tap a cleared level to replay it. Your saved progress is not affected.", Theme.Caption, Theme.Dim, TextAnchor.MiddleLeft, wrap: true);
            UiKit.Size(hint, h: 70);

            var grid = UiKit.Rect("Grid", c);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 5;
            g.spacing = new Vector2(14, 14);
            g.cellSize = new Vector2(168, 150);
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var fitter = grid.gameObject.AddComponent<GridFit>();
            fitter.columns = 5; fitter.spacing = 14; fitter.aspect = 150f / 168f;

            for (int lvl = 1; lvl <= last; lvl++)
            {
                int level = lvl;
                bool unlocked = lvl <= progress;
                var d = LevelCatalog.DifficultyOf(lvl);
                var s = UiKit.Card(grid, lvl == current ? Theme.Surface2 : Theme.Surface, lvl == current ? Theme.Text : Theme.Line, 14f, raycast: true);
                s.root.name = "L" + lvl;
                var col = UiKit.Column(s.content, 2, fitHeight: false, align: TextAnchor.MiddleCenter);
                UiKit.Stretch(col);
                var num = UiKit.Label(col, lvl.ToString(), Theme.H2, unlocked ? Theme.Text : Theme.Faint, TextAnchor.MiddleCenter, bold: true);
                UiKit.Size(num, h: 50);
                string sub = !unlocked ? "" : Stats.FirstTryWon(lvl) ? "first try" : DifficultyInfo.IsMilestone(d) ? DifficultyInfo.Name(d) : "";
                var cap = UiKit.Label(col, sub, Theme.Micro - 2, d == Difficulty.Nightmare ? Theme.Danger : Theme.Dim, TextAnchor.MiddleCenter);
                UiKit.Size(cap, h: 28);
                var tap = UiKit.Tappable(s, () => { if (unlocked) { SetLayer(_levelsPage.root, false); LevelPicked?.Invoke(level); } });
                tap.button.interactable = unlocked;
            }
            SetLayer(_levelsPage.root, true);
        }

        /// <summary>Makes a grid's cells fill the available width.</summary>
        private class GridFit : MonoBehaviour
        {
            public int columns; public float spacing, aspect;
            private float _lastW;
            private void Update()
            {
                var rt = (RectTransform)transform;
                float w = rt.rect.width;
                if (w <= 1f || Mathf.Abs(w - _lastW) < 0.5f) return;
                _lastW = w;
                float cell = (w - spacing * (columns - 1)) / columns;
                var g = GetComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(cell, cell * aspect);
            }
        }

        // =====================================================================
        // Records and achievements
        // =====================================================================
        public void ShowRecords()
        {
            ClearChildren(_recordsPage.content);
            var c = _recordsPage.content;

            Section(c, "Personal records");
            var r1 = UiKit.Row(c, 16, TextAnchor.UpperCenter, expand: true); UiKit.Size(r1, h: 210);
            StatCard(r1, "Day streak", Stats.BestDayStreak.ToString(), Rec(Stats.BestDayStreakEnd, "Best, "), "Current " + Stats.DayStreak);
            StatCard(r1, "Win streak", Stats.BestWinStreak.ToString(), Rec(Stats.BestWinStreakDate, "Best, "), "Current " + Stats.WinStreak);
            var r2 = UiKit.Row(c, 16, TextAnchor.UpperCenter, expand: true); UiKit.Size(r2, h: 210);
            StatCard(r2, "Most wins in a day", Stats.MostWinsInDay.ToString(), Rec(Stats.MostWinsInDayDate, ""), "Today " + Stats.WinsToday);
            StatCard(r2, "Overload best", Stats.OverloadBest.ToString("N0"), Rec(Stats.OverloadBestDate, ""), Stats.OverloadBestRounds > 0 ? Stats.OverloadBestRounds + " boards" : "");
            var r3 = UiKit.Row(c, 16, TextAnchor.UpperCenter, expand: true); UiKit.Size(r3, h: 210);
            StatCard(r3, "Total wins", Stats.TotalWins.ToString("N0"), Stats.PerfectWins + " perfect", Stats.FirstTryWins + " first try");
            StatCard(r3, "Highest level", Stats.HighestCleared.ToString(), "Nightmare clears " + Stats.TierWins(Difficulty.Nightmare), "Monthly badges " + Stats.MonthlyBadges);

            UiKit.Spacer(c, 10);
            Section(c, "Achievements  " + Achievements.UnlockedCount + " / " + Achievements.All.Count);

            AchievementGroup? lastGroup = null;
            foreach (var a in Achievements.All)
            {
                if (lastGroup != a.group)
                {
                    lastGroup = a.group;
                    UiKit.Spacer(c, 6);
                    var gl = UiKit.Label(c, Achievements.GroupName(a.group).ToUpperInvariant(), Theme.Micro, Theme.Faint, TextAnchor.MiddleLeft, bold: true);
                    UiKit.Size(gl, h: 34);
                }
                AchievementRow(c, a);
            }
            UiKit.Spacer(c, 30);
            SetLayer(_recordsPage.root, true);
        }

        private static string Rec(int date, string prefix) => date <= 0 ? "No record yet" : prefix + Clock.Format(date);

        private static void Section(Transform parent, string text)
        {
            var l = UiKit.Label(parent, text, Theme.Body, Theme.Text, TextAnchor.MiddleLeft, bold: true);
            UiKit.Size(l, h: 56);
        }

        private static void StatCard(Transform row, string caption, string value, string sub1, string sub2)
        {
            var s = UiKit.Card(row, Theme.Surface, Theme.Line, 18f);
            UiKit.Size(s.root, h: 210, flexW: 1);
            var col = UiKit.Column(s.content, 4, 26, 22, fitHeight: false, align: TextAnchor.UpperLeft);
            UiKit.Stretch(col);
            var cap = UiKit.Label(col, caption, Theme.Caption, Theme.Dim, TextAnchor.MiddleLeft); UiKit.Size(cap, h: 32);
            var val = UiKit.Label(col, value, 58, Theme.Text, TextAnchor.MiddleLeft, bold: true); UiKit.Size(val, h: 72);
            var s1 = UiKit.Label(col, sub1, Theme.Micro + 1, Theme.Dim, TextAnchor.MiddleLeft); UiKit.Size(s1, h: 28);
            var s2 = UiKit.Label(col, sub2, Theme.Micro + 1, Theme.Faint, TextAnchor.MiddleLeft); UiKit.Size(s2, h: 28);
        }

        private static void AchievementRow(Transform parent, Achievement a)
        {
            bool done = a.Unlocked;
            var s = UiKit.Card(parent, Theme.Surface, Theme.Line, 16f);
            s.root.name = "Ach_" + a.id;
            UiKit.Size(s.root, h: 150);
            var row = UiKit.Row(s.content, 22, TextAnchor.MiddleLeft);
            UiKit.Stretch(row, 28, 0, 28, 0);
            var ic = UiKit.Icon(row, done ? "check" : "lock", 46, done ? Theme.Text : Theme.Faint);
            UiKit.Size(ic.rectTransform, w: 46, h: 46);

            var col = UiKit.Column(row, 6, fitHeight: false, align: TextAnchor.MiddleLeft);
            UiKit.Size(col, flexW: 1);
            var t = UiKit.Label(col, a.title, Theme.Body - 2, done ? Theme.Text : Theme.Dim, TextAnchor.MiddleLeft, bold: true); UiKit.Size(t, h: 40);
            var d = UiKit.Label(col, a.description, Theme.Micro + 2, Theme.Faint, TextAnchor.MiddleLeft); UiKit.Size(d, h: 30);
            if (done)
            {
                var u = UiKit.Label(col, "Unlocked " + Clock.Format(a.UnlockedDate), Theme.Micro + 1, Theme.Dim, TextAnchor.MiddleLeft); UiKit.Size(u, h: 28);
            }
            else if (a.target > 1)
            {
                var bar = UiKit.ProgressBar(col, a.Progress / (float)a.target, 8);
                UiKit.Size(bar.transform.parent, h: 8);
            }

            string reward = (a.coins > 0 ? "+" + a.coins : "") + (a.hints > 0 ? "  +" + a.hints + " hint" : "");
            var rl = UiKit.Label(row, done ? "" : (a.target > 1 ? a.Progress + "/" + a.target + "\n" : "") + reward, Theme.Micro + 1, Theme.Dim, TextAnchor.MiddleRight);
            UiKit.Size(rl, w: 150);
        }

        // =====================================================================
        // Settings
        // =====================================================================
        private void BuildSettingsOnce()
        {
            if (_soundToggle != null) return;
            var c = _settingsPage.content;
            Section(c, "Preferences");
            _soundToggle = UiKit.MakeToggle(c, "Sound effects", "Game sounds", !SaveSystem.Muted, on => { SoundChanged?.Invoke(on); });
            _vibToggle = UiKit.MakeToggle(c, "Vibration", "Haptic feedback on bumps", SaveSystem.Haptics, on => { VibrationChanged?.Invoke(on); });
            _glowToggle = UiKit.MakeToggle(c, "Glow effect", "Soft neon glow. Turn off to save battery", SaveSystem.Glow, on => { GlowChanged?.Invoke(on); });

            UiKit.Spacer(c, 12);
            Section(c, "Support");
            UiKit.MakeRow(c, "Send feedback", "Tell us what to improve", "info", () => Application.OpenURL(
                "mailto:" + AppLinks.SupportEmail + "?subject=" + Uri.EscapeDataString("Arrow Breaker feedback " + BuildInfo.Display) +
                "&body=" + Uri.EscapeDataString("\n\n---\nDevice: " + SystemInfo.deviceModel + "\nOS: " + SystemInfo.operatingSystem + "\nVersion: " + BuildInfo.Display)), height: 116);
            UiKit.MakeRow(c, "Privacy policy", "How data is used", "info", () => Application.OpenURL(AppLinks.PrivacyPolicyUrl), height: 116);
            var restore = UiKit.MakeRow(c, "Restore purchases", "Available when in-app purchases launch", "coin", null, height: 116, chevron: false);
            restore.SetEnabled(false);

            UiKit.Spacer(c, 12);
            Section(c, "Data");
            var reset = UiKit.MakeButton(c, "Reset progress", ButtonStyle.Secondary, () =>
                ShowConfirm("Reset progress?", "This deletes your levels, coins, items, records and achievements on this device. It cannot be undone.", "Reset everything", () => ResetProgressConfirmed?.Invoke(), true), null, 104);
            reset.label.color = Theme.Danger;

            UiKit.Spacer(c, 16);
            var v = UiKit.Label(c, "Arrow Breaker  " + BuildInfo.Display + "\n" + BuildInfo.Company, Theme.Micro, Theme.Faint, TextAnchor.MiddleCenter);
            UiKit.Size(v, h: 34);
        }

        public void ShowSettings()
        {
            BuildSettingsOnce();
            _soundToggle.Set(!SaveSystem.Muted);
            _vibToggle.Set(SaveSystem.Haptics);
            _glowToggle.Set(SaveSystem.Glow);
            SetLayer(_settingsPage.root, true);
        }

        // =====================================================================
        // Daily Run (calendar)
        // =====================================================================
        public void ShowDaily()
        {
            if (_dailySelected == 0 || _dailySelected / 100 != Clock.Today / 100) _dailySelected = Clock.Today;
            RebuildDaily();
            SetLayer(_dailyPage.root, true);
        }

        public void RefreshDaily() { if (_dailyPage != null && _dailyPage.root.activeSelf) RebuildDaily(); }

        private void RebuildDaily()
        {
            ClearChildren(_dailyPage.content);
            var c = _dailyPage.content;
            int today = Clock.Today, year = today / 10000, month = (today / 100) % 100;
            int days = Daily.DaysInMonth(year, month);
            int cleared = Daily.ClearedInMonth(year, month);

            var head = UiKit.Row(c, 12, TextAnchor.MiddleLeft);
            UiKit.Size(head, h: 60);
            var mt = UiKit.Label(head, new DateTime(year, month, 1).ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture), Theme.Body, Theme.Text, TextAnchor.MiddleLeft, bold: true);
            UiKit.Size(mt, flexW: 1);
            UiKit.Label(head, cleared + " / " + days, Theme.Body, Theme.Dim, TextAnchor.MiddleRight);

            var bar = UiKit.ProgressBar(c, cleared / (float)days, 10);
            UiKit.Size(bar.transform.parent, h: 10);
            var bl = UiKit.Label(c, Daily.BadgeEarned(year, month) ? "Monthly badge earned" : "Clear every day this month to earn the monthly badge. Missed days can be played until the month ends.",
                Theme.Caption, Theme.Dim, TextAnchor.MiddleLeft, wrap: true);
            UiKit.Size(bl, h: 70);

            // weekday header (Monday first)
            var wk = UiKit.Row(c, 10, TextAnchor.MiddleCenter, expand: true);
            UiKit.Size(wk, h: 40);
            foreach (var d in new[] { "M", "T", "W", "T", "F", "S", "S" }) { var l = UiKit.Label(wk, d, Theme.Caption, Theme.Faint, TextAnchor.MiddleCenter); UiKit.Size(l, flexW: 1); }

            var grid = UiKit.Rect("Grid", c);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 7; g.spacing = new Vector2(10, 10);
            g.cellSize = new Vector2(120, 112);
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var fit = grid.gameObject.AddComponent<GridFit>(); fit.columns = 7; fit.spacing = 10; fit.aspect = 0.95f;

            int offset = ((int)new DateTime(year, month, 1).DayOfWeek + 6) % 7;     // Monday = 0
            for (int i = 0; i < offset; i++) UiKit.Rect("Blank", grid);
            for (int day = 1; day <= days; day++)
            {
                int date = year * 10000 + month * 100 + day;
                bool isCleared = Daily.IsCleared(date), playable = Daily.IsPlayable(date), sel = date == _dailySelected;
                var s = UiKit.Card(grid, isCleared ? Theme.Surface2 : Theme.Surface, sel ? Theme.Text : (date == today ? Theme.Dim : Theme.Line), 14f, raycast: true);
                var col = UiKit.Column(s.content, 0, fitHeight: false, align: TextAnchor.MiddleCenter);
                UiKit.Stretch(col);
                var n = UiKit.Label(col, day.ToString(), Theme.Body - 2, playable ? Theme.Text : Theme.Faint, TextAnchor.MiddleCenter, bold: true);
                UiKit.Size(n, h: 44);
                if (isCleared) { var ck = UiKit.Icon(col, "check", 30, Theme.Dim); UiKit.Size(ck.rectTransform, w: 30, h: 30); }
                else UiKit.Spacer(col, 30);
                int captured = date;
                var tap = UiKit.Tappable(s, () => { if (Daily.IsPlayable(captured)) { _dailySelected = captured; RebuildDaily(); } });
                tap.button.interactable = playable;
            }

            UiKit.Spacer(c, 8);
            DailyDetail(c);
        }

        private void DailyDetail(Transform parent)
        {
            int date = _dailySelected;
            var spec = Daily.SpecFor(date);
            var dt = Clock.FromInt(date);
            bool cleared = Daily.IsCleared(date), resumable = RunSave.Has(Daily.KeyFor(date));

            var s = UiKit.Card(parent, Theme.Surface, Theme.Line, 18f);
            UiKit.Size(s.root, h: 270);
            var col = UiKit.Column(s.content, 8, 30, 26, fitHeight: false, align: TextAnchor.UpperLeft);
            UiKit.Stretch(col);
            var t = UiKit.Label(col, dt.ToString("dddd, d MMMM", System.Globalization.CultureInfo.InvariantCulture), Theme.Body, Theme.Text, TextAnchor.MiddleLeft, bold: true); UiKit.Size(t, h: 44);
            var sub = UiKit.Label(col, DifficultyInfo.Name(spec.difficulty.Value) + "  ·  " + spec.shape + (cleared ? "  ·  Cleared" : ""), Theme.Caption + 1, Theme.Dim, TextAnchor.MiddleLeft); UiKit.Size(sub, h: 36);
            Daily_Reward(col);
            UiKit.Spacer(col, 6);
            var btn = UiKit.MakeButton(col, cleared ? "Play again" : resumable ? "Resume" : "Play", ButtonStyle.Primary, () => { SetLayer(_dailyPage.root, false); DailyDayPicked?.Invoke(date); }, "play", 92);
        }

        private static void Daily_Reward(Transform col)
        {
            var l = UiKit.Label(col, "Reward: 1 hint and 30 coins on the first clear", Theme.Caption, Theme.Faint, TextAnchor.MiddleLeft); UiKit.Size(l, h: 32);
        }
    }
}
