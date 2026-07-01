using System;
using TitanTool.Runtime.Nodes.Custom;
using UnityEngine;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.AnimationNode), "Control Animation", "Action/", BossGraphNodeCategory.Action, BossGraphNodeIcons.Action, "Triggers parameters or plays animation clips on the boss animator.")]
    public class AnimationNode : BossGraphNode, IRuntimeNodeCompiler, IGraphNodeValidator {
        private const string IN_PORT_PARAMETER_NAME = "ParameterName";
        private const string IN_PORT_STATE_NAME = "StateName";
        private const string IN_PORT_ANIMATION_CLIP = "AnimationClip";
        private const string IN_PORT_BOOL_VALUE = "BoolValue";
        private const string IN_PORT_FLOAT_VALUE = "FloatValue";
        private const string IN_PORT_INT_VALUE = "IntValue";
        private const string IN_PORT_CROSS_FADE_DURATION = "CrossFadeDuration";
        private const string OPTION_COMMAND = "Command";

        protected override bool hasInput => true;

        public override void OnEnable() {
            base.OnEnable();
            InitializeNode(typeof(TitanTool.Runtime.Nodes.Custom.AnimationNode));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) {
            context.AddOption<AnimationCommand>(OPTION_COMMAND)
                .WithDisplayName("Action")
                .WithDefaultValue(AnimationCommand.Trigger)
                .Delayed();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context) {
            AddInputOutputExecutionPorts(context);

            AnimationCommand command = GetCommand();
            switch (command) {
                case AnimationCommand.Trigger:
                    AddParameterPort(context);
                    break;

                case AnimationCommand.SetBool:
                    AddParameterPort(context);
                    context.AddInputPort<bool>(IN_PORT_BOOL_VALUE)
                        .WithDisplayName("Bool Value")
                        .WithDefaultValue(true)
                        .Build();
                    break;

                case AnimationCommand.SetFloat:
                    AddParameterPort(context);
                    context.AddInputPort<float>(IN_PORT_FLOAT_VALUE)
                        .WithDisplayName("Float Value")
                        .WithDefaultValue(0f)
                        .Build();
                    break;

                case AnimationCommand.SetInt:
                    AddParameterPort(context);
                    context.AddInputPort<int>(IN_PORT_INT_VALUE)
                        .WithDisplayName("Int Value")
                        .WithDefaultValue(0)
                        .Build();
                    break;

                case AnimationCommand.Play:
                    AddAnimationClipPort(context);
                    break;

                case AnimationCommand.CrossFade:
                    AddAnimationClipPort(context);
                    context.AddInputPort<float>(IN_PORT_CROSS_FADE_DURATION)
                        .WithDisplayName("Blend Time")
                        .WithDefaultValue(0.1f)
                        .Build();
                    break;
            }
        }

        public void Compile(RuntimeNode runtimeNode) {
            if (runtimeNode is not TitanTool.Runtime.Nodes.Custom.AnimationNode animationRuntime)
                return;

            animationRuntime.SetCommand(GetCommand());
            animationRuntime.SetParameterName(GraphNodePortUtility.GetInputValue<string>(this, IN_PORT_PARAMETER_NAME));
            animationRuntime.SetStateName(GetAnimationStateName());
            animationRuntime.SetBoolValue(GraphNodePortUtility.GetInputValue<bool>(this, IN_PORT_BOOL_VALUE));
            animationRuntime.SetFloatValue(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_FLOAT_VALUE));
            animationRuntime.SetIntValue(GraphNodePortUtility.GetRuntimeIntValue(this, IN_PORT_INT_VALUE));
            animationRuntime.SetCrossFadeDuration(GraphNodePortUtility.GetRuntimeFloatValue(this, IN_PORT_CROSS_FADE_DURATION));
        }

        public void Validate(BossGraphNodeValidationContext context) {
            AnimationCommand command = GetCommand();
            if (RequiresParameter(command) && string.IsNullOrWhiteSpace(context.GetInputValue<string>(IN_PORT_PARAMETER_NAME)))
                context.Error("Animation parameter name is required.");

            if (RequiresState(command) && context.GetInputValue<AnimationClip>(IN_PORT_ANIMATION_CLIP) == null)
                context.Error("Animation clip is required.");

            if (command == AnimationCommand.CrossFade && context.GetInputValue<float>(IN_PORT_CROSS_FADE_DURATION) < 0f)
                context.Error("Animation cross fade duration cannot be negative.");
        }

        private static bool RequiresParameter(AnimationCommand command) {
            return command is AnimationCommand.Trigger or AnimationCommand.SetBool or AnimationCommand.SetFloat or AnimationCommand.SetInt;
        }

        private static bool RequiresState(AnimationCommand command) {
            return command is AnimationCommand.Play or AnimationCommand.CrossFade;
        }

        private static void AddParameterPort(IPortDefinitionContext context) {
            context.AddInputPort<string>(IN_PORT_PARAMETER_NAME)
                .WithDisplayName("Parameter Name")
                .WithDefaultValue(string.Empty)
                .Build();
        }

        private static void AddStatePort(IPortDefinitionContext context) {
            context.AddInputPort<string>(IN_PORT_STATE_NAME)
                .WithDisplayName("State Name")
                .WithDefaultValue(string.Empty)
                .Build();
        }

        private static void AddAnimationClipPort(IPortDefinitionContext context) {
            context.AddInputPort<AnimationClip>(IN_PORT_ANIMATION_CLIP)
                .WithDisplayName("Animation Clip")
                .Build();
        }

        private string GetAnimationStateName() {
            AnimationClip clip = GraphNodePortUtility.GetInputValue<AnimationClip>(this, IN_PORT_ANIMATION_CLIP);
            if (clip != null)
                return clip.name;

            return GraphNodePortUtility.GetInputValue<string>(this, IN_PORT_STATE_NAME);
        }

        private AnimationCommand GetCommand() {
            if (GetNodeOptionByName(OPTION_COMMAND)?.TryGetValue(out AnimationCommand command) == true)
                return command;

            return AnimationCommand.Trigger;
        }
    }
}
