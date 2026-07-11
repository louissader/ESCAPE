using UnityEngine;

/// <summary>Brisk patroller, full contact damage. Clean rewrite of the old
/// inverted-damage SkeletonBehavior (deleted in Phase 0).</summary>
[RequireComponent(typeof(GroundPatrol))]
public class SkeletonEnemy : MonoBehaviour, IDamageableEnemy
{
    public float contactDamage = 30f;

    void Start()
    {
        GetComponent<GroundPatrol>().speed = 2f;
    }

    void OnCollisionEnter2D(Collision2D c) { EnemyContact.Damage(c, contactDamage); }
    void OnCollisionStay2D(Collision2D c) { EnemyContact.Damage(c, contactDamage); }

    public void Die()
    {
        AudioManager.PlaySfx("enemy_poof");
        Destroy(gameObject);
    }
}
