using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using CircuitBreaker.Core;

namespace CircuitBreaker.Platform
{
    /// <summary>
    /// Coin packs and Remove Ads through Unity IAP (Google Play Billing). Purchases are granted on the device and then
    /// confirmed to the store; each transaction id is granted at most once. The shop is hidden in the EEA, UK and
    /// Switzerland (see Terms and Conditions). The game never depends on the store being reachable.
    /// </summary>
    public static class IapService
    {
        public struct Pack
        {
            public string id, title, fallbackPrice;
            public int coins;
        }

        public const string RemoveAdsId = "remove_ads";
        public static readonly Pack[] Packs =
        {
            new Pack { id = "coins_300",  coins = 300,  title = "Handful of coins",  fallbackPrice = "$0.99" },
            new Pack { id = "coins_1000", coins = 1000, title = "Pouch of coins",    fallbackPrice = "$2.99" },
            new Pack { id = "coins_2500", coins = 2500, title = "Chest of coins",    fallbackPrice = "$5.99" },
        };

        /// <summary>Set to false to sell everywhere.</summary>
        public static bool BlockRestrictedRegions = true;

        // EEA (EU + Iceland, Liechtenstein, Norway), United Kingdom, Switzerland.
        private static readonly HashSet<string> Restricted = new HashSet<string>
        {
            "AT","BE","BG","HR","CY","CZ","DK","EE","FI","FR","DE","GR","HU","IE","IT","LV","LT","LU","MT","NL","PL","PT","RO","SK","SI","ES","SE",
            "IS","LI","NO","GB","CH"
        };

        public static event Action Changed;                 // coins or ad-free status changed, or products arrived
        public static event Action<string> Notice;          // short message for the player

        private static StoreController _store;
        private static bool _starting, _connected;
        private static bool _productsReady;
        private const string SeenKey = "iapSeenTx";

        public static bool Ready => _connected && _productsReady;
        public static bool RegionAllowed => !BlockRestrictedRegions || !IsRestrictedRegion();

        /// <summary>True when the shop may be shown at all (region allows it).</summary>
        public static bool ShopVisible => RegionAllowed;

        public static string CountryCode { get; private set; } = "";

        public static bool IsRestrictedCode(string iso2) => !string.IsNullOrEmpty(iso2) && Restricted.Contains(iso2.ToUpperInvariant());

