using System;
using System.Linq;
using System.Reflection;
using TitanTool.Runtime.Nodes.Base;
using UnityEngine;

namespace TitanTool.Editor {
    public static class NodeFactory {
        public static Type[] GetAllNodeTypes() {
            return Assembly.GetAssembly(typeof(Node))
                .GetTypes()
                .Where(t => t.IsSubclassOf(typeof(Node)) && !t.IsAbstract)
                .ToArray();
        }

        public static string GetMenuPath(Type nodeType) {
            GraphNodeRegistration registration = NodeTypeRegistry.GetRegistrationForRuntime(nodeType);
            if (registration != null)
                return registration.menuPath + registration.displayName;

            NodeViewAttribute attr = nodeType.GetCustomAttribute<NodeViewAttribute>();
            return attr != null ? attr.menuPath + attr.name : "Nodes/" + nodeType.Name;
        }

        /*public static BossGraphNode CreateGraphNode(Type runtimeType) {
            Node node = ScriptableObject.CreateInstance(runtimeType) as Node;
            node.name = runtimeType.Name;

            /*if (node is BossGraphNode bossNode) {
                bossNode.runtimeGuid = Guid.NewGuid().ToString();
            }#1#

            return node;
        }*/
    }
}