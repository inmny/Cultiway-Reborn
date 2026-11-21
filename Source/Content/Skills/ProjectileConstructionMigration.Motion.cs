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

/// <summary>各基础来源共用旧路径的普通资料展开，形体、作用及收费仍由来源维护。</summary>
internal static partial class ProjectileConstructionMigration
{
    /// <summary>先展开目标和确定相位，再建立普通飞行及后续步骤，不读取旧执行状态。</summary>
    /// <param name="emission">本次真实发射输入。</param><param name="form">实际主体。</param><param name="scale">真实尺寸。</param>
    /// <param name="continues">原接触后是否继续。</param><param name="lifetime">原寿命。</param>
    /// <param name="initialAngle">本次固定附加偏角。</param><param name="speedScale">本次固定速度比例。</param>
    /// <param name="forward">实际出手方向。</param>
    /// <param name="name">来源已经提供的显示名称。</param>
    internal static AbilityDefinition Motion(SkillEmission emission, string form, float scale, bool continues,
        float lifetime, float initialAngle, float speedScale, out Vector3 forward, string name = null)
    {
        forward = emission.TargetPos - emission.SourcePos;
        forward = forward.sqrMagnitude > .0001f ? forward.normalized : Vector3.right;
        Entity skill = emission.SkillContainer;
        var container = skill.GetComponent<SkillContainer>();
        string trajectory = SkillBlueprintTrajectory.ResolveEffectiveId(skill);
        bool manifest = trajectory == SkillTrajectories.GroundEruptAtTarget.id && form is "creeping_vine" or "creeping_vine_0";
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
        if (!towardsDirection && !noRotation && !towardsPosition && !towardsTarget && !wave && !zigzag && !drift && !spiral && !vortex && !orbit && !boomerang && !arc && !descent && !ground && !snap && !manifest) return null;
        if (ground || manifest)
        {
            forward.z = 0f;
            forward = forward.sqrMagnitude > .0001f ? forward.normalized : Vector3.right;
        }
        if (boomerang) lifetime = Mathf.Min(lifetime, 1.2f);
        float haste = skill.TryGetComponent(out HasteModifier h) ? Mathf.Clamp(1f + h.SpeedMultiplier, .1f, 10f) : 1f;
        var motion = container.MotionProfile;
        float speed = motion.ResolveSpeed(Vector2.Distance(emission.SourcePos, emission.TargetPos)) * haste * speedScale;
        if (orbit) speed *= 1.35f;
        float turn = motion.TurnRate * haste * (spiral ? .7f : 1f);
        float ramp = Mathf.Max(0f, motion.RampDuration), launch = motion.LaunchMultiplier, cruise = motion.CruiseMultiplier;
        float arcDistance = arc ? Vector3.Distance(emission.SourcePos, emission.TargetPos) : 0f;
        float arcTime = 0f;
        if (arc)
        {
            if (arcDistance < .01f || arcDistance > 10000f) return null;
            speed *= arcDistance / Mathf.Max(.1f, arcDistance);
            if (speed <= 0f || Mathf.Max(launch, cruise) <= 0f) return null;
            arcTime = ArcTravelTime(arcDistance, speed, launch, cruise, ramp);
        }
        // 保留当前未能完整表达的超预算来源，不缩小原术来让转换通过。
        if (lifetime <= 0f || lifetime > 3600f ||
            speed * Mathf.Max(launch, cruise) > 10000f || turn > 7200f) return null;
        Vector3 initial = Quaternion.AngleAxis(emission.InitialAngleOffsetDegrees + initialAngle, Vector3.forward) * forward;
        // 下落剑尖对齐实际落点；抛弧沿出生切线摆剑，避免水平长剑先扫中途人物。
        Vector3 heading = descent ? Vector3.back : arc ? (forward + Vector3.forward * (4f * Mathf.PI / arcDistance)).normalized : initial;
        Quaternion facing = Quaternion.LookRotation(heading,
            Mathf.Abs(Vector3.Dot(heading, Vector3.forward)) > .999f ? Vector3.up : Vector3.forward);
        float descentHeight = descent ? DescentHeight(form, facing, scale, falling ? 7f : 8f) : 0f;
        var goal = manifest || noRotation || wave || zigzag || vortex || rain ? null : new ConstructionFlightGoal
        {
            Anchor = towardsDirection || boomerang ? ConstructionFlightAnchor.Direction : ConstructionFlightAnchor.Target,
            Follow = towardsTarget || drift || spiral || orbit || ground || snap,
            Arrival = towardsDirection || boomerang ? ConstructionFlightArrival.None : ConstructionFlightArrival.End,
            Radius = .01f, Approach = orbit
        };
        if (ground)
        {
            // 旧地面波的偏转只用于横摆；小步模拟也必须持续接近目标，再从真实到达点越过去。
            goal.Approach = true;
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
        // 实际出生点与指定落点共用一条有限曲线，中轴不再另行积分而漏掉到达。
        if (arc) path = ConstructionPathCurves.Arc(arcTime, 4f);
        Vector3 spawnOffset = Vector3.zero;
        if (descent)
        {
            path = DescentPath(lifetime, speed, launch, cruise, ramp, falling ? 28f : 32f, facing);
            if (path == null) return null;
            if (rain)
            {
                int scatterSeed = container.Asset == SkillEntities.Icefall ? seed ^ emission.StepSeed : seed;
                var random = new System.Random(scatterSeed);
                double angle = random.NextDouble() * Math.PI * 2d;
                float radius = Mathf.Sqrt((float)random.NextDouble()) * 1.4f;
                spawnOffset = new Vector3(Mathf.Cos((float)angle), Mathf.Sin((float)angle), 0f) * radius;
            }
        }
        var operations = new List<ConstructionOperation>();
        if (!manifest) operations.Add(new ConstructionOperation
            {
                Kind = ConstructionOperationKind.Fly, WorldVelocity = true, Vector = XYZ(arc ? Vector3.zero :
                    descent ? Vector3.right * (falling ? 7f : 0f) : initial * speed * (ramp > .001f ? launch : cruise)),
                TurnRate = noRotation || path != null || snap ? 0f : turn, Transfer = snap ? new ConstructionFlightTransfer { Seed = seed } : null, InheritVelocity = false, Goal = goal, Path = path,
                Ground = descent ? ConstructionGroundResponse.End : ground ? ConstructionGroundResponse.Follow : ConstructionGroundResponse.Ignore
            });
        // 地面推进的自身动作与投送分开，改成其他轨迹仍保留主体及其真实接触变形。
        if (form is "earth_ridge" or "tidal_wave" or "creeping_vine" or "earth_ridge_0" or "tidal_wave_0" or "creeping_vine_0")
            operations.Add(new ConstructionOperation { Kind = ConstructionOperationKind.Animate, Clip = "travel", Seconds = lifetime });
        // 起步曲线使用八个确定时点，调整速度不重新开始飞行，也不重置目标和接触记录。
        if (!manifest && !descent && !arc && ramp > .001f)
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
        return new AbilityDefinition
        {
            Id = container.Asset.id, Name = name ?? (skill.HasName ? skill.Name.value : container.Asset.id.Localize()), Seed = seed,
            Lifetime = lifetime, Resource = "none", Cost = 0f, Cooldown = 0, Power = 1, TargetRange = 10000,
            Use = new ConstructionUse { Channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
                Target = towardsTarget || drift || spiral || orbit || falling || ground || snap ? ActiveAbilityTargetMode.ObjectOrPoint : ActiveAbilityTargetMode.Point },
            Binding = new ConstructionBinding { Anchor = descent || manifest ? AbilityAnchor.Target : AbilityAnchor.Source, Height = manifest ? -emission.TargetPos.z : descentHeight },
            Steps = finishOrbit == null && returnSource == null ? Array.Empty<ConstructionStep>() : new[]
            {
                new ConstructionStep { Id = boomerang ? "return_source" : "approach_target", Inputs = new Dictionary<string, ConstructionOutputReference>
                    { ["part"] = new() { Node = "root" } }, Operation = returnSource ?? finishOrbit }
            },
            Root = new ConstructionNode
            {
                Id = container.Asset == SkillEntities.FlyingSword ? "sword" : "projectile", Form = form,
                Scale = scale, Rotation = XYZ(facing.eulerAngles), Offset = XYZ(spawnOffset),
                // 真实到达会提前结束。若途中偏折，有限弧走完后也能收束。
                DisappearAt = arc ? Mathf.Min(lifetime, arcTime + 1f / 60f) : null,
                Operations = operations.ToArray()
            }
        };
    }

    /// <summary>辅助和领域保留原主体姿态，将路径偏移换到同一实际坐标；不改变自身作用及间隔。</summary>
    /// <param name="definition">已有完整来源作用。</param><param name="motion">已经展开的普通运动。</param>
    internal static void BindMotion(AbilityDefinition definition, AbilityDefinition motion)
    {
        Quaternion frame = Quaternion.Inverse(Quaternion.Euler(definition.Root.Rotation[0], definition.Root.Rotation[1], definition.Root.Rotation[2])) *
            Quaternion.Euler(motion.Root.Rotation[0], motion.Root.Rotation[1], motion.Root.Rotation[2]);
        foreach (ConstructionOperation operation in motion.Root.Operations)
            if (operation.Path != null) RebasePath(operation.Path, frame);
        definition.Binding = motion.Binding;
        definition.Lifetime = motion.Lifetime;
        definition.Seed = motion.Seed;
        definition.Steps = motion.Steps;
        definition.Root.Operations = definition.Root.Operations.Concat(motion.Root.Operations).ToArray();
    }

    /// <summary>曲线及附加项共同换轴，主体保持作者原姿态，实际位置仍沿同一条完整路径。</summary>
    /// <param name="path">本次独立位置曲线。</param><param name="frame">原运动轴到实际主体轴的变换。</param>
    private static void RebasePath(ConstructionPath path, Quaternion frame)
    {
        RebaseKeys(path.Keys, frame);
        path.Forward = null;
        if (path.Additions != null)
            foreach (ConstructionPathTerm term in path.Additions) RebaseKeys(term.Keys, frame);
    }

    /// <summary>控制点与两侧柄采用相同刚性换轴，不改变曲线时点和周期。</summary>
    /// <param name="keys">本次独立控制点。</param><param name="frame">实际轴变换。</param>
    private static void RebaseKeys(ConstructionPathKey[] keys, Quaternion frame)
    {
        foreach (ConstructionPathKey key in keys)
        {
            key.Point = XYZ(frame * new Vector3(key.Point[0], key.Point[1], key.Point[2]));
            key.In = XYZ(frame * new Vector3(key.In[0], key.In[1], key.In[2]));
            key.Out = XYZ(frame * new Vector3(key.Out[0], key.Out[1], key.Out[2]));
        }
    }
}
