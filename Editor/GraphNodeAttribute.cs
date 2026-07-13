using System;
using TitanTool.Runtime.Nodes.Base;
using UnityEngine;

namespace TitanTool.Editor {
    public enum BossGraphNodeCategory {
        Flow,
        Composite,
        Action,
        Condition,
        Decorator,
        Debug,
        Utility
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class GraphNodeAttribute : Attribute {
        public GraphNodeAttribute(
            Type runtimeType,
            string displayName = null,
            string menuPath = null,
            BossGraphNodeCategory category = BossGraphNodeCategory.Utility,
            string icon = null,
            string tooltip = null,
            string searchKeywords = null
        ) {
            if (runtimeType == null)
                throw new ArgumentNullException(nameof(runtimeType));

            if (!typeof(Node).IsAssignableFrom(runtimeType))
                throw new ArgumentException($"{runtimeType.Name} must inherit from {nameof(Node)}.", nameof(runtimeType));

            RuntimeType = runtimeType;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? runtimeType.Name : displayName;
            MenuPath = string.IsNullOrWhiteSpace(menuPath) ? "Nodes/" : menuPath;
            Category = category;
            Icon = string.IsNullOrWhiteSpace(icon) ? BossGraphNodeIcons.GetDefaultIcon(category) : icon;
            Tooltip = string.IsNullOrWhiteSpace(tooltip) ? DisplayName : tooltip;
            SearchKeywords = searchKeywords ?? string.Empty;
        }

        public Type RuntimeType { get; }
        public string DisplayName { get; }
        public string MenuPath { get; }
        public BossGraphNodeCategory Category { get; }
        public string Icon { get; }
        public string Tooltip { get; }
        public string SearchKeywords { get; }

        public Type runtimeType => RuntimeType;
    }

    public static class BossGraphNodeIcons {
        public const string Flow = "source-node";
        public const string Composite = "combine-node";
        public const string Action = "set-node";
        public const string Condition = "logic-node";
        public const string Decorator = "mixer-node";
        public const string Debug = "graph-object";
        public const string Utility = "node";
        public const string Movement = "source-node";
        public const string Shoot = "split-node";
        public const string Spawn = "get-node";
        public const string Wait = "mixer-node";
        public const string Reroute = "combine-node";

        public static string GetDefaultIcon(BossGraphNodeCategory category) {
            return category switch {
                BossGraphNodeCategory.Flow => Flow,
                BossGraphNodeCategory.Composite => Composite,
                BossGraphNodeCategory.Action => Action,
                BossGraphNodeCategory.Condition => Condition,
                BossGraphNodeCategory.Decorator => Decorator,
                BossGraphNodeCategory.Debug => Debug,
                _ => Utility
            };
        }
    }

    public static class BossGraphNodeCategoryColors {
        public static Color GetColor(BossGraphNodeCategory category) {
            return category switch {
                BossGraphNodeCategory.Flow => new Color(0.25f, 0.55f, 0.95f),
                BossGraphNodeCategory.Composite => new Color(0.45f, 0.50f, 0.56f),
                BossGraphNodeCategory.Action => new Color(0.22f, 0.68f, 0.42f),
                BossGraphNodeCategory.Condition => new Color(0.95f, 0.72f, 0.22f),
                BossGraphNodeCategory.Decorator => new Color(0.62f, 0.42f, 0.90f),
                BossGraphNodeCategory.Debug => new Color(0.20f, 0.72f, 0.88f),
                _ => new Color(0.72f, 0.72f, 0.72f)
            };
        }
    }
}
