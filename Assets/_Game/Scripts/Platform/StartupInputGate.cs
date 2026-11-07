using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CircuitBreaker.Platform
{
    /// <summary>
    /// Throws away screen touches that happen while the game is still starting (logo splash, first level loading). Without this,
    /// Android keeps those touches and the game replays all of them at once when it finally starts, so the menu seems to "lag
    /// and then press everything". <see cref="OpenAfter"/> is called once the first board is ready; a failsafe opens the gate by
    /// itself so touch can never stay blocked.
    /// </summary>
    public static class StartupInputGate
    {
        private const double FailsafeSeconds = 12.0;
        private static double _openAt = double.MaxValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            _openAt = double.MaxValue;
            InputSystem.onEvent -= OnEvent;
            InputSystem.onEvent += OnEvent;
        }

        /// <summary>Touches made before now + seconds are ignored; later ones work normally.</summary>
        public static void OpenAfter(float seconds)
        {
            _openAt = System.Math.Min(_openAt, Time.realtimeSinceStartupAsDouble + seconds);
        }

        private static void OnEvent(InputEventPtr e, InputDevice device)
        {
            if (!(device is Pointer)) return;                            // touchscreen and mouse
            double now = Time.realtimeSinceStartupAsDouble;
            if (now > FailsafeSeconds && _openAt > now) _openAt = 0;       // never block for good
            if (e.time < _openAt) e.handled = true;
        }
    }
}
