using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Impacts;
using UnityEngine;

namespace Cultiway.Core.Combat.Tactical;

/// <summary>
/// 角色当前希望通过战斗行为达成的目的。意图不直接等同于移动或攻击动作。
/// </summary>
public enum CombatIntent
{
    None,
    Engage,
    Hold,
    Reposition,
    Protect,
    Regroup,
    Disengage,
}

/// <summary>
/// 军队或外部系统向战术层施加的约束。角色可以在约束内自行选择目标和站位。
/// </summary>
public enum CombatDirective
{
    Hold,
    Attack,
    Protect,
    Retreat,
}

/// <summary>
/// 根据角色当前可执行动作动态解析的战斗职责。
/// </summary>
public enum CombatRole
{
    Melee,
    Ranged,
    Skirmisher,
    Controller,
    Support,
}

/// <summary>
/// 站位候选在战术上的来源。规划器据此区分普通交战点与撤退、集结目的地。
/// </summary>
public enum CombatPositionRole
{
    Tactical,
    Safe,
    AllyRally,
    CaptainRally,
    CityRetreat,
}

/// <summary>
/// 战斗动作进入执行层后的确定性结果。
/// </summary>
public enum CombatExecutionStatus
{
    Started,
    TemporarilyBlocked,
    Invalid,
}

/// <summary>
/// 战斗动作成功启动后对角色路径推进的约束。
/// </summary>
public enum CombatActionMovementMode
{
    /// <summary>动作与移动可以同时进行。</summary>
    Mobile,
    /// <summary>短暂冻结平滑位移，但保留已经计算好的路径。</summary>
    BriefStop,
    /// <summary>公共攻击恢复结束前冻结平滑位移并保留路径。</summary>
    StationaryDuringRecovery,
}

/// <summary>
/// 动作的主要战术用途。一个动作可以同时具备多种用途。
/// </summary>
[Flags]
public enum CombatActionPurpose
{
    None = 0,
    Offense = 1 << 0,
    Defense = 1 << 1,
    Support = 1 << 2,
    Control = 1 << 3,
    Mobility = 1 << 4,
    Barrier = 1 << 5,
    Field = 1 << 6,
    Advance = 1 << 7,
    Escape = 1 << 8,
}

/// <summary>
/// 区分同一角色拥有的具体动作实例。来源实体使用 PID，避免普通实体 ID 复用造成冷却串联。
/// </summary>
public readonly struct CombatActionKey : IEquatable<CombatActionKey>
{
    public readonly string ProviderId;
    public readonly long SourcePid;
    public readonly string EntryId;

    public CombatActionKey(string providerId, long sourcePid, string entryId)
    {
        ProviderId = providerId ?? string.Empty;
        SourcePid = sourcePid;
        EntryId = entryId ?? string.Empty;
    }

    public bool Equals(CombatActionKey other)
    {
        return ProviderId == other.ProviderId &&
               SourcePid == other.SourcePid &&
               EntryId == other.EntryId;
    }

    public override bool Equals(object obj)
    {
        return obj is CombatActionKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = ProviderId?.GetHashCode() ?? 0;
            hash = hash * 397 ^ SourcePid.GetHashCode();
            return hash * 397 ^ (EntryId?.GetHashCode() ?? 0);
        }
    }

    public static bool operator ==(CombatActionKey left, CombatActionKey right) => left.Equals(right);
    public static bool operator !=(CombatActionKey left, CombatActionKey right) => !left.Equals(right);

    public override string ToString()
    {
        return $"{ProviderId}:{SourcePid}:{EntryId}";
    }
}

/// <summary>
/// 规划器使用的动作数值画像。字段只描述战术价值，不持有实时游戏对象。
/// </summary>
public readonly struct CombatActionProfile
{
    public readonly CombatActionPurpose Purpose;
    public readonly ActiveAbilityTargetMode TargetMode;
    public readonly SkillImpactKind? ImpactKind;
    public readonly float MinRange;
    public readonly float MaxRange;
    public readonly float PreferredRange;
    public readonly float EffectRadius;
    public readonly float ExpectedTargets;
    public readonly float Power;
    public readonly float Control;
    public readonly float Utility;
    public readonly float ResourceCost;
    public readonly float Cooldown;
    public readonly float Reliability;
    public readonly int BaseWeight;
    public readonly CombatActionMovementMode MovementMode;

    public CombatActionProfile(
        CombatActionPurpose purpose,
        ActiveAbilityTargetMode targetMode,
        SkillImpactKind? impactKind,
        float minRange,
        float maxRange,
        float preferredRange,
        float effectRadius,
        float expectedTargets,
        float power,
        float control,
        float utility,
        float resourceCost,
        float cooldown,
        float reliability,
        int baseWeight,
        CombatActionMovementMode movementMode)
    {
        Purpose = purpose;
        TargetMode = targetMode;
        ImpactKind = impactKind;
        MinRange = Mathf.Max(0f, minRange);
        MaxRange = Mathf.Max(MinRange, maxRange);
        PreferredRange = Mathf.Clamp(preferredRange, MinRange, MaxRange);
        EffectRadius = Mathf.Max(0f, effectRadius);
        ExpectedTargets = Mathf.Max(1f, expectedTargets);
        Power = Mathf.Max(0f, power);
        Control = Mathf.Max(0f, control);
        Utility = Mathf.Max(0f, utility);
        ResourceCost = Mathf.Clamp01(resourceCost);
        Cooldown = Mathf.Max(0f, cooldown);
        Reliability = Mathf.Clamp01(reliability);
        BaseWeight = Math.Max(0, baseWeight);
        MovementMode = movementMode;
    }

    public bool HasPurpose(CombatActionPurpose purpose)
    {
        return (Purpose & purpose) != 0;
    }
}

