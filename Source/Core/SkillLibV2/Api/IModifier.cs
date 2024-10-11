using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2.Api;

public interface IModifier<out TValue> : IComponent
{
    public TValue Value { get; }
}