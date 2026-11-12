using System;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>离手后的真实运动阶段，关系解除与后续身体控制分开报告。</summary>
public enum ConstructedBodyFlightState
{
    /// <summary>实际离手并交接运动。</summary>
    Released,
    /// <summary>真实身体到达环境。</summary>
    Contact,
    /// <summary>本次身体运动已归还。</summary>
    Returned,
    /// <summary>外部接管或人物失效。</summary>
    Cancelled,
    /// <summary>实际触地后保留原控制贴地滚动。</summary>
    Rolling,
    /// <summary>停稳后连续接回站姿。</summary>
    Recovering
}

/// <summary>只记录确认后的身体运动，不持有原人物或施放对象。</summary>
public readonly struct ConstructedBodyFlight
{
    /// <summary>原施放、来源及实际目标。</summary>
    public readonly long CastId, SourceId, TargetId;
    /// <summary>实际世界时间。</summary>
    public readonly double WorldTime;
    /// <summary>当前阶段。</summary>
    public readonly ConstructedBodyFlightState State;
    /// <summary>真实根点、中心线速度及世界角速度。</summary>
    public readonly Vector3 Root, Velocity, AngularVelocity;
    /// <summary>相对原身体的真实旋转。</summary>
    public readonly Quaternion Rotation;
    /// <summary>实际环境点；尚未接触时为空。</summary>
    public readonly Vector3? EnvironmentPoint;
    /// <summary>实际环境身份，地面为零。</summary>
    public readonly long EnvironmentOwner;
    /// <summary>接触或归还的实际原因。</summary>
    public readonly string Reason;

    /// <summary>从已经确认的状态复制。</summary>
    /// <param name="body">实际身体运动。</param><param name="state">实际阶段。</param>
    /// <param name="time">共同时间。</param><param name="reason">实际原因。</param>
    /// <param name="point">环境点。</param><param name="owner">环境身份。</param>
    internal ConstructedBodyFlight(ConstructedActorImpulse.BodyFlight body, ConstructedBodyFlightState state,
        double time, string reason, Vector3? point, long owner)
    {
        CastId = body.CastId; SourceId = body.SourceId; TargetId = body.Motion.ActorId;
        WorldTime = time; State = state; Reason = reason;
        Root = body.Surface.Foot; Velocity = body.Ground?.Velocity ?? body.Flight.Arc.Speed(time - body.Motion.StartedAt);
        AngularVelocity = body.Ground?.Angular ?? body.Flight.AngularVelocity; Rotation = body.Surface.RigidTransform.Rotation;
        EnvironmentPoint = point; EnvironmentOwner = owner;
    }
}

/// <summary>离手身体运动的实际事件入口。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>在共同步骤确认后报告离手、接触及控制归还。</summary>
    public static event Action<ConstructedBodyFlight> BodyFlightChanged;

    /// <summary>只发布已经确认的事实。</summary>
    /// <param name="fact">同一世界时间中的实际身体运动。</param>
    internal static void ReportBodyFlight(in ConstructedBodyFlight fact) => BodyFlightChanged?.Invoke(fact);
}
