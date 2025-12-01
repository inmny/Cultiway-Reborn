using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Cultiway.Abstract;

public interface IHasStatus
{
    /// <summary>
    /// 添加共享状态
    /// </summary>
    /// <param name="item">状态实体</param>
    /// <returns>是否添加成功</returns>
    public bool AddSharedStatus(Entity item);
    public void RemoveSharedStatus(Entity item);
    public List<Entity> GetStatuses();
}