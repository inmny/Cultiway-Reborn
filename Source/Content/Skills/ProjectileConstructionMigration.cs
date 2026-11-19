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

/// <summary>飞剑、贯穿、刃形波、范围爆发、地面推进及普通投射共用真实旧来源转换；发射后只保留普通构造，不建立旧执行实体。</summary>
internal static partial class ProjectileConstructionMigration
{
    /// <summary>本批已经逐项转换的词条，其他附加作用继续原入口并保持待迁移。</summary>
    private static readonly HashSet<Type> supported = new()
    {
        typeof(Trajectory), typeof(HasteModifier), typeof(HugeModifier), typeof(EmpowerModifier),
        typeof(ProficiencyModifier), typeof(BurstCount), typeof(SalvoCount), typeof(ComboModifier), typeof(VolleyModifier)
    };

    /// <summary>来源只选择实际形体和材料，所有运动及命中继续共同执行。</summary>
    /// <param name="asset">原已学来源登记。</param>
    private static (string Form, string Matter, string State) SourceForm(SkillEntityAsset asset) =>
        asset == SkillEntities.FlyingSword ? ("sword", "iron", "solid") :
        asset == SkillEntities.GoldenNeedle ? ("golden_needle", "gold", "solid") :
        asset == SkillEntities.PoisonNeedle ? ("poison_needle", "poison", "liquid") :
        asset == SkillEntities.StoneCone ? ("stone_cone", "earth", "solid") :
        asset == SkillEntities.IceLance ? ("ice_lance", "ice", "solid") :
        asset == SkillEntities.LightningSpear ? ("lightning_spear", "lightning", "energy") :
        asset == SkillEntities.WaterArrow ? ("water_arrow", "water", "liquid") :
        asset == SkillEntities.WoodSpear ? ("wood_spear", "wood", "solid") :
        asset == SkillEntities.SpiritBolt ? ("spirit_bolt", "spirit", "energy") :
        asset == SkillEntities.WindBullet ? ("wind_bullet", "wind", "energy") :
        asset == SkillEntities.WaterOrb ? ("water_orb", "water", "liquid") :
        asset == SkillEntities.RockProjectile ? ("rock_projectile", "earth", "solid") :
        asset == SkillEntities.YinBolt ? ("yin_bolt", "yin", "energy") :
        asset == SkillEntities.YangBolt ? ("yang_bolt", "yang", "energy") :
        asset == SkillEntities.IceShard ? ("ice_shard", "ice", "solid") :
        asset == SkillEntities.SwordQi ? ("sword_qi", "iron", "solid") :
        asset == SkillEntities.WindBlade ? ("wind_blade", "wind", "energy") :
        asset == SkillEntities.WaterBlade ? ("water_blade", "water", "liquid") :
        asset == SkillEntities.FireBlade ? ("fire_blade", "fire", "energy") :
        asset == SkillEntities.YinBlade ? ("yin_blade", "yin", "energy") :
        asset == SkillEntities.Fireball ? ("fireball", "fire", "energy") :
        asset == SkillEntities.ThunderOrb ? ("thunder_orb", "lightning", "energy") :
        asset == SkillEntities.ChaosOrb ? ("chaos_orb", "entropy", "energy") :
        asset == SkillEntities.FlameMeteor ? ("flame_meteor", "fire", "energy") :
        asset == SkillEntities.FallingRock ? ("falling_rock", "earth", "solid") :
        asset == SkillEntities.FallingTimber ? ("falling_timber", "wood", "solid") :
        asset == SkillEntities.LightningStrike ? ("lightning_strike", "lightning", "energy") :
        asset == SkillEntities.Icefall ? ("icefall", "ice", "solid") :
        asset == SkillEntities.EarthRidge ? ("earth_ridge", "earth", "solid") :
        asset == SkillEntities.TidalWave ? ("tidal_wave", "water", "liquid") :
        asset == SkillEntities.SpiritVine ? ("creeping_vine", "wood", "solid") : default;

