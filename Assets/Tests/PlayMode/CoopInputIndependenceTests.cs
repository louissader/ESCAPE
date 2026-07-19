using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using IndieMarc.Platformer;

/// <summary>
/// Playtest 6 P0 regression guard: "WASD and arrow key movement got
/// completely messed up after the pass-through change".
///
/// Root cause was NOT the input merge or PlayerPassThrough — it was
/// ControlModeResolver's phone/tablet size heuristic resolving SWAP on
/// desktop (retina Mac windowed builds compute a sub-7-inch "diagonal"),
/// which parks WISP and lets the new ExitDoor ferry teleport the second
/// keyboard pilot's character and insta-win the level. The fix pins
/// non-mobile platforms to Co-op (explicit user Swap still honored).
///
/// These tests drive each hero through the VirtualInputChannel seam (the
/// same merge path keyboard flows through) and pin down:
///   1. desktop Auto resolves Co-op and nobody is parked in a level,
///   2. the two heroes translate independently while overlapping,
///   3. grounded jump works for both while overlapping.
/// </summary>
public class CoopInputIndependenceTests
{
    SaveSystem.ControlModeSetting savedSetting;

    [SetUp]
    public void SaveControlModePref()
    {
        // Resolve() consults the persisted user setting first — force Auto so
        // the test exercises the desktop default, then restore the user's
        // real preference afterwards (PlayerPrefs persist on dev machines).
        savedSetting = SaveSystem.ControlMode;
        SaveSystem.ControlMode = SaveSystem.ControlModeSetting.Auto;
    }

    [TearDown]
    public void RestoreControlModePref()
    {
        SaveSystem.ControlMode = savedSetting;
    }

