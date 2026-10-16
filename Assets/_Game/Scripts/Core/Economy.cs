using System;
using System.Collections.Generic;
using UnityEngine;
using CircuitBreaker.Generation;

namespace CircuitBreaker.Core
{
    public enum AdStatus { Ready, Cooldown, Unavailable }

    /// <summary>
    /// All rewards, caps, prices and ad rules. Tune numbers here only; bump BuildInfo.EconomyVersion when you do.
    /// Design rule: the further the player gets, the harder free power-ups are to come by.
    /// </summary>
    public static class Economy
    {
        // ----- inventory -----
        public const int StartHints = 3;
        public const int HintUnlock = 1, ExtractUnlock = 8, RulerUnlock = 15;
        public const int HintCap = 3, RulerCap = 3, ExtractCap = 2;      // free sources never push past this
        public const int PurchaseOverCap = 2;                            // coins may buy up to cap + this

        public static int Cap(string id) => id == "hint" ? HintCap : id == "ruler" ? RulerCap : ExtractCap;

        // ----- rewarded ads -----
        public const int AdHintAmount = 2;
        public const int AdCooldownSeconds = 300;          // 5 minutes between hint ads (persisted across restarts)
        public const int ContinueAdsPerSession = 1;         // "+1 heart" ad, once per app launch

        // ----- free hints for returning players -----
        public const int DailyHint = 1;                    // first launch of each calendar day
        public const int SessionGiftMinGapMinutes = 20;    // restart-farming guard for the "exhausted" gift

        // ----- coins -----
        public const int RefillCost = 50;
        public static int Price(string id, int level)
        {
            int basePrice = id == "hint" ? 30 : id == "ruler" ? 60 : 120;
            float scale = Mathf.Min(2.5f, 1f + level / 100f);        // pricier as you progress
            return Mathf.RoundToInt(basePrice * scale / 5f) * 5;
        }
        // If a reward would overflow the cap it turns into coins instead of being lost.
        public static int OverflowCoins(string id) => id == "hint" ? 15 : id == "ruler" ? 25 : 50;

        // ----- difficulty rewards: the tougher the tier, the better the (still capped) reward -----
        public static Difficulty DifficultyOf(int level) => LevelCatalog.DifficultyOf(level);

        public static void TierReward(Difficulty d, out int hints, out int rulers, out int extracts, out int coins)
        {
            hints = rulers = extracts = coins = 0;
            switch (d)
            {
                case Difficulty.Hard: hints = 1; coins = 25; break;
                case Difficulty.SuperHard: hints = 2; coins = 40; break;
                case Difficulty.UltraHard: hints = 2; rulers = 1; extracts = 1; coins = 60; break;
                case Difficulty.Nightmare: hints = 3; rulers = 2; extracts = 1; coins = 150; break;
            }
        }

        public static string TierRewardText(Difficulty d)
        {
            TierReward(d, out int h, out int r, out int e, out int c);
            var parts = new List<string>();
            if (h > 0) parts.Add(h + " hint" + (h > 1 ? "s" : ""));
            if (r > 0) parts.Add(r + " ruler" + (r > 1 ? "s" : ""));
            if (e > 0) parts.Add(e + " extract");
            return parts.Count == 0 ? "" : string.Join(", ", parts);
        }

        // ----- drop rules (all get rarer with level) -----
        public static float RulerChance(int level) => Mathf.Clamp(0.22f - 0.0014f * level, 0.05f, 0.22f);
        public static int RulerPity(int level) => 10 + level / 30;               // guaranteed after this many dry perfect clears
        public static int ExtractStreak(int level) => Mathf.Min(10, 5 + level / 60); // perfect clears in a row

        public struct Rewards
        {
            public int coins, hints, rulers, extracts;
            public List<string> lines;
            public bool Any => hints + rulers + extracts > 0;
        }

