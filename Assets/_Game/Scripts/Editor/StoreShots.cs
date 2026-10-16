#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using CircuitBreaker.Core;
using CircuitBreaker.Generation;
using CircuitBreaker.UI;

namespace CircuitBreaker.EditorTools
{
    /// <summary>
    /// Builds a realistic sandbox save and captures the Play Store screenshots (1080x1920).
    /// Play Mode only. Run from execute_code or a menu: nothing touches the real save.
    /// </summary>
    public static class StoreShots
    {
        public const string Dir = "PlayStore/Screenshots";
        public const string GraphicsDir = "PlayStore/Graphics";

        public static string Run()
        {
            var gm = GameManager.Instance;
            if (gm == null) return "enter Play Mode first";
            var log = new StringBuilder();
            GameManager.SynchronousLoading = true;
            SaveSystem.EnterSandbox();
            var hidden = HideEditorOnlyUi();
            try
            {
                Populate(gm);
                string dir = Path.Combine(Directory.GetCurrentDirectory(), Dir);
                Directory.CreateDirectory(dir);

                // 1. menu
                gm.StartClassic(SaveSystem.Progress);
                gm.OpenMenu();
                gm.UI.ClearToasts();   // first-run achievement pop-up
                log.AppendLine(CaptureTool.Capture(Path.Combine(dir, "01_menu.png")));

                // 2-4. shaped boards, part way through
                Board(gm, 21, 0.25f); log.AppendLine(CaptureTool.Capture(Path.Combine(dir, "02_board_heart.png")));
                Board(gm, 25, 0.3f); log.AppendLine(CaptureTool.Capture(Path.Combine(dir, "03_board_human.png")));
                Board(gm, 27, 0.2f); log.AppendLine(CaptureTool.Capture(Path.Combine(dir, "04_board_butterfly.png")));

                // 5. win sheet
                gm.UI.HideAllOverlays();
                var win = new WinData { title = "Perfect clear", subtitle = "Level 40  ·  Hard" };
                win.rows.Add(new KeyValuePair<string, string>("Coins", "+88"));
                win.rows.Add(new KeyValuePair<string, string>("Best combo", "x7"));
                win.rows.Add(new KeyValuePair<string, string>("Depth", "9"));
                win.rows.Add(new KeyValuePair<string, string>("First attempt", "Yes"));
                win.rewards.Add("Hard level cleared"); win.rewards.Add("+1 hint");
                gm.UI.ShowWin(win);
                log.AppendLine(CaptureTool.Capture(Path.Combine(dir, "05_win.png")));

                // 6. records
                gm.UI.HideAllOverlays();
                gm.UI.ShowRecords();
                log.AppendLine(CaptureTool.Capture(Path.Combine(dir, "06_records.png")));

                // 7. daily run
                gm.UI.HideAllOverlays();
                gm.UI.ShowDaily();
                log.AppendLine(CaptureTool.Capture(Path.Combine(dir, "07_daily.png")));

                // 8. freeplay
                gm.UI.HideAllOverlays();
                gm.UI.ShowFreeplay();
                log.AppendLine(CaptureTool.Capture(Path.Combine(dir, "08_freeplay.png")));

                // 9. levels
                gm.UI.HideAllOverlays();
                gm.UI.ShowLevels(SaveSystem.Progress, SaveSystem.Progress);
                log.AppendLine(CaptureTool.Capture(Path.Combine(dir, "09_levels.png")));

                // 10. overload
                gm.UI.HideAllOverlays();
                gm.StartOverload();
                log.AppendLine(CaptureTool.Capture(Path.Combine(dir, "10_overload.png")));

                // 11. a nightmare board
                gm.UI.HideAllOverlays();
                SaveSystem.Progress = 100;
                gm.StartClassic(100, null, true);
                log.AppendLine(CaptureTool.Capture(Path.Combine(dir, "11_nightmare.png")));
            }
            finally
            {
                foreach (var go in hidden) if (go != null) go.SetActive(true);
                SaveSystem.ExitSandbox();
                GameManager.SynchronousLoading = false;
            }
            return log.ToString();
        }

