using System;
using System.Collections.Generic;
using UnityEngine;

namespace TitanTool.Runtime {
    public class Blackboard {
        private readonly Dictionary<string, object> m_data = new();
        private readonly Dictionary<string, Type> m_types = new();

        public void Set<T>(BlackboardKey<T> key, T value) {
            SetValue(key.Name, value);
        }

        public void SetValue<T>(string keyName, T value) {
            if (string.IsNullOrWhiteSpace(keyName)) {
                throw new ArgumentException("Blackboard key name cannot be empty.", nameof(keyName));
            }

            if (m_types.TryGetValue(keyName, out Type existingType)) {
                if (existingType != typeof(T)) {
                    throw new InvalidOperationException(
                        $"Type mismatch for key '{keyName}': expected {existingType.Name}, got {typeof(T).Name}"
                    );
                }
            }
            else {
                m_types[keyName] = typeof(T);
            }

            m_data[keyName] = value;
        }

        public T Get<T>(BlackboardKey<T> key) {
            return GetValue<T>(key.Name);
        }

        public T GetValue<T>(string keyName) {
            if (m_data.TryGetValue(keyName, out object value)) {
                return (T)value;
            }

            return default;
        }

        public bool TryGet<T>(BlackboardKey<T> key, out T value) {
            return TryGetValue(key.Name, out value);
        }

        public bool TryGetValue<T>(string keyName, out T value) {
            if (m_data.TryGetValue(keyName, out object rawValue)) {
                value = (T)rawValue;
                return true;
            }

            value = default;
            return false;
        }

        public void RemoveValue(string keyName) {
            m_data.Remove(keyName);
            m_types.Remove(keyName);
        }

        public IReadOnlyDictionary<string, object> values => m_data;
        public IReadOnlyDictionary<string, Type> types => m_types;
    }

    public class BlackboardKey<T> {
        public string Name { get; }
        public BlackboardKey(string name) => Name = name;
    }

    public static class BKeys {
        public static readonly BlackboardKey<int> BossHp = new("BossHp");
        public static readonly BlackboardKey<int> BossMaxHp = new("BossMaxHp");
        public static readonly BlackboardKey<Transform> PlayerTransform = new("PlayerTransform");
        public static readonly BlackboardKey<Transform> BossTransform = new("BossTransform");
        public static readonly BlackboardKey<Rigidbody2D> BossRigidbody2D = new("BossRigidbody2D");
        public static readonly BlackboardKey<Animator> BossAnimator = new("BossAnimator");
        public static readonly BlackboardKey<SpriteRenderer> BossSpriteRenderer = new("BossSpriteRenderer");
        public static readonly BlackboardKey<List<Transform>> SpawnPoints = new("SpawnPoints");
        public static readonly BlackboardKey<Dictionary<TargetPointKey, Transform>> SpawnPointKeyMap = new("SpawnPointKeyMap");
    }
}
