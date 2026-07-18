using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using IndieMarc.Platformer;

/// <summary>
/// Playtest 5: heroes overlap instead of shoving each other (Physics2D
/// ignore pairs set up by PlayerPassThrough), while WISP's separate platform
/// form stays solid so EMBER can still ride transform-boosts.
/// </summary>
public class PlayerPassThroughTests
{
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

    [UnityTest]
    public IEnumerator Heroes_OverlapWithoutShoving()
    {
        yield return LoadLevel(1);
        PlayerCharacterOne p1 = PlayerCharacterOne.Get(1);
        PlayerCharacterTwo p2 = PlayerCharacterTwo.Get(2);
        Assert.IsNotNull(p1, "Player1 missing");
        Assert.IsNotNull(p2, "Player2 missing");
        OwnInputChannels();

        // Stack P1 directly onto P2 and let physics run: without the ignore
        // pairs, depenetration shoves both apart within a few fixed steps.
        p1.Teleport(p2.transform.position);
        float p1StartX = p1.transform.position.x;
        float p2StartX = p2.transform.position.x;

        yield return StepPhysics(30);

        Assert.Less(Mathf.Abs(p1.transform.position.x - p1StartX), 0.25f,
            "P1 must not be shoved sideways by overlapping P2");
        Assert.Less(Mathf.Abs(p2.transform.position.x - p2StartX), 0.25f,
            "P2 must not be shoved sideways by overlapping P1");
        Assert.IsFalse(p1.IsDead(), "P1 must survive the overlap");
        Assert.IsFalse(p2.IsDead(), "P2 must survive the overlap");
    }

    [UnityTest]
    public IEnumerator Ember_StandsOnWispPlatformForm()
    {
        yield return LoadLevel(1);
        PlayerCharacterOne p1 = PlayerCharacterOne.Get(1);
        PlayerCharacterTwo p2 = PlayerCharacterTwo.Get(2);
        Assert.IsNotNull(p1, "Player1 missing");
        Assert.IsNotNull(p2, "Player2 missing");
        OwnInputChannels();

        if (p2.rectanglePrefab == null)
            Assert.Ignore("L1 WISP has no rectanglePrefab assigned — platform form not testable here");

        // Trigger the platform form through the real input path (one-shot
        // action latch is consumed by PlayerControlsTwo.Update).
        p2.CollectPowerUp();
        VirtualInputRouter.Get(2).PressAction();
        yield return null;
        yield return null;

        GameObject platform = GameObject.Find("RectanglePlatform");
        Assert.IsNotNull(platform, "platform form must spawn RectanglePlatform");

        // Drop EMBER onto the platform and let physics settle: the platform
        // is a separate GameObject, deliberately NOT in the ignore set.
        p1.Teleport(platform.transform.position + Vector3.up * 1.5f);
        yield return StepPhysics(50);

        float restY = p1.transform.position.y;
        Assert.Greater(restY, platform.transform.position.y + 0.1f,
            "EMBER must come to rest ON the platform, not fall through it");

        yield return StepPhysics(30);
        Assert.Less(Mathf.Abs(p1.transform.position.y - restY), 0.05f,
            "EMBER must stay atop the transformed WISP");
    }
}
