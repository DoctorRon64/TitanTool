using TitanTool.Runtime.Nodes.Base;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Custom {
    public abstract class TitanToolScriptableNode : ScriptableObject {
        public abstract NodeStatus Tick(Node runtimeNode, NodeContext ctx);

        public virtual void Abort(Node runtimeNode, NodeContext ctx) {
        }
    }
}
