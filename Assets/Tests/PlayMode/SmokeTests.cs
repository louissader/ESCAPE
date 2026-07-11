using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using IndieMarc.Platformer;

/// <summary>
/// End-to-end level smoke: every level loads, both players spawn, virtual
/// input moves P1, swap hands control over when P1 is removed, bats die to
/// player fireballs (B14 regression).
/// </summary>
public class SmokeTests
{
    static IEnumerator LoadLevel(int n)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(SceneRouter.LevelSceneName(n));
        while (!op.isDone)
            yield return null;
        yield return null; // one frame for Start()
    }

    [UnityTest]
    public IEnumerator EveryLevel_LoadsWithPlayersAndSystems()
    {
        for (int n = 1; n <= SceneRouter.LevelCount; n++)
        {
            yield return LoadLevel(n);
            Assert.IsNotNull(PlayerCharacterOne.Get(1), $"L{n}: Player1 missing");
            Assert.IsNotNull(PlayerCharacterTwo.Get(2), $"L{n}: Player2 missing");
            Assert.IsNotNull(Object.FindObjectOfType<GameState>(), $"L{n}: GameState missing");

            // 3 simulated seconds with no exceptions.
            float t = 0f;
            while (t < 3f)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }

    [UnityTest]
    public IEnumerator VirtualInput_MovesPlayerOne()
    {
        yield return LoadLevel(1);
        PlayerCharacterOne p1 = PlayerCharacterOne.Get(1);
        Assert.IsNotNull(p1);
        VirtualInputRouter.ResetForLevel();
        float startX = p1.transform.position.x;

        VirtualInputRouter.Get(1).move = 1f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            yield return null;
        }
        VirtualInputRouter.Get(1).Clear();

        Assert.Greater(p1.transform.position.x, startX + 0.5f,
            "virtual move input must drive Player1 right");
    }

    [UnityTest]
    public IEnumerator SwapMode_HandsControlToSurvivor_WhenP1Removed()
    {
        yield return LoadLevel(1);
        VirtualInputRouter.ResetForLevel();
        Assert.AreEqual(1, VirtualInputRouter.ActivePlayerId);

        // Simulate exit absorption: P1 deactivates; controller must auto-swap.
        PlayerCharacterOne.Get(1).gameObject.SetActive(false);
        yield return null;
        yield return null;

        Assert.AreEqual(2, VirtualInputRouter.ActivePlayerId,
            "auto-swap must hand control to Player2 when Player1 is absorbed");
    }

    [UnityTest]
    public IEnumerator PlayerFireball_KillsBat_B14()
    {
        yield return LoadLevel(1);

        // Arrange: a bat and a player-owned fireball flying at it.
        GameObject batObj = new GameObject("TestBat");
        batObj.transform.position = new Vector3(500f, 500f, 0f); // isolated
        batObj.AddComponent<BoxCollider2D>();
        var rb = batObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        Bat bat = batObj.AddComponent<Bat>();
        bat.canShoot = false;

        GameObject fireballObj = new GameObject("TestFireball");
        fireballObj.transform.position = new Vector3(495f, 500f, 0f);
        fireballObj.tag = "Fireball";
        var fireballCollider = fireballObj.AddComponent<BoxCollider2D>();
        fireballCollider.isTrigger = true;
        var fireballRb = fireballObj.AddComponent<Rigidbody2D>();
        fireballRb.gravityScale = 0f;
        Fireball fireball = fireballObj.AddComponent<Fireball>();
        fireball.owner = Fireball.Owner.Player;
        fireball.speed = 12f;

        yield return null; // Start() sets velocity toward +x
        float t = 0f;
        while (t < 2f && batObj != null)
        {
            t += Time.deltaTime;
            yield return null;
        }

        Assert.IsTrue(batObj == null, "player fireball must destroy the bat (B14)");
    }
}
