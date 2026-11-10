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
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private CanvasGroup group;

    const float FadeSeconds = 0.4f;

    const string DefaultLine = "Get BOTH heroes to their doors.";

    // Index = level number (1..SceneRouter.LevelCount); 0 unused.
    static readonly string[] Lines =
    {
        null,
        "Move. Jump. Get BOTH heroes to their doors.",      // L1 First Light (onboarding)
        "High road, low road. Swap heroes. Meet at the doors.", // L2 Two Paths (swap + lever)
        "Burn the bones. Boost your partner. Together.",    // L3 Spark (orbs + skeletons)
        "Two levers. Two bridges. Cross the bones together.", // L4 Skeleton Halls
        "The eyes are watching. Climb the shaft. Together.", // L5 The Depths (vertical climb)
        "Time the frogs. Cross the lava. Together.",        // L6 Frogmarch
        "Split up. One way out. Meet at the doors.",        // L7 Twin Trials (split lanes)
        "The lava is rising. Climb, together, NOW.",        // L8 finale (lava riser)
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
        // Builder wires these; find-in-children keeps a hand-placed banner working.
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 0f;   // pre-render, so no one-frame flash
    }

    void Start()
    {
        if (label != null)
            label.text = LineFor(SceneRouter.CurrentLevelNumber());
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        if (group == null)
            yield break;
        float t = 0f;
        while (t < FadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / FadeSeconds);
            yield return null;
        }
        group.alpha = 1f;   // visible for the rest of the level, no dismissal
    }
}
