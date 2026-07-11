using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Green "level completed" feedback on a level-select tile. Two modes:
/// - steady: completed tiles get a soft green pulsing glow (always on)
/// - celebrate: the just-beaten level plays a one-shot pop + bright flash
///   (the dopamine hit), then settles into the steady pulse.
/// </summary>
public class LevelTileCelebration : MonoBehaviour
{
    public Image glow;                 // green border/backdrop image behind the tile
    public bool celebrate;             // one-shot big animation on entry

    static readonly Color GlowColor = new Color(0.30f, 0.95f, 0.45f);

    Vector3 baseScale;

    void Start()
    {
        baseScale = transform.localScale;
        if (glow != null)
            glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0.35f);
        StartCoroutine(celebrate ? Celebrate() : Pulse());
    }

    IEnumerator Celebrate()
    {
        AudioManager.PlaySfx("level_complete");
        float t = 0f;
        const float pop = 0.55f;
        while (t < pop)
        {
            t += Time.deltaTime;
            float k = t / pop;
            // overshoot scale: 1 -> 1.18 -> 1
            float s = 1f + 0.18f * Mathf.Sin(k * Mathf.PI);
            transform.localScale = baseScale * s;
            if (glow != null)
                glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b,
                    Mathf.Lerp(0.9f, 0.35f, k));
            yield return null;
        }
        transform.localScale = baseScale;
        yield return Pulse();
    }

    IEnumerator Pulse()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            if (glow != null)
            {
                float a = 0.28f + 0.14f * (0.5f + 0.5f * Mathf.Sin(t * 2.4f));
                glow.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, a);
            }
            yield return null;
        }
    }
}
