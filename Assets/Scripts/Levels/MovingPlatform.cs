using UnityEngine;

/// <summary>
/// Waypoint platform (from ↔ to). Riders are parented while standing on top
/// so they move with it — the standard sticky-platform pattern.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class MovingPlatform : MonoBehaviour
{
    public Vector3 from;
    public Vector3 to;
    public float speed = 2f;
    public bool active = true;

    Rigidbody2D body;
    Vector3 target;

    void Start()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        if (from == Vector3.zero && to == Vector3.zero)
            from = transform.position;
        target = to;
    }

    void FixedUpdate()
    {
        if (!active)
            return;
        Vector3 next = Vector3.MoveTowards(transform.position, target, speed * Time.fixedDeltaTime);
        body.MovePosition(next);
        if (Vector3.Distance(next, target) < 0.05f)
            target = target == to ? from : to;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsPlayer(collision.gameObject))
            return;
        // Only carry riders standing on TOP (contact normal pointing up at us
        // means the player is above).
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f)
            {
                collision.transform.SetParent(transform);
                return;
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (IsPlayer(collision.gameObject) && collision.transform.parent == transform)
            collision.transform.SetParent(null);
    }

    static bool IsPlayer(GameObject go)
    {
        return go.CompareTag("Player") || go.CompareTag("PlayerTwo");
    }
}
