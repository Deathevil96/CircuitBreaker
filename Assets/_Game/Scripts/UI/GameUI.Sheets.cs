using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CircuitBreaker.Core;
using CircuitBreaker.Fx;
using CircuitBreaker.Generation;

namespace CircuitBreaker.UI
{
    public class WinData
    {
        public string title = "Level complete", subtitle = "";
        public List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
        public List<string> rewards = new List<string>();
        public string primaryText = "Next level", secondaryText = "Replay";
        public bool showSecondary = true;
    }

    public partial class GameUI
    {
        private GameObject _winSheet, _failSheet, _getMoreSheet, _confirmSheet, _freeplaySheet, _ovIntroSheet, _ovResultSheet;
        private RectTransform _winBody, _failBody, _getMoreBody, _confirmBody, _freeBody, _ovIntroBody, _ovResultBody;
        private Text _gmBodyText;
        private UiKit.UiButton _gmAd, _gmCoins;

        private sealed class SheetFit : MonoBehaviour
        {
            public RectTransform area; public float max;
            private void Update()
            {
                if (area == null) return;
                var rt = (RectTransform)transform;
                float w = Mathf.Min(max, area.rect.width * 0.92f);
                if (Mathf.Abs(rt.sizeDelta.x - w) > 0.5f) rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
            }
        }

        /// <summary>A centred card that sizes itself to its content. Returns the root and the body to add children to.</summary>
        private GameObject MakeSheet(string name, out RectTransform body, float maxWidth = 860)
        {
            var dim = UiKit.Img(_overlay, Theme.Overlay, null, raycast: true);
            dim.name = "Sheet_" + name;
            UiKit.Stretch(dim.rectTransform);

            var border = UiKit.Rounded(dim.transform, Theme.Line, 26f, raycast: true);
            var br = border.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.sizeDelta = new Vector2(maxWidth, 100);
            var vlg = border.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(2, 2, 2, 2);
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            border.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var fit = border.gameObject.AddComponent<SheetFit>();
            fit.area = _overlay; fit.max = maxWidth;

            var fill = UiKit.Rounded(br, Theme.Surface, 24f);
            var inner = fill.gameObject.AddComponent<VerticalLayoutGroup>();
            inner.padding = new RectOffset(44, 44, 44, 40);
            inner.spacing = 22;
            inner.childControlWidth = true; inner.childControlHeight = true;
            inner.childForceExpandWidth = true; inner.childForceExpandHeight = false;
            body = fill.rectTransform;
            dim.gameObject.SetActive(false);
            return dim.gameObject;
        }

        private void BuildSheets()
        {
            _winSheet = MakeSheet("Win", out _winBody);
            _failSheet = MakeSheet("Fail", out _failBody);
            _getMoreSheet = MakeSheet("GetMore", out _getMoreBody);
            _confirmSheet = MakeSheet("Confirm", out _confirmBody);
            _freeplaySheet = MakeSheet("Freeplay", out _freeBody);
            _ovIntroSheet = MakeSheet("OverloadIntro", out _ovIntroBody);
            _ovResultSheet = MakeSheet("OverloadResult", out _ovResultBody);
        }

        private void SheetTitle(Transform body, string title, string subtitle = null)
        {
            var t = UiKit.Label(body, title, Theme.H1, Theme.Text, TextAnchor.MiddleCenter, bold: true);
            UiKit.Size(t, h: 62);
            if (!string.IsNullOrEmpty(subtitle))
            {
                var s = UiKit.Label(body, subtitle, Theme.Body - 3, Theme.Dim, TextAnchor.MiddleCenter, wrap: true);
                UiKit.Size(s, minH: 40);
            }
        }

        private static void KeyValue(Transform parent, string key, string value)
        {
            var row = UiKit.Row(parent, 12, TextAnchor.MiddleLeft);
            UiKit.Size(row, h: 52);
            var k = UiKit.Label(row, key, Theme.Body - 2, Theme.Dim, TextAnchor.MiddleLeft); UiKit.Size(k, flexW: 1);
            UiKit.Label(row, value, Theme.Body - 2, Theme.Text, TextAnchor.MiddleRight, bold: true);
        }

        public void CloseSheet(GameObject sheet) => SetLayer(sheet, false);

