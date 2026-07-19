# TitanTool

TitanTool is a Unity boss-behavior graph tool for building, validating, compiling, and debugging boss encounters.

## Install

For assessment or local use, install TitanTool through Unity Package Manager:

1. Open `Window > Package Manager`.
2. Press `+`.
3. Choose `Add package from disk...`.
4. Select this package's `package.json`.

If the package is later pushed to its own Git repository, it can also be installed from that repository URL.

## Requirements

- Unity 6 or newer.
- Unity GraphToolkit `0.4.0-exp.2`.
- Cinemachine `3.1.6` for sample camera and impulse components.

GraphToolkit and Cinemachine are declared as package dependencies in `package.json`.

## What Is Included

- `Runtime`: boss graph runtime, blackboard, boss director, target points, runtime nodes, and the small Utility support slice TitanTool currently depends on.
- `Runtime/Data/Signals`: default signal assets used by the runtime, samples, and demo prefabs.
- `Editor`: GraphToolkit editor nodes, graph compiler, validation, debugger window, inspector tooling, and icons.
- `Documentation~`: custom-node guidance and example boss graph patterns.
- `Samples~`: optional Template Scene content importable through Unity Package Manager, including the current `DefaultBoss.titan` example graph.

## Quick Start

1. Install the package through Unity Package Manager.
2. Import the `Template Scene` sample.
3. Add a `BossDirector` to a boss GameObject.
4. Assign or create a boss graph asset.
5. Open the graph, build a flow from `Start`, then compile/debug through the TitanTool editor workflow.

## Custom Nodes And Optional Polish Packages

Use `Custom Scriptable Node` when a project needs boss-specific logic without modifying the package. Create a `TitanToolScriptableNode` asset in the game project, put custom gameplay, feedback, tween, or async code in that asset, then assign it to the node's `Node Asset` port.

TitanTool intentionally does not hard-depend on optional polish packages such as UniTask, DoTween, or FEEL. Projects that use those packages can reference them from their own custom node assets or adapter assemblies, while projects that do not use them can still install TitanTool cleanly.

Advanced users can also create full custom editor/runtime nodes in their own assemblies with `BossGraphNode` and `[GraphNode]`; the node registry now scans loaded assemblies for compatible node registrations.

## Notes For This First Package Pass

TitanTool currently bundles a minimal `Utility` assembly because the runtime uses shared types such as `DataAsset`, `Health`, `DamagableTeam`, signals, and pooling contracts.

For a cleaner public release, the next polish pass should either keep those support types as part of TitanTool intentionally or split them into a separate package dependency.