/// <summary>
/// 主线程收集出的可执行动作。Provider 负责在提交时重新验证来源、目标和资源。
/// </summary>
public sealed class CombatActionCandidate
{
    public readonly ICombatActionProvider Provider;
    public readonly CombatActionKey Key;
    public readonly CombatActionProfile Profile;
    public readonly object Payload;
    /// <summary>该动作在快照创建时是否已经结束独立冷却。</summary>
    public readonly bool IsReady;

    public CombatActionCandidate(
        ICombatActionProvider provider,
        CombatActionKey key,
        CombatActionProfile profile,
        object payload,
        bool isReady = true)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Key = key;
        Profile = profile;
        Payload = payload;
        IsReady = isReady;
    }

    /// <summary>复制动作候选并写入本轮冷却快照，不修改 Provider 创建的原对象。</summary>
    internal CombatActionCandidate WithReadiness(bool isReady)
    {
        return IsReady == isReady
            ? this
            : new CombatActionCandidate(Provider, Key, Profile, Payload, isReady);
    }
}

/// <summary>
/// Provider 收集动作时可读取的主线程上下文。
/// </summary>
public readonly struct CombatActionCollectionContext
{
    public readonly ActorExtend Caster;
    public readonly BaseSimObject PrimaryEnemy;
    public readonly Actor PreferredAlly;
    public readonly float ThreatRatio;

    public CombatActionCollectionContext(
        ActorExtend caster,
        BaseSimObject primaryEnemy,
        Actor preferredAlly,
        float threatRatio)
    {
        Caster = caster;
        PrimaryEnemy = primaryEnemy;
        PreferredAlly = preferredAlly;
        ThreatRatio = threatRatio;
    }
}

/// <summary>
/// 动作执行时使用的实时上下文。执行前必须由 Provider 再次检查所有可变条件。
/// </summary>
public readonly struct CombatActionExecutionContext
{
    public readonly ActorExtend Caster;
    public readonly BaseSimObject PrimaryEnemy;
    public readonly BaseSimObject ActionTarget;
    public readonly Vector3 TargetPosition;
    public readonly Action KillAction;
    public readonly float BonusAreaEffect;

    public CombatActionExecutionContext(
        ActorExtend caster,
        BaseSimObject primaryEnemy,
        BaseSimObject actionTarget,
        Vector3 targetPosition,
        Action killAction = null,
        float bonusAreaEffect = 0f)
    {
        Caster = caster;
        PrimaryEnemy = primaryEnemy;
        ActionTarget = actionTarget;
        TargetPosition = targetPosition;
        KillAction = killAction;
        BonusAreaEffect = bonusAreaEffect;
    }
}

/// <summary>
/// 将某一类战斗能力接入统一规划和执行流程的适配器。
/// </summary>
public interface ICombatActionProvider
{
    string Id { get; }

    void Collect(in CombatActionCollectionContext context, IList<CombatActionCandidate> output);

    CombatExecutionStatus TryExecute(
        CombatActionCandidate candidate,
        in CombatActionExecutionContext context);
}

/// <summary>
/// 主线程为规划器冻结的单位信息。工作线程不得再解引用 Object。
/// </summary>
public readonly struct CombatantSnapshot
{
    public readonly BaseSimObject Object;
    public readonly int SnapshotIndex;
    public readonly long Id;
    public readonly Vector2 Position;
    public readonly float HealthRatio;
    public readonly float EstimatedPower;
    public readonly float Confidence;
    public readonly float Size;
    public readonly bool IsActor;
    public readonly bool IsAirborne;
    public readonly bool IsRecentAttacker;
    public readonly bool IsAttackingPlanner;
    public readonly bool HasLineOfFire;

    public CombatantSnapshot(
        BaseSimObject obj,
        int snapshotIndex,
        long id,
        Vector2 position,
        float healthRatio,
        float estimatedPower,
        float confidence,
        float size,
        bool isActor,
        bool isAirborne,
        bool isRecentAttacker,
        bool isAttackingPlanner,
        bool hasLineOfFire)
    {
        Object = obj;
        SnapshotIndex = snapshotIndex;
        Id = id;
        Position = position;
        HealthRatio = Mathf.Clamp01(healthRatio);
        EstimatedPower = Mathf.Max(0.01f, estimatedPower);
        Confidence = Mathf.Clamp01(confidence);
        Size = Mathf.Max(0f, size);
        IsActor = isActor;
        IsAirborne = isAirborne;
        IsRecentAttacker = isRecentAttacker;
        IsAttackingPlanner = isAttackingPlanner;
        HasLineOfFire = hasLineOfFire;
    }
}

