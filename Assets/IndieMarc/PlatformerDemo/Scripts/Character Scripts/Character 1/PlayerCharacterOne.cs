using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace IndieMarc.Platformer
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PlayerCharacterOne : MonoBehaviour
    {
        public int player_id;

        [Header("Stats")]
        public float max_hp = 100f;

        [Header("Status")]
        public bool invulnerable = false;

        [Header("Movement")]
        public float move_accel = 50f;
        public float move_deccel = 50f;
        public float move_max = 5f;

        [Header("Jump")]
        public bool can_jump = true;
        public bool double_jump = true;
        public float jump_strength = 10f;
        public LayerMask ground_layer;
        public float ground_raycast_dist = 0.1f;

        [Header("Crouch")]
        public bool can_crouch = true;
        public float crouch_coll_percent = 0.5f;

        // Removed "Fall Below Level" fields and logic

        [Header("Shooting")]
        public bool can_shoot = false;
        public GameObject fireballPrefab;
        public Transform firePoint;

        public UnityAction onDeath;
        public UnityAction onHit;
        public UnityAction onJump;
        public UnityAction onLand;
        public UnityAction onCrouch;
        public UnityAction onShoot;
        public UnityAction onWin;

        private Rigidbody2D rigid;
        private BoxCollider2D box_coll;
        private Vector2 coll_start_size;
        private Vector2 coll_start_offset;
        private Vector3 start_scale;
        private Vector3 last_ground_pos;
        private Vector3 average_ground_pos;

        private Vector2 move_input;
        private bool jump_press;
        private bool jump_hold;
        private bool action_press;
        private bool action_hold;

        private float hp;
        public bool is_dead = false;
        private bool was_grounded = false;
        private bool is_grounded = false;
        private bool is_crouch = false;
        private bool is_jumping = false;
        private bool is_double_jump = false;
        private bool disable_controls = false;
        private float grounded_timer = 0f;
        private float hit_timer = 0f;

        private bool facingRight = true;

        private static Dictionary<int, PlayerCharacterOne> character_list = new Dictionary<int, PlayerCharacterOne>();

        private bool gate;

        void Awake()
        {
            character_list[player_id] = this;
            rigid = GetComponent<Rigidbody2D>();
            box_coll = GetComponent<BoxCollider2D>();
            coll_start_size = box_coll.size;
            coll_start_offset = box_coll.offset;
            start_scale = transform.localScale;
            average_ground_pos = rigid.position;
            last_ground_pos = rigid.position;
            hp = max_hp;
        }

        void Start()
        {
            gameObject.SetActive(true);

            if (firePoint == null)
            {
                GameObject firePointObject = new GameObject("FirePoint");
                firePointObject.transform.parent = transform;
                firePoint = firePointObject.transform;
                firePoint.localPosition = new Vector3(0.5f, 0, 0);
            }

            is_dead = false;

            gate = false;

            CheckpointManager.RegisterLevelStart(player_id, transform.position);

            // Heroes pass through each other (Playtest 5) — both Starts call
            // this; whichever runs second finds both players registered.
            PlayerPassThrough.Apply();
        }

        void OnDestroy()
        {
            character_list.Remove(player_id);
        }

        void FixedUpdate()
        {
            if (is_dead)
                return;

            if (disable_controls)
            {
                rigid.velocity = new Vector2(0f, rigid.velocity.y);
            }
            else
            {
                // Apply horizontal movement
                float targetSpeed = move_input.x * move_max;
                float speedDiff = targetSpeed - rigid.velocity.x;
                float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? move_accel : move_deccel;
                accelRate = !is_grounded ? accelRate * 0.5f : accelRate; // Reduced air control

                float movement = speedDiff * accelRate * Time.fixedDeltaTime;
                rigid.AddForce(new Vector2(movement, 0));

                UpdateFacing();
                UpdateJump();
                UpdateCrouch();
            }
        }

        void Update()
        {
            if (is_dead)
                return;

            hit_timer += Time.deltaTime;
            grounded_timer += Time.deltaTime;

            // Controls (B7: never assume the registry has us)
            PlayerControlsOne controls = PlayerControlsOne.Get(player_id);
            bool controls_ok = controls != null && !disable_controls;
            move_input = controls_ok ? controls.GetMove() : Vector2.zero;
            jump_press = controls_ok && controls.GetJumpDown();
            jump_hold = controls_ok && controls.GetJumpHold();
            action_press = controls_ok && controls.GetActionDown();
            action_hold = controls_ok && controls.GetActionHold();

            if (!disable_controls && jump_press)
                Jump();

            if (can_shoot && action_press)
            {
                ShootFireball();
            }

            // Removed fall-below-level reset code
        }

        public void EnableShooting()
        {
            can_shoot = true;
        }

        public void DisableShooting()
        {
            can_shoot = false;
        }

        private void ShootFireball()
        {
            if (fireballPrefab != null && firePoint != null)
            {
                AudioManager.PlaySfx("shoot");
                onShoot?.Invoke();
                Vector3 spawnPosition = rigid.position + new Vector2(facingRight ? 0.5f : -0.5f, 0);
                Quaternion spawnRotation = facingRight ? Quaternion.identity : Quaternion.Euler(0, 180, 0);

                GameObject fireball = Instantiate(fireballPrefab, spawnPosition, spawnRotation);

                Rigidbody2D fireballRb = fireball.GetComponent<Rigidbody2D>();
                if (fireballRb != null)
                {
                    Vector2 direction = facingRight ? Vector2.right : Vector2.left;
                    Fireball fireballScript = fireball.GetComponent<Fireball>();
                    float speed = fireballScript != null ? fireballScript.speed : 5f;
                    fireballRb.velocity = direction * speed;
                }
            }
        }

        private void UpdateFacing()
        {
            if (Mathf.Abs(move_input.x) > 0.01f)
            {
                facingRight = move_input.x > 0f;
                float side = facingRight ? 1f : -1f;
                transform.localScale = new Vector3(start_scale.x * side, start_scale.y, start_scale.z);

                if (firePoint != null)
                {
                    firePoint.localPosition = new Vector3(facingRight ? 0.5f : -0.5f, firePoint.localPosition.y, firePoint.localPosition.z);
                    firePoint.localRotation = Quaternion.Euler(0, facingRight ? 0 : 180, 0);
                }
            }
        }

        private void UpdateJump()
        {
            was_grounded = is_grounded;
            is_grounded = DetectGrounded();

            if (is_grounded)
            {
                is_jumping = false;
                is_double_jump = false;
            }

            // Adjust gravity for smoother jump
            if (rigid.velocity.y > 0.1f && !jump_hold)
            {
                rigid.gravityScale = 1.5f;
            }
            else if (rigid.velocity.y < -0.1f)
            {
                rigid.gravityScale = 1.5f;
            }
            else
            {
                rigid.gravityScale = 1f;
            }

            // Update last grounded position
            if (is_grounded)
            {
                average_ground_pos = Vector2.Lerp(rigid.position, average_ground_pos, 0.5f);
                last_ground_pos = average_ground_pos;
            }

            // Trigger landing event
            if (!was_grounded && is_grounded)
            {
                onLand?.Invoke();
            }
        }

        private void UpdateCrouch()
        {
            if (!can_crouch)
                return;

            bool was_crouch = is_crouch;
            if (move_input.y < -0.1f && is_grounded)
            {
                is_crouch = true;
                box_coll.size = new Vector2(coll_start_size.x, coll_start_size.y * crouch_coll_percent);
                box_coll.offset = new Vector2(coll_start_offset.x, coll_start_offset.y - coll_start_size.y * (1f - crouch_coll_percent) / 2f);

                if (!was_crouch && is_crouch)
                {
                    onCrouch?.Invoke();
                }
            }
            else
            {
                is_crouch = false;
                box_coll.size = coll_start_size;
                box_coll.offset = coll_start_offset;
            }
        }

        public void Jump()
        {
            if (can_jump && !is_crouch)
            {
                if (is_grounded)
                {
                    // First jump
                    rigid.velocity = new Vector2(rigid.velocity.x, jump_strength);
                    is_jumping = true;
                    onJump?.Invoke();
                }
                else if (double_jump && !is_double_jump)
                {
                    // Double jump
                    rigid.velocity = new Vector2(rigid.velocity.x, jump_strength);
                    is_double_jump = true;
                    onJump?.Invoke();
                }
            }
        }

        private bool DetectGrounded()
        {
            Vector2 position = rigid.position + box_coll.offset;
            Vector2 direction = Vector2.down;
            float distance = (box_coll.size.y / 2f) + ground_raycast_dist;

            RaycastHit2D hit = Physics2D.Raycast(position, direction, distance, ground_layer);
#if UNITY_EDITOR
            Debug.DrawRay(position, direction * distance, Color.red);
#endif

            if (hit.collider != null && hit.collider != box_coll && !hit.collider.isTrigger)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Teleport(Vector3 pos)
        {
            rigid.position = pos;
            rigid.velocity = Vector2.zero;
            is_jumping = false;
        }

        public void HealDamage(float heal)
        {
            if (!is_dead)
            {
                hp += heal;
                hp = Mathf.Min(hp, max_hp);
            }
        }

        public void TakeDamage(float damage)
        {
            if (!is_dead && !invulnerable && hit_timer > 0f)
            {
                hp -= damage;
                hit_timer = -1f;

                if (hp <= 0f)
                {
                    Kill();
                }
                else
                {
                    onHit?.Invoke();
                }
            }
        }

        public void Kill()
        {
            if (!is_dead)
            {
                is_dead = true;
                rigid.velocity = Vector2.zero;
                move_input = Vector2.zero;

                onDeath?.Invoke();

                StartCoroutine(RespawnCharacter());
            }
        }

        private IEnumerator RespawnCharacter()
        {
            yield return new WaitForSeconds(2f);

            // If the partner also died meanwhile, GameState owns the fail
            // flow — don't pop back to life underneath the overlay. An
            // exit-absorbed (inactive) partner still allows respawning.
            PlayerCharacterTwo partner = PlayerCharacterTwo.Get(2);
            bool partnerAbsorbed = partner != null && !partner.gameObject.activeSelf;
            bool partnerAlive = partner != null && partner.gameObject.activeSelf && !partner.IsDead();
            if (!partnerAlive && !partnerAbsorbed)
                yield break;

            is_dead = false;
            hp = max_hp;
            Vector3 spawn = CheckpointManager.GetRespawnPosition(
                player_id,
                partnerAlive ? partner.transform.position : transform.position,
                partnerAlive);
            Teleport(spawn);
            hit_timer = -1f; // brief spawn protection (1s, same as i-frames)
            EnableControls();
        }

        public void DisableControls() { disable_controls = true; }
        public void EnableControls() { disable_controls = false; }

        public Vector2 GetMove()
        {
            return rigid.velocity;
        }

        public Vector2 GetFacing()
        {
            return Vector2.right * Mathf.Sign(transform.localScale.x);
        }

        public bool IsJumping()
        {
            return is_jumping;
        }

        public bool IsGrounded()
        {
            return is_grounded;
        }

        public bool IsCrouching()
        {
            return is_crouch;
        }

        public float GetHP()
        {
            return hp;
        }

        public bool IsDead()
        {
            return is_dead;
        }

        public Vector2 GetSize()
        {
            if (box_coll != null)
                return new Vector2(Mathf.Abs(transform.localScale.x) * box_coll.size.x, Mathf.Abs(transform.localScale.y) * box_coll.size.y);
            return new Vector2(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y));
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (is_dead)
                return;

            // Lava must go through Kill() so onDeath fires (GameState is
            // event-driven now — a silent is_dead flag would be ignored).
            if (collision.gameObject.CompareTag("LavaSquare"))
            {
                Kill();
            }

            if (collision.gameObject.CompareTag("Dragon"))
            {
                // Playtest 5: levels with ExitDoors win via both-heroes-at-
                // door — ExitDoor owns the win path and this legacy portal
                // absorption stands down. Kept for scenes the editor pass
                // hasn't migrated yet.
                if (ExitDoor.AnyInLevel)
                    return;
                onWin?.Invoke();
                UpdateDragonCount();
                gameObject.SetActive(false);
            }
        }

        void UpdateDragonCount()
        {
            GameState gameState = FindObjectOfType<GameState>();
            if (gameState != null && gate == false)
            {
                gameState.dragonCount++;
                gate = true;
            }
        }

        public static PlayerCharacterOne GetNearest(Vector3 pos, float range = 99999f, bool alive_only = false)
        {
            PlayerCharacterOne nearest = null;
            float min_dist = range;
            foreach (PlayerCharacterOne character in GetAll())
            {
                if (!alive_only || !character.IsDead())
                {
                    float dist = (pos - character.transform.position).magnitude;
                    if (dist < min_dist)
                    {
                        min_dist = dist;
                        nearest = character;
                    }
                }
            }
            return nearest;
        }

        public static PlayerCharacterOne Get(int player_id)
        {
            if (character_list.TryGetValue(player_id, out PlayerCharacterOne character))
            {
                return character;
            }
            return null;
        }

        public static PlayerCharacterOne[] GetAll()
        {
            PlayerCharacterOne[] list = new PlayerCharacterOne[character_list.Count];
            character_list.Values.CopyTo(list, 0);
            return list;
        }
    }
}
