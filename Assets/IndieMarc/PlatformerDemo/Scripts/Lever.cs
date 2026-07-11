using UnityEngine;

namespace IndieMarc.Platformer
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class Lever : MonoBehaviour
    {
        public Sprite leverLeft;
        public Sprite leverRight;

        // B10: serialized multi-target replaces the find-one-bridge-by-tag
        // assumption so levels can wire several lever/bridge pairs.
        [SerializeField] private Bridge[] targets;

        // Re-trigger guard: brushing the trigger twice must not instantly
        // revert the bridge.
        [SerializeField] private float toggleCooldown = 0.75f;

        private SpriteRenderer spriteRenderer;
        private LeverState state = LeverState.Left;
        private float lastToggleTime = -999f;

        void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            GetComponent<BoxCollider2D>().isTrigger = true;
            spriteRenderer.sprite = leverLeft;

            // Legacy fallback for scenes not yet migrated to serialized refs.
            if (targets == null || targets.Length == 0)
            {
                GameObject bridgeObj = GameObject.FindGameObjectWithTag("Bridge");
                if (bridgeObj != null && bridgeObj.TryGetComponent(out Bridge bridge))
                    targets = new[] { bridge };
            }
        }

        void OnTriggerEnter2D(Collider2D collider)
        {
            if (collider.CompareTag("Player") || collider.CompareTag("PlayerTwo"))
                ToggleLever();
        }

        private void ToggleLever()
        {
            if (Time.time - lastToggleTime < toggleCooldown)
                return;
            lastToggleTime = Time.time;

            state = state == LeverState.Left ? LeverState.Right : LeverState.Left;
            spriteRenderer.sprite = state == LeverState.Left ? leverLeft : leverRight;
            AudioManager.PlaySfx("lever");
            AudioManager.PlaySfx("bridge");

            bool active = state == LeverState.Right;
            if (targets == null)
                return;
            foreach (Bridge bridge in targets)
            {
                if (bridge != null)
                    bridge.SetActive(active);
            }
        }
    }
}
