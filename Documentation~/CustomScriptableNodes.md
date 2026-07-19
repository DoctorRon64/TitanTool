# Custom Scriptable Nodes

Use the `Custom Scriptable Node` graph node when a project needs boss-specific logic without changing the TitanTool package.

## Quick Start

Create a ScriptableObject that inherits from `TitanToolScriptableNode`, then assign that asset to the `Node Asset` port on a `Custom Scriptable Node`.

```csharp
using TitanTool.Runtime.Nodes.Base;
using TitanTool.Runtime.Nodes.Custom;
using UnityEngine;

[CreateAssetMenu(menuName = "TitanTool/Custom Nodes/Play Boss Polish")]
public sealed class PlayBossPolishNode : TitanToolScriptableNode {
    public override NodeStatus Tick(Node runtimeNode, NodeContext ctx) {
        Transform boss = ctx.blackboard.Get(BKeys.BossTransform);
        if (boss == null)
            return NodeStatus.Failure;

        // Play particles, camera shake, audio, animation triggers, or package calls here.
        return NodeStatus.Success;
    }
}
```

## Running Over Multiple Ticks

Return `NodeStatus.Running` while work is still active. Store temporary runtime data with `ctx.GetState<T>(runtimeNode)` so each graph node instance keeps its own state.

```csharp
private sealed class State {
    public float elapsed;
}

public override NodeStatus Tick(Node runtimeNode, NodeContext ctx) {
    State state = ctx.GetState<State>(runtimeNode);
    state.elapsed += ctx.deltaTime;

    if (state.elapsed < 1f)
        return NodeStatus.Running;

    ctx.ResetNode(runtimeNode);
    return NodeStatus.Success;
}
```

Override `Abort` to stop tweens, particles, coroutines, async operations, or feedback effects when the branch is cancelled.

## Optional Package Support

TitanTool does not hard-reference optional polish packages. Keep those dependencies in your game assembly or a small adapter assembly, then call them from your `TitanToolScriptableNode` asset:

- DoTween: start or kill tweens from `Tick` and `Abort`.
- FEEL: play or stop feedbacks from the custom asset.
- UniTask: start async work from the custom asset, store the handle or cancellation source in node state, and return `Running` until it completes.

This keeps TitanTool installable in projects that do not use those packages, while still letting projects that do use them build richer boss actions.
