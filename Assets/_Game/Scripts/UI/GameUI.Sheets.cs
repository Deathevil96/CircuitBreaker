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

            var border = UiKit.Rounded(dim.transform, Theme.Line, 34f, raycast: true);
            border.gameObject.AddComponent<UiKit.PopIn>();
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

            var fill = UiKit.Rounded(br, Theme.Surface, 32f);
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
            _updateSheet = MakeSheet("UpdateRequired", out _updateBody);
            _rateSheet = MakeSheet("Rate", out _rateBody);
        }

        // =====================================================================
        // Mandatory update (cannot be dismissed)
        // =====================================================================
        private GameObject _updateSheet;
        private RectTransform _updateBody;
        public event Action UpdateClicked;
        public bool UpdateShown => _updateSheet != null && _updateSheet.activeSelf;

        public void ShowWall(string title, string message, string button)
        {
            ClearChildren(_updateBody);
            SheetTitle(_updateBody, title, message);
            UiKit.Spacer(_updateBody, 6);
            UiKit.MakeButton(_updateBody, button, ButtonStyle.Primary, () => UpdateClicked?.Invoke(), null, 108);
            _updateSheet.transform.SetAsLastSibling();
            SetLayer(_updateSheet, true);
        }

        public void HideWall() { if (UpdateShown) SetLayer(_updateSheet, false); }

        // =====================================================================
        // Rating prompt
        // =====================================================================
        private GameObject _rateSheet;
        private RectTransform _rateBody;

        public void ShowRate(Action onRate, Action onLater)
        {
            ClearChildren(_rateBody);
            SheetTitle(_rateBody, "Enjoying Arrow Breaker?", "A quick rating on Google Play helps a small studio a lot.");
            UiKit.Spacer(_rateBody, 6);
            UiKit.MakeButton(_rateBody, "Rate on Google Play", ButtonStyle.Primary, () => { SetLayer(_rateSheet, false); onRate?.Invoke(); }, null, 108);
            UiKit.MakeButton(_rateBody, "Not now", ButtonStyle.Ghost, () => { SetLayer(_rateSheet, false); onLater?.Invoke(); }, null, 80);
            SetLayer(_rateSheet, true);
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
        /// <summary>"Well done" pop-in and confetti from both screen edges, drawn above everything.</summary>
        public void Celebrate(string message = "Well done")
        {
            Celebration.Play(_canvasRect, message);
        }

        public void ShowWin(WinData d)
        {
            ClearChildren(_winBody);

            // glow behind the header, ignored by the layout
            var glow = UiKit.Img(_winBody, Theme.A(Theme.Accent, 0.16f), ProcSprites.GlowDot);
            glow.raycastTarget = false;
            glow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0, 1);
            glow.rectTransform.pivot = new Vector2(0, 1);
            glow.rectTransform.sizeDelta = new Vector2(620, 420);
            glow.rectTransform.anchoredPosition = new Vector2(-120, 140);

            // header: title left, coin reward right
            string coins = null;
            var tiles = new List<KeyValuePair<string, string>>();
            foreach (var kv in d.rows) { if (kv.Key == "Coins") coins = kv.Value; else tiles.Add(kv); }

            var head = UiKit.Row(_winBody, 16, TextAnchor.MiddleLeft);
            UiKit.Size(head, h: 120);
            var hc = UiKit.Column(head, 4, fitHeight: false, align: TextAnchor.MiddleLeft);
            UiKit.Size(hc, flexW: 1);
            var t = UiKit.Label(hc, d.title, Theme.H1 + 4, Theme.Accent, TextAnchor.MiddleLeft, bold: true); UiKit.Size(t, h: 66);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sub = UiKit.Label(hc, d.subtitle ?? "", Theme.Caption, Theme.Dim, TextAnchor.MiddleLeft); UiKit.Size(sub, h: 36);
            if (coins != null)
            {
                var cc = UiKit.Column(head, 0, fitHeight: false, align: TextAnchor.MiddleRight);
                var cv = UiKit.Label(cc, coins, 54, CyberTheme.Arrow(3), TextAnchor.MiddleRight, bold: true); UiKit.Size(cv, h: 66);
                cv.horizontalOverflow = HorizontalWrapMode.Overflow;
                var cl = UiKit.Label(cc, "coins", Theme.Micro, Theme.Dim, TextAnchor.MiddleRight); UiKit.Size(cl, h: 30);
            }

            // stat tiles side by side
            if (tiles.Count > 0)
            {
                var row = UiKit.Row(_winBody, 14, TextAnchor.MiddleLeft, expand: true);
                UiKit.Size(row, h: 112);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
                foreach (var kv in tiles)
                {
                    var tile = UiKit.Card(row, Theme.Surface2, Theme.Line, 18f);
                    UiKit.Size(tile.root, flexW: 1).preferredWidth = 0;
                    var col = UiKit.Column(tile.content, 2, fitHeight: false, align: TextAnchor.MiddleCenter);
                    UiKit.Stretch(col);
                    var v = UiKit.Label(col, kv.Value, Theme.H2 - 4, Theme.Text, TextAnchor.MiddleCenter, bold: true); UiKit.Size(v, h: 46);
                    var k = UiKit.Label(col, kv.Key, Theme.Micro - 2, Theme.Dim, TextAnchor.MiddleCenter); UiKit.Size(k, h: 28);
                }
            }

            // rewards as chips
            if (d.rewards.Count > 0)
            {
                var chips = UiKit.Column(_winBody, 10, fitHeight: true, align: TextAnchor.UpperLeft);
                foreach (var r in d.rewards)
                {
                    var chip = UiKit.Rounded(chips, Color.Lerp(Theme.Surface, CyberTheme.Arrow(2), 0.10f), 30f);
                    var g = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
                    g.padding = new RectOffset(26, 26, 8, 8); g.spacing = 12;
                    g.childAlignment = TextAnchor.MiddleLeft;
                    g.childControlWidth = g.childControlHeight = true; g.childForceExpandWidth = g.childForceExpandHeight = false;
                    chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    var star = UiKit.Icon(chip.rectTransform, "star", 30, CyberTheme.Arrow(2)); UiKit.Size(star.rectTransform, w: 30, h: 30);
                    var rl = UiKit.Label(chip.rectTransform, r, Theme.Caption, Theme.Text, TextAnchor.MiddleLeft); rl.horizontalOverflow = HorizontalWrapMode.Overflow;
                    UiKit.Size(chip.rectTransform, h: 56);
                }
            }

            UiKit.Spacer(_winBody, 4);
            UiKit.MakeButton(_winBody, d.primaryText, ButtonStyle.Primary, () => NextClicked?.Invoke(), "next", 112, Theme.H2 - 4);
            if (d.showSecondary) UiKit.MakeButton(_winBody, d.secondaryText, ButtonStyle.Ghost, () => ReplayClicked?.Invoke(), null, 80);
            SetLayer(_winSheet, true);
        }

        // =====================================================================
        // Fail
        // =====================================================================
        public void ShowFail(int coins, int refillCost, string adLabel, bool adEnabled, bool refillUsed = false, bool ironMan = false)
        {
            ClearChildren(_failBody);
            SheetTitle(_failBody, ironMan ? "Iron man down" : "Out of hearts", ironMan ? "One mistake ends an iron man run. No continues." : "Continue this board or start it over.");
            UiKit.Spacer(_failBody, 6);
            if (!ironMan)
            {
                var ad = UiKit.MakeButton(_failBody, adLabel, ButtonStyle.Primary, () => ContinueAdClicked?.Invoke(), "ad", 108);
                ad.SetEnabled(adEnabled);
                var refill = UiKit.MakeButton(_failBody, refillUsed ? "Refill used this session" : "Refill hearts  ·  " + refillCost + " coins", ButtonStyle.Secondary, () => RefillClicked?.Invoke(), "coin", 100);
                refill.SetEnabled(!refillUsed && coins >= refillCost);
            }
            UiKit.MakeButton(_failBody, "Restart level", ironMan ? ButtonStyle.Primary : ButtonStyle.Ghost, () => FailRestartClicked?.Invoke(), null, ironMan ? 108 : 80);
            UiKit.MakeButton(_failBody, "Main menu", ButtonStyle.Ghost, () => FailMenuClicked?.Invoke(), null, 80);
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
                Color tc = TierColor(d);
                row.fill.color = Color.Lerp(Theme.Surface, tc, 0.12f);
                row.border.color = Theme.A(tc, 0.55f);
                row.label.color = d == Difficulty.Nightmare ? Theme.Danger : Theme.Text;
            }
            UiKit.MakeButton(_freeBody, "Close", ButtonStyle.Ghost, () => SetLayer(_freeplaySheet, false), null, 76);
            SetLayer(_freeplaySheet, true);
        }

        private static Color TierColor(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Easy: return CyberTheme.Arrow(2);
                case Difficulty.Medium: return CyberTheme.Arrow(0);
                case Difficulty.Hard: return CyberTheme.Arrow(3);
                case Difficulty.SuperHard: return CyberTheme.Arrow(4);
                case Difficulty.UltraHard: return CyberTheme.Arrow(1);
                default: return Theme.Danger;
            }
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

    /// <summary>
    /// Level-complete celebration on the top of the UI: a "Well done" pop-in and confetti bursting in from both
    /// sides of the screen. Purely visual, never blocks input, destroys itself when finished.
    /// </summary>
    public class Celebration : MonoBehaviour
    {
        private const float Duration = 2.4f;
        private const int PiecesPerSide = 34;
        private const float Gravity = 2600f;   // canvas units / s^2

        private struct Piece
        {
            public RectTransform rt;
            public Image img;
            public Vector2 pos, vel;
            public float spin, angle, flip, flipSpeed, life, drag;
        }

        private Piece[] _pieces;
        private RectTransform _label;
        private Text _text;
        private float _t;
        private Vector2 _size;

        public static void Play(RectTransform canvas, string message)
        {
            var root = UiKit.Rect("Celebration", canvas);
            UiKit.Stretch(root);
            root.SetAsLastSibling();
            root.gameObject.AddComponent<Celebration>().Init(root, message);
        }

        private void Init(RectTransform root, string message)
        {
            _size = root.rect.size;
            if (_size.x <= 0 || _size.y <= 0) _size = new Vector2(1080, 1920);

            // confetti first so the message sits above it
            _pieces = new Piece[PiecesPerSide * 2];
            for (int i = 0; i < _pieces.Length; i++)
            {
                bool left = i < PiecesPerSide;
                var img = UiKit.Img(root, CyberTheme.Arrow(UnityEngine.Random.Range(0, 8)), ProcSprites.Pixel);
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = UnityEngine.Random.value < 0.3f ? new Vector2(16, 16) : new Vector2(UnityEngine.Random.Range(12f, 16f), UnityEngine.Random.Range(26f, 36f));

                float ang = UnityEngine.Random.Range(52f, 78f) * Mathf.Deg2Rad;
                float speed = UnityEngine.Random.Range(1500f, 2500f);
                var start = new Vector2(left ? -_size.x * 0.5f - 10f : _size.x * 0.5f + 10f, -_size.y * UnityEngine.Random.Range(0.02f, 0.18f));
                var vel = new Vector2(Mathf.Cos(ang) * speed * (left ? 1f : -1f), Mathf.Sin(ang) * speed);
                _pieces[i] = new Piece
                {
                    rt = rt, img = img, pos = start, vel = vel,
                    spin = UnityEngine.Random.Range(-540f, 540f), angle = UnityEngine.Random.Range(0f, 360f),
                    flip = UnityEngine.Random.Range(0f, 6.28f), flipSpeed = UnityEngine.Random.Range(8f, 16f),
                    life = UnityEngine.Random.Range(1.7f, Duration), drag = UnityEngine.Random.Range(1.4f, 2.2f),
                };
                rt.anchoredPosition = start;
            }

            _text = UiKit.Label(root, message, Theme.TitleSize + 34, Theme.Text, TextAnchor.MiddleCenter, bold: true);
            _text.raycastTarget = false;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label = _text.rectTransform;
            _label.anchorMin = _label.anchorMax = _label.pivot = new Vector2(0.5f, 0.5f);
            _label.sizeDelta = new Vector2(1000, 160);
            _label.anchoredPosition = new Vector2(0, _size.y * 0.26f);
            var glow = _text.gameObject.AddComponent<Outline>();
            glow.effectColor = Theme.A(Theme.Accent, 0.55f);
            glow.effectDistance = new Vector2(3, -3);
            _label.localScale = Vector3.zero;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _t += dt;

            for (int i = 0; i < _pieces.Length; i++)
            {
                ref var p = ref _pieces[i];
                if (p.rt == null) continue;
                p.vel *= Mathf.Exp(-p.drag * dt);
                p.vel.y -= Gravity * dt * 0.5f;
                p.pos += p.vel * dt;
                p.angle += p.spin * dt;
                p.flip += p.flipSpeed * dt;
                p.rt.anchoredPosition = p.pos + new Vector2(Mathf.Sin(p.flip * 0.5f) * 14f, 0f);
                p.rt.localRotation = Quaternion.Euler(0, 0, p.angle);
                p.rt.localScale = new Vector3(Mathf.Cos(p.flip), 1f, 1f);
                var c = p.img.color;
                c.a = Mathf.Clamp01((p.life - _t) / 0.4f);
                p.img.color = c;
            }

            // message: pop in with overshoot, hold, then drift up and fade
            float s;
            if (_t < 0.18f) s = Mathf.Lerp(0f, 1.12f, _t / 0.18f);
            else if (_t < 0.3f) s = Mathf.Lerp(1.12f, 1f, (_t - 0.18f) / 0.12f);
            else s = 1f;
            _label.localScale = new Vector3(s, s, 1f);
            float fade = Mathf.Clamp01((_t - 1.25f) / 0.45f);
            var tc = _text.color; tc.a = 1f - fade; _text.color = tc;
            _label.anchoredPosition = new Vector2(0, _size.y * 0.26f + fade * 60f);

            if (_t >= Duration) Destroy(gameObject);
        }
    }
}
