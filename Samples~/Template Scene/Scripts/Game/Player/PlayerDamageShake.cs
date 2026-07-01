using System;
using Unity.Cinemachine;
using UnityEngine;
using Utility;

namespace Game.Player {
    public class PlayerDamageShake : MonoBehaviour {
        [SerializeField] private CinemachineImpulseSource m_impulseSource;
        [SerializeField] private float m_shakeForce = 3f;
        [SerializeField] private DoubleIntSignalAsset m_onHealthChanged;
        [SerializeField] private AudioAsset m_hurtSound;
        
        private void OnEnable() {
            m_onHealthChanged.AddListener(DamageShake);
        }

        private void OnDisable() {
            m_onHealthChanged.RemoveListener(DamageShake);
        }

        private void DamageShake(int _maxHealth, int _newHealth) {
            Utility.CameraShakeManager.instance.Shake(m_impulseSource,m_shakeForce);
            Game.AudioManager.instance.Play(m_hurtSound);
        }
    }
}