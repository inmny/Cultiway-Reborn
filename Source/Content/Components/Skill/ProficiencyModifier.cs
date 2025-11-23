using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct ProficiencyModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Proficiency;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}
