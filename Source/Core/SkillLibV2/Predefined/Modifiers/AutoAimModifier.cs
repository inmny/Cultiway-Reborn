using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Modifiers;

public struct AutoAimModifier(bool value) : IModifier<bool>
{
    public bool Value { get; set; } = value;
}