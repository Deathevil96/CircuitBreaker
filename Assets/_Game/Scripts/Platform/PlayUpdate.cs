using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.AppUpdate;
#endif

namespace CircuitBreaker.Platform
{
    /// <summary>
    /// Mandatory updates through Google's own in-app update screen (Play In-App Updates, IMMEDIATE flow): when a newer version is
    /// published on Google Play the player gets Google's full-screen prompt and cannot use the game until it is installed.
    /// If the prompt is dismissed it comes back the next time the app is brought to the front. Does nothing in the Editor,
    /// in builds that were not installed from Google Play, or when no update is available.
    /// </summary>
    public class PlayUpdate : MonoBehaviour
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private AppUpdateManager _manager;
        private bool _busy;
        private float _lastCheck = -999f;

        private void Start() => StartCoroutine(Check());

        private void OnApplicationFocus(bool focus)
        {
            if (focus && !_busy && Time.unscaledTime - _lastCheck > 3f) StartCoroutine(Check());
        }

        private IEnumerator Check()
        {
            if (_busy) yield break;
            _busy = true;
            _lastCheck = Time.unscaledTime;
            try
            {
                if (_manager == null) _manager = new AppUpdateManager();
            }
            catch (System.Exception e) { Debug.Log("[CB] update manager: " + e.Message); _busy = false; yield break; }

            var info = _manager.GetAppUpdateInfo();
            yield return info;
            if (!info.IsSuccessful) { Debug.Log("[CB] update check: " + info.Error); _busy = false; yield break; }

            var result = info.GetResult();
            var options = AppUpdateOptions.ImmediateAppUpdateOptions();
            bool wanted = result.UpdateAvailability == UpdateAvailability.UpdateAvailable
                          || result.UpdateAvailability == UpdateAvailability.DeveloperTriggeredUpdateInProgress;
            if (wanted && result.IsUpdateTypeAllowed(options))
            {
                Debug.Log("[CB] update available: showing Google's update screen");
                var request = _manager.StartUpdate(result, options);
                yield return request;
                Debug.Log("[CB] update screen closed: " + request.Error);
            }
            _lastCheck = Time.unscaledTime;
            _busy = false;
        }
#endif
    }
}
