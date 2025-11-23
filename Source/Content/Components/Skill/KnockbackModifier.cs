using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct KnockbackModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Knockback;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}
