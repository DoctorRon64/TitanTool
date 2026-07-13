using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TitanTool.Editor.Nodes;
using RuntimeStartNode = TitanTool.Runtime.Nodes.Base.StartNode;
using UnityEngine;

namespace TitanTool.Editor {
    public sealed class GraphNodeRegistration {
        public GraphNodeRegistration(Type editorType, GraphNodeAttribute attribute) {
            this.editorType = editorType;
            runtimeType = attribute.RuntimeType;
            displayName = attribute.DisplayName;
            menuPath = attribute.MenuPath;
            category = attribute.Category;
            icon = attribute.Icon;
            tooltip = attribute.Tooltip;
            color = BossGraphNodeCategoryColors.GetColor(category);
        }

        public Type editorType { get; }
        public Type runtimeType { get; }
        public string displayName { get; }
        public string menuPath { get; }
        public BossGraphNodeCategory category { get; }
        public string icon { get; }
        public string tooltip { get; }
        public Color color { get; }
    }

    public static class NodeTypeRegistry {
        private static Dictionary<Type, GraphNodeRegistration> s_editorRegistrations;
        private static Dictionary<Type, GraphNodeRegistration> s_runtimeRegistrations;
        private static readonly HashSet<Type> s_hiddenEditorTypes = new() {
            typeof(StartNode)
        };

        static NodeTypeRegistry() {
            Build();
        }

        static void Build() {
            s_editorRegistrations = new();
            s_runtimeRegistrations = new();

            IEnumerable<Type> editorTypes = Assembly
                .GetAssembly(typeof(BossGraphNode))
                .GetTypes()
                .Where(t => !t.IsAbstract && typeof(BossGraphNode).IsAssignableFrom(t));

            foreach (Type editorType in editorTypes) {
                GraphNodeAttribute attr;
                try {
                    attr = editorType.GetCustomAttribute<GraphNodeAttribute>();
                }
                catch (Exception ex) {
                    Debug.LogError($"Failed to read GraphNodeAttribute on {editorType.Name}: {ex.Message}");
                    continue;
                }

                if (attr == null)
                    continue;

                GraphNodeRegistration registration = new(editorType, attr);

                if (s_runtimeRegistrations.TryGetValue(attr.RuntimeType, out GraphNodeRegistration existing)) {
                    Debug.LogWarning($"Runtime node {attr.RuntimeType.Name} is already registered to {existing.editorType.Name}; ignoring {editorType.Name}.");
                    continue;
                }

                s_editorRegistrations[editorType] = registration;
                s_runtimeRegistrations[attr.RuntimeType] = registration;
            }

            RegisterInternalNode(
                typeof(StartNode),
                new GraphNodeAttribute(
                    typeof(RuntimeStartNode),
                    "Start",
                    "Flow/",
                    BossGraphNodeCategory.Flow,
                    tooltip: "Entry point for the boss graph."));
        }

        private static void RegisterInternalNode(Type editorType, GraphNodeAttribute attr) {
            GraphNodeRegistration registration = new(editorType, attr);
            s_editorRegistrations[editorType] = registration;
            s_runtimeRegistrations[attr.RuntimeType] = registration;
        }

        public static Type GetRuntime(Type editorType) {
            return s_editorRegistrations.TryGetValue(editorType, out GraphNodeRegistration registration)
                ? registration.runtimeType
                : null;
        }

        public static Type GetEditor(Type runtimeType) {
            return s_runtimeRegistrations.TryGetValue(runtimeType, out GraphNodeRegistration registration)
                ? registration.editorType
                : null;
        }

        public static GraphNodeRegistration GetRegistrationForEditor(Type editorType) {
            return s_editorRegistrations.GetValueOrDefault(editorType);
        }

        public static GraphNodeRegistration GetRegistrationForRuntime(Type runtimeType) {
            return s_runtimeRegistrations.GetValueOrDefault(runtimeType);
        }

        public static IReadOnlyList<GraphNodeRegistration> GetRegistrations() {
            return s_editorRegistrations.Values
                .Where(registration => !s_hiddenEditorTypes.Contains(registration.editorType))
                .OrderBy(registration => registration.category)
                .ThenBy(registration => registration.displayName)
                .ToList();
        }
    }
}
