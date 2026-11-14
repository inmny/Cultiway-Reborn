using System;
using System.Collections.Generic;
using Cultiway.Content.Components.Skill;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
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

/// <summary>飞剑基础轨迹的真实旧来源接入，发射后只保留普通构造，不建立旧执行实体。</summary>
internal static class FlyingSwordConstructionMigration
{
    /// <summary>本批已经逐项转换的词条，其他附加作用继续原入口并保持待迁移。</summary>
    private static readonly HashSet<Type> supported = new()
    {
        typeof(Trajectory), typeof(HasteModifier), typeof(HugeModifier), typeof(EmpowerModifier),
        typeof(ProficiencyModifier), typeof(BurstCount), typeof(SalvoCount), typeof(ComboModifier), typeof(VolleyModifier)
    };

    /// <summary>检查完整能力后只接管一次；真实受理失败不再落回旧发射。</summary>
    /// <param name="emission">原来源已经确定并到达时点的逐发步骤。</param>
    internal static SkillEmissionResult Emit(SkillEmission emission)
    {
        Entity skill = emission.SkillContainer;
        if (emission.Sourceless || emission.Source?.isActor() != true ||
            skill.GetComponent<SkillContainer>().SkillEntityAssetID != SkillEntities.FlyingSword.id)
            return SkillEmissionResult.Unhandled;
        if (emission.Source.isRekt() || !emission.Source.a.isAlive()) return SkillEmissionResult.Rejected;
        if (emission.SpatialSource != null && emission.SpatialSource != emission.Source ||
            emission.AttackKingdom != null && emission.AttackKingdom != emission.Source.kingdom || emission.RuntimeData.HasElementOverride)
            return SkillEmissionResult.Unhandled;
        AbilityDefinition definition = Convert(emission, out GetHitEvent damage, out Vector3 forward);
        if (definition == null) return SkillEmissionResult.Unhandled;
        var target = new ActiveAbilityTarget(definition.Use.Target == ActiveAbilityTargetMode.Point ? null : emission.Target,
            emission.TargetPos, attackKingdom: emission.AttackKingdom, runtimeData: emission.RuntimeData,
            worldScope: emission.RuntimeData.WorldScope);
        return ConstructedAbilityService.StartPrepaid(emission.Source.a.GetExtend(), definition, target,
            emission.SourcePos, emission.Payment, damage, forward) ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>先明确是否能够完整转换，再展开运动、尺寸、强度和寿命；不调用旧运行委托。</summary>
    /// <param name="emission">实际发射。</param><param name="damage">保留绝对数值及原功率的伤害。</param>
    /// <param name="forward">旧来源确定的完整方向。</param>
    private static AbilityDefinition Convert(SkillEmission emission, out GetHitEvent damage, out Vector3 forward)
    {
        damage = default; forward = emission.TargetPos - emission.SourcePos;
        forward = forward.sqrMagnitude > .0001f ? forward.normalized : Vector3.right;
        Entity skill = emission.SkillContainer; var container = skill.GetComponent<SkillContainer>();
        var setup = new List<Delegate>();
        foreach (Type type in skill.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(type)) continue;
            if (!supported.Contains(type)) return null;
            var modifier = (IModifier)skill.GetComponent(type);
            if (modifier.ModifierAsset.OnSetup != null) setup.AddRange(modifier.ModifierAsset.OnSetup.GetInvocationList());
        }
        Delegate[] actualSetup = container.OnSetup?.GetInvocationList() ?? Array.Empty<Delegate>();
        if (actualSetup.Length != setup.Count) return null;
        foreach (Delegate callback in actualSetup) if (!setup.Contains(callback)) return null;
        if (container.Asset.Effects.Count != 0 || container.EffectPipeline?.Effects.Count > 0 ||
            container.OnTravel != null || container.OnEffectObj != null) return null;
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        bool towardsDirection = trajectory == SkillTrajectories.TowardsDirection.id;
        bool noRotation = trajectory == SkillTrajectories.TowardsDirectionNoRot.id;
        bool towardsPosition = trajectory == SkillTrajectories.TowardsPosition.id;
        bool towardsTarget = trajectory == SkillTrajectories.TowardsTarget.id;
        if (!towardsDirection && !noRotation && !towardsPosition && !towardsTarget) return null;
        bool configured = skill.TryGetComponent(out SkillSettings settings);
        if (configured && settings.NatureId != null) return null;
        float lifetime = container.Asset.ResolveRuntimeLifetime(skill,
            container.Asset.ImpactProfile.Lifetime * container.Asset.ImpactTuning.LifetimeMultiplier);
        if (configured && settings.Duration > 0) lifetime = settings.Duration;
        float scale = emission.RuntimeData.ResolveEffectScale() * SkillEffectRadius.ResolveCasterGrowth(emission.Source.a.GetExtend(), skill);
        if (skill.HasComponent<HugeModifier>()) scale *= SkillModifiers.Huge.EffectRadiusMultiplier(skill);
        if (configured) scale *= settings.AreaMultiplier;
        float haste = skill.TryGetComponent(out HasteModifier h) ? Mathf.Clamp(1f + h.SpeedMultiplier, .1f, 10f) : 1f;
        var motion = container.MotionProfile;
        float speed = motion.ResolveSpeed(Vector2.Distance(emission.SourcePos, emission.TargetPos)) * haste;
        float turn = motion.TurnRate * haste;
        float ramp = Mathf.Max(0f, motion.RampDuration), launch = motion.LaunchMultiplier, cruise = motion.CruiseMultiplier;
        // 保留当前未能完整表达的超预算来源，不缩小原术来让转换通过。
        if (scale <= 0f || scale > 64f || lifetime <= 0f || lifetime > 3600f ||
            speed * Mathf.Max(launch, cruise) > 10000f || turn > 7200f) return null;
        Vector3 initial = Quaternion.AngleAxis(emission.InitialAngleOffsetDegrees, Vector3.forward) * forward;
        Quaternion facing = Quaternion.LookRotation(initial,
            Mathf.Abs(Vector3.Dot(initial, Vector3.forward)) > .999f ? Vector3.up : Vector3.forward);
        var goal = noRotation ? null : new ConstructionFlightGoal
        {
            Anchor = towardsDirection ? ConstructionFlightAnchor.Direction : ConstructionFlightAnchor.Target,
            Follow = towardsTarget,
            Arrival = towardsDirection ? ConstructionFlightArrival.None : ConstructionFlightArrival.End,
            Radius = .01f
        };
        var operations = new List<ConstructionOperation>
        {
            new()
            {
                Kind = ConstructionOperationKind.Fly, WorldVelocity = true, Vector = XYZ(initial * speed * (ramp > .001f ? launch : cruise)),
                TurnRate = noRotation ? 0f : turn, InheritVelocity = false, Goal = goal
            }
        };
        // 起步曲线使用八个确定时点，调整速度不重新开始飞行，也不重置目标和接触记录。
        if (ramp > .001f)
            for (int i = 1; i <= 8; i++)
            {
                float at = ramp * i / 8f;
                if (at >= lifetime) break;
                float remaining = 1f - i / 8f;
                operations.Add(new ConstructionOperation { Kind = ConstructionOperationKind.ChangeSpeed, At = at,
                    Speed = speed * (cruise + (launch - cruise) * remaining * remaining) });
            }
        float strength = emission.Strength * emission.RuntimeData.ResolveEffectScale();
        if (configured) strength *= settings.PowerMultiplier;
        if (skill.TryGetComponent(out EmpowerModifier empower)) strength *= 1f + Mathf.Clamp(empower.SetupBonus, 0f, 10f);
        if (skill.TryGetComponent(out VolleyModifier volley)) strength *= Mathf.Clamp(volley.DamageMultiplier, .05f, 2f);
        strength *= container.Asset.ImpactProfile.DamageMultiplier * container.Asset.ImpactTuning.DamageMultiplier;
        damage = new GetHitEvent { Damage = strength, Element = container.Asset.Element, AttackType = AttackType.Other,
            AttackerPowerLevel = emission.PowerLevel, DamageOrigin = emission.RuntimeData.DamageOrigin };
        damage.BindAttacker(emission.Source);
        return new AbilityDefinition
        {
            Id = container.Asset.id, Name = skill.HasName ? skill.Name.value : container.Asset.id.Localize(),
            Lifetime = lifetime, Resource = "none", Cost = 0f, Cooldown = 0, Power = 1, TargetRange = 10000,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
                Target = towardsTarget ? ActiveAbilityTargetMode.ObjectOrPoint : ActiveAbilityTargetMode.Point },
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source },
            Root = new ConstructionNode
            {
                Id = "sword", Form = "sword", Matter = "iron", Scale = scale, Rotation = XYZ(facing.eulerAngles),
                OnContact = ConstructionContactResponse.End,
                ContactRole = configured && !settings.ContactEnabled ? ConstructionContactRole.Decoration : ConstructionContactRole.Attack,
                Operations = operations.ToArray()
            }
        };
    }

    /// <summary>输出自足的普通三轴参数。</summary><param name="value">实际数值。</param>
    private static float[] XYZ(Vector3 value) => new[] { value.x, value.y, value.z };
}
