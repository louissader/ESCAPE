using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    void Awake()
    {
        SaveSystem.Migrate();
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
    }
}
