#if CB_ADMOB
using System;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;

namespace CircuitBreaker.Ads
{
    /// <summary>
    /// Google AdMob provider (Google Mobile Ads Unity plugin 11.x). Banner at the bottom, rewarded and interstitial ads
    /// preloaded and reloaded after every show. The EU/UK consent form (UMP) runs before any ad is requested.
    /// Compiled only when the CB_ADMOB scripting define is set.
    /// </summary>
    public class AdMobProvider : MonoBehaviour, IAdProvider
    {
        public bool BannerSupported => true;
        public bool RewardedReady => _rewarded != null && _rewarded.CanShowAd();
        public bool InterstitialReady => _interstitial != null && _interstitial.CanShowAd();

        private bool _initialised, _bannerWanted = true;
        private BannerView _banner;
        private RewardedAd _rewarded;
        private InterstitialAd _interstitial;
        private Action<bool> _rewardCb;
        private Action _closeCb;
        private bool _earned;

        private void Start() => RequestConsentThenInitialise();

        private void RequestConsentThenInitialise()
        {
            var parameters = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
            ConsentInformation.Update(parameters, updateError =>
            {
                if (updateError != null) { Debug.LogWarning("[CB] consent update: " + updateError.Message); }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null) Debug.LogWarning("[CB] consent form: " + formError.Message);
                    if (ConsentInformation.CanRequestAds()) Initialise();
                });
            });
            // Players who already gave consent in an earlier session can be served right away.
            if (ConsentInformation.CanRequestAds()) Initialise();
        }

        private void Initialise()
        {
            if (_initialised) return;
            _initialised = true;
            MobileAds.Initialize(_ =>
            {
                Debug.Log("[CB] AdMob initialised");
                if (_bannerWanted) CreateBanner();
                LoadRewarded();
                LoadInterstitial();
            });
        }

        // ---------- banner ----------
        public void SetBannerVisible(bool visible)
        {
            _bannerWanted = visible;
            if (!_initialised) return;
            if (visible) { if (_banner == null) CreateBanner(); else _banner.Show(); }
            else _banner?.Hide();
        }

        private void CreateBanner()
        {
            _banner?.Destroy();
            // Standard 320x50 banner: a fixed, predictable height that the layout reserves (the strip never overlaps the board).
            _banner = new BannerView(AdUnits.Banner, AdSize.Banner, AdPosition.Bottom);
            _banner.OnBannerAdLoaded += () => AdService.BannerNativeHeightPx = _banner.GetHeightInPixels();
            _banner.OnBannerAdLoadFailed += e => Debug.LogWarning("[CB] banner failed: " + e.GetMessage());
            _banner.LoadAd(new AdRequest());
        }

        // ---------- rewarded ----------
        private void LoadRewarded()
        {
            _rewarded?.Destroy();
            _rewarded = null;
            RewardedAd.Load(AdUnits.Rewarded, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null) { Debug.LogWarning("[CB] rewarded load failed: " + error?.GetMessage()); return; }
                _rewarded = ad;
                ad.OnAdFullScreenContentClosed += OnRewardedClosed;
                ad.OnAdFullScreenContentFailed += e => { Debug.LogWarning("[CB] rewarded show failed: " + e.GetMessage()); FinishRewarded(false); };
            });
        }

        public void ShowRewarded(string placement, Action<bool> onComplete)
        {
            if (!RewardedReady) { onComplete?.Invoke(false); return; }
            _rewardCb = onComplete;
            _earned = false;
            AdService.IsAdShowing = true;
            _rewarded.Show(reward => _earned = true);
        }

        private void OnRewardedClosed() => FinishRewarded(_earned);

        private void FinishRewarded(bool granted)
        {
            AdService.IsAdShowing = false;
            var cb = _rewardCb; _rewardCb = null;
            LoadRewarded();
            cb?.Invoke(granted);
        }

        // ---------- interstitial ----------
        private void LoadInterstitial()
        {
            _interstitial?.Destroy();
            _interstitial = null;
            InterstitialAd.Load(AdUnits.Interstitial, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null) { Debug.LogWarning("[CB] interstitial load failed: " + error?.GetMessage()); return; }
                _interstitial = ad;
                ad.OnAdFullScreenContentClosed += FinishInterstitial;
                ad.OnAdFullScreenContentFailed += e => { Debug.LogWarning("[CB] interstitial show failed: " + e.GetMessage()); FinishInterstitial(); };
            });
        }

        public void ShowInterstitial(string placement, Action onClosed)
        {
            if (!InterstitialReady) { onClosed?.Invoke(); return; }
            _closeCb = onClosed;
            AdService.IsAdShowing = true;
            _interstitial.Show();
        }

        private void FinishInterstitial()
        {
            AdService.IsAdShowing = false;
            var cb = _closeCb; _closeCb = null;
            LoadInterstitial();
            cb?.Invoke();
        }

        private void OnDestroy()
        {
            _banner?.Destroy();
            _rewarded?.Destroy();
            _interstitial?.Destroy();
        }
    }
}
#endif
