using System;
using UnityEngine;

namespace TitanTool.Runtime.Values {
    [Serializable]
    public struct RuntimeFloatValue {
        [SerializeField] private bool m_randomRange;
        [SerializeField] private float m_value;
        [SerializeField] private float m_min;
        [SerializeField] private float m_max;

        public static RuntimeFloatValue Fixed(float value) {
            return new RuntimeFloatValue {
                m_value = value,
                m_min = value,
                m_max = value
            };
        }

        public static RuntimeFloatValue RandomRange(float min, float max) {
            return new RuntimeFloatValue {
                m_randomRange = true,
                m_value = min,
                m_min = min,
                m_max = max
            };
        }

        public float Evaluate() {
            if (!m_randomRange)
                return m_value;

            float min = Mathf.Min(m_min, m_max);
            float max = Mathf.Max(m_min, m_max);
            return UnityEngine.Random.Range(min, max);
        }
    }

    [Serializable]
    public struct RuntimeIntValue {
        [SerializeField] private bool m_randomRange;
        [SerializeField] private int m_value;
        [SerializeField] private int m_min;
        [SerializeField] private int m_max;

        public static RuntimeIntValue Fixed(int value) {
            return new RuntimeIntValue {
                m_value = value,
                m_min = value,
                m_max = value
            };
        }

        public static RuntimeIntValue RandomRange(int min, int max) {
            return new RuntimeIntValue {
                m_randomRange = true,
                m_value = min,
                m_min = min,
                m_max = max
            };
        }

        public int Evaluate() {
            if (!m_randomRange)
                return m_value;

            int min = Mathf.Min(m_min, m_max);
            int max = Mathf.Max(m_min, m_max);
            return UnityEngine.Random.Range(min, max + 1);
        }
    }

    [Serializable]
    public struct RuntimeVector2Value {
        [SerializeField] private bool m_randomRange;
        [SerializeField] private Vector2 m_value;
        [SerializeField] private Vector2 m_min;
        [SerializeField] private Vector2 m_max;

        public static RuntimeVector2Value Fixed(Vector2 value) {
            return new RuntimeVector2Value {
                m_value = value,
                m_min = value,
                m_max = value
            };
        }

        public static RuntimeVector2Value RandomRange(Vector2 min, Vector2 max) {
            return new RuntimeVector2Value {
                m_randomRange = true,
                m_value = min,
                m_min = min,
                m_max = max
            };
        }

        public Vector2 Evaluate() {
            if (!m_randomRange)
                return m_value;

            return new Vector2(
                UnityEngine.Random.Range(Mathf.Min(m_min.x, m_max.x), Mathf.Max(m_min.x, m_max.x)),
                UnityEngine.Random.Range(Mathf.Min(m_min.y, m_max.y), Mathf.Max(m_min.y, m_max.y))
            );
        }
    }
}
