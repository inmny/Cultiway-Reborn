using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

/// <summary>
/// 追踪轨迹相对施法目标所处的运行阶段。
/// </summary>
public enum TrajectoryTargetPhase : byte
{
    /// <summary>仍在向目标转向和推进。</summary>
    Seeking,
    /// <summary>已经抵达目标，等待当前逻辑帧的碰撞结算。</summary>
    AwaitingImpact,
    /// <summary>已经命中或越过目标，沿入射方向继续运动。</summary>
    PassedTarget
}

public struct TrajectoryRuntimeState : IComponent
{
    public bool Initialized;
    public bool Returning;
    public TrajectoryTargetPhase TargetPhase;
    public Vector3 StartPosition;
    public Vector3 StartDirection;
    public Vector3 TargetExitDirection;
    public float Elapsed;
    public float DistanceTravelled;
    public float Phase;
    public float Timer;
    public int StepIndex;
}
