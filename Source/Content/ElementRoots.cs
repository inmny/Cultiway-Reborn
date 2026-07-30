using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

[Dependency(typeof(CultivationSemantics))]
public class ElementRoots : ExtendLibrary<ElementRootAsset, ElementRoots>
{
    private const float DualRootNaturalWeight = 2f;
    private const float DualRootNaturalSimilarityFloor = 0.9f;
    private const float DerivedRootNaturalSimilarityFloor = 0.9f;

    public static ElementRootAsset Fire      { get; private set; }
    public static ElementRootAsset Water     { get; private set; }
    public static ElementRootAsset Wood      { get; private set; }
    public static ElementRootAsset Earth     { get; private set; }
    public static ElementRootAsset Iron      { get; private set; }
    public static ElementRootAsset Neg       { get; private set; }
    public static ElementRootAsset Pos       { get; private set; }
    public static ElementRootAsset IronWood  { get; private set; }
    public static ElementRootAsset IronWater { get; private set; }
    public static ElementRootAsset IronFire  { get; private set; }
    public static ElementRootAsset IronEarth { get; private set; }
    public static ElementRootAsset WoodWater { get; private set; }
    public static ElementRootAsset WoodFire  { get; private set; }
    public static ElementRootAsset WoodEarth { get; private set; }
    public static ElementRootAsset WaterFire { get; private set; }
    public static ElementRootAsset WaterEarth { get; private set; }
    public static ElementRootAsset FireEarth { get; private set; }
    public static ElementRootAsset Wind      { get; private set; }
    public static ElementRootAsset Ice       { get; private set; }
    public static ElementRootAsset Lightning { get; private set; }
    public static ElementRootAsset Poison    { get; private set; }

