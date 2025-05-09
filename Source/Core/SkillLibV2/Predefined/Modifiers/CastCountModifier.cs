using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Modifiers;

public struct CastCountModifier(int value) : IModifier<int>
{
    public int Value { get; set; } = value;
}