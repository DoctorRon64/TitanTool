using UnityEngine;

namespace TitanTool.Runtime.Nodes.Base {
    public class ActionNode : Node {
        [SerializeField] protected AbortType m_abortType;
        public Node child => children.Count > 0 ? children[0] : null;
        
        public override NodeStatus Tick(NodeContext ctx) {
            if (child == null) {
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            NodeStatus result = ctx.ExecuteNode(child);
            ctx.SetStatus(this, result);
            return result;
        }
    }
}
