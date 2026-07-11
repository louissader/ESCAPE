using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Batch of playtest-feedback fixes: smooth collision (players clipping /
    /// catching on floor + player-player), faster movement, blue/red powerup
    /// colors, and removal of the duplicate legacy UI (the source of the
    /// inconsistent menus and the "continue ends the game" bug).
    /// </summary>
    public static class PlaytestFixes
    {
        const string PhysMatPath = "Assets/Settings/PlayerPhysics.physicsMaterial2D";

        [MenuItem("Escape/Playtest Fixes/Run All")]
        public static void RunAll()
        {
            FixPlayerPhysics();
            RecolorPowerups();
            StripLegacyUI();
            AssetDatabase.SaveAssets();
            Debug.Log("[PlaytestFixes] all applied");
        }

        [MenuItem("Escape/Playtest Fixes/Player Physics + Speed")]
        public static void FixPlayerPhysics()
        {
            // Frictionless material: players glide along walls / each other
            // instead of catching, and don't stick when pressed against tiles.
            PhysicsMaterial2D mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(PhysMatPath);
            if (mat == null)
            {
                mat = new PhysicsMaterial2D("PlayerPhysics") { friction = 0f, bounciness = 0f };
                AssetDatabase.CreateAsset(mat, PhysMatPath);
            }
            else
            {
                mat.friction = 0f;
                mat.bounciness = 0f;
                EditorUtility.SetDirty(mat);
            }

            FixOnePlayer("Assets/IndieMarc/PlatformerDemo/Player1.prefab", mat);
            FixOnePlayer("Assets/IndieMarc/PlatformerDemo/Player2.prefab", mat);
        }

        static void FixOnePlayer(string path, PhysicsMaterial2D mat)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var box = root.GetComponent<BoxCollider2D>();
                if (box != null)
                {
                    // Match the 16x24 @16PPU sprite: full-height so feet sit
                    // exactly on the ground (no sinking), slightly narrow so
                    // shoulders don't snag doorways. edgeRadius rounds the
                    // corners so the box glides over tile seams (the "clipping
                    // on the floor" catch).
                    box.size = new Vector2(0.8f, 1.5f);
                    box.offset = new Vector2(0f, 0f);
                    box.edgeRadius = 0.04f;
                    box.sharedMaterial = mat;
                }

                var rb = root.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    rb.interpolation = RigidbodyInterpolation2D.Interpolate; // smooth visuals
                    rb.sharedMaterial = mat;
                }

                // Faster, snappier movement (feedback: too slow).
                foreach (var p1 in root.GetComponents<IndieMarc.Platformer.PlayerCharacterOne>())
                {
                    p1.move_max = 9f;
                    p1.move_accel = 120f;
                    p1.move_deccel = 120f;
                }
                foreach (var p2 in root.GetComponents<IndieMarc.Platformer.PlayerCharacterTwo>())
                {
                    p2.move_max = 9f;
                    p2.move_accel = 120f;
                    p2.move_deccel = 120f;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[PlaytestFixes] physics + speed on {path}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Escape/Playtest Fixes/Recolor Powerups")]
        public static void RecolorPowerups()
        {
            // Swap the old grotto icon for the purpose-built glowing orbs:
            // blue WISP orb for P2's transform, red EMBER orb for P1's shoot.
            SwapPowerupSprite("Assets/GrottoEscapeArtwork/Prefabs/Misc/PowerUpTwo.prefab",
                "Assets/Art/Generated/Props/powerup_wisp.png", "powerup_wisp_pulse_0");
            SwapPowerupSprite("Assets/GrottoEscapeArtwork/Prefabs/Misc/PowerUpOne.prefab",
                "Assets/Art/Generated/Props/powerup_ember.png", "powerup_ember_pulse_0");
        }

        static void SwapPowerupSprite(string prefabPath, string sheet, string frame)
        {
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(sheet)
                .OfType<Sprite>().FirstOrDefault(s => s.name == frame);
            if (sprite == null)
            {
                Debug.LogWarning($"[PlaytestFixes] powerup sprite {frame} missing — kept old icon");
                return;
            }
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var sr = root.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sprite = sprite;
                    sr.color = Color.white;
                    // 1.4 tiles: at 1 tile the orbs read too small next to
                    // the 1.5-tall heroes (playtest feedback).
                    root.transform.localScale = Vector3.one * 1.4f;

                    // Animate the pulse — all 4 frames were generated but
                    // only frame 0 was ever shown.
                    var frames = AssetDatabase.LoadAllAssetsAtPath(sheet)
                        .OfType<Sprite>().OrderBy(f => f.name).ToArray();
                    if (frames.Length > 1)
                    {
                        var cycler = sr.gameObject.GetComponent<SpriteCycler>();
                        if (cycler == null)
                            cycler = sr.gameObject.AddComponent<SpriteCycler>();
                        cycler.frames = frames;
                        cycler.fps = 6f;
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Debug.Log($"[PlaytestFixes] powerup orb (1.4x, pulsing) → {prefabPath}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Removes the original per-scene UI from the legacy levels 1-3 so the
        /// single GameplayUI/TouchControls prefabs are the only UI — fixes the
        /// inconsistent menu layout and the mis-wired old buttons that sent
        /// "continue" to the ending. Only canvases NOT named GameplayUI /
        /// TouchControls are removed.
        /// </summary>
        [MenuItem("Escape/Playtest Fixes/Strip Legacy UI")]
        public static void StripLegacyUI()
        {
            for (int level = 1; level <= 3; level++)
            {
                string path = $"Assets/Scenes/Level {level}.unity";
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int removed = 0;
                foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>(true))
                {
                    string n = canvas.gameObject.name;
                    if (n == "GameplayUI" || n == "TouchControls")
                        continue;
                    Object.DestroyImmediate(canvas.gameObject);
                    removed++;
                }
                if (removed > 0)
                    EditorSceneManager.SaveScene(scene);
                Debug.Log($"[PlaytestFixes] L{level}: removed {removed} legacy canvas(es)");
            }
        }
    }
}