        public void HideSheets()
        {
            foreach (var s in new[] { _winSheet, _failSheet, _getMoreSheet, _confirmSheet, _freeplaySheet, _ovIntroSheet, _ovResultSheet })
                if (s != null) SetLayer(s, false);
        }

        // =====================================================================
        // Win
        // =====================================================================
        public void ShowWin(WinData d)
        {
            ClearChildren(_winBody);
            SheetTitle(_winBody, d.title, d.subtitle);
            UiKit.Divider(_winBody);
            foreach (var kv in d.rows) KeyValue(_winBody, kv.Key, kv.Value);
            if (d.rewards.Count > 0)
            {
                UiKit.Divider(_winBody);
                var rl = UiKit.Label(_winBody, "Rewards", Theme.Caption, Theme.Faint, TextAnchor.MiddleLeft, bold: true); UiKit.Size(rl, h: 34);
                foreach (var r in d.rewards)
                {
                    var l = UiKit.Label(_winBody, r, Theme.Body - 2, Theme.Text, TextAnchor.MiddleLeft); UiKit.Size(l, h: 44);
                }
            }
            UiKit.Spacer(_winBody, 6);
            UiKit.MakeButton(_winBody, d.primaryText, ButtonStyle.Primary, () => NextClicked?.Invoke(), null, 108);
            if (d.showSecondary) UiKit.MakeButton(_winBody, d.secondaryText, ButtonStyle.Ghost, () => ReplayClicked?.Invoke(), null, 80);
            SetLayer(_winSheet, true);
        }

        // =====================================================================
        // Fail
        // =====================================================================
        public void ShowFail(int coins, int refillCost, string adLabel, bool adEnabled)
        {
            ClearChildren(_failBody);
            SheetTitle(_failBody, "Out of hearts", "Continue this board or start it over.");
            UiKit.Spacer(_failBody, 6);
            var ad = UiKit.MakeButton(_failBody, adLabel, ButtonStyle.Primary, () => ContinueAdClicked?.Invoke(), "ad", 108);
            ad.SetEnabled(adEnabled);
            var refill = UiKit.MakeButton(_failBody, "Refill hearts  ·  " + refillCost + " coins", ButtonStyle.Secondary, () => RefillClicked?.Invoke(), "coin", 100);
            refill.SetEnabled(coins >= refillCost);
            UiKit.MakeButton(_failBody, "Restart level", ButtonStyle.Ghost, () => FailRestartClicked?.Invoke(), null, 80);
            SetLayer(_failSheet, true);
        }

        // =====================================================================
        // Get more (rewarded ad / coins)
        // =====================================================================
        public bool GetMoreOpen => _getMoreSheet != null && _getMoreSheet.activeSelf;

        public void ShowGetMore(string title, string body, string adLabel, bool adEnabled, string coinLabel, bool coinEnabled)
        {
            ClearChildren(_getMoreBody);
            SheetTitle(_getMoreBody, title);
            _gmBodyText = UiKit.Label(_getMoreBody, body, Theme.Body - 4, Theme.Dim, TextAnchor.MiddleCenter, wrap: true);
            UiKit.Size(_gmBodyText, minH: 100);
            UiKit.Spacer(_getMoreBody, 4);
            _gmAd = UiKit.MakeButton(_getMoreBody, "", ButtonStyle.Primary, () => GetMoreAdClicked?.Invoke(), "ad", 108);
            _gmCoins = UiKit.MakeButton(_getMoreBody, "", ButtonStyle.Secondary, () => GetMoreCoinsClicked?.Invoke(), "coin", 100);
            UiKit.MakeButton(_getMoreBody, "Close", ButtonStyle.Ghost, CloseGetMore, null, 80);
            UpdateGetMore(body, adLabel, adEnabled, coinLabel, coinEnabled);
            SetLayer(_getMoreSheet, true);
        }

        public void UpdateGetMore(string body, string adLabel, bool adEnabled, string coinLabel, bool coinEnabled)
        {
            if (_gmAd == null || _gmBodyText == null) return;
            _gmBodyText.text = body;
            _gmAd.SetVisible(adLabel != null);
            if (adLabel != null) { EnsureLabel(_gmAd, adLabel); _gmAd.SetEnabled(adEnabled); }
            EnsureLabel(_gmCoins, coinLabel);
            _gmCoins.SetEnabled(coinEnabled);
        }

