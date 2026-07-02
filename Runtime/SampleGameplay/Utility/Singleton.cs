namespace Utility {
    using System;
    using UnityEngine;

    public interface ISingleton {
        void OnInitialize();
        void OnDestroy();
        virtual void OnUpdate() { }
    }

    public class Singleton<T> where T : ISingleton, new() {
        private static T s_instance;

        public static T instance {
            get {
                if (s_instance != null) return s_instance;
                s_instance = new();
                s_instance.OnInitialize();
                return s_instance;
            }
        }

        ~Singleton() {
            if (s_instance == null) return;
            s_instance.OnDestroy();
            s_instance = default;
        }
    }

    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour {
        private static T s_instance;
        private static readonly object lockObj = new();
        private static bool s_isQuitting = false;

        public static bool isInitialized => s_instance != null;

        public static T instance {
            get {
                if (s_isQuitting) {
                    Debug.LogWarning($"[SingletonMono] Instance of {typeof(T)} is requested after application quit.");
                    return null;
                }

                lock (lockObj) {
                    if (s_instance != null) return s_instance;
                    if (s_instance == null) s_instance = FindAnyObjectByType<T>();
                    if (s_instance != null) return s_instance;

                    GameObject singletonObj = new GameObject(typeof(T).Name);
                    s_instance = singletonObj.AddComponent<T>();
                    DontDestroyOnLoad(singletonObj);
                    Debug.Log($"[SingletonMono] Created singleton instance of {typeof(T)}.");
                }

                return s_instance;
            }
        }

        protected virtual void Awake() {
            if (s_instance == null) {
                s_instance = this as T;
                DontDestroyOnLoad(gameObject);
            } else if (s_instance != this) {
                Debug.LogWarning($"[SingletonMono] Duplicate instance of {typeof(T)} detected. Destroying the new one.");
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy() {
            if (s_instance == this) {
                s_instance = null;
            }
        }

        protected virtual void OnApplicationQuit() {
            s_isQuitting = true;
        }
    }
}