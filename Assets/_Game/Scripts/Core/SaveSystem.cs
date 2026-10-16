using System;
using System.Collections.Generic;
using UnityEngine;

namespace CircuitBreaker.Core
{
    /// <summary>
    /// PlayerPrefs-backed save data. Schema version lives in BuildInfo.SaveSchema.
    /// Tests call EnterSandbox to work on a separate key prefix that is deleted afterwards.
    /// </summary>
    public static class SaveSystem
    {
        private static string P = "cb_";
        private static HashSet<string> _sandboxKeys;      // non-null while a sandbox is active

        // ----- sandbox (tests) -----
        public static void EnterSandbox()
        {
            P = "cbtest" + DateTime.Now.Ticks + "_";      // unique per sandbox, so leftovers from an interrupted run can never leak in
            _sandboxKeys = new HashSet<string>();
            Clock.Override = null;
        }

        public static void ExitSandbox()
        {
            if (_sandboxKeys != null) foreach (var k in _sandboxKeys) PlayerPrefs.DeleteKey(k);
            _sandboxKeys = null;
            P = "cb_";
            Clock.Override = null;
        }

        // ----- raw access (everything funnels through these) -----
        private static int GetI(string key, int def) => PlayerPrefs.GetInt(P + key, def);
        private static void SetI(string key, int v) { PlayerPrefs.SetInt(P + key, v); _sandboxKeys?.Add(P + key); }
        private static string GetS(string key, string def) => PlayerPrefs.GetString(P + key, def);
        private static void SetS(string key, string v) { PlayerPrefs.SetString(P + key, v); _sandboxKeys?.Add(P + key); }

        public static int GetInt(string key, int def = 0) => GetI(key, def);
        public static void SetInt(string key, int value) => SetI(key, value);
        public static string GetString(string key, string def = "") => GetS(key, def);
        public static void SetString(string key, string value) => SetS(key, value);
        public static bool HasKey(string key) => PlayerPrefs.HasKey(P + key);
        public static void DeleteKey(string key) => PlayerPrefs.DeleteKey(P + key);

        // ----- core progress -----
        public static int Progress { get => GetI("progress", 1); set => SetI("progress", Mathf.Max(Progress, value)); }
        public static int Current { get => GetI("current", 1); set => SetI("current", value); }
        public static int Coins { get => GetI("coins", 0); set => SetI("coins", Mathf.Max(0, value)); }
        public static bool Muted { get => GetI("muted", 0) == 1; set => SetI("muted", value ? 1 : 0); }
        /// <summary>Bloom on the arrows. Defaults to off on low-memory phones to protect frame rate and battery.</summary>
        public static bool Glow { get => GetI("glow", SystemInfo.systemMemorySize >= 3000 ? 1 : 0) == 1; set => SetI("glow", value ? 1 : 0); }
        public static bool Haptics { get => GetI("haptics", 1) == 1; set => SetI("haptics", value ? 1 : 0); }
        public static int BestCombo { get => GetI("bestCombo", 0); set => SetI("bestCombo", Mathf.Max(BestCombo, value)); }

        // ----- monetization / economy state -----
        public static bool AdsRemoved { get => GetI("adsRemoved", 0) == 1; set => SetI("adsRemoved", value ? 1 : 0); }
        public static int PerfectStreak { get => GetI("perfectStreak", 0); set => SetI("perfectStreak", Mathf.Max(0, value)); }
        public static int RulerDry { get => GetI("rulerDry", 0); set => SetI("rulerDry", Mathf.Max(0, value)); }
        public static int Sessions { get => GetI("sessions", 0); set => SetI("sessions", value); }
        public static int LastDailyDay { get => GetI("lastDaily", 0); set => SetI("lastDaily", value); }
        public static int GeneratorVersionSeen { get => GetI("genVer", 0); set => SetI("genVer", value); }
        public static int EconomyVersionSeen { get => GetI("ecoVer", 0); set => SetI("ecoVer", value); }
        public static bool GiftGiven(string id) => GetI("gift_" + id, 0) == 1;
        public static void SetGiftGiven(string id) => SetI("gift_" + id, 1);

        // Timestamps are stored as UTC ticks in strings (PlayerPrefs has no 64-bit int).
        public static DateTime LastHintAdUtc { get => GetTime("lastHintAd"); set => SetTime("lastHintAd", value); }
        public static DateTime LastSessionGiftUtc { get => GetTime("lastGift"); set => SetTime("lastGift", value); }

        private static DateTime GetTime(string key)
        {
            string s = GetS(key, "");
            return long.TryParse(s, out long ticks) && ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue;
        }
        private static void SetTime(string key, DateTime t) => SetS(key, t.Ticks.ToString());

        // ----- power-up inventory -----
        public static int Stock(string id) => GetI("b_" + id, id == "hint" ? Economy.StartHints : 0);
        public static void SetStock(string id, int n) => SetI("b_" + id, Mathf.Max(0, n));

        /// <summary>Adds up to `n` without exceeding `cap`. Returns how many were actually added.</summary>
        public static int AddStock(string id, int n, int cap)
        {
            int cur = Stock(id);
            int add = Mathf.Max(0, Mathf.Min(n, cap - cur));
            SetStock(id, cur + add);
            return add;
        }

        public static bool TakeStock(string id)
        {
            int cur = Stock(id);
            if (cur <= 0) return false;
            SetStock(id, cur - 1);
            return true;
        }

        public static void Save() => PlayerPrefs.Save();

        /// <summary>Deletes ALL saved data for the real game. Not allowed inside a test sandbox.</summary>
        public static void ResetAll()
        {
            if (_sandboxKeys != null) { foreach (var k in _sandboxKeys) PlayerPrefs.DeleteKey(k); _sandboxKeys.Clear(); return; }
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        /// <summary>Upgrades older saves. Run once at startup before anything reads the data.</summary>
        public static string Migrate()
        {
            int schema = GetI("schema", 0);
            string note = null;
            if (schema < 2)
            {
                // v0/v1 -> v2: test builds handed out boosters freely. Clamp stocks to the new caps.
                foreach (var id in new[] { "hint", "extract", "ruler" })
                {
                    if (!PlayerPrefs.HasKey(P + "b_" + id)) continue;
                    int cap = Economy.Cap(id) + 2;
                    if (Stock(id) > cap) SetStock(id, cap);
                }
                note = "save migrated to schema 2";
            }
            if (schema < 3)
            {
                // v2 -> v3: stats, records, achievements, run slots and attempts are new keys; nothing to convert.
                // The old single "run" slot (never shipped) is dropped.
                PlayerPrefs.DeleteKey(P + "run");
                note = (note == null ? "" : note + "; ") + "save migrated to schema 3";
            }
            SetI("schema", BuildInfo.SaveSchema);

            if (GeneratorVersionSeen != 0 && GeneratorVersionSeen != BuildInfo.GeneratorVersion)
                note = (note == null ? "" : note + "; ") + $"level generator changed ({GeneratorVersionSeen} -> {BuildInfo.GeneratorVersion}): level numbers now produce different boards";
            GeneratorVersionSeen = BuildInfo.GeneratorVersion;
            EconomyVersionSeen = BuildInfo.EconomyVersion;
            PlayerPrefs.Save();
            return note;
        }
    }
}
