using UnityEditor;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Phase 11 store configuration. Sets every PlayerSettings value the
    /// stores require and logs each one (read-back) so build artifacts can
    /// never silently diverge from the intended config. BuildScript calls
    /// this before building.
    /// </summary>
    public static class ConfigureMobileSettings
    {
        public const string BundleId = "com.louissader.escape";
        public const string Version = "1.2.0";   // Build 5: iPad layout + store screenshots (was 1.1.2)

        [MenuItem("Escape/Configure Mobile Settings")]
        public static void Run()
        {
            // Identity.
            PlayerSettings.companyName = "Louis Sader";
            PlayerSettings.productName = "ESCAPE";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, BundleId);
            PlayerSettings.bundleVersion = Version;
            // Fifth store submission: build 5 / versionCode 5 (1.0.0 shipped
            // as 1). Read-first with a floor, never a blind increment — the
            // stores reject downgrades, and BuildScript calls Run() once per
            // platform build, so incrementing here would drift per run.
            int.TryParse(PlayerSettings.iOS.buildNumber, out int iosBuild);
            PlayerSettings.iOS.buildNumber = Mathf.Max(5, iosBuild).ToString();
            PlayerSettings.Android.bundleVersionCode =
                Mathf.Max(5, PlayerSettings.Android.bundleVersionCode);

            // Graphics: the project renders in LINEAR color space, but the
            // legacy Android API list included OpenGLES2 (incompatible —
            // build error). Vulkan + GLES3 covers every minSdk-24 device.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
            {
                UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3,
            });

            // Android: IL2CPP + ARM64-only (Play 64-bit requirement; the
            // project previously shipped ARMv7+X86 with NO 64-bit slice).
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)24;
            // Google Play requires API 36 for new apps/updates from Aug 31 2026.
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.Android.maxAspectRatio = 2.4f;
            PlayerSettings.Android.forceInternetPermission = false;

            // iOS.
            // 15.0: Apple warns on every upload that MinimumOSVersion must be
            // >= 15.0 by Spring 2027. iOS 15 covers effectively the whole
            // installed base in 2026.
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.BottomEdge;
            PlayerSettings.iOS.hideHomeButton = false;

            // Orientation: landscape pair only.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // Splash: Unity logo mandatory on Personal; match the cave bg.
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.008f, 0.09f, 0.13f);

            ApplyIcons();

            // Read-back log — the artifact contract.
            Debug.Log("[ConfigureMobileSettings] applied:\n" +
                $"  bundleId(android)={PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)}\n" +
                $"  bundleId(ios)={PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS)}\n" +
                $"  version={PlayerSettings.bundleVersion} iosBuild={PlayerSettings.iOS.buildNumber} versionCode={PlayerSettings.Android.bundleVersionCode}\n" +
                $"  android backend={PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)} arch={PlayerSettings.Android.targetArchitectures}\n" +
                $"  android sdk min={PlayerSettings.Android.minSdkVersion} target={(int)PlayerSettings.Android.targetSdkVersion}\n" +
                $"  appBundle={EditorUserBuildSettings.buildAppBundle} tv={PlayerSettings.Android.androidTVCompatibility} maxAspect={PlayerSettings.Android.maxAspectRatio}\n" +
                $"  ios min={PlayerSettings.iOS.targetOSVersionString} fullScreen={PlayerSettings.iOS.requiresFullScreen} deferGestures={PlayerSettings.iOS.deferSystemGesturesMode}\n" +
                $"  orientation L={PlayerSettings.allowedAutorotateToLandscapeLeft}/{PlayerSettings.allowedAutorotateToLandscapeRight} P={PlayerSettings.allowedAutorotateToPortrait}/{PlayerSettings.allowedAutorotateToPortraitUpsideDown}");

            WriteAndroidManifest();
            AssetDatabase.SaveAssets();
        }

        static void ApplyIcons()
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Art/Generated/Icons/app_icon_1024.png");
            if (icon == null)
            {
                Debug.LogWarning("[ConfigureMobileSettings] app_icon_1024.png missing — icons skipped");
                return;
            }

            // Default icon slot feeds iOS + legacy Android automatically.
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown,
                new[] { icon }, IconKind.Any);

            // Android adaptive icons.
            Texture2D fg = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Art/Generated/Icons/adaptive_fg_432.png");
            Texture2D bg = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Art/Generated/Icons/adaptive_bg_432.png");
            var androidTarget = UnityEditor.Build.NamedBuildTarget.Android;
            var kinds = PlayerSettings.GetSupportedIconKinds(androidTarget);
            foreach (var kind in kinds)
            {
                var icons = PlayerSettings.GetPlatformIcons(androidTarget, kind);
                foreach (var slot in icons)
                {
                    if (kind.ToString() == "Adaptive" && fg != null && bg != null)
                        slot.SetTextures(bg, fg);
                    else
                        slot.SetTexture(icon);
                }
                PlayerSettings.SetPlatformIcons(androidTarget, kind, icons);
            }
            Debug.Log("[ConfigureMobileSettings] icons assigned (default + android adaptive)");
        }

        /// <summary>
        /// Custom AndroidManifest disabling predictive back: on Android 15+
        /// (target 35/36) onBackInvoked replaces KEYCODE_BACK by default and
        /// Unity 2022 never sees it — the pause-on-back design would die.
        /// </summary>
        static void WriteAndroidManifest()
        {
            const string dir = "Assets/Plugins/Android";
            const string path = dir + "/AndroidManifest.xml";
            if (!AssetDatabase.IsValidFolder("Assets/Plugins"))
                AssetDatabase.CreateFolder("Assets", "Plugins");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Plugins", "Android");

            const string manifest =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"">
  <application android:enableOnBackInvokedCallback=""false"">
    <activity android:name=""com.unity3d.player.UnityPlayerActivity""
              android:theme=""@style/UnityThemeSelector"">
      <intent-filter>
        <action android:name=""android.intent.action.MAIN"" />
        <category android:name=""android.intent.category.LAUNCHER"" />
      </intent-filter>
      <meta-data android:name=""unityplayer.UnityActivity"" android:value=""true"" />
    </activity>
  </application>
</manifest>";
            System.IO.File.WriteAllText(path, manifest);
            AssetDatabase.ImportAsset(path);
            Debug.Log("[ConfigureMobileSettings] custom AndroidManifest written (predictive back disabled)");
        }
    }
}
