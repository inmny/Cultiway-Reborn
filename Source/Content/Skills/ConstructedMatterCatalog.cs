using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>首批已经提供伤害、表面及形状变化规则的材料；不将全部现实物理自动赋予名字。</summary>
internal static class ConstructedMatterCatalog
{
    /// <summary>在构造及正式存档恢复之前登记。</summary>
    internal static void Initialize()
    {
        AbilityMatters.Register(new AbilityMatter("physical", "solid", "physical"));
        AbilityMatters.Register(new AbilityMatter("earth", "solid", "earth", "stone", erosionFactor: .8f));
        AbilityMatters.Register(new AbilityMatter("gold", "solid", "iron", "gold", erosionFactor: .05f));
        AbilityMatters.Register(new AbilityMatter("vitality", "energy", "physical", editedSurface: "restoration_jade"));
        AbilityMatters.Register(new AbilityMatter("spirit", "energy", "spirit", editedSurface: "spirit_pearl"));
        AbilityMatters.Register(new AbilityMatter("yin", "energy", "yin", editedSurface: "yin_depth"));
        AbilityMatters.Register(new AbilityMatter("yang", "energy", "yang", editedSurface: "solar_light"));
        AbilityMatters.Register(new AbilityMatter("lightning", "energy", "lightning", editedSurface: "lightning_dense"));
        AbilityMatters.Register(new AbilityMatter("poison", "liquid", "poison", editedSurface: "poison_liquid"));
        AbilityMatters.Register(new AbilityMatter("poison", "gas", "poison", isDefault: false, editedSurface: "poison_vapor"));
        AbilityMatters.Register(new AbilityMatter("entropy", "energy", "entropy", editedSurface: "entropy_veil"));
        AbilityMatters.Register(new AbilityMatter("wind", "energy", "wind", "wind"));
        AbilityMatters.Register(new AbilityMatter("fire", "energy", "fire", "flame"));
        AbilityMatters.Register(new AbilityMatter("wood", "solid", "wood", "wood", combustible: true, erosionFactor: 1));
        AbilityMatters.Register(new AbilityMatter("iron", "solid", "iron", "metal", erosionFactor: .25f));
        AbilityMatters.Register(new AbilityMatter("iron", "liquid", "molten_iron", "molten_iron", isDefault: false));
        AbilityMatters.Register(new AbilityMatter("water", "liquid", "water", "water"));
        AbilityMatters.Register(new AbilityMatter("water", "solid", "ice", "ice", holdsShape: true, isDefault: false));
        AbilityMatters.Register(new AbilityMatter("ice", "solid", "ice", "ice", holdsShape: true));
    }
}