    protected override bool AutoRegisterAssets() => false;
    protected override void OnInit()
    {
        Iron = AddRoot(nameof(Iron), ElementComposition.Static.Iron,
            "cultiway/icons/element_root/iron", 9f, 0.78f, SkillSemantics.Element.Iron);
        Wood = AddRoot(nameof(Wood), ElementComposition.Static.Wood,
            "cultiway/icons/element_root/wood", 9f, 0.78f, SkillSemantics.Element.Wood);
        Water = AddRoot(nameof(Water), ElementComposition.Static.Water,
            "cultiway/icons/element_root/water", 9f, 0.78f, SkillSemantics.Element.Water);
        Fire = AddRoot(nameof(Fire), ElementComposition.Static.Fire,
            "cultiway/icons/element_root/fire", 9f, 0.78f, SkillSemantics.Element.Fire);
        Earth = AddRoot(nameof(Earth), ElementComposition.Static.Earth,
            "cultiway/icons/element_root/earth", 9f, 0.78f, SkillSemantics.Element.Earth);
        IronWood = AddDualRoot(nameof(IronWood), ElementComposition.Static.IronWood,
            SkillSemantics.Element.Iron, SkillSemantics.Element.Wood);
        IronWater = AddDualRoot(nameof(IronWater), ElementComposition.Static.IronWater,
            SkillSemantics.Element.Iron, SkillSemantics.Element.Water);
        IronFire = AddDualRoot(nameof(IronFire), ElementComposition.Static.IronFire,
            SkillSemantics.Element.Iron, SkillSemantics.Element.Fire);
        IronEarth = AddDualRoot(nameof(IronEarth), ElementComposition.Static.IronEarth,
            SkillSemantics.Element.Iron, SkillSemantics.Element.Earth);
        WoodWater = AddDualRoot(nameof(WoodWater), ElementComposition.Static.WoodWater,
            SkillSemantics.Element.Wood, SkillSemantics.Element.Water);
        WoodFire = AddDualRoot(nameof(WoodFire), ElementComposition.Static.WoodFire,
            SkillSemantics.Element.Wood, SkillSemantics.Element.Fire);
        WoodEarth = AddDualRoot(nameof(WoodEarth), ElementComposition.Static.WoodEarth,
            SkillSemantics.Element.Wood, SkillSemantics.Element.Earth);
        WaterFire = AddDualRoot(nameof(WaterFire), ElementComposition.Static.WaterFire,
            SkillSemantics.Element.Water, SkillSemantics.Element.Fire);
        WaterEarth = AddDualRoot(nameof(WaterEarth), ElementComposition.Static.WaterEarth,
            SkillSemantics.Element.Water, SkillSemantics.Element.Earth);
        FireEarth = AddDualRoot(nameof(FireEarth), ElementComposition.Static.FireEarth,
            SkillSemantics.Element.Fire, SkillSemantics.Element.Earth);
        Neg = AddRoot(nameof(Neg), new ElementComposition(neg: 1f),
            "cultiway/icons/element_root/neg", 1.75f, 0.86f, SkillSemantics.Element.Neg);
        Pos = AddRoot(nameof(Pos), new ElementComposition(pos: 1f),
            "cultiway/icons/element_root/pos", 1.75f, 0.86f, SkillSemantics.Element.Pos);
        Wind = AddRoot(nameof(Wind), ElementComposition.Static.Wind,
            "cultiway/icons/artifact_atoms/tempest_fan", 1f, DerivedRootNaturalSimilarityFloor,
            SkillSemantics.Element.Wind);
        Ice = AddRoot(nameof(Ice), ElementComposition.Static.Ice,
            "cultiway/icons/artifact_atoms/frost_jade", 1f, DerivedRootNaturalSimilarityFloor,
            SkillSemantics.Element.Ice);
        Lightning = AddRoot(nameof(Lightning), ElementComposition.Static.Lightning,
            "cultiway/icons/artifact_atoms/thunder_pattern", 1f, DerivedRootNaturalSimilarityFloor,
            SkillSemantics.Element.Lightning);
        Poison = AddRoot(nameof(Poison), ElementComposition.Static.Poison,
            "cultiway/icons/skill_modifiers/poison", 1f, DerivedRootNaturalSimilarityFloor,
            SkillSemantics.Element.Poison);

        SetSemantics(ModClass.L.ElementRootLibrary.Common);
        SetSemantics(ModClass.L.ElementRootLibrary.Entropy, SkillSemantics.Element.Entropy);
    }

    /// <summary>注册一个具名灵根原型及其自然生成参数。</summary>
    private ElementRootAsset AddRoot(
        string id,
        ElementComposition composition,
        string iconPath,
        float naturalWeight,
        float naturalSimilarityFloor,
        params SemanticAsset[] elementSemantics)
    {
        var asset = Add(new ElementRootAsset(id, composition));
        asset.IconPath = iconPath;
        asset.Archetype.NaturalWeight = naturalWeight;
        asset.Archetype.NaturalSimilarityFloor = naturalSimilarityFloor;
        asset.Archetype.PuritySimilarityBaseline = naturalSimilarityFloor;
        SetSemantics(asset, elementSemantics);
        return asset;
    }

    /// <summary>注册一个等比例五行双灵根，并复用五行灵根图标表达复合构成。</summary>
    private ElementRootAsset AddDualRoot(
        string id,
        ElementComposition composition,
        SemanticAsset first,
        SemanticAsset second)
    {
        return AddRoot(
            id,
            composition,
            "cultiway/icons/element_root/common",
            DualRootNaturalWeight,
            DualRootNaturalSimilarityFloor,
            first,
            second);
    }

    /// <summary>为灵根资产写入共同的灵根特征以及自身声明的元素语义。</summary>
    private static void SetSemantics(ElementRootAsset asset, params SemanticAsset[] elementSemantics)
    {
        var builder = new SemanticDescriptorBuilder()
            .Add(CultivationSemantics.Trait.ElementRoot);
        for (var i = 0; i < elementSemantics.Length; i++) builder.Add(elementSemantics[i]);
        asset.Semantics = builder.Build();
    }
}