    static IEnumerator LoadLevel(int n)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(SceneRouter.LevelSceneName(n));
        while (!op.isDone)
            yield return null;
        yield return null; // one frame for Start()
    }

    static IEnumerator StepPhysics(int fixedSteps)
    {
        for (int i = 0; i < fixedSteps; i++)
            yield return new WaitForFixedUpdate();
    }

    static void OwnInputChannels()
    {
        // The on-screen controls write the channels every frame (no touch =
        // move 0) — disable them so the test owns the channel.
        GameObject touchControls = GameObject.Find("TouchControls");
        if (touchControls != null)
            touchControls.SetActive(false);
        VirtualInputRouter.ResetForLevel();
    }

    static IEnumerator SettleUntilGrounded(PlayerCharacterOne p1, PlayerCharacterTwo p2)
    {
        for (int i = 0; i < 180; i++)
        {
            yield return new WaitForFixedUpdate();
            if (p1.IsGrounded() && p2.IsGrounded())
                yield break;
        }
        Assert.Fail("Heroes never settled onto the ground (grounded raycast broken?)");
    }

    [UnityTest]
    public IEnumerator Desktop_AutoControlMode_ResolvesCoop_AndNobodyIsParked()
    {
        // The regression trigger: editor/desktop must NEVER fall into the
        // phone-size heuristic (it misreads small windows as phones and turns
        // on swap parking + the ExitDoor ferry, hijacking keyboard co-op).
        Assert.AreEqual(ControlMode.Coop, ControlModeResolver.Resolve(),
            "Auto control mode must resolve Co-op on non-mobile platforms");

        yield return LoadLevel(1);

        Assert.IsFalse(SwapModeController.IsParked(1), "EMBER must not be parked in desktop co-op");
        Assert.IsFalse(SwapModeController.IsParked(2), "WISP must not be parked in desktop co-op");
        if (SwapModeController.Instance != null)
        {
            Assert.AreEqual(ControlMode.Coop, SwapModeController.Instance.Mode,
                "Level swap controller must be inert (Co-op) on desktop");
        }
    }

    [UnityTest]
    public IEnumerator Heroes_TranslateIndependently_WhileOverlapped()
    {
        yield return LoadLevel(1);
        PlayerCharacterOne p1 = PlayerCharacterOne.Get(1);
        PlayerCharacterTwo p2 = PlayerCharacterTwo.Get(2);
        Assert.IsNotNull(p1, "Player1 missing");
        Assert.IsNotNull(p2, "Player2 missing");
        OwnInputChannels();

        // Overlap the heroes (pass-through allows it) on P2's spawn ground.
        p1.Teleport(p2.transform.position);
        yield return SettleUntilGrounded(p1, p2);

        // Drive ONLY P1 right through the virtual seam (same merge path as
        // the keyboard). Short push: stays on the spawn platform.
        float p1Start = p1.transform.position.x;
        float p2Start = p2.transform.position.x;
        VirtualInputRouter.Get(1).move = 1f;
        yield return StepPhysics(15);
        VirtualInputRouter.Get(1).move = 0f;

        Assert.Greater(p1.transform.position.x - p1Start, 0.25f,
            "P1 must move when only P1's channel is driven");
        Assert.Less(Mathf.Abs(p2.transform.position.x - p2Start), 0.1f,
            "P2 must NOT move when only P1's channel is driven");

        // Re-overlap and drive ONLY P2 over the ground P1 just traversed.
        yield return StepPhysics(10);
        p1.Teleport(p2.transform.position);
        yield return SettleUntilGrounded(p1, p2);

        p1Start = p1.transform.position.x;
        p2Start = p2.transform.position.x;
        VirtualInputRouter.Get(2).move = 1f;
        yield return StepPhysics(15);
        VirtualInputRouter.Get(2).move = 0f;

        Assert.Greater(p2.transform.position.x - p2Start, 0.25f,
            "P2 must move when only P2's channel is driven");
        Assert.Less(Mathf.Abs(p1.transform.position.x - p1Start), 0.1f,
            "P1 must NOT move when only P2's channel is driven");
    }

    [UnityTest]
    public IEnumerator GroundedJump_WorksForBoth_WhileOverlapped()
    {
        yield return LoadLevel(1);
        PlayerCharacterOne p1 = PlayerCharacterOne.Get(1);
        PlayerCharacterTwo p2 = PlayerCharacterTwo.Get(2);
        Assert.IsNotNull(p1, "Player1 missing");
        Assert.IsNotNull(p2, "Player2 missing");
        OwnInputChannels();

        p1.Teleport(p2.transform.position);
        yield return SettleUntilGrounded(p1, p2);

        // --- P1 jumps, P2 stays put ---------------------------------------
        float p1StartY = p1.transform.position.y;
        float p2StartY = p2.transform.position.y;
        // Hold jump through the ascent — releasing early triggers the
        // variable-height jump cut and the hop never clears the threshold.
        // Track the PEAK rather than the position after N steps (the hero
        // may already be descending by then).
        VirtualInputRouter.Get(1).PressJump();
        float p1Peak = p1StartY;
        for (int i = 0; i < 25; i++)
        {
            yield return StepPhysics(1);
            p1Peak = Mathf.Max(p1Peak, p1.transform.position.y);
        }
        VirtualInputRouter.Get(1).ReleaseJump();

        Assert.Greater(p1Peak - p1StartY, 0.25f,
            "P1 grounded jump must lift off while overlapping P2");
        Assert.Less(Mathf.Abs(p2.transform.position.y - p2StartY), 0.15f,
            "P2 must NOT jump when only P1's channel jumped");
        Assert.IsTrue(p2.IsGrounded(),
            "P2 must stay grounded while P1 jumps out of the overlap");

        // --- P1 lands, re-overlap, P2 jumps, P1 stays put ------------------
        yield return SettleUntilGrounded(p1, p2);
        p1.Teleport(p2.transform.position);
        yield return SettleUntilGrounded(p1, p2);

        p1StartY = p1.transform.position.y;
        p2StartY = p2.transform.position.y;
        VirtualInputRouter.Get(2).PressJump();
        float p2Peak = p2StartY;
        for (int i = 0; i < 25; i++)
        {
            yield return StepPhysics(1);
            p2Peak = Mathf.Max(p2Peak, p2.transform.position.y);
        }
        VirtualInputRouter.Get(2).ReleaseJump();

        Assert.Greater(p2Peak - p2StartY, 0.25f,
            "P2 grounded jump must lift off while overlapping P1");
        Assert.Less(Mathf.Abs(p1.transform.position.y - p1StartY), 0.15f,
            "P1 must NOT jump when only P2's channel jumped");
        Assert.IsTrue(p1.IsGrounded(),
            "P1 must stay grounded while P2 jumps out of the overlap");
    }
}
