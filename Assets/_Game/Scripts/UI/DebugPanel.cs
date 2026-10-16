#if UNITY_EDITOR || DEVELOPMENT_BUILD || CB_DEBUG
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CircuitBreaker.Core;
using CircuitBreaker.Fx;

namespace CircuitBreaker.UI
{
    /// <summary>
    /// Debug overlay for the Editor, development and debug builds. Open with the DBG tab (or F1 in the Editor).
    /// Includes the level test tools: a background test of levels 1-1000 and a visible auto-play through all levels.
    /// This file is removed from the release branch.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        private GameUI _ui;
        private GameObject _panel;
        private Text _stats, _log;
        private float _nextRefresh, _fps;
        private readonly StringBuilder _sb = new StringBuilder();

        // auto-play through levels
        private bool _auto;
        private int _autoLevel, _autoPassed;
        private string _autoStatus = "auto-play idle";

        public void Build(GameUI ui)
        {
            _ui = ui;
            var toggle = UiKit.MakeButton(ui.SafeRoot, "DBG", ButtonStyle.Secondary, Toggle, null, 56, Theme.Micro + 2);
            UiKit.Anchor((RectTransform)toggle.border.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(90, 56));

            var card = UiKit.Card(ui.SafeRoot, Theme.A(Theme.Bg, 0.96f), Theme.Dim, 18f, raycast: true);
            _panel = card.root.gameObject;
            var r = card.root;
            r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(0.62f, 1); r.pivot = new Vector2(0, 0.5f);
            r.offsetMin = new Vector2(96, 150); r.offsetMax = new Vector2(0, -150);

            var col = UiKit.ScrollColumn(card.content, 10, 18, 18);
            _stats = UiKit.Label(col, "", 22, Theme.Text, TextAnchor.UpperLeft, wrap: true);
            UiKit.Size(_stats, minH: 420);

            Section(col, "Levels");
            Pair(col, "Test 1-1000", () => StartTest(1, 1000), "Stop test", LevelTester.Stop);
            Pair(col, "Auto-play from here", StartAutoPlay, "Stop auto-play", () => _auto = false);
            Pair(col, "Prev", () => GameManager.Instance.DebugJump(-1), "Next", () => GameManager.Instance.DebugJump(1));
            Pair(col, "-10", () => GameManager.Instance.DebugJump(-10), "+10", () => GameManager.Instance.DebugJump(10));
            Pair(col, "-100", () => GameManager.Instance.DebugJump(-100), "+100", () => GameManager.Instance.DebugJump(100));
            Pair(col, "Reload", () => GameManager.Instance.DebugReload(), "Unlock all", () => GameManager.Instance.DebugUnlockAll());

            Section(col, "Board");
            Pair(col, "Auto-solve", () => GameManager.Instance.DebugAutoSolve(), "Force win", () => GameManager.Instance.DebugForceWin());
            Pair(col, "Force fail", () => GameManager.Instance.DebugForceFail(), "Copy info", CopyInfo);

            Section(col, "Economy and ads");
            Pair(col, "+500 coins", () => GameManager.Instance.DebugAddCoins(500), "Reset save", () => GameManager.Instance.DebugResetSave());
            Pair(col, "Empty stock", () => GameManager.Instance.DebugEmptyStock(), "Fill stock", () => GameManager.Instance.DebugFillStock());
            Pair(col, "Reset ad CD", () => GameManager.Instance.DebugResetAdCooldown(), "New session", () => GameManager.Instance.DebugNewSession());
            Pair(col, "Fake +1 day", () => GameManager.Instance.DebugFakeNextDay(), "Unlock ach.", () => GameManager.Instance.DebugUnlockAchievements());

            Section(col, "Log");
            _log = UiKit.Label(col, "", 20, Theme.Dim, TextAnchor.UpperLeft, wrap: true);
            UiKit.Size(_log, minH: 240);
            _panel.SetActive(false);
        }

        private static void Section(Transform parent, string text)
        {
            var l = UiKit.Label(parent, text.ToUpperInvariant(), Theme.Micro, Theme.Faint, TextAnchor.MiddleLeft, bold: true);
            UiKit.Size(l, h: 34);
        }

