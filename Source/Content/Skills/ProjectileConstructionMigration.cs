using System;
using System.Collections.Generic;
using System.Linq;
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
        asset == SkillEntities.FlyingSword ? ("flying_sword", "iron", "solid") :
        asset == SkillEntities.GoldenNeedle ? ("golden_needle", "gold", "solid") :
        asset == SkillEntities.PoisonNeedle ? ("poison_needle_0", "poison", "liquid") :
        asset == SkillEntities.StoneCone ? ("stone_cone", "earth", "solid") :
        asset == SkillEntities.IceLance ? ("ice_lance", "ice", "solid") :
        asset == SkillEntities.LightningSpear ? ("lightning_spear", "lightning", "energy") :
        asset == SkillEntities.WaterArrow ? ("water_arrow", "water", "liquid") :
        asset == SkillEntities.WoodSpear ? ("wood_spear", "wood", "solid") :
        asset == SkillEntities.SpiritBolt ? ("spirit_bolt_0", "spirit", "energy") :
        asset == SkillEntities.WindBullet ? ("wind_bullet", "wind", "energy") :
        asset == SkillEntities.WaterOrb ? ("water_orb", "water", "liquid") :
        asset == SkillEntities.RockProjectile ? ("rock_projectile", "earth", "solid") :
        asset == SkillEntities.YinBolt ? ("yin_bolt_0", "yin", "energy") :
        asset == SkillEntities.YangBolt ? ("yang_bolt_0", "yang", "energy") :
        asset == SkillEntities.IceShard ? ("ice_shard", "ice", "solid") :
        asset == SkillEntities.SwordQi ? ("sword_qi", "iron", "solid") :
        asset == SkillEntities.WindBlade ? ("wind_blade", "wind", "energy") :
        asset == SkillEntities.WaterBlade ? ("water_blade", "water", "liquid") :
        asset == SkillEntities.FireBlade ? ("fire_blade", "fire", "energy") :
        asset == SkillEntities.YinBlade ? ("yin_blade_0", "yin", "energy") :
        asset == SkillEntities.Fireball ? ("fireball", "fire", "energy") :
        asset == SkillEntities.ThunderOrb ? ("thunder_orb", "lightning", "energy") :
        asset == SkillEntities.ChaosOrb ? ("chaos_orb_0", "entropy", "energy") :
        asset == SkillEntities.FlameMeteor ? ("flame_meteor_0", "fire", "energy") :
        asset == SkillEntities.FallingRock ? ("falling_rock", "earth", "solid") :
        asset == SkillEntities.FallingTimber ? ("falling_timber", "wood", "solid") :
        asset == SkillEntities.LightningStrike ? ("lightning_strike", "lightning", "energy") :
        asset == SkillEntities.Icefall ? ("icefall", "ice", "solid") :
        asset == SkillEntities.EarthRidge ? ("earth_ridge_0", "earth", "solid") :
        asset == SkillEntities.TidalWave ? ("tidal_wave_0", "water", "liquid") :
        asset == SkillEntities.SpiritVine ? ("creeping_vine_0", "wood", "solid") : default;

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
        bool thunderOrb = container.Asset == SkillEntities.ThunderOrb;
        bool lightningStrike = container.Asset == SkillEntities.LightningStrike;
        bool icefall = container.Asset == SkillEntities.Icefall;
        bool fallingRock = container.Asset == SkillEntities.FallingRock;
        bool fallingTimber = container.Asset == SkillEntities.FallingTimber;
        bool chaosOrb = container.Asset == SkillEntities.ChaosOrb;
        bool fireBlade = container.Asset == SkillEntities.FireBlade;
        int variantCount = MigratedVariantCount(sourceForm.Form);
        bool phased = fireBlade || variantCount > 0;
        string form = fireBlade ? FireBladeForm(container.AnimationIndex) :
            lightningStrike && container.AnimationIndex == 0 ? "lightning_strike_0" :
            icefall && container.AnimationIndex == 0 ? "icefall_rain" :
            fallingRock && container.AnimationIndex == 0 ? "falling_rock_0" :
            fallingTimber && container.AnimationIndex == 0 ? "falling_timber_0" :
            variantCount > 0 ? IndexedForm(sourceForm.Form, container.AnimationIndex, variantCount) : sourceForm.Form;
        if (form == null) return null;
        SkillEntityAnimation animation = phased ? container.Asset.GetAnimation(container.AnimationIndex) : null;
        bool movesDuringAppearance = animation?.AppearanceGameplay == SkillAnimationGameplayFlags.Movement;
        float frameInterval = container.MotionProfile.FrameInterval;
        float appearance = phased ? ClipSeconds(animation.Appearance, frameInterval) : 0f;
        float dissipation = phased ? ClipSeconds(animation.Dissipation, frameInterval) : 0f;
        AbilityDefinition definition = Motion(emission, form, scale, continues,
            lifetime + (movesDuringAppearance ? appearance : 0f), initialAngle, speedScale, out forward);
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
        bool contact = !configured || settings.ContactEnabled;
        definition.Root.Impact = contact && (blast || continues)
            ? new ConstructionImpact { DamageScale = blast ? 0f : damageMultiplier }
            : null;
        definition.Root.ContactRole = contact ? ConstructionContactRole.Attack : ConstructionContactRole.Decoration;
        if (blast)
        {
            bool meteor = container.Asset == SkillEntities.FlameMeteor;
            string burstForm = meteor ? "flame_meteor_impact_motion" : thunderOrb ? "thunder_orb_impact" :
                lightningStrike ? "lightning_strike_impact" : icefall ? "icefall_impact" :
                fallingRock ? "falling_rock_impact" : fallingTimber ? "falling_timber_impact" :
                chaosOrb ? "chaos_orb_impact" : null;
            float burstDuration = Mathf.Min(.22f, lifetime);
            definition.Root.Children = new[] { new ConstructionNode
            {
                Id = "burst", Form = burstForm ?? sourceForm.Matter + "_burst", AnimateWhileHeld = icefall,
                Matter = sourceForm.Matter, State = sourceForm.State,
                Scale = scale * container.Asset.ImpactProfile.EffectRadius * container.Asset.ImpactTuning.EffectRadiusMultiplier / .9f,
                DisappearAt = burstDuration, ActiveUntil = Mathf.Min(.025f, lifetime),
                Activation = new ConstructionActivation { Trigger = ConstructionChangeTrigger.End,
                    Placement = ConstructionEventPlacement.Event, InheritRemainder = contact, ResetContacts = contact,
                    BeforeSourceExpires = meteor || thunderOrb || lightningStrike || icefall || fallingRock || fallingTimber ||
                                          chaosOrb || container.Asset == SkillEntities.Fireball },
                Contact = new ConstructionContactPolicy { Mode = ConstructionContactMode.Once, MaximumPerTarget = 1 },
                Impact = contact ? new ConstructionImpact { DamageScale = damageMultiplier } : null,
                ContactRole = contact ? ConstructionContactRole.Attack : ConstructionContactRole.Decoration,
                // 展开的爆发在落点清掉入射速度。保留制作速度时，短寿命只截断动作。
                Operations = burstForm != null
                    ? new[]
                    {
                        new ConstructionOperation { Kind = ConstructionOperationKind.Fly, Vector = new float[3], InheritVelocity = false },
                        new ConstructionOperation { Kind = ConstructionOperationKind.Animate, Clip = "spread" }
                    }
                    : new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Stop,
                        When = new ConstructionCondition { Source = "..", Trigger = ConstructionChangeTrigger.End } } }
            } };
        }
        float roll = form switch
        {
            "water_orb" => 315f, "water_orb_1" => -260f, "water_orb_4" => 425f, "water_arrow_1" => 350f,
            "wind_bullet" => 410f, "thunder_orb_2" => 540f, "thunder_orb_3" => -360f, _ => 0f
        };
        if (roll != 0f)
            definition.Root.Operations = definition.Root.Operations.Concat(new[]
            {
                new ConstructionOperation { Kind = ConstructionOperationKind.Spin, Vector = new[] { 0f, 0f, roll } }
            }).ToArray();
        if (form == "wood_spear_1")
            definition.Root.Operations = definition.Root.Operations.Concat(new[]
            {
                new ConstructionOperation { Kind = ConstructionOperationKind.Animate, Clip = "gather_fibers",
                    Seconds = ClipSeconds(animation.Runtime, frameInterval) }
            }).ToArray();
        if (animation?.HasLifecycle == true &&
            !SetProjectileLifecycle(definition, animation.AppearanceGameplay, appearance, dissipation,
                container.Asset == SkillEntities.EarthRidge ? "collapse" : "dissolve")) return null;
        if (container.Asset == SkillEntities.SpiritVine && appearance > 0f)
        {
            // 根的成形先于推进和接触，生长资料贯穿两段而不在出手时重新开始。
            ConstructionOperation growth = definition.Root.Operations.First(operation => operation.Kind == ConstructionOperationKind.Animate);
            growth.At = 0f;
            growth.Seconds += appearance;
        }
        definition = ContactEffectMigration.Bind(definition, skill, emission.RuntimeData.ResolveEffectScale(), includeRoot: !blast, contactForceScale: !continues && !blast ? 1f / damageMultiplier : 1f, caster: emission.Source.a.GetExtend(), runtimeData: emission.RuntimeData);
        if (phased && definition.Root.RadialMotion != null)
            definition.Root.RadialMotion.StartAt = definition.Root.ActiveFrom;
        return definition;
    }

    /// <summary>按原索引使用各自的实体；变化也反映在沿真实焰锋的接触形状。</summary>
    /// <param name="index">原学习资料或存档保留的动画索引。</param>
    private static string FireBladeForm(int index) => index switch
    {
        0 => "fire_blade",
        1 => "fire_blade_fork",
        2 => "fire_blade_bright",
        3 => "fire_blade_hook",
        _ => null
    };

    /// <summary>原索引零沿用基础资源编号，其余索引各有可辨认的三维主体。</summary>
    /// <param name="form">不带索引的基础形体编号。</param>
    /// <param name="index">原学习资料或存档保留的动画索引。</param>
    /// <param name="count">该来源已逐项迁移的索引数量。</param>
    private static string IndexedForm(string form, int index, int count) =>
        index == 0 ? form : index > 0 && index < count ? form + "_" + index : null;

    /// <summary>只对已经制作全部旧索引的来源恢复独立形体与首尾阶段。</summary>
    /// <param name="form">该来源的基础形体。</param>
    private static int MigratedVariantCount(string form) => form switch
    {
        "fireball" => 18,
        "lightning_strike" => 11,
        "water_orb" => 5,
        "water_arrow" or "water_blade" or "thunder_orb" => 4,
        "falling_rock" => 3,
        "flying_sword" or "sword_qi" or "wind_bullet" or "wind_blade" or "wood_spear" or "falling_timber" => 2,
        "golden_needle" or "lightning_spear" or "ice_shard" or "ice_lance" or "icefall" or "rock_projectile" or "stone_cone" or
            "spirit_bolt_0" or "yin_bolt_0" or "yang_bolt_0" or "chaos_orb_0" or "poison_needle_0" or "yin_blade_0" or "flame_meteor_0" or
            "earth_ridge_0" or "tidal_wave_0" or "creeping_vine_0" => 1,
        _ => 0
    };

    /// <summary>沿用原成形期间允许的运动，并让解体只在实际结束点显示。</summary>
    /// <param name="definition">已经展开原轨迹和实际作用的投射主体。</param>
    /// <param name="gameplay">原成形期间明确允许的行为。</param>
    /// <param name="appearance">原成形的世界秒数。</param>
    /// <param name="dissipation">原收势的世界秒数。</param>
    /// <param name="endingClip">该实体制作的收势动作。</param>
    private static bool SetProjectileLifecycle(AbilityDefinition definition, SkillAnimationGameplayFlags gameplay,
        float appearance, float dissipation, string endingClip)
    {
        if (gameplay is not (SkillAnimationGameplayFlags.None or SkillAnimationGameplayFlags.Movement)) return false;
        bool moves = gameplay == SkillAnimationGameplayFlags.Movement;
        ConstructionNode root = definition.Root;
        float delay = moves ? 0f : appearance;
        float end = delay + (root.DisappearAt ?? definition.Lifetime);
        float fullLifetime = delay + definition.Lifetime + dissipation;
        if (fullLifetime > 3600f) return false;
        if (appearance > 0f)
        {
            root.ActiveFrom = appearance;
            foreach (ConstructionOperation operation in root.Operations)
            {
                operation.At += delay;
                if (moves && operation.Ground == ConstructionGroundResponse.End)
                    operation.Ground = ConstructionGroundResponse.EndWhenActive;
            }
            foreach (ConstructionStep step in definition.Steps) step.Operation.At += delay;
            root.Operations = new[] { new ConstructionOperation
                { Kind = ConstructionOperationKind.Grow, Seconds = appearance } }.Concat(root.Operations).ToArray();
        }
        root.ActiveUntil = end;
        root.DisappearAt = end;
        definition.Lifetime = fullLifetime;
        if (dissipation <= 0f) return true;
        var embers = new ConstructionNode
        {
            Id = "embers", Form = root.Form, Matter = root.Matter, State = root.State, Scale = root.Scale,
            Activation = new ConstructionActivation
            {
                Trigger = ConstructionChangeTrigger.End, Placement = ConstructionEventPlacement.Event
            },
            DisappearAt = dissipation, ContactRole = ConstructionContactRole.Decoration,
            SourceLoss = ConstructionSourceLoss.Continue, Volume = 0, AnimateWhileHeld = true,
            Operations = new[] { new ConstructionOperation
                { Kind = ConstructionOperationKind.Animate, Clip = endingClip, Seconds = dissipation } }
        };
        root.Children = (root.Children ?? Array.Empty<ConstructionNode>()).Concat(new[] { embers }).ToArray();
        return true;
    }

    /// <summary>用原动画的实际帧数和有效帧间隔还原阶段时长。</summary>
    /// <param name="clip">原出现或消散片段。</param>
    /// <param name="inheritedInterval">原运动配置提供的帧间隔。</param>
    internal static float ClipSeconds(SkillEntityAnimationClip clip, float inheritedInterval) =>
        clip == null ? 0f : clip.Frames.Length * clip.Settings.ResolveFrameInterval(inheritedInterval);

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

    /// <summary>按原起步和巡航速度换算抵达时长，并在原半秒上限内连续走完整弧。</summary>
    /// <param name="distance">实际指定起终点的直连长度。</param>
    /// <param name="speed">本次基础速度。</param><param name="launch">起步比例。</param><param name="cruise">巡航比例。</param>
    /// <param name="ramp">起步时长。</param>
    private static float ArcTravelTime(float distance, float speed, float launch, float cruise, float ramp)
    {
        const float deadline = .5f;
        const float minimum = .025f;
        float age = 0f;
        if (ramp > .001f)
            for (int i = 0; i < 8 && age < deadline; i++)
            {
                float end = Mathf.Min(deadline, ramp * (i + 1f) / 8f), remaining = 1f - i / 8f;
                float current = speed * (cruise + (launch - cruise) * remaining * remaining);
                float span = current * (end - age);
                if (span >= distance) return Mathf.Max(minimum, age + distance / current);
                distance -= span;
                age = end;
            }
        return cruise > 0f ? Mathf.Clamp(age + distance / (speed * cruise), minimum, deadline) : deadline;
    }

    /// <summary>输出自足的普通三轴参数。</summary><param name="value">实际数值。</param>
    private static float[] XYZ(Vector3 value) => new[] { value.x, value.y, value.z };
}
