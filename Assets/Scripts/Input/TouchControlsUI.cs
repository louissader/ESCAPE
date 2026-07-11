using UnityEngine;

/// <summary>
/// Runtime driver for the on-screen controls prefab. Shows the layout for
/// the resolved control mode and writes button state into the virtual input
/// channels every frame. Hold state is polled (buttons can stay held across
/// a swap); press edges are latched via TouchButton events.
///
/// Editor-only keyboard input keeps working independently — the controls
/// classes merge both sources (see PlayerControlsOne/Two).
/// </summary>
public class TouchControlsUI : MonoBehaviour
{
    [Header("Swap layout")]
    public GameObject swapLayout;
    public TouchButton swapLeft;
    public TouchButton swapDown;
    public TouchButton swapRight;
    public TouchButton swapJump;
    public TouchButton swapAction;
    public TouchButton swapSwap;

    [Header("Co-op layout")]
    public GameObject coopLayout;
    public TouchButton p1Left;
    public TouchButton p1Down;
    public TouchButton p1Right;
    public TouchButton p1Jump;
    public TouchButton p1Action;
    public TouchButton p2Left;
    public TouchButton p2Down;
    public TouchButton p2Right;
    public TouchButton p2Jump;
    public TouchButton p2Action;

    ControlMode mode;

    void Start()
    {
        mode = ControlModeResolver.Resolve();

        if (swapLayout != null)
            swapLayout.SetActive(mode == ControlMode.Swap);
        if (coopLayout != null)
            coopLayout.SetActive(mode == ControlMode.Coop);

        // Press edges latch into the channel that owns the button NOW.
        if (mode == ControlMode.Swap)
        {
            Wire(swapJump, () => ActiveChannel().PressJump());
            Wire(swapAction, () => ActiveChannel().PressAction());
            Wire(swapSwap, RequestSwap);
        }
        else
        {
            Wire(p1Jump, () => VirtualInputRouter.Get(1).PressJump());
            Wire(p1Action, () => VirtualInputRouter.Get(1).PressAction());
            Wire(p2Jump, () => VirtualInputRouter.Get(2).PressJump());
            Wire(p2Action, () => VirtualInputRouter.Get(2).PressAction());
        }

        SwapModeController.OnSwapped += OnSwapped;
        UpdateSwapButtonVisibility();

#if !UNITY_EDITOR
        if (!Application.isMobilePlatform)
            gameObject.SetActive(false); // desktop builds are keyboard-only
#endif
    }

    void OnDestroy()
    {
        SwapModeController.OnSwapped -= OnSwapped;
    }

    void Update()
    {
        if (mode == ControlMode.Swap)
        {
            WriteHolds(ActiveChannel(), swapLeft, swapDown, swapRight, swapJump, swapAction);
        }
        else
        {
            WriteHolds(VirtualInputRouter.Get(1), p1Left, p1Down, p1Right, p1Jump, p1Action);
            WriteHolds(VirtualInputRouter.Get(2), p2Left, p2Down, p2Right, p2Jump, p2Action);
        }
    }

    static VirtualInputChannel ActiveChannel()
    {
        return VirtualInputRouter.Get(VirtualInputRouter.ActivePlayerId);
    }

    static void Wire(TouchButton button, UnityEngine.Events.UnityAction onPress)
    {
        if (button != null)
            button.onPressed.AddListener(onPress);
    }

    static void WriteHolds(VirtualInputChannel ch, TouchButton left, TouchButton down,
                           TouchButton right, TouchButton jump, TouchButton action)
    {
        float move = 0f;
        if (left != null && left.IsHeld) move -= 1f;
        if (right != null && right.IsHeld) move += 1f;
        ch.move = move;
        ch.downHold = down != null && down.IsHeld;
        ch.jumpHold = jump != null && jump.IsHeld;
        ch.actionHold = action != null && action.IsHeld;
    }

    /// <summary>Also called by tapping the inactive HUD portrait (Phase 5).</summary>
    public void RequestSwap()
    {
        if (SwapModeController.Instance != null)
            SwapModeController.Instance.RequestSwap();
    }

    void OnSwapped(int newActiveId)
    {
        UpdateSwapButtonVisibility();
    }

    void UpdateSwapButtonVisibility()
    {
        if (swapSwap == null || mode != ControlMode.Swap)
            return;
        bool canSwap = SwapModeController.Instance == null || SwapModeController.Instance.CanSwap;
        swapSwap.gameObject.SetActive(canSwap);
    }
}
