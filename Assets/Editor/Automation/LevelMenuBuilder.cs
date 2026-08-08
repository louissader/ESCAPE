using TMPro;
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
            // of clashing with it: title above, nav buttons on a SafeArea
            // panel forced LAST in sibling order. LevelGrid's tiles are
            // raycast targets, so anything earlier in the hierarchy is
            // unreachable underneath them — the old dead-centre Main Menu
            // button was exactly that trap, and iOS has no Escape key to
            // fall back on, which made level select a navigation dead end.
            Reposition("Levels Text", new Vector2(0.5f, 1f), new Vector2(0, -110));

            RectTransform safeArea = EnsureSafeAreaPanel(canvas);
            StyleBackButton(safeArea);
            StyleContinueButton(safeArea);

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

        /// <summary>Full-stretch panel with a SafeAreaFitter that the nav
        /// buttons live under: keeps them out of the notch / home-indicator
        /// bands and, forced last in sibling order, renders (and raycasts)
        /// them ABOVE the LevelGrid tiles.</summary>
        static RectTransform EnsureSafeAreaPanel(Canvas canvas)
        {
            GameObject go = FindSceneObject("SafeArea");
            if (go == null)
            {
                go = new GameObject("SafeArea", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
            }
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            if (go.GetComponent<SafeAreaFitter>() == null)
                go.AddComponent<SafeAreaFitter>();
            rect.SetAsLastSibling();
            return rect;
        }

        /// <summary>Main Menu Button becomes a top-left back chevron:
        /// 160x160 units (>= the 136-unit floor that clears 44 pt on an
        /// iPhone SE3), styled like a grid tile. U+2039 is the only
        /// left-arrow-ish glyph in the LiberationSans SDF atlas — U+2190
        /// and U+25C0 would render as missing-glyph boxes.</summary>
        static void StyleBackButton(RectTransform safeArea)
        {
            GameObject go = FindSceneObject("Main Menu Button");
            if (go == null)
            {
                Debug.LogWarning("[LevelMenuBuilder] 'Main Menu Button' not found");
                return;
            }
            go.SetActive(true); // the shipped scene has it saved DISABLED
            var rect = (RectTransform)go.transform;
            rect.SetParent(safeArea, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(160, 160);
            rect.anchoredPosition = new Vector2(120, -120);

            Image image = go.GetComponent<Image>();
            if (image != null)
                image.color = new Color(0.13f, 0.35f, 0.46f, 0.95f); // matches unlocked grid tiles

            var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = "\u2039"; // single left angle quote, reads as a back chevron
                label.fontSize = 110;
                label.enableAutoSizing = false;
                label.color = Color.white;
                label.alignment = TextAlignmentOptions.Center;
            }
            EditorUtility.SetDirty(go);
        }

        /// <summary>"Continue" (the second lore button) moves to the
        /// bottom-right of the safe area at a 136-unit-tall tap target,
        /// scale reset from the legacy 2x so the hit box matches the art.
        /// What it DOES is fixed in LevelMenu.NextContinueButton: next
        /// unbeaten level, lore ending only once level 8 is complete.</summary>
        static void StyleContinueButton(RectTransform safeArea)
        {
            GameObject go = FindSceneObject("second lore button");
            if (go == null)
            {
                Debug.LogWarning("[LevelMenuBuilder] 'second lore button' not found");
                return;
            }
            var rect = (RectTransform)go.transform;
            rect.SetParent(safeArea, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(360, 136);
            rect.anchoredPosition = new Vector2(-220, 108);

            Image image = go.GetComponent<Image>();
            if (image != null)
                image.color = new Color(0.13f, 0.35f, 0.46f, 0.95f);

            var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = "CONTINUE";
                label.fontSize = 44;
                label.enableAutoSizing = false;
                label.color = Color.white;
                label.alignment = TextAlignmentOptions.Center;
            }
            EditorUtility.SetDirty(go);
        }

        /// <summary>GameObject.Find, but including inactive objects — the
        /// shipped scene has 'Main Menu Button' saved disabled, which is
        /// how the old Reposition() call silently missed it.</summary>
        static GameObject FindSceneObject(string name)
        {
            foreach (GameObject go in Object.FindObjectsOfType<GameObject>(true))
                if (go != null && go.name == name)
                    return go;
            return null;
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
