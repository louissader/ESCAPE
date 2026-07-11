using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player controls for platformer demo (Second Player)
/// Author: Indie Marc (Marc-Antoine Desbiens)
/// </summary>

namespace IndieMarc.Platformer
{
    public class PlayerControlsTwo : MonoBehaviour
    {
        public int player_id = 2; // Set this to a different ID for the second player

        // Assign default keys for the second player
        public KeyCode left_key = KeyCode.LeftArrow;
        public KeyCode right_key = KeyCode.RightArrow;
        public KeyCode up_key = KeyCode.UpArrow;
        public KeyCode down_key = KeyCode.DownArrow;
        public KeyCode jump_key = KeyCode.RightControl; // Or any preferred key
        public KeyCode action_key = KeyCode.RightShift; // Or any preferred action key

        private Vector2 move = Vector2.zero;
        private bool jump_press = false;
        private bool jump_hold = false;
        private bool action_press = false;
        private bool action_hold = false;

        private static Dictionary<int, PlayerControlsTwo> controls = new Dictionary<int, PlayerControlsTwo>();

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

            if (Input.GetKey(left_key))
                move += Vector2.left;
            if (Input.GetKey(right_key))
                move += Vector2.right;
            if (Input.GetKey(up_key))
                move += Vector2.up;
            if (Input.GetKey(down_key))
                move += Vector2.down;
            if (Input.GetKey(jump_key))
                jump_hold = true;
            if (Input.GetKeyDown(jump_key))
                jump_press = true;
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

            float move_length = Mathf.Min(move.magnitude, 1f);
            move = move.normalized * move_length;

        }

        //------ These functions should be called from the Update function, not FixedUpdate
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

        //-----------

        public static PlayerControlsTwo Get(int player_id)
        {
            if (controls.TryGetValue(player_id, out PlayerControlsTwo control))
            {
                return control;
            }
            return null;
        }

        public static PlayerControlsTwo[] GetAll()
        {
            PlayerControlsTwo[] list = new PlayerControlsTwo[controls.Count];
            controls.Values.CopyTo(list, 0);
            return list;
        }
    }
}
