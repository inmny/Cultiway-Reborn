using Cultiway.Content;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components.Skill;

public struct DazeModifier : IModifier
{
    public float Duration;
    public SkillModifierAsset ModifierAsset => SkillModifiers.Daze;
    public string GetKey() => ModifierAsset.id.Localize();
    public string GetValue() => $"硬直{Duration:F1}s";
}
