using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Title-screen controller (supersedes the legacy MainMenu; the scene is
/// assembled by TitleScreenBuilder, which wires every button here through
/// persistent onClick calls). One big PLAY button on the bare title screen:
/// first launch routes straight into the intro cutscene, returning players
/// get an in-place submenu (CONTINUE / STORY / HOW TO PLAY) that fades in
/// over the title art on unscaled time. QUIT is desktop-only — hidden on
/// mobile per platform guidelines. The VersionLabel TMP (if present) is
/// refreshed from Application.version at Start.
/// </summary>
public class TitleMenu : MonoBehaviour
{
    const float SubmenuFadeSeconds = 0.15f;
    const float SubmenuStartScale = 0.92f;

    public Button playButton;
    public Button quitButton;
    public Button continueButton;
    public Button storyButton;
    public Button howToPlayButton;
    public Button closeSubmenuButton;
    public GameObject submenuPanel;
    public HowToPlayPanel howToPlayPanel;

    CanvasGroup submenuGroup;

    void Start()
    {
        // B11: Application.Quit() is a no-op on iOS and against HIG on mobile.
        if (quitButton != null)
            quitButton.gameObject.SetActive(!Application.isMobilePlatform);
        if (submenuPanel != null)
            submenuPanel.SetActive(false);

        GameObject versionLabel = GameObject.Find("VersionLabel");
        if (versionLabel != null &&
            versionLabel.TryGetComponent(out TextMeshProUGUI versionText))
            versionText.text = "v" + Application.version;
    }

    void Update()
    {
        // Android back button maps to Escape in the legacy input manager.
        // The how-to-play card owns the key while it shows (and for the
        // press that closed it — Update order between the two is arbitrary).
        if (SubmenuVisible() && Input.GetKeyDown(KeyCode.Escape) &&
            !HowToPlayPanel.IsOpen && !HowToPlayPanel.ClosedThisFrame)
            OnCloseSubmenu();
    }

    bool SubmenuVisible()
    {
        return submenuPanel != null && submenuPanel.activeSelf;
    }

    public void OnPlay()
    {
        AudioManager.PlaySfx("ui_tap");

        // First launch: the intro cutscene flows into Level 1 (IntroCutscene
        // sets IntroSeen when it finishes or is skipped).
        if (!SaveSystem.IntroSeen)
        {
            IntroCutscene.ReplayMode = false;
            SceneRouter.LoadIntro();
            return;
        }

        // Returning players choose CONTINUE / STORY / HOW TO PLAY in place.
        if (submenuPanel == null)
        {
            SceneRouter.LoadLevelSelect();   // unmigrated-scene safety
            return;
        }
        submenuPanel.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeSubmenu(show: true));
    }

    public void OnContinue()
    {
        AudioManager.PlaySfx("ui_tap");
        SceneRouter.LoadLevelSelect();
    }

    public void OnStory()
    {
        AudioManager.PlaySfx("ui_tap");
        // Rewatch the intro: ReplayMode makes IntroCutscene return here when
        // it ends and write no progress.
        IntroCutscene.ReplayMode = true;
        SceneRouter.LoadIntro();
    }

    public void OnHowToPlay()
    {
        if (howToPlayPanel != null)
            howToPlayPanel.Open();   // Open() plays its own ui_tap
    }

    public void OnCloseSubmenu()
    {
        if (!SubmenuVisible())
            return;
        AudioManager.PlaySfx("ui_tap");
        StopAllCoroutines();
        StartCoroutine(FadeSubmenu(show: false));
    }

    public void OnQuit()
    {
        AudioManager.PlaySfx("ui_tap");
        Application.Quit();
    }

    IEnumerator FadeSubmenu(bool show)
    {
        if (submenuGroup == null)
        {
            submenuGroup = submenuPanel.GetComponent<CanvasGroup>();
            if (submenuGroup == null)
                submenuGroup = submenuPanel.AddComponent<CanvasGroup>();
        }
        // Scale the centered card, not the full-screen dim behind it.
        Transform card = submenuPanel.transform.Find("Card");
        Transform scaled = card != null ? card : submenuPanel.transform;

        float t = 0f;
        while (t < SubmenuFadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / SubmenuFadeSeconds);
            if (!show)
                k = 1f - k;
            submenuGroup.alpha = k;
            scaled.localScale = Vector3.one * Mathf.Lerp(SubmenuStartScale, 1f, k);
            yield return null;
        }

        submenuGroup.alpha = 1f;   // ready for the next activation
        scaled.localScale = Vector3.one;
        if (!show)
            submenuPanel.SetActive(false);
    }
}
