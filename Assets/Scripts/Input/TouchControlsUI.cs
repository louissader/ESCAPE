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
    CanvasGroup canvasGroup; // added at runtime; the baked prefab has none

    void Start()
    {
        // Stand down while a modal owns the screen. This canvas renders at
        // sortingOrder 100, above GameplayUI's 90, so without this it eats
        // every tap aimed at the pause/settings/win/fail buttons (fully
        // blocking them under the Coop layout on iPad — tapping SETTINGS
        // fired the shoot button). The CanvasGroup is attached here rather
        // than baked so the fix works with the already-built prefab.
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        ModalGuard.OnModalChanged += ApplyModalState;
        ApplyModalState(ModalGuard.IsModalOpen);

        // Wire BOTH layouts once. The two sets are different buttons living
        // under different parents, so the inactive layout can never fire —
        // and wiring once here means ApplyMode() can be re-run live without
        // AddListener stacking duplicate callbacks.
        Wire(swapJump, () => ActiveChannel().PressJump());
        Wire(swapAction, () => ActiveChannel().PressAction());
        Wire(swapSwap, RequestSwap);
        Wire(p1Jump, () => VirtualInputRouter.Get(1).PressJump());
        Wire(p1Action, () => VirtualInputRouter.Get(1).PressAction());
        Wire(p2Jump, () => VirtualInputRouter.Get(2).PressJump());
        Wire(p2Action, () => VirtualInputRouter.Get(2).PressAction());

        ApplyMode();
        SaveSystem.OnControlModeChanged += OnControlModeChanged;
        SwapModeController.OnSwapped += OnSwapped;

#if !UNITY_EDITOR
        if (!Application.isMobilePlatform)
            gameObject.SetActive(false); // desktop builds are keyboard-only
#endif
    }

    void OnDestroy()
    {
        SwapModeController.OnSwapped -= OnSwapped;
        ModalGuard.OnModalChanged -= ApplyModalState;
        SaveSystem.OnControlModeChanged -= OnControlModeChanged;
    }

    /// <summary>Re-resolves the mode and swaps the visible layout. Safe to
    /// call repeatedly; button listeners are wired once in Start().</summary>
    void ApplyMode()
    {
        mode = ControlModeResolver.Resolve();
        if (swapLayout != null)
            swapLayout.SetActive(mode == ControlMode.Swap);
        if (coopLayout != null)
            coopLayout.SetActive(mode == ControlMode.Coop);
        UpdateSwapButtonVisibility();
    }

    void OnControlModeChanged()
    {
        // Drop any half-pressed input from the layout being torn down,
        // otherwise a held button latches into the new layout.
        VirtualInputRouter.Get(1).Clear();
        VirtualInputRouter.Get(2).Clear();
        ApplyMode();
    }

    /// <summary>Hides the touch canvas and stops it eating raycasts while
    /// a modal (pause / settings / win / fail) is open, and releases every
    /// virtual input on BOTH edges: a tap in the frame the modal opened
    /// can latch PressJump(), and it must not fire on resume.</summary>
    void ApplyModalState(bool open)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = open ? 0f : 1f;
            canvasGroup.blocksRaycasts = !open;
            canvasGroup.interactable = !open;
        }
        VirtualInputRouter.Get(1).Clear();
        VirtualInputRouter.Get(2).Clear();
    }

    void Update()
    {
        // While a modal is open the channels were cleared and raycasts are
        // off — writing holds here would re-latch a button whose finger
        // never lifted (IsHeld persists through blocksRaycasts = false).
        if (ModalGuard.IsModalOpen)
            return;

        // Re-check every frame: CanSwap flips back when a dead partner
        // respawns (~2 s), and the respawn path fires no event. Without this
        // the SWAP button vanished for the rest of the level after any death
        // unless the player discovered tapping the inactive portrait.
        UpdateSwapButtonVisibility();

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
        if (swapSwap == null)
            return;
        bool canSwap = mode == ControlMode.Swap &&
            (SwapModeController.Instance == null || SwapModeController.Instance.CanSwap);
        if (swapSwap.gameObject.activeSelf != canSwap)
            swapSwap.gameObject.SetActive(canSwap);
    }
}
