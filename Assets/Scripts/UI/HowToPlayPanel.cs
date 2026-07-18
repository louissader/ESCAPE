using System.Collections;
using UnityEngine;

/// <summary>
/// Static "how to play" reference card (content and layout are built by the
/// editor pass). Open()/Close() toggle the panel root and fade its
/// CanvasGroup on UNSCALED time — the pause menu opens this at timeScale 0.
/// Escape also closes it, and PauseMenu stands down while it is open (see
/// IsOpen / ClosedThisFrame) so the shared Escape key doesn't simultaneously
/// toggle the pause overlay.
/// </summary>
public class HowToPlayPanel : MonoBehaviour
{
    const float FadeSeconds = 0.15f;

    [SerializeField] private GameObject root;         // defaults to this GameObject
    [SerializeField] private CanvasGroup canvasGroup; // optional fade target on root

    public static bool IsOpen { get; private set; }

    static int lastCloseFrame = -1;

    /// <summary>True on the exact frame Escape closed the panel — PauseMenu
    /// must ignore that press (Update order between the two is arbitrary).</summary>
    public static bool ClosedThisFrame
    {
        get { return lastCloseFrame == Time.frameCount; }
    }

    void Awake()
    {
        EnsureWired();
    }

    // Awake never runs on a panel that starts inactive in the scene — Open()
    // can be its first entry point, so wiring must be lazy.
    void EnsureWired()
    {
        if (root == null)
            root = gameObject;
        if (canvasGroup == null)
            canvasGroup = root.GetComponent<CanvasGroup>();
    }

    void OnDestroy()
    {
        // Scene change while open must not leave the static flag stuck.
        if (IsOpen)
            IsOpen = false;
    }

    void Update()
    {
        // Android back button maps to Escape in the legacy input manager.
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    public void Open()
    {
        EnsureWired();
        IsOpen = true;
        root.SetActive(true);
        AudioManager.PlaySfx("ui_tap");
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            StopAllCoroutines();
            StartCoroutine(FadeIn());
        }
    }

    public void Close()
    {
        EnsureWired();
        if (!IsOpen && !root.activeSelf)
            return;
        IsOpen = false;
        lastCloseFrame = Time.frameCount;
        AudioManager.PlaySfx("ui_tap");
        StopAllCoroutines();
        if (canvasGroup != null)
            canvasGroup.alpha = 1f; // ready for a fade-less external activation
        root.SetActive(false);
    }

    IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < FadeSeconds)
        {
            t += Time.unscaledDeltaTime; // pause menu opens this at timeScale 0
            canvasGroup.alpha = Mathf.Clamp01(t / FadeSeconds);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }
}
