using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cultiway.Content.Combat;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.Components.AnimOverwrite;
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
    private static readonly ConcurrentDictionary<long, float> DetachedUntil = new();
    private static readonly List<Projectile> ActiveProjectiles = new();
    private static long nextCorrelationId;
    private static int emissionFrame = -1;
    private static int emissionsThisFrame;
    private static int projectileEmissionsThisFrame;

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

    /// <summary>结束序列；仅正常完成时等待最后一道武器残影消散后再显示原手持物。</summary>
    public static void Unregister(WeaponControlCastSession session, bool preserveVisualTail)
    {
        Sessions.Remove(session.CorrelationId);
        ActiveCasters.TryRemove(session.CasterActorId, out _);
        Actor actor = session.Caster?.Base;
        if (actor.isRekt()) return;
        if (session.DetachesWeapon && preserveVisualTail)
            DetachedUntil[session.CasterActorId] = Time.time + 0.55f;
        else
            DetachedUntil.TryRemove(session.CasterActorId, out _);
    }

    /// <summary>判断角色是否已有尚未结束的御器序列。</summary>
    public static bool IsCasting(ActorExtend actor)
    {
        return actor != null && !actor.Base.isRekt() && ActiveCasters.ContainsKey(actor.Base.getID());
    }

    /// <summary>判断角色当前仍持有施放开始时的同一个武器对象。</summary>
    public static bool IsCurrentWeapon(Actor owner, Item weapon) => EquippedWeaponVisualService.IsCurrent(owner, weapon);

    /// <summary>供角色渲染补丁判断手中武器是否已被御器执行体借出。</summary>
    public static bool IsWeaponDetached(Actor actor)
    {
        if (actor.isRekt()) return false;
        long actorId = actor.getID();
        if (ActiveCasters.ContainsKey(actorId)) return true;
        if (!DetachedUntil.TryGetValue(actorId, out float until)) return false;
        if (Time.time < until) return true;
        DetachedUntil.TryRemove(actorId, out _);
        return false;
    }

    /// <summary>占用本帧一个执行名额，并限制同时存在的原版投射物数量。</summary>
    public static bool TryReserveEmission(bool createsProjectile)
    {
        RefreshFrameBudget();
        if (emissionsThisFrame >= MaxEmissionsPerFrame) return false;
        if (createsProjectile &&
            ActiveProjectiles.Count + projectileEmissionsThisFrame >= MaxActiveProjectiles)
            return false;
        emissionsThisFrame++;
        if (createsProjectile) projectileEmissionsThisFrame++;
        return true;
    }

    /// <summary>登记御器生成的原版投射物，供后续全局容量控制。</summary>
    public static void RegisterProjectile(Projectile projectile)
    {
        if (projectile != null) ActiveProjectiles.Add(projectile);
    }

    /// <summary>切换世界时清空全部运行时引用。</summary>
    public static void ClearWorldState()
    {
        Sessions.Clear();
        ActiveCasters.Clear();
        DetachedUntil.Clear();
        ActiveProjectiles.Clear();
        emissionFrame = -1;
        emissionsThisFrame = 0;
        projectileEmissionsThisFrame = 0;
    }

    /// <summary>每个渲染帧重置吞吐预算，并剔除已经落地或回收的投射物。</summary>
    private static void RefreshFrameBudget()
    {
        if (emissionFrame == Time.frameCount) return;
        emissionFrame = Time.frameCount;
        emissionsThisFrame = 0;
        projectileEmissionsThisFrame = 0;
        for (int i = ActiveProjectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = ActiveProjectiles[i];
            if (projectile == null || !projectile.isAlive() || projectile.isFinished())
                ActiveProjectiles.RemoveAt(i);
        }
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
    private readonly Color trailCoreColor;
    private readonly Color trailGlowColor;
    private Entity focusEntity;
    private Vector3 focusDirection;
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

    /// <summary>全部御器形态都会把原手持武器临时转移到世界执行体。</summary>
    public bool DetachesWeapon => true;

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
        EquippedWeaponVisualService.ResolveTrailColors(
            Caster.Base,
            weaponAsset,
            out trailCoreColor,
            out trailGlowColor);
    }

    /// <summary>在扣除第一点灵气前重新检查境界、飞行状态和武器一致性。</summary>
    public bool CanStart(in SkillCastSequenceStartContext context)
    {
        return WeaponControlRules.IsEligibleCultivator(Caster) &&
               !Caster.Base.isFlying() &&
               WeaponControlRuntimeService.IsCurrentWeapon(Caster.Base, Weapon) &&
               !WeaponControlRuntimeService.IsCasting(Caster);
    }

    /// <summary>登记运行状态并启动覆盖整段御器动作的公共冷却。</summary>
    public void OnStarted(in SkillCastSequenceStartContext context)
    {
        registered = WeaponControlRuntimeService.Register(this);
        if (!registered) return;
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
        if (!WeaponControlRuntimeService.TryReserveEmission(IsRangedMode()))
            return SkillCastStepDecision.Defer();

        float spread = mode switch
        {
            WeaponControlCastMode.SkyVolley or WeaponControlCastMode.ArrowRain => 24f,
            WeaponControlCastMode.MeleeThrust => 4f,
            WeaponControlCastMode.MeleeCrush => 9f,
            _ => 14f,
        };
        float alternating = context.StepIndex == 0
            ? 0f
            : (context.StepIndex % 2 == 0 ? 1f : -1f) *
              Mathf.Min(spread, 5f + context.StepIndex * 1.35f);
        float jitterRange = mode == WeaponControlCastMode.MeleeThrust ? 1.25f : 3.5f;
        float jitter = Randy.randomFloat(-jitterRange, jitterRange);
        return SkillCastStepDecision.Emit(
            new SkillCastStep(target, scheduledStep.Delay, alternating + jitter));
    }

    /// <summary>结束运行状态；异常终止时同步回收尚未自然结束的主视觉。</summary>
    public void OnEnded(in SkillCastSequenceResult result)
    {
        if (registered)
        {
            WeaponControlRuntimeService.Unregister(
                this,
                result.Reason == SkillCastSequenceEndReason.Completed);
            registered = false;
        }
        if (result.Reason != SkillCastSequenceEndReason.Completed && focusEntity.IsAvailable())
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(focusEntity.Id);
    }

    /// <summary>把通用 SkillEntity 配置成本步骤对应的投射物或真实武器近战执行体。</summary>
    public void ConfigureExecution(Entity execution)
    {
        if (!WeaponControlRuntimeService.IsCurrentWeapon(Caster.Base, Weapon))
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(execution.Id);
            return;
        }

        int index = executionIndex++;
        if (index == 0) StartExecutionVisual(execution);
        UpdateFocusPosition();
        if (IsRangedMode())
        {
            SpawnProjectile(execution, index);
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(execution.Id);
            return;
        }
        ConfigureMeleeExecution(execution, index);
    }

    /// <summary>判断该序列是否会为每个步骤生成一个原版投射物。</summary>
    private bool IsRangedMode()
    {
        return mode is WeaponControlCastMode.SkyVolley or WeaponControlCastMode.ArrowRain;
    }

    /// <summary>在第一道攻击已经成功支付后启动对应的主武器视觉与角色起手动作。</summary>
    private void StartExecutionVisual(Entity execution)
    {
        SpawnOpeningVisual();
        if (mode != WeaponControlCastMode.ArrowRain) return;

        ref SkillContext context = ref execution.GetComponent<SkillContext>();
        Vector3 targetPosition = context.TargetObj.isRekt()
            ? context.TargetPos
            : context.TargetObj.current_position;
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
        if (!IsRangedMode() && target.isActor() && target.a.isFlying()) return false;
        float allowed = range + target.stats[S.size];
        return (target.current_position - Caster.Base.current_position).sqrMagnitude <= allowed * allowed;
    }

    /// <summary>生成天空主武器或箭雨上举武器，并为高空形态补充升空残影。</summary>
    private void SpawnOpeningVisual()
    {
        if (!IsRangedMode()) return;
        Sprite sprite = ResolveWeaponSprite();
        if (sprite == null) return;

        Vector3 basePosition = Caster.Base.GetSimPos();
        float actorScale = Mathf.Max(0.1f, Caster.Base.stats[S.scale]);
        if (mode == WeaponControlCastMode.SkyVolley)
        {
            float height = ResolveSkyHeight();
            focusDirection = Vector3.up;
            float scale = actorScale * 2.75f;
            for (var i = 0; i < 3; i++)
            {
                ModClass.I.SkillV3.SpawnAnim(
                    new[] { sprite },
                    basePosition + new Vector3(0f, 0f, height * (i + 1) / 4f),
                    Vector3.up,
                    scale * (0.65f + i * 0.12f),
                    new Color(1f, 1f, 1f, 0.16f + i * 0.08f),
                    lifeTime: 0.16f + i * 0.04f,
                    visualRotation: VisualRotation.FixedUpright(ResolveSpriteAngle(sprite)));
            }

            focusEntity = ModClass.I.SkillV3.SpawnAnim(
                new[] { sprite },
                basePosition + new Vector3(0f, 0f, height),
                Vector3.up,
                scale,
                lifeTime: sequenceDuration + 0.3f,
                visualRotation: VisualRotation.Spin(-155f, ResolveSpriteAngle(sprite)));
            focusEntity.AddComponent(new AnimAfterimage
            {
                Count = 5,
                Layout = AnimAfterimageLayout.Angular,
                NewestAlpha = 0.28f,
                OldestAlpha = 0.035f,
                Tint = Color.white,
                ArcRadius = 0.42f,
                ArcDegreesPerLayer = 11f,
                ArcDirection = -1f,
            });
        }
        else
        {
            focusDirection = Quaternion.AngleAxis(
                Randy.randomFloat(-16f, 16f),
                Vector3.forward) * Vector3.up;
            focusEntity = ModClass.I.SkillV3.SpawnAnim(
                new[] { sprite },
                ResolveArrowRainWeaponPosition(basePosition),
                focusDirection,
                actorScale * 1.55f,
                lifeTime: sequenceDuration + 0.3f,
                visualRotation: VisualRotation.FollowRotation(ResolveSpriteAngle(sprite)));
            focusEntity.AddComponent(new AnimAfterimage
            {
                Count = 3,
                Layout = AnimAfterimageLayout.Angular,
                NewestAlpha = 0.2f,
                OldestAlpha = 0.025f,
                Tint = Color.white,
                ArcRadius = 0.16f,
                ArcDegreesPerLayer = 4f,
                ArcDirection = -1f,
            });
        }

        ProjectileAsset projectile = AssetManager.projectiles.get(weaponAsset.projectile);
        if (projectile != null && !string.IsNullOrEmpty(projectile.sound_launch))
            MusicBox.playSound(projectile.sound_launch, basePosition.x, basePosition.y, true);
    }

    /// <summary>在每次实际发射前让主武器视觉跟随可能被击退的施法者。</summary>
    private void UpdateFocusPosition()
    {
        if (!focusEntity.IsAvailable()) return;
        Vector3 basePosition = Caster.Base.GetSimPos();
        focusEntity.GetComponent<Position>().value = mode == WeaponControlCastMode.SkyVolley
            ? basePosition + new Vector3(0f, 0f, ResolveSkyHeight())
            : ResolveArrowRainWeaponPosition(basePosition);
        ref AliveTimeLimit lifetime = ref focusEntity.GetComponent<AliveTimeLimit>();
        float elapsed = focusEntity.GetComponent<AliveTimer>().value;
        lifetime.value = Mathf.Max(lifetime.value, elapsed + 0.55f);
    }

    /// <summary>返回天空齐射主武器相对角色的悬浮高度。</summary>
    private float ResolveSkyHeight()
    {
        return 5.5f + WeaponControlRules.ResolveRealm(Caster) * 0.65f;
    }

    /// <summary>返回箭雨上举武器贴近角色手部且略高于头顶的位置。</summary>
    private Vector3 ResolveArrowRainWeaponPosition(Vector3 basePosition)
    {
        Vector3 position = basePosition + focusDirection * 0.34f;
        position.z += 0.3f;
        return position;
    }

    /// <summary>以天空倾泻或高弧箭雨姿态生成武器对应的原版投射物。</summary>
    private void SpawnProjectile(Entity execution, int index)
    {
        string projectileId = WeaponControlProjectileProxyLibrary.Resolve(weaponAsset.projectile, mode);
        if (string.IsNullOrEmpty(projectileId)) return;
        ref SkillContext context = ref execution.GetComponent<SkillContext>();
        BaseSimObject target = context.TargetObj;
        Vector3 targetPosition = target.isRekt() ? context.TargetPos : target.current_position;
        float angle = index * 137.50776f * Mathf.Deg2Rad;
        float targetRadius = mode == WeaponControlCastMode.SkyVolley ? 2.4f : 1.7f;
        targetPosition += new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) *
                          targetRadius * Mathf.Sqrt((index % 11 + 1f) / 11f);

        Vector3 launchPosition = Caster.Base.current_position;
        float startHeight;
        if (mode == WeaponControlCastMode.SkyVolley)
        {
            float orbitRadius = 0.55f + index % 5 * 0.16f;
            launchPosition += new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * orbitRadius;
            startHeight = 5.5f + WeaponControlRules.ResolveRealm(Caster) * 0.65f;
        }
        else
        {
            launchPosition += new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.22f;
            startHeight = 0.45f;
        }

        Projectile projectile = World.world.projectiles.spawn(
            Caster.Base,
            target.isRekt() ? null : target,
            projectileId,
            launchPosition,
            targetPosition,
            target.isRekt() ? 0f : target.getHeight(),
            startHeight,
            pForcedKingdom: attackKingdom);
        WeaponControlRuntimeService.RegisterProjectile(projectile);
    }

    /// <summary>注入真实武器 Sprite、命中范围、残影和动作参数。</summary>
    private void ConfigureMeleeExecution(Entity execution, int index)
    {
        Sprite sprite = ResolveWeaponSprite();
        if (sprite == null)
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(execution.Id);
            return;
        }

        execution.GetComponent<AnimRuntimeFrames>().Value = new[] { sprite };
        float actorScale = Mathf.Max(0.1f, Caster.Base.stats[S.scale]);
        execution.GetComponent<Scale>().value = Vector3.one * (actorScale * 1.85f);
        execution.GetComponent<VisualRotation>() = VisualRotation.FollowRotation(ResolveSpriteAngle(sprite));

        ref EquippedWeaponMotionState state = ref execution.GetComponent<EquippedWeaponMotionState>();
        state.Weapon = Weapon;
        state.Kind = ResolveMotionKind();
        state.Elapsed = 0f;
        state.Duration = ResolveMotionDuration();
        state.Reach = ResolveReach(execution.GetComponent<SkillContext>(), state.Kind);
        state.DamageMultiplier = ResolveDamageMultiplier();
        state.Direction = execution.GetComponent<Rotation>().value;
        state.CollisionEnabled = true;
        ResolveSweepAngles(index, out state.StartAngle, out state.EndAngle);

        ref ColliderConfig collider = ref execution.GetComponent<ColliderConfig>();
        collider.Enabled = state.Kind != EquippedWeaponMotionKind.Crush;
        collider.ExplicitTargetOnly = false;
        execution.GetComponent<AliveTimeLimit>().value = state.Duration + 0.1f;
        execution.GetComponent<AnimAfterimageOverride>().Value = ResolveAfterimage(ref state);
        float worldVisualScale = EquippedWeaponVisualService.ResolveWorldVisualScale(Caster.Base);
        ConfigureMotionTrail(execution, state.Kind, worldVisualScale, state.Reach);
        ref MotionRibbonTrail trail = ref execution.GetComponent<MotionRibbonTrail>();
        ref ColliderSphere sphere = ref execution.GetComponent<ColliderSphere>();
        sphere.Radius = ResolveCollisionRadius(state.Kind, trail.CoreWidth);
    }

    /// <summary>按整体施放形态解析近战运动类型。</summary>
    private EquippedWeaponMotionKind ResolveMotionKind()
    {
        return mode switch
        {
            WeaponControlCastMode.MeleeThrust => EquippedWeaponMotionKind.Thrust,
            WeaponControlCastMode.MeleeCrush => EquippedWeaponMotionKind.Crush,
            _ => EquippedWeaponMotionKind.Sweep,
        };
    }

    /// <summary>按器形调节每一道近战动作的完整播放时间。</summary>
    private float ResolveMotionDuration()
    {
        return mode switch
        {
            WeaponControlCastMode.MeleeCrush => 0.42f,
            WeaponControlCastMode.MeleeThrust => 0.27f,
            _ => category == WeaponControlCategory.Axe ? 0.34f : 0.3f,
        };
    }

    /// <summary>按器形和目标距离解析动作长度；突刺以旧控制距离为基准整体延长到 2.5 倍。</summary>
    private float ResolveReach(SkillContext context, EquippedWeaponMotionKind kind)
    {
        float size = Mathf.Clamp(Caster.Base.stats[S.size], 0.5f, 4f);
        float baseReach = category switch
        {
            WeaponControlCategory.Spear => 2.45f,
            WeaponControlCategory.Hammer => 1.8f,
            WeaponControlCategory.Axe => 1.85f,
            WeaponControlCategory.Staff => 2.05f,
            _ => 1.72f,
        };
        baseReach += size * 0.18f;
        Vector3 targetPosition = context.TargetObj.isRekt()
            ? context.TargetPos
            : context.TargetObj.current_position;
        float targetDistance = Vector2.Distance(Caster.Base.current_position, targetPosition);
        float maximum = kind == EquippedWeaponMotionKind.Thrust
            ? WeaponControlRules.ResolveRange(Caster, category)
            : range;
        float resolved = Mathf.Clamp(targetDistance, baseReach, maximum);
        return kind == EquippedWeaponMotionKind.Thrust
            ? resolved * WeaponControlRules.ThrustReachMultiplier
            : resolved;
    }

    /// <summary>让突刺命中胶囊覆盖可见钻头主体，其余动作保留原有碰撞宽度。</summary>
    private float ResolveCollisionRadius(EquippedWeaponMotionKind kind, float trailCoreWidth)
    {
        float baseRadius = category switch
        {
            WeaponControlCategory.Hammer => 1.05f,
            WeaponControlCategory.Axe => 0.92f,
            WeaponControlCategory.Spear => 0.52f,
            _ => 0.72f,
        };
        return kind == EquippedWeaponMotionKind.Thrust
            ? Mathf.Max(baseRadius, trailCoreWidth * 0.55f)
            : baseRadius;
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

    /// <summary>只为砸落保留角向武器贴图残影；突刺和扫掠分别使用程序化几何。</summary>
    private static AnimAfterimage ResolveAfterimage(ref EquippedWeaponMotionState state)
    {
        return state.Kind switch
        {
            EquippedWeaponMotionKind.Crush => new AnimAfterimage
            {
                Count = 8,
                Layout = AnimAfterimageLayout.Angular,
                NewestAlpha = 0.4f,
                OldestAlpha = 0.035f,
                Tint = Color.white,
                ArcRadius = state.Reach,
                ArcDegreesPerLayer = 13f,
                ArcDirection = Mathf.Sign(state.EndAngle - state.StartAngle),
            },
            _ => default,
        };
    }

    /// <summary>按近战动作选择径向扫掠扇面、轴向突刺钻头或关闭程序化轨迹。</summary>
    private void ConfigureMotionTrail(
        Entity execution,
        EquippedWeaponMotionKind kind,
        float worldVisualScale,
        float reach)
    {
        ref MotionRibbonTrail trail = ref execution.GetComponent<MotionRibbonTrail>();
        trail = kind switch
        {
            EquippedWeaponMotionKind.Sweep => CreateSweepTrail(worldVisualScale),
            EquippedWeaponMotionKind.Thrust => CreateThrustTrail(worldVisualScale, reach),
            _ => default,
        };
    }

    /// <summary>按器形构造覆盖角色近身到武器外缘的双层扫掠扇面。</summary>
    private MotionRibbonTrail CreateSweepTrail(float worldVisualScale)
    {
        float coreWidth = category switch
        {
            WeaponControlCategory.Axe => 0.44f,
            WeaponControlCategory.Hammer => 0.42f,
            WeaponControlCategory.Staff => 0.24f,
            WeaponControlCategory.Spear => 0.2f,
            WeaponControlCategory.Sword => 0.3f,
            _ => 0.28f,
        } * worldVisualScale;
        float innerRadiusRatio = category switch
        {
            WeaponControlCategory.Axe => 0.2f,
            WeaponControlCategory.Hammer => 0.22f,
            WeaponControlCategory.Staff => 0.34f,
            WeaponControlCategory.Spear => 0.32f,
            _ => 0.27f,
        };
        return new MotionRibbonTrail
        {
            Enabled = true,
            Shape = MotionRibbonShape.RadialSweep,
            HistorySeconds = category == WeaponControlCategory.Axe ? 0.28f : 0.24f,
            MinSampleDistance = 0.055f * worldVisualScale,
            MaxPoints = 36,
            CoreWidth = coreWidth,
            GlowWidth = coreWidth * 2.15f,
            CoreColor = trailCoreColor,
            GlowColor = trailGlowColor,
            SourceOrigin = Caster.Base.GetSimPos(),
            SweepInnerRadiusRatio = innerRadiusRatio,
            SweepOuterExtension = coreWidth * 0.7f,
            SweepGlowExpansion = 0.16f * worldVisualScale,
            CoreAlpha = 0.72f,
            GlowAlpha = 0.22f,
            TileLength = 0.34f * worldVisualScale,
            FlowSpeed = 1.35f,
        };
    }

    /// <summary>按相对体型和实际突刺长度构造足够宽厚的双层轴向钻头。</summary>
    private MotionRibbonTrail CreateThrustTrail(float worldVisualScale, float reach)
    {
        float minimumWidth = category switch
        {
            WeaponControlCategory.Spear => 1.9f,
            WeaponControlCategory.Sword => 2.1f,
            WeaponControlCategory.Staff => 2f,
            _ => 2.05f,
        } * worldVisualScale;
        float reachWidthFactor = category == WeaponControlCategory.Sword ? 0.48f : 0.44f;
        float coreWidth = Mathf.Clamp(
            reach * reachWidthFactor,
            minimumWidth,
            4f * worldVisualScale);
        float tipExtension = category switch
        {
            WeaponControlCategory.Spear => 0.68f,
            WeaponControlCategory.Sword => 0.58f,
            WeaponControlCategory.Staff => 0.52f,
            _ => 0.56f,
        } * worldVisualScale;
        return new MotionRibbonTrail
        {
            Enabled = true,
            Shape = MotionRibbonShape.AxialThrust,
            HistorySeconds = 0.18f,
            MinSampleDistance = 0.04f * worldVisualScale,
            MaxPoints = 20,
            CoreWidth = coreWidth,
            GlowWidth = coreWidth * 1.55f,
            CoreColor = trailCoreColor,
            GlowColor = trailGlowColor,
            SourceOrigin = Caster.Base.GetSimPos(),
            ThrustStartOffset = 0.28f * worldVisualScale,
            ThrustTipExtension = tipExtension,
            CoreAlpha = 0.88f,
            GlowAlpha = 0.3f,
            TileLength = 0.3f * worldVisualScale,
            FlowSpeed = 2.1f,
        };
    }

    /// <summary>将横向和纵向武器贴图统一校正到运动方向。</summary>
    private static float ResolveSpriteAngle(Sprite sprite)
    {
        return EquippedWeaponVisualService.ResolveSpriteAngle(sprite);
    }

    /// <summary>取得与原版手持渲染一致的武器帧，并保留彩色武器的阵营配色。</summary>
    private Sprite ResolveWeaponSprite()
    {
        return EquippedWeaponVisualService.ResolveSprite(Caster.Base, weaponAsset);
    }
}
