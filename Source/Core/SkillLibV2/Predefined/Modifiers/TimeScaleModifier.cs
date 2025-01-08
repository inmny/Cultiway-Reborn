using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Modifiers;

public struct TimeScaleModifier(float value) : IModifier<float>
{
    public float Value { get; set; } = value;
}