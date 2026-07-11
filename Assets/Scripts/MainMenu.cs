using UnityEngine;

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
        // Returning players skip the story (it stays reachable from level select).
        if (SaveSystem.StorySeen)
            SceneRouter.LoadLevelSelect();
        else
            SceneRouter.LoadStory();
    }

    public void OnQuitButton()
    {
        Application.Quit();
    }
}
