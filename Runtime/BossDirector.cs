using System.Collections.Generic;
using UnityEngine;
using TitanTool.Runtime.Data;
using TitanTool.Runtime.Nodes.Base;
using Utility;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TitanTool.Runtime {
    public class BossDirector : MonoBehaviour {
        [Header("Graph")]
        [SerializeField] private BossGraphAsset m_graph;
        public BossGraphAsset graph => m_graph;
        [SerializeField] private float m_tickRate = 0.1f;
        [SerializeField] private bool m_debugLogging;
        [SerializeField] private Transform m_player;
        [SerializeField] private Animator m_animator;
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private TargetPointProvider m_targetPoints;
        [SerializeField, HideInInspector] private TargetPoint[] m_spawnPoints;
        [field: SerializeField, HideInInspector] public DamagableTeam team { get; private set; } = DamagableTeam.Opponent;
        private Rigidbody2D m_rigidbody;
        public bool paused;
        
        private BossGraphRunner m_runner;
        private BossHealth m_bossHealth;
        public NodeContext context => m_runner?.context;
        private float m_timer;

#if UNITY_EDITOR
        public void SetGraph(BossGraphAsset value) {
            m_graph = value;
        }
#endif

        private void OnValidate() {
            team = DamagableTeam.Opponent;

            if (graph != null && graph.root == null) {
                graph.EnsureValid();
            }
        }

        private void Start() {
            team = DamagableTeam.Opponent;

            if (graph == null) {
                Debug.LogError("No graph assigned.", this);
                enabled = false;
                return;
            }

#if UNITY_EDITOR
            RefreshGraphImportIfRuntimeDataIsMissing();
#endif
            graph.EnsureValid();
            if (graph.root == null) {
                Debug.LogError("Graph has no root node.", this);
                enabled = false;
                return;
            }

            m_runner = new(graph);
            context.debugLogging = m_debugLogging;
            m_bossHealth = GetComponent<BossHealth>();
            m_rigidbody = GetComponent<Rigidbody2D>();
            if (m_animator == null) {
                m_animator = GetComponent<Animator>();
            }

            if (m_animator == null) {
                m_animator = GetComponentInChildren<Animator>();
            }
            if (m_spriteRenderer == null) {
                m_spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (m_spriteRenderer == null) {
                m_spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (m_targetPoints == null) {
                m_targetPoints = FindFirstObjectByType<TargetPointProvider>();
            }
            if (m_rigidbody != null) {
                m_rigidbody.gravityScale = 0f;
            }
            
            context.blackboard.Set(BKeys.BossTransform, transform);
            SyncBossHealthToBlackboard();
            if (m_bossHealth != null) {
                context.blackboard.Set(BKeys.BossMaxHp, m_bossHealth.health);
            }
            if (m_rigidbody != null) {
                context.blackboard.Set(BKeys.BossRigidbody2D, m_rigidbody);
            }
            if (m_animator != null) {
                context.blackboard.Set(BKeys.BossAnimator, m_animator);
            }
            if (m_spriteRenderer != null) {
                context.blackboard.Set(BKeys.BossSpriteRenderer, m_spriteRenderer);
            }
            context.blackboard.Set(BKeys.SpawnPoints, BuildSpawnPointList());
            context.blackboard.Set(BKeys.SpawnPointKeyMap, BuildSpawnPointKeyMap());
            if (m_player != null) {
                context.blackboard.Set(BKeys.PlayerTransform, m_player);
            }
        }

#if UNITY_EDITOR
        private void RefreshGraphImportIfRuntimeDataIsMissing() {
            if (graph == null || graph.root != null || graph.nodes.Count > 0)
                return;

            string graphPath = AssetDatabase.GetAssetPath(graph);
            if (string.IsNullOrEmpty(graphPath))
                return;

            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadMainAssetAtPath(graphPath) is BossGraphAsset refreshedGraph)
                m_graph = refreshedGraph;
        }
#endif

        private List<Transform> BuildSpawnPointList() {
            List<Transform> spawnPoints = new();

            foreach (TargetPoint spawnPoint in GetConfiguredSpawnPoints()) {
                if (spawnPoint != null)
                    spawnPoints.Add(spawnPoint.transform);
            }

            return spawnPoints;
        }

        private Dictionary<TargetPointKey, Transform> BuildSpawnPointKeyMap() {
            Dictionary<TargetPointKey, Transform> spawnPoints = new();

            foreach (TargetPoint spawnPoint in GetConfiguredSpawnPoints()) {
                if (spawnPoint == null || spawnPoint.key == null)
                    continue;

                if (!spawnPoints.TryAdd(spawnPoint.key, spawnPoint.transform)) {
                    Debug.LogWarning($"Duplicate TargetPoint key '{spawnPoint.key.name}' on {spawnPoint.name}. The first point with that key will be used.", spawnPoint);
                }
            }

            return spawnPoints;
        }

        private IEnumerable<TargetPoint> GetConfiguredSpawnPoints() {
            if (m_targetPoints != null) {
                foreach (TargetPoint spawnPoint in m_targetPoints.GetPoints()) {
                    if (spawnPoint != null)
                        yield return spawnPoint;
                }

                yield break;
            }

            bool hasManualPoints = false;
            if (m_spawnPoints != null) {
                foreach (TargetPoint spawnPoint in m_spawnPoints) {
                    if (spawnPoint == null)
                        continue;

                    hasManualPoints = true;
                    yield return spawnPoint;
                }
            }

            if (hasManualPoints)
                yield break;

            foreach (TargetPoint spawnPoint in FindObjectsByType<TargetPoint>(FindObjectsSortMode.None)) {
                if (spawnPoint != null)
                    yield return spawnPoint;
            }
        }

        [ContextMenu("Collect Scene Target Points")]
        private void CollectSceneTargetPoints() {
            m_spawnPoints = FindObjectsByType<TargetPoint>(FindObjectsSortMode.None);
        }

        private void OnEnable() {
            if (graph != null) {
                BossDebugRegistry.Register(graph, this);
            }
        }

        private void OnDisable() {
            if (graph != null) {
                BossDebugRegistry.Unregister(graph, this);
            }
        }

        private void Update() {
            if (paused)
                return;

            m_timer += Time.deltaTime;

            if (m_timer < m_tickRate)
                return;

            context.debugLogging = m_debugLogging;

            SyncBossHealthToBlackboard();
            m_runner.Tick(m_timer);
            m_timer = 0f;
        }

        private void SyncBossHealthToBlackboard() {
            if (m_bossHealth != null) {
                context.blackboard.Set(BKeys.BossHp, m_bossHealth.health);
            }
        }

        [ContextMenu("Reset Runtime State")]
        private void ResetRuntime() {
            m_runner?.Reset();
        }

        [ContextMenu("Pause / Resume")]
        private void TogglePause() {
            paused = !paused;
        }
    }
}
