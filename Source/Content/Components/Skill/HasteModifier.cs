using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct HasteModifier : IModifier
{
    public float SpeedMultiplier;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Haste;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"速度+{SpeedMultiplier:P0}";
}
