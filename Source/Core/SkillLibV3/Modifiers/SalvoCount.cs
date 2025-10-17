namespace Cultiway.Core.SkillLibV3.Modifiers;

public struct SalvoCount : IModifier
{
    public int Value;
    public SkillModifierAsset ModifierAsset => SkillModifierLibrary.SalvoCount;
    public string GetKey()
    {
        return ModifierAsset.id.Localize();
    }

    public string GetValue()
    {
        return Value.ToString();
    }
}