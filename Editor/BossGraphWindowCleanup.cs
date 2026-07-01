using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TitanTool.Editor {
    internal static class BossGraphWindowCleanup {
        private static readonly HashSet<string> s_pendingGraphNames = new(StringComparer.OrdinalIgnoreCase);
        private static bool s_closeQueued;

        public static void QueueCloseForAsset(string assetPath) {
            if (!IsBossGraphPath(assetPath))
                return;

            s_pendingGraphNames.Add(Path.GetFileNameWithoutExtension(assetPath));
            if (s_closeQueued)
                return;

            s_closeQueued = true;
            EditorApplication.delayCall += ClosePendingWindows;
        }

        private static bool IsBossGraphPath(string assetPath) {
            return string.Equals(
                Path.GetExtension(assetPath),
                $".{BossGraph.ASSET_EXTENSION}",
                StringComparison.OrdinalIgnoreCase
            );
        }

        private static void ClosePendingWindows() {
            s_closeQueued = false;

            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>()) {
                if (window == null)
                    continue;

                string title = window.titleContent?.text;
                if (string.IsNullOrEmpty(title))
                    continue;

                foreach (string graphName in s_pendingGraphNames) {
                    if (title.Contains(graphName, StringComparison.OrdinalIgnoreCase)) {
                        window.Close();
                        break;
                    }
                }
            }

            s_pendingGraphNames.Clear();
        }
    }

    internal sealed class BossGraphDeletionProcessor : AssetModificationProcessor {
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options) {
            BossGraphWindowCleanup.QueueCloseForAsset(assetPath);
            return AssetDeleteResult.DidNotDelete;
        }
    }

    internal sealed class BossGraphDeletionPostprocessor : AssetPostprocessor {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths
        ) {
            foreach (string deletedAsset in deletedAssets)
                BossGraphWindowCleanup.QueueCloseForAsset(deletedAsset);
        }
    }
}
