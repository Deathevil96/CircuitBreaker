using UnityEngine;

namespace CircuitBreaker.Fx
{
    public static class UiFont
    {
        private static Font _font;

        /// <summary>A clean system sans (SF / Helvetica on iOS and Mac, Roboto on Android), with the built-in font as a fallback.</summary>
        public static Font Get()
        {
            if (_font != null) return _font;
            try { _font = Font.CreateDynamicFontFromOSFont(new[] { "SF Pro Text", "Helvetica Neue", "Roboto", "Inter", "Arial" }, 36); }
            catch { _font = null; }
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }
    }
}
