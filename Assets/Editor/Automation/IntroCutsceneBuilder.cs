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
        // component default and the built scene must never diverge. The
        // SPOKEN narration says more than these captions; its source of
        // truth is tools/vogen/generate_vo.sh (same beat order).
        static readonly string[] Captions =
        {
            "Two friends chased a legend: a cave full of fallen stars.",
            "They found the way in.",
            "The mountain had other plans.",
            "The stars scattered into the deep... every last one.",
            "Deeper than any map had dared to go.",
            "The only way out is down. Together.",
        };

        static readonly string[] PlateSheets =
        {
            "Assets/Art/Generated/Backgrounds/cutscene_approach.png",
            "Assets/Art/Generated/Backgrounds/cutscene_entrance.png",
            "Assets/Art/Generated/Backgrounds/cutscene_collapse.png",
            "Assets/Art/Generated/Backgrounds/cutscene_stars.png",
            "Assets/Art/Generated/Backgrounds/cutscene_descent.png",
            "Assets/Art/Generated/Backgrounds/cutscene_resolve.png",
        };

        static readonly string[] VoiceClips =
        {
            "Assets/Audio/VO/vo_beat0.wav",
            "Assets/Audio/VO/vo_beat1.wav",
            "Assets/Audio/VO/vo_beat2.wav",
            "Assets/Audio/VO/vo_beat3.wav",
            "Assets/Audio/VO/vo_beat4.wav",
            "Assets/Audio/VO/vo_beat5.wav",
        };

        // Plates are native 960x540 (16:9), drawn in a 240x135 composition
        // space — overlay positions below use those composition coords.
        const float PlateAspect = 960f / 540f;
        const float CompW = 240f, CompH = 135f;
        const string StarSheet = "Assets/Art/Generated/Props/star.png";
        // Smooth Bilinear-imported overlays (NOT the 8px pixel-art FX,
        // which would read as blocks scaled over the anti-aliased plates).
        const string DustSheet = "Assets/Art/Generated/Backgrounds/cutscene_dust.png";
        const string StreakSheet = "Assets/Art/Generated/Backgrounds/cutscene_streak.png";
        const string GlowSheet = "Assets/Art/Generated/Backgrounds/cutscene_glow.png";

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

            // Narration: scene-local source so the cutscene speaks even when
            // the persistent AudioManager is absent (direct scene play).
            AudioSource voice = root.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 0f;   // 2D
            cutscene.voiceSource = voice;
            cutscene.beatVoiceOvers = VoiceClips
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();

            if (cutscene.plates.Any(p => p == null))
                Debug.LogWarning("[IntroCutsceneBuilder] cutscene plate sprite(s) missing " +
                                 "— rerun after the art imports");
            if (cutscene.beatVoiceOvers.Any(v => v == null))
                Debug.LogWarning("[IntroCutsceneBuilder] voice-over clip(s) missing — run " +
                                 "tools/vogen/generate_vo.sh, then rebuild the intro");

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[IntroCutsceneBuilder] saved {ScenePath}");
        }

        // ---------- per-beat overlays (Playtest 6) ----------

        /// <summary>
        /// One overlay root per beat, children of the plate so they ride the
        /// Ken-Burns drift (IntroCutscene shows exactly one root per beat).
        /// 6-beat order: approach, entrance, collapse, star-scatter, descent,
        /// resolve — every beat now carries motion (twinkles via UIPulse,
        /// falls/rises/fades via UIDrift, collapse dust via IntroCutscene's
        /// Lissajous through the returned `motes`).
        /// Positions are backgrounds.py composition coords (240x135).
        /// </summary>
        static RectTransform[] BuildBeatOverlays(RectTransform plate, out RectTransform[] motes)
        {
            Sprite[] twinkle = TitleScreenBuilder.LoadFrames(StarSheet, "star_twinkle_", 4);
            // Backgrounds import Single, so these fall back to the whole asset.
            Sprite dust = TitleScreenBuilder.LoadSprite(DustSheet, "cutscene_dust_dust_0");
            Sprite streak = TitleScreenBuilder.LoadSprite(StreakSheet, "cutscene_streak_streak_0");
            Sprite glow = TitleScreenBuilder.LoadSprite(GlowSheet, "cutscene_glow_glow_0");
            if (twinkle.Length == 0)
                Debug.LogWarning("[IntroCutsceneBuilder] star_twinkle frames missing — " +
                                 "star overlays skipped; rerun after the art imports");
            if (dust == null || streak == null || glow == null)
                Debug.LogWarning("[IntroCutsceneBuilder] smooth overlay sprite(s) missing " +
                                 "(dust/streak/glow) — some overlays skipped; rerun after " +
                                 "the art imports");

            var roots = new RectTransform[PlateSheets.Length];
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i] = NewRect($"BeatOverlay_{i}", plate);
                Stretch(roots[i]);
                roots[i].gameObject.SetActive(false);   // IntroCutscene activates per beat
            }

            // Beat 0 — approach: the sky's gold stars twinkle; the distant
            // cave mouth breathes a faint warm glow.
            AddTwinkle(roots[0], 0, 52f, 14f, 40f, twinkle, 4.5f, 2.3f);
            AddTwinkle(roots[0], 1, 150f, 9f, 44f, twinkle, 5.5f, 3.0f);
            AddTwinkle(roots[0], 2, 196f, 26f, 36f, twinkle, 4f, 2.6f);
            AddGlow(roots[0], "MouthGlow", 158f, 74f, 130f, glow,
                    new Color(0.85f, 0.48f, 0.24f, 0.30f), 0.10f, 0.7f);

            // Beat 1 — entrance: gold glints deep in the cave-mouth dark +
            // warm light breathing out of the mouth itself.
            AddTwinkle(roots[1], 0, 117f, 103f, 46f, twinkle, 5f, 3.1f);
            AddTwinkle(roots[1], 1, 128f, 108f, 38f, twinkle, 4f, 2.4f);
            AddTwinkle(roots[1], 2, 133f, 114f, 30f, twinkle, 6f, 3.6f);
            AddTwinkle(roots[1], 3, 110f, 111f, 30f, twinkle, 4.5f, 2.8f);
            AddTwinkle(roots[1], 4, 122f, 117f, 34f, twinkle, 5.5f, 2.0f);
            AddGlow(roots[1], "MouthGlow", 120f, 104f, 300f, glow,
                    new Color(0.90f, 0.62f, 0.30f, 0.16f), 0.08f, 0.6f);

            // Beat 2 — collapse: dust rolling off the rubble pile (drifted by
            // IntroCutscene's Lissajous) + dark debris streaming down the gap.
            var dustList = new System.Collections.Generic.List<RectTransform>(6);
            dustList.Add(AddDust(roots[2], 0, 86f, 119f, 110f, dust));
            dustList.Add(AddDust(roots[2], 1, 154f, 116f, 120f, dust));
            dustList.Add(AddDust(roots[2], 2, 118f, 124f, 130f, dust));
            dustList.Add(AddDust(roots[2], 3, 99f, 97f, 80f, dust));
            dustList.Add(AddDust(roots[2], 4, 141f, 95f, 85f, dust));
            dustList.Add(AddDust(roots[2], 5, 120f, 86f, 70f, dust));
            motes = dustList.ToArray();
            Color debris = new Color(0.23f, 0.17f, 0.15f, 0.55f);
            AddFall(roots[2], "Debris_0", 108f, 66f, 34f, dust, debris, 300f, 420f, 6f, 0.0f);
            AddFall(roots[2], "Debris_1", 124f, 58f, 26f, dust, debris, 380f, 480f, 4f, 1.7f);
            AddFall(roots[2], "Debris_2", 116f, 72f, 20f, dust, debris, 340f, 380f, 8f, 3.1f);
            AddFall(roots[2], "Debris_3", 132f, 64f, 24f, dust, debris, 260f, 440f, 5f, 4.4f);

            // Beat 3 — star scatter: twinkles riding the painted big stars
            // (star centers = backgrounds.py cell origins + 8) + fresh
            // streaks falling through the swarm.
            AddTwinkle(roots[3], 0, 56f, 28f, 96f, twinkle, 5f, 2.2f);
            AddTwinkle(roots[3], 1, 104f, 46f, 88f, twinkle, 6f, 3.0f);
            AddTwinkle(roots[3], 2, 150f, 32f, 96f, twinkle, 4.5f, 2.6f);
            AddTwinkle(roots[3], 3, 84f, 76f, 84f, twinkle, 5.5f, 3.4f);
            AddTwinkle(roots[3], 4, 130f, 82f, 88f, twinkle, 4f, 2.0f);
            AddTwinkle(roots[3], 5, 174f, 64f, 80f, twinkle, 6.5f, 2.9f);
            Color streakGold = new Color(1f, 1f, 1f, 0.85f);
            AddFall(roots[3], "Streak_0", 44f, 18f, 150f, streak, streakGold, 200f, 560f, 10f, 0.6f);
            AddFall(roots[3], "Streak_1", 96f, 10f, 120f, streak, streakGold, 260f, 620f, 8f, 2.2f);
            AddFall(roots[3], "Streak_2", 142f, 22f, 170f, streak, streakGold, 170f, 520f, 12f, 3.9f);
            AddFall(roots[3], "Streak_3", 188f, 12f, 130f, streak, streakGold, 230f, 600f, 9f, 5.1f);

            // Beat 4 — descent: the depths breathe (layered warm glows) and
            // faint embers rise past the ledge.
            AddGlow(roots[4], "DepthGlow_Far", 120f, 132f, 520f, glow,
                    new Color(0.55f, 0.33f, 0.14f, 0.22f), 0.06f, 0.5f);
            AddGlow(roots[4], "DepthGlow_Mid", 119f, 133f, 300f, glow,
                    new Color(0.85f, 0.55f, 0.22f, 0.20f), 0.09f, 0.8f);
            AddGlow(roots[4], "DepthGlow_Core", 119f, 135f, 150f, glow,
                    new Color(1f, 0.72f, 0.35f, 0.22f), 0.12f, 1.1f);
            Color ember = new Color(0.95f, 0.70f, 0.40f, 0.28f);
            AddFall(roots[4], "Ember_0", 100f, 110f, 26f, dust, ember, -26f, 260f, 7f, 0.9f);
            AddFall(roots[4], "Ember_1", 138f, 118f, 20f, dust, ember, -34f, 300f, 5f, 2.6f);
            AddTwinkle(roots[4], 0, 122f, 96f, 40f, twinkle, 4f, 2.1f);
            AddTwinkle(roots[4], 1, 96f, 78f, 30f, twinkle, 5f, 3.2f);

            // Beat 5 — resolve: their path down glows faintly; the heroes'
            // own light holds a twinkle each; one mote drifts up between.
            AddGlow(roots[5], "PathGlow", 138f, 122f, 240f, glow,
                    new Color(0.80f, 0.50f, 0.24f, 0.14f), 0.08f, 0.55f);
            AddTwinkle(roots[5], 0, 103f, 84f, 30f, twinkle, 3.5f, 1.8f);
            AddTwinkle(roots[5], 1, 137f, 84f, 30f, twinkle, 4.2f, 2.4f);
            AddFall(roots[5], "Mote_0", 120f, 100f, 18f, dust,
                    new Color(0.9f, 0.8f, 0.7f, 0.20f), -18f, 220f, 6f, 1.2f);

            Debug.Log("[IntroCutsceneBuilder] beat overlays built for 6 beats " +
                      "(approach/entrance/collapse/scatter/descent/resolve)");
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

        /// <summary>Soft light source (tinted cutscene_glow) with a gentle
        /// UIPulse breathing scale. Alpha lives in the tint.</summary>
        static void AddGlow(RectTransform root, string name, float cx, float cy,
                            float size, Sprite glow, Color tint,
                            float pulseAmp, float pulseSpeed)
        {
            Image img = PlateChild(name, root, cx, cy, size)
                .gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = tint;
            if (glow == null)
            {
                img.gameObject.SetActive(false);
                return;
            }
            img.sprite = glow;
            UIPulse pulse = img.gameObject.AddComponent<UIPulse>();
            pulse.scaleAmplitude = pulseAmp;
            pulse.speed = pulseSpeed;
        }

        /// <summary>Falling / rising element driven by UIDrift (canvas
        /// units: positive fall drops, negative rises; wraps every `wrap`
        /// units so the motion loops for as long as the beat holds).</summary>
        static void AddFall(RectTransform root, string name, float cx, float cy,
                            float size, Sprite sprite, Color tint,
                            float fallSpeed, float wrap, float sway, float phase)
        {
            Image img = PlateChild(name, root, cx, cy, size)
                .gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = tint;
            if (sprite == null)
            {
                img.gameObject.SetActive(false);
                return;
            }
            img.sprite = sprite;
            UIDrift drift = img.gameObject.AddComponent<UIDrift>();
            drift.fallSpeed = fallSpeed;
            drift.wrapHeight = wrap;
            drift.swayX = sway;
            drift.swayY = sway * 0.4f;
            drift.swaySpeed = 0.6f;
            drift.phase = phase;
            drift.fadeInSeconds = 0.4f;
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
            // 96 -> 136 tall: 33.3 pt on an SE3 was under Apple's 44 pt
            // floor, and SKIP is the only escape from the cutscene. Same
            // 136-unit floor as Btn_Pause. Top-right corner, nothing within
            // 240 units of it, so the visual rect can simply grow.
            rect.sizeDelta = new Vector2(240, 136);
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
