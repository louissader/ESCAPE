using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Interactive end-of-level UX (win + fail panels). Replaces the old
/// auto-fade-to-level-select flow that caused "continue ends the game":
/// winning now shows a panel with explicit Next Level / Replay / Level
/// Select, and Next Level actually loads level n+1 (or the lore ending only
/// after the final level).
///
/// Lives on the GameplayUI prefab in every level; GameState finds it at
/// runtime, so legacy and generated levels behave identically.
/// </summary>
public class LevelEndController : MonoBehaviour
{
    public static LevelEndController Instance { get; private set; }

    /// <summary>True while a win/fail panel owns the timescale — the pause
    /// system must stay inert (see PauseMenu).</summary>
    public static bool IsShowing { get; private set; }

    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject failPanel;
    [SerializeField] private TextMeshProUGUI winTitle;
    [SerializeField] private TextMeshProUGUI winCrystals;
    [SerializeField] private GameObject nextButton;   // hidden on the final level

    int levelNumber;

    void Awake()
    {
        Instance = this;
        IsShowing = false;
        if (winPanel != null) winPanel.SetActive(false);
        if (failPanel != null) failPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            IsShowing = false;
        }
    }

    public void ShowWin(int level, int crystals, int totalCrystals)
    {
        levelNumber = level;
        IsShowing = true;
        Time.timeScale = 0f;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetDuck(0.4f);

        if (winTitle != null)
            winTitle.text = $"LEVEL {level} COMPLETE";
        if (winCrystals != null)
            winCrystals.text = $"STAR COINS  {crystals} / {totalCrystals}";
        if (nextButton != null)
        {
            // Final level: the button becomes the doorway to the ending —
            // hiding it made the lore finale unreachable.
            var label = nextButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = SceneRouter.IsLastLevel(level) ? "ESCAPE!" : "NEXT LEVEL";
            nextButton.SetActive(true);
        }
        if (winPanel != null)
            winPanel.SetActive(true);
    }

    public void ShowFail()
    {
        IsShowing = true;
        Time.timeScale = 0f;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetDuck(0.4f);
        if (failPanel != null)
            failPanel.SetActive(true);
    }

    // Button handlers — all restore timescale AND music duck before leaving
    // (AudioManager persists across scenes; a leaked duck muted menu music).
    void Restore()
    {
        IsShowing = false;
        Time.timeScale = 1f;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetDuck(1f);
        AudioManager.PlaySfx("ui_tap");
    }

    public void OnNextLevel()
    {
        Restore();
        if (SceneRouter.IsLastLevel(levelNumber))
            SceneRouter.LoadLore();          // the "you escaped" ending
        else
            SceneRouter.LoadLevel(levelNumber + 1);
    }

    public void OnReplay()
    {
        Restore();
        SceneRouter.ReloadCurrentLevel();
    }

    public void OnRetry()
    {
        Restore();
        SceneRouter.ReloadCurrentLevel();
    }

    public void OnLevelSelect()
    {
        Restore();
        SceneRouter.LoadLevelSelect();
    }
}
