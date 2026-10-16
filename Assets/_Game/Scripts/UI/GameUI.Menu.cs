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
        public string dailySub = "", freeplaySub = "", overloadSub = "";
    }

    public partial class GameUI
    {
        private GameObject _menuRoot;
        private RectTransform _menuContent;
        private LayoutElement _menuTopSpacer;
        private bool _menuCenter;
        private Text _menuContinueTitle, _menuContinueSub, _menuStreak, _menuCoins, _menuDailySub, _menuFreeSub, _menuOverloadSub;

        private void BuildMenu()
        {
            var bg = UiKit.Img(_overlay, Theme.Bg, null, raycast: true);
            bg.name = "Menu";
            _menuRoot = bg.gameObject;
            UiKit.Stretch(bg.rectTransform);

            var col = UiKit.ScrollColumn(_menuRoot.transform, 22, 44, 36);

            // streak + coins
            var top = UiKit.Row(col, 16, TextAnchor.MiddleLeft);
            UiKit.Size(top, h: 56);
            var flame = UiKit.Icon(top, "flame", 40, Theme.Text); UiKit.Size(flame.rectTransform, w: 40, h: 40);
            _menuStreak = UiKit.Label(top, "", Theme.Caption + 2, Theme.Dim, TextAnchor.MiddleLeft);
            UiKit.Size(_menuStreak, flexW: 1);
            _menuCoins = UiKit.Label(top, "0", Theme.Body, Theme.Text, TextAnchor.MiddleRight, bold: true);
            var coin = UiKit.Icon(top, "coin", 40, Theme.Dim); UiKit.Size(coin.rectTransform, w: 40, h: 40);

            _menuContent = col;
            var topSpacer = UiKit.Rect("TopSpacer", col);
            _menuTopSpacer = UiKit.Size(topSpacer, h: 70);
            var wm = UiKit.Label(col, "ARROW BREAKER", 56, Theme.Text, TextAnchor.MiddleCenter, bold: true);
            UiKit.Size(wm, h: 72);
            var tag = UiKit.Label(col, "Circuit puzzle", Theme.Body - 4, Theme.Dim, TextAnchor.MiddleCenter);
            UiKit.Size(tag, h: 44);
            UiKit.Spacer(col, 60);

            // continue
            var cont = UiKit.Card(col, Theme.Text, Theme.Text, 20f, raycast: true);
            UiKit.Size(cont.root, h: 150);
            var cc = UiKit.Column(cont.content, 6, fitHeight: false, align: TextAnchor.MiddleCenter);
            UiKit.Stretch(cc);
            _menuContinueTitle = UiKit.Label(cc, "Continue", Theme.H2, Theme.Bg, TextAnchor.MiddleCenter, bold: true);
            UiKit.Size(_menuContinueTitle, h: 50);
            _menuContinueSub = UiKit.Label(cc, "", Theme.Caption, Theme.Faint, TextAnchor.MiddleCenter);
            UiKit.Size(_menuContinueSub, h: 34);
            UiKit.Tappable(cont, () => ContinueClicked?.Invoke());

            UiKit.Spacer(col, 6);
            var daily = UiKit.MakeRow(col, "Daily Run", "", "calendar", () => DailyClicked?.Invoke());
            _menuDailySub = SubLabelOf(daily);
            var free = UiKit.MakeRow(col, "Freeplay", "", "freeplay", () => FreeplayClicked?.Invoke());
            _menuFreeSub = SubLabelOf(free);
            var ov = UiKit.MakeRow(col, "Overload", "", "overload", () => OverloadClicked?.Invoke());
            _menuOverloadSub = SubLabelOf(ov);

            UiKit.Spacer(col, 6);
            var bottom = UiKit.Row(col, 16, TextAnchor.MiddleCenter, expand: true);
            UiKit.Size(bottom, h: 104);
            var b1 = UiKit.MakeButton(bottom, "Levels", ButtonStyle.Secondary, () => LevelsClicked?.Invoke(), "levels", 104, Theme.Caption + 2);
            var b2 = UiKit.MakeButton(bottom, "Records", ButtonStyle.Secondary, () => RecordsClicked?.Invoke(), "records", 104, Theme.Caption + 2);
            var b3 = UiKit.MakeButton(bottom, "Settings", ButtonStyle.Secondary, () => SettingsClicked?.Invoke(), "settings", 104, Theme.Caption + 2);
            foreach (var b in new[] { b1, b2, b3 }) UiKit.Size(b.border, flexW: 1);

            UiKit.Spacer(col, 10);
            var ver = UiKit.Label(col, BuildInfo.Company + "  ·  v" + BuildInfo.AppVersion, Theme.Micro, Theme.Faint, TextAnchor.MiddleCenter);
            UiKit.Size(ver, h: 30);

            _menuRoot.SetActive(false);
        }

        private static Text SubLabelOf(UiKit.UiButton row)
        {
            // MakeRow adds the title label only; add a subtitle label under it.
            var titleRt = row.label.rectTransform.parent;
            return UiKit.Label(titleRt, "", Theme.Caption, Theme.Dim, TextAnchor.MiddleLeft);
        }

        public void ShowMenu(MenuData d)
        {
            _menuContinueTitle.text = d.continueTitle;
            _menuContinueSub.text = d.continueSub;
            _menuStreak.text = d.streak > 0 ? d.streak + (d.streak == 1 ? " day streak" : " day streak") : "Start your streak today";
            _menuCoins.text = d.coins.ToString("N0");
            _menuDailySub.text = d.dailySub;
            _menuFreeSub.text = d.freeplaySub;
            _menuOverloadSub.text = d.overloadSub;
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
            _menuTopSpacer.preferredHeight = Mathf.Max(70f, (viewport - content) * 0.36f);
        }

        public void HideMenu() => SetLayer(_menuRoot, false);
    }
}
