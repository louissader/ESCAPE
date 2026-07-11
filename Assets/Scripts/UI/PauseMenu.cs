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
        // Never leave a dead scene with frozen time.
        Time.timeScale = 1f;
        IsPaused = false;
    }

    void Update()
    {
        // Android back button maps to Escape in the legacy input manager.
        if (Input.GetKeyDown(KeyCode.Escape))
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
