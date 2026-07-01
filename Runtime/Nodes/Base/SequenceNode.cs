using UnityEngine;

namespace TitanTool.Runtime.Nodes.Base {
    public class SequenceState  {
        public int currentIndex;
    }

    [NodeView("Run In Order", "Composite/")]
    public class SequenceNode : Node {
        public override NodeStatus Tick(NodeContext ctx) {
            SequenceState state = ctx.GetState<SequenceState>(this);

            while (state.currentIndex < children.Count) {
                Node child = children[state.currentIndex];
                if (child == null) {
                    state.currentIndex = 0;
                    ctx.SetStatus(this, NodeStatus.Failure);
                    return NodeStatus.Failure;
                }

                NodeStatus status = ctx.ExecuteNode(child);

                switch (status) {
                    case NodeStatus.Running:
                        ctx.SetStatus(this, NodeStatus.Running);
                        return NodeStatus.Running;

                    case NodeStatus.Failure:
                        foreach (Node c in children) {
                            ctx.ResetBranch(c);
                        }
                        
                        state.currentIndex = 0;
                        ctx.SetStatus(this, NodeStatus.Failure);
                        return NodeStatus.Failure;

                    case NodeStatus.Success:
                        ctx.ResetBranch(child);
                        state.currentIndex++;
                        break;
                }
            }

            foreach (Node c in children) {
                ctx.ResetBranch(c);
            }
            
            state.currentIndex = 0;
            ctx.SetStatus(this, NodeStatus.Success);
            return NodeStatus.Success;
        }
    }
}
