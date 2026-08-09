using UnityEngine;
using IndieMarc.Platformer;

/// <summary>
/// Bridges player events to haptics, mirroring PlayerSfx. Added to both
/// player prefabs by editor automation; works for either character class.
/// </summary>
public class PlayerHaptics : MonoBehaviour
{
    void Start()
    {
        if (TryGetComponent(out PlayerCharacterOne p1))
        {
            p1.onLand += Haptics.ImpactLight;
            p1.onHit += Haptics.ImpactMedium;
            p1.onDeath += Haptics.ImpactHeavy;
        }
        if (TryGetComponent(out PlayerCharacterTwo p2))
        {
            p2.onLand += Haptics.ImpactLight;
            p2.onHit += Haptics.ImpactMedium;
            p2.onDeath += Haptics.ImpactHeavy;
        }
    }
}