        /// <summary>The DBG tab and the "test banner" label are editor/test-only; they must not appear in store images.</summary>
        private static List<GameObject> HideEditorOnlyUi()
        {
            var list = new List<GameObject>();
            var dbg = GameObject.Find("Btn_DBG");
            if (dbg != null) { dbg.SetActive(false); list.Add(dbg); }
            foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.text.StartsWith("Test banner ad")) { t.gameObject.SetActive(false); list.Add(t.gameObject); }
            return list;
        }

        /// <summary>1024x500 feature graphic: the title on the left, a heart-shaped board on the right.</summary>
        public static string FeatureGraphic()
        {
            var gm = GameManager.Instance;
            if (gm == null) return "enter Play Mode first";
            GameManager.SynchronousLoading = true;
            SaveSystem.EnterSandbox();
            try
            {
                gm.ResetEverything();
                SaveSystem.Progress = 30;
                gm.UI.HideAllOverlays();
                gm.StartClassic(21, null, true);
                var order = Solver.SolveOrder(gm.Level);
                if (order != null) gm.Board.RestoreRemoved(order.GetRange(0, order.Count / 6));
                string path = Path.Combine(Directory.GetCurrentDirectory(), GraphicsDir, "feature_graphic_1024x500.png");
                var temp = new List<GameObject>();
                string result = CaptureTool.CaptureWorld(path, 1024, 500, cam =>
                {
                    cam.orthographicSize = 8.6f;
                    cam.transform.position = new Vector3(-9.5f, 0f, -10f);
                    temp.Add(Text("ARROW BREAKER", new Vector3(-26f, 1.4f, 0), 1.5f, Color.white, true));
                    temp.Add(Text("Circuit puzzle", new Vector3(-26f, -0.9f, 0), 0.85f, new Color(0.62f, 0.62f, 0.66f), false));
                });
                foreach (var go in temp) UnityEngine.Object.DestroyImmediate(go);
                return result;
            }
            finally { SaveSystem.ExitSandbox(); GameManager.SynchronousLoading = false; }
        }

        private static GameObject Text(string text, Vector3 pos, float size, Color color, bool bold)
        {
            var go = new GameObject("StoreText");
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = CircuitBreaker.Fx.UiFont.Get();
            go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            go.GetComponent<MeshRenderer>().sortingOrder = 100;
            tm.text = text;
            tm.fontSize = 120;
            tm.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            tm.characterSize = size * 0.1f;
            tm.anchor = TextAnchor.MiddleLeft;
            tm.color = color;
            return go;
        }

        private static void Board(GameManager gm, int level, float clearedFraction)
        {
            gm.UI.HideAllOverlays();
            SaveSystem.Progress = Mathf.Max(SaveSystem.Progress, level + 1);
            gm.StartClassic(level, null, true);
            var order = Solver.SolveOrder(gm.Level);
            if (order != null) gm.Board.RestoreRemoved(order.GetRange(0, Mathf.RoundToInt(order.Count * clearedFraction)));
        }

        private static void Populate(GameManager gm)
        {
            gm.ResetEverything();
            for (int day = 9; day <= 16; day++)
            {
                Clock.Override = new DateTime(2026, 10, day, 18, 0, 0);
                Stats.RegisterSessionStart();
                int wins = day == 12 ? 6 : 3;
                for (int w = 0; w < wins; w++)
                {
                    int lvl = (day - 9) * 3 + w + 1;
                    Stats.RegisterWin(lvl, LevelCatalog.DifficultyOf(lvl), w != 1, w != 2);
                    SaveSystem.Progress = lvl + 1;
                }
                if (day == 13) Stats.RegisterLoss();
            }
            for (int day = 9; day <= 15; day++) Daily.MarkCleared(20261000 + day);
            Stats.DailyClears = 7;
            Stats.RegisterOverload(3480, 5);
            SaveSystem.Coins = 412;
            SaveSystem.SetStock("hint", 2); SaveSystem.SetStock("ruler", 1); SaveSystem.SetStock("extract", 1);
            Clock.Override = new DateTime(2026, 10, 16, 18, 0, 0);
            Achievements.Evaluate();
        }
    }
}
#endif
