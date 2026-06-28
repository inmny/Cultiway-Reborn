using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

public struct OrbitTrajectoryParams : IComponent
{
    public float StartRadius;
    public float AngularSpeed;
    public float ShrinkSpeed;
    public float HomingStrength;
}
