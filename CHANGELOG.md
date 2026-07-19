# Changelog

## 0.2.0

- Added a Custom Scriptable Node action so projects can plug in ScriptableObject-backed boss behavior and optional polish-package adapters.
- Extended editor node registration to discover compatible custom graph nodes from other loaded assemblies.
- Added package default editor sound clips for graph open/create/detach, comment creation, and target point provider actions.
- Removed the Starter Data and Cuphead Example samples so the package ships one maintained Template Scene sample.
- Refreshed the packaged Template Scene `DefaultBoss.titan` from the current imported example project sample.
- Renamed Repeat Children to Repeat Sequence and added an After Completion option for restart vs remember-success behavior.
- Added graph value-source chips for constants, wires, variables, and blackboard keys.
- Added clearer composite-node wording for Run Together, Run In Order, and Repeat Sequence.
- Added Run Together behavior presets for fail-fast, any-success, wait-for-all, and first-result workflows.
- Improved runtime debug data with tick counts, status change timing, and status reasons.
- Updated the Runtime Debugger with tick count and reason columns.
- Added documentation for parallel presets and common example graph patterns.

## 0.1.1

- Refreshed package docs to describe the package layout instead of the in-project source layout.
- Updated the Cuphead sample data with the current `AllNodes.titan` graph.
- Updated the packaged editor icon to match the current `TTIcon.jpg` asset.

## 0.1.0

- Created the first package-ready TitanTool layout.
- Added package manifest metadata and sample declarations.
- Added runtime, editor, docs, Starter Data sample, and Cuphead Example sample.
- Bundled the minimal Utility support code required by TitanTool runtime nodes.
