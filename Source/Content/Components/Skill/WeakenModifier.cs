using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct WeakenModifier : IModifier
{
    public float Duration;
    public float AttackReduction;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Weaken;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"持续{Duration:F1}s，攻降{AttackReduction:P0}";
}
