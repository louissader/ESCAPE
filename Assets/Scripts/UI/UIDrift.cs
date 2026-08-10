using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Unscaled-time drift for cutscene overlay elements: sinusoid sway with a
/// per-instance phase, optional constant fall with wraparound, optional
/// alpha ease-in. Complements UIPulse (scale + frame cycle), which other
/// scenes depend on and deliberately stays untouched. Captures its base
/// position on enable so beat re-activation restarts cleanly; restores it
/// on disable (same discipline as UIPulse.OnDisable).
/// </summary>
public class UIDrift : MonoBehaviour
{
    public float swayX = 8f;          // canvas units
    public float swayY = 4f;
    public float swaySpeed = 0.4f;
    public float phase = 0f;          // radians; stagger siblings so they never sync
    public float fallSpeed = 0f;      // canvas units/sec downward (negative = rise)
    public float wrapHeight = 0f;     // >0: fall wraps every this many units
    public float fadeInSeconds = 0f;  // >0: alpha eases 0 -> authored value on enable

    RectTransform rect;
    Graphic graphic;
    Vector2 basePos;
    float baseAlpha = 1f;
    float enabledAt;
    float fallen;

    void Awake()
    {
        rect = (RectTransform)transform;
        graphic = GetComponent<Graphic>();
        if (graphic != null)
            baseAlpha = graphic.color.a;
    }

    void OnEnable()
    {
        basePos = rect.anchoredPosition;
        enabledAt = Time.unscaledTime;
        fallen = 0f;
        if (graphic != null && fadeInSeconds > 0f)
            SetAlpha(0f);
    }

    void OnDisable()
    {
        rect.anchoredPosition = basePos;
        if (graphic != null)
            SetAlpha(baseAlpha);
    }

    void Update()
    {
        float t = Time.unscaledTime;
        Vector2 p = basePos + new Vector2(
            Mathf.Sin(t * swaySpeed + phase) * swayX,
            Mathf.Sin(t * swaySpeed * 0.8f + phase * 1.6f) * swayY);
        if (fallSpeed != 0f)
        {
            fallen += fallSpeed * Time.unscaledDeltaTime;
            if (wrapHeight > 0f)
                fallen %= wrapHeight;
            p.y -= fallen;
        }
        rect.anchoredPosition = p;
        if (graphic != null && fadeInSeconds > 0f)
            SetAlpha(baseAlpha * Mathf.Clamp01((t - enabledAt) / fadeInSeconds));
    }

    void SetAlpha(float a)
    {
        Color c = graphic.color;
        c.a = a;
        graphic.color = c;
    }
}
