using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace IndieMarc.Platformer
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PlayerCharacterTwo : MonoBehaviour
    {
        public int player_id = 2;

        [Header("Stats")]
        public float max_hp = 100f;

        [Header("Status")]
        public bool invulnerable = false;

        [Header("Movement")]
        public float move_accel = 50f; // Increased acceleration
        public float move_deccel = 50f; // Increased deceleration
        public float move_max = 5f;

        [Header("Jump")]
        public bool can_jump = true;
        public bool double_jump = true;
        public float jump_strength = 10f; // Increased jump strength for better upward flow
        public LayerMask ground_layer;
        public float ground_raycast_dist = 0.1f;

        [Header("Crouch")]
        public bool can_crouch = true;
        public float crouch_coll_percent = 0.5f;

        // Removed "Fall Below Level" fields and logic

        [Header("Transformation")]
        public GameObject rectanglePrefab;

        public UnityAction onDeath;
        public UnityAction onHit;
        public UnityAction onJump;
        public UnityAction onLand;
        public UnityAction onCrouch;
        public UnityAction onTransform;
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

        private bool hasPowerUp = false;
        private bool isRectangleForm = false;
        private GameObject rectangleInstance;

        private static Dictionary<int, PlayerCharacterTwo> character_list = new Dictionary<int, PlayerCharacterTwo>();

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

        void OnDestroy()
        {
            character_list.Remove(player_id);
        }

        void Start()
        {
            gameObject.SetActive(true);
            is_dead = false;

            CheckpointManager.RegisterLevelStart(player_id, transform.position);

            // Heroes pass through each other (Playtest 5) — both Starts call
            // this; whichever runs second finds both players registered.
            PlayerPassThrough.Apply();
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

                float movement = speedDiff * accelRate * Time.fixedDeltaTime; // Adjusted for frame rate

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
            PlayerControlsTwo controls = PlayerControlsTwo.Get(player_id);
            bool controls_ok = controls != null && !disable_controls;
            move_input = controls_ok ? controls.GetMove() : Vector2.zero;
            jump_press = controls_ok && controls.GetJumpDown();
            jump_hold = controls_ok && controls.GetJumpHold();

            // Action stays readable while controls are disabled so the
            // platform form can be exited (disable_controls is true in-form).
            action_press = controls != null && controls.GetActionDown();
            action_hold = controls != null && controls.GetActionHold();

            if (!disable_controls && jump_press)
                Jump();

            // Handle transformation
            if (hasPowerUp && action_press)
            {
                ToggleForm();
            }

            // Removed fall-below-level reset code
        }

        private void UpdateFacing()
        {
            if (Mathf.Abs(move_input.x) > 0.01f)
            {
                float side = (move_input.x < 0f) ? -1f : 1f;
                transform.localScale = new Vector3(start_scale.x * side, start_scale.y, start_scale.z);
            }
        }

        private void UpdateJump()
        {
            was_grounded = is_grounded;
            
            // Detect if the character is grounded
            is_grounded = DetectGrounded();

            // Reset jump variables when grounded
            if (is_grounded)
            {
                is_jumping = false;
                is_double_jump = false;
            }

            // Adjust gravity for smoother jump
            if (rigid.velocity.y > 0.1f && !jump_hold)
            {
                // Apply higher gravity when player releases jump early
                rigid.gravityScale = 1.5f;
            }
            else if (rigid.velocity.y < -0.1f)
            {
                // Apply higher gravity when falling
                rigid.gravityScale = 1.5f;
            }
            else
            {
                // Normal gravity
                rigid.gravityScale = 1f;
            }

            // Update grounded position and save the last valid grounded position
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
                // Dying in platform form reverts first so the respawned
                // character isn't invisible with physics disabled.
                if (isRectangleForm)
                    ToggleForm();

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
            PlayerCharacterOne partner = PlayerCharacterOne.Get(1);
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

        public void CollectPowerUp()
        {
            hasPowerUp = true;
        }

        private void ToggleForm()
        {
            isRectangleForm = !isRectangleForm;
            onTransform?.Invoke();
            AudioManager.PlaySfx("swap", 0.1f);

            if (isRectangleForm)
            {
                // Transformation into rectangle form

                if (rectangleInstance == null)
                {
                    rectangleInstance = Instantiate(rectanglePrefab);
                    rectangleInstance.name = "RectanglePlatform";
                }
                else
                {
                    rectangleInstance.SetActive(true);
                }

                rectangleInstance.transform.position = transform.position;

                // Disable Player 2's collider and rigidbody
                box_coll.enabled = false;
                rigid.simulated = false;

                // Enable rectangle's collider
                BoxCollider2D rectCollider = rectangleInstance.GetComponent<BoxCollider2D>();
                if (rectCollider == null)
                {
                    rectCollider = rectangleInstance.AddComponent<BoxCollider2D>();
                }
                rectCollider.enabled = true;
                rectCollider.isTrigger = false;

                int platformLayerIndex = LayerMask.NameToLayer("Platform");
                if (platformLayerIndex == -1)
                {
                    Debug.LogError("Layer 'Platform' does not exist. Please add it in the Tags and Layers settings.");
                }
                else
                {
                    rectangleInstance.layer = platformLayerIndex;
                }

                Rigidbody2D rectRigid = rectangleInstance.GetComponent<Rigidbody2D>();
                if (rectRigid == null)
                {
                    rectRigid = rectangleInstance.AddComponent<Rigidbody2D>();
                }
                rectRigid.bodyType = RigidbodyType2D.Static;
                rectRigid.simulated = true;

                SpriteRenderer rectSprite = rectangleInstance.GetComponent<SpriteRenderer>();
                if (rectSprite != null)
                {
                    rectSprite.enabled = true;
                }

                // Hide Player 2's sprite
                GetComponent<SpriteRenderer>().enabled = false;

                // Disable Player 2's controls
                disable_controls = true;
            }
            else
            {
                // Reverting back to normal form

                // Re-enable Player 2's collider and rigidbody
                box_coll.enabled = true;
                rigid.simulated = true;

                // Show Player 2's sprite
                GetComponent<SpriteRenderer>().enabled = true;

                // Disable rectangleInstance's components
                if (rectangleInstance != null)
                {
                    BoxCollider2D rectCollider = rectangleInstance.GetComponent<BoxCollider2D>();
                    if (rectCollider != null)
                    {
                        rectCollider.enabled = false;
                    }

                    Rigidbody2D rectRigid = rectangleInstance.GetComponent<Rigidbody2D>();
                    if (rectRigid != null)
                    {
                        rectRigid.simulated = false;
                    }

                    SpriteRenderer rectSprite = rectangleInstance.GetComponent<SpriteRenderer>();
                    if (rectSprite != null)
                    {
                        rectSprite.enabled = false;
                    }
                }

                // Re-enable Player 2's controls
                disable_controls = false;

                // Reposition Player 2 to the rectangle's position
                transform.position = rectangleInstance.transform.position;

                // Re-enabling the collider drops the Physics2D ignore pairs
                // with Player 1 — restore pass-through (see PlayerPassThrough).
                PlayerPassThrough.Apply();
            }
        }

        public static PlayerCharacterTwo GetNearest(Vector3 pos, float range = 99999f, bool alive_only = false)
        {
            PlayerCharacterTwo nearest = null;
            float min_dist = range;
            foreach (PlayerCharacterTwo character in GetAll())
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

        public static PlayerCharacterTwo Get(int player_id)
        {
            if (character_list.TryGetValue(player_id, out PlayerCharacterTwo character))
            {
                return character;
            }
            return null;
        }

        public static PlayerCharacterTwo[] GetAll()
        {
            PlayerCharacterTwo[] list = new PlayerCharacterTwo[character_list.Count];
            character_list.Values.CopyTo(list, 0);
            return list;
        }
    }
}
