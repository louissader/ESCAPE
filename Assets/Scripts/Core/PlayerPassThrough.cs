using UnityEngine;
using IndieMarc.Platformer;

/// <summary>
/// The heroes overlap instead of colliding (no standing on heads, no shoving
/// a partner off a ledge). WISP's platform form is a SEPARATE GameObject
/// ("RectanglePlatform", spawned by PlayerCharacterTwo.ToggleForm), not a
/// child of Player2, so its collider is naturally excluded — EMBER can still
/// stand on the transformed WISP (level solvability depends on
/// transform-boosts).
///
/// Physics2D ignore-collision pairs are not persistent across collider
/// disable/enable, so PlayerCharacterTwo.ToggleForm re-applies this after
/// reverting from platform form (which toggles its BoxCollider2D off and on).
/// </summary>
public static class PlayerPassThrough
{
    /// <summary>Idempotent. Called from both players' Start (whichever runs
    /// second finds both registered — registration happens in Awake) and
    /// again whenever WISP reverts from platform form.</summary>
    public static void Apply()
    {
        PlayerCharacterOne p1 = PlayerCharacterOne.Get(1);
        PlayerCharacterTwo p2 = PlayerCharacterTwo.Get(2);
        if (p1 == null || p2 == null)
            return;

        Collider2D[] collidersOne = p1.GetComponentsInChildren<Collider2D>(true);
        Collider2D[] collidersTwo = p2.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D a in collidersOne)
        {
            foreach (Collider2D b in collidersTwo)
                Physics2D.IgnoreCollision(a, b, true);
        }
    }
}
