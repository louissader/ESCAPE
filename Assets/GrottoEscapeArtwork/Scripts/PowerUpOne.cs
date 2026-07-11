using UnityEngine;
using IndieMarc.Platformer;

public class PowerUpOne : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        PlayerCharacterOne player = collision.GetComponent<PlayerCharacterOne>();
        if (player != null)
        {
            player.EnableShooting();
            AudioManager.PlaySfx("powerup");
            Destroy(gameObject);
        }
    }
}
