using System.Collections.Generic;
using TitanTool.Runtime.Nodes.Base;
using UnityEngine;

namespace TitanTool.Runtime {
    public static class SpawnPointResolver {
        public static bool TryResolveByIndex(NodeContext ctx, int index, out Vector2 position) {
            position = default;

            if (!ctx.blackboard.TryGet(BKeys.SpawnPoints, out List<Transform> spawnPoints) ||
                index < 0 ||
                index >= spawnPoints.Count ||
                spawnPoints[index] == null) {
                return false;
            }

            position = spawnPoints[index].position;
            return true;
        }

        public static bool TryResolveByKey(NodeContext ctx, TargetPointKey key, out Vector2 position) {
            position = default;

            if (key == null ||
                !ctx.blackboard.TryGet(BKeys.SpawnPointKeyMap, out Dictionary<TargetPointKey, Transform> spawnPoints) ||
                !spawnPoints.TryGetValue(key, out Transform spawnPoint) ||
                spawnPoint == null) {
                return false;
            }

            position = spawnPoint.position;
            return true;
        }
    }
}
