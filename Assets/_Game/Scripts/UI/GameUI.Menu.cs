using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CircuitBreaker.Core;
using CircuitBreaker.Fx;
using CircuitBreaker.Generation;

namespace CircuitBreaker.UI
{
    public class MenuData
    {
        public string continueTitle = "Continue";
        public string continueSub = "";
        public int streak, coins;
        public string dailySub = "", dailyMonth = "", freeplaySub = "", overloadSub = "";
        public float dailyFraction;
        public string coinAdSub = "";
        public bool coinAdEnabled;
    }

    public partial class GameUI
    {
        private GameObject _menuRoot;
        private RectTransform _menuContent;
        private LayoutElement _menuTopSpacer;
        private bool _menuCenter;
        private Text _menuContinueTitle, _menuContinueSub, _menuStreak, _menuCoins, _menuDailySub, _menuDailyMonth, _menuFreeSub, _menuOverloadSub;
        private RectTransform _menuDailyFill;
        private Image _menuStreakIcon;
        private UiKit.UiButton _menuCoinAd;
        private UiKit.Bob _menuCoinAdPulse;
        public event Action CoinAdClicked;

        private static readonly Color Cyan = CyberTheme.Arrow(0), Magenta = CyberTheme.Arrow(1), Lime = CyberTheme.Arrow(2), Amber = CyberTheme.Arrow(3), Orange = Theme.Hex("FF9A3D");

        /// <summary>Tinted tile with an accent icon badge. Returns the card so callers can add content to `content`.</summary>
        private static UiKit.Surface AccentCard(Transform parent, Color accent, string icon, float height, Action onClick, float flex)
        {
            var card = UiKit.Card(parent, accent, Theme.Lip(accent), 34f, raycast: true);
            UiKit.Chunk(card, 10f);
            UiKit.Size(card.root, h: height, flexW: flex).preferredWidth = 0;
            // accent glow in the corner
            var glow = UiKit.Img(card.content, new Color(1f, 1f, 1f, 0.30f), ProcSprites.GlowDot);
            glow.raycastTarget = false;
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(1, 1);
            glow.rectTransform.pivot = new Vector2(1, 1);
            glow.rectTransform.sizeDelta = new Vector2(320, 320);
            glow.rectTransform.anchoredPosition = new Vector2(70, 70);
            UiKit.Tappable(card, onClick);
            return card;
        }

