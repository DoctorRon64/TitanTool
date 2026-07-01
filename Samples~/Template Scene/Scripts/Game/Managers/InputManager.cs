using UnityEngine;
using Utility;
using UnityEngine.InputSystem;

namespace Game {
    public class InputManager : Singleton<InputManager>, ISingleton {
        public Signal<InputAction.CallbackContext> onJumpPerformed { get; private set; } = new();
        public Signal<InputAction.CallbackContext> onJumpCanceled { get; private set; } = new();
        public Signal<InputAction.CallbackContext> onShootPerformed { get; private set; } = new();
        private PlayerInputActions m_playerInputActions;

        void ISingleton.OnInitialize() {
            m_playerInputActions = new();
            m_playerInputActions.Enable();
            m_playerInputActions.Player.Enable();

            m_playerInputActions.Player.Jump.performed += _ctx => onJumpPerformed?.Invoke(_ctx);
            m_playerInputActions.Player.Jump.canceled += _ctx => onJumpCanceled?.Invoke(_ctx);
            m_playerInputActions.Player.Attack.performed += _ctx => onShootPerformed?.Invoke(_ctx);
        }

        void ISingleton.OnDestroy() {
            m_playerInputActions?.Disable();
            onJumpPerformed?.Clear();
            onJumpCanceled?.Clear();
            onShootPerformed?.Clear();
        }

        public Vector2 GetPointerPosition() {
            if (Pointer.current != null)
                return Pointer.current.position.ReadValue();

            return Vector2.zero;
        }

        public Vector2 GetMousePosition() => GetPointerPosition();
        public Vector2 GetMovementVectorRaw() => m_playerInputActions.Player.Move.ReadValue<Vector2>();
        public Vector2 GetLookPosition() => m_playerInputActions.Player.Look.ReadValue<Vector2>();
        public Vector2 GetMovementVectorNormalized() => m_playerInputActions.Player.Move.ReadValue<Vector2>().normalized;
    }
}