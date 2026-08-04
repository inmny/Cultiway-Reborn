using System;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Semantics;

namespace Cultiway.Content.Libraries;

/// <summary>修炼方式可以同时响应的触发阶段。</summary>
[Flags]
public enum CultivationTriggerKind
{
    /// <summary>不响应任何触发。</summary>
    None = 0,

    /// <summary>角色执行明确的修炼行为时触发。</summary>
    ActiveTick = 1 << 0,

    /// <summary>无需占用角色工作的定时修炼结算。</summary>
    TimedTick = 1 << 1,

    /// <summary>角色对其他单位造成最终有效伤害后触发。</summary>
    DamageDealt = 1 << 2,

    /// <summary>角色承受其他单位造成的最终有效伤害后触发。</summary>
    DamageTaken = 1 << 3,

    /// <summary>角色完成一次击杀后触发。</summary>
    Kill = 1 << 4,

    /// <summary>角色完成由内容系统认定的善行后触发。</summary>
    GoodDeed = 1 << 5,

    /// <summary>角色在主动雷霆淬体期间承受无来源天雷的实际伤害后触发。</summary>
    HeavenlyLightningDamage = 1 << 6
}

/// <summary>主动修炼触发时所处的具体活动环境。</summary>
public enum CultivationActivityKind
{
    /// <summary>不是主动修炼行为。</summary>
    None,

    /// <summary>普通闭关或室内吐纳。</summary>
    Meditation,

    /// <summary>在功法指定的自然环境中吐纳修炼。</summary>
    EnvironmentalMeditation,

    /// <summary>植物式野外修炼，以净化浊气为能量来源。</summary>
    PlantPurification
}

/// <summary>一次修炼触发的只读上下文。</summary>
public readonly struct CultivationTriggerContext
{
    /// <summary>创建一条不允许调用方后续修改的修炼触发记录。</summary>
    public CultivationTriggerContext(
        ActorExtend practitioner,
        CultivationTriggerKind trigger,
        CultivationActivityKind activity = CultivationActivityKind.None,
        float elapsedSeconds = 0f,
        float actualDamage = 0f,
        float practitionerPower = 0f,
        float opponentPower = 0f,
        float referenceMaxHealth = 0f,
        int tileX = -1,
        int tileY = -1)
    {
        Practitioner = practitioner;
        Trigger = trigger;
        Activity = activity;
        ElapsedSeconds = elapsedSeconds;
        ActualDamage = actualDamage;
        PractitionerPower = practitionerPower;
        OpponentPower = opponentPower;
        ReferenceMaxHealth = referenceMaxHealth;
        TileX = tileX;
        TileY = tileY;
    }

    /// <summary>获得本次修炼收益的角色。</summary>
    public ActorExtend Practitioner { get; }

    /// <summary>本次进入规则的触发阶段。</summary>
    public CultivationTriggerKind Trigger { get; }

    /// <summary>主动修炼时的环境类型。</summary>
    public CultivationActivityKind Activity { get; }

    /// <summary>本次定时或主动结算覆盖的秒数。</summary>
    public float ElapsedSeconds { get; }

    /// <summary>伤害触发中已经通过最终伤害层的实际伤害。</summary>
    public float ActualDamage { get; }

    /// <summary>事件发生时修炼者的战力层级。</summary>
    public float PractitionerPower { get; }

    /// <summary>事件发生时对手的战力层级。</summary>
    public float OpponentPower { get; }

    /// <summary>伤害占比计算所使用的受击者生命上限。</summary>
    public float ReferenceMaxHealth { get; }

    /// <summary>事件发生地块的横坐标；不涉及地块时为 -1。</summary>
    public int TileX { get; }

    /// <summary>事件发生地块的纵坐标；不涉及地块时为 -1。</summary>
    public int TileY { get; }
}

/// <summary>处理一种修炼方式在指定触发下的完整规则。</summary>
public delegate void CultivationMethodRule(in CultivationTriggerContext context);

/// <summary>修炼方式资产。</summary>
public class CultivateMethodAsset : Asset
{
    /// <summary>修炼方式本身表达的路径语义。</summary>
    public SemanticDescriptor Semantics = new();

    /// <summary>检查角色是否满足使用该方式的身份与环境前置条件。</summary>
    public Func<ActorExtend, bool> CanCultivate;

    /// <summary>计算方式自身的环境倍率，不包含灵根资质。</summary>
    public Func<ActorExtend, float> GetMethodMultiplier;

    /// <summary>为自动生成功法提供该方式对角色的额外适合度。</summary>
    public Func<ActorExtend, float> GetSelectionScore;

    /// <summary>返回该方式需要占用角色执行的行为任务；为空表示不需要专门工作。</summary>
    public Func<ActorExtend, string> GetBehaviourJobId;

    /// <summary>非空时表示该方式由统一环境选址与修炼行为驱动。</summary>
    public CultivationEnvironmentRule EnvironmentRule;

    /// <summary>该方式可以同时响应的全部触发阶段。</summary>
    public CultivationTriggerKind TriggerKinds;

    /// <summary>执行该方式的资源结算、实践累计及副作用。</summary>
    public CultivationMethodRule Execute;

    /// <summary>判断该方式是否响应指定触发阶段。</summary>
    public bool Handles(CultivationTriggerKind trigger)
    {
        return (TriggerKinds & trigger) != 0;
    }
}
