using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2.Components.TrajectoryParams;

public struct AngleVelocity(float value) : IComponent
{
    public float value = value;
}