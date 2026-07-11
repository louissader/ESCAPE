using UnityEngine;
using IndieMarc.Platformer;

/// <summary>
/// Shared projectile for players and enemies, disambiguated by owner (B14 —
/// previously bats were unkillable and enemy shots dealt no damage because
/// this trigger only handled Ghost and Bat waited for collision events that
/// triggers never send).
///
/// Player-owned: kills anything implementing IDamageableEnemy.
/// Enemy-owned: deals damage to players.
/// Both: die against solid world geometry.
/// </summary>
public class Fireball : MonoBehaviour
{
    public enum Owner { Player, Enemy }

    public Owner owner = Owner.Player;
    public float speed = 12f;
    public float lifetime = 5f;
    public float enemyDamage = 20f;

    private Rigidbody2D rb;

    void Start()
    {
        Destroy(gameObject, lifetime);

        rb = GetComponent<Rigidbody2D>();
        // Spawners that aim (bats) set velocity themselves; the player's
        // straight shot inherits facing from spawn rotation.
        if (rb != null && rb.velocity.sqrMagnitude < 0.01f)
            rb.velocity = transform.right * speed;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.isTrigger)
            return; // pickups, levers, other projectiles

        if (owner == Owner.Player)
        {
            IDamageableEnemy enemy = other.GetComponent<IDamageableEnemy>();
            if (enemy != null)
            {
                enemy.Die();
                Explode();
                return;
            }
            // Never hurts players (including the shooter standing at spawn).
            if (other.CompareTag("Player") || other.CompareTag("PlayerTwo"))
                return;
        }
        else
        {
            PlayerCharacterOne p1 = other.GetComponent<PlayerCharacterOne>();
            if (p1 != null)
            {
                p1.TakeDamage(enemyDamage);
                Explode();
                return;
            }
            PlayerCharacterTwo p2 = other.GetComponent<PlayerCharacterTwo>();
            if (p2 != null)
            {
                p2.TakeDamage(enemyDamage);
                Explode();
                return;
            }
            if (other.GetComponent<IDamageableEnemy>() != null)
                return; // enemy shots pass through enemies
        }

        // Solid world geometry stops everything else.
        Explode();
    }

    void Explode()
    {
        AudioManager.PlaySfx("fireball_impact");
        // Phase 4 hooks impact particles here.
        Destroy(gameObject);
    }
}
