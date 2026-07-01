using TitanTool.Runtime.Nodes.Base;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Custom {
    [NodeView("Set Sprite", "Action/")]
    public class SwapSpriteNode : Node {
        [SerializeField] private Sprite m_sprite;

        public void SetSprite(Sprite sprite) => m_sprite = sprite;

        public override NodeStatus Tick(NodeContext ctx) {
            if (m_sprite == null) {
                Debug.LogError($"{name}: Swap Sprite requires a sprite.");
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            if (!TryResolveSpriteRenderer(ctx, out SpriteRenderer spriteRenderer)) {
                Debug.LogError($"{name}: Swap Sprite requires a SpriteRenderer on the boss.");
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            spriteRenderer.sprite = m_sprite;
            ctx.SetStatus(this, NodeStatus.Success);
            return NodeStatus.Success;
        }

        private static bool TryResolveSpriteRenderer(NodeContext ctx, out SpriteRenderer spriteRenderer) {
            if (ctx.blackboard.TryGet(BKeys.BossSpriteRenderer, out spriteRenderer) && spriteRenderer != null)
                return true;

            spriteRenderer = null;
            if (ctx.blackboard.TryGet(BKeys.BossTransform, out Transform boss) && boss != null) {
                spriteRenderer = boss.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                    return true;

                spriteRenderer = boss.GetComponentInChildren<SpriteRenderer>();
                return spriteRenderer != null;
            }

            return false;
        }
    }
}
