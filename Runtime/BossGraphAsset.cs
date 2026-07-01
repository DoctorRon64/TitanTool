using System.Collections.Generic;
using System.Linq;
using TitanTool.Runtime.Nodes.Base;
using UnityEngine;
using UnityEngine.Serialization;
using Utility;

namespace TitanTool.Runtime.Data {
    public class BossGraphAsset : DataAsset {
        [FormerlySerializedAs("<root>k__BackingField")]
        [SerializeField] private StartNode m_root;
        [FormerlySerializedAs("<nodes>k__BackingField")]
        [SerializeField] private List<Node> m_nodes = new();

        public StartNode root => m_root;
        public IReadOnlyList<Node> nodes => m_nodes;
        
        private Dictionary<string, Node> m_guidLookup;

        public void SetNodes(IEnumerable<Node> nodes) {
            m_nodes = nodes?.Where(node => node != null).ToList() ?? new List<Node>();
            BuildLookup();
            EnsureValid();
        }

        public void BuildLookup() {
            m_guidLookup = m_nodes.ToDictionary(n => n.guid);
        }

        public Node GetNode(string guid) {
            if (m_guidLookup == null)
                BuildLookup();

            return m_guidLookup.GetValueOrDefault(guid);
        }
        
        public void EnsureValid() {
            m_root = m_nodes.OfType<StartNode>().FirstOrDefault();
        }
    }
}
