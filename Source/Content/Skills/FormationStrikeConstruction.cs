using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>由修炼事件点名的追加伤害，从对应人物身边显形并由共同接触结算。</summary>
internal static class FormationStrikeConstruction
{
    /// <summary>保留原碰撞大小、时间、伤害和本次触发确定的元素。</summary>
    /// <param name="emission">原修炼效果排入的实际发射步骤。</param>
    internal static SkillEmissionResult Emit(SkillEmission emission)
    {
        var skill = emission.SkillContainer;
        bool fivePhase = skill == CoreFormationSkills.FivePhaseStrike;
        bool rebirth = skill == CoreFormationSkills.ChaosRebirthEcho;
        if (!fivePhase && !rebirth && skill != CoreFormationSkills.ChaosEcho)
            return SkillEmissionResult.Unhandled;
        if (emission.Sourceless || emission.Source.isRekt() || !emission.Source.isActor() ||
            !emission.Source.a.isAlive() || emission.Target.isRekt() || !emission.Target.isActor())
            return SkillEmissionResult.Rejected;
        var caster = emission.Source.a.GetExtend();
        string family = fivePhase ? CoreFormationEffectFamilies.FivePhase : CoreFormationEffectFamilies.Chaos;
        if (!CoreFormationEffectResolver.TryResolveFamily(caster, family, out var effect) ||
            effect.Definition.TriggerSkill != skill)
            return SkillEmissionResult.Rejected;

        var asset = skill.GetComponent<SkillContainer>().Asset;
        var element = emission.RuntimeData.ResolveElement(asset.Element);
        string form = rebirth ? "entropy_burst" : "chaos_orb_impact";
        string matter = "entropy";
        if (fivePhase)
        {
            (form, matter) = element switch
            {
                var phase when phase[ElementIndex.Iron] > 0f => ("sword_qi", "iron"),
                var phase when phase[ElementIndex.Wood] > 0f => ("wood_burst", "wood"),
                var phase when phase[ElementIndex.Water] > 0f => ("water_orb_2", "water"),
                var phase when phase[ElementIndex.Fire] > 0f => ("fire_burst", "fire"),
                _ => ("earth_burst", "earth")
            };
        }

        float radius = SkillEffectRadius.ResolveForCaster(caster, skill, asset.ImpactProfile.CollisionRadius);
        float duration = asset.ResolveRuntimeLifetime(skill, asset.ImpactProfile.Lifetime * asset.ImpactTuning.LifetimeMultiplier);
        float strength = emission.Strength * emission.RuntimeData.ResolveEffectScale();
        var definition = new AbilityDefinition
        {
            Id = asset.id, Name = asset.id.Localize(), Resource = "none", Cost = 0f, Cooldown = 0f,
            Lifetime = duration, TargetRange = 10000f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat, Target = ActiveAbilityTargetMode.Object },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Target },
            Impact = new ConstructionImpact
            {
                SelectedTargetOnly = true, Targets = SkillUseTargetRelation.Hostile, Element = element,
                DamageBasis = ConstructionDamageBasis.InitialForce,
                DamageScale = asset.ImpactProfile.DamageMultiplier * asset.ImpactTuning.DamageMultiplier
            },
            Root = new ConstructionNode
            {
                Id = "strike", Form = "formation_area_sphere", Matter = matter, Scale = radius, Volume = 0f,
                ContactRole = ConstructionContactRole.Attack, Contact = new ConstructionContactPolicy { MaximumPerTarget = 1 },
                Children = new[]
                {
                    new ConstructionNode
                    {
                        Id = "burst", Form = form, Matter = matter, Scale = Mathf.Min(1.5f, 1f / radius), Volume = 0f,
                        ContactRole = ConstructionContactRole.Decoration, ActiveUntil = duration * .52f,
                        Operations = new[]
                        {
                            new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = duration * .16f }
                        }
                    }
                }
            },
            Steps = new[]
            {
                new ConstructionStep
                {
                    Id = "fade", Operation = new ConstructionOperation
                        { Kind = ConstructionOperationKind.Grow, At = duration * .52f, Seconds = duration * .46f, TargetScale = 0f },
                    Inputs = new Dictionary<string, ConstructionOutputReference> { ["part"] = new() { Node = "root/burst" } }
                }
            }
        };
        var target = new ActiveAbilityTarget(emission.Target, emission.Target.GetSimPos(),
            attackKingdom: emission.AttackKingdom, runtimeData: emission.RuntimeData, worldScope: emission.RuntimeData.WorldScope);
        var damage = new GetHitEvent
        {
            Damage = strength, Element = element, AttackType = AttackType.Other,
            AttackerPowerLevel = emission.PowerLevel, DamageOrigin = emission.RuntimeData.DamageOrigin
        };
        var attacker = emission.SpatialSource.isRekt() ? emission.Source : emission.SpatialSource;
        damage.BindAttacker(attacker);
        return ConstructedAbilityService.StartPrepaid(caster, definition, target, emission.SourcePos, emission.Payment,
            damage, sourceInstance: skill.Id, spatialSource: emission.SpatialSource,
            groundContact: ConstructedAbilityService.LegacyContactElement(in emission), explicitReactionTarget: true)
            ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }
}
