using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TitanTool.Editor {
    [InitializeOnLoad]
    internal static class TitanToolDocumentationAutoOpen {
        private const string PrefKeyPrefix = "TitanTool.Documentation.Opened.";

        static TitanToolDocumentationAutoOpen() {
            EditorApplication.delayCall += OpenDocumentationOncePerVersion;
        }

        private static void OpenDocumentationOncePerVersion() {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            string version = GetPackageVersion();
            string prefKey = PrefKeyPrefix + version;
            if (EditorPrefs.GetBool(prefKey, false))
                return;

            EditorPrefs.SetBool(prefKey, true);
            TitanToolDocumentationWindow.OpenWindow();
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
