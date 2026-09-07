using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Rebuilds Menu Scene into the title-screen redesign: cave backdrop +
    /// vignette, styled ESCAPE! title flanked by twinkling star and EMBER /
    /// WISP idle sprites (UI Images cycled by UIPulse — the canvas is
    /// ScreenSpaceOverlay, so SpriteRenderers can't layer into it), one big
    /// pulsing PLAY button, desktop-only QUIT bottom-right, version label
    /// bottom-left, and the hidden returning-player submenu (CONTINUE /
    /// STORY / HOW TO PLAY / close). TitleMenu on the canvas root drives it
    /// all via persistent onClick calls; the legacy MainMenu component and
    /// its buttons are removed. MUST run after HowToPlayBuilder in any
    /// pipeline — that pass rebuilds the menu's HowToPlayPanel object this
    /// builder wires TitleMenu at. Idempotent: everything this builder owns
    /// lives under GEN_TitleScreen, rebuilt wholesale by name on every run.
    /// </summary>
    public static class TitleScreenBuilder
    {
        const string ScenePath = "Assets/Scenes/Menu Scene.unity";
        const string RootName = "GEN_TitleScreen";

        const string TitleBgSheet = "Assets/Art/Generated/Backgrounds/title_bg.png";
        internal const string BtnPlaySheet = "Assets/Art/Generated/UI/btn_play.png";
        internal const string BtnSmallSheet = "Assets/Art/Generated/UI/btn_small.png";
        const string StarSheet = "Assets/Art/Generated/Props/star.png";
        const string EmberSheet = "Assets/Art/Generated/Characters/hero1_ember.png";
        const string WispSheet = "Assets/Art/Generated/Characters/hero2_wisp.png";
        const string TitleMatPath = "Assets/Art/Generated/UI/TitleFont.mat";

        static readonly Color OutlineColor = new Color(0.165f, 0.102f, 0.133f); // #2a1a22
        static readonly Color EmberGold = new Color(0.957f, 0.753f, 0.353f);    // #f4c05a
        static readonly Color WarmWhite = new Color(1f, 0.965f, 0.878f);        // #fff6e0
        static readonly Color StoneButton = new Color(0.13f, 0.35f, 0.46f, 0.95f);

        [MenuItem("Escape/Build Title Screen")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TitleScreenBuilder] Menu Scene has no canvas — aborted");
                return;
            }
            RectTransform canvasRect = (RectTransform)canvas.transform;

            RemoveLegacyMenu(canvas);

            // Wholesale rebuild of everything this builder owns.
            Transform oldRoot = canvasRect.Find(RootName);
            if (oldRoot != null)
                Object.DestroyImmediate(oldRoot.gameObject);
            RectTransform root = NewRect(RootName, canvasRect);
            Stretch(root);
            root.SetAsFirstSibling();   // HowToPlayPanel etc. draw above

            BuildBackdrop(root);
            BuildTitleGroup(root);
            Button play = BuildPlayButton(root);
            Button quit = BuildCorners(root);
            (GameObject submenu, Button cont, Button story, Button howTo, Button close)
                = BuildSubmenu(root);

            // The reference card HowToPlayBuilder owns — reuse, never duplicate.
            Transform panelTransform = canvasRect.Find("HowToPlayPanel");
            HowToPlayPanel howToPlayPanel = panelTransform != null
                ? panelTransform.GetComponent<HowToPlayPanel>()
                : null;
            if (howToPlayPanel == null)
                howToPlayPanel = HowToPlayBuilder.BuildPanel(canvasRect);
            howToPlayPanel.transform.SetAsLastSibling();   // above the submenu

            // Controller on the canvas root, wired to all of the above.
            TitleMenu menu = canvas.GetComponent<TitleMenu>();
            if (menu == null)
                menu = canvas.gameObject.AddComponent<TitleMenu>();
            menu.playButton = play;
            menu.quitButton = quit;
            menu.continueButton = cont;
            menu.storyButton = story;
            menu.howToPlayButton = howTo;
            menu.closeSubmenuButton = close;
            menu.submenuPanel = submenu;
            menu.howToPlayPanel = howToPlayPanel;
            EditorUtility.SetDirty(menu);

            Wire(play, menu.OnPlay);
            Wire(quit, menu.OnQuit);
            Wire(cont, menu.OnContinue);
            Wire(story, menu.OnStory);
            Wire(howTo, menu.OnHowToPlay);
            Wire(close, menu.OnCloseSubmenu);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[TitleScreenBuilder] title screen rebuilt in Menu Scene");
        }

        /// <summary>Old flat menu: stacked default buttons + plain title text,
        /// plus the MainMenu controller they called. All superseded.</summary>
        static void RemoveLegacyMenu(Canvas canvas)
        {
            string[] legacyNames =
                { "PlayButton", "QuitButton", "StoryButton", "HowToPlayButton", "Escape! Text" };
            foreach (string name in legacyNames)
            {
                Transform t = canvas.transform.Find(name);
                if (t != null)
                    Object.DestroyImmediate(t.gameObject);
            }

            MainMenu legacy = canvas.GetComponent<MainMenu>();
            if (legacy == null)
                legacy = Object.FindObjectOfType<MainMenu>();
            if (legacy != null)
                Object.DestroyImmediate(legacy);
        }

        // ---------- backdrop ----------

        static void BuildBackdrop(RectTransform root)
        {
            // Aspect-fill: EnvelopeParent keeps 480x270 art edge-to-edge at
            // any device ratio (crops instead of letterboxing).
            Image bg = NewRect("Background", root).gameObject.AddComponent<Image>();
            Stretch((RectTransform)bg.transform);
            Sprite bgSprite = LoadSprite(TitleBgSheet, "title_bg_plate_0");
            if (bgSprite != null)
            {
                bg.sprite = bgSprite;
                var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = 480f / 270f;
            }
            else
            {
                bg.color = new Color(0.07f, 0.10f, 0.16f);   // cave-dark stand-in
                Debug.LogWarning("[TitleScreenBuilder] title_bg sprite missing — " +
                                 "rerun after the art imports");
            }
            bg.raycastTarget = false;

            // Vignette approximation per design contract: a full-screen dark
            // wash plus a heavier bottom band (plain Images, kept simple).
            Image wash = NewRect("Vignette", root).gameObject.AddComponent<Image>();
            Stretch((RectTransform)wash.transform);
            wash.color = new Color(0f, 0f, 0f, 0.25f);
            wash.raycastTarget = false;

            Image bottom = NewRect("VignetteBottom", root).gameObject.AddComponent<Image>();
            RectTransform bottomRect = (RectTransform)bottom.transform;
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(1f, 0.38f);
            bottomRect.offsetMin = Vector2.zero;
            bottomRect.offsetMax = Vector2.zero;
            bottom.color = new Color(0f, 0f, 0f, 0.2f);
            bottom.raycastTarget = false;
        }

        // ---------- title ----------

        static void BuildTitleGroup(RectTransform root)
        {
            RectTransform group = NewRect("TitleGroup", root);
            Place(group, new Vector2(0, 240), new Vector2(1600, 300));

            TextMeshProUGUI title = NewRect("TitleText", group).gameObject
                .AddComponent<TextMeshProUGUI>();
            Place((RectTransform)title.transform, new Vector2(0, 10), new Vector2(1240, 260));
            title.text = "ESCAPE!";
            title.fontStyle = FontStyles.Bold;
            title.enableAutoSizing = true;                 // size-fit
            title.fontSizeMin = 90;
            title.fontSizeMax = 170;
            title.characterSpacing = 6;
            title.alignment = TextAlignmentOptions.Center;
            title.enableVertexGradient = true;             // hero-palette accent
            title.colorGradient = new VertexGradient(WarmWhite, WarmWhite, EmberGold, EmberGold);
            title.raycastTarget = false;
            Material titleMat = TitleMaterial(title.font);
            if (titleMat != null)
                title.fontSharedMaterial = titleMat;       // outline + drop shadow

            // Twinkling star accent riding the "!" end of the title.
            Sprite[] twinkle = LoadFrames(StarSheet, "star_twinkle_", 4);
            Image star = NewRect("TitleStar", group).gameObject.AddComponent<Image>();
            Place((RectTransform)star.transform, new Vector2(505, 118), new Vector2(80, 80));
            star.raycastTarget = false;
            star.preserveAspect = true;
            if (twinkle.Length > 0)
            {
                star.sprite = twinkle[0];
                UIPulse starPulse = star.gameObject.AddComponent<UIPulse>();
                starPulse.glowFrames = twinkle;
                starPulse.fps = 5f;
                starPulse.scaleAmplitude = 0.08f;
                starPulse.speed = 3f;
            }
            else
            {
                star.gameObject.SetActive(false);
            }

            HeroFlank(group, "Ember", EmberSheet, "hero1_ember_idle_", new Vector2(-640, -20), false);
            HeroFlank(group, "Wisp", WispSheet, "hero2_wisp_idle_", new Vector2(640, -20), true);
        }

        static void HeroFlank(RectTransform group, string name, string sheet,
                              string framePrefix, Vector2 pos, bool faceLeft)
        {
            Sprite[] idle = LoadFrames(sheet, framePrefix, 2);
            if (idle.Length == 0)
            {
                Debug.LogWarning($"[TitleScreenBuilder] {name} idle frames missing — flank skipped");
                return;
            }
            Image img = NewRect(name, group).gameObject.AddComponent<Image>();
            RectTransform rect = (RectTransform)img.transform;
            Place(rect, pos, new Vector2(120, 180));       // 16x24 art at 7.5x
            if (faceLeft)
                rect.localScale = new Vector3(-1f, 1f, 1f); // UIPulse preserves the flip
            img.sprite = idle[0];
            img.preserveAspect = true;
            img.raycastTarget = false;
            UIPulse cycle = img.gameObject.AddComponent<UIPulse>();
            cycle.glowFrames = idle;
            cycle.fps = 3f;
            cycle.scaleAmplitude = 0f;                     // frame cycle only, no pulse
        }

        // ---------- buttons ----------

        static Button BuildPlayButton(RectTransform root)
        {
            // Own stretch+fitter wrapper (not BuildCorners' container, which
            // is created later) so the primary CTA respects the safe area
            // without changing sibling render order.
            RectTransform safe = NewRect("PlaySafeArea", root);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            Sprite[] glow = LoadFrames(BtnPlaySheet, "btn_play_glow_", 4);
            Button play = MakeSpriteButton(safe, "Btn_Play", "PLAY",
                glow.Length > 0 ? glow[0] : null,
                new Vector2(0, -160), new Vector2(512, 160), 66);
            if (glow.Length > 0)
            {
                UIPulse pulse = play.gameObject.AddComponent<UIPulse>();
                pulse.glowFrames = glow;                   // breathing rim glow
                pulse.fps = 6f;
                pulse.scaleAmplitude = 0.04f;
                pulse.speed = 2f;
            }
            else
            {
                Debug.LogWarning("[TitleScreenBuilder] btn_play sprites missing — " +
                                 "rerun after the art imports");
            }
            return play;
        }

        static Button BuildCorners(RectTransform root)
        {
            RectTransform safe = NewRect("CornerSafeArea", root);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // Small, low-emphasis QUIT; TitleMenu hides it on mobile at Start.
            Button quit = MakeSpriteButton(safe, "Btn_Quit", "QUIT",
                LoadSprite(BtnSmallSheet, "btn_small_pill_0"),
                Vector2.zero, new Vector2(180, 56), 26);
            RectTransform quitRect = (RectTransform)quit.transform;
            quitRect.anchorMin = new Vector2(1f, 0f);
            quitRect.anchorMax = new Vector2(1f, 0f);
            quitRect.anchoredPosition = new Vector2(-140, 60);
            Image quitImage = quit.GetComponent<Image>();
            quitImage.color = new Color(quitImage.color.r, quitImage.color.g,
                                        quitImage.color.b, 0.85f);
            TextMeshProUGUI quitLabel = quit.GetComponentInChildren<TextMeshProUGUI>();
            if (quitLabel != null)
                quitLabel.color = new Color(0.80f, 0.82f, 0.85f, 0.9f);

            // TitleMenu refreshes this from Application.version at runtime;
            // bundleVersion is the same value at build time.
            TextMeshProUGUI version = NewRect("VersionLabel", safe).gameObject
                .AddComponent<TextMeshProUGUI>();
            RectTransform versionRect = (RectTransform)version.transform;
            versionRect.anchorMin = new Vector2(0f, 0f);
            versionRect.anchorMax = new Vector2(0f, 0f);
            versionRect.pivot = new Vector2(0f, 0.5f);
            versionRect.anchoredPosition = new Vector2(30, 60);
            versionRect.sizeDelta = new Vector2(400, 40);
            version.text = "v" + PlayerSettings.bundleVersion;
            version.fontSize = 24;
            version.alignment = TextAlignmentOptions.Left;
            version.color = new Color(1f, 1f, 1f, 0.45f);
            version.raycastTarget = false;

            return quit;
        }

        static (GameObject panel, Button cont, Button story, Button howTo, Button close)
            BuildSubmenu(RectTransform root)
        {
            RectTransform panel = NewRect("SubmenuPanel", root);
            Stretch(panel);
            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);      // blocks the title buttons
            panel.gameObject.AddComponent<CanvasGroup>();

            RectTransform card = NewRect("Card", panel);
            Place(card, new Vector2(0, -10), new Vector2(620, 560));
            Image cardImg = card.gameObject.AddComponent<Image>();
            cardImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            cardImg.type = Image.Type.Sliced;
            cardImg.color = new Color(0.05f, 0.11f, 0.15f, 0.97f);

            Sprite pill = LoadSprite(BtnSmallSheet, "btn_small_pill_0");
            // 110 units is 38.2 pt on an SE3, under Apple's 44 pt floor.
            // 136 (47.2 pt) is the project's tap-target floor; pitch goes
            // 140 -> 144 so the taller rows keep a visible gap, and the
            // stack still centres on 10 and stays inside the 560-tall card
            // (top 222, bottom -202 against +/-280).
            Button cont = MakeSpriteButton(card, "Btn_Continue", "CONTINUE",
                pill, new Vector2(0, 154), new Vector2(460, 136), 44);
            Button story = MakeSpriteButton(card, "Btn_Story", "STORY",
                pill, new Vector2(0, 10), new Vector2(460, 136), 44);
            Button howTo = MakeSpriteButton(card, "Btn_HowToPlay", "HOW TO PLAY",
                pill, new Vector2(0, -134), new Vector2(460, 136), 40);

            Button close = MakeSpriteButton(card, "Btn_CloseSubmenu", "X",
                pill, Vector2.zero, new Vector2(60, 60), 30);
            RectTransform closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-16, -16);

            // The 60x60 pill is 20.8 pt on an SE3. It cannot simply grow:
            // a 136x136 rect centred on the card corner would cover the
            // top-right corner of Btn_Continue (460x136 at y 154, so
            // x -230..230, y 86..222) and, being the last sibling, would win
            // that raycast. So the hit surface is an invisible 136x136 child
            // - HUDBuilder's MakeTapArea idiom - offset +34/+34 up and
            // right. It still contains the whole visible pill (x 234..294,
            // y 204..264 in card space) but starts at x 230 / y 200, so it
            // only touches Btn_Continue's corner instead of overlapping it.
            // No Button of its own: pointer events bubble to the parent via
            // ExecuteHierarchy, so the pill keeps its own press tint.
            Image closeHit = NewRect("TapArea", closeRect).gameObject.AddComponent<Image>();
            RectTransform closeHitRect = (RectTransform)closeHit.transform;
            closeHitRect.anchorMin = new Vector2(0.5f, 0.5f);
            closeHitRect.anchorMax = new Vector2(0.5f, 0.5f);
            closeHitRect.pivot = new Vector2(0.5f, 0.5f);
            closeHitRect.anchoredPosition = new Vector2(34f, 34f);
            closeHitRect.sizeDelta = new Vector2(136f, 136f);
            closeHit.color = Color.clear;

            panel.gameObject.SetActive(false);             // TitleMenu.OnPlay opens it
            return (panel.gameObject, cont, story, howTo, close);
        }

        /// <summary>Button from a generated sprite, falling back to the
        /// builtin sliced UISprite (dark stone tint) until the art imports.</summary>
        static Button MakeSpriteButton(RectTransform parent, string name, string label,
                                       Sprite sprite, Vector2 pos, Vector2 size, float fontSize)
        {
            Image img = NewRect(name, parent).gameObject.AddComponent<Image>();
            Place((RectTransform)img.transform, pos, size);
            if (sprite != null)
            {
                img.sprite = sprite;
            }
            else
            {
                img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                img.type = Image.Type.Sliced;
                img.color = StoneButton;
            }
            Button button = img.gameObject.AddComponent<Button>();
            // Touch UI: no focus concept, don't latch the "selected" tint.
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            TextMeshProUGUI text = NewRect("Label", (RectTransform)img.transform)
                .gameObject.AddComponent<TextMeshProUGUI>();
            Stretch((RectTransform)text.transform);
            text.text = label;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = WarmWhite;
            text.raycastTarget = false;
            return button;
        }

        /// <summary>Find-or-create the title's TMP material: thick #2a1a22
        /// outline + soft dark underlay (drop shadow) over the default font
        /// atlas. A saved asset — never mutates the font's own material.</summary>
        static Material TitleMaterial(TMP_FontAsset font)
        {
            if (font == null)
                return null;
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(TitleMatPath);
            if (mat == null)
            {
                mat = new Material(font.material);
                AssetDatabase.CreateAsset(mat, TitleMatPath);
            }
            else
            {
                mat.shader = font.material.shader;
                mat.CopyPropertiesFromMaterial(font.material);
            }
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.06f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.7f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.5f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.25f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        // ---------- sprite loading (LevelMenuBuilder pattern) ----------

        /// <summary>Named sub-sprite, falling back to the whole asset for
        /// Single-import-mode sheets (Backgrounds import unsliced).</summary>
        internal static Sprite LoadSprite(string path, string spriteName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite && sprite.name == spriteName)
                    return sprite;
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Frames prefix_0..prefix_(count-1); missing frames are
        /// skipped so a partial import still animates.</summary>
        internal static Sprite[] LoadFrames(string path, string prefix, int count)
        {
            var frames = new System.Collections.Generic.List<Sprite>(count);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < count; i++)
            {
                foreach (Object asset in assets)
                {
                    if (asset is Sprite sprite && sprite.name == prefix + i)
                    {
                        frames.Add(sprite);
                        break;
                    }
                }
            }
            return frames.ToArray();
        }

        // ---------- rect helpers (HUDBuilder idioms) ----------

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
    }
}
