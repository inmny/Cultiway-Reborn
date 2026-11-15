using System;
using System.Collections.Generic;
using Cultiway.Content.Components.Skill;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Settings;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>原墙与护盾共用真实形体、明确接触作用、来源连接及防御容量；逐发支付和并存上限沿用正式受理。</summary>
internal static class BarrierConstructionMigration
{
    /// <summary>已经完整映射的原词条；额外作用不静默丢弃。</summary>
    private static readonly HashSet<Type> supported = new()
    {
        typeof(Trajectory), typeof(HasteModifier), typeof(HugeModifier), typeof(EmpowerModifier),
        typeof(ProficiencyModifier), typeof(BurstCount), typeof(SalvoCount), typeof(ComboModifier), typeof(VolleyModifier)
    };

    /// <summary>仅接管完整可表达的实际发射，保留原逐发支付及受理失败的撤销。</summary>
    /// <param name="emission">已到达真实发射时点的原步骤。</param>
    internal static SkillEmissionResult Emit(SkillEmission emission)
    {
        Entity skill = emission.SkillContainer;
        if (emission.Sourceless || emission.Source?.isActor() != true ||
            Form(skill.GetComponent<SkillContainer>().Asset) == null)
            return SkillEmissionResult.Unhandled;
        if (emission.Source.isRekt() || !emission.Source.a.isAlive()) return SkillEmissionResult.Rejected;
        if (emission.SpatialSource != null && emission.SpatialSource != emission.Source ||
            emission.AttackKingdom != null && emission.AttackKingdom != emission.Source.kingdom || emission.RuntimeData.HasElementOverride)
            return SkillEmissionResult.Unhandled;
        AbilityDefinition definition = Convert(emission, out GetHitEvent damage);
        if (definition == null) return SkillEmissionResult.Unhandled;
        var target = new ActiveAbilityTarget(null, emission.TargetPos, attackKingdom: emission.AttackKingdom,
            runtimeData: emission.RuntimeData, worldScope: emission.RuntimeData.WorldScope);
        return ConstructedAbilityService.StartPrepaid(emission.Source.a.GetExtend(), definition, target,
            emission.SourcePos, emission.Payment, damage, sourceInstance: skill.Id) ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>已完整映射的原墙与护盾分别提供自身形体和实际材料。</summary>
    /// <param name="asset">原来源登记。</param>
    private static string Form(SkillEntityAsset asset) => asset == SkillEntities.EarthWall ? "earth_wall" :
        asset == SkillEntities.IceWall ? "ice_wall" : asset == SkillEntities.WoodWall ? "wood_wall" :
        asset == SkillEntities.FireWall ? "fire_wall" : asset == SkillEntities.WindWall ? "wind_wall" :
        asset == SkillEntities.SwordCurtain ? "sword_curtain" :
        asset == SkillEntities.WaterCurtain ? "water_curtain" :
        asset == SkillEntities.SpiritShield ? "spirit_aegis" : asset == SkillEntities.FlameShield ? "flame_aegis" :
        asset == SkillEntities.WindShield ? "wind_aegis" : asset == SkillEntities.LightningShield ? "lightning_aegis" :
        asset == SkillEntities.YangShield ? "solar_aegis" : null;

    /// <summary>先核对附加作用，再将实际强度、尺寸、寿命和固定目标展开为可保存资料。</summary>
    /// <param name="emission">原来源的本次发射输入。</param>
    /// <param name="damage">保留原绝对强度及境界的真实伤害。</param>
    private static AbilityDefinition Convert(SkillEmission emission, out GetHitEvent damage)
    {
        damage = default;
        Entity skill = emission.SkillContainer;
        var container = skill.GetComponent<SkillContainer>();
        string form = Form(container.Asset);
        if (form == null) return null;
        bool shield = container.Asset.ImpactProfile.Kind == Core.SkillLibV3.Impacts.SkillImpactKind.Shield;
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        if (trajectory != (shield ? SkillTrajectories.ShieldOnCaster.id : SkillTrajectories.WallBetweenCasterAndTarget.id) ||
            container.Asset.ImpactTuning.BarrierLengthMultiplier != 1f) return null;
        var callbacks = new List<Delegate>();
        foreach (Type type in skill.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(type)) continue;
            if (!supported.Contains(type) && !ContactStatusMigration.Supports(type)) return null;
            var modifier = (IModifier)skill.GetComponent(type);
            if (modifier.ModifierAsset.OnSetup != null) callbacks.AddRange(modifier.ModifierAsset.OnSetup.GetInvocationList());
        }
        Delegate[] setup = container.OnSetup?.GetInvocationList() ?? Array.Empty<Delegate>();
        if (setup.Length != callbacks.Count) return null;
        foreach (Delegate callback in setup) if (!callbacks.Contains(callback)) return null;
        if (container.Asset.Effects.Count != 0 || container.EffectPipeline?.Effects.Count > 0 ||
            container.OnTravel != null || !ContactStatusMigration.MatchesCallbacks(skill)) return null;
        bool configured = skill.TryGetComponent(out SkillSettings settings);
        if (configured && settings.NatureId != null) return null;

        // 固定墙体不因加速词条改变寿命；范围和容量仍从真实原发射计算。
        float lifetime = container.Asset.ResolveRuntimeLifetime(skill,
            container.Asset.ImpactProfile.Lifetime * container.Asset.ImpactTuning.LifetimeMultiplier);
        if (configured && settings.Duration > 0f) lifetime = settings.Duration;
        float scale = emission.RuntimeData.ResolveEffectScale() * SkillEffectRadius.ResolveCasterGrowth(emission.Source.a.GetExtend(), skill);
        if (skill.HasComponent<HugeModifier>()) scale *= SkillModifiers.Huge.EffectRadiusMultiplier(skill);
        if (configured) scale *= settings.AreaMultiplier;
        if (scale <= 0f || scale > 100f) return null;
        float strength = emission.Strength;
        if (configured) strength *= settings.PowerMultiplier;
        if (skill.TryGetComponent(out EmpowerModifier empower)) strength *= 1f + Mathf.Clamp(empower.SetupBonus, 0f, 10f);
        if (skill.TryGetComponent(out ComboModifier combo)) strength *= Mathf.Clamp(combo.DamageMultiplier, .05f, 2f);
        if (skill.TryGetComponent(out VolleyModifier volley)) strength *= Mathf.Clamp(volley.DamageMultiplier, .05f, 2f);
        damage = new GetHitEvent
        {
            Damage = strength, Element = container.Asset.Element, AttackType = AttackType.Other,
            AttackerPowerLevel = emission.PowerLevel, DamageOrigin = emission.RuntimeData.DamageOrigin
        };
        damage.BindAttacker(emission.Source);
        var profile = container.Asset.ImpactProfile;
        var tuning = container.Asset.ImpactTuning;
        bool contact = (!configured || settings.ContactEnabled) && (profile.ContactDamage || tuning.ContactDamage || tuning.ContactForce != 0f);
        ConstructionImpact impact = contact ? new ConstructionImpact
        {
            DamageScale = profile.ContactDamage || tuning.ContactDamage
                ? profile.DamageMultiplier * tuning.DamageMultiplier * emission.RuntimeData.ResolveEffectScale() / profile.DurabilityMultiplier : 0f,
            DamageBasis = ConstructionDamageBasis.InitialForce,
            Force = tuning.ContactForce, Lift = 0f,
            // 原墙只有水平推力；明确沿地持续到下一次接触，不凭空加上抛飞。
            SlideSeconds = tuning.ContactForce != 0f ? profile.RepeatHitInterval : 0f,
            Origin = ConstructionForceOrigin.Shape
        } : null;
        string matter = form switch { "ice_wall" => "ice", "wood_wall" => "wood", "fire_wall" => "fire",
            "wind_wall" or "wind_aegis" => "wind", "sword_curtain" => "iron", "flame_aegis" => "fire",
            "water_curtain" => "water", "earth_wall" => "earth",
            "spirit_aegis" => "spirit", "lightning_aegis" => "lightning", "solar_aegis" => "yang", _ => "physical" };
        Vector3 angles = shield ? Vector3.zero : FormResource.ReadFacing(new[] {0f, 1f, 0f}, new[] {0f, 0f, 1f}, form).eulerAngles;
        return ContactStatusMigration.Bind(new AbilityDefinition
        {
            Id = container.Asset.id, Name = skill.HasName ? skill.Name.value : container.Asset.id.Localize(),
            Lifetime = lifetime, ConcurrentLimit = container.Asset.ImpactProfile.PersistentLimit,
            Resource = "none", Cost = 0f, Cooldown = 0f, Power = 1f, TargetRange = 10000f,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
                Target = shield ? ActiveAbilityTargetMode.Self : ActiveAbilityTargetMode.Point },
            Binding = shield ? new ConstructionBinding { Anchor = AbilityAnchor.Source, Follow = true, ContactAtDestination = true } :
                new ConstructionBinding { Anchor = AbilityAnchor.Between, Fraction = .6f, FaceTarget = true },
            Root = new ConstructionNode
            {
                Id = shield ? "shield" : "wall", Form = form, Matter = matter,
                State = matter == "water" ? "liquid" : shield || matter is "fire" or "wind" ? "energy" : "solid", Scale = scale,
                Impact = impact,
                Contact = contact ? new ConstructionContactPolicy { Mode = ConstructionContactMode.Interval,
                    Interval = profile.RepeatHitInterval } : null,
                Rotation = new[] { angles.x, angles.y, angles.z },
                ContactRole = ConstructionContactRole.Barrier, BlocksGroundActors = !shield,
                Strength = container.Asset.ImpactProfile.DurabilityMultiplier,
                SourceLoss = shield ? ConstructionSourceLoss.End : ConstructionSourceLoss.Continue,
                Operations = shield ? Array.Empty<ConstructionOperation>() : new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Fly,
                    Vector = new float[3], WorldVelocity = true, Ground = ConstructionGroundResponse.Follow } }

            }
        }, skill, emission.RuntimeData.ResolveEffectScale());
    }

}
