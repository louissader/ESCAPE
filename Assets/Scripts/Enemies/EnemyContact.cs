using UnityEngine;
using IndieMarc.Platformer;

/// <summary>Shared contact-damage helper (player i-frames throttle repeats).</summary>
public static class EnemyContact
{
    public static void Damage(Collision2D collision, float amount)
    {
        if (collision.gameObject.CompareTag("Player"))
            collision.gameObject.GetComponent<PlayerCharacterOne>()?.TakeDamage(amount);
        else if (collision.gameObject.CompareTag("PlayerTwo"))
            collision.gameObject.GetComponent<PlayerCharacterTwo>()?.TakeDamage(amount);
    }
}
