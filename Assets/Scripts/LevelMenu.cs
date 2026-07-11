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
    public void NextContinueButton() { SceneRouter.LoadLore(); }
    public void LoreRestartButton() { SceneRouter.LoadMainMenu(); }
    public void LevelMenuButton() { SceneRouter.LoadLevelSelect(); }
}
