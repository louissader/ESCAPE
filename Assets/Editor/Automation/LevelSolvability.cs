using System.Collections.Generic;
using UnityEngine;

namespace Escape.EditorAutomation
{
    /// <summary>
    /// Tile-grid reachability with PER-HERO movement envelopes measured from
    /// the real physics (playtest 9 audit — the old single envelope assumed
    /// a rise-9/gap-6 boost for everyone, everywhere, so PASS proved almost
    /// nothing):
    ///
    ///   EMBER  jump_strength 10, apex ~3.1 units with LinearDrag 1 and the
    ///          1.5x fall gravity -> rise 3, gap 5.
    ///   WISP   jump_strength 15, apex ~5.9 -> rise 5, gap 6.
    ///   BOOST  Ember standing on Wisp's crystal slab (top ~1 unit above the
    ///          Wisp): rise 5, gap 6 — and only where BOTH can stand
    ///          (approximated as Wisp's reachable set) and only after the
    ///          blue orb is itself reachable by Wisp. Wisp cannot boost
    ///          herself.
    ///
    /// Rises also pay for distance: a jump's apex sits mid-arc, so a rising
    /// move must satisfy gap + rise <= gap-limit + 1 (e.g. Ember can rise 3
    /// across a 3-gap, but not across a 5-gap).
    ///
    /// Win rule (Playtest 5): EMBER must reach door 1 and WISP door 2 — from
    /// their spawns and from every checkpoint. Crystals need only some hero.
    /// Bridges count as open ground; moving platforms connect endpoints.
    /// </summary>
    public static class LevelSolvability
    {
        struct Envelope
        {
            public int rise;
            public int gap;
            public Envelope(int rise, int gap) { this.rise = rise; this.gap = gap; }
        }

        static readonly Envelope EmberEnv = new Envelope(3, 5);
        static readonly Envelope WispEnv = new Envelope(5, 6);
        static readonly Envelope BoostEnv = new Envelope(5, 6);

        public static bool Check(LevelDefinition def)
        {
            var standable = BuildStandableSet(def);
            bool ok = true;

            // WISP first: her reach decides where the boost can exist.
            HashSet<Vector2Int> wispReach = Reachable(def, standable, def.player2Spawn, WispEnv, null);
            bool boostAvailable = BoostAvailable(def, wispReach);
            HashSet<Vector2Int> emberReach = Reachable(def, standable, def.player1Spawn, EmberEnv,
                boostAvailable ? wispReach : null);

            if (!ReachesNear(emberReach, def.exit1, 2))
            {
                Debug.LogError($"[Solvability] L{def.levelNumber}: EMBER (rise {EmberEnv.rise}/gap {EmberEnv.gap}" +
                    $"{(boostAvailable ? " +boost" : "")}) cannot reach door 1 {def.exit1} from spawn {def.player1Spawn}");
                ok = false;
            }
            if (!ReachesNear(wispReach, def.exit2, 2))
            {
                Debug.LogError($"[Solvability] L{def.levelNumber}: WISP (rise {WispEnv.rise}/gap {WispEnv.gap}) " +
                    $"cannot reach door 2 {def.exit2} from spawn {def.player2Spawn}");
                ok = false;
            }
            foreach (Vector2Int crystal in def.crystals)
                if (!ReachesNear(emberReach, crystal, 2) && !ReachesNear(wispReach, crystal, 2))
                {
                    Debug.LogError($"[Solvability] L{def.levelNumber}: crystal {crystal} unreachable by either hero");
                    ok = false;
                }

            // After a respawn both heroes continue from the checkpoint, so
            // each hero's own door must be reachable from there too.
            foreach (Vector2Int cp in def.checkpoints)
            {
                HashSet<Vector2Int> wispCp = Reachable(def, standable, cp, WispEnv, null);
                HashSet<Vector2Int> emberCp = Reachable(def, standable, cp, EmberEnv,
                    BoostAvailable(def, wispCp) ? wispCp : null);

                if (!ReachesNear(emberCp, def.exit1, 2))
                {
                    Debug.LogError($"[Solvability] L{def.levelNumber}: EMBER cannot reach door 1 {def.exit1} from checkpoint {cp}");
                    ok = false;
                }
                if (!ReachesNear(wispCp, def.exit2, 2))
                {
                    Debug.LogError($"[Solvability] L{def.levelNumber}: WISP cannot reach door 2 {def.exit2} from checkpoint {cp}");
                    ok = false;
                }
            }

            if (ok)
                Debug.Log($"[Solvability] L{def.levelNumber}: PASS (per-hero envelopes" +
                    $"{(boostAvailable ? ", boost available" : "")})");
            return ok;
        }

