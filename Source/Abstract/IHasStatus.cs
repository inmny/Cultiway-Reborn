using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Cultiway.Abstract;

public interface IHasStatus
{
    public void         AddSharedStatus(Entity     item);
    public void RemoveSharedStatus(Entity item);
    public List<Entity> GetStatuses();
}