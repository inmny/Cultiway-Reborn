using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct MercyModifier : IModifier
{
    public float DamageMultiplier;
    public float HealRatio;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Mercy;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"伤害×{DamageMultiplier:F2}，返生{HealRatio:P0}";
}
