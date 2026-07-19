using UnityEngine;

/// <summary>
/// LEGACY — superseded by TitleMenu. TitleScreenBuilder removes this
/// component (and the buttons that called it) from Menu Scene when it
/// rebuilds the title screen. Kept compiling unchanged so an unmigrated /
/// not-yet-rebuilt scene still routes correctly; delete once the redesign
/// has shipped everywhere.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject quitButton;

    void Start()
    {
        // B11: Application.Quit() is a no-op on iOS and against HIG on mobile.
        if (quitButton == null)
            quitButton = GameObject.Find("QuitButton");
        if (quitButton != null)
            quitButton.SetActive(!Application.isMobilePlatform);
    }

    public void OnPlayButton()
    {
        // First launch plays the intro cutscene, which flows into Level 1
        // (IntroCutscene sets IntroSeen when it finishes or is skipped).
        if (!SaveSystem.IntroSeen)
        {
            SceneRouter.LoadIntro();
            return;
        }

        // Returning players skip the story (it stays reachable from level select).
        if (SaveSystem.StorySeen)
            SceneRouter.LoadLevelSelect();
        else
            SceneRouter.LoadStory();
    }

    public void OnStoryButton()
    {
        // Rewatch the intro from the menu: ReplayMode makes IntroCutscene
        // return here when it ends and write no progress.
        IntroCutscene.ReplayMode = true;
        SceneRouter.LoadIntro();
    }

    public void OnQuitButton()
    {
        Application.Quit();
    }
}
