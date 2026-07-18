using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 法器主动能力使用的技能实体资产。根执行承载跨帧状态，实际 Body 可借用法器或由普通技能实体提供。
/// </summary>
[Dependency(typeof(ArtifactSkillTrajectories), typeof(SkillVfxElements))]
public sealed class ArtifactSkillExecutions : ExtendLibrary<SkillEntityAsset, ArtifactSkillExecutions>
{
    /// <summary>借用真实法器 Body 追踪、穿刺并折返的空间攻击执行，可持续寻敌或单次出击。</summary>
    public static SkillEntityAsset FlyingSword { get; private set; }

    /// <summary>统一调度分光剑阵阵形、出剑时序和生命周期的隐藏根执行。</summary>
    public static SkillEntityAsset SwordArray { get; private set; }

    /// <summary>分光剑阵中单道可见剑影，使用通用动画渲染与扫掠碰撞。</summary>
    public static SkillEntityAsset SwordArrayBlade { get; private set; }

    protected override bool AutoRegisterAssets() => true;

    protected override string Prefix() => "Cultiway.ArtifactSkillExecution";

    protected override void OnInit()
    {
        FlyingSword.Element = ElementComposition.Static.Iron;
        FlyingSword.PrefabEntity = FlyingSword.World.CreateEntity(
            new SkillExecution(),
            new SkillEntity
            {
                Asset = FlyingSword,
                VfxElement = SkillVfxElements.Metal,
            },
            new SkillContext(),
            new AnimAfterimage
            {
                Count = 2,
                Layout = AnimAfterimageLayout.Linear,
                SpacingRatio = 0.085f,
                MinSpacing = 0f,
                NewestAlpha = 0.32f,
                OldestAlpha = 0.025f,
                LocalDirection = Vector2.down,
                Tint = Color.white,
            },
            new Position(),
            new Rotation(),
            new AliveTimer(),
            Tags.Get<TagPrefab>());
        FlyingSword
            .SetupColliderSphere(1f, new ColliderConfig
            {
                Enabled = true,
                Actor = true,
                Building = true,
                Enemy = true,
            })
            .SetupDefaultTraj(ArtifactSkillTrajectories.FlyingSword)
            .OnObjCollision = ResolveFlyingSwordHit;

        SwordArray.Element = ElementComposition.Static.Iron;
        SwordArray.PrefabEntity = SwordArray.World.CreateEntity(
            new SkillExecution(),
            new SkillExecutionWithoutBody(),
            new SkillEntity
            {
                Asset = SwordArray,
                VfxElement = SkillVfxElements.Metal,
            },
            new SkillContext(),
            new ArtifactSwordArrayExecutionState(),
            new Position(),
            new Rotation(),
            new AliveTimer(),
            Tags.Get<TagPrefab>());
        SwordArray.SetupDefaultTraj(ArtifactSkillTrajectories.SwordArray);

        SwordArrayBlade.Element = ElementComposition.Static.Iron;
        SwordArrayBlade.PrefabEntity = SwordArrayBlade.World.CreateEntity(
            new SkillEntity
            {
                Asset = SwordArrayBlade,
                VfxElement = SkillVfxElements.Metal,
            },
            new SkillContext(),
            new Position(),
            new Rotation(),
            new Scale(1f),
            new AnimBindRenderer(),
            new AnimController
            {
                meta = new()
                {
                    frame_interval = 1f,
                    loop = true,
                },
            },
            new AnimData { frames = [] },
            Tags.Get<TagPrefab>());
        SwordArrayBlade.PrefabEntity.AddComponent(new AnimAfterimage
        {
            Count = 0,
            Layout = AnimAfterimageLayout.Linear,
            SpacingRatio = 0.12f,
            MinSpacing = 0f,
            NewestAlpha = 0.3f,
            OldestAlpha = 0.12f,
            LocalDirection = Vector2.left,
            Tint = Color.white,
        });
        SwordArrayBlade.PrefabEntity.AddComponent(new SkillHitMemory());
        SwordArrayBlade.PrefabEntity.AddComponent(new ColliderLinearExtent());
        SwordArrayBlade.PrefabEntity.AddComponent(new AliveTimer());
        SwordArrayBlade
            .SetupVisualRotation(VisualRotation.FollowRotation(-90f))
            .SetupColliderSphere(1f, new ColliderConfig
            {
                Enabled = false,
                Actor = true,
                Enemy = true,
            })
            .OnObjCollision = ResolveSwordArrayBladeHit;
    }

    private static bool ResolveSwordArrayBladeHit(
        ref SkillContext context,
        Entity skillContainer,
        Entity blade,
        BaseSimObject target)
    {
        if (context.SourceObj == null || !context.SourceObj.isActor() || context.SourceObj.isRekt() ||
            !target.isActor()) return true;

        Actor owner = context.SourceObj.a;
        if (!owner.canAttackTarget(target.a)) return true;
        SkillHitResolver.HitTarget(
            SwordArrayBlade,
            ref context,
            skillContainer,
            blade,
            target,
            playImpact: true);
        return true;
    }

    private static bool ResolveFlyingSwordHit(
        ref SkillContext context,
        Entity skillContainer,
        Entity execution,
        BaseSimObject target)
    {
        if (context.SourceObj == null || !context.SourceObj.isActor() || context.SourceObj.isRekt()) return true;

        Actor owner = context.SourceObj.a;
        ref ArtifactSpatialAttackMotion motion = ref execution.GetComponent<ArtifactSpatialAttackMotion>();
        if (!ArtifactSpatialTargeting.IsValidTarget(owner, target, motion.control_range, context.AttackKingdom))
            return true;

        long targetKey = ArtifactSpatialTargeting.GetTargetKey(target);
        if (!motion.hit_target_keys.Add(targetKey)) return true;

        bool completesStrike = motion.mode == ArtifactSpatialAttackMode.ContinuousHunt ||
                               context.TargetObj != null &&
                               ArtifactSpatialTargeting.GetTargetKey(context.TargetObj) == targetKey;
        SkillHitResolver.HitTarget(FlyingSword, ref context, skillContainer, execution, target, playImpact: true);
        if (motion.impact_force > 0f && target.isActor())
        {
            ArtifactForceEffects.ApplyRadialForce(
                owner,
                target.a,
                owner.current_position,
                motion.impact_force,
                pull: false);
        }
        if (!completesStrike) return true;

        motion.last_target_key = targetKey;
        motion.has_last_target = true;
        motion.repeat_ready_at = (float)World.world.getCurWorldTime() + motion.repeat_cooldown;
        motion.pierce_remaining = Mathf.Max(
            motion.pierce_remaining,
            motion.pierce_distance + target.stats[S.size]);
        motion.phase = ArtifactSpatialAttackPhase.Piercing;
        return true;
    }
}
