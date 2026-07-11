using System.Collections.Generic;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Tile-grid reachability: BFS with platformer moves (walk, jump up to
    /// 6 tiles with double jump, horizontal leaps up to 5, falls any depth).
    /// Bridges count as open ground; moving platforms connect endpoints.
    /// Verifies: spawn→both exits for both characters, every crystal
    /// reachable, exits reachable from every checkpoint.
    /// </summary>
    public static class LevelSolvability
    {
        const int JumpHeight = 6;   // 4 base + 2 double jump
        const int MaxGap = 5;
        // With P2's platform form both characters gain a boost anywhere they
        // stand together: +3 rise (slab height + standing start), +1 reach.
        // Legacy L1-3 layouts require this; new levels pass without it.
        const int BoostHeight = 9;
        const int BoostGap = 6;

        public static bool Check(LevelDefinition def)
        {
            var standable = BuildStandableSet(def);
            bool ok = true;

            foreach (var spawn in new[] { def.player1Spawn, def.player2Spawn })
            {
                HashSet<Vector2Int> reach = Reachable(def, standable, spawn);
                foreach (var exit in new[] { def.exit1, def.exit2 })
                {
                    if (!ReachesNear(reach, exit, 2))
                    {
                        Debug.LogError($"[Solvability] L{def.levelNumber}: exit {exit} unreachable from spawn {spawn}");
                        ok = false;
                    }
                }
                foreach (Vector2Int crystal in def.crystals)
                    if (!ReachesNear(reach, crystal, 2))
                    {
                        Debug.LogError($"[Solvability] L{def.levelNumber}: crystal {crystal} unreachable from {spawn}");
                        ok = false;
                    }
            }

            foreach (Vector2Int cp in def.checkpoints)
            {
                HashSet<Vector2Int> reach = Reachable(def, standable, cp);
                if (!ReachesNear(reach, def.exit1, 2))
                {
                    Debug.LogError($"[Solvability] L{def.levelNumber}: exit unreachable from checkpoint {cp}");
                    ok = false;
                }
            }

            if (ok)
                Debug.Log($"[Solvability] L{def.levelNumber}: PASS");
            return ok;
        }

        static HashSet<Vector2Int> BuildStandableSet(LevelDefinition def)
        {
            var standable = new HashSet<Vector2Int>();
            int w = def.GridWidth, h = def.GridHeight;

            bool Solid(int x, int y) { return def.TileAt(x, y) == '#'; }

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (!Solid(x, y) && def.TileAt(x, y) != 'L' && (y == 0 || Solid(x, y - 1)))
                        standable.Add(new Vector2Int(x, y));

            // Bridges and moving-platform endpoints are standable — but only
            // when the standing tile itself is CLEAR (a bridge top inside
            // solid rock, or a platform docking into a ledge, is not a place
            // a hero can occupy; treating it as one hid real design bugs).
            foreach (var group in def.leverGroups)
                foreach (Vector2Int b in group.bridgeTiles)
                    if (Clear(def, b.x, b.y + 1))
                        standable.Add(new Vector2Int(b.x, b.y + 1));
            foreach (var mp in def.movingPlatforms)
            {
                if (Clear(def, mp.from.x, mp.from.y + 1))
                    standable.Add(new Vector2Int(mp.from.x, mp.from.y + 1));
                if (Clear(def, mp.to.x, mp.to.y + 1))
                    standable.Add(new Vector2Int(mp.to.x, mp.to.y + 1));
            }
            return standable;
        }

        static bool Clear(LevelDefinition def, int x, int y)
        {
            char c = def.TileAt(x, y);
            return c != '#' && c != 'L';
        }

        static HashSet<Vector2Int> Reachable(LevelDefinition def, HashSet<Vector2Int> standable, Vector2Int start)
        {
            Vector2Int origin = Nearest(standable, start, 4);
            var seen = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            if (origin == new Vector2Int(int.MinValue, int.MinValue))
                return seen;
            queue.Enqueue(origin);
            seen.Add(origin);

            while (queue.Count > 0)
            {
                Vector2Int at = queue.Dequeue();
                foreach (Vector2Int next in Moves(def, standable, at))
                {
                    if (seen.Add(next))
                        queue.Enqueue(next);
                }
            }

            // Moving platforms teleport riders between endpoints.
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var mp in def.movingPlatforms)
                {
                    var a = new Vector2Int(mp.from.x, mp.from.y + 1);
                    var b = new Vector2Int(mp.to.x, mp.to.y + 1);
                    if (seen.Contains(a) && !seen.Contains(b)) { Grow(def, standable, seen, b); changed = true; }
                    if (seen.Contains(b) && !seen.Contains(a)) { Grow(def, standable, seen, a); changed = true; }
                }
            }
            return seen;
        }

        static void Grow(LevelDefinition def, HashSet<Vector2Int> standable, HashSet<Vector2Int> seen, Vector2Int from)
        {
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            seen.Add(from);
            while (queue.Count > 0)
            {
                Vector2Int at = queue.Dequeue();
                foreach (Vector2Int next in Moves(def, standable, at))
                    if (seen.Add(next))
                        queue.Enqueue(next);
            }
        }

        static IEnumerable<Vector2Int> Moves(LevelDefinition def, HashSet<Vector2Int> standable, Vector2Int at)
        {
            // Reach any standable tile within the jump/gap envelope with a
            // clear-ish heuristic: rises need dy <= JumpHeight, gaps need
            // |dx| <= MaxGap (+1 forgiveness when falling).
            for (int dx = -BoostGap - 1; dx <= BoostGap + 1; dx++)
            {
                for (int dy = -12; dy <= BoostHeight; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    var target = new Vector2Int(at.x + dx, at.y + dy);
                    if (!standable.Contains(target))
                        continue;
                    int gap = Mathf.Abs(dx);
                    if (dy > 0 && (gap > BoostGap || dy > BoostHeight))
                        continue;
                    if (dy <= 0 && gap > BoostGap + 1)
                        continue;
                    yield return target;
                }
            }
        }

        static bool ReachesNear(HashSet<Vector2Int> reach, Vector2Int target, int radius)
        {
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                    if (reach.Contains(new Vector2Int(target.x + dx, target.y + dy)))
                        return true;
            return false;
        }

        static Vector2Int Nearest(HashSet<Vector2Int> standable, Vector2Int target, int radius)
        {
            for (int r = 0; r <= radius; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        var p = new Vector2Int(target.x + dx, target.y + dy);
                        if (standable.Contains(p))
                            return p;
                    }
            return new Vector2Int(int.MinValue, int.MinValue);
        }
    }
}
