using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Press-and-hold touch button (uGUI onClick can't do holds). Tracks its own
/// pointerId so simultaneous multi-touch on other buttons never releases it.
///
/// Slide-between-buttons (standard virtual-dpad behaviour): a held finger
/// sliding off this button releases it (OnPointerExit) and a held finger
/// sliding onto it presses it (OnPointerEnter), so a thumb rolling from
/// DpadLeft onto DpadRight swaps cleanly instead of leaving DpadLeft latched.
/// No IDragHandler is needed: StandaloneInputModule re-raycasts every frame
/// for touch and mouse pointers alike and delivers enter/exit mid-press.
/// Exit alone is not enough to rely on either way - uGUI sends pointerUp to
/// the object that received pointerDown, so a button pressed via enter never
/// sees pointerUp; exit (which also fires when a touch ends or is cancelled)
/// is its release path, and Update() is the last-ditch guard for events the
/// module never delivers (focus loss, EventSystem teardown). A button must
/// never stay latched after the pointer leaves it.
///
/// Press feedback: tints the Image to (1,1,1,0.85) and scales to 0.94 while
/// held; the baked resting colour and scale are restored on every release
/// path, so feedback can never stick either.
/// </summary>
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    const float PressedAlpha = 0.85f;
    const float PressedScale = 0.94f;

    public UnityEvent onPressed;
    public UnityEvent onReleased;

    public bool IsHeld { get; private set; }

    int activePointerId = int.MinValue;

    Image image;
    Color restingColor;
    Vector3 restingScale;

    void Awake()
    {
        image = GetComponent<Image>();
        if (image != null)
            restingColor = image.color;
        restingScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Press(eventData.pointerId);
    }

    /// <summary>
    /// A pointer that is already down sliding onto this button presses it.
    /// Enter also fires for a fresh touch (immediately before pointerDown)
    /// and for a hovering mouse, so only pointers that are actually down
    /// count; the fresh-touch case presses here and the pointerDown that
    /// follows is a no-op via the IsHeld guard (onPressed fires once).
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (PointerIsDown(eventData))
            Press(eventData.pointerId);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!IsHeld || eventData.pointerId != activePointerId)
            return;
        Release();
    }

    /// <summary>
    /// The active pointer sliding off releases the button. This is also the
    /// lift path for enter-pressed buttons: StandaloneInputModule sends exit
    /// to whatever is under a touch when it ends or is cancelled, while
    /// pointerUp only ever goes to the pointerDown receiver.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!IsHeld || eventData.pointerId != activePointerId)
            return;
        Release();
    }

    void Update()
    {
        // Safety net: if the active pointer vanished without this button
        // receiving up or exit (app focus loss, EventSystem disabled
        // mid-press), release rather than stay latched.
        if (IsHeld && !PointerStillDown(activePointerId))
            Release();
    }

    void OnDisable()
    {
        if (IsHeld)
            Release();
    }

    void Press(int pointerId)
    {
        if (IsHeld)
            return;
        activePointerId = pointerId;
        IsHeld = true;
        if (image != null)
            image.color = new Color(1f, 1f, 1f, PressedAlpha);
        transform.localScale = restingScale * PressedScale;
        onPressed?.Invoke();
    }

    void Release()
    {
        IsHeld = false;
        activePointerId = int.MinValue;
        if (image != null)
            image.color = restingColor;
        transform.localScale = restingScale;
        onReleased?.Invoke();
    }

    /// <summary>
    /// Touch pointerIds (>= 0) only exist while the finger is on the glass;
    /// a mouse enter (id -1..-3) is just a hover unless a press is live.
    /// </summary>
    static bool PointerIsDown(PointerEventData eventData)
    {
        if (eventData.pointerId >= 0)
            return true;
        return eventData.pointerPress != null || Input.GetMouseButton(0);
    }

    /// <summary>
    /// True while the given uGUI pointerId is still pressed. Legacy Input
    /// Manager only (activeInputHandler 0): touch ids map to fingerId,
    /// mouse ids are -1 left / -2 right / -3 middle.
    /// </summary>
    static bool PointerStillDown(int pointerId)
    {
        if (pointerId < 0)
            return Input.GetMouseButton(-pointerId - 1);
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId == pointerId)
                return touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
        }
        return false;
    }
}