        /// <summary>Buttons created with empty text have no label yet; add one on first use.</summary>
        private static void EnsureLabel(UiKit.UiButton b, string text)
        {
            if (b.label == null)
            {
                var parent = b.icon != null ? b.icon.transform.parent : b.border.transform;
                Color c = b.style == ButtonStyle.Primary ? Theme.Bg : Theme.Text;
                b.label = UiKit.Label(parent, text, Theme.Body, c, TextAnchor.MiddleCenter, bold: true);
            }
            else b.label.text = text;
            if (b.icon != null) b.icon.color = b.label.color;
        }

        public void CloseGetMore() => SetLayer(_getMoreSheet, false);

        // =====================================================================
        // Confirm
        // =====================================================================
        public void ShowConfirm(string title, string body, string confirmText, Action onConfirm, bool danger)
        {
            ClearChildren(_confirmBody);
            SheetTitle(_confirmBody, title, body);
            UiKit.Spacer(_confirmBody, 6);
            var ok = UiKit.MakeButton(_confirmBody, confirmText, danger ? ButtonStyle.Secondary : ButtonStyle.Primary, () => { SetLayer(_confirmSheet, false); onConfirm?.Invoke(); }, null, 100);
            if (danger) ok.label.color = Theme.Danger;
            UiKit.MakeButton(_confirmBody, "Cancel", ButtonStyle.Ghost, () => SetLayer(_confirmSheet, false), null, 80);
            SetLayer(_confirmSheet, true);
        }

        // =====================================================================
        // Freeplay
        // =====================================================================
        public void ShowFreeplay()
        {
            ClearChildren(_freeBody);
            SheetTitle(_freeBody, "Freeplay", "A fresh board every time. Choose how hard.");
            foreach (var d in DifficultyInfo.All)
            {
                var diff = d;
                var row = UiKit.MakeRow(_freeBody, DifficultyInfo.Name(d), Describe(d), null, () => { SetLayer(_freeplaySheet, false); FreeplayStartClicked?.Invoke(diff); }, null, true, 104);
                if (d == Difficulty.Nightmare) row.label.color = Theme.Danger;
            }
            UiKit.MakeButton(_freeBody, "Close", ButtonStyle.Ghost, () => SetLayer(_freeplaySheet, false), null, 76);
            SetLayer(_freeplaySheet, true);
        }

        private static string Describe(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Easy: return "Relaxed. Small boards.";
                case Difficulty.Medium: return "Steady. A good warm-up.";
                case Difficulty.Hard: return "Tough. Bigger boards, ice and keys.";
                case Difficulty.SuperHard: return "Demanding. Long, winding arrows.";
                case Difficulty.UltraHard: return "Ruthless. Deep dependency chains.";
                default: return "Two hearts. Huge boards.";
            }
        }

        // =====================================================================
        // Overload
        // =====================================================================
        public void ShowOverloadIntro(int best, int bestDate)
        {
            ClearChildren(_ovIntroBody);
            SheetTitle(_ovIntroBody, "Overload", "A score attack. Clear boards before the clock runs out.");
            string[] lines =
            {
                "Each arrow scores points; chaining arrows builds a multiplier.",
                "Clearing a board adds time. Hitting a blocker costs time.",
                "The clock drains faster with every board."
            };
            foreach (var l in lines) { var t = UiKit.Label(_ovIntroBody, l, Theme.Body - 4, Theme.Dim, TextAnchor.MiddleLeft, wrap: true); UiKit.Size(t, minH: 44); }
            UiKit.Divider(_ovIntroBody);
            KeyValue(_ovIntroBody, "Best score", best.ToString("N0"));
            KeyValue(_ovIntroBody, "Set on", Clock.Format(bestDate));
            UiKit.Spacer(_ovIntroBody, 6);
            UiKit.MakeButton(_ovIntroBody, "Start", ButtonStyle.Primary, () => { SetLayer(_ovIntroSheet, false); OverloadStartClicked?.Invoke(); }, "play", 108);
            UiKit.MakeButton(_ovIntroBody, "Close", ButtonStyle.Ghost, () => SetLayer(_ovIntroSheet, false), null, 80);
            SetLayer(_ovIntroSheet, true);
        }

