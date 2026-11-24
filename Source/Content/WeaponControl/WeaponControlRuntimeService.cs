using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cultiway.Content.Combat;
using Cultiway.Content.Skills;
using Cultiway.Content.Systems.Logic;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.WeaponControl;

/// <summary>保存御器施放序列与原版投射物之间无法持久化的短期关联。</summary>
internal static class WeaponControlRuntimeService
{
    private const int MaxEmissionsPerFrame = 64;
    private const int MaxActiveProjectiles = 512;
    private static readonly Dictionary<long, WeaponControlCastSession> Sessions = new();
    private static readonly ConcurrentDictionary<long, byte> ActiveCasters = new();
    private static long nextCorrelationId;
    private static int emissionFrame = -1;
    private static int emissionsThisFrame;
    private static int projectileEmissionsThisFrame;
    private static int projectilesAtFrameStart;

    /// <summary>为新施放序列生成进程内唯一关联 ID。</summary>
    public static long NextCorrelationId()
    {
        return Interlocked.Increment(ref nextCorrelationId);
    }

    /// <summary>登记已经正式启动的御器序列。</summary>
    public static bool Register(WeaponControlCastSession session)
    {
        if (!ActiveCasters.TryAdd(session.CasterActorId, 0)) return false;
        Sessions.Add(session.CorrelationId, session);
        return true;
    }

    /// <summary>按施法运行参数关联 ID 解析当前御器序列。</summary>
    public static bool TryGet(long correlationId, out WeaponControlCastSession session)
    {
        return Sessions.TryGetValue(correlationId, out session);
    }

    /// <summary>解除序列占用。真实器物由连续运动自行归还。</summary>
    public static void Unregister(WeaponControlCastSession session)
    {
        Sessions.Remove(session.CorrelationId);
        ActiveCasters.TryRemove(session.CasterActorId, out _);
    }

    /// <summary>判断角色是否已有尚未结束的御器序列。</summary>
    public static bool IsCasting(ActorExtend actor)
    {
        return actor != null && !actor.Base.isRekt() && ActiveCasters.ContainsKey(actor.Base.getID());
    }

    /// <summary>判断角色当前仍持有施放开始时的同一个武器对象。</summary>
    public static bool IsCurrentWeapon(Actor owner, Item weapon) =>
        EquippedWeaponVisualService.IsCurrent(owner, weapon);

    /// <summary>占用本帧一个执行名额，并限制同时存在的原版投射物数量。</summary>
    public static bool TryReserveEmission(bool createsProjectile)
    {
        RefreshFrameBudget();
        if (emissionsThisFrame >= MaxEmissionsPerFrame) return false;
        if (createsProjectile &&
            projectilesAtFrameStart + projectileEmissionsThisFrame >= MaxActiveProjectiles)
            return false;
        emissionsThisFrame++;
        if (createsProjectile) projectileEmissionsThisFrame++;
        return true;
    }

    /// <summary>切换世界时清空全部运行时引用。</summary>
    public static void ClearWorldState()
    {
        Sessions.Clear();
        ActiveCasters.Clear();
        WeaponControlFlight.Clear();
        NativeProjectileFlights.Clear();
        emissionFrame = -1;
        emissionsThisFrame = 0;
        projectileEmissionsThisFrame = 0;
        projectilesAtFrameStart = 0;
    }

    /// <summary>先取本帧已经在飞的数量，再对本帧每次新受理单独计数。</summary>
    private static void RefreshFrameBudget()
    {
        if (emissionFrame == Time.frameCount) return;
        emissionFrame = Time.frameCount;
        emissionsThisFrame = 0;
        projectileEmissionsThisFrame = 0;
        projectilesAtFrameStart = NativeProjectileFlights.Prune();
    }
}

