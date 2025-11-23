using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct SlowModifier : IModifier
{
    public float Duration;
    public float Strength;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Slow;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"持续{Duration:F1}s，减速{Strength:P0}";
}
