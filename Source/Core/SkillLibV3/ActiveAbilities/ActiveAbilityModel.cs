using System;
using System.Collections.Generic;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.SkillLibV3.Usage;
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

/// <summary>
/// 主动能力在释放期间对施法者移动的约束。
/// </summary>
public enum ActiveAbilityCastMobility
{
    /// <summary>释放动作不阻断现有移动。</summary>
    Mobile,
    /// <summary>释放成功后短暂停止平滑位移，但不清除现有路径。</summary>
    BriefStop,
    /// <summary>从释放成功到公共攻击恢复结束期间冻结平滑位移。</summary>
    StationaryDuringRecovery,
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
    public readonly ActiveAbilityCastMobility CastMobility;
    public readonly SkillUseTargetRelation TargetRelation;

    public ActiveAbilityDescriptor(
        string name,
        Sprite icon,
        ActiveAbilityChannel channels,
        ActiveAbilityTargetMode targetMode,
        ActiveAbilityActivationMode activationMode,
        ActiveAbilityCastMobility castMobility = ActiveAbilityCastMobility.Mobile,
        SkillUseTargetRelation? targetRelation = null)
    {
        Name = name ?? string.Empty;
        Icon = icon;
        Channels = channels;
        TargetMode = targetMode;
        ActivationMode = activationMode;
        CastMobility = castMobility;
        TargetRelation = targetRelation ?? (targetMode == ActiveAbilityTargetMode.Self
            ? SkillUseTargetRelation.Self
            : SkillUseTargetRelation.Hostile);
    }
}

/// <summary>
/// 主动能力向通用战斗规划器公开的用途和强度，不包含具体体系类型。
/// </summary>
public readonly struct ActiveAbilityTacticalProfile
{
    public readonly float Offensive;
    public readonly float Defensive;
    public readonly float Support;
    public readonly float Control;
    public readonly float Power;
    public readonly float ResourceDemand;
    public readonly float ExpectedTargets;
    public readonly SkillImpactKind? ImpactKind;
    /// <summary>不带用途含义的通用效用评级。</summary>
    public readonly float Utility;

    public ActiveAbilityTacticalProfile(
        float offensive,
        float defensive,
        float support,
        float control,
        float power,
        float resourceDemand,
        float expectedTargets,
        SkillImpactKind? impactKind = null,
        float utility = 0f)
    {
        Offensive = Mathf.Max(0f, offensive);
        Defensive = Mathf.Max(0f, defensive);
        Support = Mathf.Max(0f, support);
        Control = Mathf.Max(0f, control);
        Power = Mathf.Max(0f, power);
        ResourceDemand = Mathf.Max(0f, resourceDemand);
        ExpectedTargets = Mathf.Max(1f, expectedTargets);
        ImpactKind = impactKind;
        Utility = Mathf.Max(0f, utility);
    }

    /// <summary>
    /// 将技能容器的通用评级与命中形态转换为战术画像，供不同来源的主动能力共享同一口径。
    /// </summary>
    public static ActiveAbilityTacticalProfile FromSkill(
        Entity skill,
        float potency = 1f,
        bool forceDefensive = false)
    {
        if (skill.IsNull || !skill.HasComponent<SkillContainer>()) return default;

        SkillEntityAsset asset = skill.GetComponent<SkillContainer>().Asset;
        SkillImpactProfileAsset impact = asset.ImpactProfile;
        bool evaluated = SkillContainerEvaluator.TryEvaluate(
            skill,
            out SkillEvaluationResult evaluation);
        float fallbackPower = Mathf.Max(0.1f, impact.DamageMultiplier);
        float power = (evaluated
            ? Mathf.Max(0.1f, evaluation.PowerScore)
            : fallbackPower) * Mathf.Max(0f, potency);
        float directPower = (evaluated
            ? Mathf.Max(0.1f, evaluation.DirectPower)
            : fallbackPower) * Mathf.Max(0f, potency);
        bool defensive = forceDefensive ||
                         asset.Type == SkillEntityType.Defense ||
                         impact.Kind is SkillImpactKind.Wall or SkillImpactKind.Shield;
        float utility = evaluated ? evaluation.Utility : 0f;
        var semantics = new HashSet<SemanticAsset>();
        SkillSemanticCollector.CollectAssetSemantics(asset, semantics);
        SkillSemanticCollector.CollectModifierSemantics(skill, semantics);
        SkillSemanticCollector.CollectTrajectorySemantics(asset, skill, semantics);
        bool explicitSupport = semantics.Contains(SkillSemantics.Role.Support);
        bool explicitDefense = semantics.Contains(SkillSemantics.Role.Defensive);
        bool explicitOffense = semantics.Contains(SkillSemantics.Role.Offensive);
        bool utilityOnly = asset.Type == SkillEntityType.Utility ||
                           semantics.Contains(SkillSemantics.Role.Utility);
        defensive |= explicitDefense;
        float support = explicitSupport
            ? Mathf.Max(0.25f, Mathf.Max(utility, power * 0.5f))
            : 0f;
        float offense = explicitOffense || (!defensive && !explicitSupport && !utilityOnly)
            ? directPower
            : 0f;

        return new ActiveAbilityTacticalProfile(
            offense,
            defensive ? power : 0f,
            support,
            evaluated ? evaluation.Control : 0f,
            power,
            evaluated ? evaluation.ResourceDemandPerStep : 0f,
            evaluated && evaluation.ExpectedTargets > 0f
                ? evaluation.ExpectedTargets
                : impact.ExpectedTargets,
            impact.Kind,
            utility);
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
    public readonly SkillCastRuntimeData RuntimeData;

    public ActiveAbilityTarget(
        BaseSimObject target,
        Vector3 position,
        SkillTargetSelectionArea selectionArea = default,
        IReadOnlyList<BaseSimObject> explicitTargets = null,
        Kingdom attackKingdom = null,
        SkillCastRuntimeData runtimeData = default)
    {
        Object = target;
        Position = position;
        SelectionArea = selectionArea;
        ExplicitTargets = explicitTargets;
        AttackKingdom = attackKingdom;
        RuntimeData = runtimeData;
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

    ActiveAbilityTacticalProfile ResolveTacticalProfile(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        BaseSimObject target);

    float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target);

    /// <summary>返回能力在落点处实际影响的半径；0 表示没有固定范围预览。</summary>
    float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle);

    bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin);
}

/// <summary>可选的主动能力目标顾问，用于在主线程按能力自身效果选择具体友军。</summary>
public interface IActiveAbilityTargetAdvisor
{
    bool TryResolvePreferredTarget(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        IReadOnlyList<Actor> nearbyAllies,
        out BaseSimObject target);
}
