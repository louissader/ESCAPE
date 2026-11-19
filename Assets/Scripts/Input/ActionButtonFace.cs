using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puts a picture of what the power actually DOES on the power button
/// (playtest 9: "B" said nothing): a fireball for Ember, the crystal
/// platform for Wisp. Sprites are baked in by TouchControlsBuilder —
/// runtime code cannot reach them via AssetDatabase.
///
/// playerId 0 follows the ACTIVE hero (swap-mode button); 1 or 2 pins the
/// icon to that hero (co-op buttons, where each player owns a B).
/// </summary>
public class ActionButtonFace : MonoBehaviour
{
    public Image icon;
    public Sprite emberPowerSprite; // fireball
    public Sprite wispPowerSprite;  // crystal platform
    [Tooltip("0 = follow the active hero; 1/2 = fixed to that hero")]
    public int playerId;

    void OnEnable()
    {
        SwapModeController.OnSwapped += OnSwapped;
        Refresh();
    }

    void OnDisable()
    {
        SwapModeController.OnSwapped -= OnSwapped;
    }

    void OnSwapped(int newActiveId)
    {
        Refresh();
    }

    void Refresh()
    {
        if (icon == null)
            return;
        int hero = playerId != 0 ? playerId : VirtualInputRouter.ActivePlayerId;
        icon.sprite = hero == 1 ? emberPowerSprite : wispPowerSprite;
        icon.enabled = icon.sprite != null;
    }
}
