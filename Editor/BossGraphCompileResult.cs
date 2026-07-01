using System.Collections.Generic;
using System.Linq;
using TitanTool.Runtime.Data;
using TitanTool.Runtime.Nodes.Base;

namespace TitanTool.Editor {
    public sealed class BossGraphCompileResult {
        public BossGraphCompileResult(BossGraphAsset runtimeAsset, IEnumerable<Node> runtimeNodes, IEnumerable<BossGraphValidationIssue> issues) {
            this.runtimeAsset = runtimeAsset;
            this.runtimeNodes = runtimeNodes?.ToList() ?? new List<Node>();
            this.issues = issues?.ToList() ?? new List<BossGraphValidationIssue>();
        }

        public BossGraphAsset runtimeAsset { get; }
        public IReadOnlyList<Node> runtimeNodes { get; }
        public IReadOnlyList<BossGraphValidationIssue> issues { get; }
        public bool hasErrors => issues.Any(issue => issue.severity == BossGraphValidationSeverity.Error);
    }
}
