using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TitanTool.Editor.Nodes;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TitanTool.Editor {
    [InitializeOnLoad]
    internal static class TitanToolGraphShortcuts {
        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private struct NodeShortcut {
            public readonly Type type;
            public readonly string name;

            public NodeShortcut(Type type, string name) {
                this.type = type;
                this.name = name;
            }
        }

        private sealed class ShortcutState {
            public KeyCode pendingNodeKey = KeyCode.None;
            public object hoveredWire;
            public Vector2 lastGraphPosition;
        }

        private static readonly Dictionary<KeyCode, NodeShortcut> s_nodeShortcuts = new Dictionary<KeyCode, NodeShortcut> {
            { KeyCode.S, new NodeShortcut(typeof(SequenceNode), "Sequence") },
            { KeyCode.D, new NodeShortcut(typeof(WaitNode), "Delay") },
            { KeyCode.F, new NodeShortcut(typeof(RepeaterNode), "Loop") },
            { KeyCode.O, new NodeShortcut(typeof(RunOnceNode), "Do Once") },
            { KeyCode.R, new NodeShortcut(typeof(RerouteNode), "Reroute") },
            { KeyCode.B, new NodeShortcut(typeof(SelectorNode), "Branch") },
            { KeyCode.N, new NodeShortcut(typeof(RepeaterNode), "Do N") },
            { KeyCode.M, new NodeShortcut(typeof(RandomSelectorNode), "Multi Gate") },
        };

        private static readonly HashSet<VisualElement> s_attachedGraphViews = new HashSet<VisualElement>();
        private static readonly Dictionary<VisualElement, ShortcutState> s_states = new Dictionary<VisualElement, ShortcutState>();

        static TitanToolGraphShortcuts() {
            EditorApplication.update += AttachToOpenBossGraphViews;
        }

        private static void AttachToOpenBossGraphViews() {
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>()) {
                if (!TryGetBossGraphView(window, out VisualElement graphView))
                    continue;

                if (!s_attachedGraphViews.Add(graphView))
                    continue;

                s_states[graphView] = new ShortcutState();
                graphView.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
                graphView.RegisterCallback<KeyUpEvent>(OnKeyUp, TrickleDown.TrickleDown);
                graphView.RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
                graphView.RegisterCallback<MouseMoveEvent>(OnMouseMove, TrickleDown.TrickleDown);
                graphView.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
                graphView.RegisterCallback<DetachFromPanelEvent>(OnDetached);
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
            if (!(rawGraphView is VisualElement view))
                return false;

            object graphModel = GetProperty(rawGraphView, "GraphModel");
            object graph = graphModel != null ? GetProperty(graphModel, "Graph") : null;
            if (!(graph is BossGraph))
                return false;

            graphView = view;
            return true;
        }

        private static void OnKeyDown(KeyDownEvent evt) {
            if (!(evt.currentTarget is VisualElement graphView))
                return;

            if (IsTextInputFocused(graphView))
                return;

            if (evt.ctrlKey || evt.commandKey || evt.altKey || evt.shiftKey)
                return;

            if (evt.keyCode == KeyCode.C) {
                if (TryCreateCommentPlacemat(graphView))
                    Consume(evt);
                return;
            }

            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace) {
                object hoveredWire = s_states.TryGetValue(graphView, out ShortcutState shortcutState) ? shortcutState.hoveredWire : null;
                if (TryDeleteSelectedOrHoveredWires(graphView, hoveredWire))
                    Consume(evt);
                return;
            }

            if (evt.keyCode == KeyCode.Q) {
                if (TryAlignSelection(graphView))
                    Consume(evt);
                return;
            }

            if (!s_nodeShortcuts.ContainsKey(evt.keyCode))
                return;

            if (!s_states.TryGetValue(graphView, out ShortcutState state)) {
                state = new ShortcutState();
                s_states[graphView] = state;
            }

            state.pendingNodeKey = evt.keyCode;
            Consume(evt);
        }

        private static void OnKeyUp(KeyUpEvent evt) {
            if (!(evt.currentTarget is VisualElement graphView))
                return;

            if (!s_states.TryGetValue(graphView, out ShortcutState state))
                return;

            if (state.pendingNodeKey == evt.keyCode) {
                state.pendingNodeKey = KeyCode.None;
                Consume(evt);
            }
        }

        private static void OnMouseDown(MouseDownEvent evt) {
            if (evt.button != 0)
                return;

            if (!(evt.currentTarget is VisualElement graphView))
                return;

            UpdateLastGraphPosition(graphView, evt.localMousePosition);

            object portAtMouse = FindPortAt(graphView, evt.localMousePosition);
            if (evt.altKey && portAtMouse != null) {
                if (TryDeletePortWires(graphView, portAtMouse))
                    Consume(evt);
                return;
            }

            object wireAtMouse = FindWireAt(graphView, evt.localMousePosition);
            if (evt.altKey && wireAtMouse != null) {
                if (TryDeleteWires(graphView, new[] { wireAtMouse }))
                    Consume(evt);
                return;
            }

            if (evt.clickCount == 2 && wireAtMouse != null) {
                if (TryCreateNode(graphView, new NodeShortcut(typeof(RerouteNode), "Reroute"), evt.localMousePosition, out object rerouteNodeModel)) {
                    TrySplitWireWithNode(graphView, wireAtMouse, rerouteNodeModel);
                    Consume(evt);
                }
                return;
            }

            if (!s_states.TryGetValue(graphView, out ShortcutState state))
                return;

            if (!s_nodeShortcuts.TryGetValue(state.pendingNodeKey, out NodeShortcut shortcut))
                return;

            if (TryCreateNode(graphView, shortcut, evt.localMousePosition, out object createdNodeModel)) {
                TrySplitWireWithNode(graphView, wireAtMouse, createdNodeModel);
                Consume(evt);
            }
        }

        private static void OnMouseMove(MouseMoveEvent evt) {
            if (!(evt.currentTarget is VisualElement graphView))
                return;

            if (!s_states.TryGetValue(graphView, out ShortcutState state))
                return;

            state.hoveredWire = FindWireAt(graphView, evt.localMousePosition);
            state.lastGraphPosition = GetGraphPosition(graphView, evt.localMousePosition);
        }

        private static void OnMouseUp(MouseUpEvent evt) {
            if (evt.button != 0)
                return;

            if (!(evt.currentTarget is VisualElement graphView))
                return;

            UpdateLastGraphPosition(graphView, evt.localMousePosition);

            if (!TryGetSingleInsertableSelectedNode(graphView, null, out object nodeModel))
                return;

            object wireToSplit = FindDropWireForNode(graphView, nodeModel, evt.localMousePosition);
            if (wireToSplit == null)
                return;

            if (TrySplitWireWithNode(graphView, wireToSplit, nodeModel))
                Consume(evt);
        }

        private static void OnDetached(DetachFromPanelEvent evt) {
            if (!(evt.currentTarget is VisualElement graphView))
                return;

            s_attachedGraphViews.Remove(graphView);
            s_states.Remove(graphView);
        }

        private static bool TryCreateNode(VisualElement graphView, NodeShortcut shortcut, Vector2 mousePosition, out object createdNodeModel) {
            createdNodeModel = null;
            object graphModel = GetProperty(graphView, "GraphModel");
            if (graphModel == null)
                return false;

            object node;
            try {
                node = Activator.CreateInstance(shortcut.type);
            } catch (Exception exception) {
                Debug.LogException(exception);
                return false;
            }

            Vector2 graphPosition = GetGraphPosition(graphView, mousePosition);
            Type graphModelType = graphModel.GetType();
            MethodInfo registerUndo = graphModelType.GetMethod("RegisterUndo", FLAGS, null, new[] { typeof(string) }, null);
            MethodInfo endUndo = graphModelType.GetMethod("EndUndo", FLAGS, null, Type.EmptyTypes, null);
            MethodInfo createNode = FindCreateNodeModelMethod(graphModelType);

            if (createNode == null)
                return false;

            try {
                registerUndo?.Invoke(graphModel, new object[] { $"Create {shortcut.name}" });
                createdNodeModel = createNode.Invoke(graphModel, new[] { node, graphPosition });
                return createdNodeModel != null;
            } catch (TargetInvocationException exception) {
                Debug.LogException(exception.InnerException ?? exception);
                return false;
            } catch (Exception exception) {
                Debug.LogException(exception);
                return false;
            } finally {
                endUndo?.Invoke(graphModel, null);
            }
        }

        private static MethodInfo FindCreateNodeModelMethod(Type graphModelType) {
            foreach (MethodInfo method in graphModelType.GetMethods(FLAGS)) {
                if (method.Name != "CreateNodeModel")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[1].ParameterType == typeof(Vector2))
                    return method;
            }

            return null;
        }

        private static Vector2 GetGraphPosition(VisualElement graphView, Vector2 mousePosition) {
            object rawContentViewContainer = GetProperty(graphView, "ContentViewContainer");
            if (rawContentViewContainer is VisualElement contentViewContainer) {
                Vector2 worldPosition = graphView.LocalToWorld(mousePosition);
                return contentViewContainer.WorldToLocal(worldPosition);
            }

            return mousePosition;
        }

        private static void UpdateLastGraphPosition(VisualElement graphView, Vector2 localPosition) {
            if (!s_states.TryGetValue(graphView, out ShortcutState state))
                return;

            state.lastGraphPosition = GetGraphPosition(graphView, localPosition);
        }

        private static object FindWireAt(VisualElement graphView, Vector2 localPosition) {
            if (graphView.panel == null)
                return null;

            Vector2 worldPosition = graphView.LocalToWorld(localPosition);
            return FindWireAtWorldPosition(graphView, worldPosition, null);
        }

        private static object FindDropWireForNode(VisualElement graphView, object nodeModel, Vector2 localMousePosition) {
            object wireAtMouse = FindWireAt(graphView, localMousePosition);
            if (wireAtMouse != null && !IsWireConnectedToNode(wireAtMouse, nodeModel))
                return wireAtMouse;

            VisualElement nodeView = FindViewForModel(graphView, nodeModel);
            if (nodeView == null || graphView.panel == null)
                return null;

            object intersectingWire = FindWireIntersectingNode(graphView, nodeView, nodeModel);
            if (intersectingWire != null)
                return intersectingWire;

            foreach (Vector2 worldPosition in GetDropSamplePoints(nodeView.worldBound)) {
                object wire = FindWireAtWorldPosition(graphView, worldPosition, nodeModel);
                if (wire != null)
                    return wire;
            }

            return null;
        }

        private static object FindWireIntersectingNode(VisualElement graphView, VisualElement nodeView, object ignoredNodeModel) {
            Rect bounds = nodeView.worldBound;
            if (bounds.width <= 0f || bounds.height <= 0f)
                return null;

            foreach (VisualElement wireView in GetWireViews(graphView)) {
                object wireModel = GetProperty(wireView, "Model");
                if (ignoredNodeModel != null && IsWireConnectedToNode(wireModel, ignoredNodeModel))
                    continue;

                VisualElement wireControl = GetProperty(wireView, "WireControl") as VisualElement ?? wireView;
                foreach (Vector2 worldPoint in GetDropProbePoints(bounds)) {
                    Vector2 localPoint = wireControl.WorldToLocal(worldPoint);
                    if (wireControl.ContainsPoint(localPoint))
                        return wireModel;
                }
            }

            return null;
        }

        private static IEnumerable<VisualElement> GetWireViews(VisualElement root) {
            Stack<VisualElement> stack = new Stack<VisualElement>();
            stack.Push(root);

            while (stack.Count > 0) {
                VisualElement current = stack.Pop();
                if (IsWireModel(GetProperty(current, "Model")))
                    yield return current;

                for (int index = current.childCount - 1; index >= 0; index--)
                    stack.Push(current[index]);
            }
        }

        private static object FindWireAtWorldPosition(VisualElement graphView, Vector2 worldPosition, object ignoredNodeModel) {
            List<VisualElement> pickedElements = new List<VisualElement>();
            graphView.panel.PickAll(worldPosition, pickedElements);

            foreach (VisualElement pickedElement in pickedElements) {
                for (VisualElement current = pickedElement; current != null && current != graphView.parent; current = current.parent) {
                    object model = GetProperty(current, "Model");
                    if (IsWireModel(model) && (ignoredNodeModel == null || !IsWireConnectedToNode(model, ignoredNodeModel)))
                        return model;

                    if (current == graphView)
                        break;
                }
            }

            return null;
        }

        private static IEnumerable<Vector2> GetDropProbePoints(Rect worldBounds) {
            foreach (Vector2 point in GetDropSamplePoints(worldBounds))
                yield return point;

            const float spacing = 24f;
            float left = worldBounds.xMin + 8f;
            float right = worldBounds.xMax - 8f;
            float top = worldBounds.yMin + 8f;
            float bottom = worldBounds.yMax - 8f;

            if (right < left || bottom < top)
                yield break;

            for (float y = top; y <= bottom; y += spacing) {
                for (float x = left; x <= right; x += spacing)
                    yield return new Vector2(x, y);
            }

            yield return new Vector2(right, bottom);
        }

        private static IEnumerable<Vector2> GetDropSamplePoints(Rect worldBounds) {
            Vector2 center = worldBounds.center;
            yield return center;
            yield return new Vector2(worldBounds.xMin + 10f, center.y);
            yield return new Vector2(worldBounds.xMax - 10f, center.y);
            yield return new Vector2(center.x, worldBounds.yMin + 10f);
            yield return new Vector2(center.x, worldBounds.yMax - 10f);
            yield return new Vector2(worldBounds.xMin + 10f, worldBounds.yMin + 10f);
            yield return new Vector2(worldBounds.xMax - 10f, worldBounds.yMin + 10f);
            yield return new Vector2(worldBounds.xMin + 10f, worldBounds.yMax - 10f);
            yield return new Vector2(worldBounds.xMax - 10f, worldBounds.yMax - 10f);
        }

        private static VisualElement FindViewForModel(VisualElement root, object model) {
            if (root == null || model == null)
                return null;

            Stack<VisualElement> stack = new Stack<VisualElement>();
            stack.Push(root);

            while (stack.Count > 0) {
                VisualElement current = stack.Pop();
                if (ReferenceEquals(GetProperty(current, "Model"), model))
                    return current;

                for (int index = current.childCount - 1; index >= 0; index--)
                    stack.Push(current[index]);
            }

            return null;
        }

        private static object FindPortAt(VisualElement graphView, Vector2 localPosition) {
            if (graphView.panel == null)
                return null;

            Vector2 worldPosition = graphView.LocalToWorld(localPosition);
            List<VisualElement> pickedElements = new List<VisualElement>();
            graphView.panel.PickAll(worldPosition, pickedElements);

            foreach (VisualElement pickedElement in pickedElements) {
                for (VisualElement current = pickedElement; current != null && current != graphView.parent; current = current.parent) {
                    object model = GetProperty(current, "Model");
                    if (IsPortModel(model))
                        return model;

                    if (current == graphView)
                        break;
                }
            }

            return null;
        }

        private static bool TryDeleteSelectedOrHoveredWires(VisualElement graphView, object hoveredWire) {
            List<object> wires = GetSelectedWireModels(graphView);
            if (wires.Count == 0 && hoveredWire != null)
                wires.Add(hoveredWire);

            return TryDeleteWires(graphView, wires);
        }

        private static bool TryDeletePortWires(VisualElement graphView, object portModel) {
            List<object> wires = GetConnectedWires(portModel);
            return TryDeleteWires(graphView, wires);
        }

        private static List<object> GetConnectedWires(object portModel) {
            List<object> wires = new List<object>();
            object rawWires = portModel?.GetType().GetMethod("GetConnectedWires", FLAGS, null, Type.EmptyTypes, null)?.Invoke(portModel, null);
            if (!(rawWires is IEnumerable enumerable))
                return wires;

            foreach (object wire in enumerable) {
                if (IsWireModel(wire))
                    wires.Add(wire);
            }

            return wires;
        }

        private static List<object> GetSelectedWireModels(VisualElement graphView) {
            List<object> wires = new List<object>();
            object selection = graphView.GetType().GetMethod("GetSelection", FLAGS, null, Type.EmptyTypes, null)?.Invoke(graphView, null);
            if (!(selection is IEnumerable enumerable))
                return wires;

            foreach (object model in enumerable) {
                if (IsWireModel(model))
                    wires.Add(model);
            }

            return wires;
        }

        private static bool TryDeleteWires(VisualElement graphView, IReadOnlyList<object> wires) {
            if (wires == null || wires.Count == 0)
                return false;

            Type wireType = FindType("Unity.GraphToolkit.Editor.WireModel");
            Type commandType = FindType("Unity.GraphToolkit.Editor.DeleteWireCommand");
            if (wireType == null || commandType == null)
                return false;

            Array wireArray = Array.CreateInstance(wireType, wires.Count);
            for (int i = 0; i < wires.Count; i++)
                wireArray.SetValue(wires[i], i);

            object command = CreateCommandWithSingleArgument(commandType, wireArray);
            if (command == null)
                return false;

            return TryDispatchCommand(graphView, command);
        }

        private static bool TryGetSingleInsertableSelectedNode(VisualElement graphView, object wireModel, out object nodeModel) {
            nodeModel = null;
            object selection = graphView.GetType().GetMethod("GetSelection", FLAGS, null, Type.EmptyTypes, null)?.Invoke(graphView, null);
            if (!(selection is IEnumerable enumerable))
                return false;

            Type inputOutputPortsNodeModelType = FindType("Unity.GraphToolkit.Editor.InputOutputPortsNodeModel");
            if (inputOutputPortsNodeModelType == null)
                return false;

            foreach (object model in enumerable) {
                if (model == null || !inputOutputPortsNodeModelType.IsInstanceOfType(model))
                    continue;

                if (IsWireConnectedToNode(wireModel, model))
                    continue;

                if (nodeModel != null)
                    return false;

                nodeModel = model;
            }

            return nodeModel != null;
        }

        private static bool TrySplitWireWithNode(VisualElement graphView, object wireModel, object nodeModel) {
            if (wireModel == null || nodeModel == null)
                return false;

            if (!HasCompatiblePortsForWire(wireModel, nodeModel))
                return false;

            Type commandType = FindType("Unity.GraphToolkit.Editor.SplitWireAndInsertExistingNodeCommand");
            if (commandType == null)
                return false;

            object command = CreateSplitWireCommand(commandType, wireModel, nodeModel);
            if (command == null)
                return false;

            return TryDispatchCommand(graphView, command);
        }

        private static bool HasCompatiblePortsForWire(object wireModel, object nodeModel) {
            object wireInput = GetProperty(wireModel, "ToPort");
            object wireOutput = GetProperty(wireModel, "FromPort");
            object wireInputType = GetProperty(wireInput, "PortType");
            object wireOutputType = GetProperty(wireOutput, "PortType");
            if (wireInputType == null || wireOutputType == null)
                return false;

            return HasPortWithType(GetProperty(nodeModel, "OutputsByDisplayOrder"), wireInputType) &&
                   HasPortWithType(GetProperty(nodeModel, "InputsByDisplayOrder"), wireOutputType);
        }

        private static bool HasPortWithType(object rawPorts, object portType) {
            if (!(rawPorts is IEnumerable ports))
                return false;

            foreach (object port in ports) {
                object candidateType = GetProperty(port, "PortType");
                if (candidateType != null && candidateType.Equals(portType))
                    return true;
            }

            return false;
        }

        private static object CreateSplitWireCommand(Type commandType, object wireModel, object nodeModel) {
            foreach (ConstructorInfo constructor in commandType.GetConstructors(FLAGS)) {
                ParameterInfo[] parameters = constructor.GetParameters();
                if (parameters.Length != 2)
                    continue;

                if (!parameters[0].ParameterType.IsInstanceOfType(wireModel))
                    continue;

                if (!parameters[1].ParameterType.IsInstanceOfType(nodeModel))
                    continue;

                return constructor.Invoke(new[] { wireModel, nodeModel });
            }

            return null;
        }

        private static object CreateCommandWithSingleArgument(Type commandType, object argument) {
            foreach (ConstructorInfo constructor in commandType.GetConstructors(FLAGS)) {
                ParameterInfo[] parameters = constructor.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(argument))
                    return constructor.Invoke(new[] { argument });
            }

            return null;
        }

        private static bool TryDispatchCommand(VisualElement graphView, object command) {
            MethodInfo dispatch = FindDispatchMethod(graphView.GetType(), command);
            if (dispatch == null)
                return false;

            try {
                dispatch.Invoke(graphView, new[] { command });
                return true;
            } catch (TargetInvocationException exception) {
                Debug.LogException(exception.InnerException ?? exception);
                return false;
            } catch (Exception exception) {
                Debug.LogException(exception);
                return false;
            }
        }

        private static bool IsWireModel(object model) {
            return model != null && FindType("Unity.GraphToolkit.Editor.WireModel")?.IsInstanceOfType(model) == true;
        }

        private static bool IsPortModel(object model) {
            return model != null && FindType("Unity.GraphToolkit.Editor.PortModel")?.IsInstanceOfType(model) == true;
        }

        private static bool IsWireConnectedToNode(object wireModel, object nodeModel) {
            object nodeGuid = GetProperty(nodeModel, "Guid");
            if (nodeGuid == null)
                return false;

            object fromNodeGuid = GetProperty(wireModel, "FromNodeGuid");
            object toNodeGuid = GetProperty(wireModel, "ToNodeGuid");
            return nodeGuid.Equals(fromNodeGuid) || nodeGuid.Equals(toNodeGuid);
        }

        private static bool TryAlignSelection(VisualElement graphView) {
            object selection = graphView.GetType().GetMethod("GetSelection", FLAGS, null, Type.EmptyTypes, null)?.Invoke(graphView, null);
            if (selection == null)
                return false;

            Type commandType = FindType("Unity.GraphToolkit.Editor.AlignNodesCommand");
            if (commandType == null)
                return false;

            object command = CreateAlignCommand(commandType, graphView, selection);
            if (command == null)
                return false;

            return TryDispatchCommand(graphView, command);
        }

        private static bool TryCreateCommentPlacemat(VisualElement graphView) {
            Rect bounds = GetCommentBounds(graphView);
            Type commandType = FindType("Unity.GraphToolkit.Editor.CreatePlacematCommand");
            if (commandType == null)
                return false;

            object command = CreatePlacematCommand(commandType, bounds, "Comment");
            if (command == null)
                return false;

            return TryDispatchCommand(graphView, command);
        }

        private static Rect GetCommentBounds(VisualElement graphView) {
            const float margin = 60f;
            Rect? bounds = null;
            object selection = graphView.GetType().GetMethod("GetSelection", FLAGS, null, Type.EmptyTypes, null)?.Invoke(graphView, null);

            if (selection is IEnumerable enumerable) {
                foreach (object model in enumerable) {
                    if (model == null || IsWireModel(model) || IsPortModel(model))
                        continue;

                    if (!TryGetModelRect(model, out Rect modelRect))
                        continue;

                    bounds = bounds.HasValue ? Union(bounds.Value, modelRect) : modelRect;
                }
            }

            if (!bounds.HasValue) {
                Vector2 position = s_states.TryGetValue(graphView, out ShortcutState state) ? state.lastGraphPosition : Vector2.zero;
                bounds = new Rect(position - new Vector2(175f, 100f), new Vector2(350f, 200f));
            }

            Rect expanded = bounds.Value;
            expanded.xMin -= margin;
            expanded.yMin -= margin;
            expanded.xMax += margin;
            expanded.yMax += margin;
            return expanded;
        }

        private static bool TryGetModelRect(object model, out Rect rect) {
            rect = default;

            object positionAndSize = GetProperty(model, "PositionAndSize");
            if (positionAndSize is Rect sizedRect) {
                rect = sizedRect;
                return true;
            }

            object position = GetProperty(model, "Position");
            if (position is Vector2 vectorPosition) {
                rect = new Rect(vectorPosition, new Vector2(260f, 140f));
                return true;
            }

            return false;
        }

        private static Rect Union(Rect a, Rect b) {
            return Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin),
                Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax),
                Mathf.Max(a.yMax, b.yMax));
        }

        private static object CreatePlacematCommand(Type commandType, Rect bounds, string title) {
            foreach (ConstructorInfo constructor in commandType.GetConstructors(FLAGS)) {
                ParameterInfo[] parameters = constructor.GetParameters();
                if (parameters.Length < 1 || parameters.Length > 2)
                    continue;

                if (parameters[0].ParameterType != typeof(Rect))
                    continue;

                if (parameters.Length == 1)
                    return constructor.Invoke(new object[] { bounds });

                if (parameters[1].ParameterType == typeof(string))
                    return constructor.Invoke(new object[] { bounds, title });
            }

            return null;
        }

        private static object CreateAlignCommand(Type commandType, VisualElement graphView, object selection) {
            foreach (ConstructorInfo constructor in commandType.GetConstructors(FLAGS)) {
                ParameterInfo[] parameters = constructor.GetParameters();
                if (parameters.Length != 3)
                    continue;

                if (!parameters[0].ParameterType.IsInstanceOfType(graphView))
                    continue;

                if (parameters[1].ParameterType != typeof(bool))
                    continue;

                if (!parameters[2].ParameterType.IsInstanceOfType(selection))
                    continue;

                return constructor.Invoke(new[] { graphView, true, selection });
            }

            return null;
        }

        private static MethodInfo FindDispatchMethod(Type graphViewType, object command) {
            foreach (MethodInfo method in graphViewType.GetMethods(FLAGS)) {
                if (method.Name != "Dispatch")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(command))
                    return method;
            }

            return null;
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

        private static bool IsTextInputFocused(VisualElement graphView) {
            VisualElement focusedElement = graphView.panel?.focusController?.focusedElement as VisualElement;
            for (VisualElement current = focusedElement; current != null; current = current.parent) {
                if (current is TextField || current is IMGUIContainer)
                    return true;

                string typeName = current.GetType().Name;
                if (typeName.Contains("TextInput") || typeName.Contains("SearchField"))
                    return true;
            }

            return false;
        }

        private static void Consume(EventBase evt) {
            evt.StopImmediatePropagation();
        }
    }
}
