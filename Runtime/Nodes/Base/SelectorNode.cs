namespace TitanTool.Runtime.Nodes.Base {
    public class SelectorState {
        public int currentIndex;
    }

    [NodeView("Try Children", "Composite/")]
    public class SelectorNode : Node {
        public override NodeStatus Tick(NodeContext ctx) {
            SelectorState state = ctx.GetState<SelectorState>(this);

            while (state.currentIndex < children.Count) {
                Node child = children[state.currentIndex];
                if (child == null) {
                    state.currentIndex++;
                    continue;
                }

                NodeStatus result = ctx.ExecuteNode(child);

                switch (result) {
                    case NodeStatus.Running:
                        ctx.SetStatus(this, NodeStatus.Running);
                        return NodeStatus.Running;

                    case NodeStatus.Success:
                        ctx.ResetBranch(child);
                        state.currentIndex = 0;

                        ctx.SetStatus(this, NodeStatus.Success);
                        return NodeStatus.Success;

                    case NodeStatus.Failure:
                        ctx.ResetBranch(child);
                        state.currentIndex++;
                        break;
                }
            }

            foreach (Node c in children) {
                ctx.ResetBranch(c);
            }

            state.currentIndex = 0;

            ctx.SetStatus(this, NodeStatus.Failure);
            return NodeStatus.Failure;
        }
    }
}
