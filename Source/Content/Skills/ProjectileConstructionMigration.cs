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
internal static class ProjectileConstructionMigration
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
        var sourceForm = SourceForm(container.Asset);
        if (sourceForm.Form == null) return null;
        bool blast = container.Asset.ImpactProfile.Kind is Core.SkillLibV3.Impacts.SkillImpactKind.Explosion or Core.SkillLibV3.Impacts.SkillImpactKind.HeavySkyfall;
        bool continues = container.Asset.ImpactProfile.ContinueAfterHit;
        bool once = container.Asset.ImpactProfile.HitOncePerTarget;
        var setup = new List<Delegate>();
        foreach (Type type in skill.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(type)) continue;
            if (!supported.Contains(type) && !ContactStatusMigration.Supports(type)) return null;
            var modifier = (IModifier)skill.GetComponent(type);
            if (modifier.ModifierAsset.OnSetup != null) setup.AddRange(modifier.ModifierAsset.OnSetup.GetInvocationList());
        }
        Delegate[] actualSetup = container.OnSetup?.GetInvocationList() ?? Array.Empty<Delegate>();
        if (actualSetup.Length != setup.Count) return null;
        foreach (Delegate callback in actualSetup) if (!setup.Contains(callback)) return null;
        if (container.Asset.Effects.Count != 0 || container.EffectPipeline?.Effects.Count > 0 ||
            container.OnTravel != null || !ContactStatusMigration.MatchesCallbacks(skill)) return null;
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        bool towardsDirection = trajectory == SkillTrajectories.TowardsDirection.id;
        bool noRotation = trajectory == SkillTrajectories.TowardsDirectionNoRot.id;
        bool towardsPosition = trajectory == SkillTrajectories.TowardsPosition.id;
        bool towardsTarget = trajectory == SkillTrajectories.TowardsTarget.id;
        bool drift = trajectory == SkillTrajectories.DriftHoming.id;
        bool spiral = trajectory == SkillTrajectories.SpiralHoming.id;
        bool vortex = trajectory == SkillTrajectories.SlowVortex.id;
        bool orbit = trajectory == SkillTrajectories.OrbitTarget.id;
        bool boomerang = trajectory == SkillTrajectories.Boomerang.id;
        bool arc = trajectory == SkillTrajectories.ArcToPosition.id;
        bool falling = trajectory == SkillTrajectories.FallingStrike.id, rain = trajectory == SkillTrajectories.RainFall.id;
        bool descent = falling || rain, ground = trajectory == SkillTrajectories.GroundCrawl.id, snap = trajectory == SkillTrajectories.LightningSnap.id;
        bool wave = trajectory == SkillTrajectories.SineWave.id, zigzag = trajectory == SkillTrajectories.Zigzag.id;
        if (!towardsDirection && !noRotation && !towardsPosition && !towardsTarget && !wave && !zigzag && !drift && !spiral && !vortex && !orbit && !boomerang && !arc && !descent && !ground && !snap) return null;
        if (ground)
        {
            forward.z = 0f;
            forward = forward.sqrMagnitude > .0001f ? forward.normalized : Vector3.right;
        }
        bool configured = skill.TryGetComponent(out SkillSettings settings);
        if (configured && settings.NatureId != null) return null;
        float lifetime = container.Asset.ResolveRuntimeLifetime(skill,
            container.Asset.ImpactProfile.Lifetime * container.Asset.ImpactTuning.LifetimeMultiplier);
        if (configured && settings.Duration > 0) lifetime = settings.Duration;
        if (boomerang) lifetime = Mathf.Min(lifetime, 1.2f);
        float scale = emission.RuntimeData.ResolveEffectScale() * SkillEffectRadius.ResolveCasterGrowth(emission.Source.a.GetExtend(), skill);
        if (skill.HasComponent<HugeModifier>()) scale *= SkillModifiers.Huge.EffectRadiusMultiplier(skill);
        if (configured) scale *= settings.AreaMultiplier;
        float haste = skill.TryGetComponent(out HasteModifier h) ? Mathf.Clamp(1f + h.SpeedMultiplier, .1f, 10f) : 1f;
        var motion = container.MotionProfile;
        float speed = motion.ResolveSpeed(Vector2.Distance(emission.SourcePos, emission.TargetPos)) * haste;
        if (orbit) speed *= 1.35f;
        float turn = motion.TurnRate * haste * (spiral ? .7f : 1f);
        float ramp = Mathf.Max(0f, motion.RampDuration), launch = motion.LaunchMultiplier, cruise = motion.CruiseMultiplier;
        float arcDistance = arc ? Vector3.Distance(emission.SourcePos, emission.TargetPos) : 0f;
        if (arc)
        {
            if (arcDistance < .01f || arcDistance > 10000f) return null;
            // 原术半秒超时会跳到终点；现在预先连续赶上，不在末帧瞬移。
            speed *= arcDistance / Mathf.Max(.1f, arcDistance);
            float travel = PlannedDistance(speed, launch, cruise, ramp, .5f);
            if (travel <= 0f) return null;
            speed *= Mathf.Max(1f, arcDistance / travel);
        }
        // 保留当前未能完整表达的超预算来源，不缩小原术来让转换通过。
        if (scale <= 0f || scale > 64f || lifetime <= 0f || lifetime > 3600f ||
            speed * Mathf.Max(launch, cruise) > 10000f || turn > 7200f) return null;
        Vector3 initial = Quaternion.AngleAxis(emission.InitialAngleOffsetDegrees, Vector3.forward) * forward;
        // 下落剑尖对齐实际落点；抛弧沿出生切线摆剑，避免水平长剑先扫中途人物。
        Vector3 heading = descent ? Vector3.back : arc ? (forward + Vector3.forward * (4f * Mathf.PI / arcDistance)).normalized : initial;
        Quaternion facing = Quaternion.LookRotation(heading,
            Mathf.Abs(Vector3.Dot(heading, Vector3.forward)) > .999f ? Vector3.up : Vector3.forward);
        float descentHeight = descent ? DescentHeight(sourceForm.Form, facing, scale, falling ? 7f : 8f) : 0f;
        var goal = noRotation || wave || zigzag || vortex || rain ? null : new ConstructionFlightGoal
        {
            Anchor = towardsDirection || boomerang ? ConstructionFlightAnchor.Direction : ConstructionFlightAnchor.Target,
            Follow = towardsTarget || drift || spiral || orbit || ground || snap,
            Arrival = towardsDirection || boomerang ? ConstructionFlightArrival.None : ConstructionFlightArrival.End,
            Radius = .01f, Approach = orbit
        };
        if (ground)
        {
            goal.DirectArrival = true;
            goal.Arrival = continues ? ConstructionFlightArrival.Continue : ConstructionFlightArrival.End;
        }
        if (snap) goal.Arrival = ConstructionFlightArrival.Stop;
        if (falling)
        {
            goal.Follow = true; goal.Arrival = ConstructionFlightArrival.None;
            goal.Approach = true; goal.Offset = new[] { 0f, 0f, descentHeight };
        }
        int seed = unchecked((int)emission.Source.id * 397 ^ skill.Id ^ (int)emission.RuntimeData.CorrelationId ^
                             emission.InitialAngleOffsetDegrees.GetHashCode());
        const float orbitEnd = (2.2f - .2f) / 1.8f;
        if (boomerang)
        {
            float phase = (float)(new System.Random(seed).NextDouble() * Math.PI * 4d - Math.PI * 2d);
            if (Mathf.Abs(phase) < .1f) phase = phase < 0f ? -.1f : .1f;
            Vector3 plane = new(initial.x, initial.y, 0f);
            plane = plane.sqrMagnitude < .0001f ? Vector3.right : plane.normalized;
            Vector3 desired = (initial + new Vector3(-plane.y, plane.x, 0f) * (phase * .15f)).normalized;
            goal.Offset = XYZ(desired - forward);
        }
        if (orbit)
        {
            float phase = (float)(new System.Random(seed).NextDouble() * Math.PI * 4d - Math.PI * 2d);
            goal.Arrival = ConstructionFlightArrival.None;
            goal.Offset = XYZ(new Vector3(Mathf.Cos(phase), Mathf.Sin(phase), 0f) * 2.2f);
            goal.OffsetPath = ConstructionPathCurves.ContractingSpiral(orbitEnd, .75f,
                Vector3.up * 2.2f, Vector3.right * 2.2f, 1.8f / 2.2f, phase);
        }
        if (drift)
        {
            float bias = new System.Random(seed).Next(2) == 0 ? -.65f : .65f;
            goal.Steering = new ConstructionFlightSteering
            {
                Side = bias,
                Path = new ConstructionPath { Forward = null, Keys = new[]
                {
                    new ConstructionPathKey { Out = XYZ(Vector3.left * (bias / 3f)) },
                    new ConstructionPathKey { At = .75f, Point = XYZ(Vector3.left * bias), In = XYZ(Vector3.right * (bias / 3f)) }
                } }
            };
        }
        if (ground)
        {
            float phase = (float)(new System.Random(seed).NextDouble() * Math.PI * 4d - Math.PI * 2d);
            goal.Steering = new ConstructionFlightSteering
            {
                Side = Mathf.Sin(phase) * .2f,
                Path = ConstructionPathCurves.Wave(1f / 2.2f, Vector3.right * .2f, phase)
            };
            goal.Steering.Path.Forward = null;
        }
        if (spiral)
        {
            float phase = (float)(new System.Random(seed).NextDouble() * Math.PI * 2d);
            ConstructionPath direction = ConstructionPathCurves.Spiral(1f, .25f, Vector3.right * .7f, Vector3.up * (.7f * .04f), phase: phase);
            direction.DistancePerSecond = 30f; direction.Forward = null;
            goal.Steering = new ConstructionFlightSteering
            {
                Side = Mathf.Sin(phase) * .7f, Lift = Mathf.Cos(phase) * (.7f * .04f), Decay = .75f, Path = direction
            };
        }
        // 相位只在资料准备阶段确定并展开，运行和预测不重新取随机数。
        ConstructionPath path = wave ? ConstructionPathCurves.Wave(1f / 3.2f, Vector3.right * .65f,
            (float)(new System.Random(seed).NextDouble() * Math.PI * 4d - Math.PI * 2d)) :
            zigzag ? ConstructionPathCurves.Zigzag(.24f, Vector3.right * .75f, -Mathf.PI * .5f) : null;
        if (vortex)
            path = ConstructionPathCurves.PulsingSpiral(360f / 520f, .5f, Vector3.forward * (.9f * .25f),
                Vector3.right * .9f, .25f / .9f, (float)(new System.Random(seed).NextDouble() * Math.PI * 2d));
        if (path != null) path.DistancePerSecond = vortex ? 28f : wave ? 30f : 34f;
        if (arc)
        {
            path = ConstructionPathCurves.WaveSpan(1f, 2f, Quaternion.Inverse(facing) * (Vector3.forward * 4f));
            path.DistancePerSecond = arcDistance;
        }
        Vector3 spawnOffset = Vector3.zero;
        if (descent)
        {
            path = DescentPath(lifetime, speed, launch, cruise, ramp, falling ? 28f : 32f, facing);
            if (path == null) return null;
            if (rain)
            {
                var random = new System.Random(seed);
                double angle = random.NextDouble() * Math.PI * 2d;
                float radius = Mathf.Sqrt((float)random.NextDouble()) * 1.4f;
                spawnOffset = new Vector3(Mathf.Cos((float)angle), Mathf.Sin((float)angle), 0f) * radius;
            }
        }
        var operations = new List<ConstructionOperation>
        {
            new()
            {
                Kind = ConstructionOperationKind.Fly, WorldVelocity = true, Vector = XYZ(descent ? Vector3.right * (falling ? 7f : 0f) : (arc ? forward : initial) * speed * (ramp > .001f ? launch : cruise)),
                TurnRate = noRotation || path != null || snap ? 0f : turn, Transfer = snap ? new ConstructionFlightTransfer { Seed = seed } : null, InheritVelocity = false, Goal = goal, Path = path,
                Ground = descent ? ConstructionGroundResponse.End : ground ? ConstructionGroundResponse.Follow : ConstructionGroundResponse.Ignore
            }
        };
        // 地面推进的自身动作与投送分开，改成其他轨迹仍保留主体及其真实接触变形。
        if (sourceForm.Form is "earth_ridge" or "tidal_wave" or "creeping_vine")
            operations.Add(new ConstructionOperation { Kind = ConstructionOperationKind.Animate, Clip = "travel", Seconds = lifetime });
        // 起步曲线使用八个确定时点，调整速度不重新开始飞行，也不重置目标和接触记录。
        if (!descent && ramp > .001f)
            for (int i = 1; i <= 8; i++)
            {
                float at = ramp * i / 8f;
                if (at >= lifetime) break;
                float remaining = 1f - i / 8f;
                operations.Add(new ConstructionOperation { Kind = ConstructionOperationKind.ChangeSpeed, At = at,
                    Speed = speed * (cruise + (launch - cruise) * remaining * remaining) });
            }
        ConstructionOperation finishOrbit = null;
        if (orbit && orbitEnd < lifetime)
        {
            // 在原有0.2格阈值换向目标；仍是同一部位、同一身份和命中记录，不重新支付。
            float remaining = ramp > .001f ? 1f - Mathf.Min(8f, Mathf.Floor(orbitEnd / ramp * 8f)) / 8f : 0f;
            finishOrbit = new ConstructionOperation
            {
                Kind = ConstructionOperationKind.Fly, At = orbitEnd, WorldVelocity = true,
                Vector = XYZ(initial * (speed * (cruise + (launch - cruise) * remaining * remaining))),
                InheritVelocity = false, TurnRate = turn,
                Goal = new ConstructionFlightGoal { Approach = true, Follow = true, Radius = .01f, Arrival = ConstructionFlightArrival.End }
            };
        }
        ConstructionOperation returnSource = boomerang ? new ConstructionOperation
        {
            Kind = ConstructionOperationKind.Fly, WorldVelocity = true, Vector = new float[3], InheritVelocity = true, TurnRate = 520f,
            When = new ConstructionCondition { Source = "root", Trigger = ConstructionChangeTrigger.DistanceFromFlightStart, Threshold = 5f },
            Goal = new ConstructionFlightGoal { Anchor = ConstructionFlightAnchor.Source, Follow = true,
                Arrival = ConstructionFlightArrival.End, Radius = .01f, DirectArrival = true }
        } : null;
        float strength = emission.Strength * emission.RuntimeData.ResolveEffectScale();
        if (configured) strength *= settings.PowerMultiplier;
        if (skill.TryGetComponent(out EmpowerModifier empower)) strength *= 1f + Mathf.Clamp(empower.SetupBonus, 0f, 10f);
        if (skill.TryGetComponent(out VolleyModifier volley)) strength *= Mathf.Clamp(volley.DamageMultiplier, .05f, 2f);
        if (skill.TryGetComponent(out ComboModifier combo)) strength *= Mathf.Clamp(combo.DamageMultiplier, .05f, 2f);
        float damageMultiplier = container.Asset.ImpactProfile.DamageMultiplier * container.Asset.ImpactTuning.DamageMultiplier;
        // 防御量保留实际本体强度，持续投射的伤害倍率单独交给人物作用；命中不会凭空消耗本体。
        if (!continues && !blast) strength *= damageMultiplier;
        damage = new GetHitEvent { Damage = strength, Element = container.Asset.Element, AttackType = AttackType.Other,
            AttackerPowerLevel = emission.PowerLevel, DamageOrigin = emission.RuntimeData.DamageOrigin };
        damage.BindAttacker(emission.Source);
        var definition = new AbilityDefinition
        {
            Id = container.Asset.id, Name = skill.HasName ? skill.Name.value : container.Asset.id.Localize(), Seed = seed,
            Lifetime = lifetime, Resource = "none", Cost = 0f, Cooldown = 0, Power = 1, TargetRange = 10000,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
                Target = towardsTarget || drift || spiral || orbit || falling || ground || snap ? ActiveAbilityTargetMode.ObjectOrPoint : ActiveAbilityTargetMode.Point },
            Binding = new ConstructionBinding { Anchor = descent ? AbilityAnchor.Target : AbilityAnchor.Source, Height = descentHeight },
            Steps = finishOrbit == null && returnSource == null ? Array.Empty<ConstructionStep>() : new[]
            {
                new ConstructionStep { Id = boomerang ? "return_source" : "approach_target", Inputs = new Dictionary<string, ConstructionOutputReference>
                    { ["part"] = new() { Node = "root" } }, Operation = returnSource ?? finishOrbit }
            },
            Root = new ConstructionNode
            {
                Id = container.Asset == SkillEntities.FlyingSword ? "sword" : "projectile", Form = sourceForm.Form, Matter = sourceForm.Matter, State = sourceForm.State,
                Scale = scale, Rotation = XYZ(facing.eulerAngles), Offset = XYZ(spawnOffset),
                OnContact = continues ? ConstructionContactResponse.Continue : ConstructionContactResponse.End,
                Contact = once ? new ConstructionContactPolicy { Mode = ConstructionContactMode.Once, MaximumPerTarget = 1 } : null,
                Impact = blast ? new ConstructionImpact { DamageScale = 0f } :
                    continues && (!configured || settings.ContactEnabled) ? new ConstructionImpact { DamageScale = damageMultiplier } : null,
                ContactRole = configured && !settings.ContactEnabled ? ConstructionContactRole.Decoration : ConstructionContactRole.Attack,
                Operations = operations.ToArray()
            }
        };
        if (blast)
        {
            bool contact = !configured || settings.ContactEnabled;
            definition.Root.Children = new[] { new ConstructionNode
            {
                Id = "burst", Form = sourceForm.Matter + "_burst", Matter = sourceForm.Matter, State = sourceForm.State,
                Scale = scale * container.Asset.ImpactProfile.EffectRadius * container.Asset.ImpactTuning.EffectRadiusMultiplier / .9f,
                DisappearAt = Mathf.Min(.22f, lifetime), ActiveUntil = Mathf.Min(.025f, lifetime),
                Activation = new ConstructionActivation { Trigger = ConstructionChangeTrigger.End,
                    Placement = ConstructionEventPlacement.Event, InheritRemainder = contact, ResetContacts = contact },
                Contact = new ConstructionContactPolicy { Mode = ConstructionContactMode.Once, MaximumPerTarget = 1 },
                Impact = new ConstructionImpact { DamageScale = damageMultiplier },
                ContactRole = contact ? ConstructionContactRole.Attack : ConstructionContactRole.Decoration,
                Operations = new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Stop,
                    When = new ConstructionCondition { Source = "..", Trigger = ConstructionChangeTrigger.End } } }
            } };
        }
        return ContactStatusMigration.Bind(definition, skill, emission.RuntimeData.ResolveEffectScale(), includeRoot: !blast, tickForceScale: !continues && !blast ? 1f / damageMultiplier : 1f);
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
