/// <summary>
/// Implemented by every enemy so projectiles and effects can kill them
/// without type-switching. Phase 6's enemy suite (slime/skeleton/eye/frog)
/// implements this too.
/// </summary>
public interface IDamageableEnemy
{
    /// <summary>Kill this enemy (fireball hit). Plays death FX and removes it.</summary>
    void Die();
}
