using System;
using UnityEngine;
using CircuitBreaker.Core;

namespace CircuitBreaker.Ads
{
    /// <summary>Plug a real ad SDK in here (AdMob, LevelPlay, ...). The game only talks to this interface.</summary>
    public interface IAdProvider
    {
        bool BannerSupported { get; }
        bool RewardedReady { get; }
        bool InterstitialReady { get; }
        void SetBannerVisible(bool visible);
        /// <summary>Full-screen ad shown at a natural pause. onClosed fires when the player dismisses it (or it fails to show).</summary>
        void ShowInterstitial(string placement, Action onClosed);
        /// <summary>Calls back with true only if the player watched enough to earn the reward.</summary>
        void ShowRewarded(string placement, Action<bool> onComplete);
    }

    /// <summary>
    /// Single entry point for ads. Rules (see GDD section 13): one banner always visible, rewarded ads only (no interstitials),
    /// and a release build without an SDK provider shows no ads and grants no ad rewards.
    /// </summary>
    public static class AdService
    {
        public static IAdProvider Provider { get; private set; }

        /// <summary>True while a full-screen ad is on screen (the Android back key must not act on the game underneath).</summary>
        public static bool IsAdShowing { get; set; }

        /// <summary>Set by the UI each frame: banner height in screen pixels, so the camera can leave room for it.</summary>
        public static float BannerHeightPx;

        /// <summary>Set by a real ad provider once the banner has loaded (its true pixel height). 0 means "use the default 50dp slot".</summary>
        public static float BannerNativeHeightPx;

        public static bool AdsEnabled => !SaveSystem.AdsRemoved;
        public static bool BannerVisible => AdsEnabled && Provider != null && Provider.BannerSupported;
        // Remove-Ads only removes the banner: rewarded ads stay available because the player opts in to them.
        public static bool RewardedReady => Provider != null && Provider.RewardedReady;
        public static float BannerFraction => BannerVisible && Screen.height > 0 ? Mathf.Clamp01(BannerHeightPx / Screen.height) : 0f;

        public static void Init(IAdProvider provider)
        {
            Provider = provider;
            Provider?.SetBannerVisible(AdsEnabled);
        }

        /// <summary>Interstitials respect Remove-Ads. The game only calls this at the restart ad moment (see GDD 13).</summary>
        public static bool InterstitialReady => AdsEnabled && Provider != null && Provider.InterstitialReady;

        public static void ShowInterstitial(string placement, Action onClosed)
        {
            if (!InterstitialReady) { onClosed?.Invoke(); return; }
            Provider.ShowInterstitial(placement, onClosed);
        }

        public static void ShowRewarded(string placement, Action<bool> onComplete)
        {
            if (!RewardedReady) { onComplete?.Invoke(false); return; }
            Provider.ShowRewarded(placement, onComplete);
        }
    }
}
