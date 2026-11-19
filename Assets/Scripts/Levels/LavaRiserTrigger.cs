using UnityEngine;

/// <summary>Trigger zone that starts the riser + ride when a player crosses it.
/// Must live in its own file: a MonoBehaviour sharing another class's file
/// gets no MonoScript asset, so the scene saved a guid-less script reference
/// and the iOS player crashed deserializing Level 8.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LavaRiserTrigger : MonoBehaviour
{
    public LavaRiser riser;
    public MovingPlatform ride;   // the escape elevator: parked until the chase starts

    void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("PlayerTwo"))
        {
            if (riser != null)
                riser.Activate();
            if (ride != null)
                ride.active = true;
            Destroy(gameObject);
        }
    }
}
