using System;
using System.Collections.Generic;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Base {
    public abstract class Node : ScriptableObject {
        [SerializeField] private string m_guid = Guid.NewGuid().ToString();
        public string guid => m_guid;
        public void SetGuid(string newGuid) => m_guid = newGuid;

        [SerializeField] public List<Node> children = new();
        public abstract NodeStatus Tick(NodeContext ctx);
        public virtual void Abort(NodeContext ctx) {
        }

        [SerializeField] private Vector2 m_position;
        [SerializeField] private string m_displayName;
        [SerializeField] private string m_category;
        [SerializeField] private string m_comment;
        [SerializeField] private Color m_color = Color.white;

        public Vector2 position => m_position;
        public string displayName => string.IsNullOrEmpty(m_displayName) ? name : m_displayName;
        public string category => m_category;
        public string comment => m_comment;
        public Color color => m_color;

        public void SetViewMetadata(string displayName, string category, Color color) {
            m_displayName = displayName;
            m_category = category;
            m_color = color;
        }
    }

    public enum AbortType {
        None,
        Self,
        LowerPriority,
        Both
    }

    public enum NodeStatus {
        Success,
        Failure,
        Running
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class NodeViewAttribute : Attribute {
        public string name { get; }
        public string menuPath { get; }

        public NodeViewAttribute(string name, string menuPath = "Nodes/") {
            this.name = name;
            this.menuPath = menuPath;
        }
    }
}
