using System.Collections;
using UnityEngine;

/// <summary>
/// Persistent audio hub: cross-fading music per zone + a pooled one-shot SFX
/// bank with pitch jitter. Volumes come from SaveSystem (settings sliders
/// write there); no AudioMixer asset — mixers can't be created from editor
/// code, and two independent gain stages are all this game needs.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music (wired by AudioSetup automation)")]
    public AudioClip menuMusic;
    public AudioClip grottoMusic;   // levels 1-4
    public AudioClip depthsMusic;   // levels 5-8

    [Header("SFX")]
    public SfxLibrary sfxLibrary;

    const int SfxVoices = 8;
    const float CrossfadeSeconds = 1.25f;

    AudioSource musicA;
    AudioSource musicB;
    bool usingA = true;
    AudioSource[] sfxPool;
    int nextVoice;
    float duckFactor = 1f;
    Coroutine fadeRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicA = gameObject.AddComponent<AudioSource>();
        musicB = gameObject.AddComponent<AudioSource>();
        foreach (AudioSource src in new[] { musicA, musicB })
        {
            src.loop = true;
            src.playOnAwake = false;
        }

        sfxPool = new AudioSource[SfxVoices];
        for (int i = 0; i < SfxVoices; i++)
        {
            sfxPool[i] = gameObject.AddComponent<AudioSource>();
            sfxPool[i].playOnAwake = false;
        }
    }

    void Start()
    {
        PlayMusic(menuMusic);
    }

    public static void PlayMusicForScene(string sceneName, int levelNumber)
    {
        if (Instance == null)
            return;
        AudioClip clip;
        if (levelNumber >= 5)
            clip = Instance.depthsMusic;
        else if (levelNumber >= 1)
            clip = Instance.grottoMusic;
        else
            clip = Instance.menuMusic;
        Instance.PlayMusic(clip);
    }

    public void PlayMusic(AudioClip clip)
    {
        AudioSource current = usingA ? musicA : musicB;
        if (clip == null || current.clip == clip && current.isPlaying)
            return;

        AudioSource next = usingA ? musicB : musicA;
        usingA = !usingA;
        next.clip = clip;
        next.volume = 0f;
        next.Play();

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Crossfade(current, next));
    }

    IEnumerator Crossfade(AudioSource from, AudioSource to)
    {
        float t = 0f;
        while (t < CrossfadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / CrossfadeSeconds);
            float target = MusicGain();
            to.volume = k * target;
            from.volume = (1f - k) * target;
            yield return null;
        }
        from.Stop();
        fadeRoutine = null;
    }

    /// <summary>0.5 duck while paused; 1 in gameplay.</summary>
    public void SetDuck(float factor)
    {
        duckFactor = Mathf.Clamp01(factor);
        RefreshMusicVolume();
    }

    public void RefreshMusicVolume()
    {
        float gain = MusicGain();
        if (fadeRoutine == null)
        {
            (usingA ? musicA : musicB).volume = gain;
            (usingA ? musicB : musicA).volume = 0f;
        }
    }

    float MusicGain()
    {
        return SaveSystem.MusicVolume * duckFactor;
    }

    public static void PlaySfx(string id, float pitchJitter = 0.05f)
    {
        if (Instance == null || Instance.sfxLibrary == null)
            return;
        AudioClip clip = Instance.sfxLibrary.Get(id);
        if (clip == null)
            return;

        AudioSource voice = Instance.sfxPool[Instance.nextVoice];
        Instance.nextVoice = (Instance.nextVoice + 1) % SfxVoices;
        voice.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        voice.PlayOneShot(clip, SaveSystem.SfxVolume);
    }
}
