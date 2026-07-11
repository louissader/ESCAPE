using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IndieMarc.Platformer
{
    [RequireComponent(typeof(PlayerCharacterTwo))] // Ensure this is PlayerCharacterTwo
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimTwo : MonoBehaviour
    {
        private PlayerCharacterTwo character;
        private Animator animator;

        void Awake()
        {
            character = GetComponent<PlayerCharacterTwo>();
            animator = GetComponent<Animator>();

            character.onJump += OnJump;
            character.onCrouch += OnCrouch;
            character.onDeath += OnDeath;
            character.onHit += () => animator.SetTrigger("Hurt");
            character.onTransform += () => animator.SetTrigger("Transform");
            character.onWin += () => animator.SetTrigger("Win");
        }

        void Update()
        {
            if (character.IsDead())
            {
                animator.SetBool("Dead", true);
                return;
            }
            else
            {
                animator.SetBool("Dead", false);
            }

            // Animations
            animator.SetBool("Jumping", character.IsJumping());
            animator.SetBool("InAir", !character.IsGrounded());
            animator.SetBool("Crouching", character.IsCrouching());
            animator.SetFloat("Speed", Mathf.Abs(character.GetMove().x));
        }

        void OnCrouch()
        {
            animator.SetTrigger("Crouch");
        }

        void OnJump()
        {
            animator.SetTrigger("Jump");
        }

        void OnDeath()
        {
            animator.SetTrigger("Death");
        }
    }
}
