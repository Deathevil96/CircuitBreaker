namespace CircuitBreaker.Core
{
    /// <summary>Every version that matters, in one place. Bump the matching number when that area changes.</summary>
    public static class BuildInfo
    {
        /// <summary>Player-facing app version (keep PlayerSettings.bundleVersion in sync).</summary>
        public const string AppVersion = "1.0.0";

        /// <summary>Layout of the saved data. Bump when keys change and add a step in SaveSystem.Migrate.</summary>
        public const int SaveSchema = 3;

        /// <summary>Level generation output. Bump whenever the generator, catalog or shapes change what a level number produces.</summary>
        public const int GeneratorVersion = 5;

        /// <summary>Rewards, caps, prices, ad rules. Bump whenever Economy numbers change (useful for analytics comparisons).</summary>
        public const int EconomyVersion = 2;

        public const string ProductName = "Arrow Breaker";
        public const string StoreTitle = "Arrow Breaker: Circuit Puzzle";
        public const string Company = "Misal Interactive";
        public const string PackageId = "com.misalinteractive.arrowbreaker";

        public static string Display => $"v{AppVersion}  gen{GeneratorVersion}  eco{EconomyVersion}  save{SaveSchema}";
    }
}
