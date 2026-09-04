using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content.CreatureCompositions.Combat;

/// <summary>一条等待延后执行的器官后果。</summary>
public readonly struct CreatureConsequenceEntry
{
    /// <summary>创建一条后果记录。</summary>
    public CreatureConsequenceEntry(Entity owner, int revision, string effectFamilyId, string slotId,
                                    string organId, long targetActorId, float value)
    {
        Owner = owner;
        Revision = revision;
        EffectFamilyId = effectFamilyId;
        SlotId = slotId;
        OrganId = organId;
        TargetActorId = targetActorId;
        Value = value;
    }

    /// <summary>后果所属的生物实体；回收后过期条目会被直接丢弃。</summary>
    public Entity Owner { get; }

    /// <summary>提交后果时的身体变更序号，用于丢弃不属于当前身体的过期条目。</summary>
    public int Revision { get; }

    /// <summary>产生后果的效果类别编号。</summary>
    public string EffectFamilyId { get; }

    /// <summary>来源器官占用的身体位置。</summary>
    public string SlotId { get; }

    /// <summary>来源器官编号。</summary>
    public string OrganId { get; }

    /// <summary>关联目标（例如被吞噬者）的编号；没有目标时为 -1。</summary>
    public long TargetActorId { get; }

    /// <summary>提交时已经算好的数值载荷。</summary>
    public float Value { get; }
}

/// <summary>延后后果的实际处理体；在主循环安全时机被调用。</summary>
public delegate void CreatureConsequenceProcessor(in CreatureConsequenceEntry entry);

/// <summary>
///     数量受限的器官后果队列。保命机会已经同步预留后，或者其他反应需要改变结构时，
///     再把后续动作放进队列；同一生物单位同一类别最多一条。
/// </summary>
public static class CreatureConsequenceQueue
{
    private const int Capacity = 256;

    private static readonly List<CreatureConsequenceEntry> entries = new(Capacity);
    private static readonly Dictionary<string, CreatureConsequenceProcessor> processors =
        new(StringComparer.Ordinal);

    /// <summary>登记一个效果类别的延后处理体；重复登记会替换旧处理体。</summary>
    public static void RegisterProcessor(string effectFamilyId, CreatureConsequenceProcessor processor)
    {
        if (string.IsNullOrEmpty(effectFamilyId) || processor == null) return;
        processors[effectFamilyId] = processor;
    }

    /// <summary>尝试排队一条后果；队列已满或类别没有处理体时直接丢弃并返回假。</summary>
    public static bool TryEnqueue(CreatureConsequenceEntry entry)
    {
        if (string.IsNullOrEmpty(entry.EffectFamilyId) || !processors.ContainsKey(entry.EffectFamilyId))
            return false;
        if (entries.Count >= Capacity) return false;

        // 同一生物单位同一类别未处理后果最多一条；新条目替换旧条目。
        for (int i = 0; i < entries.Count; i++)
        {
            CreatureConsequenceEntry existing = entries[i];
            if (existing.Owner.Id == entry.Owner.Id &&
                string.Equals(existing.EffectFamilyId, entry.EffectFamilyId, StringComparison.Ordinal))
            {
                entries[i] = entry;
                return true;
            }
        }

        entries.Add(entry);
        return true;
    }

    /// <summary>在安全时机处理全部排队后果；由每帧系统调用。</summary>
    public static void Flush()
    {
        if (entries.Count == 0) return;

        // 倒序消费，处理体可以直接继续排队新的后果而不影响本次遍历。
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            CreatureConsequenceEntry entry = entries[i];
            entries.RemoveAt(i);
            if (entry.Owner.IsNull || !entry.Owner.HasComponent<ActorBinder>()) continue;
            if (!processors.TryGetValue(entry.EffectFamilyId, out CreatureConsequenceProcessor processor))
                continue;
            processor(entry);
        }
    }

    /// <summary>清理世界时直接清空队列与登记的处理体。</summary>
    public static void ClearWorldState()
    {
        entries.Clear();
        processors.Clear();
    }
}
