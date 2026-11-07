using UnityEngine;

namespace CircuitBreaker.Fx
{
    /// <summary>Cyber-Circuit Data Packets skin (GDD section 5).</summary>
    public static class CyberTheme
    {
        public static readonly Color Background = Hex("15103A");
        public static readonly Color Pad = new Color(0.85f, 0.82f, 1.00f, 1f);       // copper solder pad
        public static readonly Color Trace = new Color(0.30f, 0.24f, 0.70f, 1f);         // PCB trace
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
            Hex("2EE6FF"), // cyan
            Hex("FF5FA8"), // pink
            Hex("8CF542"), // lime
            Hex("FFC930"), // amber
            Hex("A175FF"), // violet
        };

        public static Color Arrow(int colorIndex) => Palette[Mathf.Abs(colorIndex) % Palette.Length];

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
