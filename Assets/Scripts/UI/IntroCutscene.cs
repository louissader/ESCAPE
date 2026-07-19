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
/// Playtest 6 adds an unscaled Ken-Burns drift on the plate and per-beat
/// overlay roots (builder-wired star twinkles / drifting dust motes).
/// Everything runs on unscaled time via plain coroutines on this component
/// (no animator; coroutines die with the scene, so nothing leaks on unload).
///
/// End action: sets SaveSystem.IntroSeen, then opens the post-cutscene
/// controls explainer (postCutscenePanel, when the editor pass assigned
/// one — its LET'S GO button proceeds to Level 1) or loads Level 1
/// directly in an unmigrated scene — unless ReplayMode, which returns to
/// the main menu and touches no progress (and never shows the explainer).
///
/// EDITOR PASS builds the "Intro" scene (SceneRouter.IntroScene): a
/// fullscreen plateImage (aspect-FILLed by the builder's
/// AspectRatioFitter; this script still forces preserveAspect, a no-op
/// while the rect ratio matches the art), the caption TMP, a blackout Image whose
/// CanvasGroup is `fader` (alpha 1 = black, drawn above the plate), and a
/// skip Button assigned to `skipButton` (Awake hooks its onClick). plates =
/// cutscene_entrance / cutscene_collapse / cutscene_stars / cutscene_resolve
/// plate_0 sprites, in that order; captions default below. It also builds
/// the hidden controls explainer (postCutscenePanel + letsGoButton).
/// </summary>
public class IntroCutscene : MonoBehaviour
{
    const float AutoAdvanceSeconds = 4.5f;
    const float CrossfadeSeconds = 0.4f;
    const float CharsPerSecond = 30f;

    // Ken-Burns drift (Playtest 6): ~4% zoom per beat, alternating in/out,
    // with a pan that grows with the zoom margin so the plate never
    // uncovers an edge (pan magnitude < (zoom-1)/2 * rect size, always).
    const float KenBurnsZoom = 0.04f;
    const float KenBurnsPanX = 18f;    // canvas units at full zoom
    const float KenBurnsPanY = 7f;
    const float DustSway = 22f;        // collapse-beat mote drift, canvas units
    const float DustRise = 34f;

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

    // First-run controls explainer (built by IntroCutsceneBuilder; optional
    // — when null the old straight-to-Level-1 behavior applies).
    public HowToPlayPanel postCutscenePanel;
    public Button letsGoButton;

    // Per-beat overlay roots (index == beat), children of the plate so they
    // ride the Ken-Burns drift. IntroCutsceneBuilder wires them (twinkling
    // stars on the entrance / star-scatter beats via UIPulse, dust motes on
    // the collapse beat); every entry is optional — null-safe throughout.
    public RectTransform[] beatOverlayRoots;
    // Collapse-beat dust motes this script drifts (slow two-axis sinusoids,
    // allocation-free). Wired by the builder; optional.
    public RectTransform[] dustMotes;

    /// <summary>Set true before SceneRouter.LoadIntro() to rewatch the intro:
    /// the end returns to the main menu and no progress is written.</summary>
    public static bool ReplayMode;

    bool finished;
    bool explainerOpen;
    bool proceeding;

    RectTransform plateRect;
    Vector2 plateBasePos;
    int beatIndex = -1;
    float beatStart;
    Vector2[] dustBase;

