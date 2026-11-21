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
            var entry when entry == CoreFormationSkills.BodyCounter =>
                (CoreFormationEffectFamilies.Body, "earth_wall_strata_rubble", "earth", FormationTargetEffect.BodyCounter),
            var entry when entry == CoreFormationSkills.PrimalBodyCounter =>
                (CoreFormationEffectFamilies.Body, "earth_burst", "earth", FormationTargetEffect.BodyCounter),
            var entry when entry == CoreFormationSkills.CondensedRelease =>
                (CoreFormationEffectFamilies.Condensed, "entropy_wedge", "entropy", FormationTargetEffect.CondensedRelease),
            var entry when entry == CoreFormationSkills.SwordChase =>
                (CoreFormationEffectFamilies.Sword, "flying_sword", "iron", FormationTargetEffect.SwordChase),
            var entry when entry == CoreFormationSkills.SwordEmbryoStrike =>
                (CoreFormationEffectFamilies.Sword, "flying_sword_1", "iron", FormationTargetEffect.SwordChase),
            var entry when entry == CoreFormationSkills.YinDrain =>
                (CoreFormationEffectFamilies.Yin, "yin_bolt_0", "yin", FormationTargetEffect.YinDrain),
            var entry when entry == CoreFormationSkills.MysteriousYinDrain =>
                (CoreFormationEffectFamilies.Yin, "yin_bolt", "yin", FormationTargetEffect.YinDrain),
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

        if (contact == FormationTargetEffect.BodyCounter)
        {
            if (!SkillCasterContextService.TryGetCurrent(caster, out SkillCasterContext context))
                context = SkillCasterContextService.Resolve(caster);
            if (!context.IsValid || context.Kind != SkillCarrierKind.Physical || !context.HasPhysicalBody)
                return SkillEmissionResult.Rejected;
        }

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
        Vector3 forward = default;
        var definition = formation.TracksTarget
            ? ProjectileConstructionMigration.Motion(emission, "formation_area_sphere", radius, false, duration,
                0f, 1f, out forward, effect.Definition.GetName())
            : Manifestation(asset.id, effect.Definition.GetName(), duration, radius, form, matter);
        if (definition == null) return SkillEmissionResult.Rejected;

        definition.Use = new ConstructionUse
            { Channels = ActiveAbilityChannel.Combat, Target = self ? ActiveAbilityTargetMode.Self : ActiveAbilityTargetMode.Object };
        definition.Impact = new ConstructionImpact
        {
            SelectedTargetOnly = true, Targets = self ? SkillUseTargetRelation.Self : SkillUseTargetRelation.Hostile, Element = element,
            DamageBasis = ConstructionDamageBasis.InitialForce,
            DamageScale = asset.ImpactProfile.DamageMultiplier * asset.ImpactTuning.DamageMultiplier
        };
        definition.Root.Matter = matter;
        definition.Root.Volume = 0f;
        definition.Root.ContactRole = ConstructionContactRole.Attack;
        definition.Root.Contact = new ConstructionContactPolicy { MaximumPerTarget = 1 };
        if (formation.TracksTarget)
        {
            // 旧圆形碰撞仍在飞行轴上，立体本体只随它移动和转向。
            definition.Root.OnContact = ConstructionContactResponse.End;
            definition.Root.Children = new[]
            {
                new ConstructionNode
                {
                    Id = "body", Form = form, Matter = matter, Scale = Mathf.Min(1.3f, 1.75f / radius),
                    Volume = 0f, ContactRole = ConstructionContactRole.Decoration
                }
            };
        }
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
            damage, direction: formation.TracksTarget ? forward : null, sourceInstance: skill.Id, spatialSource: emission.SpatialSource,
            groundTravel: formation.TracksTarget ? LegacyGroundTravel.From(in emission, 0f) : null,
            groundContact: ConstructedAbilityService.LegacyContactElement(in emission), explicitReactionTarget: true,
            formationTarget: formation) ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>建立在目标实际位置显形的一次接触与有限退场。</summary>
    /// <param name="id">原技能编号。</param><param name="name">当前修炼效果名。</param>
    /// <param name="duration">原实际寿命。</param><param name="radius">真实接触半径。</param>
    /// <param name="form">对应外观。</param><param name="matter">实际材料。</param>
    private static AbilityDefinition Manifestation(string id, string name, float duration, float radius, string form, string matter) =>
        new AbilityDefinition
        {
            Id = id, Name = name, Resource = "none", Cost = 0f, Cooldown = 0f,
            Lifetime = duration, TargetRange = 10000f,
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Target },
            Root = new ConstructionNode
            {
                Id = "strike", Form = "formation_area_sphere", Scale = radius,
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
}
