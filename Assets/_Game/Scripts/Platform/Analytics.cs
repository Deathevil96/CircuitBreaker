using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Firebase;
using Firebase.Analytics;
#endif

namespace CircuitBreaker.Platform
{
    /// <summary>
    /// Firebase Analytics (real-time events in the Firebase console). Collection is OFF in the manifest and only switched on by
    /// <see cref="Enable"/>, which the ad provider calls once the Google consent flow allows it (so EU/UK players who decline
    /// are never tracked). Does nothing in the Editor or in builds without ads consent (test-ad builds). Never sends names,
    /// emails or anything that identifies a person: only gameplay events.
    /// </summary>
    public static class Analytics
    {
        private static bool _ready;
        private static readonly Queue<System.Action> Pending = new Queue<System.Action>();

        public static void Enable()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_ready) return;
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
            {
                if (task.Result != DependencyStatus.Available) { Debug.LogWarning("[CB] firebase: " + task.Result); return; }
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                _ready = true;
                lock (Pending) { while (Pending.Count > 0) Pending.Dequeue()(); }
            });
#endif
        }

        public static void Log(string name, params (string key, object value)[] args)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            System.Action send = () =>
            {
                var ps = new List<Parameter>();
                foreach (var a in args)
                {
                    if (a.value is string s) ps.Add(new Parameter(a.key, s));
                    else if (a.value is int i) ps.Add(new Parameter(a.key, (long)i));
                    else if (a.value is long l) ps.Add(new Parameter(a.key, l));
                    else if (a.value is float f) ps.Add(new Parameter(a.key, (double)f));
                    else if (a.value is bool b) ps.Add(new Parameter(a.key, b ? 1L : 0L));
                }
                FirebaseAnalytics.LogEvent(name, ps.ToArray());
            };
            if (_ready) send();
            else lock (Pending) { if (Pending.Count < 50) Pending.Enqueue(send); }
#endif
        }
    }
}
