using System;
using TitanTool.Runtime.Nodes.Base;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Custom {
    [NodeView("Custom Scriptable Node", "Action/Custom/")]
    public class ScriptableActionNode : Node {
        [SerializeField] private TitanToolScriptableNode m_nodeAsset;

        public void SetNodeAsset(TitanToolScriptableNode nodeAsset) => m_nodeAsset = nodeAsset;

        public override NodeStatus Tick(NodeContext ctx) {
            if (m_nodeAsset == null) {
                ctx.SetStatusReason(this, "Missing custom scriptable node asset.");
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }

            try {
                NodeStatus status = m_nodeAsset.Tick(this, ctx);
                ctx.SetStatusReason(this, $"{m_nodeAsset.name} returned {status}.");
                ctx.SetStatus(this, status);
                return status;
            }
            catch (Exception ex) {
                Debug.LogException(ex, m_nodeAsset);
                ctx.SetStatusReason(this, $"{m_nodeAsset.name} threw {ex.GetType().Name}.");
                ctx.SetStatus(this, NodeStatus.Failure);
                return NodeStatus.Failure;
            }
        }

        public override void Abort(NodeContext ctx) {
            if (m_nodeAsset == null)
                return;

            try {
                m_nodeAsset.Abort(this, ctx);
            }
            catch (Exception ex) {
                Debug.LogException(ex, m_nodeAsset);
            }
        }
    }
}
