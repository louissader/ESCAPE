using UnityEngine;

/// <summary>Optional collectible: 3 per level drive the star rating.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class Crystal : MonoBehaviour
{
    void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void Start()
    {
        // Registration MUST happen in Start: SceneManager.sceneLoaded (which
        // resets the tracker) fires after Awake but before Start — coins
        // registered in Awake were being zeroed and never counted.
        CrystalTracker.RegisterInLevel();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") && !other.CompareTag("PlayerTwo"))
            return;
        CrystalTracker.Collect();
        AudioManager.PlaySfx("crystal");
        Haptics.ImpactLight();
        Destroy(gameObject);
    }
}

/// <summary>Static per-level crystal tally. GameState records stars on win.</summary>
public static class CrystalTracker
{
    public static int Collected { get; private set; }
    public static int Total { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) =>
        {
            Collected = 0;
            Total = 0;
        };
    }

    public static void RegisterInLevel()
    {
        Total++;
    }

    public static void Collect()
    {
        Collected = Mathf.Min(Collected + 1, Total);
    }

    public static void RecordStars(int levelNumber)
    {
        if (levelNumber > 0 && Total > 0)
            SaveSystem.RecordStars(levelNumber, Collected);
    }
}
