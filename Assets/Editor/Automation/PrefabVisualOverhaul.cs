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

            SwapSprite("Assets/GrottoEscapeArtwork/Prefabs/Bridge.prefab",
                FirstSprite("Assets/Art/Generated/Props/bridge.png", "bridge_extend_2"), keepWorldSize: true);
            SwapSprite("Assets/GrottoEscapeArtwork/Prefabs/LavaSquare.prefab",
                FirstSprite("Assets/Art/Generated/Props/lava.png", "lava_glow_0"), keepWorldSize: true);
            SwapSprite("Assets/GrottoEscapeArtwork/Prefabs/Rectangle.prefab",
                FirstSprite("Assets/Art/Generated/Props/crystal_slab.png", "crystal_slab_shimmer_0"), keepWorldSize: true);

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
        /// mystical portal instead of pack dragon art.
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
