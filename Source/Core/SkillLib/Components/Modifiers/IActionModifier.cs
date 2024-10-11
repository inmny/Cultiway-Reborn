using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLib.Components.Modifiers;

public interface IActionModifier<TVal> : IComponent
{
    public TVal Default { get; }
    public TVal Value   { get; }
}