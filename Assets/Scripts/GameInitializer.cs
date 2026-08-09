using UnityEngine;

/// <summary>
/// One-time boot work. Runs via RuntimeInitializeOnLoadMethod so EVERY
/// entry path gets it (play-from-scene in the editor, deep links), not
/// just Menu Scene — which is the only scene that carries the legacy
/// GameObject shim kept below for compatibility.
/// </summary>
public class GameInitializer : MonoBehaviour
{
    static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (initialized)
            return;
        initialized = true;

        SaveSystem.Migrate();
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        // The intro cutscene (~20 s) and ending screen are touch-free;
        // without this the device dims mid-scene.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    void Awake()
    {
        Bootstrap();
    }
}
