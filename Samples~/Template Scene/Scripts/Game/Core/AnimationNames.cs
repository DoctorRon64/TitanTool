using UnityEngine;

namespace Game {
    public static class AnimationNames {
        public const string BULLET_EXPLODE = "bullet_explode";
        public const string BULLET_IDLE = "bullet_idle";
        public static readonly int PLAYER_IDLE = Animator.StringToHash("player_idle");
        public static readonly int PLAYER_RUN = Animator.StringToHash("player_run");
        public static readonly int PLAYER_JUMP = Animator.StringToHash("player_jump");
    }
}