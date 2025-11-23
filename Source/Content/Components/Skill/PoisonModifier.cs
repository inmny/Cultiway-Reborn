using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct PoisonModifier : IModifier
{
    public float Duration;
    public float DamageRatio;
    public int MaxStacks;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Poison;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"持续{Duration:F1}s，每层{DamageRatio:P0}伤害，最多{MaxStacks}层";
}
