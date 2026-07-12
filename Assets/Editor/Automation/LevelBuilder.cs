using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Builds/updates a level scene from a LevelDefinition — tilemaps,
    /// entities, camera rig, UI, lights, parallax. Idempotent: objects it
    /// owns are tagged by name prefix "GEN_" and rebuilt on every run;
    /// everything else in an existing scene is left alone.
    /// </summary>
    public static class LevelBuilder
    {
        const string GenPrefix = "GEN_";
        const int GroundLayer = 0; // resolved at runtime from "Level" layer if present

        // Prefab paths.
        const string Player1Prefab = "Assets/IndieMarc/PlatformerDemo/Player1.prefab";
        const string Player2Prefab = "Assets/IndieMarc/PlatformerDemo/Player2.prefab";
        const string ExitPrefab = "Assets/GrottoEscapeArtwork/Prefabs/Enemies/Dragon.prefab";
        const string LeverPrefab = "Assets/IndieMarc/PlatformerDemo/Prefabs/Lever.prefab";
        const string BridgePrefab = "Assets/GrottoEscapeArtwork/Prefabs/Misc/Bridge.prefab";
        const string PowerUp1Prefab = "Assets/GrottoEscapeArtwork/Prefabs/Misc/PowerUpOne.prefab";
        const string PowerUp2Prefab = "Assets/GrottoEscapeArtwork/Prefabs/Misc/PowerUpTwo.prefab";
        const string RuleTilePath = "Assets/GrottoEscapeArtwork/Sprites/GrottoRuleTile.asset";

        static readonly Dictionary<LevelDefinition.EnemyType, string> EnemyPrefabs =
            new Dictionary<LevelDefinition.EnemyType, string>
        {
            { LevelDefinition.EnemyType.Bat, "Assets/GrottoEscapeArtwork/Prefabs/Enemies/Bat.prefab" },
            { LevelDefinition.EnemyType.BatPassive, "Assets/GrottoEscapeArtwork/Prefabs/Enemies/Bat.prefab" },
            { LevelDefinition.EnemyType.Ghost, "Assets/GrottoEscapeArtwork/Prefabs/Enemies/Ghost.prefab" },
            { LevelDefinition.EnemyType.Slime, "Assets/GrottoEscapeArtwork/Prefabs/Enemies/Slime.prefab" },
            { LevelDefinition.EnemyType.Skeleton, "Assets/GrottoEscapeArtwork/Prefabs/Enemies/Skeleton.prefab" },
            { LevelDefinition.EnemyType.Eye, "Assets/GrottoEscapeArtwork/Prefabs/Enemies/Eye.prefab" },
            { LevelDefinition.EnemyType.Frog, "Assets/GrottoEscapeArtwork/Prefabs/Enemies/Frog.prefab" },
        };

        [MenuItem("Escape/Build All Levels")]
        public static void BuildAllLevels()
        {
            var defs = AssetDatabase.FindAssets("t:LevelDefinition", new[] { "Assets/Levels" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LevelDefinition>)
                .Where(d => d != null)
                .OrderBy(d => d.levelNumber)
                .ToList();

            if (defs.Count == 0)
            {
                Debug.LogError("[LevelBuilder] no LevelDefinition assets under Assets/Levels");
                return;
            }

            foreach (LevelDefinition def in defs)
            {
                BuildLevel(def, $"Assets/Scenes/Level {def.levelNumber}.unity");
                if (IsLegacyLevel(def.levelNumber))
                {
                    // Original hand-built geometry is preserved verbatim and
                    // human-verified (the team's gameplay videos beat all
                    // three) — the grid model can't see the P2-boost chains
                    // they use, so the automated check doesn't apply.
                    Debug.Log($"[Solvability] L{def.levelNumber}: SKIPPED (legacy geometry, human-verified)");
                    continue;
                }
                bool solvable = LevelSolvability.Check(def);
                if (!solvable)
                    Debug.LogError($"[LevelBuilder] Level {def.levelNumber} FAILED solvability!");
            }

            UpdateBuildSettings(defs.Select(d => d.levelNumber).ToList());
            Debug.Log($"[LevelBuilder] built {defs.Count} levels");
        }

        /// <summary>
        /// Levels 1-3 keep their proven hand-built geometry and entities;
        /// the builder only AUGMENTS them (camera rig, lights, parallax,
        /// pickups, hints, retunes). Levels 4+ are fully generated.
        /// </summary>
        public static bool IsLegacyLevel(int levelNumber) { return levelNumber <= 3; }

        public static void BuildLevel(LevelDefinition def, string scenePath)
        {
            Scene scene = System.IO.File.Exists(scenePath)
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Clear previously generated objects (idempotency).
            foreach (GameObject root in scene.GetRootGameObjects().ToArray())
                if (root.name.StartsWith(GenPrefix))
                    Object.DestroyImmediate(root);

            bool augment = IsLegacyLevel(def.levelNumber);
            if (augment)
            {
                AugmentLegacyLevel(def);
            }
            else
            {
                BuildTilemaps(def);
                BuildEntities(def);
            }
            BuildCamera(def);
            BuildLighting(def);
            BuildParallax(def);
            EnsureSystems(def);

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[LevelBuilder] saved {scenePath} " +
                      (augment ? "(augmented legacy geometry)" :
                       $"({def.GridWidth}x{def.GridHeight} tiles, {def.enemies.Length} enemies)"));
        }

        /// <summary>
        /// In-place upgrades for the original scenes: ghost retunes (L2 5,
        /// L3 8), passive L1 bat, hearts/crystals/checkpoint at extractor-
        /// picked standable spots, L1 tutorial hints. Tile coords from the
        /// definition are translated back to world space via the scene's own
        /// tilemap bounds (the extractor normalized against them).
        /// </summary>
        static void AugmentLegacyLevel(LevelDefinition def)
        {
            // The legacy scenes carry TWO tilemaps: a completely-full
            // background layer (tiles == bounds area — no clear cells at
            // all) and the real platform layer with the TilemapCollider2D.
            // Only the collider layer represents standable geometry.
            var tilemap = Object.FindObjectsOfType<Tilemap>()
                .OrderByDescending(t => t.GetComponent<TilemapCollider2D>() != null ? 1 : 0)
                .ThenByDescending(t =>
                {
                    t.CompressBounds();
                    int count = 0;
                    foreach (Vector3Int pos in t.cellBounds.allPositionsWithin)
                        if (t.HasTile(pos))
                            count++;
                    return count;
                })
                .FirstOrDefault();
            Vector2Int offset = Vector2Int.zero;
            if (tilemap != null)
            {
                tilemap.CompressBounds();
                offset = new Vector2Int(tilemap.cellBounds.xMin, tilemap.cellBounds.yMin);
            }
            Vector3 LegacyWorld(Vector2Int tile)
            {
                if (tilemap == null)
                    return World(tile);
                Vector3Int cell = new Vector3Int(tile.x + offset.x, tile.y + offset.y, 0);
                return tilemap.layoutGrid.CellToWorld(cell) + new Vector3(0.5f, 0.5f, 0f);
            }

            // Ghost retune: keep an even spread, delete the rest.
            int keep = def.levelNumber == 2 ? 5 : def.levelNumber == 3 ? 8 : int.MaxValue;
            var ghosts = Object.FindObjectsOfType<IndieMarc.Platformer.Ghost>()
                .OrderBy(g => g.transform.position.x).ToList();
            if (ghosts.Count > keep)
            {
                var keepSet = new HashSet<int>();
                for (int i = 0; i < keep; i++)
                    keepSet.Add(Mathf.RoundToInt(i * (ghosts.Count - 1) / (float)(keep - 1)));
                for (int i = ghosts.Count - 1; i >= 0; i--)
                    if (!keepSet.Contains(i))
                        Object.DestroyImmediate(ghosts[i].gameObject);
                Debug.Log($"[LevelBuilder] L{def.levelNumber}: ghosts {ghosts.Count} → {keep}");
            }

            // L1 bat teaches without shooting.
            if (def.levelNumber == 1)
            {
                foreach (var bat in Object.FindObjectsOfType<IndieMarc.Platformer.Bat>())
                    bat.canShoot = false;
            }

            GameObject holder = new GameObject(GenPrefix + "Pickups");
            Sprite heart = FindGrottoSprite("heart");
            foreach (Vector2Int p in def.hearts)
                SpriteEntity<HeartPickup>(holder, "Heart", LegacyWorld(p), heart, 0.8f);
            Sprite starSpriteL = FirstSprite("Assets/Art/Generated/Props/star.png", "star_twinkle_0");
            // The extractor's standable-scan silently produced empty arrays
            // for the legacy levels (their star/heart spots were never
            // authored) — derive positions from the LIVE tilemap instead so
            // every level is guaranteed a collectible star on real ground.
            Vector3? starSpot = def.crystals.Length > 0
                ? LegacyWorld(def.crystals[0])
                : FindStandableWorld(tilemap, 0.55f);
            if (starSpot.HasValue)
                AddCoinSpin(SpriteEntity<Crystal>(holder, "Star", starSpot.Value, starSpriteL, 1.1f));
            else
                Debug.LogError($"[LevelBuilder] L{def.levelNumber}: no star spot found!");

            if (def.hearts.Length == 0 && tilemap != null)
            {
                Sprite heartFallback = FindGrottoSprite("heart");
                foreach (float fx in new[] { 0.3f, 0.75f })
                {
                    Vector3? spot = FindStandableWorld(tilemap, fx);
                    if (spot.HasValue)
                        SpriteEntity<HeartPickup>(holder, "Heart", spot.Value, heartFallback, 0.8f);
                }
            }
            Sprite torchUnlit = FirstSprite("Assets/Art/Generated/Props/checkpoint_torch.png", "checkpoint_torch_unlit_0");
            Sprite torchLit = FirstSprite("Assets/Art/Generated/Props/checkpoint_torch.png", "checkpoint_torch_lit_0");
            foreach (Vector2Int p in def.checkpoints)
            {
                GameObject cp = SpriteEntity<Checkpoint>(holder, "Checkpoint", LegacyWorld(p), torchUnlit, 1.6f);
                var comp = cp.GetComponent<Checkpoint>();
                comp.unlitSprite = torchUnlit;
                comp.litSprite = torchLit;
            }

            if (def.levelNumber == 1)
            {
                GameObject p1 = GameObject.Find("Player1");
                Vector3 basePos = p1 != null ? p1.transform.position : LegacyWorld(def.player1Spawn);
                (string msg, Vector3 at, bool last)[] hints =
                {
                    ("Hold ◀ ▶ to move, tap A to jump\n(tap A again mid-air!)", basePos + new Vector3(2, 0, 0), false),
                    ("Tap ⇄ (or your partner's portrait)\nto control the other hero", basePos + new Vector3(8, 0, 0), false),
                    ("BOTH heroes must reach\na portal to escape!", basePos + new Vector3(14, 0, 0), true),
                };
                foreach (var (msg, at, last) in hints)
                    AddHint(holder, msg, at, false, last);
            }

            AddSceneContextualHints(holder);
        }

        /// <summary>
        /// Places contextual hints next to the ACTUAL instantiated mechanics
        /// in the current scene (used for the legacy levels whose original
        /// instruction text is stripped). Always-show, one per mechanic type.
        /// </summary>
        static void AddSceneContextualHints(GameObject holder)
        {
            var p1Powerup = Object.FindObjectOfType<PowerUpOne>();
            if (p1Powerup != null)
                AddHint(holder, "Red orb: the ember hero can now\ntap B to shoot fireballs!",
                    p1Powerup.transform.position + new Vector3(0, 2, 0), true, false);
            var p2Powerup = Object.FindObjectOfType<PowerUpTwo>();
            if (p2Powerup != null)
                AddHint(holder, "Blue orb: the wisp hero can tap B to\nbecome a platform to boost a partner",
                    p2Powerup.transform.position + new Vector3(0, 2, 0), true, false);
            var lever = Object.FindObjectOfType<IndieMarc.Platformer.Lever>();
            if (lever != null)
                AddHint(holder, "Touch a lever to extend its bridge",
                    lever.transform.position + new Vector3(0, 2, 0), true, false);
            foreach (GameObject lava in GameObject.FindGameObjectsWithTag("LavaSquare"))
            {
                AddHint(holder, "Lava is instant death —\ntime your jumps",
                    lava.transform.position + new Vector3(0, 2.5f, 0), true, false);
                break;
            }
        }

        // ---------------- tilemaps ----------------

        static void BuildTilemaps(LevelDefinition def)
        {
            // The RuleTile's rules resolve solid interiors to its empty
            // default (no collision, dark visual) — paint with the concrete
            // Grotto tiles instead, like the hand-built levels do:
            // grass-top where exposed above, speckled interior elsewhere.
            const string tilesetFolder = "Assets/GrottoEscapeArtwork/Sprites/Tileset";
            var topTile = AssetDatabase.LoadAssetAtPath<TileBase>($"{tilesetFolder}/grotto-tiles_25.asset");
            var fillTile = AssetDatabase.LoadAssetAtPath<TileBase>($"{tilesetFolder}/grotto-tiles_33.asset");
            if (topTile == null || fillTile == null)
                Debug.LogError("[LevelBuilder] grotto tile assets missing");
            var ruleTile = fillTile;

            GameObject gridObj = new GameObject(GenPrefix + "Grid", typeof(Grid));

            // Ground — layer must match the hand-built levels ("Platform"),
            // which is what player physics and ground raycasts collide with.
            Tilemap ground = NewTilemap(gridObj, "Ground");
            int levelLayer = LayerMask.NameToLayer("Platform");
            if (levelLayer >= 0)
                ground.gameObject.layer = levelLayer;
            // Plain per-tile collider — matches the proven legacy levels.
            // (A CompositeCollider2D generated zero paths at runtime here:
            // players fell straight through. Not worth the optimization.)
            ground.gameObject.AddComponent<TilemapCollider2D>();

            // Decoration (non-colliding).
            Tilemap deco = NewTilemap(gridObj, "Decoration");
            deco.GetComponent<TilemapRenderer>().sortingOrder = -1;

            Sprite lavaSprite = FirstSprite("Assets/Art/Generated/Props/lava.png", "lava_glow_0");
            Tile lavaTile = null;
            if (lavaSprite != null)
            {
                lavaTile = ScriptableObject.CreateInstance<Tile>();
                lavaTile.sprite = lavaSprite;
            }
            Tilemap lava = NewTilemap(gridObj, "Lava");
            lava.GetComponent<TilemapRenderer>().sortingOrder = 1;

            var lavaRuns = new List<(int x0, int x1, int y)>();
            int width = def.GridWidth, height = def.GridHeight;
            int runStart = -1;

            for (int y = 0; y < height; y++)
            {
                runStart = -1;
                for (int x = 0; x <= width; x++)
                {
                    char c = x < width ? def.TileAt(x, y) : '.';
                    if (c == '#')
                    {
                        bool exposedAbove = def.TileAt(x, y + 1) != '#';
                        ground.SetTile(new Vector3Int(x, y, 0),
                            exposedAbove ? topTile : fillTile);
                    }
                    else if (c == 'D' && ruleTile != null)
                        deco.SetTile(new Vector3Int(x, y, 0), ruleTile);

                    if (c == 'L')
                    {
                        if (lavaTile != null)
                            lava.SetTile(new Vector3Int(x, y, 0), lavaTile);
                        if (runStart < 0)
                            runStart = x;
                    }
                    else if (runStart >= 0)
                    {
                        lavaRuns.Add((runStart, x - 1, y));
                        runStart = -1;
                    }
                }
            }

            // One trigger volume per contiguous lava run (LavaSquare-compatible).
            foreach (var run in lavaRuns)
            {
                GameObject vol = new GameObject(GenPrefix + $"LavaVolume_{run.x0}_{run.y}");
                vol.tag = "LavaSquare";
                var box = vol.AddComponent<BoxCollider2D>();
                box.isTrigger = false;   // players die via collision tag check
                float w = run.x1 - run.x0 + 1;
                vol.transform.position = new Vector3(run.x0 + w / 2f, run.y + 0.4f, 0);
                box.size = new Vector2(w, 0.8f);
            }
        }

        static Tilemap NewTilemap(GameObject grid, string name)
        {
            GameObject obj = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            obj.transform.SetParent(grid.transform, false);
            return obj.GetComponent<Tilemap>();
        }

        // ---------------- entities ----------------

        static Vector3 World(Vector2Int tile) { return new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f); }

        static void BuildEntities(LevelDefinition def)
        {
            GameObject holder = new GameObject(GenPrefix + "Entities");

            Place(Player1Prefab, World(def.player1Spawn), holder, "Player1", asPrefab: true, reuseExisting: true);
            Place(Player2Prefab, World(def.player2Spawn), holder, "Player2", asPrefab: true, reuseExisting: true);
            NormalizeSpriteHeight(Place(ExitPrefab, World(def.exit1), holder, "ExitPortal1", asPrefab: true), 2.0f, resizeCollider: false);
            NormalizeSpriteHeight(Place(ExitPrefab, World(def.exit2), holder, "ExitPortal2", asPrefab: true), 2.0f, resizeCollider: false);

            foreach (var e in def.enemies)
                PlaceEnemy(e, holder);

            foreach (var group in def.leverGroups)
            {
                var bridges = new List<IndieMarc.Platformer.Bridge>();
                foreach (Vector2Int tile in group.bridgeTiles)
                {
                    GameObject bridge = Place(BridgePrefab, World(tile), holder, $"Bridge_{tile.x}_{tile.y}", asPrefab: true);
                    if (bridge != null && bridge.TryGetComponent(out IndieMarc.Platformer.Bridge b))
                        bridges.Add(b);
                }
                GameObject lever = Place(LeverPrefab, World(group.lever), holder, $"Lever_{group.lever.x}", asPrefab: true);
                if (lever != null && lever.TryGetComponent(out IndieMarc.Platformer.Lever l))
                {
                    var so = new SerializedObject(l);
                    var targets = so.FindProperty("targets");
                    targets.arraySize = bridges.Count;
                    for (int i = 0; i < bridges.Count; i++)
                        targets.GetArrayElementAtIndex(i).objectReferenceValue = bridges[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            foreach (Vector2Int p in def.powerUpOne)
                NormalizeSpriteHeight(Place(PowerUp1Prefab, World(p), holder, "PowerUpOne", asPrefab: true), 1.4f);
            foreach (Vector2Int p in def.powerUpTwo)
                NormalizeSpriteHeight(Place(PowerUp2Prefab, World(p), holder, "PowerUpTwo", asPrefab: true), 1.4f);

            Sprite heart = FindGrottoSprite("heart");
            foreach (Vector2Int p in def.hearts)
                SpriteEntity<HeartPickup>(holder, "Heart", World(p), heart, 0.8f);

            Sprite starSprite = FirstSprite("Assets/Art/Generated/Props/star.png", "star_twinkle_0");
            // Mario-style: exactly ONE star per level (first authored spot).
            if (def.crystals.Length > 0)
                AddCoinSpin(SpriteEntity<Crystal>(holder, "Star", World(def.crystals[0]), starSprite, 1.1f));

            Sprite torchUnlit = FirstSprite("Assets/Art/Generated/Props/checkpoint_torch.png", "checkpoint_torch_unlit_0");
            Sprite torchLit = FirstSprite("Assets/Art/Generated/Props/checkpoint_torch.png", "checkpoint_torch_lit_0");
            foreach (Vector2Int p in def.checkpoints)
            {
                GameObject cp = SpriteEntity<Checkpoint>(holder, "Checkpoint", World(p), torchUnlit, 1.6f);
                var comp = cp.GetComponent<Checkpoint>();
                comp.unlitSprite = torchUnlit;
                comp.litSprite = torchLit;
            }

            BuildHints(def, holder);

            Sprite bridgeArt = FirstSprite("Assets/Art/Generated/Props/bridge.png", "bridge_extend_2");
            foreach (var mp in def.movingPlatforms)
            {
                GameObject platform = new GameObject(GenPrefix + (mp.isLavaRiserRide ? "RideElevator" : "MovingPlatform"));
                platform.transform.SetParent(holder.transform, false);
                platform.transform.position = World(mp.from);
                var renderer = platform.AddComponent<SpriteRenderer>();
                renderer.sprite = bridgeArt;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = new Vector2(mp.isLavaRiserRide ? 4f : 3f, 1f);
                var box = platform.AddComponent<BoxCollider2D>();
                box.size = renderer.size;
                platform.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                var mover = platform.AddComponent<MovingPlatform>();
                mover.from = World(mp.from);
                mover.to = World(mp.to);
                mover.speed = mp.speed > 0.01f ? mp.speed : 2f;
                if (mp.isLavaRiserRide)
                {
                    // Ride waits for the chase trigger, then goes up ONCE.
                    mover.active = false;
                    mover.oneWay = true;
                }

                int platformLayer = LayerMask.NameToLayer("Platform");
                if (platformLayer >= 0)
                    platform.layer = platformLayer;

                if (mp.isLavaRiserRide)
                {
                    // Riser volume spanning the shaft bottom + its trigger.
                    Sprite lavaArt = FirstSprite("Assets/Art/Generated/Props/lava.png", "lava_glow_0");
                    GameObject riserObj = new GameObject(GenPrefix + "LavaRiser");
                    riserObj.transform.SetParent(holder.transform, false);
                    riserObj.transform.position = World(mp.from) + Vector3.down * 3f;
                    var riserSprite = riserObj.AddComponent<SpriteRenderer>();
                    riserSprite.sprite = lavaArt;
                    riserSprite.drawMode = SpriteDrawMode.Tiled;
                    riserSprite.size = new Vector2(def.GridWidth, 2f);
                    var riserBox = riserObj.AddComponent<BoxCollider2D>();
                    riserBox.size = riserSprite.size;
                    var riser = riserObj.AddComponent<LavaRiser>();

                    GameObject trigger = new GameObject(GenPrefix + "LavaRiserTrigger");
                    trigger.transform.SetParent(holder.transform, false);
                    trigger.transform.position = World(mp.from) + Vector3.up * 1.5f;
                    var triggerBox = trigger.AddComponent<BoxCollider2D>();
                    triggerBox.size = new Vector2(6f, 3f);
                    var riserTrigger = trigger.AddComponent<LavaRiserTrigger>();
                    riserTrigger.riser = riser;
                    riserTrigger.ride = mover;
                }
            }
        }

        /// <summary>
        /// L1 gets the gated first-launch onboarding chain; every level gets
        /// contextual "always show" bubbles next to its mechanics so the
        /// player is never surprised by a new hazard.
        /// </summary>
        static void BuildHints(LevelDefinition def, GameObject holder)
        {
            if (def.levelNumber == 1)
            {
                AddHint(holder, "Hold ◀ ▶ to move, tap A to jump\n(tap A again mid-air!)",
                    World(def.player1Spawn) + new Vector3(2, 0, 0), false, false);
                AddHint(holder, "Tap ⇄ (or your partner's portrait)\nto control the other hero",
                    World(def.player1Spawn) + new Vector3(8, 0, 0), false, false);
                AddHint(holder, "BOTH heroes must reach\na portal to escape!",
                    World(def.exit1) + new Vector3(-6, 0, 0), false, true);
            }

            // Contextual hints (always shown) placed at each mechanic's first
            // instance. Kept short so they read on a phone.
            if (def.powerUpOne.Length > 0)
                AddHint(holder, "Red orb: the ember hero can now\ntap B to shoot fireballs!",
                    World(def.powerUpOne[0]) + new Vector3(0, 2, 0), true, false);
            if (def.powerUpTwo.Length > 0)
                AddHint(holder, "Blue orb: the wisp hero can tap B to\nbecome a platform to boost a partner",
                    World(def.powerUpTwo[0]) + new Vector3(0, 2, 0), true, false);
            if (def.leverGroups.Length > 0)
                AddHint(holder, "Touch a lever to extend its bridge",
                    World(def.leverGroups[0].lever) + new Vector3(0, 2, 0), true, false);
            foreach (var e in def.enemies)
            {
                if (e.type == LevelDefinition.EnemyType.Skeleton)
                { AddHint(holder, "Skeletons hit hard —\nshoot them or slip past", World(e.position) + new Vector3(-3, 2, 0), true, false); break; }
            }
            foreach (var e in def.enemies)
            {
                if (e.type == LevelDefinition.EnemyType.Eye)
                { AddHint(holder, "Eyes chase the active hero.\nSwap to split their attention", World(e.position) + new Vector3(-3, 2, 0), true, false); break; }
            }
            if (def.movingPlatforms.Length > 0)
            {
                var mp = def.movingPlatforms[0];
                AddHint(holder, mp.isLavaRiserRide
                        ? "Ride up together — the lava rises!\nHit levers on time to stay ahead"
                        : "Ride the moving platform across",
                    World(mp.from) + new Vector3(0, 2, 0), true, false);
            }
            // Lava warning at the first lava run.
            for (int y = 0; y < def.GridHeight; y++)
                for (int x = 0; x < def.GridWidth; x++)
                    if (def.TileAt(x, y) == 'L')
                    {
                        AddHint(holder, "Lava is instant death —\ntime your jumps",
                            new Vector3(x + 0.5f, y + 2.5f, 0), true, false);
                        return;
                    }
        }

        static void AddHint(GameObject holder, string msg, Vector3 pos, bool always, bool marksSeen)
        {
            GameObject hint = new GameObject(GenPrefix + "Hint");
            hint.transform.SetParent(holder.transform, false);
            hint.transform.position = pos;
            var box = hint.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(3f, 4f);
            var tut = hint.AddComponent<TutorialHint>();
            tut.message = msg;
            tut.alwaysShow = always;
            tut.marksTutorialSeen = marksSeen;
        }

        /// <summary>
        /// Standable spot from live tilemap geometry: the first cell (searching
        /// down from 60% height at the given width fraction, then nearby
        /// columns) whose cell below has a tile and which is itself clear,
        /// returned as a world position 1.5 tiles above the ground surface.
        /// </summary>
        static Vector3? FindStandableWorld(Tilemap tilemap, float fractionX)
        {
            if (tilemap == null)
                return null;
            tilemap.CompressBounds();
            BoundsInt b = tilemap.cellBounds;
            int targetX = b.xMin + Mathf.RoundToInt(b.size.x * fractionX);
            for (int dx = 0; dx < b.size.x / 2; dx++)
            {
                foreach (int x in new[] { targetX + dx, targetX - dx })
                {
                    if (x <= b.xMin || x >= b.xMax - 1)
                        continue;
                    for (int y = b.yMax - 2; y > b.yMin; y--)
                    {
                        bool groundBelow = tilemap.HasTile(new Vector3Int(x, y - 1, 0));
                        bool clearHere = !tilemap.HasTile(new Vector3Int(x, y, 0)) &&
                                         !tilemap.HasTile(new Vector3Int(x, y + 1, 0));
                        if (groundBelow && clearHere)
                            return tilemap.layoutGrid.CellToWorld(new Vector3Int(x, y, 0)) +
                                   new Vector3(0.5f, 1.0f, 0f);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Rescales an object so its sprite renders at targetHeight world
        /// units, and resizes its BoxCollider2D to roughly match the body.
        /// </summary>
        static void AddCoinSpin(GameObject coin)
        {
            if (coin == null)
                return;
            var frames = AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/Art/Generated/Props/star.png")
                .OfType<Sprite>().OrderBy(sp => sp.name).ToArray();
            if (frames.Length < 2)
                return;
            var cycler = coin.AddComponent<SpriteCycler>();
            cycler.frames = frames;
            cycler.fps = 8f;
        }

        static void NormalizeSpriteHeight(GameObject go, float targetHeight, bool resizeCollider = true)
        {
            if (go == null)
                return;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null)
                return;
            float spriteH = sr.sprite.bounds.size.y;   // world units at scale 1
            if (spriteH < 0.001f)
                return;
            float scale = targetHeight / spriteH;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            // Collider is in local units; a ~0.7x0.8 box on the normalized
            // body keeps hitboxes fair and consistent.
            if (resizeCollider)
            {
                var box = go.GetComponent<BoxCollider2D>();
                if (box != null)
                {
                    Vector2 local = sr.sprite.bounds.size;
                    box.size = new Vector2(local.x * 0.7f, local.y * 0.8f);
                    box.offset = Vector2.zero;
                }
            }
        }

        static void PlaceEnemy(LevelDefinition.EnemyPlacement e, GameObject holder)
        {
            if (!EnemyPrefabs.TryGetValue(e.type, out string path))
                return;
            GameObject enemy = Place(path, World(e.position), holder, GenPrefix + e.type, asPrefab: true);
            if (enemy == null)
                return;

            // The pack prefabs carry wildly inconsistent scales (Slime x50 =
            // giant, Eye/Frog x1 = invisible). Normalize every enemy to a
            // uniform ~1.2-unit height regardless of prefab scale/PPU.
            NormalizeSpriteHeight(enemy, e.type == LevelDefinition.EnemyType.Slime ? 1.1f : 1.25f);

            switch (e.type)
            {
                case LevelDefinition.EnemyType.BatPassive:
                    var bat = enemy.GetComponent<IndieMarc.Platformer.Bat>();
                    if (bat != null) bat.canShoot = false;
                    break;
                case LevelDefinition.EnemyType.Slime:
                    Retrofit<SlimeEnemy>(enemy, addPatrol: true, e.patrolSpan);
                    break;
                case LevelDefinition.EnemyType.Skeleton:
                    Retrofit<SkeletonEnemy>(enemy, addPatrol: true, e.patrolSpan);
                    break;
                case LevelDefinition.EnemyType.Eye:
                    Retrofit<EyeEnemy>(enemy, addPatrol: false, 0f);
                    break;
                case LevelDefinition.EnemyType.Frog:
                    Retrofit<FrogEnemy>(enemy, addPatrol: false, 0f);
                    break;
            }
            if (e.moveRange > 0.01f)
            {
                var batAny = enemy.GetComponent<IndieMarc.Platformer.Bat>();
                if (batAny != null) batAny.moveRange = e.moveRange;
                var ghost = enemy.GetComponent<IndieMarc.Platformer.Ghost>();
                if (ghost != null) ghost.moveRange = e.moveRange;
            }
        }

        static void Retrofit<T>(GameObject enemy, bool addPatrol, float span) where T : Component
        {
            // Strip any legacy behavior scripts that came with the pack prefab.
            foreach (MonoBehaviour mb in enemy.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName != typeof(T).Name && typeName != "GroundPatrol" &&
                    mb.GetType().Namespace != "UnityEngine")
                {
                    // keep animators etc. — only strip other enemy brains
                    if (mb is IDamageableEnemy || typeName.Contains("Movement") || typeName.Contains("Behavior"))
                        Object.DestroyImmediate(mb);
                }
            }
            if (addPatrol && enemy.GetComponent<GroundPatrol>() == null)
            {
                var patrol = enemy.AddComponent<GroundPatrol>();
                patrol.spanLimit = span > 0.01f ? span : 0f;
                var rb = enemy.GetComponent<Rigidbody2D>();
                if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
            }
            if (enemy.GetComponent<T>() == null)
                enemy.AddComponent<T>();
        }

        static GameObject SpriteEntity<T>(GameObject holder, string name, Vector3 pos, Sprite sprite,
                                          float targetHeight = 1.0f)
            where T : Component
        {
            GameObject obj = new GameObject(GenPrefix + name);
            obj.transform.SetParent(holder.transform, false);
            obj.transform.position = pos;
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 2;
            // Grotto item sprites are 16px @100 PPU (0.16 world units) — scale
            // them up so pickups read at ~1 tile like everything else.
            if (sprite != null && sprite.bounds.size.y > 0.001f)
            {
                float scale = targetHeight / sprite.bounds.size.y;
                obj.transform.localScale = new Vector3(scale, scale, 1f);
            }
            var box = obj.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            if (sprite != null)
                box.size = sprite.bounds.size;
            obj.AddComponent<T>();
            return obj;
        }

        static GameObject Place(string prefabPath, Vector3 pos, GameObject holder, string name,
                                bool asPrefab, bool reuseExisting = false)
        {
            if (reuseExisting)
            {
                GameObject existing = GameObject.Find(name);
                if (existing != null)
                {
                    existing.transform.position = pos;
                    return existing;
                }
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[LevelBuilder] prefab missing: {prefabPath}");
                return null;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(holder.transform, false);
            instance.transform.position = pos;
            return instance;
        }

        // ---------------- camera / lights / parallax / systems ----------------

        static void BuildCamera(LevelDefinition def)
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
            cam.backgroundColor = def.zone == LevelDefinition.Zone.Depths
                ? new Color(0.10f, 0.03f, 0.04f)
                : new Color(0.01f, 0.09f, 0.13f);
            // Remove the legacy hand-rolled camera controller if present.
            foreach (MonoBehaviour mb in cam.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == "DynamicZoomCamera")
                    Object.DestroyImmediate(mb);
            var brain = cam.GetComponent<CinemachineBrain>();
            if (brain == null)
                cam.gameObject.AddComponent<CinemachineBrain>();

            GameObject rig = new GameObject(GenPrefix + "CameraRig");

            var groupObj = new GameObject("TargetGroup");
            groupObj.transform.SetParent(rig.transform, false);
            var group = groupObj.AddComponent<CinemachineTargetGroup>();
            GameObject p1 = GameObject.Find("Player1");
            GameObject p2 = GameObject.Find("Player2");
            if (p1 != null) group.AddMember(p1.transform, 1f, 2f);
            if (p2 != null) group.AddMember(p2.transform, 1f, 2f);

            var vcamObj = new GameObject("VCam");
            vcamObj.transform.SetParent(rig.transform, false);
            var vcam = vcamObj.AddComponent<CinemachineVirtualCamera>();
            vcam.Follow = groupObj.transform;
            vcam.m_Lens.OrthographicSize = 8f;
            var transposer = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
            transposer.m_XDamping = 1f;
            transposer.m_YDamping = 1f;
            transposer.m_GroupFramingMode = CinemachineFramingTransposer.FramingMode.HorizontalAndVertical;
            transposer.m_MinimumOrthoSize = 7f;
            transposer.m_MaximumOrthoSize = 11.25f;

            var boundsObj = new GameObject("CameraBounds");
            boundsObj.transform.SetParent(rig.transform, false);
            var poly = boundsObj.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            Vector2 min = new Vector2(def.boundsMin.x, def.boundsMin.y);
            Vector2 max = new Vector2(def.boundsMax.x, def.boundsMax.y);
            if (max.x - min.x < 1f) { min = Vector2.zero; max = new Vector2(def.GridWidth, def.GridHeight); }
            poly.points = new[]
            {
                new Vector2(min.x, min.y), new Vector2(max.x, min.y),
                new Vector2(max.x, max.y), new Vector2(min.x, max.y),
            };

            var confiner = vcamObj.AddComponent<CinemachineConfiner2D>();
            confiner.m_BoundingShape2D = poly;
        }

        static void BuildLighting(LevelDefinition def)
        {
            // Remove leftover 3D lights/volumes.
            foreach (Light light in Object.FindObjectsOfType<Light>())
                Object.DestroyImmediate(light.gameObject);

            GameObject holder = new GameObject(GenPrefix + "Lights");
            var globalObj = new GameObject("GlobalLight");
            globalObj.transform.SetParent(holder.transform, false);
            var global = globalObj.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 0.95f;
            global.color = def.zone == LevelDefinition.Zone.Depths
                ? new Color(1f, 0.88f, 0.82f)
                : new Color(0.95f, 1f, 0.98f);

            foreach (Vector2Int c in def.crystals)
                PointLight(holder, World(c), new Color(0.3f, 0.9f, 0.9f), 3f, 0.8f);
            PointLight(holder, World(def.exit1), new Color(0.4f, 1f, 0.6f), 4f, 1f);
            PointLight(holder, World(def.exit2), new Color(0.4f, 1f, 0.6f), 4f, 1f);
            foreach (Vector2Int p in def.checkpoints)
                PointLight(holder, World(p) + Vector3.up, new Color(1f, 0.7f, 0.3f), 3f, 0.9f);
        }

        static void PointLight(GameObject holder, Vector3 pos, Color color, float radius, float intensity)
        {
            var obj = new GameObject("PointLight");
            obj.transform.SetParent(holder.transform, false);
            obj.transform.position = pos;
            var light = obj.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightOuterRadius = radius;
        }

        static void BuildParallax(LevelDefinition def)
        {
            // One SEAMLESS tiled layer per depth, each sized to cover the
            // whole level plus margin. The old approach placed discrete
            // background tiles at a fixed height — they showed as a "floating
            // map strip" that scrolled. A single tiled renderer has no seams
            // and no floating band.
            string zone = def.zone == LevelDefinition.Zone.Depths ? "depths" : "grotto";
            GameObject holder = new GameObject(GenPrefix + "Parallax");
            (string layer, float factor, float z)[] layers =
            {
                ("far", 0.92f, 40f), ("near", 0.75f, 25f),
            };
            float coverW = def.GridWidth + 60f;
            float coverH = def.GridHeight + 40f;
            Vector3 center = new Vector3(def.GridWidth / 2f, def.GridHeight / 2f, 0f);

            foreach (var (layer, factor, z) in layers)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    $"Assets/Art/Generated/Backgrounds/{zone}_{layer}.png");
                if (sprite == null)
                    continue;
                var obj = new GameObject(GenPrefix + "BG_" + layer);
                obj.transform.SetParent(holder.transform, false);
                obj.transform.position = new Vector3(center.x, center.y, z);
                var renderer = obj.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(coverW, coverH);
                renderer.sortingOrder = -20 - (int)z;
                var parallax = obj.AddComponent<ParallaxLayer>();
                parallax.factor = factor;
            }
        }

        static void EnsureSystems(LevelDefinition def)
        {
            if (Object.FindObjectOfType<GameState>() == null)
            {
                GameObject gm = new GameObject("GameManager");
                gm.AddComponent<GameState>();
                // Overlay canvas fields stay null — GameState null-checks all
                // overlays; Phase 5's GameplayUI carries the visible feedback.
            }
            if (Object.FindObjectOfType<SwapModeController>() == null)
                new GameObject("SwapModeController", typeof(SwapModeController));
            if (Object.FindObjectOfType<LevelMusic>() == null)
                new GameObject("LevelMusic", typeof(LevelMusic));
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));

            InstantiateUiPrefab("Assets/Prefabs/TouchControls.prefab", "TouchControls");
            InstantiateUiPrefab("Assets/Prefabs/GameplayUI.prefab", "GameplayUI");
        }

        static void InstantiateUiPrefab(string path, string name)
        {
            if (GameObject.Find(name) != null)
                return;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[LevelBuilder] UI prefab missing: {path}");
                return;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
        }

        static void UpdateBuildSettings(List<int> levelNumbers)
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene("Assets/Scenes/Menu Scene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Story Details.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Level Scene.unity", true),
            };
            foreach (int n in levelNumbers.OrderBy(n => n))
                scenes.Add(new EditorBuildSettingsScene($"Assets/Scenes/Level {n}.unity", true));
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/MoreLore.unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[LevelBuilder] Build Settings now lists {scenes.Count} scenes");
        }

        // ---------------- shared helpers ----------------

        internal static Sprite FirstSprite(string path, string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .FirstOrDefault(s => s.name == name);
        }

        internal static Sprite FindGrottoSprite(string name)
        {
            string[] guids = AssetDatabase.FindAssets($"{name} t:Sprite",
                new[] { "Assets/GrottoEscapeArtwork/Sprites" });
            foreach (string guid in guids)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                if (sprite != null)
                    return sprite;
            }
            return null;
        }
    }
}
