using Utility;
using UnityEngine;

namespace Game {
    public class AudioManager : Singleton<AudioManager>, ISingleton {
        private const int SfxSourceCount = 12;

        private GameObject m_audioRoot;
        private AudioSource[] m_sfxSources;
        private int m_nextSfxSourceIndex;

        public void OnInitialize() {
            m_audioRoot = new GameObject(nameof(AudioManager));
            Object.DontDestroyOnLoad(m_audioRoot);

            m_sfxSources = new AudioSource[SfxSourceCount];
            for (int i = 0; i < m_sfxSources.Length; i++) {
                AudioSource source = m_audioRoot.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                m_sfxSources[i] = source;
            }
        }

        public void OnDestroy() {
            if (m_audioRoot != null)
                Object.Destroy(m_audioRoot);
        }

        public void Play(AudioAsset audioAsset) {
            if (audioAsset == null || audioAsset.clips == null || audioAsset.clips.Length == 0)
                return;

            AudioClip clip = audioAsset.clips[Random.Range(0, audioAsset.clips.Length)];
            if (clip == null)
                return;

            AudioSource source = GetNextSfxSource();
            source.pitch = Random.Range(audioAsset.pitch.x, audioAsset.pitch.y);
            source.volume = Mathf.Clamp01(audioAsset.volume);
            source.PlayOneShot(clip);
        }

        private AudioSource GetNextSfxSource() {
            AudioSource source = m_sfxSources[m_nextSfxSourceIndex];
            m_nextSfxSourceIndex = (m_nextSfxSourceIndex + 1) % m_sfxSources.Length;
            return source;
        }
    }
}
