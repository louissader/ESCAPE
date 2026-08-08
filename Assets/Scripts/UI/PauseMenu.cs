using UnityEngine;

/// <summary>
/// Time-scale pause with music duck. Android back key (Escape) toggles it;
/// OnApplicationPause auto-pauses so backgrounding never costs a life.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject overlay;
    [SerializeField] private GameObject settingsPanel;

    public static bool IsPaused { get; private set; }

    void Start()
    {
        SetPaused(false);
    }

    void OnDestroy()
    {
        // Never leave a dead scene with frozen time — and never leave the
        // (static, scene-surviving) modal guard held by a dead menu.
        if (IsPaused)
            ModalGuard.Pop();
        Time.timeScale = 1f;
        IsPaused = false;
    }

    void Update()
    {
        // Android back button maps to Escape in the legacy input manager.
        // The how-to-play card owns Escape while showing (and for the press
        // that closed it — Update order between the two is arbitrary).
        if (Input.GetKeyDown(KeyCode.Escape) &&
            !HowToPlayPanel.IsOpen && !HowToPlayPanel.ClosedThisFrame)
            Toggle();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && !IsPaused && !LevelEndController.IsShowing)
            SetPaused(true);
    }

    public void Toggle()
    {
        // A win/fail panel owns the timescale — unpausing here would resume
        // gameplay behind it (enemies moving, lava rising).
        if (LevelEndController.IsShowing)
            return;
        SetPaused(!IsPaused);
    }

    public void OnPauseButton()
    {
        if (LevelEndController.IsShowing)
            return;
        SetPaused(true);
    }
    public void OnResumeButton() { SetPaused(false); }

    public void OnRestartButton()
    {
        SetPaused(false);
        SceneRouter.ReloadCurrentLevel();
    }

    public void OnLevelSelectButton()
    {
        SetPaused(false);
        SceneRouter.LoadLevelSelect();
    }

    public void OnSettingsButton()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    void SetPaused(bool paused)
    {
        // Edge-triggered guard push/pop: Start() calls SetPaused(false) on
        // an already-unpaused menu, and the guard is a ref-count — only a
        // real state change may move it. The settings panel needs no push
        // of its own: it only ever opens while paused, so this one guard
        // covers it.
        bool wasPaused = IsPaused;
        if (paused && !wasPaused)
            ModalGuard.Push();
        else if (!paused && wasPaused)
            ModalGuard.Pop();

        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        if (overlay != null)
            overlay.SetActive(paused);
        if (!paused && settingsPanel != null)
            settingsPanel.SetActive(false);
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetDuck(paused ? 0.4f : 1f);
    }
}
