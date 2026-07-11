using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static per-level registry of spawn points and activated checkpoints.
/// Respawn rule (design review): a dead character returns at the activated
/// checkpoint NEAREST THE SURVIVING character — not the furthest-back one —
/// so co-op camera framing and swap-mode pacing stay intact.
/// </summary>
public static class CheckpointManager
{
    static readonly Dictionary<int, Vector3> levelStarts = new Dictionary<int, Vector3>();
    static readonly List<Vector3> activated = new List<Vector3>();

    /// <summary>Players call this from Start; also clears stale state on level 1st register.</summary>
    public static void RegisterLevelStart(int playerId, Vector3 position)
    {
        if (levelStarts.ContainsKey(playerId) == false && levelStarts.Count == 0)
            activated.Clear();
        levelStarts[playerId] = position;
    }

    /// <summary>Called on scene unload/load boundaries (GameState.Start).</summary>
    public static void ResetForLevel()
    {
        levelStarts.Clear();
        activated.Clear();
    }

    public static void Activate(Vector3 position)
    {
        if (!activated.Contains(position))
            activated.Add(position);
    }

    public static Vector3 GetRespawnPosition(int deadPlayerId, Vector3 survivorPosition, bool hasSurvivor)
    {
        if (hasSurvivor && activated.Count > 0)
        {
            Vector3 best = activated[0];
            float bestDist = float.MaxValue;
            foreach (Vector3 cp in activated)
            {
                float d = (cp - survivorPosition).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = cp;
                }
            }
            return best;
        }

        if (levelStarts.TryGetValue(deadPlayerId, out Vector3 start))
            return start;
        return survivorPosition;
    }
}
