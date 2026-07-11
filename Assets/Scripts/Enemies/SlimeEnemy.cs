using UnityEngine;
using IndieMarc.Platformer;

/// <summary>The gentlest enemy: slow patrol, light contact damage. L1's teacher.</summary>
[RequireComponent(typeof(GroundPatrol))]
public class SlimeEnemy : MonoBehaviour, IDamageableEnemy
{
    public float contactDamage = 20f;

    void Start()
    {
        GetComponent<GroundPatrol>().speed = 1f;
    }

    void OnCollisionEnter2D(Collision2D c) { EnemyContact.Damage(c, contactDamage); }
    void OnCollisionStay2D(Collision2D c) { EnemyContact.Damage(c, contactDamage); }

    public void Die()
    {
        AudioManager.PlaySfx("enemy_poof");
        Destroy(gameObject);
    }
}
