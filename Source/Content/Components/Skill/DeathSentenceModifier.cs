using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct DeathSentenceModifier : IModifier
{
    public float ExecuteHealthRatio;
    public float BonusDamageRatio;
    public SkillModifierAsset ModifierAsset => SkillModifiers.DeathSentence;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"生命低于{ExecuteHealthRatio:P0}斩杀，否则追加{BonusDamageRatio:P0}伤害";
}