/// <summary>一次御器施放的动态步骤钩子和武器快照。</summary>
internal sealed class WeaponControlCastSession : ISkillCastSequenceHooks
{
    private readonly Entity skillContainer;
    private readonly EquipmentAsset weaponAsset;
    private readonly WeaponControlCategory category;
    private readonly WeaponControlCastMode mode;
    private readonly Kingdom attackKingdom;
    private readonly float range;
    private readonly float sequenceDuration;
    private WeaponControlFlight flight;
    private int executionIndex;
    private bool registered;

    /// <summary>施放该序列的角色。</summary>
    public ActorExtend Caster { get; }

    /// <summary>施放开始时装备的真实武器。</summary>
    public Item Weapon { get; }

    /// <summary>本次序列与生成执行体共享的唯一 ID。</summary>
    public long CorrelationId { get; }

    /// <summary>施放开始时保存的稳定角色 ID，供角色死亡后的运行状态清理使用。</summary>
    public long CasterActorId { get; }

    internal bool IsRanged => mode is WeaponControlCastMode.SkyVolley or WeaponControlCastMode.ArrowRain;

    /// <summary>创建一次已经完成动作规划但尚未进入技能系统的序列。</summary>
    public WeaponControlCastSession(
        ActorExtend caster,
        Item weapon,
        EquipmentAsset weaponAsset,
        WeaponControlCategory category,
        WeaponControlCastMode mode,
        Entity skillContainer,
        Kingdom attackKingdom,
        float range,
        float sequenceDuration,
        long correlationId)
    {
        Caster = caster;
        Weapon = weapon;
        this.weaponAsset = weaponAsset;
        this.category = category;
        this.mode = mode;
        this.skillContainer = skillContainer;
        this.attackKingdom = attackKingdom;
        this.range = range;
        this.sequenceDuration = sequenceDuration;
        CorrelationId = correlationId;
        CasterActorId = caster.Base.getID();
    }

    /// <summary>在扣除第一点灵气前重新检查境界、飞行状态和武器一致性。</summary>
    public bool CanStart(in SkillCastSequenceStartContext context)
    {
        return WeaponControlRules.IsEligibleCultivator(Caster) &&
               !Caster.Base.isFlying() &&
               WeaponControlRuntimeService.IsCurrentWeapon(Caster.Base, Weapon) &&
               !ConstructedAbilityService.HasWeaponControl(Caster.Base) &&
               !WeaponControlRuntimeService.IsCasting(Caster);
    }

    /// <summary>登记运行状态并启动覆盖整段御器动作的公共冷却。</summary>
    public void OnStarted(in SkillCastSequenceStartContext context)
    {
        registered = WeaponControlRuntimeService.Register(this);
        if (!registered) return;
        flight = WeaponControlFlight.TryBegin(Caster.Base, Weapon, weaponAsset, mode, ResolveLaunchHeight());
        float recovery = Mathf.Clamp(1.45f - Caster.Base.stats[S.attack_speed] * 0.08f, 0.65f, 1.45f);
        SkillCooldownService.Start(Caster, skillContainer, sequenceDuration + recovery);
    }

    /// <summary>逐发检查武器与目标，并在全局容量不足时保留当前步骤等待下一帧。</summary>
    public SkillCastStepDecision PrepareStep(
        in SkillCastSequenceStepContext context,
        in SkillCastStep scheduledStep)
    {
        if (!registered || !WeaponControlRules.IsEligibleCultivator(Caster) || Caster.Base.isFlying() ||
            !WeaponControlRuntimeService.IsCurrentWeapon(Caster.Base, Weapon))
            return SkillCastStepDecision.Cancel();

        if (!TryResolveTarget(scheduledStep.Target, out BaseSimObject target))
            return SkillCastStepDecision.Cancel();
        if (!IsRanged && flight == null) return SkillCastStepDecision.Cancel();
        if (flight != null && !flight.ReadyToRelease)
            return SkillCastStepDecision.Defer();
        if (!WeaponControlRuntimeService.TryReserveEmission(IsRanged))
            return SkillCastStepDecision.Defer();

        float spread = mode switch
        {
            WeaponControlCastMode.SkyVolley or WeaponControlCastMode.ArrowRain => 24f,
            WeaponControlCastMode.MeleeThrust => 0f,
            WeaponControlCastMode.MeleeCrush => 0f,
            _ => 14f,
        };
        float alternating = context.StepIndex == 0
            ? 0f
            : (context.StepIndex % 2 == 0 ? 1f : -1f) *
              Mathf.Min(spread, 5f + context.StepIndex * 1.35f);
        float jitterRange = mode is WeaponControlCastMode.MeleeThrust or WeaponControlCastMode.MeleeCrush ? .25f : 3.5f;
        float jitter = Randy.randomFloat(-jitterRange, jitterRange);
        return SkillCastStepDecision.Emit(
            new SkillCastStep(target, scheduledStep.Delay, alternating + jitter));
    }

