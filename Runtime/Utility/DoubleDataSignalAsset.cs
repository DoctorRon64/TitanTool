using System;

namespace Utility {
    public abstract class DoubleDataSignalAsset<T, TU> : DataAsset {
        private readonly Signal<T, TU> m_signal = new();

        public void AddListener(Action<T, TU> _callback) => m_signal.AddListener(_callback);
        public void RemoveListener(Action<T, TU> _callback) => m_signal.RemoveListener(_callback);
        public void Clear() => m_signal.Clear();
        public void Invoke(T _value, TU _value2) => m_signal.Invoke(_value, _value2);
    }
}