using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct ArmorBreakModifier : IModifier
{
    public float Duration;
    public float ArmorReduction;
    public SkillModifierAsset ModifierAsset => SkillModifiers.ArmorBreak;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"持续{Duration:F1}s，破甲{ArmorReduction:P0}";
}
