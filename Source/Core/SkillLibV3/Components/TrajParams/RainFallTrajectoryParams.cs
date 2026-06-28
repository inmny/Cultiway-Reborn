using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

public struct RainFallTrajectoryParams : IComponent
{
    public float StartHeight;
    public float FallSpeed;
    public float HorizontalDrift;
    public float ImpactHeight;
}