        /// <summary>Round icon badge with a fixed size, left aligned inside a layout column.</summary>
        private static void Badge(Transform parent, Color accent, string icon, float size)
        {
            var holder = UiKit.Row(parent, 0, TextAnchor.MiddleLeft);
            UiKit.Size(holder, h: size);
            var ring = UiKit.Img(holder, Theme.A(Theme.Bg, 0.20f), ProcSprites.Circle);
            ring.raycastTarget = false;
            UiKit.Size(ring.rectTransform, w: size, h: size);
            var ic = UiKit.Icon(ring.rectTransform, icon, size * 0.56f, Theme.Bg);
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ic.rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>Rounded chip that sizes itself to its contents.</summary>
        private static RectTransform Pill(Transform parent, Color accent)
        {
            var bgImg = UiKit.Rounded(parent, Color.Lerp(Theme.Surface, accent, 0.26f), 36f);
            var g = bgImg.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = 12; g.padding = new RectOffset(22, 26, 0, 0);
            g.childAlignment = TextAnchor.MiddleCenter;
            g.childControlWidth = g.childControlHeight = true;
            g.childForceExpandWidth = g.childForceExpandHeight = false;
            bgImg.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiKit.Size(bgImg.rectTransform, h: 72);
            return bgImg.rectTransform;
        }

        private void BuildMenu()
        {
            var bg = UiKit.Img(_overlay, Theme.Bg, null, raycast: true);
            bg.name = "Menu";
            _menuRoot = bg.gameObject;
            UiKit.Stretch(bg.rectTransform);
            MenuArrows.Create(bg.rectTransform);

            var col = UiKit.ScrollColumn(_menuRoot.transform, 20, 44, 36);

            // ---- top bar: streak and coins as pills on the left, settings on the right ----
            var top = UiKit.Row(col, 14, TextAnchor.MiddleLeft);
            UiKit.Size(top, h: 80);
            var sIn = Pill(top, Amber);
            _menuStreakIcon = UiKit.Icon(sIn, "flame", 38, Amber); UiKit.Size(_menuStreakIcon.rectTransform, w: 38, h: 38);
            _menuStreak = UiKit.Label(sIn, "", Theme.Caption, Theme.Text, TextAnchor.MiddleLeft);
            _menuStreak.horizontalOverflow = HorizontalWrapMode.Overflow;
            var flex = UiKit.Rect("Flex", top); UiKit.Size(flex, flexW: 1);
            var cIn = Pill(top, Cyan);
            _menuCoins = UiKit.Label(cIn, "0", Theme.Body - 2, Theme.Text, TextAnchor.MiddleRight, bold: true);
            _menuCoins.horizontalOverflow = HorizontalWrapMode.Overflow;
            var coin = UiKit.Icon(cIn, "coin", 38, Cyan); UiKit.Size(coin.rectTransform, w: 38, h: 38);
            var gear = UiKit.MakeIconButton(top, "settings", () => SettingsClicked?.Invoke(), 72);

            // ---- small free-coins ad button, right-aligned under the settings button ----
            var adRow = UiKit.Row(col, 0, TextAnchor.MiddleRight);
            UiKit.Size(adRow, h: 68);
            var adFlex = UiKit.Rect("Flex", adRow); UiKit.Size(adFlex, flexW: 1);
            _menuCoinAd = UiKit.MakeButton(adRow, "", ButtonStyle.Primary, () => { HideCoinTip(); CoinAdClicked?.Invoke(); }, "ad", 68, Theme.Micro);
            UiKit.Size(_menuCoinAd.border, w: 250, h: 68);
            if (_menuCoinAd.icon != null) UiKit.Size(_menuCoinAd.icon.rectTransform, w: 46, h: 46);      // default icon size (34% of height) was too small
            BuildCoinTip();
            _menuCoinAdPulse = _menuCoinAd.border.gameObject.AddComponent<UiKit.Bob>();     // gentle pulse while the ad is ready
            _menuCoinAdPulse.amount = 0.045f; _menuCoinAdPulse.speed = 3f;

            // ---- hero title, left aligned ----
            _menuContent = col;
            var topSpacer = UiKit.Rect("TopSpacer", col);
            _menuTopSpacer = UiKit.Size(topSpacer, h: 40);
            var t1 = UiKit.Label(col, "ARROW", 128, Theme.Text, TextAnchor.LowerLeft, bold: true);
            t1.gameObject.AddComponent<UiKit.Bob>().phase = 0f;
            t1.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Size(t1, h: 124);
            var t2 = UiKit.Label(col, "BREAKER", 128, Cyan, TextAnchor.UpperLeft, bold: true);
            t2.gameObject.AddComponent<UiKit.Bob>().phase = 1.4f;
            t2.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Size(t2, h: 124);
            var tag = UiKit.Label(col, "C I R C U I T    P U Z Z L E", Theme.Micro, Theme.Dim, TextAnchor.MiddleLeft);
            UiKit.Size(tag, h: 40);
            UiKit.Spacer(col, 26);

            // ---- hero play card ----
            var cont = UiKit.Card(col, Amber, Theme.Lip(Amber), 40f, raycast: true);
            UiKit.Chunk(cont, 12f);
            cont.root.gameObject.AddComponent<UiKit.Bob>().amount = 0.014f;
            UiKit.Size(cont.root, h: 230);
            var deco = UiKit.Img(cont.content, Theme.A(Color.black, 0.10f), ProcSprites.ArrowGlyph);
            deco.raycastTarget = false;
            deco.rectTransform.anchorMin = deco.rectTransform.anchorMax = new Vector2(1, 0);
            deco.rectTransform.pivot = new Vector2(1, 0);
            deco.rectTransform.sizeDelta = new Vector2(420, 420);
            deco.rectTransform.anchoredPosition = new Vector2(40, -90);
            var crow = UiKit.Row(cont.content, 20, TextAnchor.MiddleLeft);
            UiKit.Stretch(crow, 44, 0, 36, 0);
            var ctext = UiKit.Column(crow, 6, fitHeight: false, align: TextAnchor.MiddleLeft);
            UiKit.Size(ctext, flexW: 1);
            _menuContinueTitle = UiKit.Label(ctext, "Continue", 64, Theme.Bg, TextAnchor.MiddleLeft, bold: true);
            UiKit.Size(_menuContinueTitle, h: 78);
            _menuContinueSub = UiKit.Label(ctext, "", Theme.Caption, Theme.A(Theme.Bg, 0.72f), TextAnchor.MiddleLeft);
            UiKit.Size(_menuContinueSub, h: 36);
            var playDisc = UiKit.Img(crow, Theme.Bg, ProcSprites.Circle);
            playDisc.raycastTarget = false;
            UiKit.Size(playDisc.rectTransform, w: 120, h: 120);
            var playIc = UiKit.Icon(playDisc.rectTransform, "play", 60, Amber);
            playIc.rectTransform.anchorMin = playIc.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            playIc.rectTransform.anchoredPosition = new Vector2(4, 0);
            UiKit.Tappable(cont, () => ContinueClicked?.Invoke());

            // ---- bento: tall Daily tile left, two short tiles stacked right ----
            UiKit.Spacer(col, 2);
            var bento = UiKit.Row(col, 18, TextAnchor.UpperLeft, expand: true);
            UiKit.Size(bento, h: 340);
            bento.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

            var daily = AccentCard(bento, Orange, "calendar", 340, () => DailyClicked?.Invoke(), 1.2f);
            var dcol = UiKit.Column(daily.content, 8, 30, 28, fitHeight: false, align: TextAnchor.UpperLeft);
            UiKit.Stretch(dcol);
            Badge(dcol, Orange, "calendar", 84);
            var dflex = UiKit.Rect("Flex", dcol); UiKit.Size(dflex, flexW: 0, minH: 0).flexibleHeight = 1;
            var dt = UiKit.Label(dcol, "Daily Run", Theme.H2 - 2, Theme.Bg, TextAnchor.MiddleLeft, bold: true); UiKit.Size(dt, h: 48);
            _menuDailySub = UiKit.Label(dcol, "", Theme.Caption, Theme.A(Theme.Bg, 0.78f), TextAnchor.MiddleLeft); UiKit.Size(_menuDailySub, h: 34);
            var bar = UiKit.ProgressBar(dcol, 0f, 10);
            bar.color = Theme.Bg;
            _menuDailyFill = bar.rectTransform;
            _menuDailyMonth = UiKit.Label(dcol, "", Theme.Micro, Theme.A(Theme.Bg, 0.65f), TextAnchor.MiddleLeft); UiKit.Size(_menuDailyMonth, h: 28);

            var right = UiKit.Column(bento, 18, fitHeight: false, align: TextAnchor.UpperCenter);
            UiKit.Size(right, flexW: 1).preferredWidth = 0;
            right.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = true;

            var free = AccentCard(right, Magenta, "freeplay", 161, () => FreeplayClicked?.Invoke(), 1f);
            var frow = UiKit.Row(free.content, 14, TextAnchor.MiddleLeft); UiKit.Stretch(frow, 24, 0, 18, 0);
            var fcol = UiKit.Column(frow, 2, fitHeight: false, align: TextAnchor.MiddleLeft); UiKit.Size(fcol, flexW: 1);
            var ft = UiKit.Label(fcol, "Freeplay", Theme.Body - 2, Theme.Bg, TextAnchor.MiddleLeft, bold: true); UiKit.Size(ft, h: 42);
            _menuFreeSub = UiKit.Label(fcol, "", Theme.Micro, Theme.A(Theme.Bg, 0.78f), TextAnchor.MiddleLeft); UiKit.Size(_menuFreeSub, h: 30);
            var fic = UiKit.Icon(frow, "freeplay", 48, Theme.Bg); UiKit.Size(fic.rectTransform, w: 48, h: 48);

            var ov = AccentCard(right, Lime, "overload", 161, () => OverloadClicked?.Invoke(), 1f);
            var orow = UiKit.Row(ov.content, 14, TextAnchor.MiddleLeft); UiKit.Stretch(orow, 24, 0, 18, 0);
            var ocol = UiKit.Column(orow, 2, fitHeight: false, align: TextAnchor.MiddleLeft); UiKit.Size(ocol, flexW: 1);
            var ot = UiKit.Label(ocol, "Overload", Theme.Body - 2, Theme.Bg, TextAnchor.MiddleLeft, bold: true); UiKit.Size(ot, h: 42);
            _menuOverloadSub = UiKit.Label(ocol, "", Theme.Micro, Theme.A(Theme.Bg, 0.78f), TextAnchor.MiddleLeft); UiKit.Size(_menuOverloadSub, h: 30);
            var oic = UiKit.Icon(orow, "overload", 48, Theme.Bg); UiKit.Size(oic.rectTransform, w: 48, h: 48);

            // ---- bottom: two uneven buttons ----
            var bottom = UiKit.Row(col, 18, TextAnchor.MiddleLeft, expand: true);
            UiKit.Size(bottom, h: 104);
            var b1 = UiKit.MakeButton(bottom, "Levels", ButtonStyle.Secondary, () => LevelsClicked?.Invoke(), "levels", 104, Theme.Caption + 2);
            var b2 = UiKit.MakeButton(bottom, "Records", ButtonStyle.Secondary, () => RecordsClicked?.Invoke(), "records", 104, Theme.Caption + 2);
            UiKit.Size(b1.border, flexW: 1.35f).preferredWidth = 0;
            UiKit.Size(b2.border, flexW: 1f).preferredWidth = 0;

            UiKit.Spacer(col, 4);
            var ver = UiKit.Label(col, BuildInfo.Company.ToUpperInvariant() + "   ·   v" + BuildInfo.AppVersion, Theme.Micro - 2, Theme.Faint, TextAnchor.MiddleLeft);
            UiKit.Size(ver, h: 28);

            _menuRoot.SetActive(false);
        }

        public void ShowMenu(MenuData d)
        {
            _menuContinueTitle.text = d.continueTitle;
            _menuContinueSub.text = d.continueSub;
            _menuStreak.text = d.streak > 0 ? d.streak + " day streak" : "Start a streak";
            _menuStreakIcon.color = d.streak > 0 ? Amber : Theme.Faint;
            _menuCoins.text = d.coins.ToString("N0");
            _menuDailySub.text = d.dailySub;
            _menuDailyMonth.text = d.dailyMonth;
            _menuDailyFill.anchorMax = new Vector2(Mathf.Clamp01(d.dailyFraction), 1f);
            _menuFreeSub.text = d.freeplaySub;
            _menuOverloadSub.text = d.overloadSub;
            SetCoinAd(d.coinAdSub, d.coinAdEnabled);
            SetLayer(_menuRoot, true);
            _menuCenter = true;
        }

        /// <summary>On tall screens the menu sits a little above the vertical centre instead of hugging the top.</summary>
        private void CenterMenu()
        {
            if (!_menuCenter || _menuContent == null || !_menuRoot.activeSelf) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_menuContent);
            float viewport = ((RectTransform)_menuContent.parent).rect.height;
            if (viewport <= 1f) return;
            _menuCenter = false;
            float content = _menuContent.rect.height - _menuTopSpacer.preferredHeight;
            _menuTopSpacer.preferredHeight = Mathf.Max(40f, (viewport - content) * 0.22f);
        }

