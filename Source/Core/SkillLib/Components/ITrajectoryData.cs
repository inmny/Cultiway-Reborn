using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLib.Components;

public interface ITrajectoryData<out T> : IComponent
{
    public T Value { get; }
}