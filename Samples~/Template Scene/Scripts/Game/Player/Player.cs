using UnityEngine;
using UnityEngine.InputSystem;
using Utility;

namespace Game.Player {
    [RequireComponent(typeof(Rigidbody2D))]
    public class Player : MonoBehaviour {
        [Header("Signals")]

        [SerializeField] private BoolSignalAsset m_isWalking;
        [SerializeField] private BoolSignalAsset m_isOnGround;

        [Header("Movement")]
        [Range(4f, 20f)] [SerializeField] private float m_moveSpeed = 8f;

        [Range(20f, 200f)] [SerializeField] private float m_acceleration = 90f;
        [Range(20f, 200f)] [SerializeField] private float m_deceleration = 110f;
        [Range(20f, 260f)] [SerializeField] private float m_turnAcceleration = 150f;
        [Range(10f, 150f)] [SerializeField] private float m_airAcceleration = 65f;
        [Range(5f, 100f)] [SerializeField] private float m_airDeceleration = 45f;
        [Range(5f, 180f)] [SerializeField] private float m_airTurnAcceleration = 95f;
        [Range(0f, 0.3f)] [SerializeField] private float m_inputSmoothing = 0.08f;
        [Range(1f, 1.25f)] [SerializeField] private float m_apexSpeedBoost = 1.08f;

        [Header("Jump")]
        [Range(3f, 20f)] [SerializeField] private float m_jumpForce = 10.5f;

        [Range(0.05f, 0.3f)] [SerializeField] private float m_coyoteTime = 0.12f;
        [Range(0.05f, 0.3f)] [SerializeField] private float m_jumpBufferTime = 0.12f;
        [Range(0.1f, 0.9f)] [SerializeField] private float m_jumpCutMultiplier = 0.35f;

        [Header("Audio")]
        [SerializeField] private AudioAsset m_jumpSound;

        [Header("Gravity")]
        [Range(1f, 6f)] [SerializeField] private float m_fallGravityMultiplier = 4f;

        [Range(1f, 6f)] [SerializeField] private float m_lowJumpGravityMultiplier = 3f;
        [Range(0.1f, 1.5f)] [SerializeField] private float m_apexGravityMultiplier = 0.9f;
        [Range(0.05f, 0.8f)] [SerializeField] private float m_apexThreshold = 0.2f;
        [Range(-40f, -5f)] [SerializeField] private float m_maxFallSpeed = -22f;
        
        private Rigidbody2D m_rb;
        private float m_smoothedMoveInput;
        private float m_moveInputVelocity;
        private float m_moveInput;
        private bool m_isGrounded;
        private bool m_jumpHeld;
        private float m_coyoteTimer;
        private float m_jumpBufferTimer;

