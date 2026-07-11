using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using IndieMarc.Platformer;

/// <summary>
/// Per-level orchestrator. Event-driven off player deaths (no is_dead
/// polling); scene navigation via SceneRouter; unlocks via SaveSystem.
/// Phase 5 replaces the one-death-fails rule with the HP/checkpoint system.
/// </summary>
public class GameState : MonoBehaviour
{
    private PlayerCharacterOne player1;
    private PlayerCharacterTwo player2;

    public int dragonCount;

    private bool gameOver = false;
    private bool levelCompleteProcessed = false;
    private bool gameOverSequenceStarted = false;

    public Image gameOverOverlay;
    public Image levelCompleteOverlay;
    public Image gameCompleteOverlay;
    public float fadeSpeed = 0.5f;
    private float maxAlpha = 120f / 255f;

    public TextMeshProUGUI gameOverTextTMP;
    public TextMeshProUGUI levelCompleteTextTMP;
    public TextMeshProUGUI gameCompleteTextTMP;
    private Color overlayColor;
    private Color textColor;

    void Start()
    {
        player1 = GameObject.Find("Player1")?.GetComponent<PlayerCharacterOne>();
        player2 = GameObject.Find("Player2")?.GetComponent<PlayerCharacterTwo>();

        if (player1 != null)
            player1.onDeath += OnPlayerDeath;
        if (player2 != null)
            player2.onDeath += OnPlayerDeath;

        dragonCount = 0;
        gameOver = false;
        CheckpointManager.ResetForLevel();

        if (gameOverOverlay != null)
        {
            Color startColor = gameOverOverlay.color;
            startColor.a = 0f;
            gameOverOverlay.color = startColor;
            gameOverOverlay.gameObject.SetActive(false);
        }

        if (gameOverTextTMP != null)
        {
            Color textStartColor = gameOverTextTMP.color;
            textStartColor.a = 0f;
            gameOverTextTMP.color = textStartColor;
        }
    }

    void OnDestroy()
    {
        if (player1 != null)
            player1.onDeath -= OnPlayerDeath;
        if (player2 != null)
            player2.onDeath -= OnPlayerDeath;
    }

    void Update()
    {
        if (dragonCount >= 2 && !levelCompleteProcessed && !gameOver)
        {
            levelCompleteProcessed = true;
            OnLevelComplete();
        }
    }

    private void OnPlayerDeath()
    {
        // Phase 5 death rule: a single death respawns at a checkpoint; the
        // level fails only when both in-scene characters are dead at once.
        // (Exit-absorbed characters are inactive, not dead — never counted.)
        bool p1Dead = player1 != null && player1.gameObject.activeSelf && player1.IsDead();
        bool p2Dead = player2 != null && player2.gameObject.activeSelf && player2.IsDead();

        if (p1Dead && p2Dead)
            TriggerGameOver();
    }

    public void TriggerGameOver()
    {
        if (levelCompleteProcessed)
            return;

        gameOver = true;
        if (!gameOverSequenceStarted)
        {
            gameOverSequenceStarted = true;
            StartCoroutine(GameOverWaitAndReload());
        }
    }

    public void HandleLavaCollision(GameObject player)
    {
        if (player.CompareTag("Player") || player.CompareTag("PlayerTwo"))
            TriggerGameOver();
    }

    private void OnLevelComplete()
    {
        int level = SceneRouter.CurrentLevelNumber();
        if (level > 0)
            SaveSystem.OnLevelCompleted(level);
        CrystalTracker.RecordStars(level);
        AudioManager.PlaySfx("level_complete");
        StartCoroutine(LevelCompleteWaitAndLoad());
    }

    private IEnumerator GameOverWaitAndReload()
    {
        while ((gameOverOverlay != null && gameOverOverlay.color.a < maxAlpha) ||
               (gameOverTextTMP != null && gameOverTextTMP.color.a < 1.0f))
        {
            DisplayGameOverScreen();
            yield return null;
        }

        yield return new WaitForSeconds(2.0f);
        // B3: failing a level restarts it instead of dumping to level select.
        SceneRouter.ReloadCurrentLevel();
    }

    private IEnumerator LevelCompleteWaitAndLoad()
    {
        while ((levelCompleteOverlay != null && levelCompleteOverlay.color.a < maxAlpha) ||
               (levelCompleteTextTMP != null && levelCompleteTextTMP.color.a < 1.0f))
        {
            DisplayLevelCompleteScreen();
            yield return null;
        }

        yield return new WaitForSeconds(2.0f);
        SceneRouter.LoadLevelSelect();
    }

    private void DisplayGameOverScreen()
    {
        if (gameOverOverlay != null)
        {
            if (!gameOverOverlay.gameObject.activeSelf)
                gameOverOverlay.gameObject.SetActive(true);

            overlayColor = gameOverOverlay.color;
            overlayColor.a = Mathf.Clamp(overlayColor.a + fadeSpeed * Time.deltaTime, 0, maxAlpha);
            gameOverOverlay.color = overlayColor;
        }

        if (gameOverTextTMP != null)
        {
            textColor = gameOverTextTMP.color;
            textColor.a = Mathf.Clamp(textColor.a + 2 * fadeSpeed * Time.deltaTime, 0, 1);
            gameOverTextTMP.color = textColor;
        }
    }

    private void DisplayLevelCompleteScreen()
    {
        if (levelCompleteOverlay != null)
        {
            if (!levelCompleteOverlay.gameObject.activeSelf)
                levelCompleteOverlay.gameObject.SetActive(true);

            overlayColor = levelCompleteOverlay.color;
            overlayColor.a = Mathf.Clamp(overlayColor.a + fadeSpeed * Time.deltaTime, 0, maxAlpha);
            levelCompleteOverlay.color = overlayColor;
        }

        if (levelCompleteTextTMP != null)
        {
            textColor = levelCompleteTextTMP.color;
            textColor.a = Mathf.Clamp(textColor.a + 2 * fadeSpeed * Time.deltaTime, 0, 1);
            levelCompleteTextTMP.color = textColor;
        }
    }

    public void BackToLevelMenu() { SceneRouter.LoadLevelSelect(); }
    public void BackToMainMenu() { SceneRouter.LoadMainMenu(); }
    public void RestartButton() { SceneRouter.ReloadCurrentLevel(); }
}
