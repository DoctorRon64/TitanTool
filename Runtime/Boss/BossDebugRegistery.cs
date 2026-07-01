using System.Collections.Generic;
using TitanTool.Runtime.Data;

namespace TitanTool.Runtime {
    public static class BossDebugRegistry {
        private static readonly Dictionary<BossGraphAsset, HashSet<BossDirector>> map = new();

        public static void Register(BossGraphAsset graph, BossDirector bossDirector) {
            if (!map.TryGetValue(graph, out HashSet<BossDirector> set)) {
                map[graph] = set = new HashSet<BossDirector>();
            }

            set.Add(bossDirector);
        }

        public static HashSet<BossDirector> Get(BossGraphAsset graph) => map.GetValueOrDefault(graph);

        public static void Unregister(BossGraphAsset graph, BossDirector bossDirector) {
            if (!map.TryGetValue(graph, out HashSet<BossDirector> set)) return;
            set.Remove(bossDirector);
            if (set.Count == 0) {
                map.Remove(graph);
            }
        }
    }
}