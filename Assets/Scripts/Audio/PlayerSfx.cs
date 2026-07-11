using UnityEngine;
using IndieMarc.Platformer;

/// <summary>
/// Bridges player events to SFX. Added to both player prefabs by editor
/// automation; works for either character class.
/// </summary>
public class PlayerSfx : MonoBehaviour
{
    void Start()
    {
        if (TryGetComponent(out PlayerCharacterOne p1))
        {
            p1.onJump += () => AudioManager.PlaySfx("jump");
            p1.onLand += () => AudioManager.PlaySfx("land");
            p1.onHit += () => AudioManager.PlaySfx("hurt");
            p1.onDeath += () => AudioManager.PlaySfx("death");
        }
        if (TryGetComponent(out PlayerCharacterTwo p2))
        {
            p2.onJump += () => AudioManager.PlaySfx("jump");
            p2.onLand += () => AudioManager.PlaySfx("land");
            p2.onHit += () => AudioManager.PlaySfx("hurt");
            p2.onDeath += () => AudioManager.PlaySfx("death");
        }
    }
}
