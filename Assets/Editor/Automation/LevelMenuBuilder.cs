using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Replaces the hand-wired 3-level buttons in the Level Scene with the
    /// runtime LevelSelectGrid (8 tiles, locks, crystal pips). Keeps the
    /// Main Menu / lore navigation buttons.
    /// </summary>
    public static class LevelMenuBuilder
    {
        [MenuItem("Escape/Rebuild Level Select")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level Scene.unity", OpenSceneMode.Single);

            // Remove old per-level buttons/locks (Lvl X Button, Lock images).
            foreach (GameObject go in Object.FindObjectsOfType<GameObject>(true))
            {
                if (go == null) continue;
                string n = go.name;
                if (n.StartsWith("Lvl") || n.Contains("Lock") && go.GetComponent<Image>() != null)
                    Object.DestroyImmediate(go);
            }

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[LevelMenuBuilder] no canvas in Level Scene");
                return;
            }

            GameObject gridObj = GameObject.Find("LevelGrid");
            if (gridObj == null)
            {
                gridObj = new GameObject("LevelGrid", typeof(RectTransform));
                gridObj.transform.SetParent(canvas.transform, false);
                var rect = (RectTransform)gridObj.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(940, 520);
                rect.anchoredPosition = new Vector2(0, -30);
            }

            // Tidy the legacy scene furniture so it frames the grid instead
            // of clashing with it: title above, nav buttons in bottom corners.
            Reposition("Levels Text", new Vector2(0.5f, 1f), new Vector2(0, -110));
            Reposition("Main Menu Button", new Vector2(0f, 0f), new Vector2(240, 80));
            Reposition("second lore button", new Vector2(1f, 0f), new Vector2(-240, 80));

            var grid = gridObj.GetComponent<LevelSelectGrid>();
            if (grid == null)
                grid = gridObj.AddComponent<LevelSelectGrid>();
            grid.lockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Lock.png");
            grid.starFilled = LoadSprite("Assets/Art/Generated/Props/star.png", "star_twinkle_0");
            grid.starEmpty = LoadSprite("Assets/Art/Generated/Props/star_empty.png", "star_empty_empty_0");
            if (grid.starEmpty == null)   // single-frame sheets may import as Single mode
                grid.starEmpty = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/Props/star_empty.png");
            EditorUtility.SetDirty(grid);

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[LevelMenuBuilder] Level Scene rebuilt with LevelSelectGrid");
        }

        static void Reposition(string objectName, Vector2 anchor, Vector2 anchoredPos)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null || !(go.transform is RectTransform rect))
                return;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
        }

        static Sprite LoadSprite(string path, string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite && sprite.name == name)
                    return sprite;
            return null;
        }
    }
}
