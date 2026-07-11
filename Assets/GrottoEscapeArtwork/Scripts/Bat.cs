using System.Collections;
using UnityEngine;

namespace IndieMarc.Platformer
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class Bat : MonoBehaviour, IDamageableEnemy
    {
        [Header("Movement Settings")]
        public float moveRange = 3f;
        public float moveSpeed = 1f;

        [Header("Shooting Settings")]
        public bool canShoot = true;   // false = contact-only variant (L1 teaches bats before the player can shoot back)
        public GameObject fireballPrefab;
        public float fireballSpeed = 6f;
        public float initialShootDelay = 1.5f;
        public float shootInterval = 2.5f;   // B9: was 0.5s x2 players = projectile flood
        public float shootRange = 12f;       // world units
        public int maxLiveFireballs = 3;

        [Header("Contact")]
        public float contactDamage = 30f;

        private Vector3 startPosition;
        private Rigidbody2D rigidBody;
        private Vector3 currentTargetPosition;
        private int liveFireballs;

        public System.Action onTelegraph;  // Phase 4: wind-up flash before a shot
        public System.Action onDeath;      // Phase 4: dissolve/poof FX

        void Start()
        {
            startPosition = transform.position;

            rigidBody = GetComponent<Rigidbody2D>();
            rigidBody.bodyType = RigidbodyType2D.Kinematic;
            rigidBody.gravityScale = 0;
            GetComponent<BoxCollider2D>().isTrigger = false;

            StartCoroutine(FloatRandomly());
            if (canShoot)
                StartCoroutine(ShootLoop());
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

        /// <summary>
        /// Nearest LIVING, NON-PARKED player in range. Parked swap-mode
        /// characters are never targeted — an unattended character must not
        /// be sniped off-screen.
        /// </summary>
        Transform AcquireTarget()
        {
            Transform best = null;
            float bestDist = shootRange;

            PlayerCharacterOne p1 = PlayerCharacterOne.Get(1);
            if (p1 != null && !p1.IsDead() && p1.gameObject.activeSelf && !SwapModeController.IsParked(1))
            {
                float d = Vector3.Distance(transform.position, p1.transform.position);
                if (d < bestDist) { bestDist = d; best = p1.transform; }
            }

            PlayerCharacterTwo p2 = PlayerCharacterTwo.Get(2);
            if (p2 != null && !p2.IsDead() && p2.gameObject.activeSelf && !SwapModeController.IsParked(2))
            {
                float d = Vector3.Distance(transform.position, p2.transform.position);
                if (d < bestDist) { bestDist = d; best = p2.transform; }
            }

            return best;
        }

        IEnumerator ShootLoop()
        {
            yield return new WaitForSeconds(initialShootDelay);

            while (true)
            {
                Transform target = AcquireTarget();
                if (target != null && liveFireballs < maxLiveFireballs && fireballPrefab != null)
                {
                    onTelegraph?.Invoke();
                    yield return new WaitForSeconds(0.3f);

                    // Re-acquire after the wind-up so the shot tracks honestly.
                    target = AcquireTarget();
                    if (target != null)
                        LaunchFireballAt(target.position);
                }
                yield return new WaitForSeconds(shootInterval);
            }
        }

        void LaunchFireballAt(Vector3 targetPosition)
        {
            Vector2 direction = (targetPosition - transform.position).normalized;
            GameObject fireballObj = Instantiate(fireballPrefab, transform.position, Quaternion.identity);
            fireballObj.tag = "Fireball";

            Fireball fireball = fireballObj.GetComponent<Fireball>();
            if (fireball != null)
                fireball.owner = Fireball.Owner.Enemy;

            Rigidbody2D fireballRb = fireballObj.GetComponent<Rigidbody2D>();
            if (fireballRb != null)
                fireballRb.velocity = direction * fireballSpeed;

            liveFireballs++;
            StartCoroutine(ReleaseSlotWhenGone(fireballObj));
        }

        IEnumerator ReleaseSlotWhenGone(GameObject fireball)
        {
            while (fireball != null)
                yield return new WaitForSeconds(0.5f);
            liveFireballs = Mathf.Max(0, liveFireballs - 1);
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
