using UnityEngine;
using UnityEngine.UI;
using IndieMarc.Platformer;

/// <summary>
/// In-level HUD. Layout contract (design review): top-left = P1 portrait +
/// HP bar; top-right corner = pause, with P2 portrait + HP bar immediately
/// left of it; the active-character indicator is a highlight on the active
/// portrait (no separate label). Tapping the INACTIVE portrait swaps.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [SerializeField] private Image p1HpFill;
    [SerializeField] private Image p2HpFill;
    [SerializeField] private Image p1Portrait;
    [SerializeField] private Image p2Portrait;
    [SerializeField] private Image p1ActiveGlow;
    [SerializeField] private Image p2ActiveGlow;

    static readonly Color HealthyColor = new Color(0.30f, 0.85f, 0.35f);
    static readonly Color LowColor = new Color(0.90f, 0.25f, 0.20f);

    PlayerCharacterOne player1;
    PlayerCharacterTwo player2;

    void Start()
    {
        player1 = PlayerCharacterOne.Get(1);
        player2 = PlayerCharacterTwo.Get(2);
        SwapModeController.OnSwapped += OnSwapped;
        SaveSystem.OnControlModeChanged += RefreshActiveGlow;
        RefreshActiveGlow();
    }

    void OnDestroy()
    {
        SwapModeController.OnSwapped -= OnSwapped;
        SaveSystem.OnControlModeChanged -= RefreshActiveGlow;
    }

    void Update()
    {
        if (player1 != null && p1HpFill != null)
            SetBar(p1HpFill, player1.GetHP() / player1.max_hp);
        if (player2 != null && p2HpFill != null)
            SetBar(p2HpFill, player2.GetHP() / player2.max_hp);
    }

    static void SetBar(Image fill, float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        fill.fillAmount = fraction;
        fill.color = Color.Lerp(LowColor, HealthyColor, fraction);
    }

    public void OnPortrait1Tapped() { SwapTo(1); }
    public void OnPortrait2Tapped() { SwapTo(2); }

    void SwapTo(int playerId)
    {
        SwapModeController c = SwapModeController.Instance;
        if (c == null || c.Mode != ControlMode.Swap)
            return;
        if (VirtualInputRouter.ActivePlayerId != playerId && c.CanSwap)
            c.RequestSwap();
    }

    void OnSwapped(int newActiveId)
    {
        RefreshActiveGlow();
    }

    void RefreshActiveGlow()
    {
        // Resolve directly: SwapModeController.mode is enum-default (Swap)
        // until its Start() runs, and Start order between it and this HUD is
        // undefined — trusting it lit a glow in desktop Co-op.
        bool swapMode = ControlModeResolver.Resolve() == ControlMode.Swap;
        int active = VirtualInputRouter.ActivePlayerId;
        if (p1ActiveGlow != null)
            p1ActiveGlow.enabled = swapMode && active == 1;
        if (p2ActiveGlow != null)
            p2ActiveGlow.enabled = swapMode && active == 2;
    }
}
