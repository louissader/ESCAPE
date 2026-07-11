using UnityEngine;
using IndieMarc.Platformer;

/// <summary>Heals 30 HP to whichever character touches it.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class HeartPickup : MonoBehaviour
{
    public float healAmount = 30f;

    void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerCharacterOne p1 = other.GetComponent<PlayerCharacterOne>();
        PlayerCharacterTwo p2 = other.GetComponent<PlayerCharacterTwo>();
        if (p1 == null && p2 == null)
            return;

        p1?.HealDamage(healAmount);
        p2?.HealDamage(healAmount);
        AudioManager.PlaySfx("powerup");
        Destroy(gameObject);
    }
}
