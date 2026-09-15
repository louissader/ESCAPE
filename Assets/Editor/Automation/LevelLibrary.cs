using UnityEditor;
using UnityEngine;
using ET = LevelDefinition.EnemyType;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Authored content for ALL EIGHT levels. Campaign redesign (playtest,
    /// build 5): levels 1-3 are brand-new short tutorial levels replacing
    /// the legacy hand-built scenes; levels 4-8 keep their identities but at
    /// roughly half their old traverse. Design rules honored throughout:
    /// max rise 4 (+2 double jump), max gap 5 on the golden path; every
    /// P2-boost shortcut has a paired no-boost route; no one-way drops that
    /// strand a hero; 3 crystal spots (first = the star) plus hearts and
    /// torch checkpoints per level; L8 checkpoints only before the riser.
    /// Every grid passes LevelSolvability AND a solo 6/5 envelope
    /// (swap-mode single hero, no boost) from both spawns and checkpoints.
    /// </summary>
    public static class LevelLibrary
    {
        [MenuItem("Escape/Create Level Definitions (1-8)")]
        public static void CreateAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Levels"))
                AssetDatabase.CreateFolder("Assets", "Levels");

            Save(CreateLevel1(), 1);
            Save(CreateLevel2(), 2);
            Save(CreateLevel3(), 3);
            Save(CreateLevel4(), 4);
            Save(CreateLevel5(), 5);
            Save(CreateLevel6(), 6);
            Save(CreateLevel7(), 7);
            Save(CreateLevel8(), 8);
            AssetDatabase.SaveAssets();
            Debug.Log("[LevelLibrary] definitions 1-8 created");
        }

        static void Save(LevelDefinition def, int n)
        {
            string path = $"Assets/Levels/Level{n}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(def, existing);
                Object.DestroyImmediate(def);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(def, path);
            }
        }

        static Vector2Int V(int x, int y) { return new Vector2Int(x, y); }

        // ------------------------------------------------------------------
        // L1 "First Light" — Grotto. Pure onboarding: flat run, one raised
        // step holding the star, one slow slime, no lava. The FirstLevel
        // hint chain (move/jump at spawn+2, swap at spawn+8, doors at
        // exit1-6) bakes along the flat floor at y=1.
        //
        // SPAWN CONTRACT (PlayerPassThroughTests.Heroes_OverlapWithoutShoving,
        // which runs on Level 1): the two spawns must share the same X.
        // Teleport() writes rigid.position, and transform.position stays
        // stale until the next physics step — so the test's "displacement"
        // reading is really |p1SpawnX - p2SpawnX|. The legacy scene stacked
        // WISP ~3 tiles above EMBER at the same X (dx 0.09); keep that
        // arrangement: WISP drifts down onto EMBER at level start, which
        // also showcases the two-heroes-overlap mechanic to new players.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel1()
        {
            var g = new LevelGridBuilder(34, 14);
            g.Floor(0);
            g.Wall(0, 1, 13);
            g.Wall(33, 1, 13);
            g.Platform(12, 3, 3);

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 1;
            def.title = "First Light";
            def.zone = LevelDefinition.Zone.Grotto;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(2, 1);
            def.player2Spawn = V(2, 3);
            def.exit1 = V(28, 1);
            def.exit2 = V(31, 1);
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(17, 1), patrolSpan = 1.5f },
            };
            def.hearts = new[] { V(24, 1) };
            def.crystals = new[] { V(13, 4), V(6, 2), V(26, 2) };
            def.checkpoints = new[] { V(16, 1) };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(34, 14);
            return def;
        }

        // ------------------------------------------------------------------
        // L2 "Two Paths" — Grotto. Teaches swap + lever/bridge: WISP's door
        // sits on an upper ledge lane (steps at x5/x8, shelf x11-20, hops to
        // x24 and x30), EMBER's on the floor lane past a lava pit whose
        // bridge the lever at x13 extends. Lever is BEFORE the lava.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel2()
        {
            var g = new LevelGridBuilder(40, 18);
            g.Floor(0);
            g.Wall(0, 1, 17);
            g.Wall(39, 1, 17);
            g.Block(5, 1, 2, 2);
            g.Platform(8, 4, 2);
            g.Platform(11, 6, 10);
            g.Platform(24, 6, 3);
            g.Platform(30, 5, 6);
            g.Lava(16, 1, 6);

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 2;
            def.title = "Two Paths";
            def.zone = LevelDefinition.Zone.Grotto;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(2, 1);
            def.player2Spawn = V(3, 1);
            def.exit1 = V(36, 1);
            def.exit2 = V(33, 6);
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(28, 1), patrolSpan = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.BatPassive, position = V(22, 9), moveRange = 2.0f },
            };
            def.leverGroups = new[]
            {
                new LevelDefinition.LeverBridgeGroup { lever = V(13, 1), bridgeTiles = new[] { V(16, 2), V(17, 2), V(18, 2), V(19, 2), V(20, 2), V(21, 2) } },
            };
            def.hearts = new[] { V(8, 5), V(30, 1) };
            def.crystals = new[] { V(14, 8), V(22, 3), V(37, 1) };
            def.checkpoints = new[] { V(23, 1) };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(40, 18);
            return def;
        }

        // ------------------------------------------------------------------
        // L3 "Spark" — Grotto. Introduces both orbs: red early for three
        // floor skeletons, blue for the star ledge at y7 (boost straight up,
        // or the paired no-boost zig-zag: x43 step, x39 under-ledge shelf,
        // x43 upper step, hop left onto the ledge). One 4-wide lava patch
        // with a hop platform sets the jump rhythm.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel3()
        {
            var g = new LevelGridBuilder(46, 20);
            g.Floor(0);
            g.Wall(0, 1, 19);
            g.Wall(45, 1, 19);
            g.Lava(24, 1, 4);
            g.Platform(25, 3, 2);
            g.Platform(38, 7, 3);
            g.Block(43, 1, 2, 2);
            g.Platform(39, 4, 2);
            g.Platform(43, 6, 2);

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 3;
            def.title = "Spark";
            def.zone = LevelDefinition.Zone.Grotto;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(2, 1);
            def.player2Spawn = V(3, 1);
            def.exit1 = V(30, 1);
            def.exit2 = V(33, 1);
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(14, 1), patrolSpan = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(18, 1), patrolSpan = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(36, 1), patrolSpan = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.BatPassive, position = V(26, 7), moveRange = 2.0f },
            };
            def.powerUpOne = new[] { V(6, 1) };
            def.powerUpTwo = new[] { V(10, 1) };
            def.hearts = new[] { V(21, 1), V(29, 1) };
            def.crystals = new[] { V(39, 8), V(25, 5), V(10, 2) };
            def.checkpoints = new[] { V(22, 1) };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(46, 20);
            return def;
        }

        // ------------------------------------------------------------------
        // L4 "Skeleton Halls" — Grotto. Halved traverse, same identity:
        // three halls at rising heights, two lever/bridge groups (A spans
        // the first lava pit at height, B the gap over the big lava lake
        // before the exit shelf), skeletons + slime.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel4()
        {
            var g = new LevelGridBuilder(55, 24);
            g.Floor(0);
            g.Wall(0, 1, 23);
            g.Wall(54, 1, 23);
            g.Block(9, 1, 3, 2);
            g.Platform(13, 4, 3);
            g.Lava(17, 1, 6);
            g.Platform(23, 4, 10);
            g.Platform(34, 7, 3);
            g.Platform(38, 10, 3);
            g.Platform(41, 13, 5);
            g.Platform(50, 13, 4);
            g.Lava(36, 1, 17);

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 4;
            def.title = "Skeleton Halls";
            def.zone = LevelDefinition.Zone.Grotto;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(2, 1);
            def.player2Spawn = V(3, 1);
            def.exit1 = V(50, 14);
            def.exit2 = V(52, 14);
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(6, 1), patrolSpan = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(26, 5), patrolSpan = 2.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(35, 10), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(39, 12), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(43, 14), patrolSpan = 1.5f },
            };
            def.leverGroups = new[]
            {
                new LevelDefinition.LeverBridgeGroup { lever = V(14, 5), bridgeTiles = new[] { V(17, 4), V(18, 4), V(19, 4), V(20, 4), V(21, 4), V(22, 4) } },
                new LevelDefinition.LeverBridgeGroup { lever = V(44, 14), bridgeTiles = new[] { V(46, 13), V(47, 13), V(48, 13), V(49, 13) } },
            };
            def.powerUpOne = new[] { V(10, 3) };
            def.powerUpTwo = new[] { V(29, 5) };
            def.hearts = new[] { V(11, 3), V(25, 5), V(39, 11) };
            def.crystals = new[] { V(35, 9), V(18, 6), V(47, 15) };
            def.checkpoints = new[] { V(24, 5), V(42, 14) };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(55, 24);
            return def;
        }

        // ------------------------------------------------------------------
        // L5 "The Depths" — Depths. Halved: one clean vertical zig-zag
        // shaft (rises of 3, alternating sides), a star side-ledge detour, a
        // vertical ride platform to the top shelves, and a lever bridge
        // joining the split summit before the doors. Eyes patrol the climb.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel5()
        {
            var g = new LevelGridBuilder(46, 30);
            g.Floor(0);
            g.Wall(0, 1, 29);
            g.Wall(45, 1, 29);
            g.Platform(8, 3, 5);
            g.Platform(16, 6, 5);
            g.Platform(9, 9, 5);
            g.Platform(17, 12, 6);
            g.Platform(10, 15, 5);
            g.Platform(18, 18, 5);
            g.Platform(26, 10, 3);
            g.Platform(28, 24, 4);
            g.Platform(36, 24, 8);

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 5;
            def.title = "The Depths";
            def.zone = LevelDefinition.Zone.Depths;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(2, 1);
            def.player2Spawn = V(3, 1);
            def.exit1 = V(39, 25);
            def.exit2 = V(42, 25);
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Eye, position = V(17, 8), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.BatPassive, position = V(14, 11), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Eye, position = V(11, 17), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(20, 21), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(37, 25), patrolSpan = 1.5f },
            };
            def.leverGroups = new[]
            {
                new LevelDefinition.LeverBridgeGroup { lever = V(29, 25), bridgeTiles = new[] { V(32, 24), V(33, 24), V(34, 24), V(35, 24) } },
            };
            def.powerUpOne = new[] { V(11, 16) };
            def.hearts = new[] { V(18, 7), V(12, 16), V(28, 25) };
            def.crystals = new[] { V(27, 12), V(10, 4), V(25, 26) };
            def.checkpoints = new[] { V(18, 13), V(31, 25) };
            def.movingPlatforms = new[]
            {
                new LevelDefinition.MovingPlatformPlacement { from = V(25, 19), to = V(25, 24), speed = 2.0f },
            };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(46, 30);
            return def;
        }

        // ------------------------------------------------------------------
        // L6 "Frogmarch" — Depths. Halved: one frog-timed stepping-stone
        // lava crossing (frogs on stones 1 and 3, stone 2 safe), a short
        // bat-guarded climb, then the lever bridge over the second lava
        // stretch to the exit shelf.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel6()
        {
            var g = new LevelGridBuilder(52, 24);
            g.Floor(0);
            g.Wall(0, 1, 23);
            g.Wall(51, 1, 23);
            g.Lava(6, 1, 12);
            g.Block(8, 1, 2, 2);
            g.Block(12, 1, 2, 2);
            g.Block(16, 1, 2, 2);
            g.Block(24, 1, 3, 2);
            g.Platform(29, 4, 3);
            g.Platform(34, 6, 3);
            g.Lava(38, 1, 7);
            g.Platform(45, 6, 6);

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 6;
            def.title = "Frogmarch";
            def.zone = LevelDefinition.Zone.Depths;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(2, 1);
            def.player2Spawn = V(3, 1);
            def.exit1 = V(46, 7);
            def.exit2 = V(49, 7);
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(9, 3), patrolSpan = 1.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(17, 3), patrolSpan = 1.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(22, 1), patrolSpan = 1.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(27, 5), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(32, 7), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(41, 9), moveRange = 2.0f },
            };
            def.leverGroups = new[]
            {
                new LevelDefinition.LeverBridgeGroup { lever = V(35, 7), bridgeTiles = new[] { V(38, 6), V(39, 6), V(40, 6), V(41, 6), V(42, 6), V(43, 6), V(44, 6) } },
            };
            def.powerUpOne = new[] { V(19, 1) };
            def.powerUpTwo = new[] { V(25, 3) };
            def.hearts = new[] { V(13, 3), V(30, 5), V(45, 7) };
            def.crystals = new[] { V(12, 5), V(33, 8), V(41, 8) };
            def.checkpoints = new[] { V(18, 1), V(34, 7) };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(52, 24);
            return def;
        }

        // ------------------------------------------------------------------
        // L7 "Twin Trials" — Depths. Halved split lanes: a two-tile divider
        // slab separates the floor lane (skeletons + lava hop) from the top
        // lane (bat, ghost, star). Connector shafts at x20-23 (with ladder
        // platforms) and x38-41 (lever-bridged on top) keep the lanes
        // mutually reachable; reunion chamber before the shared doors.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel7()
        {
            var g = new LevelGridBuilder(56, 26);
            g.Floor(0);
            g.Wall(0, 1, 25);
            g.Wall(55, 1, 25);
            g.Block(6, 8, 14, 2);
            g.Block(24, 8, 14, 2);
            g.Block(42, 8, 8, 2);
            g.Platform(20, 3, 2);
            g.Platform(22, 6, 2);
            g.Lava(32, 1, 3);

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 7;
            def.title = "Twin Trials";
            def.zone = LevelDefinition.Zone.Depths;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(2, 1);
            def.player2Spawn = V(3, 1);
            def.exit1 = V(51, 1);
            def.exit2 = V(53, 1);
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(10, 1), patrolSpan = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(28, 1), patrolSpan = 2.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(44, 1), patrolSpan = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(15, 12), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(30, 12), moveRange = 2.0f },
            };
            def.leverGroups = new[]
            {
                new LevelDefinition.LeverBridgeGroup { lever = V(35, 10), bridgeTiles = new[] { V(38, 9), V(39, 9), V(40, 9), V(41, 9) } },
            };
            def.powerUpOne = new[] { V(5, 1) };
            def.powerUpTwo = new[] { V(17, 10) };
            def.hearts = new[] { V(21, 4), V(36, 10), V(50, 1) };
            def.crystals = new[] { V(31, 11), V(33, 3), V(22, 7) };
            def.checkpoints = new[] { V(25, 1), V(25, 10) };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(56, 26);
            return def;
        }

        // ------------------------------------------------------------------
        // L8 "ESCAPE" — finale, halved approach: star hop, lava hop, lever
        // bridge, then the shaft where the shared ride elevator races the
        // LavaRiser (ride 1.6 t/s over 26 tiles ≈ 16s vs riser 0.8 t/s to
        // the shelf ≈ 37s — comfortably beatable). Checkpoints ONLY before
        // the riser trigger; both orbs granted at the start.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel8()
        {
            var g = new LevelGridBuilder(58, 34);
            g.Floor(0);
            g.Wall(0, 0, 34);
            g.Wall(57, 0, 34);
            g.Platform(4, 3, 3);
            g.Lava(16, 1, 4);
            g.Platform(17, 3, 2);
            g.Lava(29, 1, 5);
            g.Block(38, 6, 2, 26);
            g.Block(48, 6, 2, 24);
            g.Platform(50, 28, 8);

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 8;
            def.title = "ESCAPE";
            def.zone = LevelDefinition.Zone.Depths;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(2, 1);
            def.player2Spawn = V(3, 1);
            def.exit1 = V(53, 29);
            def.exit2 = V(55, 29);
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(13, 1), patrolSpan = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(19, 6), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(23, 1), patrolSpan = 1.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(36, 1) },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(41, 12), moveRange = 1.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(44, 20), moveRange = 1.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Eye, position = V(50, 31), moveRange = 2.0f },
            };
            def.leverGroups = new[]
            {
                new LevelDefinition.LeverBridgeGroup { lever = V(27, 1), bridgeTiles = new[] { V(29, 2), V(30, 2), V(31, 2), V(32, 2), V(33, 2) } },
            };
            def.powerUpOne = new[] { V(8, 1) };
            def.powerUpTwo = new[] { V(10, 1) };
            def.hearts = new[] { V(20, 1), V(34, 1), V(50, 29) };
            def.crystals = new[] { V(5, 5), V(17, 5), V(44, 31) };
            def.checkpoints = new[] { V(25, 1), V(37, 1) };
            def.movingPlatforms = new[]
            {
                new LevelDefinition.MovingPlatformPlacement { from = V(43, 3), to = V(43, 29), speed = 1.6f, isLavaRiserRide = true },
            };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(58, 34);
            return def;
        }
    }
}
