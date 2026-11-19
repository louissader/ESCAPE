using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Phase 3.3: swaps placeholder/mismatched visuals for the generated
    /// pixel art. Players get their hero sheets + new animators (tint gone,
    /// root scale normalized, collider world-size preserved ~1.03 x 1.46).
    /// Bridge/Lava/Rectangle/Lever/exit prefabs get their new sprites.
    /// Verifies renderer/collider world bounds within 1% where preserving.
    /// </summary>
    public static class PrefabVisualOverhaul
    {
        [MenuItem("Escape/Overhaul Prefab Visuals")]
        public static void Run()
        {
            OverhaulPlayer(
                "Assets/IndieMarc/PlatformerDemo/Player1.prefab",
                "hero1_ember",
                "Assets/Art/Generated/Animations/Hero1/Hero1.controller");
            OverhaulPlayer(
                "Assets/IndieMarc/PlatformerDemo/Player2.prefab",
                "hero2_wisp",
                "Assets/Art/Generated/Animations/Hero2/Hero2.controller");

            OverhaulBridge();
            OverhaulLavaSquare();
            OverhaulCrystalPlatform();

            OverhaulLever();
            OverhaulExitPortals();

            AssetDatabase.SaveAssets();
            Debug.Log("[PrefabVisualOverhaul] done");
        }

        static void OverhaulPlayer(string prefabPath, string sheet, string controllerPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                BoxCollider2D collider = root.GetComponent<BoxCollider2D>();
                Vector2 beforeColliderWorld = Vector2.Scale(
                    collider.size,
                    new Vector2(Mathf.Abs(root.transform.localScale.x), Mathf.Abs(root.transform.localScale.y)));

                Sprite idle = FirstSprite($"Assets/Art/Generated/Characters/{sheet}.png", $"{sheet}_idle_0");
                if (idle == null)
                {
                    Debug.LogError($"[PrefabVisualOverhaul] missing idle sprite for {sheet}");
                    return;
                }

                renderer.sprite = idle;
                renderer.color = Color.white;   // tint-identity is dead

                // Normalize scale; restore the collider's world size exactly.
                root.transform.localScale = Vector3.one;
                collider.size = beforeColliderWorld;
                collider.offset = Vector2.zero;

                var animator = root.GetComponent<Animator>();
                if (animator == null)
                    animator = root.AddComponent<Animator>();
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                if (controller != null)
                    animator.runtimeAnimatorController = controller;
                else
                    Debug.LogError($"[PrefabVisualOverhaul] missing controller {controllerPath}");

                if (root.GetComponent<PlayerSfx>() == null)
                    root.AddComponent<PlayerSfx>();
                if (root.GetComponent<PlayerHaptics>() == null)
                    root.AddComponent<PlayerHaptics>();

                // FirePoint stays at +/-0.5 on X — matches the new 1-unit-wide body.

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

                Vector2 afterColliderWorld = Vector2.Scale(collider.size, Vector2.one);
                float drift = Vector2.Distance(beforeColliderWorld, afterColliderWorld) /
                              beforeColliderWorld.magnitude;
                Debug.Log($"[PrefabVisualOverhaul] {prefabPath}: collider {beforeColliderWorld} → " +
                          $"{afterColliderWorld} (drift {drift:P2}) sprite={idle.name}");
                if (drift > 0.01f)
                    Debug.LogError($"[PrefabVisualOverhaul] {prefabPath} collider drift exceeds 1%!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void OverhaulBridge()
        {
            const string path = "Assets/GrottoEscapeArtwork/Prefabs/Misc/Bridge.prefab";
            const string sheet = "Assets/Art/Generated/Props/bridge.png";
            var frames = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>()
                .Where(s => s.name.Contains("extend"))
                .OrderBy(s => s.name)
                .ToArray();
            if (frames.Length == 0)
            {
                Debug.LogWarning("[PrefabVisualOverhaul] bridge frames missing — skipped");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                // The pack prefab was saved with an INACTIVE root — Start()
                // never ran on instances, so levers toggled components on an
                // invisible object and generated bridges never extended.
                root.SetActive(true);

                var renderer = root.GetComponent<SpriteRenderer>();
                renderer.sprite = frames[frames.Length - 1];
                renderer.color = Color.white;
                // Playtest 9.3 ("bridge graphics and hitbox don't line up"):
                // the pack prefab carried a leftover x-scale of 8, so every
                // ONE-TILE segment LevelBuilder places was eight tiles wide —
                // art and collider both overhung ~3.5 tiles past each end of
                // a bridge. Each segment is now exactly one tile wide. Height:
                // the 16px frame only has planks on rows 2-13 (12px), so a
                // 0.379 world height renders the planks 0.284 thick, same as
                // the old look.
                FitScaleToWorldSize(root.transform, renderer,
                    new Vector2(1.0f, 0.284f * (16f / 12f)));

                // Collider hugs the opaque plank band (centred 12 of 16 rows)
                // instead of the sprite rect's transparent margins.
                var box = root.GetComponent<BoxCollider2D>();
                if (box != null)
                {
                    box.size = new Vector2(1.0f, 12f / 16f);
                    box.offset = Vector2.zero;
                }

                var bridge = root.GetComponent<IndieMarc.Platformer.Bridge>();
                if (bridge != null)
                {
                    var so = new SerializedObject(bridge);
                    var arr = so.FindProperty("extendFrames");
                    arr.arraySize = frames.Length;
                    for (int i = 0; i < frames.Length; i++)
                        arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[PrefabVisualOverhaul] bridge animated ({frames.Length} frames)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Legacy L2/L3 lava pools are LavaSquare prefab instances. New art +
        /// the 6-frame loop via SpriteCycler; tag/collider untouched so
        /// kill-on-touch behavior cannot change. (The old pass pointed at a
        /// wrong path — the prefab lives under Prefabs/Misc — and at the
        /// retired lava_glow_* frame names, so it always skipped.)
        /// </summary>
        static void OverhaulLavaSquare()
        {
            const string path = "Assets/GrottoEscapeArtwork/Prefabs/Misc/LavaSquare.prefab";
            Sprite[] frames = LevelBuilder.LavaFrames();
            if (frames.Length == 0)
            {
                Debug.LogWarning("[PrefabVisualOverhaul] lava frames missing — LavaSquare skipped");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"[PrefabVisualOverhaul] {path} not found — skipped");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                Vector2 worldSize = renderer.bounds.size;
                renderer.sprite = frames[0];
                renderer.color = Color.white;
                if (worldSize.sqrMagnitude > 0.001f)
                    FitScaleToWorldSize(root.transform, renderer, worldSize);

                var cycler = renderer.gameObject.GetComponent<SpriteCycler>();
                if (cycler == null)
                    cycler = renderer.gameObject.AddComponent<SpriteCycler>();
                cycler.frames = frames;
                cycler.fps = 6f;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[PrefabVisualOverhaul] LavaSquare animated ({frames.Length} frames, world {worldSize})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void OverhaulLever()
        {
            const string path = "Assets/IndieMarc/PlatformerDemo/Prefabs/Lever.prefab";
            Sprite left = FirstSprite("Assets/Art/Generated/Props/lever.png", "lever_flip_0");
            Sprite right = FirstSprite("Assets/Art/Generated/Props/lever.png", "lever_flip_3");
            if (left == null || right == null)
            {
                Debug.LogError("[PrefabVisualOverhaul] lever sprites missing");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                Vector2 worldSize = renderer.bounds.size;
                renderer.sprite = left;
                renderer.color = Color.white;
                FitScaleToWorldSize(root.transform, renderer, worldSize);

                var lever = root.GetComponent<IndieMarc.Platformer.Lever>();
                var so = new SerializedObject(lever);
                so.FindProperty("leverLeft").objectReferenceValue = left;
                so.FindProperty("leverRight").objectReferenceValue = right;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[PrefabVisualOverhaul] lever re-skinned (world size {worldSize})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The "Dragon"-tagged exit prefab keeps its tag + collider (the
        /// dragonCount win mechanic is untouched) but now looks like a
        /// mystical portal instead of pack dragon art. Playtest 5: portals
        /// are superseded by hero-matched ExitDoors — LevelBuilder no longer
        /// places this prefab and disables the legacy instances; the re-skin
        /// stays so any un-migrated scene still looks intentional.
        /// </summary>
        static void OverhaulExitPortals()
        {
            const string path = "Assets/GrottoEscapeArtwork/Prefabs/Enemies/Dragon.prefab";
            Sprite portal = FirstSprite("Assets/Art/Generated/Props/exit_portal.png", "exit_portal_swirl_0");
            if (portal == null || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning("[PrefabVisualOverhaul] portal sprite or Dragon.prefab missing — skipped");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.sprite = portal;
                    renderer.color = Color.white;
                    root.transform.localScale = Vector3.one;
                }
                // Portal doesn't walk or attack.
                foreach (Animator animator in root.GetComponents<Animator>())
                    Object.DestroyImmediate(animator);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[PrefabVisualOverhaul] exit portal re-skinned");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Wisp's transform platform. This pass silently no-oped for two
        /// releases: the old call used the wrong path (the prefab lives in
        /// Prefabs/Misc/) and the wrong sprite name (slices are
        /// crystal_slab_crystal_slab_0..3), so the slab stayed the plain
        /// blue block Louis flagged in playtest 9. Now: crystal art at the
        /// same world size, 4-frame shimmer via SpriteCycler, and a pop-in +
        /// glow from PlatformMaterialize on every transform.
        /// </summary>
        static void OverhaulCrystalPlatform()
        {
            const string prefabPath = "Assets/GrottoEscapeArtwork/Prefabs/Misc/Rectangle.prefab";
            const string sheetPath = "Assets/Art/Generated/Props/crystal_slab.png";

            var frames = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; ; i++)
            {
                Sprite s = FirstSprite(sheetPath, $"crystal_slab_crystal_slab_{i}");
                if (s == null) break;
                frames.Add(s);
            }
            if (frames.Count == 0)
            {
                Debug.LogError($"[PrefabVisualOverhaul] no crystal_slab frames at {sheetPath} — platform untouched");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogError($"[PrefabVisualOverhaul] {prefabPath} not found — platform untouched");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                Vector2 worldSize = renderer.bounds.size;
                renderer.sprite = frames[0];
                renderer.color = Color.white;   // the crystal art carries its own blue
                // Playtest 9.3: Louis wants the platform longer — 4.2 world
                // units instead of 3. The collider lives on the same
                // transform, so it stretches in lockstep with the sprite.
                worldSize.x = 4.2f;
                if (worldSize.sqrMagnitude > 0.001f)
                    FitScaleToWorldSize(root.transform, renderer, worldSize);

                // Playtest 9.4 ("hitbox still broken"): the prefab collider
                // was authored against the OLD 1x1-ish sprite and ToggleForm
                // never resizes it, so after the 2x1-local crystal sprite
                // went in, the collider covered only HALF the visual. Size
                // it to the sprite's local bounds, trimmed vertically to the
                // opaque pixels (rows 4-12 of 16 -> 9/16 of the height).
                var slabBox = root.GetComponent<BoxCollider2D>();
                if (slabBox != null)
                {
                    Vector2 spriteLocal = frames[0].bounds.size;
                    slabBox.size = new Vector2(spriteLocal.x, spriteLocal.y * (9f / 16f));
                    slabBox.offset = Vector2.zero;
                }

                var cycler = root.GetComponent<SpriteCycler>();
                if (cycler == null) cycler = root.AddComponent<SpriteCycler>();
                cycler.frames = frames.ToArray();
                cycler.fps = 6f;

                if (root.GetComponent<PlatformMaterialize>() == null)
                    root.AddComponent<PlatformMaterialize>();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[PrefabVisualOverhaul] crystal platform: {frames.Count} shimmer frames, materialize pop (world {worldSize})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SwapSprite(string prefabPath, Sprite sprite, bool keepWorldSize)
        {
            if (sprite == null)
            {
                Debug.LogError($"[PrefabVisualOverhaul] sprite missing for {prefabPath}");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogWarning($"[PrefabVisualOverhaul] {prefabPath} not found — skipped");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                Vector2 worldSize = renderer.bounds.size;
                renderer.sprite = sprite;
                renderer.color = Color.white;
                if (keepWorldSize && worldSize.sqrMagnitude > 0.001f)
                    FitScaleToWorldSize(root.transform, renderer, worldSize);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[PrefabVisualOverhaul] {prefabPath} → {sprite.name} (world {worldSize})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void FitScaleToWorldSize(Transform transform, SpriteRenderer renderer, Vector2 targetWorld)
        {
            Vector2 spriteWorld = renderer.sprite.bounds.size;
            if (spriteWorld.x < 0.001f || spriteWorld.y < 0.001f)
                return;
            transform.localScale = new Vector3(
                targetWorld.x / spriteWorld.x,
                targetWorld.y / spriteWorld.y,
                1f);
        }

        static Sprite FirstSprite(string path, string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == name);
        }
    }
}
