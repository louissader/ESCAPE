using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent fade-through-black on every scene transition. Self-bootstraps
/// on first scene load — no per-scene setup needed.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    const float FadeSeconds = 0.25f;

    static ScreenFader instance;
    Image blackout;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;
        GameObject go = new GameObject("ScreenFader");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ScreenFader>();
        instance.Build();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += instance.OnSceneLoaded;
    }

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        GameObject imageObj = new GameObject("Blackout", typeof(RectTransform));
        imageObj.transform.SetParent(transform, false);
        RectTransform rect = (RectTransform)imageObj.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        blackout = imageObj.AddComponent<Image>();
        blackout.color = new Color(0f, 0f, 0f, 0f);
        blackout.raycastTarget = false;
        // Starts off. FadeIn turns it on for its 0.25 s and off again — see
        // the note there for why leaving it on is not free.
        imageObj.SetActive(false);
    }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                       UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    /// <summary>
    /// Fade the black quad out, then switch it OFF. Unity does not cull a
    /// zero-alpha Graphic: leaving it enabled kept this DontDestroyOnLoad
    /// ScreenSpaceOverlay canvas (sortingOrder 999) submitting an invisible
    /// full-screen blended quad every frame of every scene for the whole
    /// session — full-screen overdraw on a battery-bound device for nothing.
    /// Anything that ever adds a fade-OUT must re-activate the object first
    /// (SetActive(true)) before animating alpha up.
    /// </summary>
    IEnumerator FadeIn()
    {
        blackout.gameObject.SetActive(true);
        float t = FadeSeconds;
        blackout.color = new Color(0f, 0f, 0f, 1f);
        while (t > 0f)
        {
            t -= Time.unscaledDeltaTime;
            blackout.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / FadeSeconds));
            yield return null;
        }
        blackout.gameObject.SetActive(false);
    }
}
