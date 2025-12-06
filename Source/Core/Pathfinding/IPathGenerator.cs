using System.Threading;
using System.Threading.Tasks;

namespace Cultiway.Core.Pathfinding;

public interface IPathGenerator
{
    Task GenerateAsync(PathRequest request, IPathStreamWriter stream, CancellationToken cancellationToken);
}