    /// <summary>检查完整能力后只接管一次；真实受理失败不再落回旧发射。</summary>
    /// <param name="emission">原来源已经确定并到达时点的逐发步骤。</param>
    internal static SkillEmissionResult Emit(SkillEmission emission)
    {
        Entity skill = emission.SkillContainer;
        if (emission.Sourceless || emission.Source?.isActor() != true ||
            SourceForm(skill.GetComponent<SkillContainer>().Asset).Form == null)
            return SkillEmissionResult.Unhandled;
        if (emission.Source.isRekt() || !emission.Source.a.isAlive()) return SkillEmissionResult.Rejected;
        if (emission.SpatialSource != null && !emission.SpatialSource.isActor())
            return SkillEmissionResult.Unhandled;
        AbilityDefinition definition = Convert(emission, out GetHitEvent damage, out Vector3 forward);
        if (definition == null) return SkillEmissionResult.Unhandled;
        var target = new ActiveAbilityTarget(definition.Use.Target == ActiveAbilityTargetMode.Point ? null : emission.Target,
            emission.TargetPos, attackKingdom: emission.AttackKingdom, runtimeData: emission.RuntimeData,
            worldScope: emission.RuntimeData.WorldScope);
        return ConstructedAbilityService.StartPrepaid(emission.Source.a.GetExtend(), definition, target,
            emission.SourcePos, emission.Payment, damage, forward, spatialSource: emission.SpatialSource) ? SkillEmissionResult.Accepted : SkillEmissionResult.Rejected;
    }

