using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

public struct BoomerangTrajectoryParams : IComponent
{
    public float OutDistance;
    public float ReturnTurnRate;
    public float MaxLifetime;
}
