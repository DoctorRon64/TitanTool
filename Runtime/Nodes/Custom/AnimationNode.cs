using TitanTool.Runtime.Nodes.Base;
using TitanTool.Runtime.Values;
using UnityEngine;

namespace TitanTool.Runtime.Nodes.Custom {
    [NodeView("Control Animation", "Action/")]
    public class AnimationNode : Node {
        [SerializeField] private AnimationCommand m_command = AnimationCommand.Trigger;
        [SerializeField] private string m_parameterName;
        [SerializeField] private string m_stateName;
        [SerializeField] private bool m_boolValue;
        [SerializeField] private RuntimeFloatValue m_floatValue = RuntimeFloatValue.Fixed(0f);
        [SerializeField] private RuntimeIntValue m_intValue = RuntimeIntValue.Fixed(0);
        [SerializeField] private RuntimeFloatValue m_crossFadeDuration = RuntimeFloatValue.Fixed(0.1f);

        public void SetCommand(AnimationCommand command) => m_command = command;
        public void SetParameterName(string parameterName) => m_parameterName = parameterName;
        public void SetStateName(string stateName) => m_stateName = stateName;
        public void SetBoolValue(bool value) => m_boolValue = value;
        public void SetFloatValue(float value) => m_floatValue = RuntimeFloatValue.Fixed(value);
        public void SetFloatValue(RuntimeFloatValue value) => m_floatValue = value;
        public void SetIntValue(int value) => m_intValue = RuntimeIntValue.Fixed(value);
        public void SetIntValue(RuntimeIntValue value) => m_intValue = value;
        public void SetCrossFadeDuration(float duration) => m_crossFadeDuration = RuntimeFloatValue.Fixed(Mathf.Max(0f, duration));
        public void SetCrossFadeDuration(RuntimeFloatValue duration) => m_crossFadeDuration = duration;

        public override NodeStatus Tick(NodeContext ctx) {
            if (!TryResolveAnimator(ctx, out Animator animator)) {
                Debug.LogError($"{name}: Animation node requires an Animator on the boss.");
                return NodeStatus.Failure;
            }

            switch (m_command) {
                case AnimationCommand.Trigger:
                    if (!HasParameterName())
                        return NodeStatus.Failure;
                    animator.SetTrigger(m_parameterName);
                    break;

                case AnimationCommand.SetBool:
                    if (!HasParameterName())
                        return NodeStatus.Failure;
                    animator.SetBool(m_parameterName, m_boolValue);
                    break;

                case AnimationCommand.SetFloat:
                    if (!HasParameterName())
                        return NodeStatus.Failure;
                    animator.SetFloat(m_parameterName, m_floatValue.Evaluate());
                    break;

                case AnimationCommand.SetInt:
                    if (!HasParameterName())
                        return NodeStatus.Failure;
                    animator.SetInteger(m_parameterName, m_intValue.Evaluate());
                    break;

                case AnimationCommand.Play:
                    if (!HasStateName())
                        return NodeStatus.Failure;
                    animator.Play(m_stateName);
                    break;

                case AnimationCommand.CrossFade:
                    if (!HasStateName())
                        return NodeStatus.Failure;
                    animator.CrossFade(m_stateName, Mathf.Max(0f, m_crossFadeDuration.Evaluate()));
                    break;
            }

            return NodeStatus.Success;
        }

        private bool TryResolveAnimator(NodeContext ctx, out Animator animator) {
            if (ctx.blackboard.TryGet(BKeys.BossAnimator, out animator) && animator != null)
                return true;

            animator = null;
            if (ctx.blackboard.TryGet(BKeys.BossTransform, out Transform boss) && boss != null)
                return boss.TryGetComponent(out animator);

            return false;
        }

        private bool HasParameterName() {
            if (!string.IsNullOrWhiteSpace(m_parameterName))
                return true;

            Debug.LogError($"{name}: Animation parameter name is required.");
            return false;
        }

        private bool HasStateName() {
            if (!string.IsNullOrWhiteSpace(m_stateName))
                return true;

            Debug.LogError($"{name}: Animation state name is required.");
            return false;
        }
    }

    public enum AnimationCommand {
        Trigger,
        SetBool,
        SetFloat,
        SetInt,
        Play,
        CrossFade
    }
}
