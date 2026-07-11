using UnityEngine;

/// <summary>
/// Torch checkpoint: either player touching it activates it for both.
/// Phase 3 gives it unlit/lit sprites; Phase 4 a flame animation + SFX.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class Checkpoint : MonoBehaviour
{
    public Sprite unlitSprite;
    public Sprite litSprite;

    public bool IsLit { get; private set; }

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        if (unlitSprite != null && TryGetComponent(out SpriteRenderer sprite))
            sprite.sprite = unlitSprite;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsLit)
            return;
        if (!other.CompareTag("Player") && !other.CompareTag("PlayerTwo"))
            return;

        IsLit = true;
        CheckpointManager.Activate(transform.position);
        if (litSprite != null && TryGetComponent(out SpriteRenderer sprite))
            sprite.sprite = litSprite;
        AudioManager.PlaySfx("checkpoint");
    }
}
