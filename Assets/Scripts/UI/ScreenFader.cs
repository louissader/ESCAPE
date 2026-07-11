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
    }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                       UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        float t = FadeSeconds;
        blackout.color = new Color(0f, 0f, 0f, 1f);
        while (t > 0f)
        {
            t -= Time.unscaledDeltaTime;
            blackout.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / FadeSeconds));
            yield return null;
        }
    }
}
