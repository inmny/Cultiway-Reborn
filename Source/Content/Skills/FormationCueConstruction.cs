using System.Collections.Generic;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>修炼已成立的结果通过共用立体部位显示，不因显示重新确认一次作用。</summary>
internal static class FormationCueConstruction
{
    /// <summary>在原事件排入的步骤位置，用预付入口建立一次装饰显形。</summary>
    /// <param name="emission">原修炼效果实际排入的步骤。</param>
    internal static SkillEmissionResult Emit(SkillEmission emission)
    {
        var skill = emission.SkillContainer;
        (string family, string form, string matter, float scale, float height, float spin) = skill switch
        {
            var entry when entry == CoreFormationSkills.EarthWard =>
                (CoreFormationEffectFamilies.Earth, "guard_seal", "earth", 1.35f, .10f, 0f),
            var entry when entry == CoreFormationSkills.EarthWardImpact =>
                (CoreFormationEffectFamilies.Earth, "earth_wall_strata_rubble", "earth", .65f, .18f, 100f),
            var entry when entry == CoreFormationSkills.BalancedAdaptation =>
                (CoreFormationEffectFamilies.Balanced, "entropy_guard", "entropy", 1f, .32f, -70f),
            var entry when entry == CoreFormationSkills.ReservoirOrb =>
                (CoreFormationEffectFamilies.Condensed, "spirit_bolt", "wood", 1.46f, .75f, 80f),
            var entry when entry == CoreFormationSkills.SpiritEcho =>
                (CoreFormationEffectFamilies.Spiritual, "purification_surge", "spirit", .55f, .19f, -55f),
            var entry when entry == CoreFormationSkills.IllusionDecoy =>
                (CoreFormationEffectFamilies.Illusion, "spirit_colossus", "yin", .31f, 0f, 0f),
            var entry when entry == CoreFormationSkills.InfantGuard =>
                (CoreFormationEffectFamilies.Survival, "spirit_aegis", "yang", .66f, .32f, 48f),
            var entry when entry == CoreFormationSkills.ChaosRebirth =>
                (CoreFormationEffectFamilies.Survival, "chaos_orb", "entropy", 1.7f, .43f, -95f),
            _ => default
        };
        if (family == null) return SkillEmissionResult.Unhandled;
        if (emission.Sourceless || emission.Source.isRekt() || !emission.Source.isActor() ||
            !emission.Source.a.isAlive()) return SkillEmissionResult.Rejected;
        var owner = emission.Source.a.GetExtend();
        if (!CoreFormationEffectResolver.TryResolveFamily(owner, family, out var effect) ||
            skill == CoreFormationSkills.InfantGuard && effect.Definition.TriggerSkill != skill ||
            skill == CoreFormationSkills.ChaosRebirth && effect.Definition.TriggerSkill != skill)
            return SkillEmissionResult.Rejected;

        var asset = skill.GetComponent<SkillContainer>().Asset;
        float duration = asset.ResolveRuntimeLifetime(skill, asset.ImpactProfile.Lifetime * asset.ImpactTuning.LifetimeMultiplier);
        Vector3 point = emission.Target?.isRekt() == false ? emission.Target.GetSimPos() : emission.TargetPos;
        var definition = new AbilityDefinition
        {
            Id = asset.id, Name = asset.id.Localize(), Resource = "none", Cost = 0f, Cooldown = 0f,
            Lifetime = duration, TargetRange = 10000f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat, Target = ActiveAbilityTargetMode.Point },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Target, Height = height },
            Impact = new ConstructionImpact { Targets = SkillUseTargetRelation.Self, DamageScale = 0f },
            Root = new ConstructionNode
            {
                Id = "cue", Form = form, Matter = matter, Scale = scale, Volume = 0f,
                ContactRole = ConstructionContactRole.Decoration, ActiveUntil = duration * .58f,
                Operations = new[]
                {
                    new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = duration * .27f },
                    new ConstructionOperation { Kind = ConstructionOperationKind.Spin, Vector = new[] { 0f, 0f, spin } }
                }
            },
            Steps = new[]
            {
                new ConstructionStep
                {
                    Id = "close", Operation = new ConstructionOperation
                        { Kind = ConstructionOperationKind.Grow, At = duration * .58f, Seconds = duration * .40f, TargetScale = 0f },
                    Inputs = new Dictionary<string, ConstructionOutputReference> { ["part"] = new() { Node = "root" } }
                }
            }
        };
        var target = new ActiveAbilityTarget(null, point, runtimeData: emission.RuntimeData,
            worldScope: emission.RuntimeData.WorldScope);
        var force = new GetHitEvent { DamageOrigin = emission.RuntimeData.DamageOrigin };
        force.BindAttacker(emission.Source);
        return ConstructedAbilityService.StartPrepaid(owner, definition, target, emission.SourcePos, emission.Payment,
            force, sourceInstance: skill.Id, spatialSource: emission.SpatialSource,
            reportPreview: emission.ReportPreview)
            ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }
}