        /// <summary>The slab boost exists only when the level HAS a blue orb
        /// and Wisp can actually get to one (the orb floats 1.5 above its
        /// tile, within the near-radius).</summary>
        static bool BoostAvailable(LevelDefinition def, HashSet<Vector2Int> wispReach)
        {
            foreach (Vector2Int orb in def.powerUpTwo)
                if (ReachesNear(wispReach, orb, 2))
                    return true;
            return false;
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

        /// <summary>BFS under one hero's envelope. From any tile inside
        /// boostTiles (the partner's reach), the stronger boost envelope
        /// also applies — Ember can climb from wherever Wisp can stand.</summary>
        static HashSet<Vector2Int> Reachable(LevelDefinition def, HashSet<Vector2Int> standable,
            Vector2Int start, Envelope env, HashSet<Vector2Int> boostTiles)
        {
            Vector2Int origin = Nearest(standable, start, 4);
            var seen = new HashSet<Vector2Int>();
            if (origin == new Vector2Int(int.MinValue, int.MinValue))
                return seen;

            var queue = new Queue<Vector2Int>();
            queue.Enqueue(origin);
            seen.Add(origin);
            while (queue.Count > 0)
                Step(def, standable, seen, queue, env, boostTiles);

            // Moving platforms teleport riders between endpoints.
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var mp in def.movingPlatforms)
                {
                    var a = new Vector2Int(mp.from.x, mp.from.y + 1);
                    var b = new Vector2Int(mp.to.x, mp.to.y + 1);
                    if (seen.Contains(a) && !seen.Contains(b)) { Grow(def, standable, seen, b, env, boostTiles); changed = true; }
                    if (seen.Contains(b) && !seen.Contains(a)) { Grow(def, standable, seen, a, env, boostTiles); changed = true; }
                }
            }
            return seen;
        }

        static void Grow(LevelDefinition def, HashSet<Vector2Int> standable, HashSet<Vector2Int> seen,
            Vector2Int from, Envelope env, HashSet<Vector2Int> boostTiles)
        {
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            seen.Add(from);
            while (queue.Count > 0)
                Step(def, standable, seen, queue, env, boostTiles);
        }

        static void Step(LevelDefinition def, HashSet<Vector2Int> standable, HashSet<Vector2Int> seen,
            Queue<Vector2Int> queue, Envelope env, HashSet<Vector2Int> boostTiles)
        {
            Vector2Int at = queue.Dequeue();
            foreach (Vector2Int next in Moves(standable, at, env))
                if (seen.Add(next))
                    queue.Enqueue(next);
            if (boostTiles != null && boostTiles.Contains(at))
                foreach (Vector2Int next in Moves(standable, at, BoostEnv))
                    if (seen.Add(next))
                        queue.Enqueue(next);
        }

        static IEnumerable<Vector2Int> Moves(HashSet<Vector2Int> standable, Vector2Int at, Envelope env)
        {
            for (int dx = -env.gap - 1; dx <= env.gap + 1; dx++)
            {
                for (int dy = -12; dy <= env.rise; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    var target = new Vector2Int(at.x + dx, at.y + dy);
                    if (!standable.Contains(target))
                        continue;
                    int gap = Mathf.Abs(dx);
                    if (dy > 0)
                    {
                        // Rising: both limits, plus the arc trade-off — the
                        // apex is spent mid-jump, so height costs distance.
                        if (gap > env.gap || dy > env.rise || gap + dy > env.gap + 1)
                            continue;
                    }
                    else if (gap > env.gap + 1)
                    {
                        continue; // falling: +1 forgiveness, any depth
                    }
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
