using UnityEngine;
using Cinemachine;
using IndieMarc.Platformer;

/// <summary>
/// Swap-mode brain: applies active/parked state to the two characters and
/// auto-swaps when the active character becomes uncontrollable (absorbed by
/// the exit, or dead). Present in level scenes only; inert in Co-op mode.
///
/// Parked characters are invulnerable and skipped by enemy targeting
/// (enemies query IsParked) — an unattended character must never be whittled
/// down off-screen with no counterplay.
///
/// It also owns the camera's framing weights. LevelBuilder wires both heroes
/// into a CinemachineTargetGroup at weight 1 (correct for Co-op). In Swap mode
/// that framed the MIDPOINT of the pair, so once the parked hero was left
/// behind — which the How To Play card actively encourages — the group grew
/// past the vcam's ortho clamp (11.25) and the hero the player was actually
/// driving slid off-screen. Swap mode therefore drops the parked hero to
/// weight 0 so the group collapses onto the active hero.
/// </summary>
public class SwapModeController : MonoBehaviour
{
    public const float ParkedAlpha = 0.6f;

    /// <summary>Seconds to blend a hero's camera weight in/out. The group's
    /// centre and bounds move continuously across the blend (a member's
    /// contribution is scaled by weight/maxWeight, so it fades rather than
    /// pops), and the FramingTransposer's 1s X/Y damping smooths what is
    /// left — no cut, and no reliance on damping alone for the zoom.</summary>
    public const float CameraBlendSeconds = 0.45f;

    public static SwapModeController Instance { get; private set; }

    /// <summary>Fires after control moves to a character (arg: player id).</summary>
    public static event System.Action<int> OnSwapped;

    PlayerCharacterOne player1;
    PlayerCharacterTwo player2;
    ControlMode mode;
    bool player1Gone;   // absorbed by exit or dead
    bool player2Gone;

    // Camera framing. Cached once in Start — never per frame.
    CinemachineTargetGroup targetGroup;
    int groupIndex1 = -1;
    int groupIndex2 = -1;
    float weightTarget1 = 1f;
    float weightTarget2 = 1f;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        SaveSystem.OnControlModeChanged -= OnControlModeChanged;
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        VirtualInputRouter.ResetForLevel();

        player1 = PlayerCharacterOne.Get(1);
        player2 = PlayerCharacterTwo.Get(2);

        if (player1 != null)
            player1.onDeath += () => OnCharacterUncontrollable(1);
        if (player2 != null)
            player2.onDeath += () => OnCharacterUncontrollable(2);

        CacheTargetGroup();
        ApplyMode(true);
        SaveSystem.OnControlModeChanged += OnControlModeChanged;
    }

    /// <summary>Applies the current mode to both characters. In Co-op both
    /// heroes are player-driven, so nothing may stay parked.</summary>
    /// <param name="snapCamera">Set the camera weights instantly instead of
    /// blending — true only for the level's opening frame.</param>
    void ApplyMode(bool snapCamera = false)
    {
        mode = ControlModeResolver.Resolve();
        if (mode == ControlMode.Swap)
        {
            ApplyActiveState(VirtualInputRouter.ActivePlayerId);
        }
        else
        {
            // Un-park everything: a hero left dimmed and invulnerable from
            // Swap mode would be uncontrollable-looking in Co-op.
            ApplyToCharacter(1, true);
            ApplyToCharacter(2, true);
        }
        RefreshCameraTargets(snapCamera);
    }

    void OnControlModeChanged()
    {
        ApplyMode();
    }

    void Update()
    {
        // Camera weights first: they must keep converging in Co-op too (a
        // live mode switch mid-level has to blend the parked hero back in).
        StepCameraWeights();

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
        Haptics.SelectionTick();
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
        RefreshCameraTargets();
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

    // ---------------- camera framing ----------------

    /// <summary>One-time lookup of the LevelBuilder-built target group and of
    /// each hero's slot in it. Levels without the rig simply keep whatever
    /// camera they have.</summary>
    void CacheTargetGroup()
    {
        targetGroup = FindObjectOfType<CinemachineTargetGroup>();
        if (targetGroup == null)
            return;

        if (player1 != null)
            groupIndex1 = targetGroup.FindMember(player1.transform);
        if (player2 != null)
            groupIndex2 = targetGroup.FindMember(player2.transform);

        if (groupIndex1 < 0 || groupIndex2 < 0)
            Debug.LogWarning("[SwapModeController] hero missing from the camera TargetGroup — " +
                             "swap framing disabled for this level.");
    }

    /// <summary>Decides the weight each hero should be framed at. Called on
    /// every state change AND once per frame, so a hero that disappears
    /// without a swap (legacy exit absorption deactivates the object) can
    /// never leave the camera anchored to a weight-0 member.</summary>
    void RefreshCameraTargets(bool immediate = false)
    {
        if (targetGroup == null)
            return;

        int active = VirtualInputRouter.ActivePlayerId;

        // Solo framing only in Swap mode, and only while the active hero is
        // actually framable. If it is not (absorbed by the exit, and no
        // partner was left to hand control to) both weights go back to 1 so
        // the group falls back to whoever is still in the scene — a group
        // whose only weighted member is inactive stops updating entirely.
        bool solo = mode == ControlMode.Swap && IsFramable(active);

        weightTarget1 = !solo || active == 1 ? 1f : 0f;
        weightTarget2 = !solo || active == 2 ? 1f : 0f;

        // Invariant: exactly one hero carries weight 1 in Swap mode and both
        // do otherwise — the group is never left with every weight at 0.
        if (immediate)
        {
            SetWeight(groupIndex1, weightTarget1);
            SetWeight(groupIndex2, weightTarget2);
        }
    }

    bool IsFramable(int playerId)
    {
        GameObject go = playerId == 1
            ? (player1 != null ? player1.gameObject : null)
            : (player2 != null ? player2.gameObject : null);
        return go != null && go.activeInHierarchy;
    }

    void StepCameraWeights()
    {
        if (targetGroup == null)
            return;

        RefreshCameraTargets();

        // Scaled time on purpose: the brain runs on scaled time too, so the
        // blend freezes with the rest of the game behind a pause/win panel.
        float step = Time.deltaTime / CameraBlendSeconds;
        StepWeight(groupIndex1, weightTarget1, step);
        StepWeight(groupIndex2, weightTarget2, step);
    }

    void StepWeight(int index, float target, float step)
    {
        if (!IndexValid(index))
            return;
        float current = targetGroup.m_Targets[index].weight;
        if (current == target)
            return;
        targetGroup.m_Targets[index].weight = Mathf.MoveTowards(current, target, step);
    }

    void SetWeight(int index, float weight)
    {
        if (IndexValid(index))
            targetGroup.m_Targets[index].weight = weight;
    }

    bool IndexValid(int index)
    {
        return index >= 0 && targetGroup.m_Targets != null && index < targetGroup.m_Targets.Length;
    }
}
