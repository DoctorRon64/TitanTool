using System;
using System.Collections.Generic;
using TitanTool.Runtime;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    public class RandomTargetPointKeyNode : Node, IResizableSlotNode, IGraphValueProvider, IGraphTargetPointKeySetProvider, IGraphValueNodeValidator {
        private const string IN_PORT_TARGET_KEY_PREFIX = "TargetPointKey";
        private const string OUT_PORT_VALUE = "Value";
        private const string OPTION_TARGET_KEY_COUNT = "TargetKeyCount";
        private const int MIN_TARGET_KEY_COUNT = 2;

        public string slotCountOptionName => OPTION_TARGET_KEY_COUNT;
        public string slotDisplayName => "Target Points";
        public int minimumSlotCount => MIN_TARGET_KEY_COUNT;
        public ResizableSlotPortDirection slotPortDirection => ResizableSlotPortDirection.Input;

        public override void OnEnable() {
            base.OnEnable();
            BossGraphNodeMetadataUtility.ApplyTooltip(this, "Random Target Point Key: outputs one assigned scene TargetPointKey at random. Empty slots are ignored and missing scene keys are reported.");
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<int>(OPTION_TARGET_KEY_COUNT)
                .WithDisplayName("Target Points")
                .WithDefaultValue(MIN_TARGET_KEY_COUNT)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            for (int i = 0; i < GetTargetKeyCount(); i++) {
                context.AddInputPort<TargetPointKey>(GetTargetPointKeyPortName(i))
                    .WithDisplayName($"Target Point {i}")
                    .Build();
            }

            context.AddOutputPort<TargetPointKey>(OUT_PORT_VALUE)
                .WithDisplayName("Random Target Key")
                .Build();
        }

        public bool TryGetValue<T>(out T value) {
            TargetPointKey randomKey = GetRandomTargetPointKey();
            if (randomKey is T typedValue) {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetTargetPointKeys(out TargetPointKey[] keys) {
            List<TargetPointKey> keyList = GetTargetPointKeys();
            keys = keyList.ToArray();
            return keyList.Count > 0;
        }

        public void Validate(GraphValueNodeValidationContext context) {
            int filledKeys = 0;
            for (int i = 0; i < GetTargetKeyCount(); i++) {
                string portName = GetTargetPointKeyPortName(i);
                TargetPointKey key = context.GetInputValue<TargetPointKey>(portName);
                if (key == null) {
                    context.Warning($"Random Target Point Key slot {i} is empty and will be ignored.");
                    continue;
                }

                filledKeys++;
                context.ValidateTargetPointKey(portName, $"Random Target Point Key {i}");
            }

            if (filledKeys == 0)
                context.Error("Random Target Point Key has no target keys assigned.");
        }

        public string GetSlotPortName(int index) => GetTargetPointKeyPortName(index);

        private TargetPointKey GetRandomTargetPointKey() {
            List<TargetPointKey> keys = GetTargetPointKeys();
            return keys.Count > 0 ? keys[UnityEngine.Random.Range(0, keys.Count)] : null;
        }

        private List<TargetPointKey> GetTargetPointKeys() {
            List<TargetPointKey> keys = new();
            for (int i = 0; i < GetTargetKeyCount(); i++) {
                TargetPointKey key = GraphNodePortUtility.GetInputValue<TargetPointKey>(this, GetTargetPointKeyPortName(i));
                if (key != null)
                    keys.Add(key);
            }

            return keys;
        }

        private int GetTargetKeyCount() {
            if (GetNodeOptionByName(OPTION_TARGET_KEY_COUNT)?.TryGetValue(out int count) == true)
                return Math.Max(MIN_TARGET_KEY_COUNT, count);

            return MIN_TARGET_KEY_COUNT;
        }

        private static string GetTargetPointKeyPortName(int index) => $"{IN_PORT_TARGET_KEY_PREFIX}{index}";
    }
}
