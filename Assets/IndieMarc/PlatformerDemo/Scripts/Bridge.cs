using System.Collections;
using UnityEngine;

namespace IndieMarc.Platformer
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class Bridge : MonoBehaviour
    {
        // Extend animation frames (assigned by PrefabVisualOverhaul). When
        // the lever activates the bridge, it builds plank-by-plank instead of
        // popping in; the collider enables only once fully formed.
        public Sprite[] extendFrames;
        public float extendFps = 18f;

        private SpriteRenderer spriteRenderer;
        private BoxCollider2D boxCollider;
        private Rigidbody2D rigidBody;
        private Coroutine anim;

        void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            boxCollider = GetComponent<BoxCollider2D>();
            rigidBody = GetComponent<Rigidbody2D>();

            rigidBody.bodyType = RigidbodyType2D.Static;
            SetActive(false);
        }

        public void SetActive(bool active)
        {
            if (anim != null)
                StopCoroutine(anim);

            if (active && extendFrames != null && extendFrames.Length > 1 && isActiveAndEnabled)
            {
                anim = StartCoroutine(Extend());
            }
            else
            {
                spriteRenderer.enabled = active;
                boxCollider.enabled = active;
                rigidBody.simulated = active;
                if (active && extendFrames != null && extendFrames.Length > 0)
                    spriteRenderer.sprite = extendFrames[extendFrames.Length - 1];
            }
        }

        IEnumerator Extend()
        {
            spriteRenderer.enabled = true;
            rigidBody.simulated = true;
            boxCollider.enabled = false;   // no footing until fully built

            float frameTime = 1f / extendFps;
            for (int i = 0; i < extendFrames.Length; i++)
            {
                spriteRenderer.sprite = extendFrames[i];
                yield return new WaitForSeconds(frameTime);
            }

            boxCollider.enabled = true;
            anim = null;
        }
    }
}
