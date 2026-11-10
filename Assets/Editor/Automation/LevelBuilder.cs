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

        // Draw-order contract, ALL on the Default sorting layer:
        // parallax BG -60/-45, deco tilemap -1, terrain/art tilemap
        // 0, doors + decorative props 1, pickups/stars/hearts/checkpoints 2.
        // Doors sit ONE below the pickups so collectibles always read on
        // top of a doorway.
        const int PickupSortingOrder = 2;
        const int DoorSortingOrder = PickupSortingOrder - 1;

        // Prefab paths.
        const string Player1Prefab = "Assets/IndieMarc/PlatformerDemo/Player1.prefab";
        const string Player2Prefab = "Assets/IndieMarc/PlatformerDemo/Player2.prefab";
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
                bool solvable = LevelSolvability.Check(def);
                if (!solvable)
                    Debug.LogError($"[LevelBuilder] Level {def.levelNumber} FAILED solvability!");
            }

            UpdateBuildSettings(defs.Select(d => d.levelNumber).ToList());
            Debug.Log($"[LevelBuilder] built {defs.Count} levels");
        }

        public static void BuildLevel(LevelDefinition def, string scenePath)
        {
            Scene scene = System.IO.File.Exists(scenePath)
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Clear previously generated objects (idempotency).
            foreach (GameObject root in scene.GetRootGameObjects().ToArray())
                if (root.name.StartsWith(GenPrefix))
                    Object.DestroyImmediate(root);

            BuildTilemaps(def);
            BuildEntities(def);
            BuildCamera(def);
            BuildLighting(def);
            BuildParallax(def);
            EnsureSystems(def);

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[LevelBuilder] saved {scenePath} " +
                      $"({def.GridWidth}x{def.GridHeight} tiles, {def.enemies.Length} enemies)");
        }

        // ---------------- exit doors ----------------

        const string DoorEmberSheet = "Assets/Art/Generated/Props/door_ember.png";
        const string DoorWispSheet = "Assets/Art/Generated/Props/door_wisp.png";

        /// <summary>
        /// Generated levels: drop the doorway to the floor — the authored
        /// exit tile may sit above the actual ground. Returns the center of
        /// a 2-tile-tall door whose bottom rests on the floor.
        /// </summary>
        static Vector3 DoorWorld(LevelDefinition def, Vector2Int exit)
        {
            int y = exit.y;
            while (y > 0 && def.TileAt(exit.x, y - 1) != '#')
                y--;
            return new Vector3(exit.x + 0.5f, y + 1f, 0f);
        }

        /// <summary>
        /// Hero-matched exit door: heroNumber 1 = EMBER (door_ember), 2 =
        /// WISP (door_wisp); frames per the ExitDoor.cs editor-pass contract
        /// (closed_0 / half_0..1 / open_0..3). ~1x2 tiles with a trigger
        /// covering the doorway down to the floor — ExitDoor occupancy tests
        /// the hero's PIVOT via OverlapPoint, so a short box would never fire.
        /// </summary>
        internal static GameObject PlaceExitDoor(GameObject holder, string name, Vector3 pos, int heroNumber)
        {
            string sheet = heroNumber == 1 ? DoorEmberSheet : DoorWispSheet;
            string baseName = System.IO.Path.GetFileNameWithoutExtension(sheet);

            GameObject door = new GameObject(name);
            door.transform.SetParent(holder.transform, false);
            door.transform.position = pos;

            var renderer = door.AddComponent<SpriteRenderer>();
            Sprite closed = FirstSprite(sheet, baseName + "_closed_0");
            // Serialize the hero-COLOURED half-lit frame as the resting
            // sprite: ExitDoor pulses these dimmed at runtime anyway, and the
            // colourless sealed arch would show in edit-mode captures and the
            // first rendered frame (it read as scenery — playtest 6).
            renderer.sprite = FirstSprite(sheet, baseName + "_half_0") ?? closed;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = DoorSortingOrder;   // above terrain art (0), below pickups (2)
            // Hero-hue tint: the closed arch frame is near-colourless, so
            // without this the resting door reads as scenery. The lit/open
            // frames carry strong hero colour already — the mild multiply
            // shifts them imperceptibly.
            renderer.color = heroNumber == 1
                ? new Color(1f, 0.80f, 0.68f)     // EMBER: warm red-amber cast
                : new Color(0.66f, 0.86f, 1f);    // WISP: cool blue cast

            // 16x32 @ 16 PPU is exactly 1x2 tiles at scale 1 — normalize
            // anyway in case the art resolution changes again.
            if (closed != null && closed.bounds.size.y > 0.001f)
            {
                float scale = 2f / closed.bounds.size.y;
                door.transform.localScale = new Vector3(scale, scale, 1f);
            }

            var box = door.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = closed != null ? (Vector2)closed.bounds.size : new Vector2(1f, 2f);

            var exit = door.AddComponent<ExitDoor>();
            exit.heroNumber = heroNumber;
            exit.closedFrames = DoorFrames(sheet, baseName, "closed_0");
            exit.halfLitFrames = DoorFrames(sheet, baseName, "half_0", "half_1");
            exit.openFrames = DoorFrames(sheet, baseName, "open_0", "open_1", "open_2", "open_3");
            exit.fps = 6f;
            if (closed == null)
                Debug.LogWarning($"[LevelBuilder] {baseName} sprites missing — door frames stay empty until the sheet imports");

            // Hero-colored glow ON the door: the closed frames are dark,
            // near-colorless arches (playtest 6: they read as background
            // decoration). A warm/cool Light2D makes each doorway read as
            // EMBER's or WISP's. Lives as a child so both the legacy and
            // generated paths carry it and it dies with the door on rebuild.
            // (Replaces BuildLighting's old exit lights, which used authored
            // TILE coords — meaningless world positions in legacy scenes.)
            var glowObj = new GameObject("DoorGlow");
            glowObj.transform.SetParent(door.transform, false);
            glowObj.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            var glow = glowObj.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = heroNumber == 1 ? new Color(1f, 0.55f, 0.3f) : new Color(0.3f, 0.7f, 1f);
            glow.intensity = 1.1f;
            glow.pointLightOuterRadius = 3.5f;

            Debug.Log($"[DoorPass] {name} hero {heroNumber} at {pos} (layer Default, order {DoorSortingOrder}, {(heroNumber == 1 ? "ember" : "wisp")} glow)");
            return door;
        }

        static Sprite[] DoorFrames(string sheet, string baseName, params string[] frames)
        {
            return frames.Select(f => FirstSprite(sheet, baseName + "_" + f))
                .Where(s => s != null).ToArray();
        }

        // ---------------- tilemaps ----------------

        static void BuildTilemaps(LevelDefinition def)
        {
            // The RuleTile's rules resolve solid interiors to its empty
            // default (no collision, dark visual) — paint with the concrete
            // Grotto tiles instead, like the hand-built levels do.
            // Playtest 6: single top tile + single fill tile read monotonous,
            // so paint the full blue terrain family (indices verified against
            // grotto-tiles.png edge-brightness): 24/25/26/27 = lit-top row
            // (24 left cap, 26 crystal variant, 27 right cap); 32/35 = lit
            // left/right side fills; 0/1/2/3 = lit-bottom ceiling row for
            // platform undersides (0 left, 3 right); 9 plain interior with
            // 10 (chevron), 11/34 (speckle), 33 (crystal) variants scattered
            // by a deterministic cell-coordinate hash — NO randomness.
            const string tilesetFolder = "Assets/GrottoEscapeArtwork/Sprites/Tileset";
            TileBase T(int i) { return AssetDatabase.LoadAssetAtPath<TileBase>($"{tilesetFolder}/grotto-tiles_{i}.asset"); }
            var topTile = T(25);
            var fillTile = T(33);
            if (topTile == null || fillTile == null)
                Debug.LogError("[LevelBuilder] grotto tile assets missing");
            var ruleTile = fillTile;

            TileBase topLeftCap = T(24), topCrystal = T(26), topRightCap = T(27);
            TileBase sideLeft = T(32), sideRight = T(35);
            TileBase ceilLeft = T(0), ceilA = T(1), ceilB = T(2), ceilRight = T(3);
            TileBase fillPlain = T(9), fillChevron = T(10), fillSpeck = T(11), fillSpeck2 = T(34);
            bool familyOk = topTile != null && fillTile != null &&
                topLeftCap != null && topCrystal != null && topRightCap != null &&
                sideLeft != null && sideRight != null &&
                ceilLeft != null && ceilA != null && ceilB != null && ceilRight != null &&
                fillPlain != null && fillChevron != null && fillSpeck != null && fillSpeck2 != null;
            if (!familyOk)
                Debug.LogError("[PlatformArt] terrain tile family incomplete — falling back to flat 25/33 painting");

            TileBase PickTerrainTile(int cx, int cy)
            {
                bool solidUp = def.TileAt(cx, cy + 1) == '#';
                if (!familyOk)
                    return solidUp ? fillTile : topTile;
                bool solidL = def.TileAt(cx - 1, cy) == '#';
                bool solidR = def.TileAt(cx + 1, cy) == '#';
                // y == 0 is bedrock: the world floor is never a "ceiling".
                bool solidDown = cy == 0 || def.TileAt(cx, cy - 1) == '#';
                if (!solidUp)
                {
                    if (!solidL && solidR) return topLeftCap;
                    if (solidL && !solidR) return topRightCap;
                    if (!solidL && !solidR) return topTile;                        // 1-wide pillar
                    return CellHash(cx, cy) % 7 == 0 ? topCrystal : topTile;       // highlight top row
                }
                if (!solidDown)
                {
                    if (!solidL && solidR) return ceilLeft;
                    if (solidL && !solidR) return ceilRight;
                    return CellHash(cx, cy) % 2 == 0 ? ceilA : ceilB;              // platform underside
                }
                if (!solidL) return sideLeft;
                if (!solidR) return sideRight;
                int roll = CellHash(cx, cy) % 100;                                  // interior scatter
                if (roll < 6) return fillChevron;
                if (roll < 14) return fillSpeck;
                if (roll < 22) return fillSpeck2;
                if (roll < 26) return fillTile;                                     // rare crystal
                return fillPlain;
            }

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

            TileBase lavaTile = EnsureLavaTile();
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
                        ground.SetTile(new Vector3Int(x, y, 0), PickTerrainTile(x, y));
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

            BuildGeneratedProps(def);
        }

        /// <summary>
        /// Deterministic cell hash for tile-variant / prop scatter. Pure
        /// function of the coordinates so every rebuild paints identically.
        /// </summary>
        static int CellHash(int x, int y)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663;
                h ^= h >> 13;
                h *= 83492791;
                h ^= h >> 7;
                return h & 0x7fffffff;
            }
        }

        static readonly string[] DecorPropSprites =
            { "crystal-01", "crystal-02", "crystal-03", "crystal-04" };

        /// <summary>
        /// Sparse non-colliding Grotto props on standable surfaces of the
        /// generated levels (hash-gated ~7% of eligible cells; never within
        /// 2 tiles of a door or spawn so gameplay landmarks stay clean).
        /// </summary>
        static void BuildGeneratedProps(LevelDefinition def)
        {
            GameObject holder = new GameObject(GenPrefix + "Props");
            var keepClear = new List<Vector2Int>
                { def.exit1, def.exit2, def.player1Spawn, def.player2Spawn };
            // The real doorways may sit lower than the authored exit tiles
            // (DoorWorld drops them to the floor) — keep those cells clear too.
            foreach (Vector2Int exit in new[] { def.exit1, def.exit2 })
                keepClear.Add(new Vector2Int(exit.x, Mathf.RoundToInt(DoorWorld(def, exit).y - 1f)));
            int placed = 0;
            for (int x = 0; x < def.GridWidth; x++)
                for (int y = 0; y + 1 < def.GridHeight; y++)
                {
                    if (def.TileAt(x, y) != '#' || def.TileAt(x, y + 1) != '.' ||
                        def.TileAt(x, y + 2) == '#')
                        continue;
                    if (CellHash(x * 5 + 3, y * 7 + 1) % 100 >= 7)
                        continue;
                    int px = x, py = y;
                    if (keepClear.Any(k => Mathf.Abs(k.x - px) <= 2 && Mathf.Abs(k.y - (py + 1)) <= 2))
                        continue;
                    Sprite sprite = FindGrottoSprite(DecorPropSprites[CellHash(x, y) % DecorPropSprites.Length]);
                    if (sprite == null)
                        continue;
                    PlaceDecorProp(holder, new Vector3(x + 0.5f, y + 1f, 0f), sprite, $"{GenPrefix}Prop_{x}_{y}");
                    placed++;
                }
            Debug.Log($"[PlatformArt] L{def.levelNumber}: {placed} decorative props (non-colliding, Default order {DoorSortingOrder})");
        }

        /// <summary>
        /// One decorative prop with its feet on the given floor point.
        /// Behind gameplay: order sits below pickups; doors share the order
        /// but props are excluded from doorway cells, so they never overlap.
        /// No collider — purely visual.
        /// </summary>
        static void PlaceDecorProp(GameObject holder, Vector3 floorAt, Sprite sprite, string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(holder.transform, false);
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = DoorSortingOrder;
            float spriteH = sprite.bounds.size.y;
            float scale = spriteH > 0.001f ? 0.55f / spriteH : 1f;
            obj.transform.localScale = new Vector3(scale, scale, 1f);
            obj.transform.position = floorAt + new Vector3(0f, 0.275f, 0f);
        }

        static Tilemap NewTilemap(GameObject grid, string name)
        {
            GameObject obj = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            obj.transform.SetParent(grid.transform, false);
            return obj.GetComponent<Tilemap>();
        }

        /// <summary>
        /// Persistent AnimatedTile asset for the 6-frame lava loop
        /// (2d-tilemap-extras is in the manifest). Replaces the old
        /// in-memory Tile instance, which was never saved as an asset — the
        /// reference died on scene reload. Collider stays None: the
        /// GEN_LavaVolume boxes own kill-on-touch, which must not change.
        /// </summary>
        internal static TileBase EnsureLavaTile()
        {
            const string assetPath = "Assets/Levels/LavaTile.asset";
            Sprite[] frames = LavaFrames();
            if (frames.Length == 0)
            {
                Debug.LogError("[LevelBuilder] lava.png has no sprites — lava tiles skipped");
                return null;
            }

            var tile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.AnimatedTile>(assetPath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.AnimatedTile>();
                if (!AssetDatabase.IsValidFolder("Assets/Levels"))
                    AssetDatabase.CreateFolder("Assets", "Levels");
                AssetDatabase.CreateAsset(tile, assetPath);
            }
            tile.m_AnimatedSprites = frames;
            tile.m_MinSpeed = 6f;
            tile.m_MaxSpeed = 6f;
            tile.m_TileColliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        /// <summary>
        /// All lava frames in loop order. The regenerated manifest names its
        /// frames lava_0..lava_5 and the slicer prefixes the sheet name
        /// (lava_lava_0...), so load the whole sheet sorted instead of
        /// hardcoding a spelling or a frame count.
        /// </summary>
        internal static Sprite[] LavaFrames()
        {
            return AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Generated/Props/lava.png")
                .OfType<Sprite>().OrderBy(s => s.name).ToArray();
        }

        // ---------------- entities ----------------

        static Vector3 World(Vector2Int tile) { return new Vector3(tile.x + 0.5f, tile.y + 0.5f, 0f); }

        static void BuildEntities(LevelDefinition def)
        {
            GameObject holder = new GameObject(GenPrefix + "Entities");

            Place(Player1Prefab, World(def.player1Spawn), holder, "Player1", asPrefab: true, reuseExisting: true);
            Place(Player2Prefab, World(def.player2Spawn), holder, "Player2", asPrefab: true, reuseExisting: true);
            // Playtest 5: hero-matched ExitDoors replace the solid Dragon
            // portals (ExitDoor wins when BOTH heroes hold their doors).
            PlaceExitDoor(holder, "ExitDoor1", DoorWorld(def, def.exit1), 1);
            PlaceExitDoor(holder, "ExitDoor2", DoorWorld(def, def.exit2), 2);

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

            // 1.8 tiles: keep in lockstep with PlaytestFixes.SwapPowerupSprite.
            foreach (Vector2Int p in def.powerUpOne)
                NormalizeSpriteHeight(Place(PowerUp1Prefab, World(p), holder, "PowerUpOne", asPrefab: true), 1.8f);
            foreach (Vector2Int p in def.powerUpTwo)
                NormalizeSpriteHeight(Place(PowerUp2Prefab, World(p), holder, "PowerUpTwo", asPrefab: true), 1.8f);

            foreach (Vector2Int p in def.hearts)
                AddHeart(holder, World(p));

            Sprite starSprite = FirstSprite("Assets/Art/Generated/Props/star.png", "star_twinkle_0");
            // Mario-style: exactly ONE star per level (first authored spot).
            if (def.crystals.Length > 0)
                AddCoinSpin(SpriteEntity<Crystal>(holder, "Star", World(def.crystals[0]), starSprite, 1.1f));

            foreach (Vector2Int p in def.checkpoints)
                AddCheckpoint(holder, World(p));

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
                    Sprite[] lavaFrames = LavaFrames();
                    GameObject riserObj = new GameObject(GenPrefix + "LavaRiser");
                    riserObj.transform.SetParent(holder.transform, false);
                    riserObj.transform.position = World(mp.from) + Vector3.down * 3f;
                    var riserSprite = riserObj.AddComponent<SpriteRenderer>();
                    riserSprite.sprite = lavaFrames.FirstOrDefault();
                    riserSprite.drawMode = SpriteDrawMode.Tiled;
                    riserSprite.size = new Vector2(def.GridWidth, 2f);
                    // SpriteCycler swaps sprites under the tiled draw mode —
                    // the surface churns as it rises.
                    if (lavaFrames.Length > 1)
                    {
                        var riserCycler = riserObj.AddComponent<SpriteCycler>();
                        riserCycler.frames = lavaFrames;
                        riserCycler.fps = 6f;
                    }
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
            // The onboarding chain bakes into the shipped progression's
            // first level (SceneRouter.FirstLevel — L1 "First Light", whose
            // flat floor the anchors below assume).
            if (def.levelNumber == SceneRouter.FirstLevel)
            {
                AddHint(holder, "Hold < > to move, tap A to jump\n(tap A again mid-air!)",
                    World(def.player1Spawn) + new Vector3(2, 0, 0), false, false);
                AddHint(holder, "Tap the SWAP button (or your partner's portrait)\nto control the other hero",
                    World(def.player1Spawn) + new Vector3(8, 0, 0), false, false);
                AddHint(holder, "BOTH heroes must stand in\ntheir matching doors to escape!",
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
                { AddHint(holder, "Skeletons hit hard -\nshoot them or slip past", World(e.position) + new Vector3(-3, 2, 0), true, false); break; }
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
                        ? "Ride up together - the lava rises!\nHit levers on time to stay ahead"
                        : "Ride the moving platform across",
                    World(mp.from) + new Vector3(0, 2, 0), true, false);
            }
            // Lava warning at the first lava run.
            for (int y = 0; y < def.GridHeight; y++)
                for (int x = 0; x < def.GridWidth; x++)
                    if (def.TileAt(x, y) == 'L')
                    {
                        AddHint(holder, "Lava is instant death -\ntime your jumps",
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
        /// Scenes carry multiple tilemaps (deco/lava layers have no
        /// colliders) — only the layer with the TilemapCollider2D represents
        /// standable geometry. Used by PlaytestFixes.EaseLevelOne.
        /// </summary>
        internal static Tilemap FindColliderTilemap()
        {
            return Object.FindObjectsOfType<Tilemap>()
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
        }

        /// <summary>
        /// Standable spot from live tilemap geometry: the first cell (searching
        /// down from 60% height at the given width fraction, then nearby
        /// columns) whose cell below has a tile and which is itself clear,
        /// returned as a world position 1.5 tiles above the ground surface.
        /// </summary>
        internal static Vector3? FindStandableWorld(Tilemap tilemap, float fractionX)
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

        /// <summary>Torch checkpoint in the standard structure (shared with
        /// PlaytestFixes.EaseLevelOne — keep every checkpoint identical).</summary>
        internal static GameObject AddCheckpoint(GameObject holder, Vector3 pos)
        {
            const string torchSheet = "Assets/Art/Generated/Props/checkpoint_torch.png";
            Sprite torchUnlit = FirstSprite(torchSheet, "checkpoint_torch_unlit_0");
            Sprite torchLit = FirstSprite(torchSheet, "checkpoint_torch_lit_0");
            GameObject cp = SpriteEntity<Checkpoint>(holder, "Checkpoint", pos, torchUnlit, 1.6f);
            var comp = cp.GetComponent<Checkpoint>();
            comp.unlitSprite = torchUnlit;
            comp.litSprite = torchLit;
            return cp;
        }

        internal static GameObject AddHeart(GameObject holder, Vector3 pos)
        {
            return SpriteEntity<HeartPickup>(holder, "Heart", pos, FindGrottoSprite("heart"), 0.8f);
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
            renderer.sortingOrder = PickupSortingOrder;
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
            // Door glow now lives ON each ExitDoor (see PlaceExitDoor). The
            // old World(def.exit1/2) lights here floated at authored TILE
            // coords — mid-air nonsense in legacy scenes (whose defs are
            // normalized to a different origin) and above the doorway in
            // generated ones (DoorWorld drops doors to the floor).
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
                // "Story Details" intentionally omitted since build 3 — the
                // scene is orphaned (keyboard-only copy, no live callers) and
                // re-adding it here silently reverted its removal from Build
                // Settings on every level rebuild.
                new EditorBuildSettingsScene("Assets/Scenes/Level Scene.unity", true),
            };
            // Intro ships once IntroCutsceneBuilder has created it — listing
            // a missing scene would fail the player build outright.
            if (System.IO.File.Exists("Assets/Scenes/Intro.unity"))
                scenes.Insert(1, new EditorBuildSettingsScene("Assets/Scenes/Intro.unity", true));
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
