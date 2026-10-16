using System;

namespace CircuitBreaker.Core
{
    /// <summary>Dates are stored as yyyyMMdd integers (local time). One place to fake "now" in tests.</summary>
    public static class Clock
    {
        /// <summary>Tests set this to simulate another moment; null means the real clock.</summary>
        public static DateTime? Override;

        public static DateTime Now => Override ?? DateTime.Now;
        public static int Today => ToInt(Now);

        public static int ToInt(DateTime d) => d.Year * 10000 + d.Month * 100 + d.Day;

        public static DateTime FromInt(int yyyymmdd) =>
            new DateTime(yyyymmdd / 10000, (yyyymmdd / 100) % 100, yyyymmdd % 100);

        public static int DaysBetween(int earlier, int later) => (int)(FromInt(later) - FromInt(earlier)).TotalDays;

        /// <summary>"16 Oct 2026", or "-" when there is no date yet.</summary>
        public static string Format(int yyyymmdd) => yyyymmdd <= 0 ? "-" : FromInt(yyyymmdd).ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }
}
