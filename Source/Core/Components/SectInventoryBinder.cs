using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Core.Components;

/// <summary>
/// 将宗门运行期库存实体绑定到对应宗门。
/// </summary>
public struct SectInventoryBinder(long id) : IComponent
{
    public readonly long ID = id;

    [Ignore]
    public Sect Sect => WorldboxGame.I?.Sects?.get(ID);
}
