using System.Collections;
using UnityEngine;

/// <summary>
/// Crystal-platform entrance: every time the Wisp transforms, the slab pops
/// in with a cartoon overshoot (scale 0 -> 1.15 -> 1, same curve family as
/// LevelTileCelebration) and then breathes with a soft brightness pulse.
/// Lives on Rectangle.prefab, which PlayerCharacterTwo reuses with
/// SetActive(true) — so everything is driven from OnEnable and restored in
/// OnDisable. Scale animation wraps the captured base scale, preserving the
/// prefab's 3 x 0.5 slab shape.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlatformMaterialize : MonoBehaviour
{
    [Tooltip("Seconds for the pop-in overshoot")]
    public float popDuration = 0.28f;
    [Tooltip("Brightness pulse depth once settled (0 = off)")]
    public float glowAmplitude = 0.12f;
    public float glowSpeed = 2.4f;

    SpriteRenderer sr;
    Vector3 baseScale;
    Color baseColor;
    bool captured;
    Coroutine pop;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        // Capture once, lazily: Awake order vs. the character's Instantiate
        // is not guaranteed, and the prefab's authored scale must be the
        // truth we return to.
        if (!captured)
        {
            baseScale = transform.localScale;
            baseColor = sr.color;
            captured = true;
        }
        if (pop != null) StopCoroutine(pop);
        pop = StartCoroutine(PopIn());
    }

    void OnDisable()
    {
        if (pop != null) { StopCoroutine(pop); pop = null; }
        transform.localScale = baseScale;
        sr.color = baseColor;
    }

    void Update()
    {
        if (pop != null || glowAmplitude <= 0f)
            return;
        float glow = 1f + glowAmplitude * (0.5f + 0.5f * Mathf.Sin(Time.time * glowSpeed * Mathf.PI));
        sr.color = new Color(
            Mathf.Clamp01(baseColor.r * glow),
            Mathf.Clamp01(baseColor.g * glow),
            Mathf.Clamp01(baseColor.b * glow),
            baseColor.a);
    }

    IEnumerator PopIn()
    {
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popDuration);
            // Overshoot: rises past 1 around k ~ 0.7 and settles back.
            float s = k < 0.7f
                ? Mathf.SmoothStep(0f, 1.15f, k / 0.7f)
                : Mathf.SmoothStep(1.15f, 1f, (k - 0.7f) / 0.3f);
            transform.localScale = baseScale * s;
            yield return null;
        }
        transform.localScale = baseScale;
        pop = null;
    }
}
