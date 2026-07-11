using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Headless world-render captures of every level (no HUD — screen-space
    /// canvases don't render through camera RTs): one 1920x1080 QA shot per
    /// level framed on the spawn area, plus store-resolution candidates for
    /// the three showcase levels. Written to StoreAssets/.
    /// </summary>
    public static class ScreenshotCapture
    {
        [MenuItem("Escape/Capture Level Screenshots")]
        public static void Run()
        {
            Directory.CreateDirectory("StoreAssets");

            for (int level = 1; level <= SceneRouter.LevelCount; level++)
            {
                string path = $"Assets/Scenes/Level {level}.unity";
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                Camera cam = Camera.main;
                if (cam == null)
                {
                    Debug.LogError($"[Screenshot] no camera in {path}");
                    continue;
                }

                // Frame on the players' spawn area (vcam doesn't run in edit mode).
                GameObject p1 = GameObject.Find("Player1");
                Vector3 focus = p1 != null ? p1.transform.position : Vector3.zero;
                cam.transform.position = new Vector3(focus.x + 8f, focus.y + 4f, -10f);
                cam.orthographicSize = 8f;

                Capture(cam, 1920, 1080, $"StoreAssets/qa_level{level}.png");

                if (level == 1 || level == 5 || level == 8)
                {
                    Capture(cam, 2868, 1320, $"StoreAssets/iphone69_level{level}.png");
                    Capture(cam, 2752, 2064, $"StoreAssets/ipad13_level{level}.png");
                }
            }
            Debug.Log("[Screenshot] captures complete → StoreAssets/");
        }

        static void Capture(Camera cam, int width, int height, string file)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Debug.Log($"[Screenshot] {file} ({width}x{height})");
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
