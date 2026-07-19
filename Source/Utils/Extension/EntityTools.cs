using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Utils.Extension;

public static class EntityTools
{
    /// <summary>
    /// 判断实体是否仍然存在，且未处于未完成、占用、消耗或回收状态。
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>实体存在且可用时返回 true，否则返回 false。</returns>
    public static bool IsAvailable(this Entity entity)
    {
        return !entity.IsNull &&
               !entity.Tags.HasAny(Tags.Get<TagUncompleted, TagOccupied, TagConsumed, TagRecycle>());
    }
}
