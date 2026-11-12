using System;
using System.IO;
using System.Linq;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>可共用的单次发力动作，不表示多招连招或结算规则。</summary>
public enum BodyActionKind
{
    /// <summary>向前送出接触部位。</summary>
    Thrust,

    /// <summary>沿横向弧线扫过。</summary>
    Sweep,

    /// <summary>由高向低劈落。</summary>
    Cleave,

    /// <summary>由低向高挑起。</summary>
    Lift,

    /// <summary>短促拍击后回收。</summary>
    Slap,

    /// <summary>抬起接触面并保持架势。</summary>
    Guard,

    /// <summary>以较短侧向行程拨开。</summary>
    Deflect,

    /// <summary>送肩伸掌，沿地推出。</summary>
    Push,

    /// <summary>先伸后收，瞬时牵回。</summary>
    Pull,

    /// <summary>由高向低发力并实际下压。</summary>
    Press,

    /// <summary>转腕转体，以扭劲损伤攻击能力。</summary>
    Twist,

    /// <summary>明确刺入并使实际护甲受损。</summary>
    Pierce,

    /// <summary>短程发力与回弹，造成真实受击停顿。</summary>
    Shock
}

/// <summary>创建动作时的有限输入，结果保存实际关键点，运行不再解释本请求。</summary>
public sealed class BodyActionRequest
{
    /// <summary>单次动作的发力方向。</summary>
    public BodyActionKind Kind;

    /// <summary>实际参与的身体部位。</summary>
    public string Effector = "hand.right";

    /// <summary>包括起势和收势的制作秒数。</summary>
    public float Seconds = .6f;

    /// <summary>整段动作的速度倍率，不单独加速接触。</summary>
    public float Speed = 1f;

    /// <summary>行程幅度，实际骨长和支撑继续约束结果。</summary>
    public float Amplitude = 1f;

    /// <summary>横向动作的扫角，单位为度。</summary>
    public float Arc = 100f;

    /// <summary>动作面绕前方轴的倾斜角，单位为度。</summary>
    public float Plane;

    /// <summary>手部可借用的实际器物，空值直接使用身体。</summary>
    public string WeaponForm;

    /// <summary>借用器物时的真实握点。</summary>
    public string Grip = "grip";

    /// <summary>实际器物的接触编号或分组，空值使用全部实际形状。</summary>
    public string Contact;

    /// <summary>发力强度，与动作幅度分别调整；结果展开为可修改的实际作用参数。</summary>
    public float Strength = 1f;

    /// <summary>真实结果对应的恢复姿态和基准秒数；可选recover.pose或rest，整体速度同时缩放转入时长。</summary>
    public BodyResultResponse[] Responses = Array.Empty<BodyResultResponse>();
}