    /// <summary>停止排程。已经支付且仍有效的动作完成后，原器物连续回手。</summary>
    public void OnEnded(in SkillCastSequenceResult result)
    {
        if (registered)
        {
            WeaponControlRuntimeService.Unregister(this);
            registered = false;
        }

        flight?.End();
    }

    /// <summary>按正式发射启动一次实际攻击。</summary>
    /// <param name="emission">本次序列确定的发射资料。</param>
    internal bool Emit(in SkillEmission emission)
    {
        Actor source = Caster.Base;
        if (source != emission.Source || !WeaponControlRuntimeService.IsCurrentWeapon(source, Weapon))
            return false;
        if (!IsRanged) return BeginStrike(emission);
        if (!SpawnProjectile(emission, executionIndex)) return false;
        if (executionIndex++ == 0) PlayRangedRelease(emission);
        return true;
    }

    /// <summary>在第一发实际发出后播放原放箭声音及箭雨动作。</summary>
    /// <param name="emission">已经发生的发射。</param>
    private void PlayRangedRelease(in SkillEmission emission)
    {
        ProjectileAsset projectile = AssetManager.projectiles.get(weaponAsset.projectile);
        if (!string.IsNullOrEmpty(projectile?.sound_launch))
            MusicBox.playSound(projectile.sound_launch, Caster.Base.current_position.x, Caster.Base.current_position.y, true);
        if (mode != WeaponControlCastMode.ArrowRain) return;

        Vector3 targetPosition = emission.Target.isRekt()
            ? emission.TargetPos
            : emission.Target.current_position;
        Caster.Base.punchTargetAnimation(targetPosition, true, true);
    }

    /// <summary>保留有效主目标；主目标失效时从施法距离内选择最近敌人继续序列。</summary>
    private bool TryResolveTarget(BaseSimObject scheduledTarget, out BaseSimObject target)
    {
        if (IsValidTarget(scheduledTarget))
        {
            target = scheduledTarget;
            return true;
        }

        target = null;
        float nearest = float.MaxValue;
        foreach (BaseSimObject candidate in SkillUtils.IterEnemyInSphere(
                     Caster.Base.current_position, range, Caster.Base, attackKingdom))
        {
            if (!IsValidTarget(candidate)) continue;
            float distance = (candidate.current_position - Caster.Base.current_position).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            target = candidate;
        }

        return target != null;
    }

    /// <summary>检查候选对象仍可被当前角色攻击且处于本形态作用距离内。</summary>
    private bool IsValidTarget(BaseSimObject target)
    {
        if (target.isRekt() || !Caster.Base.canAttackTarget(target)) return false;
        if (!IsRanged && target.isActor() && target.a.isFlying()) return false;
        float allowed = range + target.stats[S.size];
        return (target.current_position - Caster.Base.current_position).sqrMagnitude <= allowed * allowed;
    }

    private float ResolveLaunchHeight() => mode == WeaponControlCastMode.SkyVolley
        ? 5.5f + WeaponControlRules.ResolveRealm(Caster) * .65f : .45f;

