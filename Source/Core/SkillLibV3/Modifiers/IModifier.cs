using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Modifiers;

public interface IModifier : IComponent
{
    public SkillModifierAsset ModifierAsset { get; }
}