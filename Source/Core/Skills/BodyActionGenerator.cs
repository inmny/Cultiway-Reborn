using System;
using System.IO;
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
    Deflect
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
}

/// <summary>将共用发力方向生成实际部位的连续姿态资料，复用共同编译、骨长和接触。</summary>
public static class BodyActionGenerator
{
    /// <summary>先核对部位和输入，再生成准备、发力、经过及收势，最后填入该部位实际控制的通道。</summary>
    /// <param name="request">创建边界提供的完整动作输入。</param>
    public static BodyActionDefinition Create(BodyActionRequest request)
    {
        if (request == null || request.Kind is < BodyActionKind.Thrust or > BodyActionKind.Deflect ||
            !Positive(request.Seconds) || !Positive(request.Speed) || !Positive(request.Amplitude) ||
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
        // 七种发力保持不同的路径拓扑；幅度、左右侧和动作面随后统一变换。
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
            _ => (new Vector3(.24f, .3f, .68f), new Vector3(.37f, .14f, .66f), new Vector3(.28f, -.06f, .65f))
        };
        Vector3[] path = { rest, prepare, strike, through, rest };
        float[] times = { 0f, .27f, .46f, .68f, 1f };
        float[] pulse = { 0f, -.35f, 1f, .55f, 0f };
        var body = new BodyActionDefinition
        {
            Effector = part.Id, WeaponForm = request.WeaponForm, Grip = request.Grip,
            ActiveFrom = duration * .38f, ActiveUntil = duration * .7f, ReleaseAt = duration * .3f,
            Occupancy = part.Occupancy | BodyOccupancy.Stance,
            MatchDistance = true, Keys = new BodyMotionKey[path.Length],
            Impact = request.Kind is BodyActionKind.Guard or BodyActionKind.Deflect
                ? new ConstructionImpact { DamageScale = 0f }
                : null
        };
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
            var key = new BodyMotionKey
            {
                Id = i == 2 ? "strike" : i == 4 ? "rest" : "pose." + i,
                At = times[i] * duration, Torso = Values(torso), Turn = turn
            };
            if (part.IsLeg)
            {
                // 足底使用自己的地面路径，膝部通过同一条真实腿链到达，不复制一条手路。
                Vector3 foot = new(point.x - rest.x, point.y * .55f,
                    Mathf.Max(0f, (point.z - .32f) * .7f));
                if (i is 0 or 4) foot = new Vector3(0f, side * CompiledBodyAction.FootSide, 0f);
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
                key.Rotation = Values(new Vector3(request.Plane,
                    request.Kind == BodyActionKind.Cleave ? 0f : (point.y / side - .16f) * 100f,
                    request.Kind is BodyActionKind.Slap or BodyActionKind.Guard ? 90f * side : 0f));
                if (part.Contact == BodyContactPart.Shoulder) key.Torso[0] *= 2.2f;
            }

            body.Keys[i] = key;
        }

        return body;
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