    /// <summary>以天空倾泻或高弧箭雨姿态生成武器对应的原版投射物。</summary>
    /// <param name="emission">正式位置和对象。</param>
    /// <param name="index">此前已经发出的箭数。</param>
    private bool SpawnProjectile(in SkillEmission emission, int index)
    {
        string projectileId = WeaponControlProjectileProxyLibrary.Resolve(weaponAsset.projectile, mode);
        if (string.IsNullOrEmpty(projectileId)) return false;
        BaseSimObject target = emission.Target;
        Vector3 targetPosition = target.isRekt() ? emission.TargetPos : target.current_position;
        float angle = index * 137.50776f * Mathf.Deg2Rad;
        float targetRadius = mode == WeaponControlCastMode.SkyVolley ? 2.4f : 1.7f;
        targetPosition += new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) *
                          targetRadius * Mathf.Sqrt((index % 11 + 1f) / 11f);

        Vector3 position = Caster.Base.current_position;
        position.z = ResolveLaunchHeight();
        Vector3 launch = flight?.LaunchPoint() ?? position;
        return World.world.projectiles.spawn(
            Caster.Base,
            target.isRekt() ? null : target,
            projectileId,
            new Vector3(launch.x, launch.y, 0f),
            targetPosition,
            target.isRekt() ? 0f : target.getHeight(),
            launch.z,
            pForcedKingdom: attackKingdom) != null;
    }

    /// <summary>用原器形和既有出击规则建立实际刃端动作。</summary>
    /// <param name="emission">本次序列确认的目标与来向。</param>
    private bool BeginStrike(in SkillEmission emission)
    {
        if (flight == null || !flight.IsCurrent) return false;
        Vector3 direction = emission.TargetPos - emission.SourcePos;
        direction = direction.sqrMagnitude < .0001f ? Vector3.right : direction.normalized;
        var context = new SkillContext
        {
            Strength = emission.Strength,
            PowerLevel = emission.PowerLevel,
            RuntimeData = emission.RuntimeData,
            TargetObj = emission.Target.isRekt() ? null : emission.Target,
            TargetPos = emission.TargetPos,
            TargetDir = direction,
            AttackKingdom = emission.AttackKingdom,
        };
        context.BindSource(emission.Source, emission.SpatialSource);
        Vector3 aim = context.TargetObj.isRekt() ? context.TargetPos : context.TargetObj.current_position;
        ResolveSweepAngles(executionIndex++, out float startAngle, out float endAngle);
        var motion = new EquippedWeaponMotionState
        {
            Weapon = Weapon,
            Duration = WeaponControlRules.StrokeSeconds(Caster, category, mode),
            Reach = Mathf.Clamp(Vector2.Distance(Caster.Base.current_position, aim), .38f, range),
            DamageMultiplier = ResolveDamageMultiplier(),
            Direction = Core.SkillLibV3.Manager.ApplyInitialAngleOffset(direction, emission.InitialAngleOffsetDegrees),
            StartAngle = startAngle,
            EndAngle = endAngle,
        };
        WeaponControlStrikeSystem.Begin(context, flight, motion);
        return true;
    }

    /// <summary>按动作重量返回相对一次原版普攻的伤害倍率。</summary>
    private float ResolveDamageMultiplier()
    {
        return mode switch
        {
            WeaponControlCastMode.MeleeCrush => 1.25f,
            WeaponControlCastMode.MeleeThrust => category == WeaponControlCategory.Spear ? 0.62f : 0.52f,
            _ => category switch
            {
                WeaponControlCategory.Axe => 0.72f,
                WeaponControlCategory.Sword => 0.55f,
                WeaponControlCategory.Staff => 0.44f,
                _ => 0.48f,
            },
        };
    }

    /// <summary>让连续挥砍左右交替，避免全部武器残影重叠成同一条弧。</summary>
    private void ResolveSweepAngles(int index, out float startAngle, out float endAngle)
    {
        float halfArc = category switch
        {
            WeaponControlCategory.Axe => 112f,
            WeaponControlCategory.Staff => 92f,
            _ => 82f,
        };
        bool reverse = index % 2 != 0;
        startAngle = reverse ? halfArc : -halfArc;
        endAngle = reverse ? -halfArc : halfArc;
    }
}
