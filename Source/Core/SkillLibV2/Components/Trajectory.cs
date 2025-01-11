using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Core.SkillLibV2.Components;

public struct Trajectory : IComponent
{
    [Ignore]
    public TrajectoryMeta meta;
}