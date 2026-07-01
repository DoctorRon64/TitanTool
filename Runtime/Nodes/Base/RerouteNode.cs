namespace TitanTool.Runtime.Nodes.Base {
    [NodeView("Reroute", "Utility/")]
    public class RerouteNode : ActionNode {
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
