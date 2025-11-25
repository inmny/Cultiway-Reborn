using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct KnockbackModifier : IModifier
{
    public float Distance;
    public float Height;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Knockback;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"距离{Distance:F1}，高度{Height:F1}";
}
