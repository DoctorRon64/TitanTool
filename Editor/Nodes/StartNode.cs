using System;
using TitanTool.Runtime.Nodes.Base;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    internal class StartNode : BossGraphNode {
        protected override bool hasInput => false;
        protected override int outputCount => 1;
        protected override bool hasOutput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Base.StartNode));
            StartNodeLockUtility.Apply(this);
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);
        }
    }

    internal static class StartNodeLockUtility {
        private const System.Reflection.BindingFlags FLAGS =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static;

        public static void Apply(Unity.GraphToolkit.Editor.Node node) {
            object implementation = typeof(Unity.GraphToolkit.Editor.Node)
                .GetField("m_Implementation", FLAGS)
                ?.GetValue(node);

            if (implementation == null)
                return;

            SetCapability(implementation, "Deletable", false);
            SetCapability(implementation, "Copiable", false);
        }

        private static void SetCapability(object model, string capabilityName, bool active) {
            Type capabilityType = FindType("Unity.GraphToolkit.Editor.Capabilities");
            object capability = capabilityType
                ?.GetField(capabilityName, FLAGS)
                ?.GetValue(null);

            if (capability == null)
                return;

            model.GetType()
                .GetMethod("SetCapability", FLAGS)
                ?.Invoke(model, new[] { capability, active });
        }

        private static Type FindType(string fullName) {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                Type type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
