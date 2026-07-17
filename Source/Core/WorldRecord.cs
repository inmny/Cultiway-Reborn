using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Utils;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class WorldRecord
{
    private readonly HashSet<string> recordKeys = new();

    public WorldRecord(EntityStore world)
    {
        E = world.CreateEntity();
    }

    public Entity E { get; private set; }

    public void CheckAndLogFirstLevelup<T>(string cultisys, ActorExtend ae, ref T component)
        where T : ICultisysComponent
    {
        string key = $"{cultisys}_{component.CurrLevel}";
        if (!recordKeys.Add(key) && !ae.Base.isFavorite()) return;

        WorldLogUtils.LogCultisysLevelup(ae, ref component);
    }

    public void AddKey(string key)
    {
        recordKeys.Add(key);
    }

    public bool HasKey(string key)
    {
        return recordKeys.Contains(key);
    }

    public void RemoveKey(string key)
    {
        recordKeys.Remove(key);
    }
}
