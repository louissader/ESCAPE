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
            var g = new LevelGridBuilder(84, 24);
            g.Floor(0);
            g.Clear(26, 0, 6, 1);            // pit A (bridged)
            g.Lava(56, 1, 8); g.Platform(56, 0, 8);  // lava pit B on floor
            // Hall 1: gentle intro, ceiling deco.
            g.Platform(8, 4, 6);             // crystal shelf (jump 4)
            g.Stairs(18, 1, 3);              // up to lever 1 ledge
            g.Platform(22, 4, 4);            // lever 1 ledge
            // Hall 2 (after pit A): patrol floor + upper walk.
            g.Platform(34, 5, 10);           // skeleton patrol balcony
            g.Stairs(32, 1, 2);              // access to balcony (paired route)
            g.Platform(46, 3, 4);            // lever 2 ledge
            g.Platform(50, 7, 5);            // crystal ledge high (double jump from balcony)
            // Hall 3: approach to lava pit B, exit plateau.
            g.Stairs(66, 1, 3);
            g.Platform(72, 4, 12);           // exit plateau
            g.Wall(83, 0, 24); g.Wall(0, 0, 24); // side walls

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 4;
            def.title = "Skeleton Halls";
            def.zone = LevelDefinition.Zone.Grotto;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(3, 1);
            def.player2Spawn = V(5, 1);
            def.exit1 = V(78, 5);
            def.exit2 = V(80, 5);
            def.powerUpOne = new[] { V(10, 5) };      // shoot early — skeletons need it
            def.powerUpTwo = new[] { V(12, 1) };
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(14, 1), patrolSpan = 5 },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(38, 6), patrolSpan = 4 },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(40, 1), patrolSpan = 6 },
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(50, 1), patrolSpan = 4 },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(62, 6), moveRange = 2.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(74, 5), patrolSpan = 4 },
            };
            def.leverGroups = new[]
            {
                // Lever 1 (hall 1 ledge) opens bridge A over pit A.
                new LevelDefinition.LeverBridgeGroup
                {
                    lever = V(23, 5),
                    bridgeTiles = new[] { V(26, 0), V(28, 0), V(30, 0) },
                },
                // Lever 2 (hall 2) opens bridge B over the lava pit.
                new LevelDefinition.LeverBridgeGroup
                {
                    lever = V(47, 4),
                    bridgeTiles = new[] { V(56, 2), V(58, 2), V(60, 2), V(62, 2) },
                },
            };
            def.hearts = new[] { V(33, 6), V(65, 4) };
            def.crystals = new[] { V(10, 5), V(52, 8), V(44, 6) };
            def.checkpoints = new[] { V(34, 6) };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(84, 24);
            return def;
        }

        // ------------------------------------------------------------------
        // L5 "The Depths" — new zone. Eyes + moving platforms; a vertical
        // shaft climb with a mid checkpoint. Chambers stay within 40x22.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel5()
        {
            var g = new LevelGridBuilder(72, 36);
            g.Floor(0);
            g.Wall(0, 0, 36); g.Wall(71, 0, 36);
            // Entry corridor with eye ambush.
            g.Platform(10, 4, 5);
            g.Lava(20, 1, 6); g.Platform(20, 0, 6);
            // Shaft (x 30-46): climbing platforms zig-zag; moving platform
            // spans the tall middle jump both directions (paired route).
            g.Platform(30, 3, 5);
            g.Platform(40, 6, 5);
            g.Platform(30, 9, 5);
            g.Platform(40, 12, 5);
            g.Platform(30, 15, 5);   // checkpoint shelf
            g.Platform(40, 18, 5);
            g.Platform(30, 21, 5);
            // Upper gallery to exit.
            g.Platform(46, 24, 26);
            g.Platform(60, 28, 6);   // crystal ledge above gallery
            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 5;
            def.title = "The Depths";
            def.zone = LevelDefinition.Zone.Depths;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(3, 1);
            def.player2Spawn = V(5, 1);
            def.exit1 = V(66, 25);
            def.exit2 = V(68, 25);
            def.powerUpOne = new[] { V(11, 5) };
            def.powerUpTwo = new[] { V(13, 1) };
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Eye, position = V(16, 6) },
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(33, 4), patrolSpan = 2 },
                new LevelDefinition.EnemyPlacement { type = ET.Eye, position = V(37, 14) },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(43, 20), moveRange = 2f },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(52, 27), moveRange = 3f },
                new LevelDefinition.EnemyPlacement { type = ET.Eye, position = V(58, 26) },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(52, 25), patrolSpan = 8 },
            };
            def.movingPlatforms = new[]
            {
                // Shaft escalator: connects mid-shaft shelf to the gallery.
                new LevelDefinition.MovingPlatformPlacement { from = V(36, 22), to = V(44, 24), speed = 2f },
            };
            def.hearts = new[] { V(31, 16), V(48, 25) };
            def.crystals = new[] { V(23, 3), V(42, 13), V(62, 29) };
            def.checkpoints = new[] { V(32, 16) };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(72, 36);
            return def;
        }

        // ------------------------------------------------------------------
        // L6 "Frogmarch" — Depths. Frog-timed corridors over lava; P2
        // platform doubles as a fireball shield. Every boost is paired with
        // stairs for P2.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel6()
        {
            var g = new LevelGridBuilder(96, 26);
            g.Floor(0);
            g.Wall(0, 0, 26); g.Wall(95, 0, 26);
            // Corridor 1: lava stretch with island hops, frogs on islands.
            g.Lava(12, 1, 26); g.Platform(12, 0, 26);
            g.Platform(16, 2, 3); g.Platform(23, 2, 3); g.Platform(30, 2, 3);
            // Rest zone + shooting-bat arena with shield spots.
            g.Platform(42, 4, 6);           // high sniper bat ledge
            g.Stairs(46, 1, 2);
            // Corridor 2: higher lava run with staggered hops.
            g.Lava(56, 1, 22); g.Platform(56, 0, 22);
            g.Platform(58, 3, 3); g.Platform(64, 2, 3); g.Platform(70, 4, 3); g.Platform(75, 2, 3);
            // Exit hill with paired stairs.
            g.Stairs(82, 1, 3);
            g.Platform(88, 4, 8);
            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 6;
            def.title = "Frogmarch";
            def.zone = LevelDefinition.Zone.Depths;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(3, 1);
            def.player2Spawn = V(5, 1);
            def.exit1 = V(90, 5);
            def.exit2 = V(92, 5);
            def.powerUpOne = new[] { V(8, 1) };
            def.powerUpTwo = new[] { V(10, 1) };
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(17, 3) },
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(24, 3) },
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(31, 3) },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(45, 8), moveRange = 2f },
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(59, 4) },
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(71, 5) },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(66, 9), moveRange = 2.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Eye, position = V(85, 7) },
            };
            def.hearts = new[] { V(44, 5), V(52, 1), V(80, 1) };
            def.crystals = new[] { V(26, 4), V(47, 6), V(72, 6) };
            def.checkpoints = new[] { V(50, 1) };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(96, 26);
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
            var g = new LevelGridBuilder(100, 30);
            g.Floor(0);
            g.Wall(0, 0, 30); g.Wall(99, 0, 30);
            // The divider slab between lanes (y=8..9), with connector gaps.
            g.Block(14, 8, 70, 2);
            g.Clear(30, 8, 3, 2);    // connector 1
            g.Clear(52, 8, 3, 2);    // connector 2
            g.Clear(72, 8, 3, 2);    // connector 3
            // Upper lane platforms (enter via stairs at start).
            g.Stairs(6, 1, 5);       // up to lane top (y~5→ but slab at 8; stairs reach y=5; add hop)
            g.Platform(16, 12, 6); g.Platform(26, 14, 6); g.Platform(36, 12, 6);
            g.Platform(46, 15, 6); g.Platform(58, 13, 6); g.Platform(68, 15, 6);
            // Low ceilings on upper lane forcing crouch (deco slabs above walk).
            g.Block(40, 16, 4, 1); g.Block(62, 17, 4, 1);
            // Lower lane obstacles: tall steps P2 boosts P1 over, stairs pair them.
            g.Wall(24, 1, 4); g.Stairs(20, 1, 2);
            g.Wall(48, 1, 4); g.Stairs(44, 1, 2);
            g.Wall(78, 1, 5); g.Stairs(84, 1, 3, -1);
            // Reunion chamber + exit plateau.
            g.Platform(88, 6, 12);
            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 7;
            def.title = "Twin Trials";
            def.zone = LevelDefinition.Zone.Depths;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(3, 1);
            def.player2Spawn = V(5, 1);
            def.exit1 = V(92, 7);
            def.exit2 = V(94, 7);
            def.powerUpOne = new[] { V(8, 1) };
            def.powerUpTwo = new[] { V(10, 1) };
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(22, 18), moveRange = 2f },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(34, 4), moveRange = 2.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(40, 13), patrolSpan = 2 },
                new LevelDefinition.EnemyPlacement { type = ET.Eye, position = V(56, 5) },
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(62, 1) },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(64, 20), moveRange = 2f },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(84, 12), moveRange = 3f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(90, 7), patrolSpan = 4 },
            };
            def.hearts = new[] { V(31, 10), V(53, 10), V(86, 1) };
            def.crystals = new[] { V(28, 16), V(48, 17), V(73, 10) };
            def.checkpoints = new[] { V(52, 1) };
            def.boundsMin = V(0, 0);
            def.boundsMax = V(100, 30);
            return def;
        }

        // ------------------------------------------------------------------
        // L8 "ESCAPE" — finale. Everything, then the shared-ride elevator
        // racing the LavaRiser (Swap-safe: both characters board one
        // platform). Checkpoints ONLY before the riser trigger.
        // ------------------------------------------------------------------
        static LevelDefinition CreateLevel8()
        {
            var g = new LevelGridBuilder(90, 44);
            g.Floor(0);
            g.Wall(0, 0, 44); g.Wall(89, 0, 44);
            // Act 1: gauntlet recap on the ground floor.
            g.Lava(14, 1, 8); g.Platform(14, 0, 8);
            g.Platform(16, 3, 2); g.Platform(20, 2, 2);
            g.Wall(30, 1, 4); g.Stairs(26, 1, 2);      // boost + paired stairs
            g.Platform(38, 4, 8);                        // checkpoint plateau
            g.Lava(50, 1, 6); g.Platform(50, 0, 6);
            g.Platform(51, 3, 2);
            // Act 2: approach hall to the shaft (x 60-76 is the shaft).
            g.Platform(58, 3, 4);
            // Shaft walls: interior clear from y=1 up; ride platform inside.
            g.Block(60, 1, 2, 40); g.Block(74, 1, 2, 40);
            g.Clear(60, 1, 2, 4);  // shaft entrance doorway
            // Shaft ledges for bat perches (not path-critical).
            g.Platform(64, 14, 3); g.Platform(69, 22, 3); g.Platform(64, 30, 3);
            // Summit gallery + exit.
            g.Platform(62, 38, 26);
            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = 8;
            def.title = "ESCAPE";
            def.zone = LevelDefinition.Zone.Depths;
            def.tileGrid = g.Emit();
            def.player1Spawn = V(3, 1);
            def.player2Spawn = V(5, 1);
            def.exit1 = V(82, 39);
            def.exit2 = V(84, 39);
            def.powerUpOne = new[] { V(8, 1) };
            def.powerUpTwo = new[] { V(10, 1) };
            def.enemies = new[]
            {
                new LevelDefinition.EnemyPlacement { type = ET.Slime, position = V(24, 1), patrolSpan = 3 },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(41, 5), patrolSpan = 3 },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(46, 8), moveRange = 3f },
                new LevelDefinition.EnemyPlacement { type = ET.Frog, position = V(53, 4) },
                new LevelDefinition.EnemyPlacement { type = ET.Eye, position = V(57, 8) },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(65, 15), moveRange = 1.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(70, 23), moveRange = 1.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Bat, position = V(65, 31), moveRange = 1.5f },
                new LevelDefinition.EnemyPlacement { type = ET.Skeleton, position = V(70, 39), patrolSpan = 6 },
                new LevelDefinition.EnemyPlacement { type = ET.Ghost, position = V(78, 41), moveRange = 3f },
            };
            def.movingPlatforms = new[]
            {
                // THE ride: shaft floor to summit; LavaRiser chases beneath.
                new LevelDefinition.MovingPlatformPlacement
                {
                    from = V(67, 2), to = V(67, 37), speed = 1.6f, isLavaRiserRide = true,
                },
            };
            def.hearts = new[] { V(39, 5), V(58, 4), V(66, 39) };
            def.crystals = new[] { V(17, 4), V(42, 6), V(70, 24) };
            def.checkpoints = new[] { V(40, 5) };  // BEFORE the riser only
            def.boundsMin = V(0, 0);
            def.boundsMax = V(90, 44);
            return def;
        }
    }
}
