using Cultiway.Content.Combat;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;

namespace Cultiway.Content.Skills;

/// <summary>用正式法钟的实际护持者启动共同的显形过程；护盾仍由法宝活动持有。</summary>
internal static class GoldenBellActivation
{
    /// <summary>进入共同施放后，原主动入口可提交那一次支付和护盾启动。</summary>
    /// <param name="context">实际法宝、持有人及载体。</param>
    /// <param name="target">原入口本次提供的目标和世界范围。</param>
    /// <param name="origin">本次出手方式。</param>
    internal static bool Start(ArtifactAbilityExecutionContext context, in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        var owner = context.controller.GetComponent<ActorBinder>().AE;
        Actor carrier = context.ResolveCarrierActor();
        if (carrier == null || carrier.isRekt()) return false;
        var asset = ArtifactAbilities.GoldenBellBarrier;
        var runtime = target.RuntimeData;
        runtime.Origin = origin;
        var definition = new AbilityDefinition
        {
            Id = asset.id, Name = asset.GetName(), Resource = "none", Cost = 0f, Cooldown = 0f, Lifetime = .39f,
            Use = new ConstructionUse { Channels = asset.active_use.channels, Target = ActiveAbilityTargetMode.Self },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source, Follow = true },
            Impact = new ConstructionImpact { Targets = SkillUseTargetRelation.Self, DamageScale = 0f },
            Root = new ConstructionNode
            {
                Id = "raise", Form = "pressure_wave", Matter = "spirit", Scale = SpatialActorPose.Height(carrier) * .63f,
                Volume = 0f, ContactRole = ConstructionContactRole.Decoration,
                Operations =
                [
                    new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = .13f },
                    new ConstructionOperation { Kind = ConstructionOperationKind.Animate, Clip = "dissolve", At = .14f, Seconds = .25f }
                ]
            }
        };
        var aim = new ActiveAbilityTarget(owner.Base, target.Position, runtimeData: runtime, worldScope: target.WorldScope);
        var force = new GetHitEvent { DamageOrigin = runtime.DamageOrigin };
        force.BindAttacker(carrier);
        return ConstructedAbilityService.StartPrepaid(owner, definition, aim, null, damage: force,
            sourceInstance: context.artifact.Id, spatialSource: carrier, selectionAlreadyChecked: true);
    }
}
