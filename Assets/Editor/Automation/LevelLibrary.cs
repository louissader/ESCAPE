using UnityEditor;
using UnityEngine;
using ET = LevelDefinition.EnemyType;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Authored content for the five NEW levels (4-8). Design rules honored
    /// throughout: max jump 4 tiles (+2 double), max gap 5; every P2-boost
    /// has a paired P2 route; no one-way drops that strand a parked
    /// character; co-op chambers fit a ~40x22 view; 3 crystals off the
    /// golden path; checkpoints (L8: only before the lava riser).
    /// </summary>
    public static class LevelLibrary
    {
        [MenuItem("Escape/Create Level Definitions (4-8)")]
        public static void CreateAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Levels"))
                AssetDatabase.CreateFolder("Assets", "Levels");

            Save(CreateLevel4(), 4);
            Save(CreateLevel5(), 5);
            Save(CreateLevel6(), 6);
            Save(CreateLevel7(), 7);
            Save(CreateLevel8(), 8);
            AssetDatabase.SaveAssets();
            Debug.Log("[LevelLibrary] definitions 4-8 created");
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
        // L4 "Skeleton Halls" — Grotto. Skeletons + slimes; the multi-lever
        // showcase: two levers each opening a separate bridge group.
        // Layout: three halls at rising heights joined by stairs; bridge A
        // spans a pit between hall 1-2, bridge B a lava pit before the exit.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel4()
        {
            var g = new LevelGridBuilder(90, 30);
            g.Floor(0);
            g.Wall(0, 1, 29);
            g.Wall(89, 1, 29);
            g.Wall(4, 1, 4);
            g.Platform(8, 3, 4);
            g.Wall(11, 1, 3);
            g.Platform(13, 5, 3);
            g.Platform(18, 7, 4);
            g.Lava(23, 1, 10);
            g.Platform(32, 7, 5);
            g.Wall(33, 1, 6);
            g.Platform(27, 11, 3);
            g.Block(26, 26, 3, 3);
            g.Wall(38, 1, 3);
            g.Platform(37, 10, 3);
            g.Platform(42, 12, 3);
            g.Platform(47, 14, 3);
            g.Platform(50, 17, 4);
            g.Platform(50, 21, 5);
            g.Platform(49, 24, 6);
            g.Wall(46, 1, 4);
            g.Platform(40, 15, 2);
            g.Platform(34, 18, 3);
            g.Platform(39, 22, 3);
            g.Block(44, 26, 2, 3);
            g.Lava(55, 1, 12);
            g.Platform(63, 24, 5);
            g.Platform(70, 23, 4);
            g.Platform(75, 21, 4);
            g.Platform(80, 19, 8);
            g.Platform(68, 27, 3);
            g.Wall(68, 1, 5);
            g.Wall(78, 1, 4);
            g.Block(58, 27, 2, 2);

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 4;
            def.title = "Skeleton Halls";
            def.zone = LevelDefinition.Zone.Grotto;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(2, 1);
            def.player2Spawn = V(3, 1);
            def.exit1 = V(84, 20);
            def.exit2 = V(85, 20);
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(9, 1), patrolSpan = 4.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(15, 1), patrolSpan = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(34, 8), patrolSpan = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(45, 17), moveRange = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(48, 20), moveRange = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(65, 25), patrolSpan = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(72, 27), moveRange = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(81, 20), patrolSpan = 3.0f },
            };
            def.leverGroups = new[]
            {
                new LevelDefinition.LeverBridgeGroup { lever = V(21, 8), bridgeTiles = new[] { V(23, 7), V(24, 7), V(25, 7), V(26, 7), V(27, 7), V(28, 7), V(29, 7), V(30, 7), V(31, 7) } },
                new LevelDefinition.LeverBridgeGroup { lever = V(53, 25), bridgeTiles = new[] { V(55, 24), V(56, 24), V(57, 24), V(58, 24), V(59, 24), V(60, 24), V(61, 24), V(62, 24) } },
            };
            def.powerUpOne = new[] { V(19, 8) };
            def.powerUpTwo = new[] { V(43, 13) };
            def.hearts = new[] { V(21, 8), V(37, 11), V(51, 25), V(71, 24) };
            def.crystals = new[] { V(28, 12), V(35, 19), V(40, 23) };
            def.checkpoints = new[] { V(32, 8), V(50, 25) };
            def.movingPlatforms = new[]
            {
                new LevelDefinition.MovingPlatformPlacement { from = V(37, 16), to = V(37, 21), speed = 1.0f },
            };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(90, 30);
            return def;
        }

        // ------------------------------------------------------------------
        // L5 "The Depths" — new zone. Eyes + moving platforms; a vertical
        // shaft climb with a mid checkpoint. Chambers stay within 40x22.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel5()
        {
            var g = new LevelGridBuilder(78, 44);
            g.Floor(0);
            g.Wall(4, 1, 42);
            g.Wall(52, 1, 42);
            g.Lava(20, 1, 10);
            g.Lava(36, 1, 10);
            g.Platform(8, 3, 6);
            g.Platform(15, 6, 6);
            g.Platform(23, 9, 6);
            g.Platform(17, 12, 6);
            g.Platform(9, 15, 6);
            g.Platform(15, 18, 7);
            g.Platform(9, 21, 6);
            g.Platform(15, 24, 6);
            g.Platform(24, 25, 7);
            g.Platform(17, 28, 6);
            g.Platform(24, 31, 6);
            g.Platform(16, 34, 6);
            g.Platform(22, 37, 8);
            g.Platform(24, 40, 10);
            g.Platform(31, 7, 5);
            g.Platform(38, 5, 6);
            g.Platform(45, 4, 5);
            g.Platform(34, 31, 5);
            g.Platform(44, 31, 5);
            g.Platform(14, 37, 5);
            g.Platform(8, 39, 5);
            g.Platform(30, 12, 4);
            g.Platform(33, 16, 4);
            g.Wall(51, 1, 8);
            g.Block(48, 8, 4, 6);
            g.Clear(48, 9, 2, 2);
            g.Block(13, 41, 2, 2);
            g.Block(36, 41, 2, 2);
            g.Block(44, 41, 2, 2);

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 5;
            def.title = "The Depths";
            def.zone = LevelDefinition.Zone.Depths;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(6, 1);
            def.player2Spawn = V(7, 1);
            def.exit1 = V(28, 41);
            def.exit2 = V(29, 41);
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(17, 7), patrolSpan = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.BatPassive, position = V(22, 8), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Eye, position = V(16, 20), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(31, 23), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(27, 26), patrolSpan = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Eye, position = V(22, 33), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(24, 36), moveRange = 2.0f },
            };
            def.leverGroups = new[]
            {
                new LevelDefinition.LeverBridgeGroup { lever = V(36, 32), bridgeTiles = new[] { V(39, 31), V(40, 31), V(41, 31), V(42, 31), V(43, 31) } },
            };
            def.powerUpOne = new[] { V(12, 16) };
            def.hearts = new[] { V(45, 5), V(18, 19), V(28, 38) };
            def.crystals = new[] { V(47, 5), V(46, 32), V(10, 40) };
            def.checkpoints = new[] { V(11, 16), V(19, 29) };
            def.movingPlatforms = new[]
            {
                new LevelDefinition.MovingPlatformPlacement { from = V(26, 19), to = V(26, 25), speed = 2.0f },
            };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(78, 44);
            return def;
        }

        // ------------------------------------------------------------------
        // L6 "Frogmarch" — Depths. Frog-timed corridors over lava; P2
        // platform doubles as a fireball shield. Every boost is paired with
        // stairs for P2.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel6()
        {
            var g = new LevelGridBuilder(100, 30);
            g.Floor(0);
            g.Wall(0, 0, 30);
            g.Wall(99, 0, 30);
            g.Lava(6, 1, 17);
            g.Platform(6, 0, 17);
            g.Lava(55, 1, 10);
            g.Platform(55, 0, 10);
            g.Block(9, 1, 2, 2);
            g.Block(14, 1, 2, 2);
            g.Block(19, 1, 2, 2);
            g.Platform(4, 5, 2);
            g.Platform(14, 6, 2);
            g.Platform(9, 8, 2);
            g.Wall(28, 1, 3);
            g.Wall(32, 1, 3);
            g.Wall(38, 1, 4);
            g.Wall(45, 1, 4);
            g.Wall(50, 1, 3);
            g.Platform(27, 2, 3);
            g.Platform(29, 3, 3);
            g.Platform(35, 6, 3);
            g.Platform(41, 6, 3);
            g.Platform(46, 8, 3);
            g.Platform(44, 11, 2);
            g.Platform(38, 12, 3);
            g.Platform(49, 8, 5);
            g.Platform(51, 2, 2);
            g.Platform(57, 9, 2);
            g.Platform(62, 9, 3);
            g.Platform(66, 3, 3);
            g.Platform(70, 6, 3);
            g.Platform(67, 9, 3);
            g.Platform(71, 12, 3);
            g.Platform(67, 15, 3);
            g.Platform(63, 16, 2);
            g.Platform(70, 18, 4);
            g.Platform(76, 18, 14);
            g.Block(20, 25, 2, 3);
            g.Block(44, 24, 2, 4);
            g.Block(72, 23, 2, 4);

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 6;
            def.title = "Frogmarch";
            def.zone = LevelDefinition.Zone.Depths;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(3, 1);
            def.player2Spawn = V(2, 1);
            def.exit1 = V(86, 19);
            def.exit2 = V(88, 19);
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(10, 3), patrolSpan = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(19, 3), patrolSpan = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(36, 1), patrolSpan = 4.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(33, 9), moveRange = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(40, 11), moveRange = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(46, 12), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(69, 11), moveRange = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(66, 1), patrolSpan = 2.0f },
            };
            def.leverGroups = new[]
            {
                new LevelDefinition.LeverBridgeGroup { lever = V(52, 3), bridgeTiles = new[] { V(55, 2), V(56, 2), V(57, 2), V(58, 2), V(59, 2), V(60, 2), V(61, 2), V(62, 2), V(63, 2), V(64, 2) } },
            };
            def.powerUpOne = new[] { V(26, 1) };
            def.powerUpTwo = new[] { V(30, 5) };
            def.hearts = new[] { V(14, 3), V(41, 7), V(54, 1) };
            def.crystals = new[] { V(15, 7), V(44, 12), V(64, 17) };
            def.checkpoints = new[] { V(25, 1), V(65, 1) };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(100, 30);
            return def;
        }

        // ------------------------------------------------------------------
        // L7 "Twin Trials" — Depths. Split parallel lanes: upper lane is
        // P1-flavored (crouch tunnels + shooting), lower lane P2-flavored
        // (boost spots with paired stairs). Both lanes stay mutually
        // reachable via connector shafts every ~20 tiles (leapfrog rule),
        // reunion chamber before the shared double exit.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel7()
        {
            var g = new LevelGridBuilder(100, 34);
            g.Floor(0);
            g.Wall(0, 1, 33);
            g.Wall(99, 1, 33);
            g.Block(1, 11, 16, 3);
            g.Block(25, 11, 14, 3);
            g.Block(47, 11, 12, 3);
            g.Block(67, 11, 12, 3);
            g.Platform(6, 3, 4);
            g.Platform(13, 5, 4);
            g.Platform(20, 3, 4);
            g.Platform(27, 6, 4);
            g.Platform(34, 4, 4);
            g.Platform(41, 7, 4);
            g.Platform(48, 5, 4);
            g.Platform(55, 8, 4);
            g.Platform(62, 6, 4);
            g.Platform(69, 9, 4);
            g.Platform(76, 7, 4);
            g.Platform(83, 9, 4);
            g.Platform(18, 7, 2);
            g.Platform(20, 10, 2);
            g.Platform(18, 13, 2);
            g.Platform(45, 9, 2);
            g.Platform(43, 12, 2);
            g.Platform(45, 15, 2);
            g.Platform(60, 9, 2);
            g.Platform(62, 12, 2);
            g.Platform(60, 15, 2);
            g.Platform(80, 10, 2);
            g.Platform(82, 13, 2);
            g.Platform(80, 16, 2);
            g.Platform(20, 16, 4);
            g.Platform(27, 18, 4);
            g.Platform(34, 16, 4);
            g.Platform(41, 19, 4);
            g.Platform(48, 17, 4);
            g.Platform(55, 20, 4);
            g.Platform(62, 18, 4);
            g.Platform(69, 21, 4);
            g.Platform(76, 19, 4);
            g.Platform(83, 17, 4);
            g.Platform(33, 19, 2);
            g.Platform(34, 22, 3);
            g.Platform(52, 2, 2);
            g.Platform(69, 24, 3);
            g.Platform(90, 11, 8);
            g.Platform(87, 15, 3);
            g.Lava(45, 1, 4);
            g.Block(46, 20, 6, 2);
            g.Block(28, 21, 4, 2);
            g.Block(23, 26, 2, 2);
            g.Block(73, 26, 2, 2);

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 7;
            def.title = "Twin Trials";
            def.zone = LevelDefinition.Zone.Depths;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(2, 1);
            def.player2Spawn = V(3, 1);
            def.exit1 = V(92, 12);
            def.exit2 = V(94, 12);
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(8, 4), patrolSpan = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.BatPassive, position = V(24, 11), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(28, 19), patrolSpan = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(56, 21), moveRange = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(70, 22), moveRange = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(56, 9), patrolSpan = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(70, 10), patrolSpan = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(60, 12), moveRange = 2.0f },
            };
            def.leverGroups = new[]
            {
                new LevelDefinition.LeverBridgeGroup { lever = V(36, 5), bridgeTiles = new[] { V(45, 1), V(46, 1), V(47, 1), V(48, 1) } },
            };
            def.powerUpOne = new[] { V(28, 19) };
            def.powerUpTwo = new[] { V(7, 4) };
            def.hearts = new[] { V(49, 6), V(58, 21), V(86, 10) };
            def.crystals = new[] { V(35, 23), V(53, 3), V(70, 25) };
            def.checkpoints = new[] { V(44, 8), V(50, 14) };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(100, 34);
            return def;
        }

        // ------------------------------------------------------------------
        // L8 "ESCAPE" — finale. Everything, then the shared-ride elevator
        // racing the LavaRiser (Swap-safe: both characters board one
        // platform). Checkpoints ONLY before the riser trigger.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel8()
        {
            var g = new LevelGridBuilder(92, 46);
            g.Floor(0);
            g.Wall(0, 0, 46);
            g.Wall(91, 0, 46);
            g.Platform(6, 4, 3);
            g.Wall(11, 1, 3);
            g.Platform(14, 3, 2);
            g.Platform(18, 5, 3);
            g.Lava(16, 1, 6);
            g.Platform(16, 0, 6);
            g.Platform(15, 3, 2);
            g.Platform(19, 3, 2);
            g.Block(30, 1, 3, 4);
            g.Stairs(25, 1, 2, 1);
            g.Platform(28, 3, 2);
            g.Platform(34, 5, 3);
            g.Platform(37, 2, 5);
            g.Platform(49, 2, 5);
            g.Lava(43, 1, 5);
            g.Platform(43, 0, 5);
            g.Platform(52, 5, 3);
            g.Wall(56, 1, 4);
            g.Block(60, 6, 2, 38);
            g.Block(77, 6, 2, 32);
            g.Platform(62, 13, 3);
            g.Platform(71, 21, 3);
            g.Platform(62, 29, 3);
            g.Platform(71, 35, 3);
            g.Platform(60, 38, 31);
            g.Platform(84, 41, 4);
            g.Block(66, 42, 2, 2);

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 8;
            def.title = "ESCAPE";
            def.zone = LevelDefinition.Zone.Depths;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(3, 1);
            def.player2Spawn = V(5, 1);
            def.exit1 = V(85, 39);
            def.exit2 = V(87, 39);
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(13, 1), patrolSpan = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(20, 6), moveRange = 3.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(26, 1), patrolSpan = 4.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(38, 3) },
                new LevelDefinition.EnemyPlacement { type = ET.Eye, position = V(50, 6) },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(46, 5), moveRange = 2.0f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(64, 16), moveRange = 1.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(73, 24), moveRange = 1.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(64, 32), moveRange = 1.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(70, 39), patrolSpan = 6.0f },
            };
            def.leverGroups = new[]
            {
                new LevelDefinition.LeverBridgeGroup { lever = V(38, 3), bridgeTiles = new[] { V(43, 2), V(44, 2), V(45, 2), V(46, 2), V(47, 2) } },
            };
            def.powerUpOne = new[] { V(8, 1) };
            def.powerUpTwo = new[] { V(10, 1) };
            def.hearts = new[] { V(37, 3), V(52, 6), V(66, 39) };
            def.crystals = new[] { V(6, 5), V(18, 6), V(71, 22) };
            def.checkpoints = new[] { V(24, 1), V(38, 3) };
            def.movingPlatforms = new[]
            {
                new LevelDefinition.MovingPlatformPlacement { from = V(68, 3), to = V(68, 37), speed = 1.6f, isLavaRiserRide = true },
            };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(92, 46);
            return def;
        }
    }
}
