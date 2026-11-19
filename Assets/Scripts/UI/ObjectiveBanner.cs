using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Persistent top-center objective line shown for the whole level (Playtest 6
/// P1: players kept forgetting the goal). Reads the level number from
/// SceneRouter in Start, sets a per-level themed line, and fades in once over
/// ~0.4s of UNSCALED time (so pausing on frame one cannot freeze it mid-fade).
/// Never dismissed; pause/win/fail overlays are later siblings and simply
/// draw above it. Voice matches the intro cutscene captions: short
/// declaratives with the "Together." motif ("The only way out is down.
/// Together."). Plain ASCII on purpose - the default TMP LiberationSans
/// atlas has no em-dash glyph.
/// </summary>
public class ObjectiveBanner : MonoBehaviour
{
    /// <summary>Scene singleton so gameplay events can Flash() a notice.</summary>
    public static ObjectiveBanner Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private CanvasGroup group;

    const float FadeSeconds = 0.4f;
    static readonly Color FlashColor = new Color(1f, 0.85f, 0.3f);

    Coroutine flashRoutine;

    const string DefaultLine = "Get BOTH heroes to their doors.";

    // Index = level number (1..SceneRouter.LevelCount); 0 unused.
    // Playtest 9.3 rewrite: the themed flavor lines ("Burn the bones.
    // Boost your partner.") read as word soup to a new player. The first
    // three levels now give plain instructions; later levels warn about
    // the hazard the player is about to meet, in the hazard's own terms.
    static readonly string[] Lines =
    {
        null,
        "Move with < >, tap JUMP. Get BOTH heroes to their doors.", // L1 First Light (onboarding)
        "Tap SWITCH TO to control the other hero. Levers open bridges.", // L2 Two Paths (swap + lever)
        "Red orb = fireballs. Blue orb = platform. You need both.", // L3 Spark (orbs + skeletons)
        "Skeletons hit hard - shoot them or slip past. Two levers here.", // L4 Skeleton Halls
        "Flying eyes chase the hero you control. Swap to shake them.", // L5 The Depths (vertical climb)
        "Frogs block the lava path. Wait for their hop, then jump.", // L6 Frogmarch
        "The paths split. Each hero takes their own route to their door.", // L7 Twin Trials (split lanes)
        "The lava RISES. Keep climbing - do not stop.",             // L8 finale (lava riser)
    };

    /// <summary>Objective line for a level number; default outside 1..8.</summary>
    public static string LineFor(int levelNumber)
    {
        if (levelNumber >= 1 && levelNumber < Lines.Length &&
            !string.IsNullOrEmpty(Lines[levelNumber]))
            return Lines[levelNumber];
        return DefaultLine;
    }

    void Awake()
    {
        Instance = this;
        // Builder wires these; find-in-children keeps a hand-placed banner working.
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 0f;   // pre-render, so no one-frame flash
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Temporarily replaces the objective line with a gold notice
    /// (playtest 9.3: the auto-handoff at a door needed words, not just a
    /// sound), then restores the level line. Re-entrant: a second flash
    /// restarts the timer.</summary>
    public void Flash(string message, float seconds = 2.5f)
    {
        if (label == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(message, seconds));
    }

    IEnumerator FlashRoutine(string message, float seconds)
    {
        label.text = message;
        label.color = FlashColor;
        yield return new WaitForSecondsRealtime(seconds);
        label.color = Color.white;
        label.text = LineFor(SceneRouter.CurrentLevelNumber());
        flashRoutine = null;
    }

    void Start()
    {
        if (label != null)
            label.text = LineFor(SceneRouter.CurrentLevelNumber());
        StartCoroutine(Intro());
    }

    // Playtest 9.5: the line is each level's main lesson, so it opens big
    // at screen center, holds, then shrinks and docks to its top slot.
    const float IntroScale = 1.6f;
    const float IntroHoldSeconds = 2.6f;
    const float DockSeconds = 0.5f;

    IEnumerator Intro()
    {
        var rect = (RectTransform)transform;
        var parent = rect.parent as RectTransform;
        Vector2 dockedPos = rect.anchoredPosition;
        Vector3 dockedScale = rect.localScale;

        Vector2 centerPos = dockedPos;
        if (parent != null)
        {
            // anchoredPosition is measured from the anchor point; aim the
            // pill's middle (pivot is its top edge) at the parent's middle.
            Rect pr = parent.rect;
            Vector2 anchorPt = new Vector2(
                Mathf.Lerp(pr.xMin, pr.xMax, rect.anchorMin.x),
                Mathf.Lerp(pr.yMin, pr.yMax, rect.anchorMin.y));
            centerPos = pr.center - anchorPt +
                new Vector2(0f, rect.rect.height * IntroScale * (rect.pivot.y - 0.5f));
        }
        rect.anchoredPosition = centerPos;
        rect.localScale = dockedScale * IntroScale;

        float t = 0f;
        while (t < FadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            if (group != null) group.alpha = Mathf.Clamp01(t / FadeSeconds);
            yield return null;
        }
        if (group != null) group.alpha = 1f;

        yield return new WaitForSecondsRealtime(IntroHoldSeconds);

        t = 0f;
        while (t < DockSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / DockSeconds));
            rect.anchoredPosition = Vector2.Lerp(centerPos, dockedPos, k);
            rect.localScale = Vector3.Lerp(dockedScale * IntroScale, dockedScale, k);
            yield return null;
        }
        rect.anchoredPosition = dockedPos;   // visible for the rest of the level
        rect.localScale = dockedScale;
    }
}
