using System.Collections.Generic;
using UnityEngine;

namespace TitanTool.Runtime {
    public class TargetPointProvider : MonoBehaviour {
        [SerializeField] private TargetPoint[] m_points;

        public IReadOnlyList<TargetPoint> points => m_points;

        public IEnumerable<TargetPoint> GetPoints() {
            bool hasManualPoints = false;
            if (m_points != null) {
                foreach (TargetPoint point in m_points) {
                    if (point == null)
                        continue;

                    hasManualPoints = true;
                    yield return point;
                }
            }

            if (hasManualPoints)
                yield break;

            foreach (TargetPoint point in GetComponentsInChildren<TargetPoint>(true)) {
                if (point != null)
                    yield return point;
            }
        }

        [ContextMenu("Collect Child Target Points")]
        private void CollectChildTargetPoints() {
            m_points = GetComponentsInChildren<TargetPoint>(true);
        }
    }
}
