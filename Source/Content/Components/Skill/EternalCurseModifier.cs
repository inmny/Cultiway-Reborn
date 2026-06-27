using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct EternalCurseModifier : IModifier
{
    public float Duration;
    public float DamageRatio;
    public float DebuffRatio;
    public SkillModifierAsset ModifierAsset => SkillModifiers.EternalCurse;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"诅咒{Duration:F1}s，持续{DamageRatio:P0}伤害，攻防-{DebuffRatio:P0}";
}
