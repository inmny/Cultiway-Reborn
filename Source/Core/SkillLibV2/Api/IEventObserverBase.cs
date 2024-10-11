using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2.Api;

public interface IEventObserverBase : IComponent
{
    public void Setup(int hashcode);
}