        // ---- tooltip under the ad button: at launch, then every 2 minutes on the main menu ----
        private GameObject _coinTip;
        private float _coinTipHideAt, _coinTipLastT, _coinTipAccum = CoinTipEvery - 1f;
        private const float CoinTipEvery = 120f, CoinTipShown = 6f;

        private void BuildCoinTip()
        {
            // Lives on the safe-area layer like the achievement toast (which does show), pinned under the ad button:
            // top bar 36..116, ad row 136..204, tip right below it. Shown with SetAsLastSibling so it is drawn on top.
            var bubble = UiKit.Rounded(_safe, Color.Lerp(Theme.Surface2, Theme.Accent, 0.35f), 30f);
            bubble.name = "CoinTip";
            bubble.raycastTarget = false;
            _coinTip = bubble.gameObject;
            var br = bubble.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(1f, 1f);
            br.pivot = new Vector2(1f, 1f);
            br.anchoredPosition = new Vector2(-44f, -214f);
            br.sizeDelta = new Vector2(560f, 64f);
            var l = UiKit.Label(br, "Watch an ad for +" + Economy.CoinAdAmount + " free coins", Theme.Micro, Theme.Text, TextAnchor.MiddleCenter, bold: true);
            l.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(l.rectTransform, 16, 0, 16, 0);
            _coinTip.SetActive(false);
        }

