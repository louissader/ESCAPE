using System.Collections;
using UnityEngine;

/// <summary>
/// Periodic arc hopper: leaps toward its facing direction every hopInterval,
/// turning at walls. Blocks corridors with rhythm the player must read.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class FrogEnemy : MonoBehaviour, IDamageableEnemy
{
    public float hopInterval = 2f;
    public float hopForceX = 3f;
    public float hopForceY = 6f;
    public float contactDamage = 20f;
    public LayerMask groundMask = ~0;

    Rigidbody2D body;
    BoxCollider2D box;
    int direction = 1;

    void Start()
    {
        body = GetComponent<Rigidbody2D>();
        box = GetComponent<BoxCollider2D>();
        body.freezeRotation = true;
        StartCoroutine(HopLoop());
    }

    IEnumerator HopLoop()
    {
        var wait = new WaitForSeconds(hopInterval);
        while (true)
        {
            yield return wait;
            if (!IsGrounded())
                continue;

            if (WallAhead())
                direction = -direction;

            body.velocity = new Vector2(direction * hopForceX, hopForceY);
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * direction;
            transform.localScale = scale;
        }
    }

    bool IsGrounded()
    {
        Bounds bounds = box.bounds;
        return Physics2D.Raycast(bounds.center, Vector2.down, bounds.extents.y + 0.1f, groundMask);
    }

    bool WallAhead()
    {
        Bounds bounds = box.bounds;
        Vector2 origin = new Vector2(
            direction > 0 ? bounds.max.x + 0.05f : bounds.min.x - 0.05f, bounds.center.y);
        RaycastHit2D hit = Physics2D.Raycast(origin, new Vector2(direction, 0), 1f, groundMask);
        return hit.collider != null && !hit.collider.isTrigger;
    }

    void OnCollisionEnter2D(Collision2D c) { EnemyContact.Damage(c, contactDamage); }
    void OnCollisionStay2D(Collision2D c) { EnemyContact.Damage(c, contactDamage); }

    public void Die()
    {
        AudioManager.PlaySfx("enemy_poof");
        Destroy(gameObject);
    }
}
