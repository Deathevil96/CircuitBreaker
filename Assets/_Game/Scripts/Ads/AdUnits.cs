namespace CircuitBreaker.Ads
{
    /// <summary>
    /// Ad unit IDs. The values below are Google's PUBLIC TEST IDs, safe for closed testing and development.
    /// Before production create real units in AdMob and put them in the PRODUCTION block (define CB_ADMOB_PRODUCTION).
    /// Never click your own real ads: use test ads until release.
    /// </summary>
    public static class AdUnits
    {
#if CB_ADMOB_PRODUCTION
  #if UNITY_ANDROID
        public const string Banner = "ca-app-pub-REPLACE_ME/REPLACE_ME";
        public const string Rewarded = "ca-app-pub-REPLACE_ME/REPLACE_ME";
        public const string Interstitial = "ca-app-pub-REPLACE_ME/REPLACE_ME";
  #else
        public const string Banner = "ca-app-pub-REPLACE_ME/REPLACE_ME";
        public const string Rewarded = "ca-app-pub-REPLACE_ME/REPLACE_ME";
        public const string Interstitial = "ca-app-pub-REPLACE_ME/REPLACE_ME";
  #endif
#else
  #if UNITY_ANDROID
        public const string Banner = "ca-app-pub-3940256099942544/6300978111";
        public const string Rewarded = "ca-app-pub-3940256099942544/5224354917";
        public const string Interstitial = "ca-app-pub-3940256099942544/1033173712";
  #else
        public const string Banner = "ca-app-pub-3940256099942544/2934735716";
        public const string Rewarded = "ca-app-pub-3940256099942544/1712485313";
        public const string Interstitial = "ca-app-pub-3940256099942544/4411468910";
  #endif
#endif
    }
}
