using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Utils;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class WorldRecord
{
    private readonly HashSet<string> recordKeys = new();
    private readonly EntityStore world;
    private Entity e;

    public WorldRecord(EntityStore world)
    {
        this.world = world;
    }

    public Entity E => e.IsNull ? e = world.CreateEntity() : e;

    /// <summary>保存世界中已经发生过的首次事件，防止读取后重复公告。</summary>
    internal string[] CaptureKeys()
    {
        var keys = new string[recordKeys.Count];
        recordKeys.CopyTo(keys);
        System.Array.Sort(keys, System.StringComparer.Ordinal);
        return keys;
    }

    /// <summary>人物恢复之前接回世界历史标记。</summary>
    /// <param name="keys">当前操作使用的资料。</param>
    internal void RestoreKeys(IEnumerable<string> keys)
    {
        recordKeys.Clear();
        recordKeys.UnionWith(keys);
    }

    public void ClearWorldState()
    {
        recordKeys.Clear();
        if (!e.IsNull) e.DeleteEntity();
        e = default;
    }

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
