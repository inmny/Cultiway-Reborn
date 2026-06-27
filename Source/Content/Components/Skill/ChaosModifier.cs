using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct ChaosModifier : IModifier
{
    public float DamageVariance;
    public float AngleVariance;
    public float SpeedVariance;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Chaos;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"伤害波动±{DamageVariance:P0}，角度±{AngleVariance:F0}°，速度±{SpeedVariance:P0}";
}
