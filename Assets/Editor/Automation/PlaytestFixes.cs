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
            EaseLevelOne();
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

        // Hero mapping READ from the pickup scripts, not assumed:
        //   PowerUpOne.cs  → PlayerCharacterOne.EnableShooting()  → EMBER (P1) → powerup_ember
        //   PowerUpTwo.cs  → PlayerCharacterTwo.CollectPowerUp()  → WISP  (P2) → powerup_wisp
        const string PowerUp1Prefab = "Assets/GrottoEscapeArtwork/Prefabs/Misc/PowerUpOne.prefab";
        const string PowerUp2Prefab = "Assets/GrottoEscapeArtwork/Prefabs/Misc/PowerUpTwo.prefab";
        const string EmberOrbSheet = "Assets/Art/Generated/Props/powerup_ember.png";
        const string WispOrbSheet = "Assets/Art/Generated/Props/powerup_wisp.png";
        const float OrbWorldHeight = 2f;   // exactly 2 tiles, playtest 6
        const float OrbFps = 5f;

        /// <summary>Kept so MasterPipeline's existing wiring keeps working.</summary>
        [MenuItem("Escape/Playtest Fixes/Recolor Powerups")]
        public static void RecolorPowerups()
        {
            FixPowerupVisuals();
        }

        /// <summary>
        /// Playtest 6 P0. The round-5 prefab patch never showed at runtime:
        /// both powerup prefabs still carry the ENABLED legacy Grotto Animator
        /// (power-up-01.controller → PowerUpAnimation.anim) whose clip rewrites
        /// m_Sprite every frame from the old 16px @100PPU Grotto icons. The
        /// animation update runs after script Update, so it wins over both the
        /// serialized sprite AND SpriteCycler — every scene instance rendered
        /// the tiny old icon. This pass removes that Animator and re-normalizes
        /// sprite/cycler/scale/trigger on the PREFABS (PowerUpOne.prefab is a
        /// VARIANT of PowerUpTwo.prefab, so the base is fixed first) and on
        /// EVERY component instance in every level scene. Idempotent: absolute
        /// target values only, never multiplied onto existing state.
        /// Pipeline ordering: must run AFTER LevelBuilder.BuildAllLevels —
        /// that pass re-instantiates generated-level powerups at 1.8 tiles.
        /// </summary>
        [MenuItem("Escape/Playtest Fixes/Fix Powerup Visuals")]
        public static void FixPowerupVisuals()
        {
            // Base prefab first: removing the legacy Animator from the base
            // clears it from the variant too.
            FixPowerupPrefab(PowerUp2Prefab);
            FixPowerupPrefab(PowerUp1Prefab);

            for (int level = 1; level <= SceneRouter.LevelCount; level++)
            {
                string path = $"Assets/Scenes/{SceneRouter.LevelSceneName(level)}.unity";
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogError($"[PowerupFix] scene missing: {path}");
                    continue;
                }
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int touched = 0;
                foreach (var p1 in Object.FindObjectsOfType<PowerUpOne>(true))
                    touched += FixPowerupObject(p1.gameObject, EmberOrbSheet, scene.name) ? 1 : 0;
                foreach (var p2 in Object.FindObjectsOfType<PowerUpTwo>(true))
                    touched += FixPowerupObject(p2.gameObject, WispOrbSheet, scene.name) ? 1 : 0;
                if (touched > 0)
                    EditorSceneManager.SaveScene(scene);
                Debug.Log($"[PowerupFix] {scene.name}: {touched} powerup instance(s) processed");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[PowerupFix] complete (prefabs + all level scenes)");
        }

        static void FixPowerupPrefab(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogError($"[PowerupFix] prefab missing: {path}");
                return;
            }
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool ember = root.GetComponentInChildren<PowerUpOne>(true) != null;
                bool wisp = root.GetComponentInChildren<PowerUpTwo>(true) != null;
                if (ember == wisp)   // both or neither — hero can't be inferred
                {
                    Debug.LogError($"[PowerupFix] {path}: PowerUpOne={ember} PowerUpTwo={wisp} — skipped");
                    return;
                }
                if (FixPowerupObject(root, ember ? EmberOrbSheet : WispOrbSheet, "prefab:" + path))
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Normalizes one powerup object (prefab contents or scene instance):
        /// kills the sprite-clobbering legacy Animator, assigns the 4 pulse
        /// frames by EXPLICIT sheet-prefixed name (missing frame ⇒ null sprite
        /// + error, never an unsliced whole-sheet fallback), wires SpriteCycler
        /// at 5 fps, sets an ABSOLUTE scale for exactly 2.0 tiles of world
        /// height, and sizes the pickup trigger to the orb (the Grotto-era box
        /// was 0.09 local units — authored for the old 0.16-unit icon).
        /// Returns true when the object was modified (caller saves).
        /// </summary>
        static bool FixPowerupObject(GameObject go, string sheet, string context)
        {
            if (go.GetComponentInChildren<PowerUpOne>(true) != null &&
                go.GetComponentInChildren<PowerUpTwo>(true) != null)
            {
                Debug.LogError($"[PowerupFix] {context}: {go.name} has BOTH pickup scripts — skipped");
                return false;
            }
            var sr = go.GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null)
            {
                Debug.LogError($"[PowerupFix] {context}: {go.name} has no SpriteRenderer — skipped");
                return false;
            }

            // The legacy Animator is THE reason the round-5 fix never showed.
            foreach (var animator in go.GetComponentsInChildren<Animator>(true))
            {
                try { Object.DestroyImmediate(animator); }
                catch (System.InvalidOperationException) { /* prefab-instance component — fall through */ }
                if (animator == null)
                {
                    Debug.Log($"[PowerupFix] {context}: {go.name} legacy Animator removed");
                }
                else
                {
                    animator.enabled = false;   // couldn't destroy here (prefab-instance component)
                    EditorUtility.SetDirty(animator);
                    Debug.LogWarning($"[PowerupFix] {context}: {go.name} Animator could not be destroyed — disabled instead");
                }
            }

            string baseName = System.IO.Path.GetFileNameWithoutExtension(sheet);
            Sprite[] all = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().ToArray();
            var frames = new Sprite[4];
            bool missing = false;
            for (int i = 0; i < frames.Length; i++)
            {
                string frameName = $"{baseName}_pulse_{i}";   // slicer prefixes the sheet name
                frames[i] = all.FirstOrDefault(s => s.name == frameName);
                if (frames[i] == null)
                {
                    missing = true;
                    Debug.LogError($"[PowerupFix] {context}: frame '{frameName}' missing from {sheet} " +
                                   $"(sheet has: {string.Join(", ", all.Select(s => s.name))})");
                }
            }
            if (missing)
            {
                // Never show a whole/unsliced sheet for a multi-frame asset —
                // and don't let a stale cycler re-assign old frames at runtime.
                sr.sprite = null;
                var staleCycler = sr.GetComponent<SpriteCycler>();
                if (staleCycler != null)
                    staleCycler.frames = new Sprite[0];
                Debug.LogError($"[PowerupFix] {context}: {go.name} sprite set to NULL until {baseName} slices correctly");
                EditorUtility.SetDirty(go);
                return true;
            }

            sr.sprite = frames[0];
            sr.color = Color.white;

            var cycler = sr.GetComponent<SpriteCycler>();
            if (cycler == null)
                cycler = sr.gameObject.AddComponent<SpriteCycler>();
            cycler.frames = frames;
            cycler.fps = OrbFps;

            // ABSOLUTE scale from sprite bounds (never multiply existing
            // scale): re-runs and stale scales from earlier passes converge on
            // the same value regardless of the sheet's pixel size or PPU.
            float spriteH = frames[0].bounds.size.y;
            float parentH = go.transform.parent != null ? Mathf.Abs(go.transform.parent.lossyScale.y) : 1f;
            if (parentH < 1e-4f)
                parentH = 1f;
            float scale = spriteH > 0.001f ? OrbWorldHeight / (spriteH * parentH) : 1f;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var box = go.GetComponentInChildren<BoxCollider2D>(true);
            if (box != null)
            {
                box.isTrigger = true;
                box.size = frames[0].bounds.size;   // local units at scale 1
                box.offset = Vector2.zero;
            }

            float worldH = frames[0].bounds.size.y * Mathf.Abs(go.transform.lossyScale.y);
            Debug.Log($"[PowerupFix] {context}: {go.name} sprite={frames[0].name} frames=4 fps={OrbFps} " +
                      $"worldHeight={worldH:F2} trigger={(box != null ? box.size.ToString("F2") : "none")}");
            EditorUtility.SetDirty(go);
            return true;
        }

        /// <summary>
        /// Playtest 5: Level 1 filtered too many new players. One extra
        /// checkpoint ~midway, one extra heart ~2/3 through, and every
        /// enemy's patrol/chase speed at 0.7x. Additions live under a GEN_
        /// holder (rebuilt here, cleared by LevelBuilder's GEN_ sweep — so
        /// this must run AFTER BuildAllLevels in any pipeline); the speed
        /// scale only applies while a value still matches its prefab default,
        /// so re-runs can never compound it.
        /// </summary>
        [MenuItem("Escape/Playtest Fixes/Ease Level One")]
        public static void EaseLevelOne()
        {
            const string holderName = "GEN_EaseL1";
            // Eases the FIRST level of the shipped progression (L1 "First
            // Light" — already gentle by design; this pass just adds a
            // midway torch + heart and softens enemy speeds further).
            var scene = EditorSceneManager.OpenScene(
                $"Assets/Scenes/{SceneRouter.LevelSceneName(SceneRouter.FirstLevel)}.unity",
                OpenSceneMode.Single);

            GameObject old = GameObject.Find(holderName);
            if (old != null)
                Object.DestroyImmediate(old);
            GameObject holder = new GameObject(holderName);

            var tilemap = LevelBuilder.FindColliderTilemap();

            // Extra checkpoint ~midway, skipping spots that crowd an
            // existing torch (the augment pass already placed one mid-level).
            var checkpoints = Object.FindObjectsOfType<Checkpoint>();
            Vector3? cpSpot = null;
            foreach (float fx in new[] { 0.5f, 0.42f, 0.58f, 0.35f })
            {
                Vector3? candidate = LevelBuilder.FindStandableWorld(tilemap, fx);
                if (candidate.HasValue &&
                    !checkpoints.Any(c => Vector3.Distance(c.transform.position, candidate.Value) < 4f))
                {
                    cpSpot = candidate;
                    break;
                }
            }
            if (cpSpot.HasValue)
                LevelBuilder.AddCheckpoint(holder, cpSpot.Value);
            else
                Debug.LogWarning("[PlaytestFixes] L1: no clear midway checkpoint spot found");

            Vector3? heartSpot = LevelBuilder.FindStandableWorld(tilemap, 0.67f);
            if (heartSpot.HasValue)
                LevelBuilder.AddHeart(holder, heartSpot.Value);
            else
                Debug.LogWarning("[PlaytestFixes] L1: no standable heart spot at 2/3");

            int slowed = 0;
            foreach (var bat in Object.FindObjectsOfType<IndieMarc.Platformer.Bat>())
                slowed += SlowEnemyOnce(bat, b => b.moveSpeed, (b, v) => b.moveSpeed = v);
            foreach (var ghost in Object.FindObjectsOfType<IndieMarc.Platformer.Ghost>())
                slowed += SlowEnemyOnce(ghost, g => g.moveSpeed, (g, v) => g.moveSpeed = v);
            foreach (var patrol in Object.FindObjectsOfType<GroundPatrol>())
                slowed += SlowEnemyOnce(patrol, p => p.speed, (p, v) => p.speed = v);
            foreach (var eye in Object.FindObjectsOfType<EyeEnemy>())
                slowed += SlowEnemyOnce(eye, e => e.speed, (e, v) => e.speed = v);

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PlaytestFixes] L1 eased: checkpoint={cpSpot.HasValue} " +
                      $"heart={heartSpot.HasValue} enemies slowed x0.7: {slowed}");
        }

        /// <summary>
        /// 0.7x an enemy speed field exactly once: only while the current
        /// value still matches the prefab default (±5%). Already-slowed or
        /// hand-tuned values are left alone; non-prefab components have no
        /// baseline to compare against, so they are skipped.
        /// </summary>
        static int SlowEnemyOnce<T>(T component, System.Func<T, float> get, System.Action<T, float> set)
            where T : Component
        {
            T source = PrefabUtility.GetCorrespondingObjectFromSource(component);
            if (source == null)
                return 0;
            float baseline = get(source);
            float current = get(component);
            if (baseline < 0.001f || Mathf.Abs(current - baseline) > baseline * 0.05f)
                return 0;
            set(component, current * 0.7f);
            EditorUtility.SetDirty(component);
            return 1;
        }

    }
}
