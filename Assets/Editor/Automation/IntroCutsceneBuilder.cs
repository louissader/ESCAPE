using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Builds Assets/Scenes/Intro.unity for the first-launch story beats
    /// (IntroCutscene owns the runtime behavior — this pass only assembles
    /// the canvas it documents in its header) and adds the STORY replay
    /// button to the main menu. Idempotent: the IntroUI root is rebuilt by
    /// name on every run; camera/EventSystem/menu button are find-or-create.
    /// </summary>
    public static class IntroCutsceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Intro.unity";
        const string CanvasName = "IntroUI";

        // Must mirror the IntroCutscene.captions field initializer — the
        // component default and the built scene must never diverge.
        static readonly string[] Captions =
        {
            "Two friends chased a legend: a cave full of fallen stars.",
            "The mountain had other plans.",
            "The stars scattered into the deep... every last one.",
            "The only way out is down. Together.",
        };

        static readonly string[] PlateSheets =
        {
            "Assets/Art/Generated/Backgrounds/cutscene_entrance.png",
            "Assets/Art/Generated/Backgrounds/cutscene_collapse.png",
            "Assets/Art/Generated/Backgrounds/cutscene_stars.png",
            "Assets/Art/Generated/Backgrounds/cutscene_resolve.png",
        };

        [MenuItem("Escape/Build Intro Cutscene")]
        public static void Run()
        {
            BuildIntroScene();
            EnsureInBuildSettings();
            AddStoryButtonToMainMenu();
            AssetDatabase.SaveAssets();
            Debug.Log("[IntroCutsceneBuilder] Intro scene + STORY button built");
        }

        static void BuildIntroScene()
        {
            Scene scene = System.IO.File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The UI root is fully owned by this builder — rebuild wholesale.
            GameObject oldRoot = GameObject.Find(CanvasName);
            if (oldRoot != null)
                Object.DestroyImmediate(oldRoot);

            EnsureCamera();
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));

            GameObject root = new GameObject(CanvasName);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            // Bottom → top: backdrop (black letterbox bars), plate, caption
            // (under the fade so a finished line never floats over black),
            // fade overlay, skip (always visible and clickable, above all).
            Image backdrop = NewRect("Backdrop", root.transform).gameObject.AddComponent<Image>();
            Stretch((RectTransform)backdrop.transform);
            backdrop.color = Color.black;
            backdrop.raycastTarget = false;

            Image plate = NewRect("Plate", root.transform).gameObject.AddComponent<Image>();
            Stretch((RectTransform)plate.transform);
            plate.preserveAspect = true;   // IntroCutscene re-asserts in Awake
            plate.raycastTarget = false;

            RectTransform captionSafe = NewRect("CaptionSafeArea", root.transform);
            Stretch(captionSafe);
            captionSafe.gameObject.AddComponent<SafeAreaFitter>();
            TextMeshProUGUI caption = NewRect("Caption", captionSafe).gameObject
                .AddComponent<TextMeshProUGUI>();
            RectTransform captionRect = (RectTransform)caption.transform;
            captionRect.anchorMin = new Vector2(0.5f, 0f);
            captionRect.anchorMax = new Vector2(0.5f, 0f);
            captionRect.pivot = new Vector2(0.5f, 0f);
            captionRect.anchoredPosition = new Vector2(0, 90);
            captionRect.sizeDelta = new Vector2(1500, 200);
            caption.fontSize = 46;              // reads on a phone at 1080 ref
            caption.alignment = TextAlignmentOptions.Center;
            caption.raycastTarget = false;

            Image fadeImage = NewRect("FadeOverlay", root.transform).gameObject.AddComponent<Image>();
            Stretch((RectTransform)fadeImage.transform);
            fadeImage.color = Color.black;
            fadeImage.raycastTarget = false;
            CanvasGroup fader = fadeImage.gameObject.AddComponent<CanvasGroup>();
            fader.alpha = 1f;                   // open on black (IntroCutscene contract)
            fader.interactable = false;
            fader.blocksRaycasts = false;

            RectTransform skipSafe = NewRect("SkipSafeArea", root.transform);
            Stretch(skipSafe);
            skipSafe.gameObject.AddComponent<SafeAreaFitter>();
            Button skip = MakeButton(skipSafe, "Btn_Skip", "SKIP");

            IntroCutscene cutscene = root.AddComponent<IntroCutscene>();
            cutscene.plateImage = plate;
            cutscene.caption = caption;
            cutscene.fader = fader;
            cutscene.plates = PlateSheets.Select(LoadPlate).ToArray();
            cutscene.captions = (string[])Captions.Clone();
            cutscene.skipButton = skip;         // Awake hooks onClick — no persistent call needed
            if (cutscene.plates.Any(p => p == null))
                Debug.LogWarning("[IntroCutsceneBuilder] cutscene plate sprite(s) missing " +
                                 "— rerun after the art imports");

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[IntroCutsceneBuilder] saved {ScenePath}");
        }

        static void EnsureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camObj.tag = "MainCamera";
                cam = camObj.GetComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        /// <summary>Intro must ship in every build — LevelBuilder's scene-list
        /// rewrite also carries it, but a standalone run must not depend on that.</summary>
        static void EnsureInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int existing = scenes.FindIndex(s => s.path == ScenePath);
            if (existing >= 0)
            {
                if (!scenes[existing].enabled)
                {
                    scenes[existing] = new EditorBuildSettingsScene(ScenePath, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                }
                return;
            }
            // Menu Scene stays index 0 (first loaded scene); slot Intro into
            // the menu block like LevelBuilder.UpdateBuildSettings does.
            scenes.Insert(Mathf.Min(2, scenes.Count), new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[IntroCutsceneBuilder] Intro added to Build Settings");
        }

        /// <summary>
        /// STORY button under PLAY on the main menu: rewatches the intro via
        /// MainMenu.OnStoryButton (sets IntroCutscene.ReplayMode so no
        /// progress is written and the end returns to the menu).
        /// </summary>
        static void AddStoryButtonToMainMenu()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Menu Scene.unity", OpenSceneMode.Single);
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            MainMenu mainMenu = Object.FindObjectOfType<MainMenu>();
            if (canvas == null || mainMenu == null)
            {
                Debug.LogError("[IntroCutsceneBuilder] Menu Scene canvas/MainMenu missing — STORY button skipped");
                return;
            }

            // Between PLAY (0,-40) and QUIT (0,-170), matching their 160x30 style.
            Button story = FindOrCreateMenuButton((RectTransform)canvas.transform,
                "StoryButton", "STORY", new Vector2(0, -85));
            for (int i = story.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(story.onClick, i);
            UnityEventTools.AddPersistentListener(story.onClick, mainMenu.OnStoryButton);

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IntroCutsceneBuilder] STORY button wired in Menu Scene");
        }

        /// <summary>Small menu-style button (find-or-create by name).</summary>
        internal static Button FindOrCreateMenuButton(RectTransform canvas, string name,
                                                      string label, Vector2 anchoredPos)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null && existing.TryGetComponent(out Button existingButton))
            {
                ((RectTransform)existing.transform).anchoredPosition = anchoredPos;
                return existingButton;
            }

            Image img = NewRect(name, canvas).gameObject.AddComponent<Image>();
            RectTransform rect = (RectTransform)img.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(160, 30);
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            Button button = img.gameObject.AddComponent<Button>();

            TextMeshProUGUI text = NewRect("Label", rect).gameObject.AddComponent<TextMeshProUGUI>();
            Stretch((RectTransform)text.transform);
            text.text = label;
            text.fontSize = 16;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.1f, 0.1f, 0.15f, 1f);
            text.raycastTarget = false;
            return button;
        }

        static Button MakeButton(RectTransform parent, string name, string label)
        {
            Image img = NewRect(name, parent).gameObject.AddComponent<Image>();
            RectTransform rect = (RectTransform)img.transform;
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-48, -40);
            rect.sizeDelta = new Vector2(240, 96);
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = new Color(0.13f, 0.35f, 0.46f, 0.8f);
            Button button = img.gameObject.AddComponent<Button>();

            TextMeshProUGUI text = NewRect("Label", rect).gameObject.AddComponent<TextMeshProUGUI>();
            Stretch((RectTransform)text.transform);
            text.text = label;
            text.fontSize = 40;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return button;
        }

        /// <summary>LoadSprite pattern from LevelMenuBuilder, incl. the
        /// Single-import-mode fallback (Backgrounds import unsliced).</summary>
        static Sprite LoadPlate(string path)
        {
            string baseName = System.IO.Path.GetFileNameWithoutExtension(path);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite && sprite.name == baseName + "_plate_0")
                    return sprite;
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

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
    }
}
