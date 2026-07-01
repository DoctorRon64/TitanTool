using System;
using UnityEngine;

namespace Utility {
    public class SignalAsset : DataAsset {
        private readonly Signal m_signal = new();

        public void AddListener(Action callback) {
            m_signal.AddListener(callback);
        }

        public void RemoveListener(Action callback) {
            m_signal.RemoveListener(callback);
        }
        
        public void Clear() => m_signal.Clear();

        public void Invoke() {
            m_signal.Invoke();
        }
    }

    public abstract class SignalAsset<T> : DataAsset {
        private readonly Signal<T> m_signal = new();

        public void AddListener(Action<T> callback) {
            m_signal.AddListener(callback);
        }

        public void RemoveListener(Action<T> callback) {
            m_signal.RemoveListener(callback);
        }
        
        public void Clear() => m_signal.Clear();
        
        public void Invoke(T value) {
            m_signal.Invoke(value);
        }
    }
}