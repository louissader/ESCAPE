using System.IO;
using Escape.Automation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Edit-mode capture of every shipped level, WITH the HUD and touch
    /// controls (StoreScreenshotRig flips the screen-space-overlay canvases
    /// into camera space for the render — a plain camera RenderTexture never
    /// contains them, which is why the images uploaded for 1.1.0 were bare
    /// world renders and drew the Guideline 2.3.3 rejection).
    ///
    /// This path is a QA convenience: nothing has run, so it shows the
    /// AUTHORED state of the scene, not a played one — no parked-hero dimming,
    /// no HP drain, no cutscene/tutorial state, no Cinemachine framing. The
    /// files it writes are prefixed "editor_" for that reason.
    ///
    /// THE SET YOU UPLOAD comes from the play-mode capture, which runs the
    /// real game:  Window > General > Test Runner > PlayMode >
    /// ScreenshotTests.CaptureEveryLevel  (writes StoreAssets/store_*.png).
    /// </summary>
    public static class ScreenshotCapture
    {
        [MenuItem("Escape/Capture Level Screenshots (edit mode QA)")]
        public static void Run()
        {
            Directory.CreateDirectory(StoreScreenshotRig.OutputDirectory);

            var specs = new ScreenshotSpec[StoreScreenshotRig.StoreSpecs.Length + 1];
            StoreScreenshotRig.StoreSpecs.CopyTo(specs, 0);
            specs[specs.Length - 1] = StoreScreenshotRig.Qa1080;

            // Shipped progression only, named by the number PLAYERS see —
            // identical range and naming to ScreenshotTests, so neither file
            // has to change when SceneRouter.FirstLevel does.
            for (int level = SceneRouter.FirstLevel; level <= SceneRouter.LevelCount; level++)
            {
                string scene = $"Assets/Scenes/{SceneRouter.LevelSceneName(level)}.unity";
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);

                Camera cam = Camera.main;
                if (cam == null)
                {
                    Debug.LogError($"[Screenshot] no camera in {scene}");
                    continue;
                }

                int display = SceneRouter.DisplayNumber(level);

                foreach (ScreenshotSpec spec in specs)
                {
                    // try/finally on both: an exception must not leave this
                    // scene's canvases in camera space or the camera moved.
                    StoreScreenshotRig.CaptureSession session =
                        StoreScreenshotRig.Begin(cam, spec);
                    try
                    {
                        // The virtual camera doesn't run in edit mode, so frame
                        // the pair directly — clamped to the level's
                        // CameraBounds, which is what stops a capture showing
                        // the black void outside the level (the old +8/+4 offset
                        // at ortho 8 put roughly a fifth of the frame there).
                        StoreScreenshotRig.CameraFraming framing =
                            StoreScreenshotRig.FrameOnHeroes(cam);
                        try
                        {
                            session.Write(StoreScreenshotRig.PathFor(spec, "editor", display));
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

            Debug.LogWarning("[Screenshot] edit-mode QA captures written to " +
                             StoreScreenshotRig.OutputDirectory + "/editor_*.png. " +
                             "These show the AUTHORED scene, not a played one — upload the " +
                             "store_*.png set from the play-mode ScreenshotTests instead.");
        }
    }
}
