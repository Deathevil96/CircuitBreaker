#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using CircuitBreaker.Core;
using Debug = UnityEngine.Debug;

namespace CircuitBreaker.EditorTools
{
    /// <summary>
    /// One-click Android setup and builds for Google Play. Menu: CircuitBreaker > Android.
    /// Closed-test builds include clearly labelled test ads; release builds contain no ad code until a real SDK is added.
    /// </summary>
    public static class BuildTools
    {
        private const string TestAdsDefine = "CB_TEST_ADS";
        private const string AdMobDefine = "CB_ADMOB";
        private const string AdMobProdDefine = "CB_ADMOB_PRODUCTION";
        private const string AdMobAndroidTestAppId = "ca-app-pub-3940256099942544~3347511713";   // Google public sample app id
        private const string AdMobAndroidProdAppId = "ca-app-pub-7841394126110372~5099598560";   // Arrow Breaker (AdMob)
        private const string AdMobIosTestAppId = "ca-app-pub-3940256099942544~1458002511";
        private const string KeystoreDir = "Keystore";
        private const string KeystoreFile = "Keystore/arrowbreaker-upload.keystore";
        private const string KeystoreInfo = "Keystore/keystore.local.json";
        private const string Alias = "arrowbreaker-upload";
        private const string DebugDefine = "CB_DEBUG";
        private const string BuildNumberFile = "ProjectSettings/BuildNumber.txt";
        private const string LogoPath = "Assets/_Game/Art/Branding/MISAL Interactive Metallic Logo.png";

        [Serializable] private class KeystoreData { public string path, alias, storePass, keyPass; }

        // ------------------------------------------------------------------
        // Menu
        // ------------------------------------------------------------------
        public enum AdMode { None, TestStub, AdMobTest, AdMobProduction }

        [MenuItem("CircuitBreaker/Android/Prepare (settings, icon, splash, keystore)")]
        public static void PrepareMenu()
        {
            string report = Prepare(AdMode.TestStub, debug: false);
            Debug.Log("[CircuitBreaker] Android prepared.\n" + report);
        }

        [MenuItem("CircuitBreaker/Android/1. Build Closed-Test AAB (test ads)")]
        public static void BuildClosedTest() => Build(AdMode.TestStub, bundle: true, debug: false);

        [MenuItem("CircuitBreaker/Android/2. Build Closed-Test AAB (AdMob test ads)")]
        public static void BuildClosedTestAdMob() => Build(AdMode.AdMobTest, bundle: true, debug: false);

        [MenuItem("CircuitBreaker/Android/3. Build Release AAB (AdMob production IDs)")]
        public static void BuildReleaseAdMob() => Build(AdMode.AdMobProduction, bundle: true, debug: false);

        private static bool _qa;
        private const string QaDefine = "CB_QA";

        [MenuItem("CircuitBreaker/Android/6. Build QA APK (test ads, level and coin jump file)")]
        public static void BuildQaApk()
        {
            _qa = true;
            try { Build(AdMode.TestStub, bundle: false, debug: false); }
            finally { _qa = false; }
        }

        [MenuItem("CircuitBreaker/Android/5. Build Release APK (no ads, install on a phone)")]
        public static void BuildReleaseApk() => Build(AdMode.None, bundle: false, debug: false);

        [MenuItem("CircuitBreaker/Android/4. Build Release AAB (no ads)")]
        public static void BuildRelease() => Build(AdMode.None, bundle: true, debug: false);

