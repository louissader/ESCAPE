using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IndieMarc.Platformer;

public class PowerUpTwo : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerCharacterTwo player = other.GetComponent<PlayerCharacterTwo>();
        if (player != null)
        {
            player.CollectPowerUp();
            AudioManager.PlaySfx("powerup");
            Destroy(gameObject);

            // Debugging
            Debug.Log("[PowerUpTwo] Power-up collected by Player 2.");
        }
    }
}
