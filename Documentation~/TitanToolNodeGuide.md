# TitanTool Node Guide

This guide explains what TitanTool nodes do, how they fit together, and when a designer might choose one node over another. It is written for building boss behavior graphs, not for reading the C# implementation.

## How To Think About A Graph

A TitanTool graph is a visual behavior plan for a boss. Execution starts at `Start`, travels through flow connections, and visits nodes that decide, wait, move, shoot, spawn, or check state.

Most nodes fit into one of these roles:

- `Flow` nodes define where execution begins.
- `Composite` nodes decide which child branch runs.
- `Decorator` nodes wrap a child and change when or how it can run.
- `Condition` nodes check whether something is true.
- `Action` nodes make the boss do visible work.
- `Blackboard` nodes read or write shared graph values.
- `Utility` nodes keep large graphs readable.

The most important idea is that composites shape the boss logic, actions perform behavior, and conditions decide whether a branch is allowed to continue. The blackboard works like shared memory for the graph, so one node can store a value and another node can use it later.

## Flow Nodes

### Start

`Start` is the entry point of a boss graph. Every graph should have one clear start path, because this is where the runtime begins executing the boss behavior.

Use it to connect into the first major decision in the boss logic. For a simple boss, that might be a `Run In Order` attack loop. For a phase-based boss, it is usually a `Try Children` node that chooses the correct phase branch.

## Composite Nodes

Composite nodes control child branches. They are useful when the graph needs to decide order, priority, randomness, repetition, or parallel behavior.

### Run In Order

`Run In Order` runs children from top to bottom. If a child fails, the sequence stops.

Use this for scripted behavior where every step should happen in a predictable order, such as:

```text
Move To Target -> Wait -> Show Telegraph -> Fire Bullets
```

This node is best for attack chains, intro sequences, and patterns where timing matters.

### Try Children

`Try Children` checks children from top to bottom until one succeeds.

Use this when branches have priority. A common example is phase selection:

```text
Try Children
    Health Phase 30-0
    Health Phase 70-30
    Health Phase 100-70
```

The graph tries each phase branch and runs the first one that is valid. This is useful because only one phase should usually control the boss at a time.

### Pick Random Child

`Pick Random Child` chooses one child at random and runs it.

Use this when the boss should feel less predictable. For example, a boss can randomly choose between a bullet spread, a thrown object, or a movement attack.

### Pick Weighted Child

`Pick Weighted Child` also chooses one child randomly, but each child has a weight. Higher weights make a branch more likely to be selected.

Use this when one attack should be common and another should be rare. For example, a basic shot can have a high weight while a dangerous special attack has a low weight.

### Run In Parallel

`Run In Parallel` runs multiple child branches at the same time. Its success and failure rules decide when the parallel node is finished.

Use this for layered boss behavior. For example, one branch can move the boss while another branch fires bullets or shows telegraphs.

### Repeat Child

`Repeat Child` runs its child multiple times.

Use it for repeated attack loops, multi-shot patterns, or short behaviors that should happen several times before the graph moves on.

## Decorator Nodes

Decorator nodes wrap a child branch. They are useful when a behavior should only run under certain timing or state rules.

### Cooldown Gate

`Cooldown Gate` lets its child run only after the cooldown has finished.

Use this to prevent attacks from firing too often. It is useful for special moves, teleports, or heavy attacks that need breathing room.

### Run Once

`Run Once` runs its child one time. After that, it returns the configured result on later visits.

Use it for one-time events such as an intro animation, a phase transition, or a setup action that should not repeat.

### Health Phase

`Health Phase` runs its child only while the boss health is inside a percent range.

Use it to organize phase-based boss design. Each phase branch can contain its own movement, attack choices, animation changes, or blackboard values.

## Condition Nodes

Condition nodes ask a question. They succeed if the answer is true and fail if the answer is false.

### Check Boss HP

`Check Boss HP` compares the boss's current HP against a threshold.

Use it for simple health gates, phase transitions, or emergency behavior. For percentage-based phase ranges, prefer `Health Phase`.

### Check Blackboard Number

`Check Blackboard Number` compares a number stored in the blackboard against another value.

Use it when graph behavior depends on custom state, such as a counter, random roll, attack level, or tuning value.

## Blackboard Nodes

The blackboard is shared memory for the boss graph. It can store values such as numbers, vectors, transforms, health, target points, and runtime references. Blackboard nodes are useful when multiple parts of the graph need to share the same information.

### Change Blackboard Number

`Change Blackboard Number` sets or modifies a numeric value in the blackboard. It can set, add, subtract, multiply, or divide.

Use it for counters, difficulty ramps, repeated attack tracking, or values that should change over time.

Example:

```text
Change Blackboard Number: AttackCount + 1
Check Blackboard Number: AttackCount >= 3
```

