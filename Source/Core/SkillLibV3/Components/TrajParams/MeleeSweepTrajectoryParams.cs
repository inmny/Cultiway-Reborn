using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

/// <summary>
/// 近身挥砍轨迹参数。角度以施法时的目标方向为零度。
/// </summary>
public struct MeleeSweepTrajectoryParams : IComponent
{
    public float Radius;
    public float StartAngle;
    public float EndAngle;
    public float Duration;
}
