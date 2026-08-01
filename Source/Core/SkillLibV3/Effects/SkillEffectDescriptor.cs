using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Effects;

/// <summary>结构化技能效果所作用的目标关系。</summary>
public enum SkillEffectTargetRelation
{
    /// <summary>只作用于敌对目标。</summary>
    Hostile,

    /// <summary>只作用于施法者本人、同国或同联盟单位。</summary>
    Friendly,

    /// <summary>只作用于施法者本人。</summary>
    Self,

    /// <summary>作用于世界地块，不枚举单位。</summary>
    WorldTile,
}

/// <summary>结构化效果的结算时机。</summary>
public enum SkillEffectTrigger
{
    /// <summary>技能命中对象或抵达落点时结算一次。</summary>
    Impact,

    /// <summary>技能实体存续期间按固定间隔重复结算。</summary>
    Periodic,
}

/// <summary>一次结构化效果实际造成的变化类别，供后续逻辑与视觉表现精确消费。</summary>
[System.Flags]
public enum SkillEffectOutcomeFlags : ulong
{
    /// <summary>没有产生任何变化。</summary>
    None = 0,

    /// <summary>恢复了单位生命。</summary>
    HealthRestored = 1UL << 0,

    /// <summary>成功施加或刷新了一个状态。</summary>
    StatusApplied = 1UL << 1,

    /// <summary>成功移除了一个状态。</summary>
    StatusRemoved = 1UL << 2,

    /// <summary>地块火焰被移除。</summary>
    FireRemoved = 1UL << 3,

    /// <summary>地块烧焦阶段被移除。</summary>
    BurnRemoved = 1UL << 4,

    /// <summary>地块冻结状态被移除。</summary>
    FrozenRemoved = 1UL << 5,

    /// <summary>地块热量被移除。</summary>
    HeatRemoved = 1UL << 6,

    /// <summary>荒地表层被移除。</summary>
    WastelandRemoved = 1UL << 7,

    /// <summary>地形层级被抬升。</summary>
    TerrainRaised = 1UL << 8,

    /// <summary>地形层级被降低。</summary>
    TerrainLowered = 1UL << 9,

    /// <summary>地块被填充为水体。</summary>
    WaterFilled = 1UL << 10,

    /// <summary>地块水体被排除。</summary>
    WaterDrained = 1UL << 11,

    /// <summary>地块上实际生成了植被。</summary>
    FloraCreated = 1UL << 12,

    /// <summary>在目标地块成功生成了原版雨云实体。</summary>
    RainCloudSummoned = 1UL << 13,

    /// <summary>尚未成熟的农作物被直接催熟。</summary>
    CropFertilized = 1UL << 14,
}

/// <summary>一次结构化效果的不可变结算结果。</summary>
public readonly struct SkillEffectResult
{
    /// <summary>本次结算产生的全部变化类别。</summary>
    public readonly SkillEffectOutcomeFlags Flags;

    /// <summary>实际发生变化的对象、状态或地块数量。</summary>
    public readonly int Count;

    /// <summary>本次变化的有效数值，例如实际治疗量或状态强度。</summary>
    public readonly float Magnitude;

    /// <summary>结算是否确实改变了游戏状态。</summary>
    public bool Changed => Flags != SkillEffectOutcomeFlags.None || Count > 0;

    /// <summary>表示没有产生变化的结果。</summary>
    public static SkillEffectResult None => default;

    /// <summary>创建一次确定发生的结算结果。</summary>
    public SkillEffectResult(SkillEffectOutcomeFlags flags, int count = 1, float magnitude = 0f)
    {
        Flags = flags;
        Count = Mathf.Max(0, count);
        Magnitude = magnitude;
    }

    /// <summary>合并两次结算结果，保留全部变化类别并累加数量与数值。</summary>
    public SkillEffectResult Merge(in SkillEffectResult other)
    {
        return new SkillEffectResult(Flags | other.Flags, Count + other.Count, Magnitude + other.Magnitude);
    }
}

/// <summary>技能效果执行时使用的只读上下文。</summary>
public readonly struct SkillEffectContext
{
    public readonly Entity SkillContainer;
    public readonly Entity SkillEntity;
    public readonly SkillContext Cast;
    public readonly Vector3 Position;
    public readonly float Radius;

    public SkillEffectContext(
        Entity skillContainer,
        Entity skillEntity,
        in SkillContext cast,
        Vector3 position,
        float radius)
    {
        SkillContainer = skillContainer;
        SkillEntity = skillEntity;
        Cast = cast;
        Position = position;
        Radius = Mathf.Max(0f, radius);
    }
}

/// <summary>技能效果在施放前进行目标或地块预检时使用的上下文。</summary>
public readonly struct SkillEffectEvaluationContext
{
    public readonly ActorExtend Caster;
    public readonly Entity SkillContainer;
    public readonly Vector3 Position;
    public readonly float Radius;

    public SkillEffectEvaluationContext(
        ActorExtend caster,
        Entity skillContainer,
        Vector3 position,
        float radius)
    {
        Caster = caster;
        SkillContainer = skillContainer;
        Position = position;
        Radius = Mathf.Max(0f, radius);
    }
}

public delegate bool SkillObjectEffectApplicability(
    in SkillEffectEvaluationContext context,
    BaseSimObject target);

public delegate SkillEffectResult SkillObjectEffectAction(in SkillEffectContext context, BaseSimObject target);

public delegate float SkillObjectEffectUtility(
    in SkillEffectEvaluationContext context,
    BaseSimObject target);

public delegate bool SkillTileEffectApplicability(
    in SkillEffectEvaluationContext context,
    WorldTile tile);

public delegate SkillEffectResult SkillTileEffectAction(in SkillEffectContext context, WorldTile tile);

public delegate float SkillTileEffectUtility(
    in SkillEffectEvaluationContext context,
    WorldTile tile);

/// <summary>
/// 描述一个可组合的技能效果。目标关系、预检、实际结算和 AI 边际收益在同一处声明，
/// 视觉元素不再承担玩法副作用。
/// </summary>
public sealed class SkillEffectDescriptor
{
    public string Id;
    public SkillEffectTargetRelation TargetRelation;
    public SkillEffectTrigger Trigger;
    public float Interval;
    public SkillObjectEffectApplicability CanApplyObject;
    public SkillObjectEffectAction ApplyObject;
    public SkillObjectEffectUtility EvaluateObjectUtility;
    public SkillTileEffectApplicability CanApplyTile;
    public SkillTileEffectAction ApplyTile;
    public SkillTileEffectUtility EvaluateTileUtility;

    public bool IsObjectEffect => ApplyObject != null;
    public bool IsTileEffect => ApplyTile != null;
    public bool IsPeriodic => Trigger == SkillEffectTrigger.Periodic;
}
