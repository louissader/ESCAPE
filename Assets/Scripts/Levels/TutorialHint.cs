using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// One-time zone-triggered hint bubble for L1 (move/jump → swap → both must
/// reach a portal). Skipped forever once SaveSystem.TutorialSeen; dismissed
/// only by a deliberate input — a key press, or on touch a NEW tap that is
/// clear of the on-screen controls — after a short minimum display time.
/// Pauses nothing — reading is optional.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class TutorialHint : MonoBehaviour
{
    // Hints spawn from OnTriggerEnter2D, i.e. while the player's thumb is
    // still holding the d-pad. The old check (Input.touchCount > 0) is true
    // for HELD fingers, so on touch every hint dismissed the frame it
    // appeared — and the L1 chain's last hint latched SaveSystem.TutorialSeen
    // on a hint nobody read. Dismissal now requires: (a) the hint has been up
    // long enough to read, and (b) a deliberate input — TouchPhase.Began
    // outside raycastable UI on touch, anyKeyDown on keyboard.
    const float MinDisplaySeconds = 1.5f;

    [TextArea] public string message = "";
    public bool marksTutorialSeen = false;   // set on the LAST hint of the L1 onboarding chain
    public bool alwaysShow = false;          // contextual per-mechanic hints ignore the seen-gate

    GameObject bubble;
    bool shown;
    float shownAt;                           // Time.unscaledTime when the bubble appeared

    void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        // First-launch onboarding hints vanish once seen; contextual hints
        // (levers, lava, platform spots, ...) always show on level entry.
        if (SaveSystem.TutorialSeen && !alwaysShow)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (shown || (!other.CompareTag("Player") && !other.CompareTag("PlayerTwo")))
            return;
        shown = true;
        ShowBubble();
    }

    // LateUpdate, not Update: the EventSystem raycasts this frame's touches
    // in its own Update, and every Update runs before any LateUpdate, so a
    // touch that began THIS frame already has a valid pointer-over result by
    // the time we query it. In Update the ordering would be undefined and a
    // began-on-d-pad touch could falsely read as "not over UI".
    void LateUpdate()
    {
        if (bubble == null || !bubble.activeSelf)
            return;
        if (Time.unscaledTime - shownAt < MinDisplaySeconds)
            return; // swallow all input until the hint was actually readable

        if (Input.touchCount > 0)
        {
            // Touch present: only a fresh tap clear of the controls counts.
            // Taps on the d-pad / A / B / swap clusters, the HUD pause button
            // and the portraits are all UI raycast targets, so the pointer
            // query filters them without duplicating TouchControlsBuilder's
            // layout constants (same pattern as IntroCutscene.AdvancePressed
            // — it also keeps working if the layout is ever rebaked). The
            // keyboard branch is skipped entirely while touches exist:
            // touches emulate mouse button 0, which would sneak held fingers
            // and control taps back in through anyKeyDown.
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began && !TouchOverUI(touch.fingerId))
                {
                    Dismiss();
                    return;
                }
            }
        }
        else if (Input.anyKeyDown)
        {
            Dismiss(); // editor/desktop keyboard path
        }
    }

    static bool TouchOverUI(int fingerId)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    void ShowBubble()
    {
        bubble = new GameObject("HintBubble");
        bubble.transform.SetParent(transform, false);
        bubble.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        var text = bubble.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = 3;
        text.alignment = TextAlignmentOptions.Center;
        text.sortingOrder = 50;
        var rect = text.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(8f, 3f);

        shownAt = Time.unscaledTime;
    }

    /// <summary>
    /// Deliberate-dismissal path only (gated in LateUpdate). There is no
    /// auto-timeout: a hint left unread simply despawns with the scene and
    /// never touches SaveSystem.TutorialSeen, so the chain returns next run.
    /// </summary>
    void Dismiss()
    {
        if (marksTutorialSeen)
            SaveSystem.TutorialSeen = true;
        Destroy(gameObject);
    }
}
