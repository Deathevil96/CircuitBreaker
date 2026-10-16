#if UNITY_EDITOR || DEVELOPMENT_BUILD || CB_TEST_ADS
using System;
using UnityEngine;
using UnityEngine.UI;
using CircuitBreaker.Fx;
using CircuitBreaker.UI;

namespace CircuitBreaker.Ads
{
    /// <summary>
    /// Test provider for the Editor, development builds and closed-test builds (define CB_TEST_ADS).
    /// It draws a fake banner slot and shows clearly labelled fake full-screen ads.
    /// </summary>
    public class StubAdProvider : MonoBehaviour, IAdProvider
    {
        public bool BannerSupported => true;
        public bool RewardedReady => !_playing;
        public bool InterstitialReady => !_playing;

        private bool _playing;
        private GameObject _overlay;
        private Text _countdown;
        private UiKit.UiButton _claim;
        private Action<bool> _rewardCb;
        private Action _closeCb;
        private float _remaining;
        private bool _rewardedKind;

        public void SetBannerVisible(bool visible) { }   // the banner strip is drawn by GameUI

        public void ShowRewarded(string placement, Action<bool> onComplete)
        {
            _rewardedKind = true; _rewardCb = onComplete; _closeCb = null;
            Begin("Test rewarded ad", placement, 3f, "Close and claim", "Close early, no reward");
        }

        public void ShowInterstitial(string placement, Action onClosed)
        {
            _rewardedKind = false; _closeCb = onClosed; _rewardCb = null;
            Begin("Test interstitial ad", placement, 2f, "Close ad", null);
        }

        private void Begin(string title, string placement, float seconds, string claimText, string skipText)
        {
            _playing = true;
            AdService.IsAdShowing = true;
            _remaining = seconds;

            var canvasGo = new GameObject("StubAdOverlay");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _overlay = canvasGo;

            var bg = UiKit.Img(canvasGo.transform, Theme.Bg, null, true);
            UiKit.Stretch(bg.rectTransform);
            var col = UiKit.Column(bg.transform, 24, 60, 0, fitHeight: false, align: TextAnchor.MiddleCenter);
            UiKit.Stretch(col);

            var t = UiKit.Label(col, title, Theme.H1, Theme.Text, TextAnchor.MiddleCenter, bold: true); UiKit.Size(t, h: 70);
            var s = UiKit.Label(col, "Placement: " + placement + "\nA real ad SDK shows the ad here.", Theme.Body - 4, Theme.Dim, TextAnchor.MiddleCenter, wrap: true); UiKit.Size(s, h: 100);
            _countdown = UiKit.Label(col, Mathf.CeilToInt(seconds).ToString(), 120, Theme.Text, TextAnchor.MiddleCenter, bold: true); UiKit.Size(_countdown, h: 150);
            _claim = UiKit.MakeButton(col, claimText, ButtonStyle.Primary, () => Finish(true), null, 108);
            _claim.SetEnabled(false);
            if (skipText != null) UiKit.MakeButton(col, skipText, ButtonStyle.Ghost, () => Finish(false), null, 80);
        }

        private void Update()
        {
            if (!_playing) return;
            _remaining -= Time.unscaledDeltaTime;
            _countdown.text = Mathf.Max(0, Mathf.CeilToInt(_remaining)).ToString();
            if (_remaining <= 0f && !_claim.button.interactable) _claim.SetEnabled(true);
        }

        private void Finish(bool granted)
        {
            _playing = false;
            AdService.IsAdShowing = false;
            if (_overlay != null) Destroy(_overlay);
            var r = _rewardCb; var c = _closeCb;
            _rewardCb = null; _closeCb = null;
            if (_rewardedKind) r?.Invoke(granted); else c?.Invoke();
        }
    }
}
#endif
