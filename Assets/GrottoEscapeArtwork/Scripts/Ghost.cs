using System.Collections;
using UnityEngine;

namespace IndieMarc.Platformer
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class Ghost : MonoBehaviour, IDamageableEnemy
    {
        [Header("Movement Settings")]
        public float moveRange = 3f;
        public float moveSpeed = 1f;

        [Header("Contact")]
        public float contactDamage = 30f;

        private Vector3 startPosition;
        private Rigidbody2D rigidBody;
        private Vector3 currentTargetPosition;

        public System.Action onDeath;  // Phase 4: dissolve/poof FX

        void Awake()
        {
            rigidBody = GetComponent<Rigidbody2D>();
        }

        void Start()
        {
            startPosition = transform.position;

            rigidBody.bodyType = RigidbodyType2D.Kinematic;
            rigidBody.gravityScale = 0;
            GetComponent<BoxCollider2D>().isTrigger = false;

            StartCoroutine(FloatRandomly());
        }

        void FixedUpdate()
        {
            if (currentTargetPosition != Vector3.zero)
            {
                Vector3 newPosition = Vector3.MoveTowards(
                    transform.position, currentTargetPosition, moveSpeed * Time.fixedDeltaTime);
                rigidBody.MovePosition(newPosition);

                if (Vector3.Distance(transform.position, currentTargetPosition) < 0.1f)
                    currentTargetPosition = Vector3.zero;
            }
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            DealContactDamage(collision);
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            // Player i-frames (1s) throttle this to one hit per second.
            DealContactDamage(collision);
        }

        void DealContactDamage(Collision2D collision)
        {
            if (collision.gameObject.CompareTag("Player"))
                collision.gameObject.GetComponent<PlayerCharacterOne>()?.TakeDamage(contactDamage);
            else if (collision.gameObject.CompareTag("PlayerTwo"))
                collision.gameObject.GetComponent<PlayerCharacterTwo>()?.TakeDamage(contactDamage);
        }

        public void Die()
        {
            AudioManager.PlaySfx("enemy_poof");
            onDeath?.Invoke();
            Destroy(gameObject);
        }

        /// <summary>Kept for legacy scene wiring; routes to Die().</summary>
        public void DestroySelf()
        {
            Die();
        }

        IEnumerator FloatRandomly()
        {
            while (true)
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-moveRange, moveRange),
                    Random.Range(-moveRange, moveRange),
                    0f
                );
                currentTargetPosition = startPosition + randomOffset;
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
