using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    void Awake()
    {
        SaveSystem.Migrate();
    }
}
