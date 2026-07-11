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

        // Swap mode: the parked character would be left below and swallowed
        // through no fault of the player — bring it to its partner so both
        // board the ride together (the level's design intent).
        var p1 = PlayerCharacterOne.Get(1);
        var p2 = PlayerCharacterTwo.Get(2);
        if (SwapModeController.IsParked(1) && p1 != null && p2 != null && p2.gameObject.activeSelf)
            p1.Teleport(p2.transform.position);
        else if (SwapModeController.IsParked(2) && p2 != null && p1 != null && p1.gameObject.activeSelf)
            p2.Teleport(p1.transform.position);
    }

    void Update()
    {
        if (Active)
            transform.position += Vector3.up * (riseSpeed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Inert until the chase begins (before activation it sits harmlessly
        // below the shaft floor) and never punishes the parked character.
        if (!Active)
            return;
        if (!other.CompareTag("Player") && !other.CompareTag("PlayerTwo"))
            return;

        var p1 = other.GetComponent<PlayerCharacterOne>();
        var p2 = other.GetComponent<PlayerCharacterTwo>();
        if (p1 != null && SwapModeController.IsParked(1)) return;
        if (p2 != null && SwapModeController.IsParked(2)) return;

        p1?.Kill();
        p2?.Kill();
        // During the chase a piloted death fails the level immediately —
        // a checkpoint respawn below the risen lava would be an insta-kill loop.
        Object.FindObjectOfType<GameState>()?.TriggerGameOver();
    }
}

/// <summary>Trigger zone that starts the riser + ride when a player crosses it.</summary>
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
