using System.Collections.Generic;
using Cultiway.Content.Libraries;
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

/// <summary>三种范围主动分别沿用原本的结算规则。</summary>
internal enum FormationAreaKind
{
    /// <summary>龙相震击。</summary>
    Dragon,
    /// <summary>纯阳净域。</summary>
    Yang,
    /// <summary>玄阴寒域。</summary>
    Yin
}

/// <summary>冻结本次形成效果强度，与人物、模型及可编辑资料分开。</summary>
internal readonly struct FormationAreaContact
{
    /// <summary>实际结算规则。</summary>
    internal readonly FormationAreaKind Kind;
    /// <summary>本次出手确定的状态和资源强度。</summary>
    internal readonly float Strength;
    /// <summary>未受屏障或交锋衰减的震击伤害。</summary>
    internal readonly float DragonDamage;

    /// <summary>保存本次发射的不可变数值。</summary>
    /// <param name="kind">实际规则。</param><param name="strength">形成强度。</param>
    /// <param name="dragonDamage">震击原始伤害。</param>
    internal FormationAreaContact(FormationAreaKind kind, float strength, float dragonDamage)
    {
        Kind = kind;
        Strength = strength;
        DragonDamage = dragonDamage;
    }
}

/// <summary>修炼范围主动能力从原技能发射进入同一套立体接触。</summary>
internal static class FormationAreaConstruction
{
    /// <summary>根据原圆面半径和持续时间生成实际落点，继承该次施展的付款和所属人物。</summary>
    /// <param name="emission">原入口已受理的单次发射。</param>
    internal static SkillEmissionResult Emit(SkillEmission emission)
    {
        var skill = emission.SkillContainer;
        (string family, FormationAreaKind kind, string matter) = skill switch
        {
            var entry when entry == CoreFormationSkills.DragonAspectBurst =>
                (CoreFormationEffectFamilies.Dragon, FormationAreaKind.Dragon, "earth"),
            var entry when entry == CoreFormationSkills.PureYangDomain =>
                (CoreFormationEffectFamilies.Yang, FormationAreaKind.Yang, "yang"),
            var entry when entry == CoreFormationSkills.MysteriousYinDomain =>
                (CoreFormationEffectFamilies.Yin, FormationAreaKind.Yin, "yin"),
            _ => default
        };
        if (family == null) return SkillEmissionResult.Unhandled;
        if (emission.Sourceless || emission.Source.isRekt() || !emission.Source.isActor() ||
            !emission.Source.a.isAlive()) return SkillEmissionResult.Rejected;
        var caster = emission.Source.a.GetExtend();
        if (!CoreFormationEffectResolver.TryResolveFamily(caster, family, out var effect) ||
            effect.Definition.active?.SkillContainer != skill)
            return SkillEmissionResult.Rejected;

        var asset = skill.GetComponent<SkillContainer>().Asset;
        float radius = SkillEffectRadius.ResolveForCaster(caster, skill, asset.ImpactProfile.CollisionRadius);
        float duration = asset.ResolveRuntimeLifetime(skill, asset.ImpactProfile.Lifetime * asset.ImpactTuning.LifetimeMultiplier);
        float strength = emission.RuntimeData.ResolveEffectScale();
        float damage = kind == FormationAreaKind.Dragon ? emission.Strength * strength * 1.2f : 0f;
        const float release = .30f;
        var definition = new AbilityDefinition
        {
            Id = asset.id, Name = effect.Definition.active.GetName(),
            Resource = "none", Cost = 0f, Cooldown = 0f, Lifetime = duration + release, TargetRange = 10000f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat, Target = ActiveAbilityTargetMode.Point },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Target },
            Impact = new ConstructionImpact { Targets = SkillUseTargetRelation.Hostile, DamageScale = 0f },
            Root = new ConstructionNode
            {
                Id = "origin", Form = "pivot", Matter = matter, ContactRole = ConstructionContactRole.Decoration,
                Children = new[]
                {
                    new ConstructionNode
                    {
                        Id = "range", Form = "formation_area_sphere", Matter = matter,
                        Scale = radius, Volume = 0f, DisappearAt = duration, ContactRole = ConstructionContactRole.Attack,
                        Contact = new ConstructionContactPolicy { MaximumPerTarget = 1 }
                    },
                    Visual(kind, matter, radius)
                }
            },
            Steps = new[]
            {
                new ConstructionStep
                {
                    Id = "release", Operation = new ConstructionOperation
                        { Kind = ConstructionOperationKind.Grow, At = duration, Seconds = release, TargetScale = 0f },
                    Inputs = new Dictionary<string, ConstructionOutputReference>
                        { ["part"] = new() { Node = "root/field" } }
                }
            }
        };
        Vector3 point = emission.Target?.isRekt() == false ? emission.Target.GetSimPos() : emission.TargetPos;
        var target = new ActiveAbilityTarget(null, point, runtimeData: emission.RuntimeData,
            worldScope: emission.RuntimeData.WorldScope);
        var force = new GetHitEvent
        {
            Damage = Mathf.Max(1f, damage),
            Element = emission.RuntimeData.ResolveElement(asset.Element), AttackType = AttackType.Other,
            AttackerPowerLevel = emission.PowerLevel, DamageOrigin = emission.RuntimeData.DamageOrigin
        };
        force.BindAttacker(emission.Source);
        return ConstructedAbilityService.StartPrepaid(caster, definition, target, emission.SourcePos,
            emission.Payment, force, sourceInstance: skill.Id, spatialSource: emission.SpatialSource,
            groundContact: ConstructedAbilityService.LegacyContactElement(in emission),
            formationArea: new FormationAreaContact(kind, strength, damage))
            ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>按真实半径排布矮体分件。提高境界时拉开排布，保留地面、人物和边缘的可见空间。</summary>
    /// <param name="kind">本次产生的领域。</param><param name="matter">实际材料。</param>
    /// <param name="radius">真实接触半径。</param>
    private static ConstructionNode Visual(FormationAreaKind kind, string matter, float radius)
    {
        (string edge, string core, float edgeScale, float coreScale, int segments) = kind switch
        {
            FormationAreaKind.Dragon => ("earth_wall_strata_rubble", "earth_burst", .86f, 1.3f, 24),
            FormationAreaKind.Yang => ("solar_scale_release", "yang_bolt", 1.23f, 2.25f, 24),
            _ => ("yin_transfer_wrap", "yin_field_basin", 1.38f, 1.16f, 28)
        };
        var parts = new ConstructionNode[segments + 1];
        parts[0] = Decoration("core", core, coreScale, 0f, 0f, 0f, 0f);
        for (int i = 0; i < segments; i++)
        {
            float angle = (i + .13f * Mathf.Sin(i * 3.2f)) * (2f * Mathf.PI / segments);
            float distance = radius * (i % 3 == 0 ? .93f : .99f);
            float scale = Mathf.Min(edgeScale, radius * .24f) * (i % 4 == 0 ? 1.22f : .91f);
            parts[i + 1] = Decoration("rim" + i, edge, scale,
                Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance,
                angle * Mathf.Rad2Deg, .016f * (i % 4));
        }
        return new ConstructionNode
        {
            Id = "field", Form = "pivot", Matter = matter, ContactRole = ConstructionContactRole.Decoration,
            Volume = 0f, Children = parts
        };

        ConstructionNode Decoration(string name, string form, float scale, float x, float y, float angle, float delay) =>
            new()
            {
                Id = name, Form = form, Matter = matter, Scale = scale, Volume = 0f,
                ContactRole = ConstructionContactRole.Decoration,
                Offset = new[] { x, y, name == "core" && kind == FormationAreaKind.Yang ? .68f : .25f },
                Rotation = new[] { 0f, 0f, angle },
                Operations = new[]
                {
                    new ConstructionOperation { Kind = ConstructionOperationKind.Grow, At = delay, Seconds = .13f }
                }
            };
    }
}
