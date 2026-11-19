using TMPro;
using UnityEngine;

/// <summary>
/// "TAP TO SWITCH" floating above the INACTIVE hero on the early levels
/// (playtest 9.3: tap-the-hero switching needs a visible teacher). Spawned
/// by SwapModeController on levels 1-4, Swap mode only. Follows whichever
/// hero is currently parked, bobs gently, and hides itself whenever
/// swapping is impossible (co-op, a dead hero, a modal). Permanent for the
/// level by design — Louis wants the reminder, not a one-shot.
/// </summary>
public class HeroSwitchFloaty : MonoBehaviour
{
    const float BobAmplitude = 0.12f;
    const float BobSpeed = 2.4f;
    const float HeightAboveHero = 2.1f;

    TextMeshPro text;

    public static HeroSwitchFloaty Spawn()
    {
        var go = new GameObject("HeroSwitchFloaty");
        var floaty = go.AddComponent<HeroSwitchFloaty>();
        return floaty;
    }

    void Awake()
    {
        text = gameObject.AddComponent<TextMeshPro>();
        text.text = "TAP TO SWITCH";
        text.fontSize = 3.6f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.85f, 0.3f);
        text.outlineWidth = 0.3f;
        text.outlineColor = new Color32(10, 10, 26, 240);
        text.sortingOrder = 48; // under hint bubbles (49/50), over the world
        var rect = text.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(6f, 1.4f);
    }

    void LateUpdate()
    {
        var controller = SwapModeController.Instance;
        bool show = controller != null && controller.CanSwap;
        Transform target = null;
        if (show)
        {
            int inactiveId = VirtualInputRouter.ActivePlayerId == 1 ? 2 : 1;
            var p1 = IndieMarc.Platformer.PlayerCharacterOne.Get(1);
            var p2 = IndieMarc.Platformer.PlayerCharacterTwo.Get(2);
            target = inactiveId == 1
                ? (p1 != null ? p1.transform : null)
                : (p2 != null ? p2.transform : null);
            show = target != null;
        }

        if (text.enabled != show)
            text.enabled = show;
        if (!show)
            return;

        float bob = Mathf.Sin(Time.unscaledTime * BobSpeed) * BobAmplitude;
        transform.position = target.position + new Vector3(0f, HeightAboveHero + bob, 0f);
    }
}