    /// <summary>先明确是否能够完整转换，再展开运动、尺寸、强度和寿命；不调用旧运行委托。</summary>
    /// <param name="emission">实际发射。</param><param name="damage">保留绝对数值及原功率的伤害。</param>
    /// <param name="forward">旧来源确定的完整方向。</param>
    private static AbilityDefinition Convert(SkillEmission emission, out GetHitEvent damage, out Vector3 forward)
    {
        damage = default; forward = emission.TargetPos - emission.SourcePos;
        forward = forward.sqrMagnitude > .0001f ? forward.normalized : Vector3.right;
        Entity skill = emission.SkillContainer; var container = skill.GetComponent<SkillContainer>();
        var sourceForm = SourceForm(container.Asset);
        if (sourceForm.Form == null) return null;
        bool blast = container.Asset.ImpactProfile.Kind is Core.SkillLibV3.Impacts.SkillImpactKind.Explosion or Core.SkillLibV3.Impacts.SkillImpactKind.HeavySkyfall;
        bool continues = container.Asset.ImpactProfile.ContinueAfterHit;
        bool once = container.Asset.ImpactProfile.HitOncePerTarget;
        var setup = new List<Delegate>();
        foreach (Type type in skill.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(type)) continue;
            if (!supported.Contains(type) && !ContactEffectMigration.Supports(type)) return null;
            var modifier = (IModifier)skill.GetComponent(type);
            if (modifier.ModifierAsset.OnSetup != null) setup.AddRange(modifier.ModifierAsset.OnSetup.GetInvocationList());
        }
        Delegate[] actualSetup = container.OnSetup?.GetInvocationList() ?? Array.Empty<Delegate>();
        if (actualSetup.Length != setup.Count) return null;
        foreach (Delegate callback in actualSetup) if (!setup.Contains(callback)) return null;
        if (container.Asset.Effects.Count != 0 || container.EffectPipeline?.Effects.Count > 0 ||
            !ContactEffectMigration.MatchesTravelCallbacks(skill) || !ContactEffectMigration.MatchesCallbacks(skill)) return null;
        bool configured = skill.TryGetComponent(out SkillSettings settings);
        if (!NatureConstructionMigration.Supports(skill)) return null;
        float setupScale = ContactEffectMigration.SetupStrengthScale(skill, out float initialAngle, out float speedScale);
        float lifetime = container.Asset.ResolveRuntimeLifetime(skill,
            container.Asset.ImpactProfile.Lifetime * container.Asset.ImpactTuning.LifetimeMultiplier);
        if (configured && settings.Duration > 0) lifetime = settings.Duration;
        float scale = emission.RuntimeData.ResolveEffectScale() * SkillEffectRadius.ResolveCasterGrowth(emission.Source.a.GetExtend(), skill);
        if (skill.HasComponent<HugeModifier>()) scale *= SkillModifiers.Huge.EffectRadiusMultiplier(skill);
        if (configured) scale *= settings.AreaMultiplier;
        if (scale <= 0f || scale > 64f || lifetime <= 0f || lifetime > 3600f) return null;
        AbilityDefinition definition = Motion(emission, sourceForm.Form, scale, continues, lifetime, initialAngle, speedScale, out forward);
        if (definition == null) return null;
        lifetime = definition.Lifetime;
        float strength = setupScale * emission.Strength * emission.RuntimeData.ResolveEffectScale();
        if (configured) strength *= settings.PowerMultiplier;
        if (skill.TryGetComponent(out EmpowerModifier empower)) strength *= 1f + Mathf.Clamp(empower.SetupBonus, 0f, 10f);
        if (skill.TryGetComponent(out VolleyModifier volley)) strength *= Mathf.Clamp(volley.DamageMultiplier, .05f, 2f);
        if (skill.TryGetComponent(out ComboModifier combo)) strength *= Mathf.Clamp(combo.DamageMultiplier, .05f, 2f);
        float damageMultiplier = container.Asset.ImpactProfile.DamageMultiplier * container.Asset.ImpactTuning.DamageMultiplier;
        // 防御量保留实际本体强度，持续投射的伤害倍率单独交给人物作用；命中不会凭空消耗本体。
        if (!continues && !blast) strength *= damageMultiplier;
        damage = new GetHitEvent { Damage = strength, Element = NatureConstructionMigration.Element(skill, emission.RuntimeData), AttackType = AttackType.Other,
            AttackerPowerLevel = emission.PowerLevel, DamageOrigin = emission.RuntimeData.DamageOrigin };
        damage.BindAttacker(emission.Source);
        definition.Root.Matter = sourceForm.Matter;
        definition.Root.State = sourceForm.State;
        definition.Root.OnContact = continues ? ConstructionContactResponse.Continue : ConstructionContactResponse.End;
        definition.Root.Contact = once ? new ConstructionContactPolicy { Mode = ConstructionContactMode.Once, MaximumPerTarget = 1 } : null;
        definition.Root.Impact = blast ? new ConstructionImpact { DamageScale = 0f } :
            continues && (!configured || settings.ContactEnabled) ? new ConstructionImpact { DamageScale = damageMultiplier } : null;
        definition.Root.ContactRole = configured && !settings.ContactEnabled ? ConstructionContactRole.Decoration : ConstructionContactRole.Attack;
        if (blast)
        {
            bool contact = !configured || settings.ContactEnabled;
            bool meteor = container.Asset == SkillEntities.FlameMeteor;
            float burstDuration = Mathf.Min(.22f, lifetime);
            definition.Root.Children = new[] { new ConstructionNode
            {
                Id = "burst", Form = meteor ? "flame_meteor_impact_motion" : sourceForm.Matter + "_burst",
                Matter = sourceForm.Matter, State = sourceForm.State,
                Scale = scale * container.Asset.ImpactProfile.EffectRadius * container.Asset.ImpactTuning.EffectRadiusMultiplier / .9f,
                DisappearAt = burstDuration, ActiveUntil = Mathf.Min(.025f, lifetime),
                Activation = new ConstructionActivation { Trigger = ConstructionChangeTrigger.End,
                    Placement = ConstructionEventPlacement.Event, InheritRemainder = contact, ResetContacts = contact },
                Contact = new ConstructionContactPolicy { Mode = ConstructionContactMode.Once, MaximumPerTarget = 1 },
                Impact = new ConstructionImpact { DamageScale = damageMultiplier },
                ContactRole = contact ? ConstructionContactRole.Attack : ConstructionContactRole.Decoration,
                // 余力交接也带来入射速度；炎陨在事件落点清掉这份速度，骨架仍可继续收势。
                Operations = meteor
                    ? new[]
                    {
                        new ConstructionOperation { Kind = ConstructionOperationKind.Fly, Vector = new float[3], InheritVelocity = false },
                        new ConstructionOperation { Kind = ConstructionOperationKind.Animate, Clip = "spread", Seconds = burstDuration }
                    }
                    : new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Stop,
                        When = new ConstructionCondition { Source = "..", Trigger = ConstructionChangeTrigger.End } } }
            } };
        }
        else if (container.Asset == SkillEntities.EarthRidge) SetRidgeCollapse(definition, lifetime, scale);
        return ContactEffectMigration.Bind(definition, skill, emission.RuntimeData.ResolveEffectScale(), includeRoot: !blast, contactForceScale: !continues && !blast ? 1f / damageMultiplier : 1f, caster: emission.Source.a.GetExtend(), runtimeData: emission.RuntimeData);
    }

    /// <summary>保留基准生成高度；按真实主体底部抬高出生点，不让放大后的尖端出生即穿地。</summary>
    /// <param name="form">实际主体资源。</param>
    /// <param name="facing">真实剑身朝向。</param><param name="scale">原词条给出的实际尺寸。</param>
    /// <param name="height">原轨迹的基准生成高度。</param>
    private static float DescentHeight(string form, Quaternion facing, float scale, float height)
    {
        FormResource resource = FormResources.Load(form); var pose = resource.Rig.CreatePose();
        float bottom = 0f;
        foreach (FormSurfaceVertex vertex in resource.GroundPoints()) bottom = Mathf.Min(bottom, (facing * vertex.Sample(pose)).z);
        foreach (FormContactBinding contact in resource.Contacts)
        {
            if (contact.IsPrism)
            {
                var box = contact.PrismFrame(pose, Matrix4x4.Rotate(facing));
                bottom = Mathf.Min(bottom, box.Bounds().min.z);
                continue;
            }
            bottom = Mathf.Min(bottom, (facing * contact.ABlend.Point(contact.A, pose)).z - contact.Radius);
            bottom = Mathf.Min(bottom, (facing * contact.BBlend.Point(contact.B, pose)).z - contact.Radius);
        }
        return Mathf.Max(height, -bottom * scale + .35f);
    }

    /// <summary>把原起步和最低下落速度展开为普通线段，横向追逐仍由独立中心运动完成。</summary>
    /// <param name="lifetime">原寿命。</param><param name="speed">词条修正后的基础速度。</param>
    /// <param name="launch">起步比例。</param><param name="cruise">巡航比例。</param><param name="ramp">起步时长。</param>
    /// <param name="minimum">最低下落速度。</param><param name="facing">固定剑身朝向。</param>
    private static ConstructionPath DescentPath(float lifetime, float speed, float launch, float cruise, float ramp,
        float minimum, Quaternion facing)
    {
        var times = new List<float> { 0f };
        if (ramp > .001f)
            for (int i = 1; i <= 8 && ramp * i / 8f < lifetime; i++) times.Add(ramp * i / 8f);
        times.Add(lifetime);
        var keys = new ConstructionPathKey[times.Count];
        keys[0] = new ConstructionPathKey();
        Vector3 down = Quaternion.Inverse(facing) * Vector3.back, position = Vector3.zero;
        for (int i = 1; i < keys.Length; i++)
        {
            float remaining = ramp > .001f ? 1f - Mathf.Min(8f, Mathf.Floor(times[i - 1] / ramp * 8f + .00001f)) / 8f : 0f;
            float velocity = Mathf.Max(minimum, speed * (cruise + (launch - cruise) * remaining * remaining));
            Vector3 delta = down * (velocity * (times[i] - times[i - 1]));
            position += delta;
            if (position.magnitude > 10000f) return null;
            keys[i - 1].Out = XYZ(delta / 3f);
            keys[i] = new ConstructionPathKey { At = times[i], Point = XYZ(position), In = XYZ(-delta / 3f) };
        }
        return new ConstructionPath { Keys = keys, Forward = null };
    }

    /// <summary>按原八段调速求指定时点前的中心路程，决定连续抵达所需速度。</summary>
    /// <param name="speed">本次基础速度。</param><param name="launch">起步比例。</param><param name="cruise">巡航比例。</param>
    /// <param name="ramp">起步时长。</param><param name="seconds">实际世界时长。</param>
    private static float PlannedDistance(float speed, float launch, float cruise, float ramp, float seconds)
    {
        if (ramp <= .001f) return speed * cruise * seconds;
        float total = 0f, previous = 0f;
        for (int i = 0; i < 8 && previous < seconds; i++)
        {
            float end = Mathf.Min(seconds, ramp * (i + 1f) / 8f), remaining = 1f - i / 8f;
            total += speed * (cruise + (launch - cruise) * remaining * remaining) * (end - previous);
            previous = end;
        }
        if (previous < seconds) total += speed * cruise * (seconds - previous);
        return total;
    }

    /// <summary>输出自足的普通三轴参数。</summary><param name="value">实际数值。</param>
    private static float[] XYZ(Vector3 value) => new[] { value.x, value.y, value.z };
}
