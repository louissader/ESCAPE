using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Play-mode captures through the real URP 2D renderer (lights + vcam).
/// ScreenCapture.CaptureScreenshot no-ops in batchmode, so this renders the
/// live camera to a RenderTexture manually. Screen-space HUD isn't part of
/// a camera render — these verify WORLD rendering.
/// </summary>
public class ScreenshotTests
{
    [UnityTest]
    public IEnumerator CaptureEveryLevel()
    {
        System.IO.Directory.CreateDirectory("StoreAssets");

        for (int n = 1; n <= SceneRouter.LevelCount; n++)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(SceneRouter.LevelSceneName(n));
            while (!op.isDone)
                yield return null;

            float t = 0f;
            while (t < 1.0f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, $"L{n}: no main camera");

            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;

            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes($"StoreAssets/play_level{n}.png", tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);

            yield return null;
        }

        Assert.Pass("captures written");
    }
}
