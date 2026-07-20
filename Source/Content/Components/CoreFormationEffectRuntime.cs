using System;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>单个效果族在角色身上的有界运行时状态。</summary>
public struct CoreFormationEffectRuntimeEntry
{
    /// <summary>与效果定义对应的稳定效果族 ID。</summary>
    public string family_id;

    /// <summary>当前生效定义的覆盖等级。</summary>
    public int rank;

    /// <summary>普通触发的剩余内部冷却秒数。</summary>
    public float cooldown_remaining;

    /// <summary>主动能力的剩余冷却秒数。</summary>
    public float active_cooldown_remaining;

    /// <summary>主动形态或持续状态的剩余秒数。</summary>
    public float active_remaining;

    /// <summary>延迟恢复、相位切换等机制共用的辅助计时器。</summary>
    public float auxiliary_timer;

    /// <summary>护盾、储备、累计伤害等机制的主浮点状态。</summary>
    public float value;

    /// <summary>恢复速率、上次伤害时间等机制的次浮点状态。</summary>
    public float secondary_value;

    /// <summary>层数、连续命中次数等机制的整数状态。</summary>
    public int counter;

    /// <summary>五相轮转等机制的当前相位。</summary>
    public int phase;

    /// <summary>凝元蓄力、灵台回响等机制的可消费次数。</summary>
    public int charges;

    /// <summary>需要跨逻辑帧关联时保存的单位 ID；零表示没有目标。</summary>
    public long target_id;

    /// <summary>清理不应跨定义升级保留的瞬时状态。</summary>
    public void ResetTransientState()
    {
        active_remaining = 0f;
        auxiliary_timer = 0f;
        value = 0f;
        secondary_value = 0f;
        counter = 0;
        phase = 0;
        charges = 0;
        target_id = 0;
    }
}

/// <summary>角色实体保存的核心形成效果运行时；数组长度由解析器限制。</summary>
public struct CoreFormationEffectRuntime : IComponent
{
    /// <summary>运行时上限，防止异常快照无限扩大角色组件。</summary>
    public const int MaxEntries = 12;

    /// <summary>最近一次同步使用的形成组合签名。</summary>
    public string signature;

    /// <summary>按合并后效果族稳定排序的运行时状态。</summary>
    public CoreFormationEffectRuntimeEntry[] entries;

    /// <summary>取得指定效果族的数组索引；不存在时返回 -1。</summary>
    public readonly int FindIndex(string familyId)
    {
        if (string.IsNullOrEmpty(familyId) || entries == null) return -1;
        for (var i = 0; i < entries.Length; i++)
            if (string.Equals(entries[i].family_id, familyId, StringComparison.Ordinal)) return i;
        return -1;
    }
}