        private static void Pair(Transform parent, string a, System.Action actA, string b, System.Action actB)
        {
            var row = UiKit.Row(parent, 10, TextAnchor.MiddleCenter, expand: true);
            UiKit.Size(row, h: 70);
            var ba = UiKit.MakeButton(row, a, ButtonStyle.Secondary, actA, null, 70, Theme.Micro + 2);
            UiKit.Size(ba.border, flexW: 1).preferredWidth = 0;
            var bb = UiKit.MakeButton(row, b, ButtonStyle.Secondary, actB, null, 70, Theme.Micro + 2);
            UiKit.Size(bb.border, flexW: 1).preferredWidth = 0;
        }

        public void Toggle()
        {
            _panel.SetActive(!_panel.activeSelf);
            _ui.DebugOpen = _panel.activeSelf;
            Refresh();
        }

        // ---------- level test ----------
        private void StartTest(int from, int to)
        {
            // Phones: two worker threads keep it cool; the Editor can use more.
            int degree = Application.isEditor ? 4 : 2;
            LevelTester.Start(from, to, degree);
            GameManager.Instance.Log($"TEST levels {from}-{to} started ({degree} threads)");
        }

        // ---------- visible auto-play ----------
        private void StartAutoPlay()
        {
            if (_auto) return;
            var gm = GameManager.Instance;
            _autoLevel = Mathf.Max(1, gm.Mode == GameMode.Classic ? gm.LevelIndex : SaveSystem.Progress);
            _autoPassed = 0;
            _auto = true;
            StartCoroutine(AutoPlayRoutine());
            gm.Log("AUTO-PLAY from level " + _autoLevel);
        }

        private IEnumerator AutoPlayRoutine()
        {
            var gm = GameManager.Instance;
            while (_auto)
            {
                int level = _autoLevel;
                gm.StartClassic(level, null, true);          // debug boards never touch the real save
                gm.UI.HideAllOverlays();
                float wait = 0f;
                while ((gm.Level == null || gm.Level.index != level || gm.InputLocked) && wait < 20f)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (wait >= 20f) { _autoStatus = $"auto-play FAILED: level {level} did not load in 20 s"; gm.Log(_autoStatus); _auto = false; break; }

                gm.UI.HideAllOverlays();
                while (gm.Board.Model.Remaining > 0 && _auto)
                {
                    var movable = gm.Board.Model.MovableArrows();
                    if (movable.Count == 0)
                    {
                        _autoStatus = $"auto-play FAILED: level {level} is stuck with {gm.Board.Model.Remaining} arrows";
                        gm.Log(_autoStatus);
                        _auto = false;
                        break;
                    }
                    int taps = Mathf.Min(movable.Count, 6);      // several arrows per frame keeps big boards quick
                    for (int i = 0; i < taps; i++)
                    {
                        var a = movable[i];
                        gm.Board.Tap(gm.Board.GridToWorld(a.cells[a.cells.Count / 2]));
                    }
                    yield return null;
                }
                if (!_auto) break;
                _autoPassed++;
                _autoStatus = $"auto-play: passed {_autoPassed}, now level {level + 1}";
                _autoLevel = level + 1;
                yield return new WaitForSecondsRealtime(0.2f);
            }
            if (_autoStatus.StartsWith("auto-play:")) _autoStatus += " (stopped)";
        }

        private void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) Toggle();
            if (_panel != null && _panel.activeSelf && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.3f;
                Refresh();
            }
        }

        private void Refresh()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Level == null) return;
            _stats.text = gm.DebugInfo(_fps) + "\n" + LevelTester.Summary() + _autoStatus;
            _sb.Clear();
            foreach (var line in gm.LogLines) _sb.AppendLine(line);
            _log.text = _sb.ToString();
        }

        private void CopyInfo()
        {
            var gm = GameManager.Instance;
            GUIUtility.systemCopyBuffer = gm.DebugInfo(_fps) + "\n" + LevelTester.Summary() + _autoStatus + "\n" + string.Join("\n", gm.LogLines);
            gm.Log("debug info copied to clipboard");
        }
    }
}
#endif
