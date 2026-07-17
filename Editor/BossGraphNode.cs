using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace TitanTool.Editor {
    public abstract class NodeFlow {
    }

    [Serializable]
    public abstract class BossGraphNode : Unity.GraphToolkit.Editor.Node {
        [SerializeField] private string m_runtimeTypeName;
        [SerializeField] private string m_runtimeGuid;
        protected const string EXECUTION_PORT_DEFAULT_NAME = "ExecutionPort";
        protected const string EXECUTION_PORT_IN = "In";
        protected const string EXECUTION_PORT_OUT = "Out";
        public string runtimeGuid => m_runtimeGuid;
        public string runtimeTypeName => m_runtimeTypeName;
        protected virtual int outputCount => 0;
        public GraphNodeRegistration registration => NodeTypeRegistry.GetRegistrationForEditor(GetType());
        public string displayName => registration?.displayName ?? GetType().Name;
        public BossGraphNodeCategory category => registration?.category ?? BossGraphNodeCategory.Utility;
        public Color categoryColor => registration?.color ?? BossGraphNodeCategoryColors.GetColor(category);
        public string icon => registration?.icon ?? BossGraphNodeIcons.GetDefaultIcon(category);
        public string tooltip => registration?.tooltip ?? displayName;
        public string metadataTooltip => string.IsNullOrWhiteSpace(behaviorBadge)
            ? tooltip
            : $"{tooltip}\n\nBadge: {behaviorBadge}";
        public virtual int minimumChildCount => 1;
        protected virtual string behaviorBadge => null;

        [SerializeField] private List<string> m_childGuids = new();
        public IReadOnlyList<string> childGuids => m_childGuids;

        public void Initialize(Type runtime) {
            m_runtimeTypeName = runtime.AssemblyQualifiedName;
            m_runtimeGuid = Guid.NewGuid().ToString();
            BossGraphNodeMetadataUtility.Apply(this, icon, metadataTooltip);
            //title = runtime.Name;
        }

        public void InitializeNode(Type runtimeType) {
            BossGraphNodeMetadataUtility.Apply(this, icon, metadataTooltip);

            if (!string.IsNullOrEmpty(m_runtimeGuid))
                return;

            m_runtimeGuid = Guid.NewGuid().ToString();
            m_runtimeTypeName = runtimeType.AssemblyQualifiedName;
        }

        public void RegenerateRuntimeGuid() {
            m_runtimeGuid = Guid.NewGuid().ToString();
        }


        //public void Bind(string guid) => m_runtimeGuid = guid;
        public BossGraphValidationIssue[] GetValidationIssues() {
            List<BossGraphValidationIssue> issues = new();

            if (string.IsNullOrEmpty(runtimeGuid)) {
                issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, $"{GetType().Name} is missing a runtime GUID.", this));
            } else if (!Guid.TryParse(runtimeGuid, out _)) {
                issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, $"{GetType().Name} has an invalid runtime GUID: {runtimeGuid}", this));
            }

            if (NodeTypeRegistry.GetRuntime(GetType()) == null)
                issues.Add(new BossGraphValidationIssue(BossGraphValidationSeverity.Error, $"No runtime type is registered for {GetType().Name}.", this));

            if (this is IGraphNodeValidator validator)
                validator.Validate(new BossGraphNodeValidationContext(this, issues));

            return issues.ToArray();
        }

        public Type GetRuntimeType() {
            if (string.IsNullOrEmpty(m_runtimeTypeName)) {
                Debug.LogError("Runtime type name missing.");
                return null;
            }

            Type type = Type.GetType(m_runtimeTypeName);
            if (type == null) {
                Debug.LogError($"Failed to resolve runtime type:\n{m_runtimeTypeName}");
            }

            return type;
        }

        protected IReadOnlyList<string> AddInputOutputExecutionPorts(IPortDefinitionContext context) {
            if (hasInput) {
                IPort inputPort = context.AddInputPort<NodeFlow>(EXECUTION_PORT_IN)
                    .WithDisplayName(string.Empty)
                    .WithConnectorUI(PortConnectorUI.Arrowhead)
                    .Build();
                SetSingleConnectionCapacity(inputPort);
            }

            IReadOnlyList<string> outputPortNames = GetExecutionOutputPortNames();
            if (hasOutput) {
                foreach (string portName in outputPortNames) {
                    IPort outputPort = context.AddOutputPort<NodeFlow>(portName)
                        .WithDisplayName(string.Empty)
                        .WithConnectorUI(PortConnectorUI.Arrowhead)
                        .Build();
                    SetSingleConnectionCapacity(outputPort);
                }
            }

            return outputPortNames;
        }

        private static void SetSingleConnectionCapacity(IPort port) {
            if (port == null)
                return;

            PropertyInfo capacityProperty = port.GetType().GetProperty("Capacity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (capacityProperty?.CanWrite != true)
                return;

            Type capacityType = capacityProperty.PropertyType;
            if (!capacityType.IsEnum)
                return;

            try {
                object singleCapacity = Enum.Parse(capacityType, "Single");
                capacityProperty.SetValue(port, singleCapacity);
            }
            catch {
                // Graph Toolkit keeps port capacity internal; if it changes, fall back to validation cleanup.
            }
        }

        protected IReadOnlyList<string> GetExecutionOutputPortNames() {
            if (!hasOutput || outputCount <= 0)
                return Array.Empty<string>();

            return Enumerable.Range(0, outputCount)
                .Select(index => $"{EXECUTION_PORT_OUT}{index}")
                .ToArray();
        }

        protected static int GetExecutionOutputPortIndex(string portName) {
            return IsExecutionOutputPortName(portName)
                ? GetExecutionOutputPortIndexUnchecked(portName)
                : -1;
        }

        private static bool IsExecutionOutputPortName(string portName) {
            return portName != null &&
                   portName.StartsWith(EXECUTION_PORT_OUT, StringComparison.Ordinal) &&
                   GetExecutionOutputPortIndexUnchecked(portName) >= 0;
        }

        private static int GetExecutionOutputPortIndexUnchecked(string portName) {
            return int.TryParse(portName[EXECUTION_PORT_OUT.Length..], out int index)
                ? index
                : -1;
        }

        protected virtual bool hasInput => true;
        protected virtual bool hasOutput => false;
    }

    public static class BossGraphNodeMetadataUtility {
        private static readonly FieldInfo s_implementationField = typeof(Unity.GraphToolkit.Editor.Node)
            .GetField("m_Implementation", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Apply(BossGraphNode node, string icon, string tooltip) {
            Apply((Unity.GraphToolkit.Editor.Node)node, icon, tooltip);
        }

        public static void ApplyTooltip(Unity.GraphToolkit.Editor.Node node, string tooltip) {
            Apply(node, null, tooltip);
        }

        private static void Apply(Unity.GraphToolkit.Editor.Node node, string icon, string tooltip) {
            object implementation = s_implementationField?.GetValue(node);
            PropertyInfo iconProperty = implementation?.GetType().GetProperty("IconTypeString");
            if (!string.IsNullOrWhiteSpace(icon) && iconProperty?.CanWrite == true) {
                iconProperty.SetValue(implementation, icon);
            }

            PropertyInfo tooltipProperty = implementation?.GetType().GetProperty("Tooltip");
            if (!string.IsNullOrWhiteSpace(tooltip) && tooltipProperty?.CanWrite == true) {
                tooltipProperty.SetValue(implementation, tooltip);
            }
        }
    }
}
