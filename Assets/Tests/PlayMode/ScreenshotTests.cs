using System.Collections;
using System.IO;
using Escape.Automation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// THE App Store screenshot path. Play-mode capture through the real URP 2D
/// renderer, with the real HUD, the real touch controls and real gameplay
/// state — i.e. the app in use, which is precisely what the 2026-08-19
/// Guideline 2.3.3 rejection said the old shots were not: every uploaded
/// image was a bare world render, because a screen-space-overlay canvas is
/// composited by the display and never appears in a camera's RenderTexture.
/// StoreScreenshotRig fixes that (and puts every canvas back afterwards).
///
/// Runs the whole shipped campaign (SceneRouter.FirstLevel..LevelCount) and
/// names files by the number the PLAYER sees (SceneRouter.DisplayNumber), so
/// nothing here has to change when the shipped range does.
///
/// Read StoreScreenshotRig's class comment before uploading: the pixel sizes
/// are exactly Apple's, but editor safe-area insets are substituted, not
/// measured.
/// </summary>
public class ScreenshotTests
{
    /// <summary>Seconds to let a level settle: ScreenFader's 0.25 s fade out,
    /// heroes landing on their spawn platform, the objective banner's 0.4 s
    /// fade in, lights and lava warming up.</summary>
    const float SettleSeconds = 1.25f;

    [UnityTest]
    public IEnumerator CaptureEveryLevel()
    {
        Directory.CreateDirectory(StoreScreenshotRig.OutputDirectory);

        // The two required store sets, plus the internal 1920x1080 reference
        // (which matches no App Store size class — never upload it).
        var specs = new ScreenshotSpec[StoreScreenshotRig.StoreSpecs.Length + 1];
        StoreScreenshotRig.StoreSpecs.CopyTo(specs, 0);
        specs[specs.Length - 1] = StoreScreenshotRig.Qa1080;

        // Swap is what every touch device resolves to (ControlModeResolver);
        // the editor would otherwise resolve to Co-op and hand the store a
        // layout no phone or iPad player ever sees — and Co-op's two mirrored
        // clusters are what drew the 1.1.0 Guideline 4 "crowded" rejection.
        // Set BEFORE the first load so SwapModeController picks it up in
        // Start: parked hero dimmed to ParkedAlpha, active portrait glowing,
        // one control cluster.
        SaveSystem.ControlModeSetting previousMode = SaveSystem.ControlMode;
        SaveSystem.ControlMode = SaveSystem.ControlModeSetting.Swap;

        try
        {
            // Shipped progression only, named by the number PLAYERS see —
            // store screenshots must never show levels outside FirstLevel..LevelCount.
            for (int n = SceneRouter.FirstLevel; n <= SceneRouter.LevelCount; n++)
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(SceneRouter.LevelSceneName(n));
                while (!op.isDone)
                    yield return null;

                float t = 0f;
                while (t < SettleSeconds)
                {
                    t += Time.deltaTime;
                    yield return null;
                }

                Camera cam = Camera.main;
                Assert.IsNotNull(cam, $"L{n}: no main camera");
                if (ModalGuard.IsModalOpen)
                    Debug.LogWarning($"[Screenshot] L{n}: a modal is open; the rig will hide it.");

                int display = SceneRouter.DisplayNumber(n);

                foreach (ScreenshotSpec spec in specs)
                {
                    string prefix = spec.Slug == StoreScreenshotRig.Qa1080.Slug ? "play" : "store";

                    // try/finally on BOTH the session and the framing: a throw
                    // must never leave the scene's canvases in camera space or
                    // the camera parked off its virtual camera.
                    StoreScreenshotRig.CaptureSession session =
                        StoreScreenshotRig.Begin(cam, spec);
                    try
                    {
                        StoreScreenshotRig.CameraFraming framing =
                            StoreScreenshotRig.FrameOnHeroes(cam);
                        try
                        {
                            // Two frames: one for the canvas rebuild at the
                            // capture size, one for the graphics to follow it.
                            yield return null;
                            yield return null;
                            session.Write(StoreScreenshotRig.PathFor(spec, prefix, display));
                        }
                        finally
                        {
                            framing.Dispose();
                        }
                    }
                    finally
                    {
                        session.Dispose();
                    }
                }
            }
        }
        finally
        {
            SaveSystem.ControlMode = previousMode;
        }

        Assert.Pass("captures written to " + StoreScreenshotRig.OutputDirectory);
    }
}
