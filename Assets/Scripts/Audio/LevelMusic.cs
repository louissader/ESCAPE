using UnityEngine;

/// <summary>
/// Per-scene music trigger: zone track by level number (1-4 grotto,
/// 5-8 depths, menus = menu theme). Added to scenes by editor automation.
/// </summary>
public class LevelMusic : MonoBehaviour
{
    void Start()
    {
        AudioManager.PlayMusicForScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            SceneRouter.CurrentLevelNumber());
    }
}
