using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gentle "alive" animation for UI: an unscaled-time scale pulse around the
/// starting localScale, plus an optional sprite-frame cycle on an attached
/// Image when glowFrames is non-empty (SpriteCycler's UI-side sibling —
/// SpriteCycler requires a SpriteRenderer, this drives a uGUI Image).
/// Safe at timeScale 0 and allocation-free per frame. The captured base
/// scale preserves mirrored placements (e.g. localScale.x = -1).
/// </summary>
public class UIPulse : MonoBehaviour
{
    public float scaleAmplitude = 0.05f;
    public float speed = 2f;
    public Sprite[] glowFrames;
    public float fps = 6f;

    Vector3 baseScale;
    Image image;
    float frameTimer;
    int frameIndex;

    void Awake()
    {
        baseScale = transform.localScale;
        image = GetComponent<Image>();
    }

    void OnEnable()
    {
        frameTimer = 0f;
        frameIndex = 0;
        if (image != null && glowFrames != null && glowFrames.Length > 0 &&
            glowFrames[0] != null)
            image.sprite = glowFrames[0];
    }

    void OnDisable()
    {
        // A panel hidden mid-pulse must not reappear stuck off-scale.
        transform.localScale = baseScale;
    }

    void Update()
    {
        transform.localScale = baseScale *
            (1f + Mathf.Sin(Time.unscaledTime * speed) * scaleAmplitude);

        if (image == null || glowFrames == null || glowFrames.Length < 2)
            return;
        frameTimer += Time.unscaledDeltaTime;
        float frameSeconds = 1f / Mathf.Max(0.01f, fps);
        if (frameTimer >= frameSeconds)
        {
            frameTimer -= frameSeconds;
            frameIndex = (frameIndex + 1) % glowFrames.Length;
            if (glowFrames[frameIndex] != null)
                image.sprite = glowFrames[frameIndex];
        }
    }
}
