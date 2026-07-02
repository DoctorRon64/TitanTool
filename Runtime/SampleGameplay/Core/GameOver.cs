using UnityEngine;
using UnityEngine.Serialization;
using Utility;

public class GameOver : MonoBehaviour {
    [FormerlySerializedAs("onBossDied")]
    [SerializeField] private MonoSignalAsset m_onBossDied;
    [FormerlySerializedAs("onPlayerDied")]
    [SerializeField] private MonoSignalAsset m_onPlayerDied;
    [FormerlySerializedAs("boss")]
    [SerializeField] private TitanTool.Runtime.BossDirector m_boss;

    private void OnEnable() {
        m_onBossDied?.AddListener(BossDied);
        m_onPlayerDied?.AddListener(PlayerDied);
    }

    private void OnDisable() {
        m_onBossDied?.RemoveListener(BossDied);
        m_onPlayerDied?.RemoveListener(PlayerDied);
    }

    private void BossDied() {
        EndGame(true);
    }

    private void PlayerDied() {
        EndGame(false);
    }

    private void EndGame(bool bossDied) {
        if (m_boss != null) {
            m_boss.paused = true;
        }

        if (Game.UIManager.isInitialized) {
            Game.UIManager.instance.ShowGameOver(bossDied);
        }
    }
}
