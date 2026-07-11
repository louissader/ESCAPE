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

    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject failPanel;
    [SerializeField] private TextMeshProUGUI winTitle;
    [SerializeField] private TextMeshProUGUI winCrystals;
    [SerializeField] private GameObject nextButton;   // hidden on the final level

    int levelNumber;

    void Awake()
    {
        Instance = this;
        if (winPanel != null) winPanel.SetActive(false);
        if (failPanel != null) failPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowWin(int level, int crystals, int totalCrystals)
    {
        levelNumber = level;
        Time.timeScale = 0f;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetDuck(0.4f);

        if (winTitle != null)
            winTitle.text = $"LEVEL {level} COMPLETE";
        if (winCrystals != null)
            winCrystals.text = $"STAR COINS  {crystals} / {totalCrystals}";
        if (nextButton != null)
            nextButton.SetActive(!SceneRouter.IsLastLevel(level));
        if (winPanel != null)
            winPanel.SetActive(true);
    }

    public void ShowFail()
    {
        Time.timeScale = 0f;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetDuck(0.4f);
        if (failPanel != null)
            failPanel.SetActive(true);
    }

    // Button handlers — all restore timescale before leaving.
    public void OnNextLevel()
    {
        Time.timeScale = 1f;
        AudioManager.PlaySfx("ui_tap");
        if (SceneRouter.IsLastLevel(levelNumber))
            SceneRouter.LoadLore();          // the "you escaped" ending
        else
            SceneRouter.LoadLevel(levelNumber + 1);
    }

    public void OnReplay()
    {
        Time.timeScale = 1f;
        AudioManager.PlaySfx("ui_tap");
        SceneRouter.ReloadCurrentLevel();
    }

    public void OnRetry()
    {
        Time.timeScale = 1f;
        AudioManager.PlaySfx("ui_tap");
        SceneRouter.ReloadCurrentLevel();
    }

    public void OnLevelSelect()
    {
        Time.timeScale = 1f;
        AudioManager.PlaySfx("ui_tap");
        SceneRouter.LoadLevelSelect();
    }
}
