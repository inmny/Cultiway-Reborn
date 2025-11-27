using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct ExplosionModifier : IModifier
{
    public float Radius;
    public float DamageRatio;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Explosion;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"半径{Radius:F1}，伤害{DamageRatio:P0}";
}
