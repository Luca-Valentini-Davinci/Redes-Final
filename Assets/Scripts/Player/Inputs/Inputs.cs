using System;
using Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Network.Platformer
{
    [CreateAssetMenu(fileName = "InputReader", menuName = "Player/Input/InputReader")]
    public class Inputs : ScriptableObject , PlayerInputsMap.IPlayerInputsActions
    {
        public Action<float> Move = delegate { };
        public Action Jump = delegate { };
        public Action Scape = delegate { };

        public float HorizontalAxis => _inputActions.PlayerInputs.Move.ReadValue<float>();
        private PlayerInputsMap _inputActions;
        #region Initialize
        public void OnEnable()
        {
            if (_inputActions == null)
            {
                _inputActions = new PlayerInputsMap();
                _inputActions.PlayerInputs.SetCallbacks(this);
            }
            EnablePlayerActions(true);
        }
        private void OnDisable()
        {
            EnablePlayerActions(false);
        }

        public void EnablePlayerActions(bool enable)
        {
            if (enable) _inputActions.Enable();
            else _inputActions.Disable();
        }
        
        #endregion

        #region PlayerInputs
        public void OnMove(InputAction.CallbackContext context)
        {
            Move.Invoke(context.ReadValue<float>());
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if(context.performed)Jump.Invoke();
            // switch (context.phase)
            // {
            //     case InputActionPhase.Started:
            //         Jump.Invoke(true);
            //         break;
            //     case InputActionPhase.Canceled:
            //         Jump.Invoke(false);
            //         break;
            // }
        }

        public void OnEscape(InputAction.CallbackContext context)
        {
            if (context.performed) Scape.Invoke();
        }
        #endregion
    }
}