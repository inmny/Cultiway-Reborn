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

/// <summary>修炼反应在原事件点名的人物身边显形，由共用接触结算状态和伤害。</summary>
internal static class FormationTargetConstruction
{
    /// <summary>保留原碰撞大小、时间、伤害和本次触发确定的元素。</summary>
    /// <param name="emission">原修炼效果排入的实际发射步骤。</param>
    internal static SkillEmissionResult Emit(SkillEmission emission)
    {
        var skill = emission.SkillContainer;
        (string family, string form, string matter, FormationTargetEffect contact) = skill switch
        {
            var entry when entry == CoreFormationSkills.FivePhaseStrike =>
                (CoreFormationEffectFamilies.FivePhase, "earth_burst", "earth", FormationTargetEffect.None),
            var entry when entry == CoreFormationSkills.ChaosEcho =>
                (CoreFormationEffectFamilies.Chaos, "chaos_orb_impact", "entropy", FormationTargetEffect.None),
            var entry when entry == CoreFormationSkills.ChaosRebirthEcho =>
                (CoreFormationEffectFamilies.Chaos, "entropy_burst", "entropy", FormationTargetEffect.None),
            var entry when entry == CoreFormationSkills.IronSeverance =>
                (CoreFormationEffectFamilies.Iron, "sword_qi_1", "iron", FormationTargetEffect.ArmorBreak),
            var entry when entry == CoreFormationSkills.WoodVenomBloom =>
                (CoreFormationEffectFamilies.Wood, "wood_burst", "wood", FormationTargetEffect.Poison),
            var entry when entry == CoreFormationSkills.WaterFrostBind =>
                (CoreFormationEffectFamilies.Water, "frost_flower", "water", FormationTargetEffect.FrostBind),
            var entry when entry == CoreFormationSkills.FireBrand =>
                (CoreFormationEffectFamilies.Fire, "fire_burst", "fire", FormationTargetEffect.Burn),
            var entry when entry == CoreFormationSkills.FireEmberBurst =>
                (CoreFormationEffectFamilies.Fire, "flame_meteor_impact", "fire", FormationTargetEffect.EmberBurst),
            var entry when entry == CoreFormationSkills.CondensedRelease =>
                (CoreFormationEffectFamilies.Condensed, "entropy_wedge", "entropy", FormationTargetEffect.CondensedRelease),
            var entry when entry == CoreFormationSkills.WoodLifeReturn =>
                (CoreFormationEffectFamilies.Wood, "healing_seed", "wood", FormationTargetEffect.LifeReturn),
            var entry when entry == CoreFormationSkills.YangCleanse =>
                (CoreFormationEffectFamilies.Yang, "purification_surge", "yang", FormationTargetEffect.YangCleanse),
            var entry when entry == CoreFormationSkills.PureYangCleanse =>
                (CoreFormationEffectFamilies.Yang, "solar_scale_release", "yang", FormationTargetEffect.YangCleanse),
            _ => default
        };
        if (family == null) return SkillEmissionResult.Unhandled;
        if (emission.Sourceless || emission.Source.isRekt() || !emission.Source.isActor() ||
            !emission.Source.a.isAlive() || emission.Target.isRekt() || !emission.Target.isActor())
            return SkillEmissionResult.Rejected;
        var caster = emission.Source.a.GetExtend();
        bool self = contact is FormationTargetEffect.LifeReturn or FormationTargetEffect.YangCleanse;
        if (self && emission.Target != emission.Source ||
            !CoreFormationEffectResolver.TryResolveFamily(caster, family, out var effect) ||
            effect.Definition.TriggerSkill != skill &&
            contact is not (FormationTargetEffect.LifeReturn or FormationTargetEffect.EmberBurst))
            return SkillEmissionResult.Rejected;

        var asset = skill.GetComponent<SkillContainer>().Asset;
        var element = emission.RuntimeData.ResolveElement(asset.Element);
        if (skill == CoreFormationSkills.FivePhaseStrike)
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
        float effectScale = emission.RuntimeData.ResolveEffectScale();
        var formation = new FormationTargetContact(contact, effectScale);
        var definition = new AbilityDefinition
        {
            Id = asset.id, Name = effect.Definition.GetName(), Resource = "none", Cost = 0f, Cooldown = 0f,
            Lifetime = duration, TargetRange = 10000f,
            Use = new ConstructionUse
                { Channels = ActiveAbilityChannel.Combat, Target = self ? ActiveAbilityTargetMode.Self : ActiveAbilityTargetMode.Object },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Target },
            Impact = new ConstructionImpact
            {
                SelectedTargetOnly = true, Targets = self ? SkillUseTargetRelation.Self : SkillUseTargetRelation.Hostile, Element = element,
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
        if (formation.UsesPlanarSpread)
            definition.Impact.Spreads = new[]
            {
                new ConstructionContactSpread
                {
                    Radius = contact == FormationTargetEffect.EmberBurst ? 2.5f : 2f,
                    Outcome = new ConstructionContactOutcome
                        { Damage = new[] { new ConstructionContactDamage { PerForce = .35f, Element = element } } }
                }
            };

        var target = new ActiveAbilityTarget(emission.Target, emission.Target.GetSimPos(),
            attackKingdom: emission.AttackKingdom, runtimeData: emission.RuntimeData, worldScope: emission.RuntimeData.WorldScope);
        var damage = new GetHitEvent
        {
            Damage = emission.Strength * effectScale, Element = element, AttackType = AttackType.Other,
            AttackerPowerLevel = formation.UsesPlanarSpread ? null : emission.PowerLevel,
            DamageOrigin = emission.RuntimeData.DamageOrigin
        };
        var attacker = formation.UsesPlanarSpread || emission.SpatialSource.isRekt() ? emission.Source : emission.SpatialSource;
        damage.BindAttacker(attacker);
        return ConstructedAbilityService.StartPrepaid(caster, definition, target, emission.SourcePos, emission.Payment,
            damage, sourceInstance: skill.Id, spatialSource: emission.SpatialSource,
            groundContact: ConstructedAbilityService.LegacyContactElement(in emission), explicitReactionTarget: true,
            formationTarget: formation)
            ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }
}