/// <summary>将共用发力方向生成实际部位的连续姿态资料，复用共同编译、骨长和接触。</summary>
public static class BodyActionGenerator
{
    /// <summary>先核对部位和输入，再生成准备、发力、经过及收势，最后填入该部位实际控制的通道。</summary>
    /// <param name="request">创建边界提供的完整动作输入。</param>
    public static BodyActionDefinition Create(BodyActionRequest request)
    {
        if (request == null || request.Kind is < BodyActionKind.Thrust or > BodyActionKind.Shock ||
            !Positive(request.Seconds) || !Positive(request.Speed) || !Positive(request.Amplitude) ||
            !Positive(request.Strength) || request.Strength > 100f ||
            !Positive(request.Arc) || request.Arc > 160f || !Finite(request.Plane) ||
            Mathf.Abs(request.Plane) > 90f)
            throw new InvalidDataException("共用身体动作需要有效部位、正有限历时、速度与幅度，扫角不超过160度，动作面不超过正负90度");
        var part = new BodyPartSelection(request.Effector);
        if (part.IsOrgan)
            throw new InvalidDataException("该器官仍使用实际根部转角资料；完整非人动作轴按对应身体接入，不能套用人形动作");
        if (request.WeaponForm != null && part.Contact != BodyContactPart.Hand)
            throw new InvalidDataException("共用动作借用器物需要真实手部，不能用其他部位冒充握点");
        float duration = request.Seconds / request.Speed;
        if (!Positive(duration)) throw new InvalidDataException("共用身体动作历时超过可表示范围");
        float side = part.Left ? -1f : 1f;
        Vector3 rest = new(.2f, .18f, .55f);
        Vector3 prepare, strike, through;
        // 各种发力保持不同的手路；幅度、左右侧和动作面随后统一变换。
        (prepare, strike, through) = request.Kind switch
        {
            BodyActionKind.Thrust => (new Vector3(.1f, .18f, .55f), new Vector3(.48f, .16f, .62f),
                new Vector3(.43f, .16f, .62f)),
            BodyActionKind.Sweep => (new Vector3(.23f, .4f, .62f), new Vector3(.48f, .02f, .62f),
                new Vector3(.23f, -.3f, .59f)),
            BodyActionKind.Cleave => (new Vector3(.16f, .14f, .96f), new Vector3(.44f, .14f, .61f),
                new Vector3(.3f, .14f, .36f)),
            BodyActionKind.Lift => (new Vector3(.19f, .16f, .3f), new Vector3(.44f, .14f, .59f),
                new Vector3(.3f, .14f, .88f)),
            BodyActionKind.Slap => (new Vector3(.18f, .37f, .66f), new Vector3(.46f, .13f, .62f),
                new Vector3(.33f, -.08f, .6f)),
            BodyActionKind.Guard => (new Vector3(.21f, .18f, .7f), new Vector3(.28f, .15f, .73f),
                new Vector3(.28f, .15f, .73f)),
            BodyActionKind.Push => (new Vector3(.10f, .18f, .56f), new Vector3(.5f, .15f, .62f),
                new Vector3(.55f, .15f, .62f)),
            BodyActionKind.Pull => (new Vector3(.48f, .18f, .62f), new Vector3(.25f, .12f, .58f),
                new Vector3(.10f, .16f, .55f)),
            BodyActionKind.Press => (new Vector3(.36f, .18f, .92f), new Vector3(.42f, .14f, .45f),
                new Vector3(.32f, .16f, .36f)),
            BodyActionKind.Twist => (new Vector3(.34f, .24f, .59f), new Vector3(.40f, .10f, .62f),
                new Vector3(.24f, -.04f, .57f)),
            BodyActionKind.Pierce => (new Vector3(.08f, .18f, .59f), new Vector3(.56f, .14f, .62f),
                new Vector3(.59f, .14f, .62f)),
            BodyActionKind.Shock => (new Vector3(.20f, .18f, .59f), new Vector3(.38f, 0f, .62f),
                new Vector3(.33f, .02f, .60f)),
            _ => (new Vector3(.24f, .3f, .68f), new Vector3(.37f, .14f, .66f), new Vector3(.28f, -.06f, .65f))
        };
        bool recovery = request.Responses?.Length > 0;
        Vector3[] path = recovery ? new[] { rest, prepare, strike, through, Vector3.Lerp(through, rest, .7f), rest } :
            new[] { rest, prepare, strike, through, rest };
        float[] times = recovery ? new[] { 0f, .27f, .46f, .68f, .84f, 1f } : new[] { 0f, .27f, .46f, .68f, 1f };
        float[] pulse = recovery ? new[] { 0f, -.35f, 1f, .55f, .15f, 0f } : new[] { 0f, -.35f, 1f, .55f, 0f };
        var body = new BodyActionDefinition
        {
            Effector = part.Id, WeaponForm = request.WeaponForm, Grip = request.Grip,
            ActiveFrom = duration * .38f, ActiveUntil = duration * .7f, ReleaseAt = duration * .3f,
            Occupancy = part.Occupancy | BodyOccupancy.Stance,
            MatchDistance = true, Keys = new BodyMotionKey[path.Length],
            Impact = Exertion(request),
            ImpactRecoil = request.Kind == BodyActionKind.Shock ? .09f : 0f,
            RecoilSeconds = .07f
        };
        Quaternion heldRotation = Quaternion.identity;
        if (request.WeaponForm != null)
        {
            var resource = FormResources.Load(request.WeaponForm);
            var grip = resource.Grip(request.Grip);
            var selected = resource.Description.Anchors.FirstOrDefault(anchor => anchor.Id == request.Contact);
            float[] normal = selected?.Direction ?? resource.Description.Forward, up = resource.Description.Up;
            Matrix4x4 inverse = grip.Frame().inverse;
            heldRotation = Quaternion.Inverse(Quaternion.LookRotation(
                inverse.MultiplyVector(new Vector3(normal[0], normal[1], normal[2])),
                inverse.MultiplyVector(new Vector3(up[0], up[1], up[2]))));
            // 架挡仍让所选接触面朝前，长轴朝身体内侧横接，不能把剑刃或杆身摆到持物手外侧。
            float[] axis = resource.Description.Forward;
            Vector3 heldAxis = heldRotation * inverse.MultiplyVector(new Vector3(axis[0], axis[1], axis[2]));
            if (request.Kind == BodyActionKind.Guard && heldAxis.x * side < -.0001f)
                heldRotation = Quaternion.AngleAxis(180f, Vector3.forward) * heldRotation;
            body.Construction = new BodyConstructionDefinition
            {
                Parts = new[]
                {
                    new BodyPartInput { Id = part.Id, Subject = BodyPartSubject.Source, Part = part.Id },
                    new BodyPartInput { Id = "held", Subject = BodyPartSubject.Held, Part = "item" },
                    new BodyPartInput { Id = "target", Subject = BodyPartSubject.Target, Part = "surface" }
                },
                Relations = new[]
                {
                    new BodyRelationDefinition
                    {
                        Id = "grip", From = part.Id, To = "held", Kind = BodyRelationKind.Grip,
                        FromAnchor = "grip", ToAnchor = request.Grip, FromMarker = "start", UntilMarker = "end"
                    },
                    new BodyRelationDefinition
                    {
                        Id = "touch", From = "held", To = "target", Kind = BodyRelationKind.Touch,
                        FromAnchor = request.Contact
                    }
                }
            };
        }
        else if (request.Contact != null)
            throw new InvalidDataException("共用动作选择器物接触部位时必须提供实际器物资源");

        for (int i = 0; i < path.Length; i++)
        {
            Vector3 point = rest + (path[i] - rest) * request.Amplitude;
            float y = (point.y - .16f) * (request.Arc / 100f), z = point.z - .62f;
            float plane = request.Plane * Mathf.Deg2Rad;
            point.y = (.16f + y * Mathf.Cos(plane) - z * Mathf.Sin(plane)) * side;
            point.z = .62f + y * Mathf.Sin(plane) + z * Mathf.Cos(plane);
            Vector3 torso = new(.035f * pulse[i] * request.Amplitude, 0f, -.012f * Mathf.Max(0f, pulse[i]));
            float turn = request.Kind is BodyActionKind.Sweep or BodyActionKind.Slap or BodyActionKind.Deflect
                ? (point.y / side - .16f) * -55f * side
                : 0f;
            if (request.Kind == BodyActionKind.Twist) turn += 35f * pulse[i] * side;
            if (request.Kind is BodyActionKind.Push or BodyActionKind.Pierce) torso.x += .035f * pulse[i];
            var key = new BodyMotionKey
            {
                Id = i == 2 ? "strike" : i == path.Length - 1 ? "rest" : recovery && i == path.Length - 2 ? "recover.pose" : "pose." + i,
                At = times[i] * duration, Torso = Values(torso), Turn = turn
            };
            if (part.IsLeg)
            {
                // 足底使用自己的地面路径，膝部通过同一条真实腿链到达，不复制一条手路。
                Vector3 foot = new(point.x - rest.x, point.y * .55f,
                    Mathf.Max(0f, (point.z - .32f) * .7f));
                if (i == 0 || i == path.Length - 1) foot = new Vector3(0f, side * CompiledBodyAction.FootSide, 0f);
                if (part.Contact == BodyContactPart.Knee && i is > 0 and < 4)
                    foot = new Vector3(.03f + foot.x * .35f, side * .1f, .18f + foot.z * .65f);
                key.Foot = Values(foot);
            }
            else if (part.IsHead)
            {
                Vector3 angles = request.Kind switch
                {
                    BodyActionKind.Sweep or BodyActionKind.Deflect => new(0f, 0f, (point.y / side - .16f) * 85f),
                    BodyActionKind.Cleave or BodyActionKind.Lift => new(0f, (point.z - .62f) * 100f, 0f),
                    _ => new(0f, pulse[i] * (request.Kind == BodyActionKind.Guard ? -10f : 18f), 0f)
                };
                key.HeadRotation = Values(angles);
                key.Torso[0] *= 1.8f;
            }
            else
            {
                if (part.Contact == BodyContactPart.Elbow)
                    point = new Vector3(point.x * .4f, side * .12f + point.y * .3f, .57f + (point.z - .62f) * .5f);
                key.Hand = Values(point);
                Vector3 rotation = new Vector3(request.Plane,
                    request.Kind == BodyActionKind.Cleave ? 0f : (point.y / side - .16f) * 100f,
                    request.Kind is BodyActionKind.Slap or BodyActionKind.Guard ? 90f * side : 0f);
                if (request.WeaponForm != null)
                {
                    // 实际接触面的法向随本次发力方向转动；收势回到原握持，不能让锤面始终朝前。
                    Vector3 velocity = path[Math.Min(path.Length - 1, i + 1)] - path[Math.Max(0, i - 1)];
                    Vector3 direction = new(-side * velocity.y, velocity.z, velocity.x);
                    Quaternion moving = request.Kind == BodyActionKind.Guard || direction.sqrMagnitude < .000001f
                        ? Quaternion.identity
                        : Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.forward, direction),
                            Mathf.Clamp01(pulse[i]));
                    rotation = (Quaternion.AngleAxis(request.Plane, Vector3.forward) * moving * heldRotation)
                        .eulerAngles;
                }

                if (request.Kind == BodyActionKind.Twist) rotation.z += 120f * pulse[i] * side;
                key.Rotation = Values(rotation);
                if (part.Contact == BodyContactPart.Shoulder) key.Torso[0] *= 2.2f;
            }

