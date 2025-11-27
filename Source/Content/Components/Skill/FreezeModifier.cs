using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct FreezeModifier : IModifier
{
    public float Duration;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Freeze;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"持续{Duration:F1}s";
}
