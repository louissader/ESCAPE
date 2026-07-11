using UnityEngine;
using IndieMarc.Platformer;

/// <summary>
/// Slow homing flyer: drifts toward the nearest living, non-parked player in
/// range; returns to spawn otherwise. Parked swap-mode characters are never
/// targeted.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class EyeEnemy : MonoBehaviour, IDamageableEnemy
{
    public float speed = 1.6f;
    public float aggroRange = 8f;
    public float contactDamage = 20f;

    Rigidbody2D body;
    Vector3 spawn;

    void Start()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        spawn = transform.position;
    }

    void FixedUpdate()
    {
        Vector3 target = spawn;
        float best = aggroRange;

        PlayerCharacterOne p1 = PlayerCharacterOne.Get(1);
        if (p1 != null && !p1.IsDead() && p1.gameObject.activeSelf && !SwapModeController.IsParked(1))
        {
            float d = Vector3.Distance(transform.position, p1.transform.position);
            if (d < best) { best = d; target = p1.transform.position; }
        }
        PlayerCharacterTwo p2 = PlayerCharacterTwo.Get(2);
        if (p2 != null && !p2.IsDead() && p2.gameObject.activeSelf && !SwapModeController.IsParked(2))
        {
            float d = Vector3.Distance(transform.position, p2.transform.position);
            if (d < best) { best = d; target = p2.transform.position; }
        }

        Vector3 next = Vector3.MoveTowards(transform.position, target, speed * Time.fixedDeltaTime);
        body.MovePosition(next);

        Vector3 scale = transform.localScale;
        if (Mathf.Abs(target.x - transform.position.x) > 0.05f)
            scale.x = Mathf.Abs(scale.x) * (target.x > transform.position.x ? 1 : -1);
        transform.localScale = scale;
    }

    void OnCollisionEnter2D(Collision2D c) { EnemyContact.Damage(c, contactDamage); }
    void OnCollisionStay2D(Collision2D c) { EnemyContact.Damage(c, contactDamage); }

    public void Die()
    {
        AudioManager.PlaySfx("enemy_poof");
        Destroy(gameObject);
    }
}