            body.Keys[i] = key;
        }

        if (recovery)
        {
            body.Responses = request.Responses.Select(rule => rule == null ? null : new BodyResultResponse
                { Result = rule.Result, RecoverFrom = rule.RecoverFrom, Seconds = rule.Seconds / request.Speed }).ToArray();
            _ = new CompiledBodyAction(body, duration);
        }
        return body;
    }

    /// <summary>创建时展开真实运动与损耗，运行只读取通用作用资料。</summary>
    /// <param name="request">已经核对强度的动作输入。</param>
    private static ConstructionImpact Exertion(BodyActionRequest request)
    {
        float strength = request.Strength;
        ConstructionImpact result = request.Kind switch
        {
            BodyActionKind.Guard or BodyActionKind.Deflect => new() { DamageScale = 0f },
            BodyActionKind.Push => new()
                { DamageScale = .2f, Impulse = new[] { 6f, 0f, 0f }, SlideSeconds = .18f, StaggerSeconds = .1f },
            BodyActionKind.Pull => new()
                { DamageScale = .15f, Impulse = new[] { -5f, 0f, 0f }, SlideSeconds = .22f, StaggerSeconds = .1f },
            BodyActionKind.Lift => new() { Impulse = new[] { 1.2f, 0f, 5f }, StaggerSeconds = .12f },
            BodyActionKind.Press => new()
                { DamageScale = .6f, Impulse = new[] { .5f, 0f, -7f }, StaggerSeconds = .12f },
            BodyActionKind.Twist => new()
            {
                DamageScale = .4f, Impulse = new[] { -.5f, 3f, 0f }, SlideSeconds = .15f, StrainSeconds = 2f,
                StaggerSeconds = .12f
            },
            BodyActionKind.Pierce => new()
                { Impulse = new[] { 2f, 0f, 0f }, SlideSeconds = .12f, ExposureSeconds = 3f },
            BodyActionKind.Shock => new() { DamageScale = .6f, Impulse = new float[3], StaggerSeconds = .45f },
            _ => null
        };
        if (result == null) return null;
        result.DamageScale *= strength;
        if (result.Impulse != null)
            for (int i = 0; i < 3; i++)
                result.Impulse[i] *= strength;
        result.StrainSeconds *= strength;
        result.ExposureSeconds *= strength;
        result.StaggerSeconds *= strength;
        return result;
    }

    /// <summary>生成独立的可编辑坐标数组，不让不同构造共享可变姿态。</summary>
    /// <param name="value">归一化姿态。</param>
    private static float[] Values(Vector3 value) => new[] { value.x, value.y, value.z };

    /// <summary>创建边界需要正有限输入。</summary>
    /// <param name="value">输入数值。</param>
    private static bool Positive(float value) => Finite(value) && value > 0f;

    /// <summary>拒绝会污染动作的非有限输入。</summary>
    /// <param name="value">输入数值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
