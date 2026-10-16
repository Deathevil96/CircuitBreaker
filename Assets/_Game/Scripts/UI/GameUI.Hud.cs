using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CircuitBreaker.Ads;
using CircuitBreaker.Core;
using CircuitBreaker.Fx;
using CircuitBreaker.Generation;

namespace CircuitBreaker.UI
{
    public partial class GameUI
    {
        private Text _titleText, _tierText, _coinText, _comboText, _statusText, _scoreText, _timeText;
        private readonly List<Image> _hearts = new List<Image>();
        private readonly List<Image> _pips = new List<Image>();
        private RectTransform _heartsRow, _pipRow, _coinChip, _overloadRow, _statusRect, _powerBar, _adBar;
        private Image _timerFill;
        private RectTransform _timerBar;
        private Coroutine _comboRoutine, _statusRoutine;
        private UiKit.UiButton _backBtn, _restartBtn;

        private class PowerButton { public UiKit.UiButton button; public Text count; public Image chip; public Text label; public Image icon; }
        private readonly Dictionary<string, PowerButton> _power = new Dictionary<string, PowerButton>();

        private void BuildHud()
        {
            // ----- top bar: back | title + difficulty | restart -----
            _backBtn = UiKit.MakeIconButton(_safe, "back", () => BackClicked?.Invoke(), 96);
            UiKit.Anchor((RectTransform)_backBtn.border.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -28), new Vector2(96, 96));
            _restartBtn = UiKit.MakeIconButton(_safe, "restart", () => RestartClicked?.Invoke(), 96);
            UiKit.Anchor((RectTransform)_restartBtn.border.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32, -28), new Vector2(96, 96));

            var title = UiKit.Column(_safe, 4, fitHeight: false, align: TextAnchor.UpperCenter);
            UiKit.Anchor(title, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(540, 100));
            _titleText = UiKit.Label(title, "Level 1", 44, Theme.Text, TextAnchor.MiddleCenter, bold: true);
            UiKit.Size(_titleText, h: 54);
            var tierRow = UiKit.Row(title, 14, TextAnchor.MiddleCenter);
            UiKit.Size(tierRow, h: 36);
            _pipRow = UiKit.Row(tierRow, 7, TextAnchor.MiddleCenter);
            _pipRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            for (int i = 0; i < 6; i++)
            {
                var pip = UiKit.Rounded(_pipRow, Theme.Faint, 8f);
                UiKit.Size(pip.rectTransform, w: 12, h: 12);
                _pips.Add(pip);
            }
            _tierText = UiKit.Label(tierRow, "Easy", Theme.Caption, Theme.Dim, TextAnchor.MiddleCenter);

