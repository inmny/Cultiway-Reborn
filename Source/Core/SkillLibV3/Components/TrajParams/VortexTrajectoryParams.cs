using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

public struct VortexTrajectoryParams : IComponent
{
    public float ForwardSpeed;
    public float Radius;
    public float AngularSpeed;
    public float PulseAmplitude;
    public float PulseFrequency;
}
