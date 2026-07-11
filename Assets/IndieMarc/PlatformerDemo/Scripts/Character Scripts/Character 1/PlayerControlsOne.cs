using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player controls for platformer demo
/// Author: Indie Marc (Marc-Antoine Desbiens)
/// </summary>

namespace IndieMarc.Platformer
{
    public class PlayerControlsOne : MonoBehaviour
    {
        public int player_id;

        // Key variables are private to prevent changes from the Inspector
        private KeyCode left_key = KeyCode.LeftArrow;
        private KeyCode right_key = KeyCode.RightArrow;
        private KeyCode up_key = KeyCode.UpArrow;
        private KeyCode down_key = KeyCode.DownArrow;
        private KeyCode jump_key = KeyCode.UpArrow; // Up arrow for jump
        private KeyCode action_key = KeyCode.Space; // Space key for action (shooting)

        private Vector2 move = Vector2.zero;
        private bool jump_press = false;
        private bool jump_hold = false;
        private bool action_press = false;
        private bool action_hold = false;

        private static Dictionary<int, PlayerControlsOne> controls = new Dictionary<int, PlayerControlsOne>();

        void Awake()
        {
            controls[player_id] = this;
        }

        void OnDestroy()
        {
            controls.Remove(player_id);
        }

        void Update()
        {
            move = Vector2.zero;
            jump_hold = false;
            jump_press = false;
            action_hold = false;
            action_press = false;

            // Movement input
            if (Input.GetKey(left_key))
                move += Vector2.left;
            if (Input.GetKey(right_key))
                move += Vector2.right;
            if (Input.GetKey(up_key))
                move += Vector2.up;
            if (Input.GetKey(down_key))
                move += Vector2.down;

            // Jump input
            if (Input.GetKey(jump_key))
                jump_hold = true;
            if (Input.GetKeyDown(jump_key))
                jump_press = true;

            // Action input
            if (Input.GetKey(action_key))
                action_hold = true;
            if (Input.GetKeyDown(action_key))
                action_press = true;

            // Merge the virtual (touch/test) channel with keyboard input.
            VirtualInputChannel virtual_input = VirtualInputRouter.Get(player_id);
            move.x = Mathf.Clamp(move.x + virtual_input.move, -1f, 1f);
            if (virtual_input.downHold)
                move.y = Mathf.Min(move.y - 1f, -1f);
            if (virtual_input.jumpHold)
                jump_hold = true;
            if (virtual_input.ConsumeJumpDown())
                jump_press = true;
            if (virtual_input.actionHold)
                action_hold = true;
            if (virtual_input.ConsumeActionDown())
                action_press = true;

            // Normalize movement vector to ensure consistent speed
            float move_length = Mathf.Min(move.magnitude, 1f);
            move = move.normalized * move_length;
        }

        // Public methods to access input states
        public Vector2 GetMove()
        {
            return move;
        }

        public bool GetJumpDown()
        {
            return jump_press;
        }

        public bool GetJumpHold()
        {
            return jump_hold;
        }

        public bool GetActionDown()
        {
            return action_press;
        }

        public bool GetActionHold()
        {
            return action_hold;
        }

        // Static methods for accessing controls by player ID
        public static PlayerControlsOne Get(int player_id)
        {
            if (controls.TryGetValue(player_id, out PlayerControlsOne control))
            {
                return control;
            }
            return null;
        }

        public static PlayerControlsOne[] GetAll()
        {
            PlayerControlsOne[] list = new PlayerControlsOne[controls.Count];
            controls.Values.CopyTo(list, 0);
            return list;
        }
    }
}
