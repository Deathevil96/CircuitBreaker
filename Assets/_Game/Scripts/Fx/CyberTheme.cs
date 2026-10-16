using UnityEngine;

namespace CircuitBreaker.Fx
{
    /// <summary>Cyber-Circuit Data Packets skin (GDD section 5).</summary>
    public static class CyberTheme
    {
        public static readonly Color Background = Hex("05070D");
        public static readonly Color Pad = new Color(0.82f, 0.84f, 0.88f, 1f);       // copper solder pad
        public static readonly Color Trace = new Color(0.07f, 0.20f, 0.26f, 1f);         // PCB trace
        public static readonly Color BlockedFlash = Hex("FF3B5C");
        public static readonly Color Hint = Hex("FFD23F");
        public static readonly Color IceColor = Hex("A9ECFF");
        public static readonly Color LockColor = Hex("7C8499");
        public static readonly Color Good = Hex("3DFF7A");
        public static readonly Color PanelFill = new Color(0.04f, 0.07f, 0.12f, 0.96f);
        public static readonly Color PanelEdge = Hex("00F0FF");
        public static readonly Color Text = Hex("D8F6FF");

        public static readonly Color[] Palette =
        {
            Hex("00F0FF"), // cyan
            Hex("FF2E93"), // magenta
            Hex("3DFF7A"), // lime
            Hex("FFC400"), // amber
            Hex("8A5CFF"), // violet
        };

        public static Color Arrow(int colorIndex) => Palette[Mathf.Abs(colorIndex) % Palette.Length];

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
