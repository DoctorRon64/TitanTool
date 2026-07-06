using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UIElements;

namespace TitanTool.Editor {
    [InitializeOnLoad]
    internal static class TitanToolUsageLogger {
        private const string SessionStartKey = "TitanTool.UsageLogger.SessionStartUtcTicks";
        private const string EnabledKey = "TitanTool.UsageLogger.Enabled";
        private const string LogFolder = "Library/TitanTool/UsageLogs";
        private const string LogFileName = "titantool-usage-log.jsonl";
        private const string MenuRoot = "Window/TitanTool/Internal/Usage Log";
        private const string LoggingEnabledMenu = MenuRoot + "/Logging Enabled";
        private const BindingFlags ShortcutFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly HashSet<VisualElement> s_attachedGraphViews = new HashSet<VisualElement>();
        private static double s_lastExplicitUndoRedoTime = -10d;

        static TitanToolUsageLogger() {
            EnsureSessionStarted();
            EditorApplication.quitting += LogSessionEnded;
            EditorApplication.update += AttachUndoRedoKeyLogger;
            Undo.undoRedoPerformed += LogUndoRedoFallback;
            Application.logMessageReceived += LogUnityError;
            CompilationPipeline.compilationStarted += LogCSharpCompileStarted;
            CompilationPipeline.assemblyCompilationFinished += LogCSharpAssemblyFinished;
        }

        public static void LogGraphCompileAttempt(BossGraph graph) {
            Log("compile_attempt", graph, "Boss graph compile started.");
        }

        public static void LogGraphCompileErrors(BossGraph graph, IReadOnlyCollection<BossGraphValidationIssue> issues) {
            if (issues == null || issues.Count == 0)
                return;

            string details = string.Join(" | ", issues.Select(issue => issue.message));
            Log("compile_errors", graph, details, issues.Count);
        }

        public static void LogGraphCompileError(string graphPath, string message) {
            Log("compile_errors", graphPath, Path.GetFileNameWithoutExtension(graphPath), message, 1);
        }

        public static void LogNodePlaced(BossGraph graph, int delta, int nodeCount) {
            Log("nodes_placed", graph, $"Nodes added: {delta}. Total nodes: {nodeCount}.", delta, nodeCount);
        }

        public static void LogNodeRemoved(BossGraph graph, int delta, int nodeCount) {
            Log("nodes_removed", graph, $"Nodes removed: {delta}. Total nodes: {nodeCount}.", delta, nodeCount);
        }

        public static void LogGraphSaved(string assetPath) {
            Log("graph_saved", assetPath, Path.GetFileNameWithoutExtension(assetPath), "Boss graph asset saved.");
        }

        public static void LogGraphOpened(string assetPath) {
            Log("graph_opened", assetPath, Path.GetFileNameWithoutExtension(assetPath), "Boss graph opened.");
        }

        [MenuItem(LoggingEnabledMenu)]
        private static void ToggleLoggingEnabled() {
            bool enabled = !IsEnabled;
            EditorPrefs.SetBool(EnabledKey, enabled);
            Menu.SetChecked(LoggingEnabledMenu, enabled);

            if (enabled)
                Log("logging_enabled", null, null, "TitanTool usage logging enabled.", force: true);
            else
                Log("logging_disabled", null, null, "TitanTool usage logging disabled.", force: true);
        }

        [MenuItem(LoggingEnabledMenu, true)]
        private static bool ValidateLoggingEnabled() {
            Menu.SetChecked(LoggingEnabledMenu, IsEnabled);
            return true;
        }

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line) {
            string assetPath = AssetDatabase.GetAssetPath(instanceId);
            if (IsTitanGraphPath(assetPath))
                LogGraphOpened(assetPath);

            return false;
        }

        [MenuItem(MenuRoot + "/Print Summary")]
        private static void PrintSummary() {
            Debug.Log(BuildSummary());
        }

