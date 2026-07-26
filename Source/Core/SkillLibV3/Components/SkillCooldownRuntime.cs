using System;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>单个技能在一个持有者身上的剩余冷却。</summary>
[Serializable]
public struct SkillCooldownEntry
{
    /// <summary>具体技能容器的持久实体 ID。</summary>
    public long SkillContainerPid;

    /// <summary>剩余冷却秒数。</summary>
    public float Remaining;
}

/// <summary>角色持有的通用技能冷却表；仅在首次进入冷却时创建。</summary>
public struct SkillCooldownRuntime : IComponent
{
    /// <summary>按具体技能容器的持久实体 ID 唯一保存的冷却项。</summary>
    public SkillCooldownEntry[] Entries;

    /// <summary>查找指定技能容器的冷却项；不存在时返回 -1。</summary>
    public readonly int FindIndex(long skillContainerPid)
    {
        if (skillContainerPid == 0 || Entries == null) return -1;
        for (var i = 0; i < Entries.Length; i++)
        {
            if (Entries[i].SkillContainerPid == skillContainerPid) return i;
        }
        return -1;
    }
}
