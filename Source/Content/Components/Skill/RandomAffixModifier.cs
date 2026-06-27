using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct RandomAffixModifier : IModifier
{
    public float Chance;
    public float EffectPower;
    public SkillModifierAsset ModifierAsset => SkillModifiers.RandomAffix;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"命中{Chance:P0}概率触发随机效果，强度{EffectPower:F1}";
}
