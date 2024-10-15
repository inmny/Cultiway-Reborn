using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Examples;

public struct CastNumModifier : IModifier<int>
{
    public int Value { get; set; }
}

public struct CastSpeedModifier : IModifier<float>
{
    public float Value { get; set; }
}

public struct AutoAimModifier : IModifier<bool>
{
    public bool Value { get; set; }
}