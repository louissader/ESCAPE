using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the hero you'd switch INTO on the swap button ("SWITCH TO" +
/// portrait). Sprites are baked in by TouchControlsBuilder (the generated
/// hero idle frames — runtime code cannot reach them via AssetDatabase).
/// Refreshes on every swap and whenever the button re-activates (the swap
/// button is toggled off while a hero is dead).
/// </summary>
public class SwapButtonFace : MonoBehaviour
{
    public Image portrait;
    public Sprite emberSprite; // player 1
    public Sprite wispSprite;  // player 2

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
        if (portrait == null)
            return;
        bool emberActive = VirtualInputRouter.ActivePlayerId == 1;
        portrait.sprite = emberActive ? wispSprite : emberSprite;
        portrait.enabled = portrait.sprite != null;
    }
}
