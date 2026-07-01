using UnityEngine;
using UnityEngine.UIElements;
using Utility;

namespace Game {
    public class UIManager : MonoSingleton<UIManager> {
        private const string m_bossHealthBarName = "BossHealth";
        private const string m_bossHealthTextName = "BossHealthText";
        private const string m_playerHealthBarName = "PlayerHealth";
        private const string m_playerHealthTextName = "PlayerHealthText";
        private const string m_gameOverOverlayName = "GameOverOverlay";
        private const string m_gameOverBadgeName = "GameOverBadge";
        private const string m_gameOverTitleName = "GameOverTitle";
        private const string m_gameOverMessageName = "GameOverMessage";
        private const string m_gameOverHintName = "GameOverHint";
        private const string m_healthWarningClass = "health-warning";
        private const string m_healthDangerClass = "health-danger";
        private const string m_hiddenClass = "hidden";
        private const string m_gameOverVictoryClass = "gameover-victory";
        private const string m_gameOverDefeatClass = "gameover-defeat";
        private const float m_warningThreshold = 0.5f;
        private const float m_dangerThreshold = 0.25f;

        private ProgressBar m_bossBar;
        private ProgressBar m_playerBar;
        private Label m_bossHealthText;
        private Label m_playerHealthText;
        private VisualElement m_gameOverOverlay;
        private Label m_gameOverBadge;
        private Label m_gameOverTitle;
        private Label m_gameOverMessage;
        private Label m_gameOverHint;

        [SerializeField] private UIDocument m_document;
        [SerializeField] private DoubleIntSignalAsset m_bossHealthChanged;
        [SerializeField] private DoubleIntSignalAsset m_playerHealthChanged;

        public void OnEnable() {
            if (m_document == null) {
                m_document = GetComponent<UIDocument>();
            }

            if (m_document == null) {
                Debug.LogWarning($"{nameof(UIManager)} needs a {nameof(UIDocument)} reference.", this);
                return;
            }

            VisualElement root = m_document.rootVisualElement;

            m_bossBar = root.Q<ProgressBar>(m_bossHealthBarName);
            m_bossHealthText = root.Q<Label>(m_bossHealthTextName);
            m_playerBar = root.Q<ProgressBar>(m_playerHealthBarName);
            m_playerHealthText = root.Q<Label>(m_playerHealthTextName);
            m_gameOverOverlay = root.Q<VisualElement>(m_gameOverOverlayName);
            m_gameOverBadge = root.Q<Label>(m_gameOverBadgeName);
            m_gameOverTitle = root.Q<Label>(m_gameOverTitleName);
            m_gameOverMessage = root.Q<Label>(m_gameOverMessageName);
            m_gameOverHint = root.Q<Label>(m_gameOverHintName);

            if (m_bossBar == null) {
                Debug.LogWarning($"{nameof(UIManager)} could not find a ProgressBar named '{m_bossHealthBarName}'.", this);
            }

            if (m_bossHealthText == null) {
                Debug.LogWarning($"{nameof(UIManager)} could not find a Label named '{m_bossHealthTextName}'.", this);
            }

            if (m_playerBar == null) {
                Debug.LogWarning($"{nameof(UIManager)} could not find a ProgressBar named '{m_playerHealthBarName}'.", this);
            }

            if (m_playerHealthText == null) {
                Debug.LogWarning($"{nameof(UIManager)} could not find a Label named '{m_playerHealthTextName}'.", this);
            }

            if (m_gameOverOverlay == null) {
                Debug.LogWarning($"{nameof(UIManager)} could not find a VisualElement named '{m_gameOverOverlayName}'.", this);
            } else {
                HideGameOver();
            }

            m_bossHealthChanged?.AddListener(UpdateBossBar);
            m_playerHealthChanged?.AddListener(UpdatePlayerBar);
        }

        public void OnDisable() {
            m_bossHealthChanged?.RemoveListener(UpdateBossBar);
            m_playerHealthChanged?.RemoveListener(UpdatePlayerBar);
        }

        public void ShowGameOver(bool bossDied) {
            if (m_gameOverOverlay == null) return;

            m_gameOverOverlay.RemoveFromClassList(m_hiddenClass);
            m_gameOverOverlay.EnableInClassList(m_gameOverVictoryClass, bossDied);
            m_gameOverOverlay.EnableInClassList(m_gameOverDefeatClass, !bossDied);

            if (m_gameOverBadge != null) {
                m_gameOverBadge.text = bossDied ? "VICTORY" : "DEFEAT";
            }

            if (m_gameOverTitle != null) {
                m_gameOverTitle.text = bossDied ? "BOSS DEFEATED" : "YOU DIED";
            }

            if (m_gameOverMessage != null) {
                m_gameOverMessage.text = bossDied
                    ? "You won the boss died!!"
                    : "The boss overwhelmed you. Try again!";
            }

            if (m_gameOverHint != null) {
                m_gameOverHint.text = "Restart to try again.";
            }
        }

        public void HideGameOver() {
            if (m_gameOverOverlay == null) return;

            m_gameOverOverlay.AddToClassList(m_hiddenClass);
            m_gameOverOverlay.RemoveFromClassList(m_gameOverVictoryClass);
            m_gameOverOverlay.RemoveFromClassList(m_gameOverDefeatClass);
        }

        private void UpdateBossBar(int _current, int _max) {
            if (m_bossBar == null || _max <= 0) return;

            float percent = Mathf.Clamp01((float)_current / _max);

            m_bossBar.value = percent * 100f;
            m_bossBar.title = string.Empty;

            if (m_bossHealthText != null) {
                m_bossHealthText.text = FormatHealth(_current, _max, percent);
            }

            SetHealthState(m_bossBar, percent);
        }

        private void UpdatePlayerBar(int _current, int _max) {
            if (m_playerBar == null || _max <= 0) return;

            float percent = Mathf.Clamp01((float)_current / _max);

            m_playerBar.value = percent * 100f;
            m_playerBar.title = string.Empty;

            if (m_playerHealthText != null) {
                m_playerHealthText.text = FormatHealth(_current, _max, percent);
            }

            SetHealthState(m_playerBar, percent);
        }

        private static string FormatHealth(int _current, int _max, float _percent) {
            int percent = Mathf.RoundToInt(_percent * 100f);
            return $"{_current} / {_max}  {percent}%";
        }

        private static void SetHealthState(VisualElement _bar, float _percent) {
            bool danger = _percent <= m_dangerThreshold;
            bool warning = !danger && _percent <= m_warningThreshold;

            _bar.EnableInClassList(m_healthDangerClass, danger);
            _bar.EnableInClassList(m_healthWarningClass, warning);
        }
    }
}