            // ----- second row: hearts + coins (or score + time in Overload) -----
            _heartsRow = UiKit.Row(_safe, 10, TextAnchor.MiddleLeft);
            UiKit.Anchor(_heartsRow, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -146), new Vector2(300, 44));
            for (int i = 0; i < 3; i++)
            {
                var h = UiKit.Icon(_heartsRow, "heart", 44, Theme.Text);
                UiKit.Size(h.rectTransform, w: 44, h: 44);
                _hearts.Add(h);
            }

            _coinChip = UiKit.Row(_safe, 10, TextAnchor.MiddleRight);
            UiKit.Anchor(_coinChip, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -146), new Vector2(300, 44));
            _coinText = UiKit.Label(_coinChip, "0", Theme.Body, Theme.Text, TextAnchor.MiddleRight, bold: true);
            var coinIcon = UiKit.Icon(_coinChip, "coin", 40, Theme.Dim);
            UiKit.Size(coinIcon.rectTransform, w: 40, h: 40);

            // Overload row (score left, time right, thin bar below)
            _overloadRow = UiKit.Rect("OverloadRow", _safe);
            UiKit.Anchor(_overloadRow, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -142), new Vector2(-80, 56));
            _scoreText = UiKit.Label(_overloadRow, "0", Theme.H2, Theme.Text, TextAnchor.MiddleLeft, bold: true);
            UiKit.Stretch(_scoreText.rectTransform);
            _timeText = UiKit.Label(_overloadRow, "60", Theme.H2, Theme.Text, TextAnchor.MiddleRight, bold: true);
            UiKit.Stretch(_timeText.rectTransform);
            _timerBar = UiKit.Rect("TimerBar", _safe);
            UiKit.Anchor(_timerBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -206), new Vector2(-80, 8));
            var tb = UiKit.Rounded(_timerBar, Theme.Line, 4f);
            UiKit.Stretch(tb.rectTransform);
            _timerFill = UiKit.Rounded(_timerBar, Theme.Text, 4f);
            _timerFill.rectTransform.anchorMin = Vector2.zero; _timerFill.rectTransform.anchorMax = Vector2.one;
            _timerFill.rectTransform.offsetMin = _timerFill.rectTransform.offsetMax = Vector2.zero;
            _overloadRow.gameObject.SetActive(false);
            _timerBar.gameObject.SetActive(false);

            // Combo
            _comboText = UiKit.Label(_safe, "", Theme.H2, Theme.Text, TextAnchor.MiddleCenter, bold: true);
            UiKit.Anchor(_comboText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -224), new Vector2(700, 60));
            _comboText.gameObject.SetActive(false);

            // Status line above the power-up bar
            _statusText = UiKit.Label(_safe, "", Theme.Caption + 2, Theme.Dim, TextAnchor.LowerCenter, wrap: true);
            _statusRect = _statusText.rectTransform;
            _statusText.gameObject.SetActive(false);

            // ----- power-up bar -----
            _powerBar = UiKit.Row(_safe, 20, TextAnchor.MiddleCenter, expand: true);
            string[] ids = { "hint", "ruler", "extract", "fit" };
            string[] labels = { "Hint", "Ruler", "Extract", "Fit" };
            string[] icons = { "hint", "ruler", "extract", "fit" };
            Action[] actions = { () => HintClicked?.Invoke(), () => RulerClicked?.Invoke(), () => ExtractClicked?.Invoke(), () => FitClicked?.Invoke() };
            for (int i = 0; i < ids.Length; i++)
            {
                var pb = new PowerButton();
                var col = UiKit.Column(_powerBar, 8, fitHeight: false, align: TextAnchor.UpperCenter);
                var colLe = UiKit.Size(col, h: 168, flexW: 1);
                colLe.preferredWidth = 0; colLe.minWidth = 0;      // equal widths whatever the label says
                pb.button = UiKit.MakeIconButton(col, icons[i], actions[i], 116);
                var btnRt = (RectTransform)pb.button.border.transform;
                var le = pb.button.border.GetComponent<LayoutElement>();
                le.preferredWidth = -1; le.minWidth = -1; le.preferredHeight = 116; le.flexibleWidth = 1;
                pb.icon = pb.button.icon;
                pb.label = UiKit.Label(col, labels[i], Theme.Micro + 2, Theme.Dim, TextAnchor.MiddleCenter);
                UiKit.Size(pb.label, h: 30);
                if (i < 3)
                {
                    pb.chip = UiKit.Rounded(btnRt, Theme.Text, 20f);
                    UiKit.Anchor(pb.chip.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-14, -14), new Vector2(46, 38));
                    pb.count = UiKit.Label(pb.chip.rectTransform, "0", 24, Theme.Bg, TextAnchor.MiddleCenter, bold: true);
                    UiKit.Stretch(pb.count.rectTransform);
                }
                _power[ids[i]] = pb;
            }
        }

        // ---------- banner ad slot (AdMob policy) ----------
        // The banner never overlaps gameplay or controls: it owns an opaque strip at the very bottom of the screen
        // (extended over the home-indicator area), and the camera and power-up bar are laid out above it with a gap.

        private void BuildAdBar(Transform canvasRoot)
        {
            var bar = UiKit.Img(canvasRoot, Theme.Bg, null, raycast: true);
            bar.name = "AdBanner";
            _adBar = bar.rectTransform;
            _adBar.anchorMin = new Vector2(0, 0); _adBar.anchorMax = new Vector2(1, 0); _adBar.pivot = new Vector2(0.5f, 0);
            _adBar.anchoredPosition = Vector2.zero;
            var edge = UiKit.Img(_adBar, Theme.Line);
            edge.rectTransform.anchorMin = new Vector2(0, 1); edge.rectTransform.anchorMax = new Vector2(1, 1);
            edge.rectTransform.pivot = new Vector2(0.5f, 1); edge.rectTransform.sizeDelta = new Vector2(0, 2);
            bool isStub = AdService.Provider != null && AdService.Provider.GetType().Name == "StubAdProvider";
            if (isStub)
            {
                var t = UiKit.Label(_adBar, "Test banner ad  (320x50)", Theme.Caption, Theme.Faint, TextAnchor.UpperCenter);
                UiKit.Anchor(t.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(0, 40));
            }
            UpdateAdBarSize();
        }

        private void UpdateAdBarSize()
        {
            if (_adBar == null) return;
            _adBar.sizeDelta = new Vector2(0, _bannerUnits + SafeBottomUnits());
        }

        private void ApplyBannerLayout()
        {
            bool show = AdService.BannerVisible;
            _bannerApplied = show;
            float h = show ? _bannerUnits : 0f;
            UpdateAdBarSize();
            if (_adBar != null) _adBar.gameObject.SetActive(show);
            if (_overlay != null) _overlay.offsetMin = new Vector2(0, h);

            // Power-up bar sits 32 units above the banner (a clear gap so taps never land on the ad).
            UiKit.Anchor(_powerBar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 32 + h), new Vector2(-64, 168));
            UiKit.Anchor(_statusRect, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 214 + h), new Vector2(900, 80));
        }

        // ---------- HUD API ----------

        /// <summary>Title line, difficulty pips and label. `difficulty` null hides the pips.</summary>
        public void SetHeader(string title, Difficulty? difficulty, string tierText)
        {
            _titleText.text = title;
            bool has = difficulty.HasValue;
            _pipRow.gameObject.SetActive(has);
            if (has)
            {
                int n = DifficultyInfo.Pips(difficulty.Value);
                bool nightmare = difficulty.Value == Difficulty.Nightmare;
                for (int i = 0; i < _pips.Count; i++) _pips[i].color = i < n ? (nightmare ? Theme.Danger : Theme.Text) : Theme.Faint;
            }
            _tierText.text = tierText;
            _tierText.color = has && difficulty.Value == Difficulty.Nightmare ? Theme.Danger : Theme.Dim;
        }

        public void SetHearts(int current, int max, bool unlimited)
        {
            _heartsRow.gameObject.SetActive(!unlimited || !_overloadMode);
            for (int i = 0; i < _hearts.Count; i++)
            {
                _hearts[i].gameObject.SetActive(i < max && !unlimited);
                _hearts[i].color = i < current ? Theme.Text : Theme.Faint;
            }
        }

        private bool _overloadMode;

        public void SetOverloadMode(bool on)
        {
            _overloadMode = on;
            _overloadRow.gameObject.SetActive(on);
            _timerBar.gameObject.SetActive(on);
            _heartsRow.gameObject.SetActive(!on);
            _coinChip.gameObject.SetActive(!on);
            _restartBtn.border.gameObject.SetActive(!on);
            foreach (var id in new[] { "hint", "ruler", "extract" })
                if (_power.TryGetValue(id, out var p)) p.button.SetEnabled(!on);     // dimmed, so the bar keeps its shape
        }

        public void SetOverload(int score, float timeLeft, float timeMax)
        {
            _scoreText.text = score.ToString("N0");
            _timeText.text = Mathf.Max(0f, timeLeft).ToString("0.0");
            _timeText.color = timeLeft <= 10f ? Theme.Danger : Theme.Text;
            _timerFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(timeLeft / Mathf.Max(1f, timeMax)), 1f);
        }

        public void SetCoins(int n) => _coinText.text = n.ToString("N0");

        public void SetPower(string id, int count, bool locked, int unlockLevel, bool active = false)
        {
            if (!_power.TryGetValue(id, out var p)) return;
            p.button.SetEnabled(!_overloadMode || id == "fit");
            p.icon.color = locked ? Theme.Faint : Theme.Text;
            p.button.border.color = active ? Theme.Text : Theme.Line;
            p.label.text = locked ? "Level " + unlockLevel : p.label.text.StartsWith("Level") ? PowerLabel(id) : p.label.text;
            if (!locked) p.label.text = PowerLabel(id);
            p.label.color = locked ? Theme.Faint : Theme.Dim;
            if (p.chip != null)
            {
                p.chip.gameObject.SetActive(!locked);
                p.count.text = count > 0 ? count.ToString() : "+";
                p.chip.color = count > 0 ? Theme.Text : Theme.Surface2;
                p.count.color = count > 0 ? Theme.Bg : Theme.Text;
            }
        }

        private static string PowerLabel(string id) => id == "hint" ? "Hint" : id == "ruler" ? "Ruler" : id == "extract" ? "Extract" : "Fit";

        public void ShowCombo(int combo)
        {
            if (combo < 2) { _comboText.gameObject.SetActive(false); return; }
            if (_comboRoutine != null) StopCoroutine(_comboRoutine);
            _comboRoutine = StartCoroutine(ComboRoutine(combo));
        }

        private IEnumerator ComboRoutine(int combo)
        {
            _comboText.gameObject.SetActive(true);
            _comboText.text = "x" + combo;
            float t = 0;
            while (t < 1.2f)
            {
                t += Time.unscaledDeltaTime;
                _comboText.rectTransform.localScale = Vector3.one * (t < 0.15f ? Mathf.Lerp(1.3f, 1f, t / 0.15f) : 1f);
                _comboText.color = Theme.A(Theme.Text, t > 0.8f ? 1f - (t - 0.8f) / 0.4f : 1f);
                yield return null;
            }
            _comboText.gameObject.SetActive(false);
        }

        /// <summary>A short message above the power-up bar.</summary>
        public void Toast(string text, float seconds = 2.4f)
        {
            if (_statusRoutine != null) StopCoroutine(_statusRoutine);
            _statusRoutine = StartCoroutine(StatusRoutine(text, seconds));
        }

        // ---------- loading overlay: shown only when a board takes longer than a moment ----------
        private GameObject _loading;
        private Text _loadingText;
        private string _loadingBase;
        private float _loadingStart;

        public void ShowLoading(string text)
        {
            if (_loading == null)
            {
                var root = UiKit.Rect("Loading", _safe);
                UiKit.Stretch(root);
                UiKit.Img(root, Theme.A(Theme.Bg, 0.92f), null, true);
                _loadingText = UiKit.Label(root, "", Theme.Body, Theme.Dim, TextAnchor.MiddleCenter);
                UiKit.Stretch(_loadingText.rectTransform);
                _loading = root.gameObject;
            }
            if (!_loading.activeSelf) _loadingStart = Time.unscaledTime;
            _loadingBase = text;
            _loading.SetActive(true);
            _loading.transform.SetAsLastSibling();
            AnimateLoading();
        }

        public void HideLoading()
        {
            if (_loading != null && _loading.activeSelf) _loading.SetActive(false);
        }

        private void AnimateLoading()
        {
            if (_loading == null || !_loading.activeSelf) return;
            int dots = (int)((Time.unscaledTime - _loadingStart) * 3f) % 4;
            _loadingText.text = _loadingBase + new string('.', dots);
        }

        private void LateUpdate() => AnimateLoading();

        public void HoldBanner(string text)
        {
            if (_statusRoutine != null) StopCoroutine(_statusRoutine);
            _statusText.text = text;
            _statusText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private IEnumerator StatusRoutine(string text, float seconds)
        {
            _statusText.text = text;
            _statusText.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(seconds);
            _statusText.gameObject.SetActive(false);
        }

        public void SetTopButtonsVisible(bool on)
        {
            _backBtn.border.gameObject.SetActive(on);
            _restartBtn.border.gameObject.SetActive(on && !_overloadMode);
        }
    }
}
