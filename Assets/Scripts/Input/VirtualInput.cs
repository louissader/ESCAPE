using UnityEngine;

/// <summary>
/// Per-player virtual input channel. Touch UI (and tests) WRITE here;
/// PlayerControlsOne/Two READ and merge with keyboard, so the editor stays
/// keyboard-playable while devices are touch-driven.
///
/// Press events (jumpDown/actionDown) are latched until consumed once per
/// frame by the owning controls component — mirrors the one-shot pattern
/// proven in the abandoned SpriteKit port.
/// </summary>
public class VirtualInputChannel
{
    public float move;           // -1 .. 1 horizontal
    public bool downHold;        // crouch
    public bool jumpHold;
    public bool actionHold;

    bool jumpLatch;
    bool actionLatch;

    public void PressJump() { jumpLatch = true; jumpHold = true; }
    public void ReleaseJump() { jumpHold = false; }
    public void PressAction() { actionLatch = true; actionHold = true; }
    public void ReleaseAction() { actionHold = false; }

    /// <summary>True exactly once per press.</summary>
    public bool ConsumeJumpDown()
    {
        bool v = jumpLatch;
        jumpLatch = false;
        return v;
    }

    public bool ConsumeActionDown()
    {
        bool v = actionLatch;
        actionLatch = false;
        return v;
    }

    /// <summary>Releases every held and latched input. Called on swap,
    /// on level reset, and by TouchControlsUI on BOTH modal edges (open
    /// and resume) so a stray tap latched while paused can never fire a
    /// queued jump/action after the modal closes (see ModalGuard).</summary>
    public void Clear()
    {
        move = 0f;
        downHold = jumpHold = actionHold = false;
        jumpLatch = actionLatch = false;
    }
}

/// <summary>
/// Static access point for the two players' virtual channels plus the
/// swap-mode state machine (which character the single pilot controls).
/// </summary>
public static class VirtualInputRouter
{
    static readonly VirtualInputChannel player1 = new VirtualInputChannel();
    static readonly VirtualInputChannel player2 = new VirtualInputChannel();

    public static VirtualInputChannel Get(int playerId)
    {
        return playerId == 2 ? player2 : player1;
    }

    /// <summary>Swap mode: id of the character the pilot is driving (1 or 2).</summary>
    public static int ActivePlayerId { get; private set; } = 1;

    /// <summary>Raised with the newly active player id after a swap.</summary>
    public static event System.Action<int> OnActivePlayerChanged;

    public static void SetActivePlayer(int playerId)
    {
        if (playerId == ActivePlayerId)
            return;
        Get(ActivePlayerId).Clear();
        ActivePlayerId = playerId;
        OnActivePlayerChanged?.Invoke(playerId);
    }

    public static void Swap()
    {
        SetActivePlayer(ActivePlayerId == 1 ? 2 : 1);
    }

    public static void ResetForLevel()
    {
        player1.Clear();
        player2.Clear();
        ActivePlayerId = 1;
    }
}
