using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct ProficiencyModifier : IModifier
{
    public float CostReduction;
    public float SalvoIntervalReduction;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Proficiency;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"施法消耗-{CostReduction:P0}，连发间隔-{SalvoIntervalReduction:P0}";
}
