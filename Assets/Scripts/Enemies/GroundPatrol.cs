using UnityEngine;

/// <summary>
/// Shared ground patrol: walks until an edge or wall, then turns. Optional
/// span limit keeps the patroller near its spawn.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class GroundPatrol : MonoBehaviour
{
    public float speed = 1f;
    public float spanLimit = 0f;   // 0 = unlimited, else max distance from spawn
    public LayerMask groundMask = ~0;

    Rigidbody2D body;
    BoxCollider2D box;
    Vector3 spawn;
    int direction = 1;

    void Start()
    {
        body = GetComponent<Rigidbody2D>();
        box = GetComponent<BoxCollider2D>();
        body.freezeRotation = true;
        spawn = transform.position;
    }

    void FixedUpdate()
    {
        if (NeedsTurn())
            direction = -direction;

        body.velocity = new Vector2(direction * speed, body.velocity.y);

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;
    }

    bool NeedsTurn()
    {
        Bounds bounds = box.bounds;
        Vector2 front = new Vector2(
            direction > 0 ? bounds.max.x + 0.05f : bounds.min.x - 0.05f,
            bounds.center.y);

        // Wall ahead?
        RaycastHit2D wall = Physics2D.Raycast(front, new Vector2(direction, 0f), 0.1f, groundMask);
        if (wall.collider != null && !wall.collider.isTrigger &&
            !wall.collider.transform.IsChildOf(transform))
            return true;

        // Edge ahead? (no ground below the front foot)
        RaycastHit2D ground = Physics2D.Raycast(front, Vector2.down, bounds.extents.y + 0.3f, groundMask);
        if (ground.collider == null)
            return true;

        // Span limit.
        if (spanLimit > 0.01f)
        {
            float offset = transform.position.x - spawn.x;
            if (offset * direction > spanLimit)
                return true;
        }
        return false;
    }
}