        private void Awake() {
            m_rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable() {
            InputManager.instance.onJumpPerformed.AddListener(OnJumpPerformed);
            InputManager.instance.onJumpCanceled.AddListener(OnJumpCanceled);
            m_isOnGround.AddListener(OnGroundedCheck);
        }

        private void OnDisable() {
            InputManager.instance.onJumpPerformed.RemoveListener(OnJumpPerformed);
            InputManager.instance.onJumpCanceled.RemoveListener(OnJumpCanceled);
            m_isOnGround.RemoveListener(OnGroundedCheck);
        }

        private void Update() {
            ReadInput();
            HandleSpriteFlip();
        }

        private void FixedUpdate() {
            UpdateTimers();
            HandleMovement();
            HandleJumping();
            HandleBetterGravity();
            ClampFallSpeed();
        }

        private void ReadInput() {
            float rawInput = InputManager.instance.GetMovementVectorRaw().x;
            rawInput = Mathf.Abs(rawInput) < 0.01f ? 0f : Mathf.Clamp(rawInput, -1f, 1f);
            m_moveInput = rawInput;
            bool isWalking = Mathf.Abs(m_moveInput) > 0.01f;
            Debug.Log($"MoveInput: {m_moveInput}, Walking: {isWalking}");
            m_isWalking?.Invoke(isWalking);
        }

        private void OnJumpPerformed(InputAction.CallbackContext ctx) {
            m_jumpHeld = true;
            m_jumpBufferTimer = m_jumpBufferTime;
        }

        private void OnJumpCanceled(InputAction.CallbackContext ctx) {
            m_jumpHeld = false;
            if (m_rb.linearVelocity.y > 0f) {
                m_rb.linearVelocity = new Vector2(m_rb.linearVelocity.x, m_rb.linearVelocity.y * m_jumpCutMultiplier);
            }
        }

        private void OnGroundedCheck(bool grounded) {
            m_isGrounded = grounded;
            if (grounded) m_rb.gravityScale = 1f;
        }

        private void UpdateTimers() {
            if (m_isGrounded) {
                m_coyoteTimer = m_coyoteTime;
            } else {
                m_coyoteTimer -= Time.fixedDeltaTime;
            }

            m_jumpBufferTimer -= Time.fixedDeltaTime;
        }

        private void HandleJumping() {
            if (!(m_jumpBufferTimer > 0f) || !(m_coyoteTimer > 0f)) return;
            Jump();
            m_jumpBufferTimer = 0f;
            m_coyoteTimer = 0f;
        }

        private void Jump() {
            m_rb.linearVelocity = new(m_rb.linearVelocity.x, m_jumpForce);
            global::Game.AudioManager.instance.Play(m_jumpSound);
        }

        private void HandleMovement() {
            m_smoothedMoveInput = Mathf.SmoothDamp(
                m_smoothedMoveInput,
                m_moveInput,
                ref m_moveInputVelocity,
                m_inputSmoothing,
                Mathf.Infinity,
                Time.fixedDeltaTime
            );

            if (m_moveInput == 0f && Mathf.Abs(m_smoothedMoveInput) < 0.01f) {
                m_smoothedMoveInput = 0f;
            }

            float speed = m_moveSpeed;
            if (!m_isGrounded && Mathf.Abs(m_rb.linearVelocity.y) < m_apexThreshold) {
                speed *= m_apexSpeedBoost;
            }

            float targetSpeed = m_smoothedMoveInput * speed;
            float accelRate;
            if (Mathf.Abs(targetSpeed) > 0.01f) {
                bool turning = Mathf.Abs(m_rb.linearVelocity.x) > 0.01f &&
                               Mathf.Sign(m_rb.linearVelocity.x) != Mathf.Sign(targetSpeed);
                accelRate = turning
                    ? (m_isGrounded ? m_turnAcceleration : m_airTurnAcceleration)
                    : (m_isGrounded ? m_acceleration : m_airAcceleration);
            } else {
                accelRate = m_isGrounded ? m_deceleration : m_airDeceleration;
            }

            float newX = Mathf.MoveTowards(m_rb.linearVelocity.x, targetSpeed, accelRate * Time.fixedDeltaTime);
            m_rb.linearVelocity = new Vector2(newX, m_rb.linearVelocity.y);
        }

        private void HandleBetterGravity() {
            if (m_isGrounded) return;
            if (m_rb.linearVelocity.y < 0f) {
                m_rb.gravityScale = m_fallGravityMultiplier;
            } else if (m_rb.linearVelocity.y > 0f && !m_jumpHeld) {
                m_rb.gravityScale = m_lowJumpGravityMultiplier;
            } else if (Mathf.Abs(m_rb.linearVelocity.y) < m_apexThreshold) {
                m_rb.gravityScale = m_apexGravityMultiplier; // apex last, only when holding jump at peak
            } else {
                m_rb.gravityScale = 1f;
            }
        }

        private void ClampFallSpeed() {
            if (m_rb.linearVelocity.y < m_maxFallSpeed) {
                m_rb.linearVelocity = new Vector2(m_rb.linearVelocity.x, m_maxFallSpeed);
            }
        }

        private void HandleSpriteFlip() {
            if (Mathf.Abs(m_moveInput) < 0.01f) return;
            transform.localScale = new Vector3(Mathf.Sign(m_moveInput), 1f, 1f);
        }
    }
}
