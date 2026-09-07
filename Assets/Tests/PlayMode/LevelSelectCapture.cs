using System.Collections;
using Escape.Automation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Visual QA: renders the level-select with simulated progress (L1 beaten
/// with 2/3 coins — green celebration; L2 unlocked; rest locked) so the
/// star-coin slots and completion glow can be inspected as pixels.
/// Restores the player's real save state afterwards.
/// </summary>
public class LevelSelectCapture
{
    [UnityTest]
    public IEnumerator CaptureLevelSelectWithProgress()
    {
        // Snapshot real progress.
        int realHighest = SaveSystem.HighestUnlockedLevel;
        bool realStory = SaveSystem.StorySeen;

        SaveSystem.Migrate();
        // Stage progress on the shipped progression's FIRST level
        // (SceneRouter.FirstLevel), wherever the progression starts, so the
        // capture always exercises a tile that actually exists in the grid.
        SaveSystem.OnLevelCompleted(SceneRouter.FirstLevel);
        SaveSystem.RecordStars(SceneRouter.FirstLevel, 2);

        AsyncOperation op = SceneManager.LoadSceneAsync(SceneRouter.LevelSelect);
        while (!op.isDone)
            yield return null;

        // Let the celebration pop reach its peak (~0.3s in).
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            yield return null;
        }

        Camera cam = Camera.main;

        // Screen-space-overlay canvases don't render through a camera RT, so
        // the rig flips them to camera space (and puts them back). It also
        // assigns the target texture BEFORE the flip — a camera-space canvas
        // lays itself out against the camera's pixel rect, so flipping first
        // would size the UI to the Game view instead of the capture — and it
        // skips ScreenFader, whose full-screen black Image would otherwise
        // composite into the shot if the fade hadn't finished.
        StoreScreenshotRig.CaptureSession session =
            StoreScreenshotRig.Begin(cam, StoreScreenshotRig.Qa1080);
        try
        {
            yield return null;
            yield return null;
            session.Write(System.IO.Path.Combine(
                StoreScreenshotRig.OutputDirectory, "levelselect_coins.png"));
        }
        finally
        {
            session.Dispose();
        }

        // Restore real progress (best effort — completion flags stay, which
        // only matters for this QA capture run, not player builds).
        if (realHighest <= SceneRouter.FirstLevel && !realStory)
            SaveSystem.ResetProgress();

        Assert.Pass("level select captured");
    }
}
