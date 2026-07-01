using System.Collections.Generic;
using UnityEngine;

namespace Utility {
    public class ObjectPool<T> where T : MonoBehaviour, IPoolable {
        private readonly List<IPoolable> m_activePool = new List<IPoolable>();
        private readonly List<IPoolable> m_inactivePool = new List<IPoolable>();
        private readonly List<T> m_prefabs;

        public ObjectPool(T _prefab) {
            m_prefabs = new List<T> {
                _prefab
            };
        }

        public ObjectPool(List<T> _prefabs) {
            m_prefabs = _prefabs;
        }

        public IPoolable RequestObject(Vector2 _position) {
            if (m_inactivePool.Count <= 0) {
                Debug.LogError("No More Inactive Pool Items. Increase Pool Size");
                return null;
            } else {
                IPoolable currentPool = m_inactivePool[0];
                currentPool.SetPosition(_position);
                ActivateItem(currentPool);
                return currentPool;
            }
        }

        public IPoolable AddNewItemToPool() {
            if (m_prefabs.Count == 0) {
                Debug.LogError("No prefabs available in the pool.");
                return null;
            }

            T prefab = m_prefabs[Random.Range(0, m_prefabs.Count)];
            T instance = Object.Instantiate(prefab);
            instance.gameObject.SetActive(false);
            m_inactivePool.Add(instance);
            return instance;
        }

        private IPoolable ActivateItem(IPoolable _item) {
            _item.EnablePoolable();
            _item.active = true;
            int index = m_inactivePool.IndexOf(_item);
            if (index != -1) {
                m_inactivePool.RemoveAt(index);
            }

            m_activePool.Add(_item);
            return _item;
        }

        public void DeactivateItem(IPoolable _item) {
            int index = m_activePool.IndexOf(_item);
            if (index != -1) {
                m_activePool.RemoveAt(index);
            }

            _item.DisablePoolable();
            _item.active = false;
            m_inactivePool.Add(_item);
        }
    }
}