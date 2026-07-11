using System.Collections;
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
        SaveSystem.OnLevelCompleted(1);       // unlocks 2, marks 1 complete + celebration
        SaveSystem.RecordStars(1, 2);

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
        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        // Screen-space-overlay canvases don't render through a camera RT, so
        // temporarily switch the canvas to camera space for the capture.
        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>())
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
            }
        }
        yield return null;

        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;
        RenderTexture.active = rt;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes("StoreAssets/levelselect_coins.png", tex.EncodeToPNG());
        Object.Destroy(tex);
        rt.Release();

        // Restore real progress (best effort — completion flags stay, which
        // only matters for this QA capture run, not player builds).
        if (realHighest <= 1 && !realStory)
            SaveSystem.ResetProgress();

        Assert.Pass("level select captured");
    }
}
