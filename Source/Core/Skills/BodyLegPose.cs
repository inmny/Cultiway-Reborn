using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>从实际身体采集的一条腿，所有坐标均按共同体高归一化。</summary>
public sealed class BodyLegFit
{
    /// <summary>静止的髋、膝及足底位置。</summary>
    public readonly Vector3 Hip, Knee, Sole;

    /// <summary>实际两段骨长。</summary>
    public readonly float Upper, Lower;

    /// <summary>膝部截面及足部包络的实际半径。</summary>
    public readonly float KneeRadius, FootRadius;

    /// <summary>足部作用轴，相对足底保留，随后与小腿一同转动。</summary>
    public readonly Vector3 FootA, FootB;

    /// <summary>保留已经采集的数值，不引用模型或人物。</summary>
    /// <param name="hip">髋点。</param><param name="knee">膝点。</param><param name="sole">足底。</param>
    /// <param name="kneeRadius">膝部截面半径。</param><param name="footA">足部轴起点相对足底的偏移。</param>
    /// <param name="footB">足部轴末点相对足底的偏移。</param><param name="footRadius">足部截面半径。</param>
    public BodyLegFit(Vector3 hip, Vector3 knee, Vector3 sole, float kneeRadius, Vector3 footA, Vector3 footB,
        float footRadius)
    {
        Hip = hip;
        Knee = knee;
        Sole = sole;
        Upper = Vector3.Distance(hip, knee);
        Lower = Vector3.Distance(knee, sole);
        KneeRadius = kneeRadius;
        FootA = footA;
        FootB = footB;
        FootRadius = footRadius;
    }

    /// <summary>个体体型只在绑定入口换算一次。</summary>
    /// <param name="scale">实际模型相对共同体高的比例。</param>
    public BodyLegFit Scaled(float scale) => new(Hip * scale, Knee * scale, Sole * scale,
        KneeRadius * scale, FootA * scale, FootB * scale, FootRadius * scale);

    /// <summary>原版站立身体的共同参考腿，沿用已有支撑骨长。</summary>
    /// <param name="left">是否为左腿。</param>
    internal static BodyLegFit Native(bool left) => left ? nativeLeft : nativeRight;

    /// <summary>共同参考身体的两条腿，在类型初始化时建立。</summary>
    private static readonly BodyLegFit nativeLeft = CreateNative(true), nativeRight = CreateNative(false);

    /// <summary>建立一条原版参考腿。</summary>
    /// <param name="left">身体侧。</param>
    private static BodyLegFit CreateNative(bool left)
    {
        float side = left ? -CompiledBodyAction.FootSide : CompiledBodyAction.FootSide;
        return new BodyLegFit(new Vector3(0, side, CompiledBodyAction.HipHeight),
            new Vector3(.04f, side, CompiledBodyAction.HipHeight * .5f), new Vector3(0, side, 0),
            .045f, new Vector3(-.025f, 0, .025f), new Vector3(.055f, 0, .025f), .035f);
    }
}

/// <summary>同一轮完成的真实腿部姿态，膝、足接触及两种显示共同读取。</summary>
public readonly struct BodyLegPose
{
    /// <summary>实际髋、膝及足底。</summary>
    public readonly Vector3 Hip, Knee, Foot;

    /// <summary>实际足部作用轴。</summary>
    public readonly Vector3 FootA, FootB;

    /// <summary>实际作用半径。</summary>
    public readonly float KneeRadius, FootRadius;

    /// <summary>本轮弯曲面及足部方向。</summary>
    public readonly Vector3 Bend;

    /// <summary>足部保持原模型方向，随小腿一同改变。</summary>
    public readonly Quaternion FootRotation;

    /// <summary>解出固定骨长，再将实际足部包络随同一段骨架变换。</summary>
    /// <param name="fit">真实腿部资料。</param><param name="frame">共同身体输入。</param>
    /// <param name="torso">已经约束的躯干世界位移。</param><param name="turn">躯干转体。</param>
    /// <param name="requested">足底的实际目标位置。</param><param name="previous">上一次完成姿态。</param>
    internal BodyLegPose(BodyLegFit fit, in BodyFrame frame, Vector3 torso, float turn,
        Vector3 requested, BodyLegPose? previous)
    {
        Vector3 forward = frame.Forward.normalized, side = Vector3.Cross(forward, Vector3.forward);
        Quaternion facing = Quaternion.LookRotation(forward, Vector3.forward);
        Vector3 Map(Vector3 value) => forward * value.x + side * value.y + Vector3.forward * value.z;
        Hip = frame.Foot + torso + Quaternion.AngleAxis(turn, Vector3.forward) * Map(fit.Hip) * frame.Height;
        Vector3 direction = (requested - Hip).normalized;
        Vector3? bend = previous?.Bend;
        BodyJointSolver.Solve(Hip, requested, fit.Upper * frame.Height, fit.Lower * frame.Height,
            forward, direction, bend, 0, -1, -1, out Vector3 knee, out Vector3 foot, out Vector3 solvedBend);
        Knee = knee;
        Foot = foot;
        Bend = solvedBend;
        Quaternion turnRotation = Quaternion.AngleAxis(turn, Vector3.forward);
        Quaternion moved = Quaternion.FromToRotation(turnRotation * Map(fit.Sole - fit.Knee), foot - knee);
        FootA = foot + moved * turnRotation * Map(fit.FootA) * frame.Height;
        FootB = foot + moved * turnRotation * Map(fit.FootB) * frame.Height;
        KneeRadius = fit.KneeRadius * frame.Height;
        FootRadius = fit.FootRadius * frame.Height;
        FootRotation = moved * turnRotation * facing;
    }
}