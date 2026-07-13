using System;
using Unity.GraphToolkit.Editor;

namespace TitanTool.Editor.Nodes {
    [Serializable]
    [UseWithGraph(typeof(BossGraph))]
    [GraphNode(typeof(TitanTool.Runtime.Nodes.Custom.ShootNode), "Shoot", "Action/", BossGraphNodeCategory.Action, BossGraphNodeIcons.Shoot, "Fires bullet or projectile prefabs from the selected source using single, burst, or spread patterns.", "bullet bullets projectile projectiles fire shoot pattern spread")]
    public class BulletNode : ShootNode {
    }
}
