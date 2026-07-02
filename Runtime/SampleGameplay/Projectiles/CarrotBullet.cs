using UnityEngine;
using Utility;

namespace Game {
    [RequireComponent(typeof(Bullet))]
    public class CarrotBullet : MonoBehaviour, IDamagable {
        [SerializeField] private int m_maxHealth = 3;
        [SerializeField] private DamagableTeam m_team = DamagableTeam.Opponent;

        private Bullet m_bullet;

        public DamagableTeam team => m_team;
        public int health { get; set; }

        private void Awake() {
            m_bullet = GetComponent<Bullet>();
        }

        private void OnEnable() {
            health = m_maxHealth;
        }

        public void TakeDamage(int damageAmount) {
            if (m_bullet.isDestroying)
                return;

            health -= damageAmount;
            if (health <= 0)
                m_bullet.BreakBullet();
        }

        public void SetOwnerTeam(DamagableTeam team) {
            m_team = team;
        }
    }
}
