using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// First-launch story beats (Playtest 5): full-screen plates with a
/// typewriter caption, crossfaded through a blackout CanvasGroup. A tap /
/// click / Space / Enter completes the caption, then advances; beats also
/// auto-advance. Skip is always visible and jumps straight to the end.
/// Everything runs on unscaled time via plain coroutines on this component
/// (no animator; coroutines die with the scene, so nothing leaks on unload).
///
/// End action: sets SaveSystem.IntroSeen and loads Level 1 — unless
/// ReplayMode, which returns to the main menu and touches no progress.
///
/// EDITOR PASS builds the "Intro" scene (SceneRouter.IntroScene): a
/// fullscreen plateImage (this script forces preserveAspect so plates
/// letterbox at any ratio), the caption TMP, a blackout Image whose
/// CanvasGroup is `fader` (alpha 1 = black, drawn above the plate), and a
/// skip Button assigned to `skipButton` (Awake hooks its onClick). plates =
/// cutscene_entrance / cutscene_collapse / cutscene_stars / cutscene_resolve
/// plate_0 sprites, in that order; captions default below.
/// </summary>
public class IntroCutscene : MonoBehaviour
{
    const float AutoAdvanceSeconds = 4.5f;
    const float CrossfadeSeconds = 0.4f;
    const float CharsPerSecond = 30f;

    public Image plateImage;
    public TextMeshProUGUI caption;
    public CanvasGroup fader;
    public Sprite[] plates;
    public string[] captions =
    {
        "Two friends chased a legend: a cave full of fallen stars.",
        "The mountain had other plans.",
        "The stars scattered into the deep... every last one.",
        "The only way out is down. Together."
    };
    public Button skipButton;

    /// <summary>Set true before SceneRouter.LoadIntro() to rewatch the intro:
    /// the end returns to the main menu and no progress is written.</summary>
    public static bool ReplayMode;

    bool finished;

    void Awake()
    {
        if (plateImage != null)
        {
            plateImage.preserveAspect = true;   // letterbox at any aspect ratio
            plateImage.raycastTarget = false;   // taps must not read as "over UI"
        }
        if (caption != null)
            caption.raycastTarget = false;
        if (fader != null)
        {
            fader.alpha = 1f;                   // open on black; first beat fades in
            fader.blocksRaycasts = false;
            fader.interactable = false;
        }
        if (skipButton != null)
            skipButton.onClick.AddListener(Skip);
    }

    void Start()
    {
        StartCoroutine(RunBeats());
    }

    IEnumerator RunBeats()
    {
        int beatCount = Mathf.Max(plates != null ? plates.Length : 0,
                                  captions != null ? captions.Length : 0);

        for (int i = 0; i < beatCount && !finished; i++)
        {
            if (plateImage != null && plates != null && i < plates.Length && plates[i] != null)
                plateImage.sprite = plates[i];
            if (caption != null)
                caption.text = string.Empty;

            yield return Fade(1f, 0f);

            string line = captions != null && i < captions.Length && captions[i] != null
                ? captions[i] : string.Empty;
            bool captionComplete = line.Length == 0;
            float revealed = 0f;
            float beatTimer = 0f;
            bool advance = false;
            while (!advance && !finished)
            {
                beatTimer += Time.unscaledDeltaTime;

                if (!captionComplete)
                {
                    revealed += CharsPerSecond * Time.unscaledDeltaTime;
                    int count = Mathf.Min(line.Length, Mathf.FloorToInt(revealed));
                    if (caption != null)
                        caption.text = line.Substring(0, count);
                    captionComplete = count >= line.Length;
                }

                if (AdvancePressed())
                {
                    if (!captionComplete)
                    {
                        // First tap completes the line, the next one advances.
                        captionComplete = true;
                        if (caption != null)
                            caption.text = line;
                    }
                    else
                    {
                        advance = true;
                    }
                }
                if (beatTimer >= AutoAdvanceSeconds)
                    advance = true;

                yield return null;
            }

            if (finished)
                yield break;

            if (i < beatCount - 1)
                yield return Fade(0f, 1f);
        }

        Finish();
    }

    IEnumerator Fade(float from, float to)
    {
        if (fader == null)
            yield break;
        float t = 0f;
        fader.alpha = from;
        while (t < CrossfadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            fader.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / CrossfadeSeconds));
            yield return null;
        }
        fader.alpha = to;
    }

    bool AdvancePressed()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter))
            return true;
        // Pointer input must not double-fire with the skip button — ignore
        // presses over raycastable UI (only the button is, see Awake).
        if (Input.GetMouseButtonDown(0) && !PointerOverUI())
            return true;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began && !TouchOverUI(touch.fingerId))
                return true;
        }
        return false;
    }

    static bool PointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    static bool TouchOverUI(int fingerId)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    /// <summary>Skip button handler (also hooked in Awake).</summary>
    public void Skip()
    {
        AudioManager.PlaySfx("ui_tap");
        Finish();
    }

    void Finish()
    {
        if (finished)
            return;
        finished = true;
        StopAllCoroutines();

        if (ReplayMode)
        {
            // Rewatch from the menu: no progress writes, straight back out.
            ReplayMode = false;
            SceneRouter.LoadMainMenu();
            return;
        }

        SaveSystem.IntroSeen = true;
        SceneRouter.LoadLevel(1);
    }
}
