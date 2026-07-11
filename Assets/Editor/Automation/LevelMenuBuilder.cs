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

            var grid = gridObj.GetComponent<LevelSelectGrid>();
            if (grid == null)
                grid = gridObj.AddComponent<LevelSelectGrid>();
            grid.lockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Lock.png");
            EditorUtility.SetDirty(grid);

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[LevelMenuBuilder] Level Scene rebuilt with LevelSelectGrid");
        }
    }
}
