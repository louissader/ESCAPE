using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Builds the HOW TO PLAY reference card (HowToPlayPanel) on both the
    /// main menu canvas and the pause canvas inside GameplayUI.prefab, plus
    /// the pause-menu button that opens it. The MAIN MENU no longer gets an
    /// open button — the title screen's submenu owns that entry
    /// (TitleScreenBuilder), so this pass only rebuilds the panel there and
    /// re-wires TitleMenu.howToPlayPanel at the fresh instance. MUST run
    /// after HUDBuilder and before TitleScreenBuilder in any pipeline —
    /// BuildGameplayUIPrefab rebuilds its prefab from scratch and drops the
    /// panel, and TitleScreenBuilder wires the panel this pass creates.
    /// Idempotent: panel/button objects are rebuilt by name.
    ///
    /// Copy is TOUCH-ONLY: this builder bakes static text while ControlMode
    /// is only resolved at runtime, so the card describes the on-screen
    /// controls and nothing else. Keyboard bindings are deliberately NOT
    /// mentioned — this text ships in the iOS/Android build, where telling
    /// the player about ARROW KEYS + SPACE is both wrong and, on an iPad,
    /// the kind of thing that draws a Guideline 4 design rejection. Desktop
    /// builds (BuildScript.BuildLocal) are dev-only. There is no keyboard
    /// swap key regardless — swap is the touch button or tapping a portrait
    /// (GameHUD.OnPortrait*Tapped).
    /// </summary>
    public static class HowToPlayBuilder
    {
        const string HudPrefabPath = "Assets/Prefabs/GameplayUI.prefab";

        static readonly Color EmberColor = new Color(0.878f, 0.455f, 0.220f); // #e07438
        static readonly Color WispColor = new Color(0.220f, 0.682f, 0.878f);  // #38aee0

        [MenuItem("Escape/Build How To Play Panels")]
        public static void Run()
        {
            BuildIntoGameplayUI();
            BuildIntoMainMenu();
            AssetDatabase.SaveAssets();
            Debug.Log("[HowToPlayBuilder] panels built (pause + main menu)");
        }

        static void BuildIntoGameplayUI()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath) == null)
            {
                Debug.LogError($"[HowToPlayBuilder] {HudPrefabPath} missing — run HUDBuilder first");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
            try
            {
                Transform safeArea = root.transform.Find("SafeArea");
                Transform overlay = safeArea != null ? safeArea.Find("PauseOverlay") : null;
                if (safeArea == null || overlay == null)
                {
                    Debug.LogError("[HowToPlayBuilder] GameplayUI structure unexpected — skipped");
                    return;
                }

                // Panel last under SafeArea so it draws above the overlay.
                HowToPlayPanel panel = BuildPanel((RectTransform)safeArea);

                // Fifth pause row, below LEVEL SELECT. HUDBuilder stacks at
                // 216 - i*144 with 560x136 rows, so row 5 is y -360 and the
                // size must match or this row alone stays under 44 pt.
                Transform oldBtn = overlay.Find("Btn_HowToPlay");
                if (oldBtn != null)
                    Object.DestroyImmediate(oldBtn.gameObject);
                Button open = MakeTextButton((RectTransform)overlay, "Btn_HowToPlay", "HOW TO PLAY",
                    new Vector2(0, -360), new Vector2(560, 136));
                UnityEventTools.AddPersistentListener(open.onClick, panel.Open);

                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
                Debug.Log($"[HowToPlayBuilder] panel added to {HudPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void BuildIntoMainMenu()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Menu Scene.unity", OpenSceneMode.Single);
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[HowToPlayBuilder] Menu Scene has no canvas — skipped");
                return;
            }

            HowToPlayPanel panel = BuildPanel((RectTransform)canvas.transform);

            // The title screen's submenu owns the HOW TO PLAY entry now —
            // clear the legacy standalone menu button if it still exists.
            Transform oldButton = canvas.transform.Find("HowToPlayButton");
            if (oldButton != null)
                Object.DestroyImmediate(oldButton.gameObject);

            // BuildPanel destroyed any previous panel instance, which nulls
            // TitleMenu's reference — re-wire so a standalone rerun of this
            // pass never fights TitleScreenBuilder.
            TitleMenu titleMenu = Object.FindObjectOfType<TitleMenu>();
            if (titleMenu != null)
            {
                titleMenu.howToPlayPanel = panel;
                panel.transform.SetAsLastSibling();   // draws above the submenu
                EditorUtility.SetDirty(titleMenu);
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HowToPlayBuilder] panel rebuilt in Menu Scene (open button lives in the title submenu)");
        }

        /// <summary>
        /// One reference card, rebuilt by name under the given canvas rect.
        /// Starts inactive: HowToPlayPanel.Open is the entry point (its
        /// wiring is lazy, and fades run on unscaled time for the pause menu).
        /// Shared: TitleScreenBuilder (missing-panel fallback) and
        /// IntroCutsceneBuilder (post-cutscene explainer) reuse this.
        /// </summary>
        internal static HowToPlayPanel BuildPanel(RectTransform parent)
        {
            Transform old = parent.Find("HowToPlayPanel");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            RectTransform panelRect = NewRect("HowToPlayPanel", parent);
            Stretch(panelRect);
            Image dim = panelRect.gameObject.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.09f, 0.13f, 0.97f);   // matches SettingsPanel
            CanvasGroup group = panelRect.gameObject.AddComponent<CanvasGroup>();
            HowToPlayPanel panel = panelRect.gameObject.AddComponent<HowToPlayPanel>();
            SetPrivate(panel, "root", panelRect.gameObject);
            SetPrivate(panel, "canvasGroup", group);

            MakeLabel(panelRect, "Title", "HOW TO PLAY", 72, new Vector2(0, 420), 1600);

            HeroRow(panelRect, "Basics", "CONTROLS", new Color(0.8f, 0.85f, 0.9f), 300,
                "on-screen < > move   -   hold A to jump higher, tap again in mid-air   -   the top-right button pauses");
            HeroRow(panelRect, "Ember", "EMBER", EmberColor, 210,
                "tap B to shoot a fireball (needs a red orb)");
            HeroRow(panelRect, "Wisp", "WISP", WispColor, 120,
                "tap B to become a platform (needs a blue orb)");
            HeroRow(panelRect, "Swap", "ONE PLAYER", new Color(1f, 0.9f, 0.4f), 30,
                "tap SWAP or the other hero's portrait to switch - the waiting hero is safe");
            MakeLabel(panelRect, "Goal", "BOTH heroes must stand in their matching doors to escape!",
                36, new Vector2(0, -70), 1600);

            // Icon legend: star / heart / checkpoint torch.
            Legend(panelRect, "LegendStar",
                LoadSpriteWithFallback("Assets/Art/Generated/Props/star.png", "star_twinkle_0"),
                "grab the star", -500);
            Legend(panelRect, "LegendHeart",
                LevelBuilder.FindGrottoSprite("heart"),
                "heals a hero", 0);
            Legend(panelRect, "LegendTorch",
                LoadSpriteWithFallback("Assets/Art/Generated/Props/checkpoint_torch.png", "checkpoint_torch_lit_0"),
                "respawn point", 500);

            // 88 -> 136 tall (33.3 -> 47.2 pt on an SE3). Raised to y -370
            // because a 136-tall button still centred on -420 would reach
            // -488, past the bottom of the shortest safe canvas (-472); the
            // legend row above ends at -225, so there is room to move up.
            Button close = MakeTextButton(panelRect, "Btn_Close", "GOT IT",
                new Vector2(0, -370), new Vector2(360, 136));
            UnityEventTools.AddPersistentListener(close.onClick, panel.Close);

            panelRect.gameObject.SetActive(false);
            return panel;
        }

        static void HeroRow(RectTransform parent, string name, string label, Color color,
                            float y, string description)
        {
            TextMeshProUGUI title = MakeLabel(parent, name + "Name", label, 42,
                new Vector2(-660, y), 320);
            title.color = color;
            title.alignment = TextAlignmentOptions.Left;
            TextMeshProUGUI desc = MakeLabel(parent, name + "Controls", description, 36,
                new Vector2(180, y), 1280);
            desc.alignment = TextAlignmentOptions.Left;
        }

        static void Legend(RectTransform parent, string name, Sprite icon, string label, float x)
        {
            RectTransform row = NewRect(name, parent);
            Place(row, new Vector2(x, -180), new Vector2(420, 90));

            Image img = NewRect("Icon", row).gameObject.AddComponent<Image>();
            Place((RectTransform)img.transform, new Vector2(-160, 0), new Vector2(72, 72));
            if (icon != null)
            {
                img.sprite = icon;
                img.preserveAspect = true;
            }
            img.raycastTarget = false;

            TextMeshProUGUI text = NewRect("Label", row).gameObject.AddComponent<TextMeshProUGUI>();
            Place((RectTransform)text.transform, new Vector2(40, 0), new Vector2(300, 90));
            text.text = label;
            text.fontSize = 36;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
        }

        // ---------- helpers (HUDBuilder idioms) ----------

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

        static void Place(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        static TextMeshProUGUI MakeLabel(RectTransform parent, string name, string content,
                                         float size, Vector2 pos, float width)
        {
            TextMeshProUGUI text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            Place((RectTransform)text.transform, pos, new Vector2(width, size * 1.8f));
            text.text = content;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        static Button MakeTextButton(RectTransform parent, string name, string label,
                                     Vector2 pos, Vector2 size)
        {
            Image img = NewRect(name, parent).gameObject.AddComponent<Image>();
            Place((RectTransform)img.transform, pos, size);
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = new Color(0.13f, 0.35f, 0.46f, 0.95f);
            Button button = img.gameObject.AddComponent<Button>();
            // Touch UI: no focus concept, don't latch the "selected" tint.
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            TextMeshProUGUI text = MakeLabel((RectTransform)img.transform, "Label", label,
                Mathf.Min(52, size.y * 0.42f), Vector2.zero, size.x);
            Stretch((RectTransform)text.transform);
            return button;
        }

        static Sprite LoadSpriteWithFallback(string path, string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite && sprite.name == name)
                    return sprite;
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void SetPrivate(Object target, string field, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[HowToPlayBuilder] Missing field {field} on {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
