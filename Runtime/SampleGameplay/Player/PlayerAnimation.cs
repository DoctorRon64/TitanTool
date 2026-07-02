using UnityEngine;
using Utility;

namespace Game.Player {
    public class PlayerAnimation : MonoBehaviour {
        [SerializeField] private BoolSignalAsset m_isOnGround;
        [SerializeField] private BoolSignalAsset m_isWalking;
        [SerializeField] private Animator animator;
        [SerializeField] private float transitionTime = 0.2f;

        private bool isGrounded;
        private bool isWalking;

        private int currentAnimation;

        private void Awake() {
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        private void OnEnable() {
            m_isOnGround.AddListener(OnGroundedChanged);
            m_isWalking.AddListener(OnWalkingChanged);
        }

        private void OnDisable() {
            m_isOnGround.RemoveListener(OnGroundedChanged);
            m_isWalking.RemoveListener(OnWalkingChanged);
        }

        private void OnGroundedChanged(bool value) {
            isGrounded = value;
            UpdateAnimation();
        }

        private void OnWalkingChanged(bool value) {
            isWalking = value;
            UpdateAnimation();
        }

        private void UpdateAnimation() {
            if (!isGrounded) {
                ChangeAnimation(AnimationNames.PLAYER_JUMP); 
            } else if (isWalking) {
                ChangeAnimation(AnimationNames.PLAYER_RUN);
            } else {
                ChangeAnimation(AnimationNames.PLAYER_IDLE); 
            }
        }

        private void ChangeAnimation(int animationHash) {
            if (currentAnimation == animationHash)
                return;

            currentAnimation = animationHash;
            animator.CrossFade(animationHash, transitionTime, 0);
        }
    }
}