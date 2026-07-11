using UnityEngine;

/// <summary>
/// Declarative level data — the single source every level scene is built
/// from (LevelBuilder editor automation). Tile layout is an ASCII grid;
/// entities are typed placement lists. Shape borrowed from the abandoned
/// SpriteKit port's LevelConfig, adapted to tilemap coordinates.
///
/// ASCII legend (one char per tile, rows top-to-bottom):
///   '#' solid ground   '.' empty          'L' lava row
///   'D' decoration     (everything else reserved)
/// Entity positions are in TILE coordinates (x right, y up from grid bottom).
/// </summary>
[CreateAssetMenu(fileName = "Level", menuName = "Escape/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    public enum Zone { Grotto, Depths }
    public enum EnemyType { Bat, BatPassive, Ghost, Slime, Skeleton, Eye, Frog }

    [Header("Identity")]
    public int levelNumber = 1;
    public string title = "";
    public Zone zone = Zone.Grotto;

    [Header("Tiles (rows top to bottom, see legend)")]
    [TextArea(10, 60)]
    public string tileGrid = "";

    [Header("Players")]
    public Vector2Int player1Spawn;
    public Vector2Int player2Spawn;

    [Header("Exits (two portals; both characters must enter)")]
    public Vector2Int exit1;
    public Vector2Int exit2;

    [Header("Entities")]
    public EnemyPlacement[] enemies = new EnemyPlacement[0];
    public LeverBridgeGroup[] leverGroups = new LeverBridgeGroup[0];
    public Vector2Int[] powerUpOne = new Vector2Int[0];   // P1 fireball unlock
    public Vector2Int[] powerUpTwo = new Vector2Int[0];   // P2 transform unlock
    public Vector2Int[] hearts = new Vector2Int[0];
    public Vector2Int[] crystals = new Vector2Int[0];     // exactly 3 per level
    public Vector2Int[] checkpoints = new Vector2Int[0];
    public MovingPlatformPlacement[] movingPlatforms = new MovingPlatformPlacement[0];

    [Header("Camera bounds (tile coords, polygon corners clockwise)")]
    public Vector2Int boundsMin;
    public Vector2Int boundsMax;

    [System.Serializable]
    public struct EnemyPlacement
    {
        public EnemyType type;
        public Vector2Int position;
        public float moveRange;      // 0 = script default
        public float patrolSpan;     // ground patrollers: half-width in tiles
    }

    [System.Serializable]
    public struct LeverBridgeGroup
    {
        public Vector2Int lever;
        public Vector2Int[] bridgeTiles;   // each gets a bridge segment
    }

    [System.Serializable]
    public struct MovingPlatformPlacement
    {
        public Vector2Int from;
        public Vector2Int to;
        public float speed;          // tiles/second, 0 = default 2
        public bool isLavaRiserRide; // L8 shared elevator
    }

    public int GridWidth
    {
        get
        {
            int width = 0;
            foreach (string row in Rows())
                width = Mathf.Max(width, row.Length);
            return width;
        }
    }

    public int GridHeight
    {
        get
        {
            int height = 0;
            foreach (string _ in Rows())
                height++;
            return height;
        }
    }

    public string[] Rows()
    {
        return tileGrid.Replace("\r", "").Trim('\n').Split('\n');
    }

    /// <summary>Tile char at tile coords (y up from bottom); '.' outside.</summary>
    public char TileAt(int x, int y)
    {
        string[] rows = Rows();
        int rowIndex = rows.Length - 1 - y;
        if (rowIndex < 0 || rowIndex >= rows.Length)
            return '.';
        if (x < 0 || x >= rows[rowIndex].Length)
            return '.';
        return rows[rowIndex][x];
    }
}
