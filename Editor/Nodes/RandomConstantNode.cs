using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace TitanTool.Editor.Nodes {
    public enum RandomConstantValueType {
        Float,
        Int,
        Vector2
    }

    [Serializable]
    public class RandomConstantNode : Node, IGraphValueProvider, IGraphRandomRangeProvider {
        private const string IN_PORT_MIN = "Min";
        private const string IN_PORT_MAX = "Max";
        private const string OUT_PORT_VALUE = "Value";
        private const string OPTION_VALUE_TYPE = "ValueType";

        public override void OnEnable() {
            base.OnEnable();
            BossGraphNodeMetadataUtility.ApplyTooltip(this, "Outputs a random value between the minimum and maximum range.");
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<RandomConstantValueType>(OPTION_VALUE_TYPE)
                .WithDisplayName("Value Type")
                .WithDefaultValue(RandomConstantValueType.Float)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            switch (GetValueType()) {
                case RandomConstantValueType.Int:
                    context.AddInputPort<int>(IN_PORT_MIN)
                        .WithDisplayName("Min Value")
                        .WithDefaultValue(0)
                        .Build();
                    context.AddInputPort<int>(IN_PORT_MAX)
                        .WithDisplayName("Max Value")
                        .WithDefaultValue(10)
                        .Build();
                    context.AddOutputPort<int>(OUT_PORT_VALUE)
                        .WithDisplayName("Random Int")
                        .Build();
                    break;

                case RandomConstantValueType.Vector2:
                    context.AddInputPort<Vector2>(IN_PORT_MIN)
                        .WithDisplayName("Min Value")
                        .WithDefaultValue(Vector2.zero)
                        .Build();
                    context.AddInputPort<Vector2>(IN_PORT_MAX)
                        .WithDisplayName("Max Value")
                        .WithDefaultValue(Vector2.one)
                        .Build();
                    context.AddOutputPort<Vector2>(OUT_PORT_VALUE)
                        .WithDisplayName("Random Vector2")
                        .Build();
                    break;

                default:
                    context.AddInputPort<float>(IN_PORT_MIN)
                        .WithDisplayName("Min Value")
                        .WithDefaultValue(0f)
                        .Build();
                    context.AddInputPort<float>(IN_PORT_MAX)
                        .WithDisplayName("Max Value")
                        .WithDefaultValue(1f)
                        .Build();
                    context.AddOutputPort<float>(OUT_PORT_VALUE)
                        .WithDisplayName("Random Float")
                        .Build();
                    break;
            }
        }

        public bool TryGetValue<T>(out T value) {
            object randomValue = GetValueType() switch {
                RandomConstantValueType.Int => GetRandomInt(),
                RandomConstantValueType.Vector2 => GetRandomVector2(),
                _ => GetRandomFloat()
            };

            if (randomValue is T typedValue) {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetRange<T>(out GraphRandomRange<T> range) {
            switch (GetValueType()) {
                case RandomConstantValueType.Int when typeof(T) == typeof(int):
                    GetIntRange(out int intMin, out int intMax);
                    range = (GraphRandomRange<T>)(object)new GraphRandomRange<int>(intMin, intMax);
                    return true;

                case RandomConstantValueType.Vector2 when typeof(T) == typeof(Vector2):
                    GetVectorRange(out Vector2 vectorMin, out Vector2 vectorMax);
                    range = (GraphRandomRange<T>)(object)new GraphRandomRange<Vector2>(vectorMin, vectorMax);
                    return true;

                case RandomConstantValueType.Float when typeof(T) == typeof(float):
                    GetFloatRange(out float floatMin, out float floatMax);
                    range = (GraphRandomRange<T>)(object)new GraphRandomRange<float>(floatMin, floatMax);
                    return true;
            }

            range = default;
            return false;
        }

        private float GetRandomFloat() {
            GetFloatRange(out float min, out float max);
            return UnityEngine.Random.Range(min, max);
        }

        private int GetRandomInt() {
            GetIntRange(out int min, out int max);
            return UnityEngine.Random.Range(min, max + 1);
        }

        private Vector2 GetRandomVector2() {
            GetVectorRange(out Vector2 min, out Vector2 max);
            return new Vector2(
                UnityEngine.Random.Range(Mathf.Min(min.x, max.x), Mathf.Max(min.x, max.x)),
                UnityEngine.Random.Range(Mathf.Min(min.y, max.y), Mathf.Max(min.y, max.y))
            );
        }

        private void GetFloatRange(out float min, out float max) {
            float minValue = GraphNodePortUtility.GetInputValue<float>(this, IN_PORT_MIN);
            float maxValue = GraphNodePortUtility.GetInputValue<float>(this, IN_PORT_MAX);
            min = Mathf.Min(minValue, maxValue);
            max = Mathf.Max(minValue, maxValue);
        }

        private void GetIntRange(out int min, out int max) {
            int minValue = GraphNodePortUtility.GetInputValue<int>(this, IN_PORT_MIN);
            int maxValue = GraphNodePortUtility.GetInputValue<int>(this, IN_PORT_MAX);
            min = Mathf.Min(minValue, maxValue);
            max = Mathf.Max(minValue, maxValue);
        }

        private void GetVectorRange(out Vector2 min, out Vector2 max) {
            min = GraphNodePortUtility.GetInputValue<Vector2>(this, IN_PORT_MIN);
            max = GraphNodePortUtility.GetInputValue<Vector2>(this, IN_PORT_MAX);
        }

        private RandomConstantValueType GetValueType() {
            if (GetNodeOptionByName(OPTION_VALUE_TYPE)?.TryGetValue(out RandomConstantValueType valueType) == true)
                return valueType;

            return RandomConstantValueType.Float;
        }
    }
}
