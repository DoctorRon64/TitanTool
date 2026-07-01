# Project Structure

This package is split between reusable TitanTool code, bundled runtime support, documentation, and optional sample content. Keep new files close to the thing that owns them so the package stays reusable without dragging the demo project along.

## Core Tool

- `Runtime`: runtime graph execution, blackboard keys, boss director, target points, values, and runtime node implementations.
- `Runtime/Data/Signals`: package-owned default signal assets used by health, death, movement, and HUD bindings.
- `Editor`: GraphToolkit editor nodes, graph windows, compiler, validation, asset creation, debugger tooling, and icons.

## Shared Runtime Support

- `Runtime/Utility`: the minimal shared support code TitanTool currently needs, including signals, health, pooling, data assets, audio assets, and damage interfaces.
- `Runtime/Utility/Interfaces`: small runtime contracts used by pooled objects and damageable gameplay objects.
- `Editor/Icons`: editor/tool icons used by TitanTool.
- Keep package support types here only when TitanTool itself needs them. Demo-only gameplay scripts should stay in the consuming project or in a sample.

## Content

- `Samples~/Starter Data`: reusable starter graph data and target point keys for a new boss setup.
- `Samples~/Cuphead Example`: the Cuphead-style sample project. Sample scenes, sample graphs, sample target points, prefabs, animations, and sample-owned sprites belong here.
- `Samples~/Cuphead Example/Data/AllNodes.titan`: a reference graph that showcases the available node set.
- `Samples~/Cuphead Example/Data/Cuphead.titan`: the playable Cuphead-style boss graph.

## Documentation

- `Documentation~/index.md`: package documentation landing page.
- `Documentation~/TitanToolNodeGuide.md`: designer-facing node reference and graph-building guide.
- `Documentation~/ProjectStructure.md`: this folder ownership guide.

## Cleanup Rules

- Put reusable TitanTool behavior in `Runtime` or `Editor`; put playable demo logic in `Samples~/<SampleName>`.
- Put reusable/default data in `Samples~/Starter Data`; put sample-specific graphs in `Samples~/<SampleName>/Data`.
- Move Unity assets with their `.meta` files so scene and prefab references keep their GUIDs.
- Keep deleted or retired examples out of README/docs until they exist again as active package samples.
- Keep package docs written from the package consumer's point of view, not the source project's `Assets` layout.
