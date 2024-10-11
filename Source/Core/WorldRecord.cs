using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Utils;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class WorldRecord
{
    private readonly HashSet<string> _tags = new();

    public WorldRecord(EntityStore world)
    {
        E = world.CreateEntity();
    }

    public Entity E { get; private set; }

    public void CheckAndLogFirstLevelup<T>(string cultisys, int level, ActorExtend ae, ref T component)
        where T : ICultisysComponent
    {
        string key = $"{cultisys}_{level}";
        if (!_tags.Add(key)) return;

        WorldLogUtils.LogCultisysLevelup(ae, ref component);
    }

    public void AddStringTag(string tag)
    {
        _tags.Add(tag);
    }

    public bool HasStringTag(string tag)
    {
        return _tags.Contains(tag);
    }

    public void RemoveStringTag(string tag)
    {
        _tags.Remove(tag);
    }
}