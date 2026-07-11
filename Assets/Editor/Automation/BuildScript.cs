using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// CI-grade builds. Both entry points run ConfigureMobileSettings first
    /// so artifacts can never diverge from store config.
    ///   Android: AAB, IL2CPP, ARM64-only. Release keystore from env vars
    ///   (ESCAPE_KEYSTORE_PATH/PASS/ALIAS/ALIAS_PASS); loud debug fallback.
    ///   iOS: Xcode project export (signing needs the human's team ID).
    /// </summary>
    public static class BuildScript
    {
        static string[] ScenePaths()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }

        public static void BuildAndroid()
        {
            ConfigureMobileSettings.Run();

            string keystore = Environment.GetEnvironmentVariable("ESCAPE_KEYSTORE_PATH");
            if (!string.IsNullOrEmpty(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("ESCAPE_KEYSTORE_PASS");
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("ESCAPE_KEYSTORE_ALIAS");
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("ESCAPE_KEYSTORE_ALIAS_PASS");
                Debug.Log("[BuildScript] Using release keystore: " + keystore);
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("[BuildScript] *** DEBUG KEYSTORE *** — artifact is NOT uploadable. " +
                                 "Set ESCAPE_KEYSTORE_PATH/PASS/ALIAS/ALIAS_PASS for a release build (see RELEASE.md).");
            }

            bool development = Environment.GetCommandLineArgs().Contains("-escapeDevBuild");
            var options = new BuildPlayerOptions
            {
                scenes = ScenePaths(),
                locationPathName = "Builds/Android/ESCAPE.aab",
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            Report(BuildPipeline.BuildPlayer(options), "Android AAB");
        }

        public static void BuildIOS()
        {
            ConfigureMobileSettings.Run();

            var options = new BuildPlayerOptions
            {
                scenes = ScenePaths(),
                locationPathName = "Builds/iOS",
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            };

            Report(BuildPipeline.BuildPlayer(options), "iOS Xcode project");
        }

        /// <summary>Local playable build — keyboard co-op, touch UI hidden.</summary>
        public static void BuildMac()
        {
            // Legacy project setting had OpenGLCore; Apple silicon needs Metal.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX, new[]
            {
                UnityEngine.Rendering.GraphicsDeviceType.Metal,
            });

            var options = new BuildPlayerOptions
            {
                scenes = ScenePaths(),
                locationPathName = "Builds/Mac/ESCAPE.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            };
            Report(BuildPipeline.BuildPlayer(options), "Mac app");
        }

        static void Report(BuildReport report, string label)
        {
            var summary = report.summary;
            string message = $"[BuildScript] {label}: {summary.result} " +
                             $"({summary.totalErrors} errors, {summary.totalWarnings} warnings, " +
                             $"{summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalMinutes:F1} min) " +
                             $"→ {summary.outputPath}";
            if (summary.result == BuildResult.Succeeded)
                Debug.Log(message);
            else
                Debug.LogError(message);

            if (summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