        // ------------------------------------------------------------------
        // Player settings
        // ------------------------------------------------------------------
        public static string Prepare(AdMode ads, bool debug, int versionCode = 0)
        {
            var sb = new StringBuilder();
            var t = NamedBuildTarget.Android;

            PlayerSettings.productName = BuildInfo.ProductName;
            PlayerSettings.companyName = BuildInfo.Company;
            PlayerSettings.bundleVersion = BuildInfo.AppVersion;
            PlayerSettings.Android.bundleVersionCode = versionCode > 0 ? versionCode : Mathf.Max(1, ReadBuildNumber());
            PlayerSettings.iOS.buildNumber = PlayerSettings.Android.bundleVersionCode.ToString();
            PlayerSettings.SetApplicationIdentifier(t, BuildInfo.PackageId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BuildInfo.PackageId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.runInBackground = true;

            PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;       // Play requires 64-bit
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;  // Android 7.1 (Unity 6 minimum)
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;   // newest installed (Play needs a recent target)
            PlayerSettings.Android.forceInternetPermission = true;                         // ads need it
            PlayerSettings.SetManagedStrippingLevel(t, ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetApiCompatibilityLevel(t, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.Android.optimizedFramePacing = true;
            ConfigureSplash(sb);
            DisableAutoResolve(sb);

            string defines = PlayerSettings.GetScriptingDefineSymbols(t);
            defines = SetDefine(defines, TestAdsDefine, ads == AdMode.TestStub);
            defines = SetDefine(defines, AdMobDefine, ads == AdMode.AdMobTest || ads == AdMode.AdMobProduction);
            defines = SetDefine(defines, AdMobProdDefine, ads == AdMode.AdMobProduction);
            defines = SetDefine(defines, DebugDefine, debug);
            defines = SetDefine(defines, QaDefine, _qa);
            PlayerSettings.SetScriptingDefineSymbols(t, defines);

            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.symlinkSources = false;

            GenerateAndAssignIcons();
            EnsureAdMobSettings(sb, ads == AdMode.AdMobProduction);
            EnsureKeystore(sb);

            sb.AppendLine($"  version {BuildInfo.AppVersion} (code {PlayerSettings.Android.bundleVersionCode}), package {PlayerSettings.GetApplicationIdentifier(t)}");
            sb.AppendLine($"  IL2CPP, ARM64, min API 25, target API auto, portrait, ads: {ads}, debug tools: {(debug ? "ON" : "off")}");
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        /// <summary>
        /// Play needs a higher version code for every upload. A simple counter in ProjectSettings/BuildNumber.txt
        /// (committed to git) starts at 1 and grows by one per build.
        /// </summary>
        private static int ReadBuildNumber()
        {
            if (!File.Exists(BuildNumberFile)) return 0;
            return int.TryParse(File.ReadAllText(BuildNumberFile).Trim(), out int n) ? n : 0;
        }

        private static int NextBuildNumber()
        {
            int n = ReadBuildNumber() + 1;
            File.WriteAllText(BuildNumberFile, n.ToString());
            return n;
        }

        // ------------------------------------------------------------------
        // Splash: the studio logo replaces "Made with Unity"
        // ------------------------------------------------------------------
        /// <summary>
        /// The Android Resolver re-resolves after every recompile and platform switch, which stalls the Editor.
        /// Dependencies are already in Assets/Plugins/Android; resolve manually (Assets > External Dependency Manager) only when the ads package changes.
        /// </summary>
        private static void DisableAutoResolve(StringBuilder sb)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = a.GetType("GooglePlayServices.SettingsDialog");
                var p = t?.GetProperty("EnableAutoResolution", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (p != null) { p.SetValue(null, false, null); sb.AppendLine("  Android Resolver auto-resolution: off"); return; }
            }
        }

        private static void ConfigureSplash(StringBuilder sb)
        {
            var importer = AssetImporter.GetAtPath(LogoPath) as TextureImporter;
            if (importer == null) { sb.AppendLine("  splash: logo not found at " + LogoPath + " (splash disabled)"); PlayerSettings.SplashScreen.show = false; return; }
            if (importer.textureType != TextureImporterType.Sprite || importer.maxTextureSize != 2048)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2.5f, sprite) };
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.backgroundColor = Color.black;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            sb.AppendLine("  splash: Misal Interactive logo, Unity logo off");
        }

        private static string SetDefine(string defines, string define, bool on)
        {
            var list = new System.Collections.Generic.List<string>((defines ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            list.Remove(define);
            if (on) list.Add(define);
            return string.Join(";", list);
        }

        // ------------------------------------------------------------------
        // AdMob plugin settings (the plugin refuses to build without an App ID)
        // ------------------------------------------------------------------
        private static void EnsureAdMobSettings(StringBuilder sb, bool production)
        {
            Type t = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = a.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings");
                if (t != null) break;
            }
            if (t == null) { sb.AppendLine("  AdMob plugin: not installed"); return; }

            const string dir = "Assets/GoogleMobileAds/Resources";
            const string path = dir + "/GoogleMobileAdsSettings.asset";
            var asset = AssetDatabase.LoadAssetAtPath(path, t) as ScriptableObject;
            if (asset == null)
            {
                Directory.CreateDirectory(dir);
                asset = ScriptableObject.CreateInstance(t);
                AssetDatabase.CreateAsset(asset, path);
            }
            const System.Reflection.BindingFlags f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var idField = t.GetField("adMobAndroidAppId", f);
            if (idField != null) idField.SetValue(asset, production ? AdMobAndroidProdAppId : AdMobAndroidTestAppId);
            SetIfEmpty(asset, t, "adMobIOSAppId", AdMobIosTestAppId, f);
            SetIfEmpty(asset, t, "userTrackingUsageDescription", "This identifier is used to deliver more relevant ads to you.", f);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            sb.AppendLine("  AdMob plugin: settings ready (" + (production ? "production" : "test") + " Android App ID)");
        }

        private static void SetIfEmpty(ScriptableObject asset, Type t, string field, string value, System.Reflection.BindingFlags f)
        {
            var fi = t.GetField(field, f);
            if (fi == null) return;
            if (string.IsNullOrEmpty(fi.GetValue(asset) as string)) fi.SetValue(asset, value);
        }

        // ------------------------------------------------------------------
        // Keystore (kept out of git; back it up, losing it blocks updates unless Play App Signing resets the key)
        // ------------------------------------------------------------------
        private static void EnsureKeystore(StringBuilder sb)
        {
            if (File.Exists(KeystoreFile) && File.Exists(KeystoreInfo)) { sb.AppendLine("  upload keystore: found"); return; }
            Directory.CreateDirectory(KeystoreDir);
            string jdk = Path.Combine(EditorApplication.applicationContentsPath, "..", "..", "PlaybackEngines", "AndroidPlayer", "OpenJDK", "bin", "keytool");
            jdk = Path.GetFullPath(jdk);
            if (!File.Exists(jdk)) { sb.AppendLine("  upload keystore: keytool not found at " + jdk); return; }

            string pass = Guid.NewGuid().ToString("N").Substring(0, 20) + "Aa1";
            var psi = new ProcessStartInfo(jdk,
                $"-genkeypair -v -keystore \"{KeystoreFile}\" -alias {Alias} -keyalg RSA -keysize 2048 -validity 10000 " +
                $"-storepass {pass} -keypass {pass} -dname \"CN=Arrow Breaker, O=Misal Interactive, C=IN\"")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(psi))
            {
                p.WaitForExit(60000);
                if (p.ExitCode != 0) { sb.AppendLine("  upload keystore: FAILED " + p.StandardError.ReadToEnd()); return; }
            }
            File.WriteAllText(KeystoreInfo, JsonUtility.ToJson(new KeystoreData { path = KeystoreFile, alias = Alias, storePass = pass, keyPass = pass }, true));
            sb.AppendLine("  upload keystore: CREATED at " + KeystoreFile + " (password in " + KeystoreInfo + "). BACK IT UP.");
        }

        private static bool ApplyKeystore()
        {
            if (!File.Exists(KeystoreInfo)) return false;
            var k = JsonUtility.FromJson<KeystoreData>(File.ReadAllText(KeystoreInfo));
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(k.path);
            PlayerSettings.Android.keystorePass = k.storePass;
            PlayerSettings.Android.keyaliasName = k.alias;
            PlayerSettings.Android.keyaliasPass = k.keyPass;
            return true;
        }

        /// <summary>Passwords must not stay in ProjectSettings (it is committed to git).</summary>
        private static void ClearKeystoreSecrets()
        {
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasPass = "";
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = "";
            PlayerSettings.Android.keyaliasName = "";
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------
        // Icons (generated, so the project ships with a real launcher icon)
        // ------------------------------------------------------------------
        public static void GenerateAndAssignIcons()
        {
            const string dir = "Assets/_Game/Art/Icons";
            Directory.CreateDirectory(dir);
            var bg = SavePng(dir + "/icon_background.png", RenderIcon(432, withBackground: true, foreground: false));
            var fg = SavePng(dir + "/icon_foreground.png", RenderIcon(432, withBackground: false, foreground: true));
            var legacy = SavePng(dir + "/icon_legacy.png", RenderIcon(512, withBackground: true, foreground: true));
            SavePng(dir + "/store_icon_512.png", RenderIcon(512, withBackground: true, foreground: true));
            AssetDatabase.Refresh();

            var t = NamedBuildTarget.Android;
            Assign(t, AndroidPlatformIconKind.Adaptive, new[] { Load(dir + "/icon_background.png"), Load(dir + "/icon_foreground.png") });
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { Load(dir + "/icon_legacy.png") }, IconKind.Any);
        }

        private static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        private static void Assign(NamedBuildTarget t, PlatformIconKind kind, Texture2D[] textures)
        {
            var icons = PlayerSettings.GetPlatformIcons(t, kind);
            foreach (var icon in icons)
            {
                for (int i = 0; i < icon.maxLayerCount; i++)
                    icon.SetTexture(i < textures.Length ? textures[i] : textures[textures.Length - 1], i);
            }
            PlayerSettings.SetPlatformIcons(t, kind, icons);
        }

        private static string SavePng(string path, Texture2D tex)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            return path;
        }

