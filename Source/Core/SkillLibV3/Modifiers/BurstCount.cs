namespace Cultiway.Core.SkillLibV3.Modifiers;

public struct BurstCount : IModifier
{
    public int Value;
    public SkillModifierAsset ModifierAsset => SkillModifierLibrary.BurstCount;
    public string GetKey()
    {
        return ModifierAsset.id.Localize();
    }

    public string GetValue()
    {
        return Value.ToString();
    }
}