/// <summary>
/// 规划器可选的一个落脚点。Tile 只在主线程提交阶段使用。
/// </summary>
public readonly struct CombatPositionCandidate
{
    public readonly WorldTile Tile;
    public readonly CombatPositionRole Role;
    public readonly Vector2 Position;
    public readonly float EnemyPressure;
    public readonly float AllySupport;
    public readonly float Crowding;
    private readonly ulong clearShotMask;

    public CombatPositionCandidate(
        WorldTile tile,
        CombatPositionRole role,
        Vector2 position,
        float enemyPressure,
        float allySupport,
        float crowding,
        ulong clearShotMask)
    {
        Tile = tile;
        Role = role;
        Position = position;
        EnemyPressure = Mathf.Max(0f, enemyPressure);
        AllySupport = Mathf.Max(0f, allySupport);
        Crowding = Mathf.Max(0f, crowding);
        this.clearShotMask = clearShotMask;
    }

    /// <summary>判断从该站位到指定敌方快照之间是否没有敌对墙体或护盾遮挡。</summary>
    public bool HasLineOfFire(int enemySnapshotIndex)
    {
        return enemySnapshotIndex is >= 0 and < 64 &&
               (clearShotMask & (1UL << enemySnapshotIndex)) != 0;
    }
}

/// <summary>
/// 战场中会真实阻挡弹丸或地面移动的持久技能快照。
/// </summary>
public readonly struct CombatObstacleSnapshot
{
    public readonly long OwnerId;
    public readonly Kingdom Kingdom;
    public readonly SkillImpactKind Kind;
    public readonly Vector2 Position;
    public readonly Vector2 Direction;
    public readonly float Length;
    public readonly float Width;
    public readonly float Durability;
    public readonly bool IsHostile;

    public CombatObstacleSnapshot(
        long ownerId,
        Kingdom kingdom,
        SkillImpactKind kind,
        Vector2 position,
        Vector2 direction,
        float length,
        float width,
        float durability,
        bool isHostile)
    {
        OwnerId = ownerId;
        Kingdom = kingdom;
        Kind = kind;
        Position = position;
        Direction = direction;
        Length = Mathf.Max(0f, length);
        Width = Mathf.Max(0f, width);
        Durability = Mathf.Max(0f, durability);
        IsHostile = isHostile;
    }
}

/// <summary>
/// 一次规划所需的完整不可变输入。
/// </summary>
public sealed class CombatPlanningSnapshot
{
    public long ActorId;
    public int Revision;
    public Vector2 Position;
    public float HealthRatio;
    public float StaminaRatio;
    public float ManaRatio;
    public float SelfPower;
    public float Morale;
    public float Aggression;
    public float Rationality;
    /// <summary>最近友军围绕自身形成有效相互支援的程度，零表示完全分散，一表示已经聚拢。</summary>
    public float FormationCohesion;
    public long CurrentTargetId;
    public CombatActionKey? CurrentActionKey;
    public bool CanRetreat;
    public bool HighFidelity;
    public bool ArmyRouted;
    public CombatDirective Directive;
    public CombatantSnapshot[] Enemies = Array.Empty<CombatantSnapshot>();
    public CombatantSnapshot[] Allies = Array.Empty<CombatantSnapshot>();
    public CombatActionCandidate[] Actions = Array.Empty<CombatActionCandidate>();
    public CombatPositionCandidate[] Positions = Array.Empty<CombatPositionCandidate>();
    public CombatObstacleSnapshot[] Obstacles = Array.Empty<CombatObstacleSnapshot>();
}

/// <summary>
/// 规划器对局部战场胜负的估计。
/// </summary>
public readonly struct CombatOutcomeEstimate
{
    public readonly float StrengthRatio;
    public readonly float Survival;
    public readonly float Confidence;

    public CombatOutcomeEstimate(float strengthRatio, float survival, float confidence)
    {
        StrengthRatio = Mathf.Max(0f, strengthRatio);
        Survival = Mathf.Clamp01(survival);
        Confidence = Mathf.Clamp01(confidence);
    }
}

/// <summary>
/// 工作线程生成、主线程提交的一次战术决定。
/// </summary>
public sealed class CombatPlan
{
    public int Revision;
    public CombatIntent Intent;
    public CombatRole Role;
    public CombatOutcomeEstimate Outcome;
    public CombatantSnapshot PrimaryEnemy;
    public CombatantSnapshot ActionTarget;
    public CombatantSnapshot BackupActionTarget;
    public CombatActionCandidate Action;
    public CombatActionCandidate BackupAction;
    /// <summary>用于维持职责和距离的动作画像；动作冷却中也会保留。</summary>
    public CombatActionProfile? PositioningProfile;
    public CombatPositionCandidate Position;
    public float TargetScore;
    public float ActionScore;
    public bool HasEnemy;
    public bool HasPosition;
}
