using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Cultiway.Abstract;

public interface IHasInventory
{
    public void         AddSpecialItem(Entity     item);
    public void         ExtractSpecialItem(Entity item);
    public List<Entity> GetItems();
}