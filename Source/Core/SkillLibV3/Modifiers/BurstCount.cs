namespace Cultiway.Core.SkillLibV3.Modifiers;

public struct BurstCount : IModifier
{
    public int Value;
    public SkillModifierAsset ModifierAsset => SkillModifierLibrary.BurstCount;
}