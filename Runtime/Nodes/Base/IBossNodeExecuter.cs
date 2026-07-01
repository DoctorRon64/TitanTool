using System.Threading.Tasks;

namespace TitanTool.Runtime.Nodes.Base {
    public interface IBossNodeExecuter<T> {
        Task ExecuteAsync(T node, BossDirector ctx);
    }
}