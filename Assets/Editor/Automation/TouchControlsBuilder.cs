using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Builds Assets/Prefabs/TouchControls.prefab (canvas + both layouts,
    /// placeholder button art — Phase 3 swaps in pixel-art sprites by object
    /// name) and injects it plus SwapModeController into every level scene.
    /// Idempotent: rebuilding replaces the prefab; injection skips scenes
    /// that already have the instance.
    /// </summary>
    public static class TouchControlsBuilder
    {
        const string PrefabPath = "Assets/Prefabs/TouchControls.prefab";

        // 1920x1080 reference-canvas sizing (master prompt Phase 2.2).
        const float Margin = 48f;
        const float DpadSize = 180f;
        const float DpadGap = 16f;
        const float JumpSize = 200f;
        const float SmallSize = 160f;

        [MenuItem("Escape/Build TouchControls Prefab")]
        public static void BuildTouchControlsPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            GameObject root = new GameObject("TouchControls");
            try
            {
                Canvas canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                CanvasScaler scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                root.AddComponent<GraphicRaycaster>();
                TouchControlsUI ui = root.AddComponent<TouchControlsUI>();

                RectTransform safeArea = NewRect("SafeArea", root.transform);
                Stretch(safeArea);
                safeArea.gameObject.AddComponent<SafeAreaFitter>();

                // ---- Swap layout ----
                RectTransform swap = NewRect("SwapLayout", safeArea);
                Stretch(swap);
                ui.swapLayout = swap.gameObject;

                ui.swapLeft = DpadButton(swap, "Btn_DpadLeft", "<", 0);
                ui.swapDown = DpadButton(swap, "Btn_DpadDown", "v", 1);
                ui.swapRight = DpadButton(swap, "Btn_DpadRight", ">", 2);

                ui.swapJump = Button(swap, "Btn_A", "A", JumpSize,
                    new Vector2(1, 0), new Vector2(-Margin, Margin), new Vector2(1, 0));
                ui.swapAction = Button(swap, "Btn_B", "B", SmallSize,
                    new Vector2(1, 0), new Vector2(-Margin - JumpSize - DpadGap, Margin), new Vector2(1, 0));
                // Swap sits directly above A: thumb-reachable without leaving jump.
                ui.swapSwap = Button(swap, "Btn_Swap", "<->", SmallSize,
                    new Vector2(1, 0), new Vector2(-Margin - 20f, Margin + JumpSize + DpadGap), new Vector2(1, 0));

                // ---- Co-op layout (mirrored edges, two thumbs per player) ----
                RectTransform coop = NewRect("CoopLayout", safeArea);
                Stretch(coop);
                ui.coopLayout = coop.gameObject;

                ui.p1Left = DpadButton(coop, "P1_DpadLeft", "<", 0);
                ui.p1Down = DpadButton(coop, "P1_DpadDown", "v", 1);
                ui.p1Right = DpadButton(coop, "P1_DpadRight", ">", 2);
                ui.p1Jump = Button(coop, "P1_A", "A", DpadSize,
                    new Vector2(0.5f, 0), new Vector2(-40f, Margin), new Vector2(1, 0));
                ui.p1Action = Button(coop, "P1_B", "B", SmallSize,
                    new Vector2(0.5f, 0), new Vector2(-50f, Margin + DpadSize + DpadGap), new Vector2(1, 0));

                ui.p2Right = MirroredDpadButton(coop, "P2_DpadRight", ">", 0);
                ui.p2Down = MirroredDpadButton(coop, "P2_DpadDown", "v", 1);
                ui.p2Left = MirroredDpadButton(coop, "P2_DpadLeft", "<", 2);
                ui.p2Jump = Button(coop, "P2_A", "A", DpadSize,
                    new Vector2(0.5f, 0), new Vector2(40f, Margin), new Vector2(0, 0));
                ui.p2Action = Button(coop, "P2_B", "B", SmallSize,
                    new Vector2(0.5f, 0), new Vector2(50f, Margin + DpadSize + DpadGap), new Vector2(0, 0));

                // Faint center divider so each player owns a visible half.
                RectTransform divider = NewRect("Divider", coop);
                divider.anchorMin = new Vector2(0.5f, 0f);
                divider.anchorMax = new Vector2(0.5f, 1f);
                divider.sizeDelta = new Vector2(4f, 0f);
                Image divImage = divider.gameObject.AddComponent<Image>();
                divImage.color = new Color(1f, 1f, 1f, 0.08f);
                divImage.raycastTarget = false;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                Debug.Log($"[TouchControlsBuilder] Saved {PrefabPath}: {success}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Escape/Add TouchControls To Level Scenes")]
        public static void AddTouchControlsToLevels()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                BuildTouchControlsPrefab();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }

            for (int level = 1; level <= SceneRouter.LevelCount; level++)
            {
                string path = $"Assets/Scenes/Level {level}.unity";
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                bool dirty = false;
                if (GameObject.Find("TouchControls") == null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    instance.name = "TouchControls";
                    dirty = true;
                }
                if (Object.FindObjectOfType<SwapModeController>() == null)
                {
                    new GameObject("SwapModeController", typeof(SwapModeController));
                    dirty = true;
                }
                if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    new GameObject("EventSystem",
                        typeof(UnityEngine.EventSystems.EventSystem),
                        typeof(UnityEngine.EventSystems.StandaloneInputModule));
                    dirty = true;
                }

                if (dirty)
                    EditorSceneManager.SaveScene(scene);
                Debug.Log($"[TouchControlsBuilder] {path} {(dirty ? "updated" : "already set up")}");
            }
        }

        // ---------- helpers ----------

        static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static TouchButton DpadButton(RectTransform parent, string name, string label, int slot)
        {
            float x = Margin + slot * (DpadSize + DpadGap);
            return Button(parent, name, label, DpadSize,
                new Vector2(0, 0), new Vector2(x, Margin), new Vector2(0, 0));
        }

        static TouchButton MirroredDpadButton(RectTransform parent, string name, string label, int slot)
        {
            float x = -(Margin + slot * (DpadSize + DpadGap));
            return Button(parent, name, label, DpadSize,
                new Vector2(1, 0), new Vector2(x, Margin), new Vector2(1, 0));
        }

        static TouchButton Button(RectTransform parent, string name, string label,
                                  float size, Vector2 anchor, Vector2 position, Vector2 pivot)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = position;

            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 0.35f);

            TouchButton button = rect.gameObject.AddComponent<TouchButton>();

            RectTransform textRect = NewRect("Label", rect);
            Stretch(textRect);
            TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = size * 0.45f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            text.raycastTarget = false;

            return button;
        }
    }
}
