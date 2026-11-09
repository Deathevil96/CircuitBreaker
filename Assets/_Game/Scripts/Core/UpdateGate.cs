using System;
using UnityEngine;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// Walls that stop play: a newer version is waiting on Google Play (set by PlayUpdate after Google's own update screen
    /// was dismissed), or the app was not installed from Google Play (sideloaded APK).
    /// </summary>
    public static class UpdateGate
    {
        public const string PackageId = "com.misalinteractive.arrowbreaker";
        public const string StoreAppUrl = "market://details?id=" + PackageId;
        public const string StoreWebUrl = "https://play.google.com/store/apps/details?id=" + PackageId;
        public const string PlayInstaller = "com.android.vending";

        /// <summary>True while a newer version is available and not installed yet.</summary>
        public static bool UpdatePending;

        /// <summary>Set by PlayUpdate: reopens Google's update screen.</summary>
        public static Action RetryUpdate;

        /// <summary>Production (AdMob production ids) builds on Android must come from Google Play. Test, QA and development builds and the Editor are exempt.</summary>
        public static bool SideloadBlocked
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR && CB_ADMOB_PRODUCTION
                if (Debug.isDebugBuild) return false;
                return Application.installerName != PlayInstaller;
#else
                return false;
#endif
            }
        }

        public static void OpenStore()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { Application.OpenURL(StoreAppUrl); return; } catch (Exception) { }
#endif
            Application.OpenURL(StoreWebUrl);
        }
    }
}
