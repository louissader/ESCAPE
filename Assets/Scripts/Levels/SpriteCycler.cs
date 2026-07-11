using UnityEngine;

/// <summary>Minimal sprite-frame animator for props (star coin spin, etc.).</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteCycler : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 8f;

    SpriteRenderer sr;
    float t;
    int index;

    void Awake() { sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        if (frames == null || frames.Length < 2)
            return;
        t += Time.deltaTime;
        if (t >= 1f / fps)
        {
            t = 0f;
            index = (index + 1) % frames.Length;
            sr.sprite = frames[index];
        }
    }
}
