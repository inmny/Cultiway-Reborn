using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

public struct FallingTrajectoryParams : IComponent
{
    public float StartHeight;
    public float FallSpeed;
    public float DriftSpeed;
    public float ImpactHeight;
}
