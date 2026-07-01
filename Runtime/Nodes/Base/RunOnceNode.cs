using UnityEngine;

namespace TitanTool.Runtime.Nodes.Base {
    public enum RunOnceCompletedStatus {
        Failure,
        Success
    }

    [NodeView("Run Once", "Decorator/")]
    public class RunOnceNode : ActionNode {
        [SerializeField] private RunOnceCompletedStatus m_completedStatus = RunOnceCompletedStatus.Failure;

        public void SetCompletedStatus(RunOnceCompletedStatus completedStatus) => m_completedStatus = completedStatus;

        public override NodeStatus Tick(NodeContext ctx) {
            if (child == null) {
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            string hasRunKey = $"TitanTool.RunOnce.{guid}";
            if (ctx.blackboard.TryGetValue(hasRunKey, out bool hasRun) && hasRun) {
                NodeStatus completedStatus = m_completedStatus == RunOnceCompletedStatus.Success
                    ? NodeStatus.Success
                    : NodeStatus.Failure;

                ctx.SetStatus(this, completedStatus);
                return completedStatus;
            }

            NodeStatus result = ctx.ExecuteNode(child);
            if (result == NodeStatus.Success) {
                ctx.blackboard.SetValue(hasRunKey, true);
                ctx.ResetBranch(child);
            }

            ctx.SetStatus(this, result);
            return result;
        }
    }
}
