using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct SilenceModifier : IModifier
{
    public float Duration;
    public float DamageReduction;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Silence;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"封印{Duration:F1}s，伤害-{DamageReduction:P0}";
}