        /// <summary>
        /// Launcher icon: three nested bent arrows (the game's signature shape), heads lined up on the right, shifting from cyan to
        /// magenta with a soft glow, on a near-black tile. `foreground` alone is the adaptive icon layer; the mark stays inside the central 60%.
        /// </summary>
        private static Texture2D RenderIcon(int size, bool withBackground, bool foreground)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            var cyan = new Color(0.00f, 0.92f, 1.00f, 1f);
            var magenta = new Color(1.00f, 0.20f, 0.62f, 1f);
            var edge = new Color(0.039f, 0.039f, 0.047f, 1f);
            var centre = new Color(0.085f, 0.09f, 0.115f, 1f);

            // Each arrow: up from the tail, then right to a triangular head. Outer arrow is cyan, inner is magenta.
            const int n = 3;
            float[] tailX = { 0.24f, 0.345f, 0.45f };
            float[] turnY = { 0.705f, 0.585f, 0.465f };
            const float tailY = 0.25f, headX = 0.655f, tipX = 0.745f, headHalf = 0.052f, half = 0.021f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = (x + 0.5f) / size, fy = (y + 0.5f) / size;
                    var p = new Vector2(fx, fy);
                    Color c = withBackground ? Color.Lerp(centre, edge, Mathf.Clamp01(Vector2.Distance(p, new Vector2(0.5f, 0.52f)) * 1.5f)) : new Color(0, 0, 0, 0);
                    if (foreground)
                    {
                        float best = 9f; int bestI = 0; float bestT = 0f;
                        for (int i = 0; i < n; i++)
                        {
                            var a = new Vector2(tailX[i], tailY); var b = new Vector2(tailX[i], turnY[i]); var e = new Vector2(headX, turnY[i]);
                            float len1 = (b - a).magnitude, len2 = (e - b).magnitude;
                            float d1 = SegDist(p, a, b, out float t1), d2 = SegDist(p, b, e, out float t2);
                            float dShaft = Mathf.Min(d1, d2) - half;
                            float along = d1 <= d2 ? t1 * len1 : len1 + t2 * len2;
                            float dHead = TriDist(p, new Vector2(headX, turnY[i] + headHalf), new Vector2(headX, turnY[i] - headHalf), new Vector2(tipX, turnY[i]));
                            float d = Mathf.Min(dShaft, dHead);
                            if (d < best)
                            {
                                best = d; bestI = i;
                                bestT = dHead < dShaft ? 1f : Mathf.Clamp01(along / (len1 + len2));
                            }
                        }
                        // colour steps across the three arrows and drifts along each one
                        float t = Mathf.Clamp01(bestI / (float)(n - 1) * 0.72f + bestT * 0.28f);
                        var lineC = Color.Lerp(cyan, magenta, t);
                        float core = Mathf.Clamp01(0.5f - best / 0.005f);
                        float glow = Mathf.Exp(-Mathf.Pow(Mathf.Max(0f, best) / 0.04f, 1.4f)) * 0.45f;
                        float inner = Mathf.Clamp01(0.5f - (best + 0.016f) / 0.008f) * 0.45f;    // brighter core line
                        var mark = Color.Lerp(lineC, Color.white, inner);
                        float al = Mathf.Max(core, glow);
                        c = withBackground ? Color.Lerp(c, mark, al) : new Color(mark.r, mark.g, mark.b, al);
                    }
                    px[y * size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            var ab = b - a;
            t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Signed distance to a triangle: negative inside.</summary>
        private static float TriDist(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d = Mathf.Min(SegDist(p, a, b, out _), Mathf.Min(SegDist(p, b, c, out _), SegDist(p, c, a, out _)));
            float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
            bool inside = (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
            return inside ? -d : d;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        // ------------------------------------------------------------------
        // Build
        // ------------------------------------------------------------------
        private static string AdLabel(AdMode ads)
        {
            switch (ads)
            {
                case AdMode.TestStub: return "closedtest";
                case AdMode.AdMobTest: return "closedtest-admob";
                case AdMode.AdMobProduction: return "release-admob";
                default: return "release-noads";
            }
        }

        private static void Build(AdMode ads, bool bundle, bool debug)
        {
            var sb = new StringBuilder();

            // 1. logic must be green
            string tests = SelfTests.RunAll();
            sb.AppendLine(tests);
            if (tests.Contains("failed") && !tests.Contains(" 0 failed"))
            {
                Debug.LogError("[CircuitBreaker] Build stopped: self tests failed.\n" + tests);
                return;
            }

            // 2. configure
            if (ads == AdMode.AdMobProduction && File.ReadAllText("Assets/_Game/Scripts/Ads/AdUnits.cs").Contains("REPLACE_ME"))
            {
                Debug.LogError("[CircuitBreaker] Build stopped: AdUnits.cs still contains REPLACE_ME production ad unit IDs.");
                return;
            }
            int code = NextBuildNumber();
            sb.AppendLine(Prepare(ads, debug, code));
            if (!ApplyKeystore()) { Debug.LogError("[CircuitBreaker] Build stopped: no upload keystore (run 'Prepare for Closed Testing')."); return; }
            EditorUserBuildSettings.buildAppBundle = bundle;
            if (AppLinks.PrivacyPolicyUrl.Contains("example.com"))
                sb.AppendLine("  WARNING: AppLinks still contains placeholder URLs. Replace before production.");

            // 3. output
            string dir = Path.GetFullPath("Builds/Android");
            Directory.CreateDirectory(dir);
            string name = $"ArrowBreaker_{BuildInfo.AppVersion}_code{PlayerSettings.Android.bundleVersionCode}_{AdLabel(ads)}{(debug ? "-debug" : "")}{(_qa ? "-qa" : "")}.{(bundle ? "aab" : "apk")}";
            string path = Path.Combine(dir, name);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/MainGame.unity" },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            var previous = EditorUserBuildSettings.activeBuildTarget;
            try
            {
                Debug.Log("[CircuitBreaker] Building " + name + " ...\n" + sb);
                BuildReport report = BuildPipeline.BuildPlayer(options);
                var s = report.summary;
                if (s.result == BuildResult.Succeeded)
                    Debug.Log($"[CircuitBreaker] BUILD SUCCEEDED: {path}  ({s.totalSize / (1024f * 1024f):F1} MB, {s.totalTime.TotalSeconds:F0} s)");
                else
                    Debug.LogError($"[CircuitBreaker] BUILD {s.result}: {s.totalErrors} errors, {s.totalWarnings} warnings. See the Editor log.");
            }
            finally
            {
                ClearKeystoreSecrets();
            }
        }
    }
}
#endif
