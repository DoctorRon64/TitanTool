using TitanTool.Runtime.Nodes.Base;

namespace TitanTool.Editor {
    public interface IRuntimeNodeCompiler {
        void Compile(Node runtimeNode);
    }
}