        public void HideCoinTip() { if (_coinTip != null) _coinTip.SetActive(false); }

        private void TickCoinTip()
        {
            if (_coinTip == null) return;
            float now = Time.unscaledTime;
            float dt = Mathf.Clamp(now - _coinTipLastT, 0f, 1f); _coinTipLastT = now;
            bool onMenu = _menuRoot.activeSelf && _stack.Count > 0 && _stack[_stack.Count - 1] == _menuRoot;
            if (!onMenu) { HideCoinTip(); return; }
            if (_coinTip.activeSelf && now >= _coinTipHideAt) HideCoinTip();
            _coinTipAccum += dt;
            if (!_coinTip.activeSelf && _coinTipAccum >= CoinTipEvery && _menuCoinAd.button.interactable)
            {
                _coinTip.transform.SetAsLastSibling();
                _coinTip.SetActive(true);
                _coinTipHideAt = now + CoinTipShown;
                _coinTipAccum = 0f;
            }
        }

        public void SetCoinAd(string sub, bool enabled)
        {
            if (_menuCoinAd == null) return;
            _menuCoinAd.SetText(sub);
            _menuCoinAd.SetEnabled(enabled);
            if (_menuCoinAdPulse != null) { _menuCoinAdPulse.enabled = enabled; if (!enabled) _menuCoinAdPulse.transform.localScale = Vector3.one; }
        }

        public void HideMenu() => SetLayer(_menuRoot, false);
    }
}