        // ----- startup -----
        public static async void Init()
        {
            if (_starting) return;
            _starting = true;
            if (!RegionAllowed) { Debug.Log("[IAP] shop hidden in this region (" + CountryCode + ")"); return; }
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                // Without BillingMode.json Unity falls back to its FAKE store ($0.01 for everything). Always use Google Play on Android.
                DefaultStoreHelper.OverrideDefaultStoreName(GooglePlay.Name);
#endif
                _store = UnityIAPServices.StoreController();
                _store.OnPurchasePending += OnPending;
                _store.OnPurchaseConfirmed += OnConfirmed;
                _store.OnPurchaseFailed += OnFailed;
                _store.OnProductsFetched += _ => { _productsReady = true; Changed?.Invoke(); };
                _store.OnProductsFetchFailed += f => Debug.LogWarning("[IAP] products fetch failed: " + f.FailureReason);
                _store.OnStoreDisconnected += d => { _connected = false; Debug.LogWarning("[IAP] store disconnected: " + d.message); };
                await _store.Connect();
                _connected = true;
                var defs = Packs.Select(p => new ProductDefinition(p.id, ProductType.Consumable)).ToList();
                defs.Add(new ProductDefinition(RemoveAdsId, ProductType.NonConsumable));
                _store.FetchProducts(defs);
                _store.FetchPurchases();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IAP] init failed: " + e.Message);
                _connected = false; _starting = false;
            }
        }

        // ----- prices -----
        public static string PriceOf(string id, string fallback)
        {
            var p = Find(id);
            return p != null ? p.metadata.localizedPriceString : fallback;
        }

        private static Product Find(string id) => _store?.GetProducts().FirstOrDefault(x => x.definition.id == id);

        // ----- buying -----
        public static void Buy(string id)
        {
            if (!RegionAllowed) { Notice?.Invoke("Purchases are not available in your region."); return; }
            var p = Find(id);
            if (!Ready || p == null) { Notice?.Invoke("Store not ready. Check your connection and try again."); Init(); return; }
            if (id == RemoveAdsId && SaveSystem.AdsRemoved) { Notice?.Invoke("Ads are already removed."); return; }
            _store.PurchaseProduct(p);
        }

        public static void Restore()
        {
            if (!RegionAllowed) { Notice?.Invoke("Purchases are not available in your region."); return; }
            if (!Ready) { Notice?.Invoke("Store not ready. Try again in a moment."); Init(); return; }
            _store.RestoreTransactions((ok, err) =>
            {
                Notice?.Invoke(ok ? "Purchases restored." : "Could not restore purchases.");
                Changed?.Invoke();
            });
        }

        // ----- store callbacks -----
        private static void OnPending(PendingOrder order)
        {
            string id = order.CartOrdered.Items().FirstOrDefault()?.Product.definition.id;
            string tx = order.Info.TransactionID ?? "";
            if (id != null && (tx.Length == 0 || !AlreadyGranted(tx)))
            {
                if (id == RemoveAdsId)
                {
                    SaveSystem.AdsRemoved = true;
                    AdsRemovedNow();
                    Notice?.Invoke("Ads removed. Thank you!");
                }
                else
                {
                    var pack = Packs.FirstOrDefault(x => x.id == id);
                    if (pack.coins > 0)
                    {
                        SaveSystem.Coins += pack.coins;
                        Notice?.Invoke("+" + pack.coins + " coins. Thank you!");
                    }
                }
                if (tx.Length > 0) MarkGranted(tx);
                SaveSystem.Save();
                Changed?.Invoke();
            }
            _store.ConfirmPurchase(order);          // grant first, then confirm: an unconfirmed order is re-delivered, never lost
        }

        private static void OnConfirmed(Order order)
        {
            if (order is FailedOrder f) Debug.LogWarning("[IAP] confirm failed: " + f.FailureReason + " " + f.Details);
        }

        private static void OnFailed(FailedOrder f)
        {
            Debug.Log("[IAP] purchase failed: " + f.FailureReason);
            if (f.FailureReason != PurchaseFailureReason.UserCancelled) Notice?.Invoke("Purchase did not complete. You were not charged.");
        }

        private static void AdsRemovedNow()
        {
            CircuitBreaker.Ads.AdService.Provider?.SetBannerVisible(false);
        }

        // ----- double-grant guard (last 30 transaction ids) -----
        private static bool AlreadyGranted(string tx) => SaveSystem.GetString(SeenKey).Split(';').Contains(tx);

        private static void MarkGranted(string tx)
        {
            var list = SaveSystem.GetString(SeenKey).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            list.Add(tx);
            if (list.Count > 30) list.RemoveRange(0, list.Count - 30);
            SaveSystem.SetString(SeenKey, string.Join(";", list));
        }

        // ----- region -----
        private static bool IsRestrictedRegion()
        {
            string c = DetectCountry();
            CountryCode = c;
            return c.Length == 2 && Restricted.Contains(c);
        }

        /// <summary>Device-side only. Checks the SIM and network country on Android and the system region, and blocks if any of them is restricted.</summary>
        private static string DetectCountry()
        {
            var seen = new List<string>();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var tm = activity.Call<AndroidJavaObject>("getSystemService", "phone"))
                {
                    if (tm != null)
                    {
                        seen.Add(tm.Call<string>("getSimCountryIso") ?? "");
                        seen.Add(tm.Call<string>("getNetworkCountryIso") ?? "");
                    }
                }
            }
            catch (Exception) { }
#endif
            try { seen.Add(RegionInfo.CurrentRegion.TwoLetterISORegionName); } catch (Exception) { }
            try
            {
                string[] parts = Application.systemLanguage == SystemLanguage.Unknown ? null : CultureInfo.CurrentCulture.Name.Split('-');
                if (parts != null && parts.Length > 1) seen.Add(parts[parts.Length - 1]);
            }
            catch (Exception) { }

            string first = "";
            foreach (var s in seen)
            {
                string u = (s ?? "").Trim().ToUpperInvariant();
                if (u.Length != 2) continue;
                if (first.Length == 0) first = u;
                if (Restricted.Contains(u)) return u;       // any restricted signal hides the shop
            }
            return first;
        }
    }
}
