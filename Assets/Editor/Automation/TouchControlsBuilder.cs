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

                // A/B anchor to the SAME screen edge as their d-pad, never to
                // centre: centre-anchored buttons drift toward the d-pad as
                // the screen narrows, and below aspect 1.3611 (every 4:3
                // iPad; canvas 1440*sqrt(4/3) = 1663 units wide) the old
                // centre-anchored P1_A (spanning 831.5-220 .. 831.5-40 =
                // 611.5..791.5) overlapped P1_DpadRight (440..620) by ~8.5
                // units in the same y-range. Edge-anchored, the P1<->P2 gap
                // grows with width instead of shrinking:
                //   d-pad ends at Margin + 3*DpadSize + 2*DpadGap = 620
                //   P1_A spans 636..816, so its right edge sits 13.5 units
                //   left of the divider edge at 4:3 (831.5 - 2 = 829.5) and
                //   241 units left at 19.5:9 (canvas 2118, divider edge
                //   1057). P2 mirrors exactly (left edge 847 vs divider edge
                //   833.5 at 4:3). The layout only re-collides below aspect
                //   (1636/1440)^2 = 1.29, narrower than any landscape device.
                // NOTE: those numbers assume Margin 48 / DpadSize 180 /
                // DpadGap 16. If the constants are ever raised (e.g. the
                // audit's Margin 96 / gap 32 spacing proposal) the row grows
                // to 912 units and no longer fits half a 4:3 canvas -
                // re-verify this arithmetic before changing them.
                float dpadEnd = Margin + 3f * DpadSize + 2f * DpadGap; // 620
                float jumpX = dpadEnd + DpadGap;                       // 636
                float actionX = jumpX + (DpadSize - SmallSize) / 2f;   // 646, B centred over A

                ui.p1Left = DpadButton(coop, "P1_DpadLeft", "<", 0);
                ui.p1Down = DpadButton(coop, "P1_DpadDown", "v", 1);
                ui.p1Right = DpadButton(coop, "P1_DpadRight", ">", 2);
                ui.p1Jump = Button(coop, "P1_A", "A", DpadSize,
                    new Vector2(0, 0), new Vector2(jumpX, Margin), new Vector2(0, 0));
                ui.p1Action = Button(coop, "P1_B", "B", SmallSize,
                    new Vector2(0, 0), new Vector2(actionX, Margin + DpadSize + DpadGap), new Vector2(0, 0));

                ui.p2Right = MirroredDpadButton(coop, "P2_DpadRight", ">", 0);
                ui.p2Down = MirroredDpadButton(coop, "P2_DpadDown", "v", 1);
                ui.p2Left = MirroredDpadButton(coop, "P2_DpadLeft", "<", 2);
                ui.p2Jump = Button(coop, "P2_A", "A", DpadSize,
                    new Vector2(1, 0), new Vector2(-jumpX, Margin), new Vector2(1, 0));
                ui.p2Action = Button(coop, "P2_B", "B", SmallSize,
                    new Vector2(1, 0), new Vector2(-actionX, Margin + DpadSize + DpadGap), new Vector2(1, 0));

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
            // Louis's playtest call (2026-09-07): the grey slabs read as
            // blocking the playfield. GOTCHA: this project renders in LINEAR
            // color space, so a low-alpha WHITE fill still displays as ~50%
            // grey over dark terrain (0.22 linear -> ~0.5 sRGB) - lowering
            // the alpha alone cannot make white look transparent here.
            // Ghost-button style instead: near-black glassy fill (dims lava
            // just enough for the label) + a light rim that defines the
            // button over dark caves. TouchButton's pressed state still
            // flashes bright for touch confirmation.
            image.color = new Color(0.03f, 0.04f, 0.08f, 0.38f);

            // The rim is a border-only child image (sliced, fillCenter off).
            // NOT UnityEngine.UI.Outline: that component draws FOUR offset
            // copies of the WHOLE sprite, which stacked into a solid light
            // slab behind the translucent fill - the very "blocking the
            // screen" look all three fill tweaks failed to fix.
            RectTransform rimRect = NewRect("Rim", rect);
            Stretch(rimRect);
            Image rim = rimRect.gameObject.AddComponent<Image>();
            rim.sprite = image.sprite;
            rim.type = Image.Type.Sliced;
            rim.fillCenter = false;
            // Rim boost (Louis, 2026-09-08): near-white, full-strength rim,
            // and pixelsPerUnitMultiplier 0.5 doubles the 9-slice border
            // thickness - the transparent centre stays, the edge pops.
            rim.color = new Color(0.96f, 0.98f, 1f, 0.95f);
            rim.pixelsPerUnitMultiplier = 0.5f;
            rim.raycastTarget = false;

            TouchButton button = rect.gameObject.AddComponent<TouchButton>();

            RectTransform textRect = NewRect("Label", rect);
            Stretch(textRect);
            TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = size * 0.52f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            // Light label + dark TMP outline: with the fill at 0.22 the
            // old dark label vanished over cave terrain. The outline keeps
            // it legible over lava and pale glow alike.
            text.color = new Color(0.95f, 0.97f, 1f, 0.95f);
            text.outlineWidth = 0.3f;
            text.outlineColor = new Color32(10, 10, 26, 240);
            text.raycastTarget = false;

            return button;
        }
    }
}
