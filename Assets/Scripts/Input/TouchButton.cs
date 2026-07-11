using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

/// <summary>
/// Press-and-hold touch button (uGUI onClick can't do holds). Tracks its own
/// pointerId so simultaneous multi-touch on other buttons never releases it.
/// </summary>
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    public bool IsHeld { get; private set; }

    int activePointerId = int.MinValue;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsHeld)
            return;
        activePointerId = eventData.pointerId;
        IsHeld = true;
        onPressed?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!IsHeld || eventData.pointerId != activePointerId)
            return;
        Release();
    }

    void OnDisable()
    {
        if (IsHeld)
            Release();
    }

    void Release()
    {
        IsHeld = false;
        activePointerId = int.MinValue;
        onReleased?.Invoke();
    }
}
