using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct GravityModifier : IModifier
{
    public float Radius;
    public float Strength;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Gravity;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"半径{Radius:F1}，强度{Strength:F1}";
}
