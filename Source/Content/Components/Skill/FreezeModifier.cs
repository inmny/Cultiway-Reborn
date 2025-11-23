using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct FreezeModifier : IModifier
{
    public SkillModifierAsset ModifierAsset => SkillModifiers.Freeze;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => string.Empty;
}
