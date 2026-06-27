using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct ReincarnationTrialModifier : IModifier
{
    public float DamageRatio;
    public float BacklashRatio;
    public float HealRatio;
    public SkillModifierAsset ModifierAsset => SkillModifiers.ReincarnationTrial;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"试炼伤害{DamageRatio:P0}，反噬{BacklashRatio:P0}，击杀返生{HealRatio:P0}";
}