        public void ShowOverloadResult(int score, int rounds, bool record, int best, int coinsEarned)
        {
            ClearChildren(_ovResultBody);
            SheetTitle(_ovResultBody, "Time's up", record ? "New personal best" : null);
            var big = UiKit.Label(_ovResultBody, score.ToString("N0"), 84, Theme.Text, TextAnchor.MiddleCenter, bold: true); UiKit.Size(big, h: 104);
            UiKit.Divider(_ovResultBody);
            KeyValue(_ovResultBody, "Boards cleared", rounds.ToString());
            KeyValue(_ovResultBody, "Best", best.ToString("N0"));
            if (coinsEarned > 0) KeyValue(_ovResultBody, "Coins", "+" + coinsEarned);
            UiKit.Spacer(_ovResultBody, 6);
            UiKit.MakeButton(_ovResultBody, "Play again", ButtonStyle.Primary, () => { SetLayer(_ovResultSheet, false); OverloadAgainClicked?.Invoke(); }, "restart", 108);
            UiKit.MakeButton(_ovResultBody, "Menu", ButtonStyle.Ghost, () => { SetLayer(_ovResultSheet, false); OverloadMenuClicked?.Invoke(); }, null, 80);
            SetLayer(_ovResultSheet, true);
        }

        // =====================================================================
        // Achievement toast
        // =====================================================================
        private RectTransform _toastRect;
        private Text _toastTitle, _toastSub;
        private readonly Queue<string[]> _toastQueue = new Queue<string[]>();
        private float _toastT = -1f;

        public void ClearToasts() { _toastQueue.Clear(); _toastT = -1f; if (_toastRect != null) _toastRect.gameObject.SetActive(false); }

        private void BuildToast()
        {
            var s = UiKit.Card(_safe, Theme.Surface2, Theme.Line, 18f);
            _toastRect = s.root;
            _toastRect.name = "AchievementToast";
            UiKit.Anchor(_toastRect, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 200), new Vector2(820, 112));
            var row = UiKit.Row(s.content, 20, TextAnchor.MiddleLeft);
            UiKit.Stretch(row, 28, 0, 28, 0);
            var ic = UiKit.Icon(row, "medal", 52, Theme.Text); UiKit.Size(ic.rectTransform, w: 52, h: 52);
            var col = UiKit.Column(row, 2, fitHeight: false, align: TextAnchor.MiddleLeft);
            UiKit.Size(col, flexW: 1);
            _toastTitle = UiKit.Label(col, "", Theme.Body - 2, Theme.Text, TextAnchor.MiddleLeft, bold: true); UiKit.Size(_toastTitle, h: 40);
            _toastSub = UiKit.Label(col, "", Theme.Caption - 2, Theme.Dim, TextAnchor.MiddleLeft); UiKit.Size(_toastSub, h: 30);
            _toastRect.gameObject.SetActive(false);
        }

        /// <summary>A generic notice in the same top toast (session gifts, badges).</summary>
        public void ShowNotice(string title, string detail) => _toastQueue.Enqueue(new[] { title, detail });

        public void ShowAchievement(Achievement a)
        {
            string reward = (a.coins > 0 ? "+" + a.coins + " coins" : "") + (a.hints > 0 ? "  +" + a.hints + " hint" : "");
            _toastQueue.Enqueue(new[] { "Achievement unlocked", a.title + (reward.Length > 0 ? "   " + reward : "") });
        }

        private void UpdateToast()
        {
            if (_toastT < 0f)
            {
                if (_toastQueue.Count == 0) return;
                var next = _toastQueue.Dequeue();
                _toastTitle.text = next[1];
                _toastSub.text = next[0];
                _toastT = 0f;
                _toastRect.gameObject.SetActive(true);
                _toastRect.SetAsLastSibling();
                Sfx.Hint();
            }
            _toastT += Time.unscaledDeltaTime;
            float y;
            if (_toastT < 0.35f) y = Mathf.Lerp(200f, -24f, 1f - Mathf.Pow(1f - _toastT / 0.35f, 3f));
            else if (_toastT < 3.6f) y = -24f;
            else y = Mathf.Lerp(-24f, 200f, Mathf.Clamp01((_toastT - 3.6f) / 0.35f));
            _toastRect.anchoredPosition = new Vector2(0, y);
            if (_toastT >= 3.95f) { _toastT = -1f; _toastRect.gameObject.SetActive(false); }
        }
    }
}
