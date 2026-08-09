using System.Linq;
using UnityEditor;
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
    /// the canvas it documents in its header), including the hidden
    /// post-cutscene controls explainer (HowToPlayBuilder's reference card
    /// + a LET'S GO button) that IntroCutscene opens before Level 1 on
    /// first run. Playtest 6: the plate aspect-FILLs via AspectRatioFitter
    /// (960x540 native plates), and per-beat overlay roots under the plate
    /// carry star twinkles / dust motes wired to IntroCutscene. The menu-side STORY entry now lives in the title screen's
    /// submenu — TitleScreenBuilder owns the Menu Scene, this builder never
    /// touches it. Idempotent: the IntroUI root is rebuilt by name on every
    /// run; camera/EventSystem are find-or-create.
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

        // Plates are native 960x540 (16:9), drawn in a 240x135 composition
        // space — overlay positions below use those composition coords.
        const float PlateAspect = 960f / 540f;
        const float CompW = 240f, CompH = 135f;
        const string StarSheet = "Assets/Art/Generated/Props/star.png";
        // Smooth Bilinear-imported mote (NOT the 8px pixel-art FX dust_puff,
        // which would read as blocks scaled over the anti-aliased plates).
        const string DustSheet = "Assets/Art/Generated/Backgrounds/cutscene_dust.png";

        [MenuItem("Escape/Build Intro Cutscene")]
        public static void Run()
        {
            BuildIntroScene();
            EnsureInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[IntroCutsceneBuilder] Intro scene built");
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
            // Aspect-FILL like the title backdrop: the 960x540 plates keep
            // their 16:9 ratio and crop at other device ratios instead of
            // letterboxing (preserveAspect stays a no-op — rect ratio ==
            // sprite ratio). Backdrop black only shows during crossfades.
            AspectRatioFitter plateFitter = plate.gameObject.AddComponent<AspectRatioFitter>();
            plateFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            plateFitter.aspectRatio = PlateAspect;

            RectTransform[] overlayRoots = BuildBeatOverlays(
                (RectTransform)plate.transform, out RectTransform[] motes);

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

            // First-run controls explainer, last under the root so it draws
            // above everything (incl. skip). Same card HowToPlayBuilder
            // generates — but here closing must lead ONWARD, so the GOT IT
            // close button becomes a big LET'S GO that starts Level 1.
            HowToPlayPanel explainer = HowToPlayBuilder.BuildPanel((RectTransform)root.transform);
            Transform gotIt = explainer.transform.Find("Btn_Close");
            if (gotIt != null)
                Object.DestroyImmediate(gotIt.gameObject);
            Button letsGo = MakeLetsGoButton((RectTransform)explainer.transform);

            IntroCutscene cutscene = root.AddComponent<IntroCutscene>();
            cutscene.plateImage = plate;
            cutscene.caption = caption;
            cutscene.fader = fader;
            cutscene.plates = PlateSheets.Select(LoadPlate).ToArray();
            cutscene.captions = (string[])Captions.Clone();
            cutscene.skipButton = skip;         // Awake hooks onClick — no persistent call needed
            cutscene.postCutscenePanel = explainer;
            cutscene.letsGoButton = letsGo;     // Awake hooks onClick, like skip
            cutscene.beatOverlayRoots = overlayRoots;
            cutscene.dustMotes = motes;
            if (cutscene.plates.Any(p => p == null))
                Debug.LogWarning("[IntroCutsceneBuilder] cutscene plate sprite(s) missing " +
                                 "— rerun after the art imports");

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[IntroCutsceneBuilder] saved {ScenePath}");
        }

        // ---------- per-beat overlays (Playtest 6) ----------

        /// <summary>
        /// One overlay root per beat, children of the plate so they ride the
        /// Ken-Burns drift (IntroCutscene shows exactly one root per beat):
        /// twinkling star glints inside the cave mouth (entrance), drifting
        /// dust motes on the rubble (collapse, drifted by IntroCutscene via
        /// the returned `motes`), twinkles riding the painted swarm (stars).
        /// The resolve beat's root stays empty — pure darkness is the shot.
        /// Positions are backgrounds.py composition coords (240x135).
        /// </summary>
        static RectTransform[] BuildBeatOverlays(RectTransform plate, out RectTransform[] motes)
        {
            Sprite[] twinkle = TitleScreenBuilder.LoadFrames(StarSheet, "star_twinkle_", 4);
            // Backgrounds import Single, so this falls back to the whole asset.
            Sprite dust = TitleScreenBuilder.LoadSprite(DustSheet, "cutscene_dust_dust_0");
            if (twinkle.Length == 0)
                Debug.LogWarning("[IntroCutsceneBuilder] star_twinkle frames missing — " +
                                 "star overlays skipped; rerun after the art imports");
            if (dust == null)
                Debug.LogWarning("[IntroCutsceneBuilder] cutscene_dust sprite missing — " +
                                 "dust overlay skipped; rerun after the art imports");

            var roots = new RectTransform[PlateSheets.Length];
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i] = NewRect($"BeatOverlay_{i}", plate);
                Stretch(roots[i]);
                roots[i].gameObject.SetActive(false);   // IntroCutscene activates per beat
            }

            // Beat 0 — entrance: gold glints deep in the cave-mouth dark.
            AddTwinkle(roots[0], 0, 117f, 103f, 46f, twinkle, 5f, 3.1f);
            AddTwinkle(roots[0], 1, 128f, 108f, 38f, twinkle, 4f, 2.4f);
            AddTwinkle(roots[0], 2, 133f, 114f, 30f, twinkle, 6f, 3.6f);
            AddTwinkle(roots[0], 3, 110f, 111f, 30f, twinkle, 4.5f, 2.8f);
            AddTwinkle(roots[0], 4, 122f, 117f, 34f, twinkle, 5.5f, 2.0f);

            // Beat 1 — collapse: dust rolling off the rubble pile + the last
            // sliver of light. IntroCutscene drifts these (slow Lissajous).
            var dustList = new System.Collections.Generic.List<RectTransform>(6);
            dustList.Add(AddDust(roots[1], 0, 86f, 119f, 110f, dust));
            dustList.Add(AddDust(roots[1], 1, 154f, 116f, 120f, dust));
            dustList.Add(AddDust(roots[1], 2, 118f, 124f, 130f, dust));
            dustList.Add(AddDust(roots[1], 3, 99f, 97f, 80f, dust));
            dustList.Add(AddDust(roots[1], 4, 141f, 95f, 85f, dust));
            dustList.Add(AddDust(roots[1], 5, 120f, 86f, 70f, dust));
            motes = dustList.ToArray();

            // Beat 2 — star scatter: twinkles riding the painted big stars
            // (star centers = backgrounds.py cell origins + 8).
            AddTwinkle(roots[2], 0, 56f, 28f, 96f, twinkle, 5f, 2.2f);
            AddTwinkle(roots[2], 1, 104f, 46f, 88f, twinkle, 6f, 3.0f);
            AddTwinkle(roots[2], 2, 150f, 32f, 96f, twinkle, 4.5f, 2.6f);
            AddTwinkle(roots[2], 3, 84f, 76f, 84f, twinkle, 5.5f, 3.4f);
            AddTwinkle(roots[2], 4, 130f, 82f, 88f, twinkle, 4f, 2.0f);
            AddTwinkle(roots[2], 5, 174f, 64f, 80f, twinkle, 6.5f, 2.9f);

            Debug.Log("[IntroCutsceneBuilder] beat overlays built: " +
                      "5 entrance twinkles, 6 collapse dust motes, 6 star-swarm twinkles");
            return roots;
        }

        /// <summary>Child pinned at plate-composition coords via FRACTIONAL
        /// anchors, so it stays glued to the artwork at any device ratio
        /// (the plate rect always shares the art's 16:9 ratio).</summary>
        static RectTransform PlateChild(string name, RectTransform parent,
                                        float cx, float cy, float size)
        {
            RectTransform rect = NewRect(name, parent);
            Vector2 anchor = new Vector2(cx / CompW, 1f - cy / CompH);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);
            return rect;
        }

        /// <summary>Twinkling star glint: star_twinkle frame cycle + gentle
        /// scale pulse via UIPulse (the title screen's TitleStar idiom),
        /// desynced per star through fps/speed.</summary>
        static void AddTwinkle(RectTransform root, int i, float cx, float cy, float size,
                               Sprite[] twinkle, float fps, float speed)
        {
            Image img = PlateChild($"Star_{i}", root, cx, cy, size)
                .gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            if (twinkle.Length == 0)
            {
                img.gameObject.SetActive(false);
                return;
            }
            img.sprite = twinkle[0];
            UIPulse pulse = img.gameObject.AddComponent<UIPulse>();
            pulse.glowFrames = twinkle;
            pulse.fps = fps;
            pulse.scaleAmplitude = 0.12f;
            pulse.speed = speed;
        }

        /// <summary>Soft warm dust mote (tinted cutscene_dust, low alpha) —
        /// the drift itself lives in IntroCutscene.UpdateDustMotes.</summary>
        static RectTransform AddDust(RectTransform root, int i, float cx, float cy,
                                     float size, Sprite dust)
        {
            Image img = PlateChild($"Dust_{i}", root, cx, cy, size)
                .gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = new Color(0.62f, 0.53f, 0.44f, 0.38f);   // rolling dust
            if (dust != null)
                img.sprite = dust;
            else
                img.gameObject.SetActive(false);   // no white-square fallback
            return (RectTransform)img.transform;
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
        /// Big pulsing LET'S GO! at the explainer card's foot (the GOT IT
        /// slot, 0/-420): btn_play glow frames + UIPulse, same styling as
        /// the title screen's PLAY. No persistent onClick — IntroCutscene
        /// hooks letsGoButton in Awake, like skip.
        /// </summary>
        static Button MakeLetsGoButton(RectTransform parent)
        {
            Sprite[] glow = TitleScreenBuilder.LoadFrames(
                TitleScreenBuilder.BtnPlaySheet, "btn_play_glow_", 4);

            Image img = NewRect("Btn_LetsGo", parent).gameObject.AddComponent<Image>();
            RectTransform rect = (RectTransform)img.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, -420);
            rect.sizeDelta = new Vector2(512, 160);
            if (glow.Length > 0)
            {
                img.sprite = glow[0];
            }
            else
            {
                img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                img.type = Image.Type.Sliced;
                img.color = new Color(0.13f, 0.35f, 0.46f, 0.95f);
                Debug.LogWarning("[IntroCutsceneBuilder] btn_play sprites missing — " +
                                 "rerun after the art imports");
            }
            Button button = img.gameObject.AddComponent<Button>();
            // Touch UI: no focus concept, don't latch the "selected" tint.
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            UIPulse pulse = img.gameObject.AddComponent<UIPulse>();
            pulse.glowFrames = glow;
            pulse.fps = 6f;
            pulse.scaleAmplitude = 0.04f;
            pulse.speed = 2f;

            TextMeshProUGUI text = NewRect("Label", rect).gameObject.AddComponent<TextMeshProUGUI>();
            Stretch((RectTransform)text.transform);
            text.text = "LET'S GO!";
            text.fontSize = 56;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.965f, 0.878f);   // #fff6e0
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
            // Touch UI: no focus concept, don't latch the "selected" tint.
            button.navigation = new Navigation { mode = Navigation.Mode.None };

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
