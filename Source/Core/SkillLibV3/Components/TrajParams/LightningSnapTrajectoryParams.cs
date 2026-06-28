using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

public struct LightningSnapTrajectoryParams : IComponent
{
    public float StepInterval;
    public float StepDistance;
    public float JitterRadius;
    public float HitDistance;
}
