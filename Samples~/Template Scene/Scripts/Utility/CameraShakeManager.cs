using Unity.Cinemachine;
using UnityEngine;

namespace Utility {
    public class CameraShakeManager : Singleton<CameraShakeManager>, ISingleton {
        public void Shake(CinemachineImpulseSource _impulse, float _force) {
            if (_impulse == null) return;

            _impulse.GenerateImpulse(_force);
        }

        public void OnInitialize() {
        }

        public void OnDestroy() {
        }
    }
}