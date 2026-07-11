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
    public void FreshInstall_StartsWithLevel1Unlocked()
    {
        SaveSystem.Migrate();
        Assert.AreEqual(1, SaveSystem.HighestUnlockedLevel);
    }

    [Test]
    public void LegacyKey_MigratesToNewSemantics()
    {
        // Legacy unlockedCount=2 meant levels 1-3 playable.
        PlayerPrefs.SetInt("unlockedCount", 2);
        SaveSystem.Migrate();
        Assert.AreEqual(3, SaveSystem.HighestUnlockedLevel);
        Assert.IsFalse(PlayerPrefs.HasKey("unlockedCount"), "legacy key must be consumed");
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
        SaveSystem.OnLevelCompleted(1);
        Assert.AreEqual(2, SaveSystem.HighestUnlockedLevel);
    }

    [Test]
    public void ReplayingAnOldLevel_NeverOverUnlocks_B2()
    {
        SaveSystem.Migrate();
        SaveSystem.OnLevelCompleted(1);
        SaveSystem.OnLevelCompleted(1);
        SaveSystem.OnLevelCompleted(1);
        Assert.AreEqual(2, SaveSystem.HighestUnlockedLevel,
            "replaying level 1 three times must not unlock level 3+");
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
}
