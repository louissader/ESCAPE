using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Converts the hand-built Level 1-3 scenes into LevelDefinition assets
    /// (so all 8 levels share the LevelBuilder pipeline), applying the
    /// difficulty retunes: L2 ghosts 9→5, L3 ghosts 17→8, L1 bat becomes
    /// passive, plus hearts/crystals/checkpoint per level and L1 tutorial
    /// zones. Snapshots entity counts to JSON for rebuild verification.
    /// </summary>
    public static class LevelExtractor
    {
        [MenuItem("Escape/Extract Levels 1-3")]
        public static void ExtractExistingLevels()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Levels"))
                AssetDatabase.CreateFolder("Assets", "Levels");

            for (int n = 1; n <= 3; n++)
                Extract(n);
            AssetDatabase.SaveAssets();
        }

        static void Extract(int levelNumber)
        {
            string scenePath = $"Assets/Scenes/Level {levelNumber}.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Tilemap tilemap = Object.FindObjectsOfType<Tilemap>()
                .OrderByDescending(t => CountTiles(t))
                .FirstOrDefault();
            if (tilemap == null)
            {
                Debug.LogError($"[LevelExtractor] no tilemap in {scenePath}");
                return;
            }

            tilemap.CompressBounds();
            BoundsInt bounds = tilemap.cellBounds;
            Vector2Int offset = new Vector2Int(bounds.xMin, bounds.yMin);
            int width = bounds.size.x, height = bounds.size.y;

            var grid = new LevelGridBuilder(width, height);
            int tileCount = 0;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (tilemap.HasTile(new Vector3Int(x + offset.x, y + offset.y, 0)))
                    {
                        grid.Platform(x, y, 1);
                        tileCount++;
                    }

            Vector2Int Tile(Vector3 world)
            {
                Vector3Int cell = tilemap.layoutGrid.WorldToCell(world);
                return new Vector2Int(cell.x - offset.x, cell.y - offset.y);
            }

            var def = ScriptableObject.CreateInstance<LevelDefinition>();
            def.levelNumber = levelNumber;
            def.title = levelNumber == 1 ? "Awakening" : levelNumber == 2 ? "The Crossing" : "Ghost Gallery";
            def.zone = LevelDefinition.Zone.Grotto;
            def.tileGrid = grid.Emit();

            var p1 = GameObject.Find("Player1");
            var p2 = GameObject.Find("Player2");
            def.player1Spawn = p1 != null ? Tile(p1.transform.position) : new Vector2Int(2, 1);
            def.player2Spawn = p2 != null ? Tile(p2.transform.position) : new Vector2Int(4, 1);

            // Exits.
            var exits = GameObject.FindGameObjectsWithTag("Dragon")
                .Select(g => Tile(g.transform.position)).ToList();
            def.exit1 = exits.Count > 0 ? exits[0] : new Vector2Int(width - 4, 1);
            def.exit2 = exits.Count > 1 ? exits[1] : def.exit1 + Vector2Int.right * 2;

            // Enemies with retunes.
            var enemies = new List<LevelDefinition.EnemyPlacement>();
            var bats = Object.FindObjectsOfType<IndieMarc.Platformer.Bat>();
            foreach (var bat in bats)
                enemies.Add(new LevelDefinition.EnemyPlacement
                {
                    type = levelNumber == 1 ? LevelDefinition.EnemyType.BatPassive
                                            : LevelDefinition.EnemyType.Bat,
                    position = Tile(bat.transform.position),
                    moveRange = bat.moveRange,
                });

            var ghosts = Object.FindObjectsOfType<IndieMarc.Platformer.Ghost>()
                .OrderBy(g => g.transform.position.x).ToList();
            int keepGhosts = levelNumber == 2 ? 5 : levelNumber == 3 ? 8 : ghosts.Count;
            foreach (var ghost in Spread(ghosts, keepGhosts))
                enemies.Add(new LevelDefinition.EnemyPlacement
                {
                    type = LevelDefinition.EnemyType.Ghost,
                    position = Tile(ghost.transform.position),
                    moveRange = ghost.moveRange,
                });

            // Legacy slime prefab instances (no behavior component to find —
            // identify by name).
            foreach (var go in Object.FindObjectsOfType<GameObject>()
                     .Where(g => g.name.StartsWith("Slime")))
                enemies.Add(new LevelDefinition.EnemyPlacement
                {
                    type = LevelDefinition.EnemyType.Slime,
                    position = Tile(go.transform.position),
                    patrolSpan = 3,
                });
            def.enemies = enemies.ToArray();

            // Lever → bridges.
            var leverGroups = new List<LevelDefinition.LeverBridgeGroup>();
            var bridges = Object.FindObjectsOfType<IndieMarc.Platformer.Bridge>();
            foreach (var lever in Object.FindObjectsOfType<IndieMarc.Platformer.Lever>())
                leverGroups.Add(new LevelDefinition.LeverBridgeGroup
                {
                    lever = Tile(lever.transform.position),
                    bridgeTiles = bridges.Select(b => Tile(b.transform.position)).ToArray(),
                });
            def.leverGroups = leverGroups.ToArray();

            def.powerUpOne = Object.FindObjectsOfType<PowerUpOne>()
                .Select(p => Tile(p.transform.position)).ToArray();
            def.powerUpTwo = Object.FindObjectsOfType<PowerUpTwo>()
                .Select(p => Tile(p.transform.position)).ToArray();

            // Retune additions: hearts near the midpoint, 3 crystals spread
            // over standable ground off the direct line, 1 checkpoint.
            var standables = StandableTiles(def).ToList();
            def.hearts = PickSpread(standables, 2, width / 3, offsetY: 1);
            def.crystals = PickSpread(standables, 3, width / 4, offsetY: 2);
            def.checkpoints = new[] { NearestStandable(standables, new Vector2Int(width / 2, height / 2)) };

            def.boundsMin = Vector2Int.zero;
            def.boundsMax = new Vector2Int(width, height);

            string assetPath = $"Assets/Levels/Level{levelNumber}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<LevelDefinition>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(def, existing);
                Object.DestroyImmediate(def);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(def, assetPath);
            }

            // Snapshot for rebuild verification.
            var snapshot = new Snapshot
            {
                tileCount = tileCount,
                enemyCount = enemies.Count,
                exitCount = 2,
                leverCount = leverGroups.Count,
            };
            System.IO.Directory.CreateDirectory("Assets/Levels/snapshots");
            System.IO.File.WriteAllText($"Assets/Levels/snapshots/level{levelNumber}.json",
                JsonUtility.ToJson(snapshot, true));

            Debug.Log($"[LevelExtractor] L{levelNumber}: {width}x{height}, {tileCount} tiles, " +
                      $"{enemies.Count} enemies, {leverGroups.Count} levers");
        }

        [System.Serializable]
        class Snapshot
        {
            public int tileCount;
            public int enemyCount;
            public int exitCount;
            public int leverCount;
        }

        static int CountTiles(Tilemap t)
        {
            t.CompressBounds();
            int count = 0;
            foreach (Vector3Int pos in t.cellBounds.allPositionsWithin)
                if (t.HasTile(pos))
                    count++;
            return count;
        }

        static IEnumerable<T> Spread<T>(List<T> items, int keep)
        {
            if (items.Count <= keep)
                return items;
            var result = new List<T>();
            for (int i = 0; i < keep; i++)
                result.Add(items[Mathf.RoundToInt(i * (items.Count - 1) / (float)(keep - 1))]);
            return result.Distinct();
        }

        static IEnumerable<Vector2Int> StandableTiles(LevelDefinition def)
        {
            for (int x = 1; x < def.GridWidth - 1; x++)
                for (int y = 1; y < def.GridHeight - 1; y++)
                    if (def.TileAt(x, y) == '.' && def.TileAt(x, y - 1) == '#')
                        yield return new Vector2Int(x, y);
        }

        static Vector2Int[] PickSpread(List<Vector2Int> standables, int count, int stride, int offsetY)
        {
            var picks = new List<Vector2Int>();
            if (standables.Count == 0)
                return picks.ToArray();
            var byX = standables.OrderBy(s => s.x).ToList();
            for (int i = 1; i <= count; i++)
            {
                int targetX = i * stride;
                Vector2Int best = byX.OrderBy(s => Mathf.Abs(s.x - targetX)).First();
                picks.Add(best + Vector2Int.up * offsetY);
            }
            return picks.Distinct().ToArray();
        }

        static Vector2Int NearestStandable(List<Vector2Int> standables, Vector2Int target)
        {
            return standables.Count == 0
                ? target
                : standables.OrderBy(s => (s - target).sqrMagnitude).First();
        }
    }
}
