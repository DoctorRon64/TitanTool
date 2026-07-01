namespace TitanTool.Runtime.Nodes.Base {
    public class StartNode : Node {
        public override NodeStatus Tick(NodeContext ctx) {
            if (children.Count == 0) {
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            if (children[0] == null) {
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            NodeStatus result = ctx.ExecuteNode(children[0]);
            ctx.SetStatus(this, result);
            return result;
        }
    }
}
