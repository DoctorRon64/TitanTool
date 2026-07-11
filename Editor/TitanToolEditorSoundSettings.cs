using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TitanTool.Editor {
    internal enum TitanToolEditorSoundEvent {
        NodeCreated,
        NodeRemoved,
        WireConnected,
        WireRemoved,
        ChildIncreased,
        ChildDecreased
    }

    public class TitanToolEditorSoundSettings : ScriptableObject {
        private const string DEFAULT_ASSET_PATH = AssetPath.ASSET_PATH + "/EditorSoundSettings.asset";
        private const string PACKAGE_SOUND_PATH = "Packages/com.drron.titantool/Editor/Sounds";

        [SerializeField] private bool m_enabled = true;
        [SerializeField] private bool m_useGeneratedFallbacks = true;
        [SerializeField, Range(0f, 1f)] private float m_volume = 0.35f;
        [SerializeField, Min(0f)] private float m_minInterval = 0.04f;
        [SerializeField] private AudioClip m_nodeCreatedClip;
        [SerializeField] private AudioClip m_nodeRemovedClip;
        [SerializeField] private AudioClip m_wireConnectedClip;
        [SerializeField] private AudioClip m_wireRemovedClip;
        [SerializeField] private AudioClip m_childIncreasedClip;
        [SerializeField] private AudioClip m_childDecreasedClip;

        public bool enabled => m_enabled;
        public bool useGeneratedFallbacks => m_useGeneratedFallbacks;
        public float volume => Mathf.Clamp01(m_volume);
        public float minInterval => Mathf.Max(0f, m_minInterval);

        private static readonly AudioClip[] s_packageDefaultClips = new AudioClip[Enum.GetValues(typeof(TitanToolEditorSoundEvent)).Length];

        [MenuItem("Window/TitanTool/Editor Sound Settings")]
        private static void SelectSettingsAsset() {
            TitanToolEditorSoundSettings settings = GetOrCreate();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        internal static void Play(TitanToolEditorSoundEvent soundEvent) {
            TitanToolEditorSoundPlayer.Play(GetOrCreate(), soundEvent);
        }

        internal AudioClip GetClip(TitanToolEditorSoundEvent soundEvent) {
            AudioClip overrideClip = soundEvent switch {
                TitanToolEditorSoundEvent.NodeCreated => m_nodeCreatedClip,
                TitanToolEditorSoundEvent.NodeRemoved => m_nodeRemovedClip,
                TitanToolEditorSoundEvent.WireConnected => m_wireConnectedClip,
                TitanToolEditorSoundEvent.WireRemoved => m_wireRemovedClip,
                TitanToolEditorSoundEvent.ChildIncreased => m_childIncreasedClip,
                TitanToolEditorSoundEvent.ChildDecreased => m_childDecreasedClip,
                _ => null
            };

            return overrideClip != null ? overrideClip : GetPackageDefaultClip(soundEvent);
        }

        private static AudioClip GetPackageDefaultClip(TitanToolEditorSoundEvent soundEvent) {
            int index = (int)soundEvent;
            if (index < 0 || index >= s_packageDefaultClips.Length)
                return null;

            if (s_packageDefaultClips[index] != null)
                return s_packageDefaultClips[index];

            string fileName = soundEvent switch {
                TitanToolEditorSoundEvent.NodeCreated => "note_add.mp3",
                TitanToolEditorSoundEvent.NodeRemoved => "note_delete.mp3",
                TitanToolEditorSoundEvent.WireConnected => "wire_add.mp3",
                TitanToolEditorSoundEvent.WireRemoved => "wire_delete.mp3",
                TitanToolEditorSoundEvent.ChildIncreased => "child_increase.mp3",
                TitanToolEditorSoundEvent.ChildDecreased => "child_decrease.mp3",
                _ => null
            };

            if (string.IsNullOrEmpty(fileName))
                return null;

            s_packageDefaultClips[index] = AssetDatabase.LoadAssetAtPath<AudioClip>($"{PACKAGE_SOUND_PATH}/{fileName}");
            return s_packageDefaultClips[index];
        }

        private static TitanToolEditorSoundSettings GetOrCreate() {
            TitanToolEditorSoundSettings settings = AssetDatabase.LoadAssetAtPath<TitanToolEditorSoundSettings>(DEFAULT_ASSET_PATH);
            if (settings != null)
                return settings;

            BossGraph.EnsureAssetFolder();
            settings = CreateInstance<TitanToolEditorSoundSettings>();
            AssetDatabase.CreateAsset(settings, DEFAULT_ASSET_PATH);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }

    internal static class TitanToolEditorSoundPlayer {
        private const int SAMPLE_RATE = 44100;
        private static readonly Type s_audioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        private static readonly MethodInfo s_setPreviewVolumeMethod = s_audioUtilType?.GetMethod("SetPreviewClipVolume", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo s_playPreviewMethod = FindPlayPreviewMethod();
        private static readonly AudioClip[] s_generatedClips = new AudioClip[Enum.GetValues(typeof(TitanToolEditorSoundEvent)).Length];
        private static double s_lastPlayTime;

        public static void Play(TitanToolEditorSoundSettings settings, TitanToolEditorSoundEvent soundEvent) {
            if (settings == null || !settings.enabled)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now - s_lastPlayTime < settings.minInterval)
                return;

            AudioClip clip = settings.GetClip(soundEvent);
            if (clip == null && settings.useGeneratedFallbacks)
                clip = GetGeneratedClip(soundEvent);

            if (clip == null)
                return;

            s_lastPlayTime = now;
            if (!TryPlayPreviewClip(clip, settings.volume))
                EditorApplication.Beep();
        }

        private static MethodInfo FindPlayPreviewMethod() {
            if (s_audioUtilType == null)
                return null;

            foreach (MethodInfo method in s_audioUtilType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
                if (method.Name != "PlayPreviewClip")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(AudioClip))
                    return method;
            }

            return null;
        }

        private static bool TryPlayPreviewClip(AudioClip clip, float volume) {
            if (s_playPreviewMethod == null)
                return false;

            try {
                s_setPreviewVolumeMethod?.Invoke(null, new object[] { Mathf.Clamp01(volume) });

                ParameterInfo[] parameters = s_playPreviewMethod.GetParameters();
                object[] args = new object[parameters.Length];
                args[0] = clip;
                for (int i = 1; i < args.Length; i++) {
                    Type parameterType = parameters[i].ParameterType;
                    if (parameterType == typeof(int))
                        args[i] = 0;
                    else if (parameterType == typeof(bool))
                        args[i] = false;
                    else if (parameterType == typeof(float))
                        args[i] = Mathf.Clamp01(volume);
                }

                s_playPreviewMethod.Invoke(null, args);
                return true;
            } catch (Exception) {
                return false;
            }
        }

        private static AudioClip GetGeneratedClip(TitanToolEditorSoundEvent soundEvent) {
            int index = (int)soundEvent;
            if (index < 0 || index >= s_generatedClips.Length)
                return null;

            if (s_generatedClips[index] != null)
                return s_generatedClips[index];

            s_generatedClips[index] = CreateTone(soundEvent);
            return s_generatedClips[index];
        }

        private static AudioClip CreateTone(TitanToolEditorSoundEvent soundEvent) {
            float frequency = soundEvent switch {
                TitanToolEditorSoundEvent.NodeCreated => 660f,
                TitanToolEditorSoundEvent.NodeRemoved => 330f,
                TitanToolEditorSoundEvent.WireConnected => 880f,
                TitanToolEditorSoundEvent.WireRemoved => 440f,
                TitanToolEditorSoundEvent.ChildIncreased => 740f,
                TitanToolEditorSoundEvent.ChildDecreased => 370f,
                _ => 550f
            };

            int sampleCount = Mathf.RoundToInt(SAMPLE_RATE * 0.08f);
            float[] samples = new float[sampleCount];
            const float amplitude = 0.25f;

            for (int i = 0; i < sampleCount; i++) {
                float t = i / (float)SAMPLE_RATE;
                float envelope = 1f - i / (float)sampleCount;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * amplitude * envelope;
            }

            AudioClip clip = AudioClip.Create($"TitanTool_{soundEvent}", sampleCount, 1, SAMPLE_RATE, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
