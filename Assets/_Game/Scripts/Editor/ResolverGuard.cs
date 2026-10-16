#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;

namespace CircuitBreaker.EditorTools
{
    /// <summary>
    /// Keeps the Android Resolver (External Dependency Manager) from re-resolving on every recompile, platform switch
    /// and build. The ads dependencies are already in Assets/Plugins/Android/mainTemplate.gradle and Gradle downloads
    /// them, so the resolver only produces popups. Runs on every Editor load and compile and switches it off again.
    /// To resolve on purpose: Assets > External Dependency Manager > Android Resolver > Force Resolve (then reload).
    /// </summary>
    [InitializeOnLoad]
    public static class ResolverGuard
    {
        private static readonly string[] Properties = { "EnableAutoResolution", "AutoResolveOnBuild", "PromptBeforeAutoResolution", "AutoResolutionDisabledWarning" };
        private const string SettingsFile = "ProjectSettings/ExternalDependencyManagerResolverSettings.asset";

        static ResolverGuard()
        {
            Apply();
            EditorApplication.delayCall += Apply;          // again once the resolver finished loading
        }

        private static void Apply()
        {
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = a.GetType("GooglePlayServices.SettingsDialog");
                    if (t == null) continue;
                    foreach (var name in Properties)
                    {
                        var p = t.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (p != null && p.CanWrite && p.PropertyType == typeof(bool) && (bool)p.GetValue(null, null)) p.SetValue(null, false, null);
                    }
                }
                if (File.Exists(SettingsFile))
                {
                    string s = File.ReadAllText(SettingsFile), o = s;
                    foreach (var k in new[] { "enableAutoResolution", "autoResolveOnBuild", "promptBeforeAutoResolution", "autoResolutionDisabledWarning" })
                        s = Regex.Replace(s, "(" + k + @":\s*\r?\n\s+value: )1", "${1}0");
                    if (s != o) File.WriteAllText(SettingsFile, s);
                }
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("[CircuitBreaker] ResolverGuard: " + e.Message); }
        }
    }
}
#endif
