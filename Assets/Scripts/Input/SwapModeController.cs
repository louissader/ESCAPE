using UnityEngine;
using IndieMarc.Platformer;

/// <summary>
/// Swap-mode brain: applies active/parked state to the two characters and
/// auto-swaps when the active character becomes uncontrollable (absorbed by
/// the exit, or dead). Present in level scenes only; inert in Co-op mode.
///
/// Parked characters are invulnerable and skipped by enemy targeting
/// (enemies query IsParked) — an unattended character must never be whittled
/// down off-screen with no counterplay.
/// </summary>
public class SwapModeController : MonoBehaviour
{
    public const float ParkedAlpha = 0.6f;

    public static SwapModeController Instance { get; private set; }

    /// <summary>Fires after control moves to a character (arg: player id).</summary>
    public static event System.Action<int> OnSwapped;

    PlayerCharacterOne player1;
    PlayerCharacterTwo player2;
    ControlMode mode;
    bool player1Gone;   // absorbed by exit or dead
    bool player2Gone;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        mode = ControlModeResolver.Resolve();
        VirtualInputRouter.ResetForLevel();

        player1 = PlayerCharacterOne.Get(1);
        player2 = PlayerCharacterTwo.Get(2);

        if (player1 != null)
            player1.onDeath += () => OnCharacterUncontrollable(1);
        if (player2 != null)
            player2.onDeath += () => OnCharacterUncontrollable(2);

        if (mode == ControlMode.Swap)
            ApplyActiveState(VirtualInputRouter.ActivePlayerId);
    }

    void Update()
    {
        if (mode != ControlMode.Swap)
            return;

        // Exit absorption deactivates the GameObject (no event) — poll it.
        if (!player1Gone && player1 != null && !player1.gameObject.activeSelf)
        {
            player1Gone = true;
            OnCharacterUncontrollable(1);
        }
        if (!player2Gone && player2 != null && !player2.gameObject.activeSelf)
        {
            player2Gone = true;
            OnCharacterUncontrollable(2);
        }
    }

    public ControlMode Mode { get { return mode; } }

    /// <summary>True when both characters can still be piloted (swap allowed).
    /// Dead characters count as unpilotable — swapping to a corpse during its
    /// respawn window would strand the player controlling nothing.</summary>
    public bool CanSwap
    {
        get
        {
            return mode == ControlMode.Swap && !player1Gone && !player2Gone &&
                   player1 != null && !player1.IsDead() &&
                   player2 != null && !player2.IsDead();
        }
    }

    public void RequestSwap()
    {
        if (!CanSwap)
            return;
        VirtualInputRouter.Swap();
        ApplyActiveState(VirtualInputRouter.ActivePlayerId);
        AudioManager.PlaySfx("swap");
        OnSwapped?.Invoke(VirtualInputRouter.ActivePlayerId);
    }

    void OnCharacterUncontrollable(int playerId)
    {
        if (mode != ControlMode.Swap)
            return;
        if (VirtualInputRouter.ActivePlayerId != playerId)
            return;

        int other = playerId == 1 ? 2 : 1;
        bool otherGone = other == 1 ? player1Gone : player2Gone;
        bool otherDead = other == 1
            ? (player1 != null && player1.IsDead())
            : (player2 != null && player2.IsDead());
        if (otherGone || otherDead)
            return; // nothing to hand control to; GameState owns level-fail

        VirtualInputRouter.SetActivePlayer(other);
        ApplyActiveState(other);
        OnSwapped?.Invoke(other);
    }

    /// <summary>True when this character is the parked (uncontrolled) one.</summary>
    public static bool IsParked(int playerId)
    {
        SwapModeController c = Instance;
        if (c == null || c.mode != ControlMode.Swap)
            return false;
        return VirtualInputRouter.ActivePlayerId != playerId;
    }

    void ApplyActiveState(int activeId)
    {
        ApplyToCharacter(1, activeId == 1);
        ApplyToCharacter(2, activeId == 2);
    }

    void ApplyToCharacter(int playerId, bool active)
    {
        GameObject go = playerId == 1
            ? (player1 != null ? player1.gameObject : null)
            : (player2 != null ? player2.gameObject : null);
        if (go == null || !go.activeSelf)
            return;

        if (playerId == 1)
            player1.invulnerable = !active;
        else
            player2.invulnerable = !active;

        SpriteRenderer sprite = go.GetComponent<SpriteRenderer>();
        if (sprite != null)
        {
            Color c = sprite.color;
            c.a = active ? 1f : ParkedAlpha;
            sprite.color = c;
        }
    }
}