        /// <summary>Applies everything a cleared level earns. Call only for real (non-debug) wins.</summary>
        public static Rewards ResolveWin(int level, bool perfect, int bestCombo)
        {
            var r = new Rewards { lines = new List<string>() };
            var diff = DifficultyOf(level);
            TierReward(diff, out int th, out int tr, out int te, out int tc);
            r.coins = 10 + 2 * bestCombo + (perfect ? 10 : 0) + tc;

            if (level >= 6) SaveSystem.PerfectStreak = perfect ? SaveSystem.PerfectStreak + 1 : 0;

            if (th > 0) Grant("hint", th, ref r);
            if (tr > 0) Grant("ruler", tr, ref r);
            if (te > 0) Grant("extract", te, ref r);

            if (tr == 0 && perfect && level >= RulerUnlock)
            {
                SaveSystem.RulerDry++;
                if (UnityEngine.Random.value < RulerChance(level) || SaveSystem.RulerDry >= RulerPity(level))
                {
                    Grant("ruler", 1, ref r);
                    SaveSystem.RulerDry = 0;
                }
            }

            if (level >= ExtractUnlock && perfect && SaveSystem.PerfectStreak >= ExtractStreak(level))
            {
                Grant("extract", 1, ref r);
                SaveSystem.PerfectStreak = 0;
                r.lines.Add("PERFECT STREAK BONUS");
            }

            SaveSystem.Coins += r.coins;
            SaveSystem.Save();
            return r;
        }

        private static void Grant(string id, int amount, ref Rewards r)
        {
            int added = SaveSystem.AddStock(id, amount, Cap(id));
            int overflow = amount - added;
            string name = id.ToUpperInvariant();
            if (added > 0) r.lines.Add($"+{added} {name}");
            if (overflow > 0)
            {
                int c = overflow * OverflowCoins(id);
                r.coins += c;
                r.lines.Add($"{name} FULL: +{c} COINS");
            }
            if (id == "hint") r.hints += added; else if (id == "ruler") r.rulers += added; else r.extracts += added;
        }

        /// <summary>One-time gift when a power-up unlocks. Returns a message or null.</summary>
        public static string CheckUnlockGift(int level)
        {
            if (level == ExtractUnlock && !SaveSystem.GiftGiven("extract"))
            {
                SaveSystem.SetGiftGiven("extract");
                SaveSystem.AddStock("extract", 1, ExtractCap);
                SaveSystem.Save();
                return "EXTRACT UNLOCKED: +1 (REMOVES ANY ARROW)";
            }
            if (level == RulerUnlock && !SaveSystem.GiftGiven("ruler"))
            {
                SaveSystem.SetGiftGiven("ruler");
                SaveSystem.AddStock("ruler", 1, RulerCap);
                SaveSystem.Save();
                return "RULER UNLOCKED: +1 (SHOWS EVERY PATH)";
            }
            return null;
        }

        /// <summary>Run at app launch. Daily bonus first, otherwise a "welcome back" hint when exhausted.</summary>
        public static string BeginSession(DateTime? nowOverride = null)
        {
            var now = nowOverride ?? DateTime.Now;
            SaveSystem.Sessions++;
            int today = now.Year * 10000 + now.Month * 100 + now.Day;

            if (SaveSystem.LastDailyDay != today)
            {
                SaveSystem.LastDailyDay = today;
                int added = SaveSystem.AddStock("hint", DailyHint, HintCap);
                SaveSystem.Save();
                if (added > 0) { SaveSystem.LastSessionGiftUtc = now.ToUniversalTime(); return "DAILY BONUS: +1 HINT"; }
            }

            if (SaveSystem.Stock("hint") == 0)
            {
                double minutes = (now.ToUniversalTime() - SaveSystem.LastSessionGiftUtc).TotalMinutes;
                if (minutes >= SessionGiftMinGapMinutes)
                {
                    SaveSystem.SetStock("hint", 1);
                    SaveSystem.LastSessionGiftUtc = now.ToUniversalTime();
                    SaveSystem.Save();
                    return "WELCOME BACK: +1 HINT";
                }
            }
            SaveSystem.Save();
            return null;
        }

        // ----- ads -----
        public static AdStatus HintAdStatus(out int cooldownSeconds, DateTime? nowOverride = null)
        {
            cooldownSeconds = 0;
            if (!Ads.AdService.RewardedReady) return AdStatus.Unavailable;
            double since = ((nowOverride ?? DateTime.UtcNow) - SaveSystem.LastHintAdUtc).TotalSeconds;
            if (since < AdCooldownSeconds)
            {
                cooldownSeconds = Mathf.CeilToInt((float)(AdCooldownSeconds - since));
                return AdStatus.Cooldown;
            }
            return AdStatus.Ready;
        }

        /// <summary>Buys one power-up with coins. Allowed slightly past the free cap.</summary>
        public static bool Buy(string id, int level)
        {
            int price = Price(id, level);
            if (SaveSystem.Coins < price) return false;
            if (SaveSystem.Stock(id) >= Cap(id) + PurchaseOverCap) return false;
            SaveSystem.Coins -= price;
            SaveSystem.SetStock(id, SaveSystem.Stock(id) + 1);
            SaveSystem.Save();
            return true;
        }
    }
}
