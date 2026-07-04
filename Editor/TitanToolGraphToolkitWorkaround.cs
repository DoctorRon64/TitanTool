using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TitanTool.Editor {
    [InitializeOnLoad]
    internal static class TitanToolGraphToolkitWorkaround {
        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static TitanToolGraphToolkitWorkaround() {
            DisableUnsafeFirstLoadFrameAll();
            EditorApplication.update += StabilizeOpenBossGraphViews;
        }

        private static void DisableUnsafeFirstLoadFrameAll() {
            // Graph Toolkit 0.4.0-exp.2 can frame while imported graph views are still attaching.
            Type graphViewType = FindType("Unity.GraphToolkit.Editor.GraphView");
            FieldInfo field = graphViewType?.GetField("ShouldFrameAllOnFirstLoad", FLAGS);

            if (field?.FieldType == typeof(bool))
                field.SetValue(null, false);
        }

        private static void StabilizeOpenBossGraphViews() {
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>()) {
                if (TryGetBossGraphView(window, out VisualElement graphView))
                    DisableUnsafeSpacePartitioning(graphView);
            }
        }

        private static bool TryGetBossGraphView(EditorWindow window, out VisualElement graphView) {
            graphView = null;

            if (window == null)
                return false;

            Type windowType = window.GetType();
            if (!windowType.Name.Contains("GraphViewEditorWindow"))
                return false;

            object rawGraphView = windowType.GetProperty("GraphView", FLAGS)?.GetValue(window);
            if (rawGraphView is not VisualElement view)
                return false;

            object graphModel = GetProperty(rawGraphView, "GraphModel");
            object graph = graphModel != null ? GetProperty(graphModel, "Graph") : null;
            if (graph is not BossGraph)
                return false;

            graphView = view;
            return true;
        }

        private static void DisableUnsafeSpacePartitioning(VisualElement graphView) {
            FieldInfo observerField = graphView.GetType().GetField("m_SpacePartitioningObserver", FLAGS);
            object observer = observerField?.GetValue(graphView);
            if (observer == null)
                return;

            object graphTool = GetProperty(graphView, "GraphTool");
            object observerManager = GetProperty(graphTool, "ObserverManager");
            MethodInfo unregister = observerManager?.GetType().GetMethod("UnregisterObserver", FLAGS);
            unregister?.Invoke(observerManager, new[] { observer });
        }

        private static Type FindType(string fullName) {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                Type type = assembly.GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static object GetProperty(object target, string propertyName) {
            return target?.GetType().GetProperty(propertyName, FLAGS)?.GetValue(target);
        }
    }
}
