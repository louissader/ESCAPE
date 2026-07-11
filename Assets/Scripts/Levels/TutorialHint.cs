using UnityEngine;
using TMPro;

/// <summary>
/// One-time zone-triggered hint bubble for L1 (move/jump → swap → both must
/// reach a portal). Skipped forever once SaveSystem.TutorialSeen; dismissed
/// by any tap/key. Pauses nothing — reading is optional.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class TutorialHint : MonoBehaviour
{
    [TextArea] public string message = "";
    public bool marksTutorialSeen = false;   // set on the LAST hint of the L1 onboarding chain
    public bool alwaysShow = false;          // contextual per-mechanic hints ignore the seen-gate

    GameObject bubble;
    bool shown;

    void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        // First-launch onboarding hints vanish once seen; contextual hints
        // (levers, lava, platform spots, ...) always show on level entry.
        if (SaveSystem.TutorialSeen && !alwaysShow)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (shown || (!other.CompareTag("Player") && !other.CompareTag("PlayerTwo")))
            return;
        shown = true;
        ShowBubble();
    }

    void Update()
    {
        if (bubble == null || !bubble.activeSelf)
            return;
        if (Input.anyKeyDown || Input.touchCount > 0)
            Dismiss();
    }

    void ShowBubble()
    {
        bubble = new GameObject("HintBubble");
        bubble.transform.SetParent(transform, false);
        bubble.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        var text = bubble.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = 3;
        text.alignment = TextAlignmentOptions.Center;
        text.sortingOrder = 50;
        var rect = text.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(8f, 3f);
    }

    void Dismiss()
    {
        if (marksTutorialSeen)
            SaveSystem.TutorialSeen = true;
        Destroy(gameObject);
    }
}
