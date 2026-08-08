using UnityEngine;

/// <summary>
/// Level select + lore navigation. Unlock state comes from SaveSystem only.
/// Phase 6 replaces the hand-wired buttons with a generated 8-tile grid;
/// until then the serialized level 2/3 fields drive the existing scene.
/// </summary>
public class LevelMenu : MonoBehaviour
{
    public GameObject level2Object;
    public GameObject level3Object;

    public GameObject level2Lock;
    public GameObject level3Lock;

    void Start()
    {
        int highest = SaveSystem.HighestUnlockedLevel;

        SetLevelState(level2Object, level2Lock, highest >= 2);
        SetLevelState(level3Object, level3Lock, highest >= 3);
    }

    static void SetLevelState(GameObject button, GameObject lockIcon, bool unlocked)
    {
        if (button != null)
            button.SetActive(unlocked);
        if (lockIcon != null)
            lockIcon.SetActive(!unlocked);
    }

    public void MainMenuButton() { SceneRouter.LoadMainMenu(); }
    public void PlayLevel1Button() { PlayLevel(1); }
    public void PlayLevel2Button() { PlayLevel(2); }
    public void PlayLevel3Button() { PlayLevel(3); }

    public void PlayLevel(int levelNumber)
    {
        if (levelNumber <= SaveSystem.HighestUnlockedLevel)
            SceneRouter.LoadLevel(levelNumber);
    }

    public void ContinueButton()
    {
        // Called from the Story Details scene: the story has now been seen.
        SaveSystem.StorySeen = true;
        SceneRouter.LoadLevelSelect();
    }

    public void StoryButton() { SceneRouter.LoadStory(); }

    /// <summary>CONTINUE on level select: resume progress at the next
    /// unbeaten level (HighestUnlockedLevel is exactly that — completing
    /// level n unlocks n+1, and replays never over-unlock). Only once the
    /// final level is beaten does it lead to the lore ending. It used to
    /// call LoadLore() unconditionally, which spoiled the ending and, with
    /// the Main Menu button unreachable and no Escape key on iOS, made
    /// level select a dead end.</summary>
    public void NextContinueButton()
    {
        if (SaveSystem.IsLevelCompleted(SceneRouter.LevelCount))
            SceneRouter.LoadLore();
        else
            SceneRouter.LoadLevel(SaveSystem.HighestUnlockedLevel);
    }
    public void LoreRestartButton() { SceneRouter.LoadMainMenu(); }
    public void LevelMenuButton() { SceneRouter.LoadLevelSelect(); }
}
