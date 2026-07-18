using UnityEngine;

/// <summary>
/// Versioned PlayerPrefs wrapper — the only place persistence keys live.
/// Fixes B1 (nothing may ever reset progress outside ResetProgress) and
/// B2 (unlocks are max-based, replays can't over-unlock).
/// </summary>
public static class SaveSystem
{
    const string KeyVersion = "escape.saveVersion";
    const string KeyHighestUnlocked = "escape.highestUnlocked";
    const string KeyStarsPrefix = "escape.stars.";
    const string KeyMusicVolume = "escape.musicVolume";
    const string KeySfxVolume = "escape.sfxVolume";
    const string KeyControlMode = "escape.controlMode";
    const string KeyStorySeen = "escape.storySeen";
    const string KeyTutorialSeen = "escape.tutorialSeen";
    const string KeyIntroSeen = "escape.introSeen";
    const string KeyCompletedPrefix = "escape.completed.";
    const string KeyLastCompleted = "escape.lastCompleted";

    const string LegacyUnlockedCount = "unlockedCount";
    const int CurrentVersion = 2;

    public enum ControlModeSetting { Auto = 0, Swap = 1, Coop = 2 }

    /// <summary>Idempotent. Call once at boot (GameInitializer).</summary>
    public static void Migrate()
    {
        int version = PlayerPrefs.GetInt(KeyVersion, 0);
        if (version >= CurrentVersion)
            return;

        if (PlayerPrefs.HasKey(LegacyUnlockedCount))
        {
            // Legacy semantics: unlockedCount N meant levels up to N+1 playable.
            int legacy = PlayerPrefs.GetInt(LegacyUnlockedCount, 0);
            int highest = Mathf.Clamp(legacy + 1, 1, SceneRouter.LevelCount);
            if (highest > HighestUnlockedLevel)
                HighestUnlockedLevel = highest;
            PlayerPrefs.DeleteKey(LegacyUnlockedCount);
        }

        PlayerPrefs.SetInt(KeyVersion, CurrentVersion);
        PlayerPrefs.Save();
    }

    /// <summary>Highest level the player may enter (1-based, >= 1).</summary>
    public static int HighestUnlockedLevel
    {
        get { return Mathf.Max(1, PlayerPrefs.GetInt(KeyHighestUnlocked, 1)); }
        private set { PlayerPrefs.SetInt(KeyHighestUnlocked, value); }
    }

    /// <summary>True once any progress beyond a fresh install exists.</summary>
    public static bool HasProgress
    {
        get { return HighestUnlockedLevel > 1 || StorySeen; }
    }

    /// <summary>Max-based unlock (B2): replaying old levels never over-unlocks.</summary>
    public static void OnLevelCompleted(int levelNumber)
    {
        int unlocked = Mathf.Clamp(levelNumber + 1, 1, SceneRouter.LevelCount);
        if (unlocked > HighestUnlockedLevel)
            HighestUnlockedLevel = unlocked;
        PlayerPrefs.SetInt(KeyCompletedPrefix + levelNumber, 1);
        // One-shot celebration marker for the level-select screen.
        PlayerPrefs.SetInt(KeyLastCompleted, levelNumber);
        PlayerPrefs.Save();
    }

    public static bool IsLevelCompleted(int levelNumber)
    {
        return PlayerPrefs.GetInt(KeyCompletedPrefix + levelNumber, 0) == 1;
    }

    /// <summary>Level that just got beaten (0 = none). Cleared after the
    /// level-select celebration animation plays once.</summary>
    public static int ConsumeLastCompleted()
    {
        int level = PlayerPrefs.GetInt(KeyLastCompleted, 0);
        if (level != 0)
        {
            PlayerPrefs.DeleteKey(KeyLastCompleted);
            PlayerPrefs.Save();
        }
        return level;
    }

    public static int StarsForLevel(int levelNumber)
    {
        return PlayerPrefs.GetInt(KeyStarsPrefix + levelNumber, 0);
    }

    public static void RecordStars(int levelNumber, int stars)
    {
        if (stars > StarsForLevel(levelNumber))
        {
            PlayerPrefs.SetInt(KeyStarsPrefix + levelNumber, Mathf.Clamp(stars, 0, 3));
            PlayerPrefs.Save();
        }
    }

    public static float MusicVolume
    {
        get { return PlayerPrefs.GetFloat(KeyMusicVolume, 0.8f); }
        set { PlayerPrefs.SetFloat(KeyMusicVolume, Mathf.Clamp01(value)); }
    }

    public static float SfxVolume
    {
        get { return PlayerPrefs.GetFloat(KeySfxVolume, 1f); }
        set { PlayerPrefs.SetFloat(KeySfxVolume, Mathf.Clamp01(value)); }
    }

    public static ControlModeSetting ControlMode
    {
        get { return (ControlModeSetting)PlayerPrefs.GetInt(KeyControlMode, 0); }
        set { PlayerPrefs.SetInt(KeyControlMode, (int)value); PlayerPrefs.Save(); }
    }

    public static bool StorySeen
    {
        get { return PlayerPrefs.GetInt(KeyStorySeen, 0) == 1; }
        set { PlayerPrefs.SetInt(KeyStorySeen, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool TutorialSeen
    {
        get { return PlayerPrefs.GetInt(KeyTutorialSeen, 0) == 1; }
        set { PlayerPrefs.SetInt(KeyTutorialSeen, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool IntroSeen
    {
        get { return PlayerPrefs.GetInt(KeyIntroSeen, 0) == 1; }
        set { PlayerPrefs.SetInt(KeyIntroSeen, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>The ONLY sanctioned full reset (settings panel, confirm dialog).</summary>
    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(KeyHighestUnlocked);
        PlayerPrefs.DeleteKey(KeyStorySeen);
        PlayerPrefs.DeleteKey(KeyTutorialSeen);
        PlayerPrefs.DeleteKey(KeyIntroSeen);
        PlayerPrefs.DeleteKey(KeyLastCompleted);
        for (int i = 1; i <= SceneRouter.LevelCount; i++)
        {
            PlayerPrefs.DeleteKey(KeyStarsPrefix + i);
            PlayerPrefs.DeleteKey(KeyCompletedPrefix + i);
        }
        PlayerPrefs.Save();
    }
}
