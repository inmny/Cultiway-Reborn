using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct EmpowerModifier : IModifier
{
    public float SetupBonus;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Empower;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"所有伤害+{SetupBonus:P0}";
}
