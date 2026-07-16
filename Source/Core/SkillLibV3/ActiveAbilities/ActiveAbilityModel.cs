using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.ActiveAbilities;

[Flags]
public enum ActiveAbilityChannel
{
    None = 0,
    Combat = 1 << 0,
    World = 1 << 1,
}

public enum ActiveAbilityTargetMode
{
    None,
    Self,
    Object,
    Point,
    ObjectOrPoint,
    Area,
}

public enum ActiveAbilityActivationMode
{
    Instant,
    Sustained,
    Toggle,
}

public enum ActiveAbilityUseOrigin
{
    Autonomous,
    Player,
    Script,
}

/// <summary>
/// 指向某个 Provider 所暴露的具体主动能力实例。Source 是能力来源实体，EntryId 区分同一来源上的多个能力。
/// </summary>
public readonly struct ActiveAbilityHandle : IEquatable<ActiveAbilityHandle>
{
    public readonly string ProviderId;
    public readonly Entity Source;
    public readonly string EntryId;

    public ActiveAbilityHandle(string providerId, Entity source, string entryId = "")
    {
        ProviderId = providerId;
        Source = source;
        EntryId = entryId ?? string.Empty;
    }

    public bool Equals(ActiveAbilityHandle other)
    {
        return ProviderId == other.ProviderId && Source == other.Source && EntryId == other.EntryId;
    }

    public override bool Equals(object obj)
    {
        return obj is ActiveAbilityHandle other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = ProviderId?.GetHashCode() ?? 0;
            hash = hash * 397 ^ Source.GetHashCode();
            return hash * 397 ^ (EntryId?.GetHashCode() ?? 0);
        }
    }

    public static bool operator ==(ActiveAbilityHandle left, ActiveAbilityHandle right) => left.Equals(right);
    public static bool operator !=(ActiveAbilityHandle left, ActiveAbilityHandle right) => !left.Equals(right);
}

/// <summary>
/// 主动能力向玩家控制和 AI 决策公开的稳定描述。
/// </summary>
public readonly struct ActiveAbilityDescriptor
{
    public readonly string Name;
    public readonly Sprite Icon;
    public readonly ActiveAbilityChannel Channels;
    public readonly ActiveAbilityTargetMode TargetMode;
    public readonly ActiveAbilityActivationMode ActivationMode;

    public ActiveAbilityDescriptor(
        string name,
        Sprite icon,
        ActiveAbilityChannel channels,
        ActiveAbilityTargetMode targetMode,
        ActiveAbilityActivationMode activationMode)
    {
        Name = name ?? string.Empty;
        Icon = icon;
        Channels = channels;
        TargetMode = targetMode;
        ActivationMode = activationMode;
    }
}

/// <summary>
/// 一次主动能力释放所使用的目标。不同 Provider 只读取自身 TargetMode 需要的字段。
/// </summary>
public readonly struct ActiveAbilityTarget
{
    public readonly BaseSimObject Object;
    public readonly Vector3 Position;
    public readonly SkillTargetSelectionArea SelectionArea;
    public readonly IReadOnlyList<BaseSimObject> ExplicitTargets;
    public readonly Kingdom AttackKingdom;

    public ActiveAbilityTarget(
        BaseSimObject target,
        Vector3 position,
        SkillTargetSelectionArea selectionArea = default,
        IReadOnlyList<BaseSimObject> explicitTargets = null,
        Kingdom attackKingdom = null)
    {
        Object = target;
        Position = position;
        SelectionArea = selectionArea;
        ExplicitTargets = explicitTargets;
        AttackKingdom = attackKingdom;
    }
}

/// <summary>
/// 主动能力来源适配器。Core 只依赖这份协议，具体法器、消耗品或其他 Content 类型由各自 Provider 解释。
/// </summary>
public interface IActiveAbilityProvider
{
    string Id { get; }

    void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output);

    ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle);

    ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle);

    bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target);

    bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target);

    int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target);

    float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target);

    /// <summary>返回能力在落点处实际影响的半径；0 表示没有固定范围预览。</summary>
    float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle);

    bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin);
}
