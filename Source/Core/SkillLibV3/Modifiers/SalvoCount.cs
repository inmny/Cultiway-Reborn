namespace Cultiway.Core.SkillLibV3.Modifiers;

public struct SalvoCount : IModifier
{
    public int Value;
    public SkillModifierAsset ModifierAsset => SkillModifierLibrary.SalvoCount;
}