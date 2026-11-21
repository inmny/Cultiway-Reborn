using System;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>召唤入口与显形模型使用同一选中地块，实际生成成功后才发布这次施放。</summary>
internal static class SpiritHostConstruction
{
    /// <summary>共同施放会先核对位置和来源，阴兵仍由原法宝活动管理。</summary>
    internal static bool Start(ArtifactAbilityExecutionContext context, in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin, Vector2 tileCenter, Func<bool> summon)
    {
        Actor carrier = context.ResolveCarrierActor();
        var owner = context.controller.GetComponent<ActorBinder>().AE;
        var asset = ArtifactAbilities.SpiritHostManifestation;
        var runtime = target.RuntimeData;
        runtime.Origin = origin;
        var aim = new ActiveAbilityTarget(null, tileCenter, runtimeData: runtime, worldScope: target.WorldScope);
        var definition = new AbilityDefinition
        {
            Id = asset.id, Name = asset.GetName(), Resource = "none", Cost = 0f, Cooldown = 0f, Lifetime = .62f,
            Use = new ConstructionUse { Channels = asset.active_use.channels, Target = ActiveAbilityTargetMode.Point },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source },
            Impact = new ConstructionImpact { Targets = SkillUseTargetRelation.Hostile, DamageScale = 0f },
            Root = new ConstructionNode
            {
                Id = "gate", Form = "spirit_host_gate", Matter = "spirit", Scale = 1.6f, Volume = 0f,
                ContactRole = ConstructionContactRole.Decoration,
                Operations =
                [
                    new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = .12f },
                    new ConstructionOperation { Kind = ConstructionOperationKind.Animate, Clip = "dissolve",
                        At = .35f, Seconds = .25f }
                ]
            }
        };
        return ConstructedAbilityService.StartPrepaid(owner, definition, aim, tileCenter,
            sourceInstance: context.artifact.Id, spatialSource: carrier,
            selectionAlreadyChecked: true, commitEffect: summon);
    }
}