        [MenuItem(MenuRoot + "/Export Summary")]
        private static void ExportSummary() {
            string path = EditorUtility.SaveFilePanel("Export TitanTool Usage Summary", "", $"TitanToolUsageSummary-{DateTime.Now:yyyyMMdd-HHmmss}.txt", "txt");
            if (string.IsNullOrEmpty(path))
                return;

            File.WriteAllText(path, BuildSummary());
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "/Export CSV")]
        private static void ExportCsv() {
            string path = EditorUtility.SaveFilePanel("Export TitanTool Usage CSV", "", $"TitanToolUsage-{DateTime.Now:yyyyMMdd-HHmmss}.csv", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            File.WriteAllText(path, BuildCsv(ReadEntries()));
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "/Open Log Folder")]
        private static void OpenLogFolder() {
            EnsureLogFolder();
            EditorUtility.RevealInFinder(LogFolder);
        }

        [MenuItem(MenuRoot + "/Clear Log")]
        private static void ClearLog() {
            if (!EditorUtility.DisplayDialog("Clear TitanTool usage log?", "This deletes the local TitanTool usage log for this project.", "Clear", "Cancel"))
                return;

            string path = LogPath;
            if (File.Exists(path))
                File.Delete(path);

            Log("log_cleared", null, null, "Usage log cleared.");
        }

        private static void EnsureSessionStarted() {
            if (long.TryParse(SessionState.GetString(SessionStartKey, string.Empty), out _))
                return;

            long ticks = DateTime.UtcNow.Ticks;
            SessionState.SetString(SessionStartKey, ticks.ToString(CultureInfo.InvariantCulture));
            Log("session_started", null, null, "Unity editor session started.");
        }

        private static void LogSessionEnded() {
            Log("session_ended", null, null, "Unity editor session ended.", durationSeconds: CurrentSessionDurationSeconds());
        }

        private static void LogCSharpCompileStarted(object context) {
            Log("csharp_compile_attempt", null, null, "Unity C# compilation started.");
        }

        private static void LogCSharpAssemblyFinished(string assemblyPath, CompilerMessage[] messages) {
            CompilerMessage[] errors = messages != null
                ? messages.Where(message => message.type == CompilerMessageType.Error).ToArray()
                : Array.Empty<CompilerMessage>();

            if (errors.Length == 0)
                return;

            string details = string.Join(" | ", errors.Select(error => $"{Path.GetFileName(error.file)}:{error.line} {error.message}"));
            string assemblyName = !string.IsNullOrEmpty(assemblyPath)
                ? Path.GetFileNameWithoutExtension(assemblyPath)
                : "UnknownAssembly";
            Log("compile_errors", null, assemblyName, details, errors.Length);
        }

        private static void LogUnityError(string condition, string stackTrace, LogType type) {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            bool titanToolRelated =
                ContainsTitanTool(condition) ||
                ContainsTitanTool(stackTrace);

            if (Application.isPlaying)
                Log("runtime_errors", null, null, condition, titanToolRelated ? 1 : 0);
            else if (titanToolRelated)
                Log("editor_errors", null, null, condition, 1);
        }

        private static bool ContainsTitanTool(string text) {
            return !string.IsNullOrEmpty(text) &&
                   text.IndexOf("TitanTool", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AttachUndoRedoKeyLogger() {
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>()) {
                if (!TryGetBossGraphView(window, out VisualElement graphView))
                    continue;

                if (!s_attachedGraphViews.Add(graphView))
                    continue;

                graphView.RegisterCallback<KeyDownEvent>(OnGraphKeyDown, TrickleDown.TrickleDown);
                graphView.RegisterCallback<DetachFromPanelEvent>(OnGraphViewDetached);
            }
        }

        private static void OnGraphKeyDown(KeyDownEvent evt) {
            if (!(evt.ctrlKey || evt.commandKey))
                return;

            if (IsTextInputFocused(evt.currentTarget as VisualElement))
                return;

            if (evt.keyCode == KeyCode.Z && !evt.shiftKey) {
                s_lastExplicitUndoRedoTime = EditorApplication.timeSinceStartup;
                Log("undo", TryResolveGraph(evt.currentTarget as VisualElement), "Undo shortcut used.");
            } else if (evt.keyCode == KeyCode.Y || (evt.keyCode == KeyCode.Z && evt.shiftKey)) {
                s_lastExplicitUndoRedoTime = EditorApplication.timeSinceStartup;
                Log("redo", TryResolveGraph(evt.currentTarget as VisualElement), "Redo shortcut used.");
            }
        }

        private static void LogUndoRedoFallback() {
            if (EditorApplication.timeSinceStartup - s_lastExplicitUndoRedoTime < 0.75d)
                return;

            Log("undo_redo", null, null, "Undo/redo action performed outside the graph shortcut logger.");
        }

        private static void OnGraphViewDetached(DetachFromPanelEvent evt) {
            if (evt.currentTarget is VisualElement graphView)
                s_attachedGraphViews.Remove(graphView);
        }

        private static bool TryGetBossGraphView(EditorWindow window, out VisualElement graphView) {
            graphView = null;
            if (window == null)
                return false;

            Type windowType = window.GetType();
            if (!windowType.Name.Contains("GraphViewEditorWindow"))
                return false;

            object rawGraphView = windowType.GetProperty("GraphView", ShortcutFlags)?.GetValue(window);
            if (!(rawGraphView is VisualElement view))
                return false;

            if (TryResolveGraph(view) == null)
                return false;

            graphView = view;
            return true;
        }

        private static BossGraph TryResolveGraph(VisualElement graphView) {
            object graphModel = graphView?.GetType().GetProperty("GraphModel", ShortcutFlags)?.GetValue(graphView);
            object graph = graphModel?.GetType().GetProperty("Graph", ShortcutFlags)?.GetValue(graphModel);
            return graph as BossGraph;
        }

        private static bool IsTextInputFocused(VisualElement graphView) {
            VisualElement focusedElement = graphView?.panel?.focusController?.focusedElement as VisualElement;
            for (VisualElement current = focusedElement; current != null; current = current.parent) {
                if (current is TextField || current is IMGUIContainer)
                    return true;

                string typeName = current.GetType().Name;
                if (typeName.Contains("TextInput") || typeName.Contains("SearchField"))
                    return true;
            }

            return false;
        }

        private static void Log(string eventName, BossGraph graph, string details, int count = 0, int nodeCount = -1, double durationSeconds = -1d, bool force = false) {
            string graphPath = graph != null ? graph.assetPath : null;
            string graphName = !string.IsNullOrEmpty(graphPath) ? Path.GetFileNameWithoutExtension(graphPath) : graph?.name;
            Log(eventName, graphPath, graphName, details, count, nodeCount, durationSeconds, force);
        }

        private static void Log(string eventName, string graphPath, string graphName, string details, int count = 0, int nodeCount = -1, double durationSeconds = -1d, bool force = false) {
            if (!force && !IsEnabled)
                return;

            UsageLogEntry entry = new UsageLogEntry {
                timestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                eventName = eventName,
                graphPath = graphPath ?? string.Empty,
                graphName = graphName ?? string.Empty,
                details = details ?? string.Empty,
                count = count,
                nodeCount = nodeCount,
                durationSeconds = durationSeconds
            };

            EnsureLogFolder();
            File.AppendAllText(LogPath, JsonUtility.ToJson(entry) + Environment.NewLine);
        }

        private static string BuildSummary() {
            List<UsageLogEntry> entries = ReadEntries();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("TitanTool Usage Summary");
            builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Logging enabled: {IsEnabled}");
            builder.AppendLine($"Current session duration: {FormatDuration(CurrentSessionDurationSeconds())}");
            builder.AppendLine($"Total log entries: {entries.Count}");
            builder.AppendLine();

            foreach (IGrouping<string, UsageLogEntry> group in entries.GroupBy(entry => entry.eventName).OrderBy(group => group.Key)) {
                int total = group.Sum(entry => entry.count > 0 ? entry.count : 1);
                builder.AppendLine($"{group.Key}: {total}");
            }

            builder.AppendLine();
            builder.AppendLine("Recent entries:");
            foreach (UsageLogEntry entry in entries.Skip(Math.Max(0, entries.Count - 15))) {
                builder.AppendLine($"{entry.timestampUtc} | {entry.eventName} | {entry.graphName} | {entry.details}");
            }

            return builder.ToString();
        }

        private static string BuildCsv(IEnumerable<UsageLogEntry> entries) {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("timestampUtc,eventName,graphName,graphPath,count,nodeCount,durationSeconds,details");

            foreach (UsageLogEntry entry in entries) {
                builder.Append(Csv(entry.timestampUtc)).Append(',');
                builder.Append(Csv(entry.eventName)).Append(',');
                builder.Append(Csv(entry.graphName)).Append(',');
                builder.Append(Csv(entry.graphPath)).Append(',');
                builder.Append(entry.count.ToString(CultureInfo.InvariantCulture)).Append(',');
                builder.Append(entry.nodeCount.ToString(CultureInfo.InvariantCulture)).Append(',');
                builder.Append(entry.durationSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
                builder.AppendLine(Csv(entry.details));
            }

            return builder.ToString();
        }

        private static List<UsageLogEntry> ReadEntries() {
            string path = LogPath;
            if (!File.Exists(path))
                return new List<UsageLogEntry>();

            List<UsageLogEntry> entries = new List<UsageLogEntry>();
            foreach (string line in File.ReadLines(path)) {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try {
                    UsageLogEntry entry = JsonUtility.FromJson<UsageLogEntry>(line);
                    if (entry != null)
                        entries.Add(entry);
                } catch {
                    // Keep export working if a log line was interrupted during editor shutdown.
                }
            }

            return entries;
        }

        private static string Csv(string value) {
            value ??= string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string FormatDuration(double seconds) {
            if (seconds < 0d)
                return "unknown";

            TimeSpan span = TimeSpan.FromSeconds(seconds);
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }

        private static double CurrentSessionDurationSeconds() {
            if (!long.TryParse(SessionState.GetString(SessionStartKey, string.Empty), out long ticks))
                return -1d;

            DateTime start = new DateTime(ticks, DateTimeKind.Utc);
            return Math.Max(0d, (DateTime.UtcNow - start).TotalSeconds);
        }

        private static bool IsTitanGraphPath(string assetPath) {
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.EndsWith("." + BossGraph.ASSET_EXTENSION, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureLogFolder() {
            if (!Directory.Exists(LogFolder))
                Directory.CreateDirectory(LogFolder);
        }

        private static string LogPath => Path.Combine(LogFolder, LogFileName);

        private static bool IsEnabled => EditorPrefs.GetBool(EnabledKey, true);

        [Serializable]
        private sealed class UsageLogEntry {
            public string timestampUtc;
            public string eventName;
            public string graphPath;
            public string graphName;
            public string details;
            public int count;
            public int nodeCount;
            public double durationSeconds;
        }
    }

    internal sealed class TitanToolUsageAssetModificationProcessor : AssetModificationProcessor {
        private static string[] OnWillSaveAssets(string[] paths) {
            foreach (string path in paths) {
                if (!string.IsNullOrEmpty(path) &&
                    path.EndsWith("." + BossGraph.ASSET_EXTENSION, StringComparison.OrdinalIgnoreCase)) {
                    TitanToolUsageLogger.LogGraphSaved(path);
                }
            }

            return paths;
        }
    }
}
