using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using IndieMarc.Platformer;

/// <summary>
/// Playtest 9.5: the device crashed right after clearing L7. Drives a
/// swap-mode L7 win, presses NEXT LEVEL, and lets L8 run; any logged
/// error or exception fails the test.
/// </summary>
public class LevelSevenToEightTests
{
    [TearDown]
    public void Reset()
    {
        Time.timeScale = 1f;
        SaveSystem.ControlMode = SaveSystem.ControlModeSetting.Auto;
    }

    [UnityTest]
    public IEnumerator LevelSeven_Win_ThenNextLevel_RunsLevelEight()
    {
        SaveSystem.ControlMode = SaveSystem.ControlModeSetting.Swap;
        AsyncOperation op = SceneManager.LoadSceneAsync(SceneRouter.LevelSceneName(7));
        while (!op.isDone)
            yield return null;
        yield return null;

        GameObject touch = GameObject.Find("TouchControls");
        if (touch != null)
            touch.SetActive(false);
        VirtualInputRouter.ResetForLevel();

        PlayerCharacterOne p1 = PlayerCharacterOne.Get(1);
        PlayerCharacterTwo p2 = PlayerCharacterTwo.Get(2);
        Assert.IsNotNull(p1, "Player1 missing");
        Assert.IsNotNull(p2, "Player2 missing");
        ExitDoor[] doors = Object.FindObjectsOfType<ExitDoor>();
        ExitDoor emberDoor = System.Array.Find(doors, d => d.heroNumber == 1);
        ExitDoor wispDoor = System.Array.Find(doors, d => d.heroNumber == 2);
        Assert.IsNotNull(emberDoor, "EMBER door missing");
        Assert.IsNotNull(wispDoor, "WISP door missing");

        // EMBER first (auto-handoff fires), then WISP - a real swap clear.
        float wispAt = Time.realtimeSinceStartup + 0.8f;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!LevelEndController.IsShowing && Time.realtimeSinceStartup < deadline)
        {
            if (!p1.IsDead()) p1.Teleport(emberDoor.TriggerCenter);
            if (Time.realtimeSinceStartup >= wispAt && !p2.IsDead())
                p2.Teleport(wispDoor.TriggerCenter);
            yield return null;
        }
        Assert.IsTrue(LevelEndController.IsShowing, "L7 win panel never showed");

        LevelEndController.Instance.OnNextLevel();

        float loadDeadline = Time.realtimeSinceStartup + 10f;
        while (SceneRouter.CurrentLevelNumber() != 8 && Time.realtimeSinceStartup < loadDeadline)
            yield return null;
        Assert.AreEqual(8, SceneRouter.CurrentLevelNumber(), "NEXT LEVEL did not reach L8");

        float runUntil = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < runUntil)
            yield return null;

        Assert.IsNotNull(PlayerCharacterOne.Get(1), "L8 Player1 missing");
        Assert.IsNotNull(PlayerCharacterTwo.Get(2), "L8 Player2 missing");
    }
}
