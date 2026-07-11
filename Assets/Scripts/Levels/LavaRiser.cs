using UnityEngine;
using IndieMarc.Platformer;

/// <summary>
/// L8 finale hazard: a lava surface that rises once activated. Touching it
/// is a level-fail, not a respawn — a mid-chase checkpoint respawn would be
/// an instant re-kill loop, so checkpoints are only placed before the
/// trigger and any death during the ride fails immediately.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LavaRiser : MonoBehaviour
{
    public float riseSpeed = 0.8f;   // units/second once active

    public static bool Active { get; private set; }

    void Awake()
    {
        Active = false;
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnDestroy()
    {
        Active = false;
    }

    public void Activate()
    {
        Active = true;
        AudioManager.PlaySfx("bridge"); // rumble
    }

    void Update()
    {
        if (Active)
            transform.position += Vector3.up * (riseSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") && !other.CompareTag("PlayerTwo"))
            return;

        other.GetComponent<PlayerCharacterOne>()?.Kill();
        other.GetComponent<PlayerCharacterTwo>()?.Kill();

        // During the chase a single death fails the level immediately.
        if (Active)
            Object.FindObjectOfType<GameState>()?.TriggerGameOver();
    }
}

/// <summary>Trigger zone that starts the riser when any player crosses it.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LavaRiserTrigger : MonoBehaviour
{
    public LavaRiser riser;

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
            Destroy(gameObject);
        }
    }
}
