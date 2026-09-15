using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using IndieMarc.Platformer;

/// <summary>
/// Playtest 6 (P0): the two-door win must actually FIRE in a built level.
/// Level 2's generated scene carries both ExitDoors; this drives both heroes
/// onto their trigger centers and asserts the win handoff (ExitDoor sets
/// GameState.dragonCount = 2 after the open animation; LevelEndController
/// then shows the win panel when the in-level UI exists).
/// </summary>
public class ExitDoorWinTests
{
    [TearDown]
    public void ResetTimeScale()
    {
        // LevelEndController freezes time when the win panel shows — never
        // leak a zero timescale into the next test.
        Time.timeScale = 1f;
    }

    static IEnumerator LoadLevel(int n)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(SceneRouter.LevelSceneName(n));
        while (!op.isDone)
            yield return null;
        yield return null; // one frame for Start() (door registry fills in Start)
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

    [UnityTest]
    public IEnumerator LevelTwo_BothHeroesAtDoors_WinFires()
    {
        yield return LoadLevel(2);
        OwnInputChannels();

        PlayerCharacterOne p1 = PlayerCharacterOne.Get(1);
        PlayerCharacterTwo p2 = PlayerCharacterTwo.Get(2);
        Assert.IsNotNull(p1, "Player1 missing");
        Assert.IsNotNull(p2, "Player2 missing");

        ExitDoor[] doors = Object.FindObjectsOfType<ExitDoor>();
        Assert.AreEqual(2, doors.Length, "L2 must carry exactly two ExitDoors");
        ExitDoor emberDoor = System.Array.Find(doors, d => d.heroNumber == 1);
        ExitDoor wispDoor = System.Array.Find(doors, d => d.heroNumber == 2);
        Assert.IsNotNull(emberDoor, "EMBER door (heroNumber 1) missing");
        Assert.IsNotNull(wispDoor, "WISP door (heroNumber 2) missing");
        Assert.IsTrue(ExitDoor.AnyInLevel,
            "doors must register in Start so the legacy Dragon win path stands down");

        GameState gameState = Object.FindObjectOfType<GameState>();
        Assert.IsNotNull(gameState, "GameState missing");

        // Hold both heroes in their doorways: re-teleport every frame so
        // gravity cannot drop a pivot out of the trigger before occupancy
        // registers (ExitDoor tests the hero PIVOT via OverlapPoint in
        // Update). The win handoff sets dragonCount = 2 roughly 0.7s of
        // realtime after both doors are held (open animation plays first).
        float deadline = Time.realtimeSinceStartup + 10f;
        while (gameState.dragonCount < 2 && !LevelEndController.IsShowing &&
               Time.realtimeSinceStartup < deadline)
        {
            if (!p1.IsDead())
                p1.Teleport(emberDoor.TriggerCenter);
            if (!p2.IsDead())
                p2.Teleport(wispDoor.TriggerCenter);
            yield return null;
        }

        Assert.IsTrue(gameState.dragonCount >= 2 || LevelEndController.IsShowing,
            "win must fire when both heroes hold their matched doors " +
            $"(dragonCount={gameState.dragonCount}, IsShowing={LevelEndController.IsShowing})");

        // Player-facing confirmation: the win panel, whenever the in-level
        // UI (GameplayUI prefab) is present in the scene.
        if (LevelEndController.Instance != null)
        {
            float panelDeadline = Time.realtimeSinceStartup + 3f;
            while (!LevelEndController.IsShowing && Time.realtimeSinceStartup < panelDeadline)
                yield return null;
            Assert.IsTrue(LevelEndController.IsShowing,
                "LevelEndController must show the win panel after the door win");
        }
    }
}