### Set Random Number

`Set Random Number` writes a random int, float, or Vector2 into the blackboard.

Use it when a later node needs a random value, such as a movement offset, shot speed, attack choice, or target position variation.

## Action Nodes

Action nodes make the boss do something visible or meaningful in the scene.

### Wait

`Wait` pauses the current branch for the configured duration.

Use it for timing between attacks, delays after movement, telegraph windows, and pacing.

### Move To Target

`Move To Target` moves the boss toward the player, a target point, or a fixed world position.

Use it when the boss should travel through the arena instead of instantly changing position. It is useful for dashes, repositioning, chasing, or moving into an attack pose.

### Teleport To Target

`Teleport To Target` instantly moves the boss to the player, a target point, or a fixed world position.

Use it for sudden repositioning, phase transitions, or attacks where instant movement is part of the design.

### Stop Movement

`Stop Movement` immediately stops the boss Rigidbody2D movement.

Use it after movement nodes, before a precise attack, or when a phase transition should reset the boss motion.

### Fire Bullets

`Fire Bullets` spawns bullets using a selected pattern, source, aim mode, bullet count, spread, speed, and owner team.

Use it for most projectile attacks. The source decides where bullets come from, and the aim mode decides whether they fire in a fixed direction or toward a target.

### Spawn Object

`Spawn Object` instantiates a prefab at a chosen source.

Use it for bombs, hazards, minions, warning objects, pickups, or arena effects.

### Throw Object

`Throw Object` launches a Rigidbody2D prefab from one source toward another.

Use it for arcing bombs, thrown hazards, falling objects, or attacks that should travel physically instead of appearing instantly.

### Show Telegraph

`Show Telegraph` displays a temporary warning marker before an attack.

Use it to make dangerous attacks readable. A good pattern is:

```text
Show Telegraph -> Wait -> Fire Bullets
```

or:

```text
Show Telegraph -> Wait -> Throw Object
```

### Control Animation

`Control Animation` triggers animator parameters or plays animation clips on the boss animator.

Use it to sync the graph with visual feedback, such as attack windups, phase changes, hurt reactions, or special move animations.

### Set Sprite

`Set Sprite` changes the boss SpriteRenderer to a selected sprite.

Use it for simple visual state changes when a full animation is not needed.

## Utility Nodes

### Reroute

`Reroute` redirects flow wires without changing behavior.

Use it to keep large graphs readable. It is especially useful when wires need to cross long distances or when a graph has multiple phase branches.

## Random Value Nodes

Random value nodes output random values that can be connected into compatible ports.

### Random Float

Outputs a random float between a minimum and maximum value.

Use it for speed, wait time, spread, cooldown, or other tuning values.

### Random Int

Outputs a random whole number between a minimum and maximum value.

Use it for counts, repeat values, or simple random choices.

### Random Vector2

Outputs a random Vector2 inside the configured range.

Use it for random offsets, positions, or movement variation.

## Common Graph Patterns

### Simple Attack Loop

```text
Start
    Repeat Child
        Run In Order
            Fire Bullets
            Wait
```

Use this for a boss that repeats one simple attack with a pause.

### Telegraphed Attack

```text
Run In Order
    Show Telegraph
    Wait
    Fire Bullets
```

Use this when the player should see danger before it happens.

### Random Attack Selection

```text
Pick Weighted Child
    Fire Bullets
    Throw Object
    Move To Target
```

Use this when the boss should choose between attacks, but some attacks should happen more often than others.

### Phase-Based Boss

```text
Start
    Try Children
        Health Phase 30-0
        Health Phase 70-30
        Health Phase 100-70
```

Use `Try Children` for phase routing because it chooses the first valid phase branch. Each `Health Phase` can contain its own attack selection, movement, and animation changes.

### Movement While Attacking

```text
Run In Parallel
    Move To Target
    Run In Order
        Show Telegraph
        Wait
        Fire Bullets
```

Use this when the boss should keep moving while another branch handles attacks.

## Debugging Tips

Use `Window > TitanTool > Runtime Debugger` while the game is running. The debugger can show visited nodes, the active execution path, timings, and live blackboard values.

The `Visited` toggle is useful when a branch is not behaving as expected. If a node is never visited, the issue is usually in the flow path before it. If a condition is visited but fails, check the values it depends on, especially blackboard keys and health thresholds.

## Writing Tips For Documentation Or Voice-Over

When explaining a node, answer three questions:

1. What does this node do?
2. When would a designer use it?
3. What should the designer be careful about?

For example:

```text
Pick Weighted Child chooses one child branch randomly, but each child has a weight.
Use it when some boss attacks should be common and others should be rare.
Keep the weights readable, because extreme values can make some branches almost never happen.
```

This format keeps the documentation practical and prevents the node list from feeling like a code reference.
