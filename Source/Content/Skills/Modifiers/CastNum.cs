using Cultiway.Core.SkillLib.Components.Modifiers;

namespace Cultiway.Content.Skills.Modifiers;

public struct CastNum : IActionModifier<int>
{
    public int Default => 1;
    public int Value   { get; set; }
}