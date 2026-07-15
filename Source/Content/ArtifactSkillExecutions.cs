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
/// 法器主动能力使用的隐藏技能实体资产。实体承载执行状态，实际画面由其借用的法器 Body 提供。
/// </summary>
[Dependency(typeof(ArtifactSkillTrajectories), typeof(SkillVfxElements))]
public sealed class ArtifactSkillExecutions : ExtendLibrary<SkillEntityAsset, ArtifactSkillExecutions>
{
    public static SkillEntityAsset FlyingSword { get; private set; }

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

        motion.last_target_key = targetKey;
        motion.has_last_target = true;
        motion.repeat_ready_at = (float)World.world.getCurWorldTime() + motion.repeat_cooldown;
        motion.pierce_remaining = Mathf.Max(
            motion.pierce_remaining,
            motion.pierce_distance + target.stats[S.size]);
        motion.phase = ArtifactSpatialAttackPhase.Piercing;

        SkillHitResolver.HitTarget(FlyingSword, ref context, skillContainer, execution, target, playImpact: true);
        return true;
    }
}
