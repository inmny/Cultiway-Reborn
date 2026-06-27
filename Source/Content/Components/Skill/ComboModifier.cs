using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct ComboModifier : IModifier
{
    public int SalvoBonus;
    public float DamageMultiplier;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Combo;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"连发倾向+{SalvoBonus}，单发伤害×{DamageMultiplier:F2}";
}
