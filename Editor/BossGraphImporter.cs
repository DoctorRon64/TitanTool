using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using RuntimeNode = TitanTool.Runtime.Nodes.Base.Node;

namespace TitanTool.Editor {
    [ScriptedImporter(1, BossGraph.ASSET_EXTENSION)]
    public class BossGraphImporter : ScriptedImporter {
        public override void OnImportAsset(AssetImportContext ctx) {
            BossGraph graph = GraphDatabase.LoadGraphForImporter<BossGraph>(ctx.assetPath);
            if (graph == null) {
                ctx.LogImportError($"Failed to load BossGraph: {ctx.assetPath}");
                return;
            }

            graph.SetAssetPath(ctx.assetPath);
            BossGraphCompileResult result = BossGraphCompiler.Compile(graph);

            foreach (RuntimeNode runtimeNode in result.runtimeNodes)
                ctx.AddObjectToAsset(runtimeNode.guid, runtimeNode);

            ctx.AddObjectToAsset("Runtime", result.runtimeAsset);
            ctx.SetMainObject(result.runtimeAsset);
        }
    }
}
