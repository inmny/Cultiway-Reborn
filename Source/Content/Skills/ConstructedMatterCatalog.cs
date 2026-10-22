using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>首批已经提供伤害、表面及形状变化规则的材料；不将全部现实物理自动赋予名字。</summary>
internal static class ConstructedMatterCatalog
{
    /// <summary>在构造及正式存档恢复之前登记。</summary>
    internal static void Initialize()
    {
        AbilityMatters.Register(new AbilityMatter("physical", "solid", "physical"));
        AbilityMatters.Register(new AbilityMatter("fire", "energy", "fire", "flame"));
        AbilityMatters.Register(new AbilityMatter("wood", "solid", "wood", "wood", combustible: true));
        AbilityMatters.Register(new AbilityMatter("iron", "solid", "iron", "metal"));
        AbilityMatters.Register(new AbilityMatter("iron", "liquid", "molten_iron", "molten_iron", isDefault: false));
        AbilityMatters.Register(new AbilityMatter("water", "liquid", "water", "water"));
        AbilityMatters.Register(new AbilityMatter("water", "solid", "ice", "ice", holdsShape: true, isDefault: false));
        AbilityMatters.Register(new AbilityMatter("ice", "solid", "ice", "ice", holdsShape: true));
    }
}
