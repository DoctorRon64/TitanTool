using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TitanTool.Editor {
    [InitializeOnLoad]
    internal static class TitanToolDocumentationAutoOpen {
        private const string PrefKeyPrefix = "TitanTool.Documentation.Opened.";
        private const string RuntimeDebuggerPrefKeyPrefix = "TitanTool.RuntimeDebugger.Opened.";

        static TitanToolDocumentationAutoOpen() {
            EditorApplication.delayCall += OpenPackageWindowsOncePerVersion;
        }

        private static void OpenPackageWindowsOncePerVersion() {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            string version = GetPackageVersion();

            OpenOnce(PrefKeyPrefix + version, () => TitanToolDocumentationWindow.OpenWindow());
            OpenOnce(RuntimeDebuggerPrefKeyPrefix + version, () => BossGraphDebugWindow.OpenWindow());
        }

        private static void OpenOnce(string prefKey, System.Action openWindow) {
            if (EditorPrefs.GetBool(prefKey, false))
                return;

            EditorPrefs.SetBool(prefKey, true);
            openWindow?.Invoke();
        }

        private static string GetPackageVersion() {
            UnityEditor.PackageManager.PackageInfo packageInfo =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(Assembly.GetExecutingAssembly());

            return string.IsNullOrWhiteSpace(packageInfo?.version)
                ? "dev"
                : packageInfo.version;
        }
    }
}
