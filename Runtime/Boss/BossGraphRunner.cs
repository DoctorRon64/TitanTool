using UnityEngine;
using TitanTool.Runtime.Data;
using TitanTool.Runtime.Nodes.Base;

namespace TitanTool.Runtime {
    public class BossGraphRunner {
        public BossGraphAsset graph { get; private set; }
        public NodeContext context { get; private set; }

        public BossGraphRunner(BossGraphAsset graph) {
            this.graph = graph;
            context = new NodeContext();
        }

        public void Tick(float deltaTime) {
            if (graph == null || graph.root == null)
                return;

            context.deltaTime = deltaTime;
            context.BeginFrame();
            context.ExecuteNode(graph.root);
        }

        public void Reset() {
            context.ResetAll();
        }
    }
}