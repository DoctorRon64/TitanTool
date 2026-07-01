# TitanTool

TitanTool is a Unity boss-behavior graph tool for building, validating, compiling, and debugging boss encounters.

## Install

When this folder is pushed as its own repository, install it in Unity with that repository URL:

```text
https://github.com/<owner>/<repo>.git
```

If this package folder stays inside another repository, install it with a path query instead:

```text
https://github.com/<owner>/<repo>.git?path=/com.drron.titantool
```

## Requirements

- Unity 6 or newer.
- Unity GraphToolkit `0.4.0-exp.2`.
- Cinemachine `3.1.6` for the Cuphead sample camera and impulse components.

GraphToolkit and Cinemachine are declared as package dependencies in `package.json`.

## What Is Included

- `Runtime`: boss graph runtime, blackboard, boss director, target points, runtime nodes, and the small Utility support slice TitanTool currently depends on.
- `Runtime/Data/Signals`: default signal assets used by the runtime, samples, and demo prefabs.
- `Editor`: GraphToolkit editor nodes, graph compiler, validation, debugger window, inspector tooling, and icons.
- `Documentation~`: node guide and project/package structure docs.
- `Samples~`: optional Starter Data and Cuphead Example content importable through Unity Package Manager, including the current `AllNodes.titan` reference graph.

## Quick Start

1. Install the package through Unity Package Manager.
2. Import the `Starter Data` sample.
3. Add a `BossDirector` to a boss GameObject.
4. Assign or create a boss graph asset.
5. Open the graph, build a flow from `Start`, then compile/debug through the TitanTool editor workflow.

## Notes For This First Package Pass

TitanTool currently bundles a minimal `Utility` assembly because the runtime uses shared types such as `DataAsset`, `Health`, `DamagableTeam`, signals, and pooling contracts.

For a cleaner public release, the next polish pass should either keep those support types as part of TitanTool intentionally or split them into a separate package dependency.
