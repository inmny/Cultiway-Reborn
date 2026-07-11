using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Utils.Extension;

public static class EntityTools
{
    /// <summary>
    /// 判断实体是否可用
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>是否可用</returns>
    public static bool IsAvailable(this Entity entity)
    {
        return !entity.Tags.HasAny(Tags.Get<TagUncompleted, TagOccupied, TagConsumed, TagRecycle>());
    }
}