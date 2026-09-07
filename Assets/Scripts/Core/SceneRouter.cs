using UnityEngine.SceneManagement;

/// <summary>
/// Single authority for scene navigation. All scene loads go through here by
/// NAME so build-order changes and new levels never break navigation (B5).
/// </summary>
public static class SceneRouter
{
    public const string MainMenu = "Menu Scene";
    public const string Story = "Story Details";
    public const string LevelSelect = "Level Scene";
    public const string Lore = "MoreLore";
    public const string IntroScene = "Intro";
    public const string LevelPrefix = "Level ";

    /// <summary>Total playable levels.</summary>
    public const int LevelCount = 8;

    /// <summary>First level of the shipped progression. All 8 levels ship
    /// (reverted from 4 for the 1.2.0 App Store resubmission — a 5-level
    /// campaign starting on mid-game content was a rejection risk). Raising
    /// this disables the early levels without deleting them; rebake
    /// (Escape/Run Playtest-Fix Pipeline) after any change. Internal
    /// numbering never changes — only what the player sees, via
    /// DisplayNumber.</summary>
    public const int FirstLevel = 1;

    /// <summary>Number of levels in the shipped progression.</summary>
    public const int PlayableLevelCount = LevelCount - FirstLevel + 1;

    /// <summary>Player-facing level number (identity while FirstLevel is 1).</summary>
    public static int DisplayNumber(int levelNumber)
    {
        return levelNumber - FirstLevel + 1;
    }

    public static string LevelSceneName(int levelNumber)
    {
        return LevelPrefix + levelNumber;
    }

    public static void LoadMainMenu() { SceneManager.LoadScene(MainMenu); }
    public static void LoadStory() { SceneManager.LoadScene(Story); }
    public static void LoadLevelSelect() { SceneManager.LoadScene(LevelSelect); }
    public static void LoadLore() { SceneManager.LoadScene(Lore); }
    public static void LoadIntro() { SceneManager.LoadScene(IntroScene); }

    public static void LoadLevel(int levelNumber)
    {
        SceneManager.LoadScene(LevelSceneName(levelNumber));
    }

    public static void ReloadCurrentLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>Level number of the active scene, or 0 when not in a level.</summary>
    public static int CurrentLevelNumber()
    {
        string name = SceneManager.GetActiveScene().name;
        if (name.StartsWith(LevelPrefix) &&
            int.TryParse(name.Substring(LevelPrefix.Length), out int n))
            return n;
        return 0;
    }

    public static bool IsLastLevel(int levelNumber)
    {
        return levelNumber >= LevelCount;
    }
}
