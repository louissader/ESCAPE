using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Regression tests for B1 (progress wipe) and B2 (unlock over-count).
/// PlayerPrefs state is snapshotted per-test via unique keys cleanup.
/// </summary>
public class SaveSystemTests
{
    [SetUp]
    public void FreshState()
    {
        SaveSystem.ResetProgress();
        PlayerPrefs.DeleteKey("unlockedCount");
        PlayerPrefs.DeleteKey("escape.saveVersion");
    }

    [TearDown]
    public void Cleanup()
    {
        SaveSystem.ResetProgress();
        PlayerPrefs.DeleteKey("unlockedCount");
        PlayerPrefs.DeleteKey("escape.saveVersion");
    }

    [Test]
    public void FreshInstall_StartsAtFirstLevel()
    {
        SaveSystem.Migrate();
        Assert.AreEqual(SceneRouter.FirstLevel, SaveSystem.HighestUnlockedLevel,
            "fresh saves start at the shipped progression's first level " +
            "(legacy 1-3 are disabled via SceneRouter.FirstLevel)");
    }

    [Test]
    public void LegacyKey_MigratesToNewSemantics()
    {
        // Legacy unlockedCount=2 -> raw highest 3. The getter floors to
        // FirstLevel at read time; the expectation is written so it stays
        // correct when FirstLevel reverts to 1 (raw 3 then reads back as 3).
        PlayerPrefs.SetInt("unlockedCount", 2);
        SaveSystem.Migrate();
        Assert.AreEqual(Mathf.Max(3, SceneRouter.FirstLevel), SaveSystem.HighestUnlockedLevel);
        Assert.AreEqual(3, PlayerPrefs.GetInt("escape.highestUnlocked", -1),
            "raw migrated value must be PRESERVED (floor applies only at read " +
            "time) so re-enabling levels 1-3 restores real progress");
        Assert.IsFalse(PlayerPrefs.HasKey("unlockedCount"), "legacy key must be consumed");
    }

    [Test]
    public void HighestUnlocked_FloorsToFirstLevel()
    {
        PlayerPrefs.SetInt("escape.highestUnlocked", 1);
        Assert.AreEqual(SceneRouter.FirstLevel, SaveSystem.HighestUnlockedLevel,
            "stale saves below FirstLevel must floor up, never route to a disabled level");
    }

    [Test]
    public void HighestUnlocked_ClampsToLevelCount()
    {
        PlayerPrefs.SetInt("escape.highestUnlocked", 99);
        Assert.AreEqual(SceneRouter.LevelCount, SaveSystem.HighestUnlockedLevel,
            "a corrupt/stale save must never route to a scene past the last level");
    }

    [Test]
    public void Migration_IsIdempotent()
    {
        PlayerPrefs.SetInt("unlockedCount", 1);
        SaveSystem.Migrate();
        int after = SaveSystem.HighestUnlockedLevel;
        SaveSystem.Migrate();
        SaveSystem.Migrate();
        Assert.AreEqual(after, SaveSystem.HighestUnlockedLevel);
    }

    [Test]
    public void CompletingALevel_UnlocksOnlyTheNext()
    {
        SaveSystem.Migrate();
        SaveSystem.OnLevelCompleted(SceneRouter.FirstLevel);
        Assert.AreEqual(SceneRouter.FirstLevel + 1, SaveSystem.HighestUnlockedLevel);
    }

    [Test]
    public void ReplayingAnOldLevel_NeverOverUnlocks_B2()
    {
        SaveSystem.Migrate();
        SaveSystem.OnLevelCompleted(SceneRouter.FirstLevel);
        SaveSystem.OnLevelCompleted(SceneRouter.FirstLevel);
        SaveSystem.OnLevelCompleted(SceneRouter.FirstLevel);
        Assert.AreEqual(SceneRouter.FirstLevel + 1, SaveSystem.HighestUnlockedLevel,
            "replaying the first level three times must not unlock further levels");
    }

    [Test]
    public void Stars_RecordMaxOnly()
    {
        SaveSystem.RecordStars(1, 2);
        SaveSystem.RecordStars(1, 1);
        Assert.AreEqual(2, SaveSystem.StarsForLevel(1));
        SaveSystem.RecordStars(1, 3);
        Assert.AreEqual(3, SaveSystem.StarsForLevel(1));
    }

    [Test]
    public void IntroSeen_RoundTrips()
    {
        Assert.IsFalse(SaveSystem.IntroSeen, "fresh state starts with the intro unseen");
        SaveSystem.IntroSeen = true;
        Assert.IsTrue(SaveSystem.IntroSeen);
        SaveSystem.IntroSeen = false;
        Assert.IsFalse(SaveSystem.IntroSeen);
    }

    [Test]
    public void IntroSeen_ClearedByResetProgress()
    {
        SaveSystem.IntroSeen = true;
        SaveSystem.ResetProgress();
        Assert.IsFalse(SaveSystem.IntroSeen,
            "the sanctioned full reset must also replay the intro");
    }
}
