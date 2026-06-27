using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct VolleyModifier : IModifier
{
    public int BurstBonus;
    public float DamageMultiplier;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Volley;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"分散倾向+{BurstBonus}，单发伤害×{DamageMultiplier:F2}";
}
