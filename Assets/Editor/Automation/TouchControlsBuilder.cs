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

                // Playtest 9.3: the crouch button is gone — nothing in the
                // game uses down, and the two-button d-pad frees thumb room.
                ui.swapLeft = DpadButton(swap, "Btn_DpadLeft", "<", 0);
                ui.swapRight = DpadButton(swap, "Btn_DpadRight", ">", 1);

                // Playtest 9: "A"/"B" said nothing — the jump button says
                // JUMP and the power button shows what the power DOES.
                ui.swapJump = Button(swap, "Btn_A", "JUMP", JumpSize,
                    new Vector2(1, 0), new Vector2(-Margin, Margin), new Vector2(1, 0));
                ui.swapAction = Button(swap, "Btn_B", "", SmallSize,
                    new Vector2(1, 0), new Vector2(-Margin - JumpSize - DpadGap, Margin), new Vector2(1, 0));
                StylePowerButton(ui.swapAction, 0);
                // Swap sits directly above A: thumb-reachable without leaving jump.
                ui.swapSwap = Button(swap, "Btn_Swap", "SWITCH TO", SmallSize,
                    new Vector2(1, 0), new Vector2(-Margin - 20f, Margin + JumpSize + DpadGap), new Vector2(1, 0));
                StyleSwapButton(ui.swapSwap);

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
                // Two-button d-pad (crouch removed, playtest 9.3): the row
                // shrank by DpadSize + DpadGap = 196 units, so the 4:3
                // overlap arithmetic above now clears with huge margin.
                float dpadEnd = Margin + 2f * DpadSize + 1f * DpadGap; // 424
                float jumpX = dpadEnd + DpadGap;                       // 440
                float actionX = jumpX + (DpadSize - SmallSize) / 2f;   // 450, B centred over A

                ui.p1Left = DpadButton(coop, "P1_DpadLeft", "<", 0);
                ui.p1Right = DpadButton(coop, "P1_DpadRight", ">", 1);
                ui.p1Jump = Button(coop, "P1_A", "JUMP", DpadSize,
                    new Vector2(0, 0), new Vector2(jumpX, Margin), new Vector2(0, 0));
                ui.p1Action = Button(coop, "P1_B", "", SmallSize,
                    new Vector2(0, 0), new Vector2(actionX, Margin + DpadSize + DpadGap), new Vector2(0, 0));
                StylePowerButton(ui.p1Action, 1);

                ui.p2Right = MirroredDpadButton(coop, "P2_DpadRight", ">", 0);
                ui.p2Left = MirroredDpadButton(coop, "P2_DpadLeft", "<", 1);
                ui.p2Jump = Button(coop, "P2_A", "JUMP", DpadSize,
                    new Vector2(1, 0), new Vector2(-jumpX, Margin), new Vector2(1, 0));
                ui.p2Action = Button(coop, "P2_B", "", SmallSize,
                    new Vector2(1, 0), new Vector2(-actionX, Margin + DpadSize + DpadGap), new Vector2(1, 0));
                StylePowerButton(ui.p2Action, 2);

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

        /// <summary>
        /// Playtest 7: the old "&lt;-&gt;" glyph said nothing. The swap button now
        /// reads "SWITCH TO" over a portrait of the hero you'd switch into;
        /// SwapButtonFace keeps the portrait pointed at the INACTIVE hero at
        /// runtime (sprites baked here — runtime can't use AssetDatabase).
        /// </summary>
        static void StyleSwapButton(TouchButton swapButton)
        {
            RectTransform rect = (RectTransform)swapButton.transform;

            // Playtest 9: the 24pt bold label overflowed the 160px button.
            // Auto-size inside real margins instead of a fixed size, so the
            // text can never touch the rim again.
            var label = rect.Find("Label").GetComponent<TextMeshProUGUI>();
            label.text = "SWITCH TO";
            label.enableAutoSizing = true;
            label.fontSizeMax = 19f;   // playtest 9.3: must read smaller than the button
            label.fontSizeMin = 12f;
            label.alignment = TextAlignmentOptions.Top;
            label.enableWordWrapping = false;
            label.margin = new Vector4(2, 6, 2, 0);
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.offsetMin = new Vector2(8, 0);
            labelRect.offsetMax = new Vector2(-8, -8);

            RectTransform portraitRect = NewRect("Portrait", rect);
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0.5f, 0f);
            portraitRect.pivot = new Vector2(0.5f, 0f);
            portraitRect.anchoredPosition = new Vector2(0, 12);
            portraitRect.sizeDelta = new Vector2(66, 96); // hero frames are 16x24
            // Cartoon pass: a slight tilt + idle bob makes the portrait feel
            // like a sticker, not a form field. UIPulse animates only this
            // child — TouchButton owns the button root's scale.
            portraitRect.localRotation = Quaternion.Euler(0f, 0f, -6f);
            Image portrait = portraitRect.gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            UIPulse pulse = portraitRect.gameObject.AddComponent<UIPulse>();
            pulse.scaleAmplitude = 0.06f;
            pulse.speed = 2.2f;

            SwapButtonFace face = rect.gameObject.AddComponent<SwapButtonFace>();
            face.portrait = portrait;
            face.emberSprite = HeroIdleSprite("hero1_ember");
            face.wispSprite = HeroIdleSprite("hero2_wisp");
            // The rim glows in the target hero's color (set live by the face).
            face.rim = rect.Find("Rim").GetComponent<Image>();
        }

        /// <summary>
        /// Playtest 9: "B" said nothing — the power button face is a picture
        /// of what the power does (fireball / crystal platform), kept in sync
        /// with the owning hero by ActionButtonFace. playerId 0 follows the
        /// active hero (swap mode); 1/2 pins it (co-op).
        /// </summary>
        static void StylePowerButton(TouchButton button, int playerId)
        {
            RectTransform rect = (RectTransform)button.transform;

            RectTransform iconRect = NewRect("PowerIcon", rect);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(96, 96);
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            ActionButtonFace face = rect.gameObject.AddComponent<ActionButtonFace>();
            face.icon = icon;
            face.emberPowerSprite = FireballSprite();
            face.wispPowerSprite = CrystalSlabSprite();
            face.playerId = playerId;
        }

        static Sprite FireballSprite()
        {
            GameObject fire = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GrottoEscapeArtwork/Prefabs/Misc/Fire.prefab");
            SpriteRenderer sr = fire != null ? fire.GetComponentInChildren<SpriteRenderer>() : null;
            if (sr != null && sr.sprite != null)
                return sr.sprite;
            Debug.LogWarning("[TouchControlsBuilder] fireball sprite missing — Ember power icon stays empty");
            return null;
        }

        static Sprite CrystalSlabSprite()
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Generated/Props/crystal_slab.png"))
                if (o is Sprite s && s.name == "crystal_slab_crystal_slab_0")
                    return s;
            Debug.LogWarning("[TouchControlsBuilder] crystal_slab frame missing — Wisp power icon stays empty");
            return null;
        }

        static Sprite HeroIdleSprite(string sheet)
        {
            string path = $"Assets/Art/Generated/Characters/{sheet}.png";
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite s && s.name == sheet + "_idle_0")
                    return s;
            Debug.LogWarning($"[TouchControlsBuilder] {sheet}_idle_0 missing at {path} — swap portrait stays empty");
            return null;
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
            // Single glyphs ("<", ">") keep the big fixed size; word labels
            // ("JUMP") get a clearly smaller cap (playtest 9.3: autosized
            // words still visually overpowered the ghost buttons) and
            // auto-shrink inside margins so they can't touch the rim.
            if (label.Length > 1)
            {
                text.enableAutoSizing = true;
                text.fontSizeMax = size * 0.26f;
                text.fontSizeMin = 16f;
                text.margin = new Vector4(14, 10, 14, 10);
            }
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
