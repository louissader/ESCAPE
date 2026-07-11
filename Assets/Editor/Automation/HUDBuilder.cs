using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Builds Assets/Prefabs/GameplayUI.prefab: HUD (portraits + HP bars +
    /// pause), pause overlay, and settings panel, fully wired via persistent
    /// listeners. Injects it (plus LevelMusic) into level scenes.
    /// HUD layout contract: P1 top-left; pause in the top-right corner with
    /// P2 immediately left of it; active-character glow on the portrait.
    /// </summary>
    public static class HUDBuilder
    {
        const string PrefabPath = "Assets/Prefabs/GameplayUI.prefab";

        [MenuItem("Escape/Build GameplayUI Prefab")]
        public static void BuildGameplayUIPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            GameObject root = new GameObject("GameplayUI");
            try
            {
                Canvas canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 90; // under TouchControls (100)
                CanvasScaler scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                root.AddComponent<GraphicRaycaster>();

                RectTransform safeArea = NewRect("SafeArea", root.transform);
                Stretch(safeArea);
                safeArea.gameObject.AddComponent<SafeAreaFitter>();

                GameHUD hud = root.AddComponent<GameHUD>();
                PauseMenu pause = root.AddComponent<PauseMenu>();

                // ---------- HUD ----------
                RectTransform hudRect = NewRect("HUD", safeArea);
                Stretch(hudRect);

                // P1 cluster: portrait + bar, top-left.
                Image p1Glow = MakeGlow(hudRect, "P1_Glow", new Vector2(0, 1), new Vector2(40, -32));
                Image p1Portrait = MakePortrait(hudRect, "P1_Portrait", new Vector2(0, 1),
                    new Vector2(48, -40), "hero1_ember", new Color(0.56f, 0.17f, 0.17f));
                (Image p1BarFill, _) = MakeBar(hudRect, "P1_HP", new Vector2(0, 1), new Vector2(136, -56));

                // Pause button: extreme top-right corner.
                Button pauseButton = MakeTextButton(hudRect, "Btn_Pause", "II",
                    new Vector2(1, 1), new Vector2(-48, -40), new Vector2(96, 96));
                UnityEventTools.AddPersistentListener(pauseButton.onClick, pause.OnPauseButton);

                // P2 cluster: immediately left of pause.
                Image p2Glow = MakeGlow(hudRect, "P2_Glow", new Vector2(1, 1), new Vector2(-160, -32));
                Image p2Portrait = MakePortrait(hudRect, "P2_Portrait", new Vector2(1, 1),
                    new Vector2(-168, -40), "hero2_wisp", new Color(0.17f, 0.29f, 0.56f));
                (Image p2BarFill, _) = MakeBar(hudRect, "P2_HP", new Vector2(1, 1), new Vector2(-464, -56));

                // Portrait taps swap (inactive portrait only; GameHUD guards).
                Button p1Tap = p1Portrait.gameObject.AddComponent<Button>();
                UnityEventTools.AddPersistentListener(p1Tap.onClick, hud.OnPortrait1Tapped);
                Button p2Tap = p2Portrait.gameObject.AddComponent<Button>();
                UnityEventTools.AddPersistentListener(p2Tap.onClick, hud.OnPortrait2Tapped);

                // ---------- Pause overlay ----------
                RectTransform overlay = NewRect("PauseOverlay", safeArea);
                Stretch(overlay);
                Image dim = overlay.gameObject.AddComponent<Image>();
                dim.color = new Color(0f, 0f, 0f, 0.66f);

                TextMeshProUGUI title = MakeLabel(overlay, "Title", "PAUSED", 72,
                    new Vector2(0.5f, 0.5f), new Vector2(0, 280));

                string[] labels = { "RESUME", "RESTART LEVEL", "SETTINGS", "LEVEL SELECT" };
                System.Action<Button>[] wires =
                {
                    b => UnityEventTools.AddPersistentListener(b.onClick, pause.OnResumeButton),
                    b => UnityEventTools.AddPersistentListener(b.onClick, pause.OnRestartButton),
                    b => UnityEventTools.AddPersistentListener(b.onClick, pause.OnSettingsButton),
                    b => UnityEventTools.AddPersistentListener(b.onClick, pause.OnLevelSelectButton),
                };
                for (int i = 0; i < labels.Length; i++)
                {
                    Button b = MakeTextButton(overlay, "Btn_" + labels[i].Replace(" ", ""), labels[i],
                        new Vector2(0.5f, 0.5f), new Vector2(0, 120 - i * 120), new Vector2(560, 96));
                    wires[i](b);
                }

                // ---------- Settings panel ----------
                RectTransform settings = NewRect("SettingsPanel", safeArea);
                Stretch(settings);
                Image settingsDim = settings.gameObject.AddComponent<Image>();
                settingsDim.color = new Color(0.02f, 0.09f, 0.13f, 0.97f);
                SettingsPanel panel = settings.gameObject.AddComponent<SettingsPanel>();

                MakeLabel(settings, "Title", "SETTINGS", 72, new Vector2(0.5f, 0.5f), new Vector2(0, 340));

                MakeLabel(settings, "MusicLabel", "MUSIC", 40, new Vector2(0.5f, 0.5f), new Vector2(-420, 200));
                Slider music = MakeSlider(settings, "MusicSlider", new Vector2(120, 200));
                UnityEventTools.AddPersistentListener(music.onValueChanged, panel.OnMusicChanged);

                MakeLabel(settings, "SfxLabel", "SOUND", 40, new Vector2(0.5f, 0.5f), new Vector2(-420, 80));
                Slider sfx = MakeSlider(settings, "SfxSlider", new Vector2(120, 80));
                UnityEventTools.AddPersistentListener(sfx.onValueChanged, panel.OnSfxChanged);

                MakeLabel(settings, "ModeLabel", "CONTROLS", 40, new Vector2(0.5f, 0.5f), new Vector2(-420, -40));
                Toggle swap = MakeToggle(settings, "SwapToggle", "SOLO (SWAP)", new Vector2(40, -40));
                UnityEventTools.AddPersistentListener(swap.onValueChanged, panel.OnSwapModeSelected);
                Toggle coop = MakeToggle(settings, "CoopToggle", "2-PLAYER CO-OP", new Vector2(440, -40));
                UnityEventTools.AddPersistentListener(coop.onValueChanged, panel.OnCoopModeSelected);

                Button reset = MakeTextButton(settings, "Btn_Reset", "RESET PROGRESS",
                    new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(560, 88));
                UnityEventTools.AddPersistentListener(reset.onClick, panel.OnResetProgress);

                Button close = MakeTextButton(settings, "Btn_Close", "BACK",
                    new Vector2(0.5f, 0.5f), new Vector2(0, -320), new Vector2(360, 88));
                UnityEventTools.AddPersistentListener(close.onClick, panel.OnClose);

                // Reset confirm sub-panel.
                RectTransform confirm = NewRect("ResetConfirm", settings);
                Stretch(confirm);
                Image confirmDim = confirm.gameObject.AddComponent<Image>();
                confirmDim.color = new Color(0f, 0f, 0f, 0.85f);
                MakeLabel(confirm, "Msg", "ERASE ALL PROGRESS?", 56, new Vector2(0.5f, 0.5f), new Vector2(0, 100));
                Button yes = MakeTextButton(confirm, "Btn_Yes", "ERASE",
                    new Vector2(0.5f, 0.5f), new Vector2(-220, -60), new Vector2(320, 88));
                UnityEventTools.AddPersistentListener(yes.onClick, panel.OnResetConfirmed);
                Button no = MakeTextButton(confirm, "Btn_No", "KEEP",
                    new Vector2(0.5f, 0.5f), new Vector2(220, -60), new Vector2(320, 88));
                UnityEventTools.AddPersistentListener(no.onClick, panel.OnResetCancelled);

                // ---------- serialized wiring ----------
                SetPrivate(hud, "p1HpFill", p1BarFill);
                SetPrivate(hud, "p2HpFill", p2BarFill);
                SetPrivate(hud, "p1Portrait", p1Portrait);
                SetPrivate(hud, "p2Portrait", p2Portrait);
                SetPrivate(hud, "p1ActiveGlow", p1Glow);
                SetPrivate(hud, "p2ActiveGlow", p2Glow);
                SetPrivate(pause, "overlay", overlay.gameObject);
                SetPrivate(pause, "settingsPanel", settings.gameObject);
                SetPrivate(panel, "musicSlider", music);
                SetPrivate(panel, "sfxSlider", sfx);
                SetPrivate(panel, "swapToggle", swap);
                SetPrivate(panel, "coopToggle", coop);
                SetPrivate(panel, "resetConfirm", confirm.gameObject);

                // ---------- Level-end panels (win + fail) ----------
                LevelEndController endController = root.AddComponent<LevelEndController>();

                RectTransform winPanel = NewRect("WinPanel", safeArea);
                Stretch(winPanel);
                Image winDim = winPanel.gameObject.AddComponent<Image>();
                winDim.color = new Color(0.02f, 0.12f, 0.08f, 0.92f);
                TextMeshProUGUI winTitle = MakeLabel(winPanel, "Title", "LEVEL COMPLETE", 76,
                    new Vector2(0.5f, 0.5f), new Vector2(0, 260));
                TextMeshProUGUI winCrystals = MakeLabel(winPanel, "Crystals", "STAR COINS 0 / 3", 44,
                    new Vector2(0.5f, 0.5f), new Vector2(0, 150));
                Button nextBtn = MakeTextButton(winPanel, "Btn_Next", "NEXT LEVEL",
                    new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(560, 96));
                UnityEventTools.AddPersistentListener(nextBtn.onClick, endController.OnNextLevel);
                Button replayBtn = MakeTextButton(winPanel, "Btn_Replay", "REPLAY",
                    new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(560, 96));
                UnityEventTools.AddPersistentListener(replayBtn.onClick, endController.OnReplay);
                Button winSelectBtn = MakeTextButton(winPanel, "Btn_Select", "LEVEL SELECT",
                    new Vector2(0.5f, 0.5f), new Vector2(0, -210), new Vector2(560, 96));
                UnityEventTools.AddPersistentListener(winSelectBtn.onClick, endController.OnLevelSelect);

                RectTransform failPanel = NewRect("FailPanel", safeArea);
                Stretch(failPanel);
                Image failDim = failPanel.gameObject.AddComponent<Image>();
                failDim.color = new Color(0.14f, 0.02f, 0.02f, 0.92f);
                MakeLabel(failPanel, "Title", "YOU FELL", 76, new Vector2(0.5f, 0.5f), new Vector2(0, 200));
                Button retryBtn = MakeTextButton(failPanel, "Btn_Retry", "RETRY",
                    new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(560, 96));
                UnityEventTools.AddPersistentListener(retryBtn.onClick, endController.OnRetry);
                Button failSelectBtn = MakeTextButton(failPanel, "Btn_Select", "LEVEL SELECT",
                    new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(560, 96));
                UnityEventTools.AddPersistentListener(failSelectBtn.onClick, endController.OnLevelSelect);

                SetPrivate(endController, "winPanel", winPanel.gameObject);
                SetPrivate(endController, "failPanel", failPanel.gameObject);
                SetPrivate(endController, "winTitle", winTitle);
                SetPrivate(endController, "winCrystals", winCrystals);
                SetPrivate(endController, "nextButton", nextBtn.gameObject);

                overlay.gameObject.SetActive(false);
                settings.gameObject.SetActive(false);
                confirm.gameObject.SetActive(false);
                winPanel.gameObject.SetActive(false);
                failPanel.gameObject.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                Debug.Log($"[HUDBuilder] Saved {PrefabPath}: {ok}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Escape/Add GameplayUI To Level Scenes")]
        public static void AddGameplayUIToLevels()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                BuildGameplayUIPrefab();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }

            for (int level = 1; level <= SceneRouter.LevelCount; level++)
            {
                string path = $"Assets/Scenes/Level {level}.unity";
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool dirty = false;

                if (GameObject.Find("GameplayUI") == null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    instance.name = "GameplayUI";
                    dirty = true;
                }
                if (Object.FindObjectOfType<LevelMusic>() == null)
                {
                    new GameObject("LevelMusic", typeof(LevelMusic));
                    dirty = true;
                }

                if (dirty)
                    EditorSceneManager.SaveScene(scene);
                Debug.Log($"[HUDBuilder] {path} {(dirty ? "updated" : "already set up")}");
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

        static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        static Sprite HeroSprite(string sheetName)
        {
            string path = $"Assets/Art/Generated/Characters/{sheetName}.png";
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite && sprite.name.Contains("idle_0"))
                    return sprite;
            return null;
        }

        static Image MakePortrait(RectTransform parent, string name, Vector2 anchor, Vector2 pos,
                                  string heroSheet, Color fallback)
        {
            Image img = NewRect(name, parent).gameObject.AddComponent<Image>();
            Place((RectTransform)img.transform, anchor, pos, new Vector2(80, 112));
            Sprite hero = HeroSprite(heroSheet);
            if (hero != null)
            {
                img.sprite = hero;
                img.preserveAspect = true;
            }
            else
            {
                img.color = fallback;
            }
            return img;
        }

        static Image MakeGlow(RectTransform parent, string name, Vector2 anchor, Vector2 pos)
        {
            Image glow = NewRect(name, parent).gameObject.AddComponent<Image>();
            Place((RectTransform)glow.transform, anchor, pos, new Vector2(96, 128));
            glow.color = new Color(1f, 0.9f, 0.4f, 0.5f);
            glow.raycastTarget = false;
            return glow;
        }

        static (Image fill, Image back) MakeBar(RectTransform parent, string name, Vector2 anchor, Vector2 pos)
        {
            Image back = NewRect(name + "_Back", parent).gameObject.AddComponent<Image>();
            Place((RectTransform)back.transform, anchor, pos, new Vector2(280, 32));
            back.color = new Color(0f, 0f, 0f, 0.55f);
            back.raycastTarget = false;

            Image fill = NewRect(name + "_Fill", back.rectTransform).gameObject.AddComponent<Image>();
            RectTransform fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4, 4);
            fillRect.offsetMax = new Vector2(-4, -4);
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.raycastTarget = false;
            return (fill, back);
        }

        static Button MakeTextButton(RectTransform parent, string name, string label,
                                     Vector2 anchor, Vector2 pos, Vector2 size)
        {
            Image img = NewRect(name, parent).gameObject.AddComponent<Image>();
            Place((RectTransform)img.transform, anchor, pos, size);
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = new Color(0.13f, 0.35f, 0.46f, 0.95f);
            Button button = img.gameObject.AddComponent<Button>();

            TextMeshProUGUI text = MakeLabel((RectTransform)img.transform, "Label", label,
                Mathf.Min(44, size.y * 0.42f), new Vector2(0.5f, 0.5f), Vector2.zero);
            RectTransform textRect = (RectTransform)text.transform;
            Stretch(textRect);
            return button;
        }

        static TextMeshProUGUI MakeLabel(RectTransform parent, string name, string content,
                                         float size, Vector2 anchor, Vector2 pos)
        {
            TextMeshProUGUI text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            Place((RectTransform)text.transform, anchor, pos, new Vector2(720, size * 1.6f));
            text.text = content;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        static Slider MakeSlider(RectTransform parent, string name, Vector2 pos)
        {
            RectTransform rect = NewRect(name, parent);
            Place(rect, new Vector2(0.5f, 0.5f), pos, new Vector2(640, 48));
            Slider slider = rect.gameObject.AddComponent<Slider>();
            slider.minValue = 0.0001f;
            slider.maxValue = 1f;

            Image back = NewRect("Background", rect).gameObject.AddComponent<Image>();
            Stretch((RectTransform)back.transform);
            back.color = new Color(0f, 0f, 0f, 0.5f);

            RectTransform fillArea = NewRect("FillArea", rect);
            Stretch(fillArea);
            Image fill = NewRect("Fill", fillArea).gameObject.AddComponent<Image>();
            Stretch((RectTransform)fill.transform);
            fill.color = new Color(0.22f, 0.68f, 0.88f, 0.95f);
            slider.fillRect = (RectTransform)fill.transform;

            RectTransform handleArea = NewRect("HandleArea", rect);
            Stretch(handleArea);
            Image handle = NewRect("Handle", handleArea).gameObject.AddComponent<Image>();
            Place((RectTransform)handle.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 72));
            handle.color = Color.white;
            slider.handleRect = (RectTransform)handle.transform;
            slider.targetGraphic = handle;
            return slider;
        }

        static Toggle MakeToggle(RectTransform parent, string name, string label, Vector2 pos)
        {
            RectTransform rect = NewRect(name, parent);
            Place(rect, new Vector2(0.5f, 0.5f), pos, new Vector2(380, 72));
            Toggle toggle = rect.gameObject.AddComponent<Toggle>();

            Image box = NewRect("Box", rect).gameObject.AddComponent<Image>();
            Place((RectTransform)box.transform, new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(56, 56));
            box.color = new Color(0f, 0f, 0f, 0.55f);
            toggle.targetGraphic = box;

            Image check = NewRect("Check", (RectTransform)box.transform).gameObject.AddComponent<Image>();
            Stretch((RectTransform)check.transform);
            ((RectTransform)check.transform).offsetMin = new Vector2(10, 10);
            ((RectTransform)check.transform).offsetMax = new Vector2(-10, -10);
            check.color = new Color(0.22f, 0.88f, 0.55f, 1f);
            toggle.graphic = check;

            TextMeshProUGUI text = MakeLabel(rect, "Label", label, 32, new Vector2(0.5f, 0.5f), new Vector2(60, 0));
            text.alignment = TextAlignmentOptions.Left;
            return toggle;
        }

        static void SetPrivate(Object target, string field, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[HUDBuilder] Missing field {field} on {target.GetType().Name}");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
