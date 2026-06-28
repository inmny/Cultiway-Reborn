using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

public struct SpiralTrajectoryParams : IComponent
{
    public float Radius;
    public float Frequency;
    public float RadiusDamping;
    public float HomingStrength;
}
