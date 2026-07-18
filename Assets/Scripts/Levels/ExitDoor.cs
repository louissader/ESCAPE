using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieMarc.Platformer;

/// <summary>
/// Playtest 5 win condition: each hero has a colour-matched door and the
/// level is won only when BOTH doors hold their hero at the same time
/// (Fireboy &amp; Watergirl style). Replaces the legacy single "Dragon" portal
/// absorption — the players' Dragon collision branch stands down whenever a
/// level contains ExitDoors (see PlayerCharacterOne/Two.OnCollisionEnter2D).
///
/// EDITOR PASS must, per level:
///   - add one ExitDoor per hero: heroNumber 1 (EMBER) with the door_ember
///     frames, heroNumber 2 (WISP) with the door_wisp frames
///     (closedFrames = closed_0, halfLitFrames = half_0..half_1,
///     openFrames = open_0..open_3),
///   - size the BoxCollider2D trigger to cover the doorway down to the floor
///     (occupancy tests the hero's PIVOT, transform.position — a knee-high
///     trigger would never register),
///   - remove or disable the old solid "Dragon" exit objects (their solid
///     collider blocks movement; their win code path is already neutralized).
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ExitDoor : MonoBehaviour
{
    public int heroNumber = 1;      // 1 = EMBER (red), 2 = WISP (blue)
    public Sprite[] closedFrames;   // hero absent
    public Sprite[] halfLitFrames;  // hero inside, waiting for the partner
    public Sprite[] openFrames;     // all doors satisfied — plays once
    public float fps = 6f;

    enum VisualState { Closed, HalfLit, Open }

    static readonly List<ExitDoor> doors = new List<ExitDoor>();
    static bool winFired;

    SpriteRenderer sprite;
    BoxCollider2D doorTrigger;
    VisualState state = VisualState.Closed;
    float frameTimer;
    int frameIndex;
    bool satisfied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        // Same lifecycle as CrystalTracker: sceneLoaded fires after Awake but
        // before Start, so doors registered in Start survive this reset.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) =>
        {
            doors.Clear();
            winFired = false;
        };
    }

    /// <summary>True when this level wins via both-heroes-at-door — the
    /// legacy Dragon absorption in the player classes must stand down.</summary>
    public static bool AnyInLevel
    {
        get { return doors.Count > 0; }
    }

    /// <summary>World-space center of the doorway trigger (ferry target for
    /// the parked partner in swap mode).</summary>
    public Vector3 TriggerCenter
    {
        get { return doorTrigger != null ? doorTrigger.bounds.center : transform.position; }
    }

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        doorTrigger = GetComponent<BoxCollider2D>();
        doorTrigger.isTrigger = true;
    }

    void Start()
    {
        // Registration MUST happen in Start — see Crystal.cs (sceneLoaded
        // clears the registry after Awake but before Start).
        if (!doors.Contains(this))
            doors.Add(this);
        SetState(VisualState.Closed);
    }

    void OnDestroy()
    {
        doors.Remove(this);
    }

    void Update()
    {
        if (!winFired)
            UpdateOccupancy();
        Animate();
    }

    void UpdateOccupancy()
    {
        bool inside = HeroInside();
        if (inside && !satisfied)
            FerryParkedPartner();
        satisfied = inside;

        if (AllDoorsSatisfied())
        {
            CommitWin(this);
            return;
        }

        VisualState want = satisfied ? VisualState.HalfLit : VisualState.Closed;
        if (state != want)
            SetState(want);
    }

    /// <summary>This door's hero currently stands in the doorway. Matched by
    /// component (not tag alone); a dead/respawning or exit-absorbed hero
    /// counts as absent.</summary>
    bool HeroInside()
    {
        if (heroNumber == 2)
        {
            PlayerCharacterTwo wisp = PlayerCharacterTwo.Get(2);
            return wisp != null && wisp.gameObject.activeSelf && !wisp.IsDead()
                && doorTrigger.OverlapPoint(wisp.transform.position);
        }
        PlayerCharacterOne ember = PlayerCharacterOne.Get(1);
        return ember != null && ember.gameObject.activeSelf && !ember.IsDead()
            && doorTrigger.OverlapPoint(ember.transform.position);
    }

    static bool AllDoorsSatisfied()
    {
        if (doors.Count == 0)
            return false;
        // Live re-poll: another door's cached flag could be a frame stale in
        // either direction (Update order across doors is arbitrary).
        for (int i = 0; i < doors.Count; i++)
        {
            if (!doors[i].HeroInside())
                return false;
        }
        return true;
    }

    /// <summary>
    /// Swap mode: a parked character must never be required to act — when the
    /// piloted hero reaches its door, bring the parked partner to its own
    /// door so the pair completes together (LavaRiser.Activate pattern).
    /// </summary>
    void FerryParkedPartner()
    {
        int partnerId = heroNumber == 1 ? 2 : 1;
        if (!SwapModeController.IsParked(partnerId))
            return;

        ExitDoor partnerDoor = null;
        for (int i = 0; i < doors.Count; i++)
        {
            if (doors[i].heroNumber == partnerId)
            {
                partnerDoor = doors[i];
                break;
            }
        }
        if (partnerDoor == null)
            return;

        if (partnerId == 1)
        {
            PlayerCharacterOne partner = PlayerCharacterOne.Get(1);
            if (partner != null && partner.gameObject.activeSelf && !partner.IsDead())
                partner.Teleport(partnerDoor.TriggerCenter);
        }
        else
        {
            PlayerCharacterTwo partner = PlayerCharacterTwo.Get(2);
            if (partner != null && partner.gameObject.activeSelf && !partner.IsDead())
                partner.Teleport(partnerDoor.TriggerCenter);
        }
    }

    static void CommitWin(ExitDoor initiator)
    {
        winFired = true;
        foreach (ExitDoor door in doors)
            door.SetState(VisualState.Open);

        // The old exit deactivated heroes on touch; here they stand in their
        // doors while these open — freeze them and fire the win anim.
        PlayerCharacterOne p1 = PlayerCharacterOne.Get(1);
        PlayerCharacterTwo p2 = PlayerCharacterTwo.Get(2);
        if (p1 != null)
        {
            p1.DisableControls();
            p1.onWin?.Invoke();
        }
        if (p2 != null)
        {
            p2.DisableControls();
            p2.onWin?.Invoke();
        }

        initiator.StartCoroutine(initiator.WinAfterDoorsOpen());
    }

    IEnumerator WinAfterDoorsOpen()
    {
        // Let the open animation read before the win panel freezes time
        // (LevelEndController sets timeScale 0; frames advance unscaled).
        float seconds = openFrames != null && fps > 0f ? openFrames.Length / fps : 0f;
        yield return new WaitForSecondsRealtime(seconds);

        // Same entry the Dragon exits used: GameState.Update completes the
        // level when dragonCount reaches 2 (levelCompleteProcessed one-shots).
        GameState gameState = FindObjectOfType<GameState>();
        if (gameState != null)
            gameState.dragonCount = 2;
    }

    void SetState(VisualState next)
    {
        state = next;
        frameIndex = 0;
        frameTimer = 0f;
        Sprite[] frames = FramesFor(next);
        if (sprite != null && frames != null && frames.Length > 0)
            sprite.sprite = frames[0];
    }

    void Animate()
    {
        Sprite[] frames = FramesFor(state);
        if (sprite == null || frames == null || frames.Length < 2)
            return;

        // Unscaled: the open frames still play under the frozen win panel.
        frameTimer += Time.unscaledDeltaTime;
        if (frameTimer >= 1f / Mathf.Max(0.01f, fps))
        {
            frameTimer = 0f;
            if (state == VisualState.Open)
                frameIndex = Mathf.Min(frameIndex + 1, frames.Length - 1); // once, hold last
            else
                frameIndex = (frameIndex + 1) % frames.Length;             // pulse loop
        }
        frameIndex = Mathf.Min(frameIndex, frames.Length - 1);
        sprite.sprite = frames[frameIndex];
    }

    Sprite[] FramesFor(VisualState s)
    {
        switch (s)
        {
            case VisualState.HalfLit: return halfLitFrames;
            case VisualState.Open: return openFrames;
            default: return closedFrames;
        }
    }
}