    void Awake()
    {
        if (plateImage != null)
        {
            plateImage.preserveAspect = true;   // letterbox at any aspect ratio
            plateImage.raycastTarget = false;   // taps must not read as "over UI"
            plateRect = (RectTransform)plateImage.transform;
            plateBasePos = plateRect.anchoredPosition;
        }
        if (dustMotes != null && dustMotes.Length > 0)
        {
            dustBase = new Vector2[dustMotes.Length];
            for (int i = 0; i < dustMotes.Length; i++)
                if (dustMotes[i] != null)
                    dustBase[i] = dustMotes[i].anchoredPosition;
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
        if (letsGoButton != null)
            letsGoButton.onClick.AddListener(OnLetsGo);
    }

    void Start()
    {
        StartCoroutine(RunBeats());
    }

    void Update()
    {
        // The explainer can also close via Escape / Android back
        // (HowToPlayPanel owns that key) — a first-run player must never be
        // stranded on a dead cutscene, so any close counts as LET'S GO.
        if (explainerOpen && !proceeding &&
            (postCutscenePanel == null || !postCutscenePanel.gameObject.activeSelf))
            ProceedToLevelOne();

        UpdatePlateMotion();
        UpdateDustMotes();
    }

    /// <summary>Unscaled Ken-Burns drift on the plate: even beats zoom in,
    /// odd beats open zoomed and pull back; the pan direction flips every
    /// two beats. Pure RectTransform writes — no per-frame allocation.</summary>
    void UpdatePlateMotion()
    {
        if (plateRect == null || beatIndex < 0 || finished)
            return;
        float u = Mathf.Clamp01((Time.unscaledTime - beatStart) /
                                (AutoAdvanceSeconds + CrossfadeSeconds));
        bool zoomIn = (beatIndex & 1) == 0;
        float zoomFrac = zoomIn ? u : 1f - u;    // 0 => rest scale, 1 => full zoom
        float s = 1f + KenBurnsZoom * zoomFrac;
        plateRect.localScale = new Vector3(s, s, 1f);
        float dir = ((beatIndex >> 1) & 1) == 0 ? 1f : -1f;
        plateRect.anchoredPosition = plateBasePos +
            new Vector2(dir * KenBurnsPanX * zoomFrac, -KenBurnsPanY * zoomFrac);
    }

    /// <summary>Slow Lissajous drift for the collapse-beat dust motes.
    /// Incommensurate per-mote frequencies, so the cloud never syncs up;
    /// motes on inactive overlay roots are skipped.</summary>
    void UpdateDustMotes()
    {
        if (dustMotes == null || dustBase == null || finished)
            return;
        float t = Time.unscaledTime;
        for (int i = 0; i < dustMotes.Length; i++)
        {
            RectTransform mote = dustMotes[i];
            if (mote == null || i >= dustBase.Length || !mote.gameObject.activeInHierarchy)
                continue;
            float sway = Mathf.Sin(t * (0.31f + 0.05f * i) + i * 2.399f);
            float rise = Mathf.Sin(t * (0.17f + 0.03f * i) + i * 1.618f);
            mote.anchoredPosition = dustBase[i] +
                new Vector2(sway * DustSway, rise * DustRise);
        }
    }

    /// <summary>Per-beat setup: restarts the Ken-Burns clock and shows only
    /// this beat's overlay root (stars / dust). Null-safe when unwired.</summary>
    void BeginBeat(int beat)
    {
        beatIndex = beat;
        beatStart = Time.unscaledTime;
        if (beatOverlayRoots == null)
            return;
        for (int i = 0; i < beatOverlayRoots.Length; i++)
            if (beatOverlayRoots[i] != null)
                beatOverlayRoots[i].gameObject.SetActive(i == beat);
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
            BeginBeat(i);

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

        // Freeze the drift and drop the beat overlays — nothing may keep
        // twinkling behind the explainer / during the scene hand-off.
        if (beatOverlayRoots != null)
            for (int i = 0; i < beatOverlayRoots.Length; i++)
                if (beatOverlayRoots[i] != null)
                    beatOverlayRoots[i].gameObject.SetActive(false);

        if (ReplayMode)
        {
            // Rewatch from the menu: no progress writes, straight back out.
            ReplayMode = false;
            SceneRouter.LoadMainMenu();
            return;
        }

        SaveSystem.IntroSeen = true;

        // First run: show the controls explainer before Level 1 (the panel
        // covers the whole canvas, so hide the now-dead skip button too).
        if (postCutscenePanel != null)
        {
            if (skipButton != null)
                skipButton.gameObject.SetActive(false);
            postCutscenePanel.Open();
            explainerOpen = true;
            return;
        }

        SceneRouter.LoadLevel(1);   // unmigrated scene: old direct path
    }

    /// <summary>LET'S GO button handler (hooked in Awake).</summary>
    void OnLetsGo()
    {
        AudioManager.PlaySfx("ui_tap");
        ProceedToLevelOne();
    }

    void ProceedToLevelOne()
    {
        if (proceeding)
            return;
        proceeding = true;
        SceneRouter.LoadLevel(1);
    }
}
