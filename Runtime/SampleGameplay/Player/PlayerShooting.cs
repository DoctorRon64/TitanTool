using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Utility;

namespace Game.Player {
    public class PlayerShooting : MonoBehaviour {
        [Header("Shooting")]
        [SerializeField] private GameObject m_bulletPrefab;
        [SerializeField] private Transform m_shootingPoint;
        [SerializeField] private float m_bulletSpawnDistance = 1.0f;
        [SerializeField] private float m_bulletSpeed = 10f;
        [SerializeField] private float m_fireRate = 0.2f;
        [SerializeField] private int m_bulletAmount = 10;
        [SerializeField] private DamagableTeam m_damagableTeam;

        [Header("Audio")]
        [SerializeField] private AudioAsset m_fireSound;

        private bool m_isShooting;
        private float m_nextFireTime;
        private ObjectPool<Bullet> m_bulletPool;
        private Action<InputAction.CallbackContext> m_shootCallback;
        private Camera m_cam;
        
        private void Awake() {
            m_cam = Camera.main;
            m_shootCallback = _ => SetIsShooting(true);

            if (m_bulletPrefab == null || !m_bulletPrefab.TryGetComponent(out Bullet bulletPrefab)) {
                Debug.LogError("PlayerShooting requires a bullet prefab with a Bullet component.", this);
                enabled = false;
                return;
            }

            if (m_shootingPoint == null) {
                Debug.LogError("PlayerShooting requires a shooting point.", this);
                enabled = false;
                return;
            }

            if (m_cam == null) {
                Debug.LogError("PlayerShooting requires a camera tagged MainCamera.", this);
                enabled = false;
                return;
            }

            m_shootingPoint.localPosition = new Vector3(m_bulletSpawnDistance, 0f, 0f);
            m_bulletPool = new ObjectPool<Bullet>(bulletPrefab);
            for (int i = 0; i < m_bulletAmount; i++) {
                Bullet bullet = (Bullet)m_bulletPool.AddNewItemToPool();
                if (bullet != null)
                    bullet.SetupBullet(m_bulletPool);
            }
        }

        private void OnEnable() {
            if (m_shootCallback != null)
                InputManager.instance.onShootPerformed.AddListener(m_shootCallback);
        }

        private void OnDisable() {
            if (m_shootCallback != null)
                InputManager.instance.onShootPerformed.RemoveListener(m_shootCallback);
        }

        private void Update() {
            if (m_isShooting && Time.time > m_nextFireTime) {
                ShootBullet();
            }
        }

        private void ShootBullet() {
            m_nextFireTime = Time.time + m_fireRate;
            m_isShooting = false;

            Vector3 mousePosition = m_cam.ScreenToWorldPoint(InputManager.instance.GetMousePosition());
            mousePosition.z = transform.position.z;
            Vector2 direction = (mousePosition - transform.position).normalized;
            Vector3 spawnPosition = transform.position + (Vector3)direction * m_bulletSpawnDistance;

            Bullet bullet = m_bulletPool.RequestObject(spawnPosition) as Bullet;
            if (bullet == null) return;

            bullet.SetOwnerTeam(m_damagableTeam);
            bullet.SetDirection(direction, m_bulletSpeed);
            bullet.SetRotation(direction);
            Game.AudioManager.instance.Play(m_fireSound);
        }

        private void SetIsShooting(bool _shooting) {
            m_isShooting = _shooting;
        }
    }
}
