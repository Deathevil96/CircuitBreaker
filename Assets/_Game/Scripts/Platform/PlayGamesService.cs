using System;
using UnityEngine;
using CircuitBreaker.Core;
#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace CircuitBreaker.Platform
{
    /// <summary>
    /// Google Play Games Services: automatic sign-in, reports local achievement unlocks to Play Games (and re-sends the
    /// ones already unlocked when a player first signs in) and opens the Play Games achievements screen.
    /// A no-op in the editor and on any non-Android platform. The game never depends on sign-in succeeding.
    /// </summary>
    public class PlayGamesService : MonoBehaviour
    {
        public static PlayGamesService Instance { get; private set; }
        public static bool SignedIn { get; private set; }

        private const string SyncedPrefix = "pgs_sync_";

        public static PlayGamesService Create(GameObject host)
        {
            if (Instance != null) return Instance;
            Instance = host.AddComponent<PlayGamesService>();
            return Instance;
        }

        private void Start()
        {
            Achievements.Unlocked += OnUnlocked;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                PlayGamesPlatform.Activate();
                PlayGamesPlatform.Instance.Authenticate(OnAuthenticated);
            }
            catch (Exception e) { Debug.LogWarning("[CB] Play Games init failed: " + e.Message); }
#endif
        }

        private void OnDestroy() => Achievements.Unlocked -= OnUnlocked;

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnAuthenticated(SignInStatus status)
        {
            SignedIn = status == SignInStatus.Success;
            Debug.Log("[CB] Play Games sign-in: " + status);
            if (SignedIn) SyncAll();
        }
#endif

        private void OnUnlocked(CircuitBreaker.Core.Achievement a) => Report(a.id);

        /// <summary>Sends every locally unlocked achievement that Play Games has not confirmed yet.</summary>
        public void SyncAll()
        {
            foreach (var a in Achievements.All)
                if (a.Unlocked) Report(a.id);
        }

        private static void Report(string id)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!SignedIn) return;
            if (!PlayGamesAchievementMap.Ids.TryGetValue(id, out var playId)) return;
            if (SaveSystem.GetInt(SyncedPrefix + id) > 0) return;
            PlayGamesPlatform.Instance.ReportProgress(playId, 100.0, ok =>
            {
                if (!ok) return;
                SaveSystem.SetInt(SyncedPrefix + id, 1);
                SaveSystem.Save();
            });
#endif
        }

        /// <summary>Opens the Play Games achievements screen. Returns false when it cannot (not signed in, editor).</summary>
        public static bool ShowAchievements()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!SignedIn) { PlayGamesPlatform.Instance.ManuallyAuthenticate(s => { SignedIn = s == SignInStatus.Success; if (SignedIn) { Instance?.SyncAll(); PlayGamesPlatform.Instance.ShowAchievementsUI(); } }); return true; }
            PlayGamesPlatform.Instance.ShowAchievementsUI();
            return true;
#else
            return false;
#endif
        }
    }
}
