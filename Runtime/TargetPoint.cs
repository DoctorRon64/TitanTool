using UnityEngine;

namespace TitanTool.Runtime {
    public class TargetPoint : MonoBehaviour {
        [SerializeField] private TargetPointKey m_key;
        public TargetPointKey key => m_key;
    }
}
