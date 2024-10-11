using Cultiway.Core.SkillLib.Components.Modifiers;

namespace Cultiway.Content.Skills.Modifiers;

public struct CastInterval : IActionModifier<float>
{
    public float Default => 1;
    public float Value   { get; set; }
}