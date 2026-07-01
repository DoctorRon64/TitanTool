using TitanTool.Runtime.Nodes.Base;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Custom {
    [NodeView("Abort", "Action/")]
    public class AbortNode : ActionNode {
        public override NodeStatus Tick(NodeContext ctx) {
            int abortedCount = ctx.AbortRunningNodes(this);

            if (ctx.debugLogging) {
                Debug.Log($"{name}: aborted {abortedCount} running node(s).");
            }

            ctx.SetStatus(this, NodeStatus.Success);
            return NodeStatus.Success;
        }
    }
}
