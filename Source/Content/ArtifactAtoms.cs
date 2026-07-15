using System;
using Cultiway.Abstract;
using Cultiway.Content.Libraries;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

[Dependency(typeof(ItemShapes))]
public class ArtifactAtoms : ExtendLibrary<ArtifactAtomAsset, ArtifactAtoms>
{
    public static ArtifactAtomAsset SwordEdge { get; private set; }
    public static ArtifactAtomAsset HeavySeal { get; private set; }
    public static ArtifactAtomAsset RobeWard { get; private set; }
    public static ArtifactAtomAsset BrightMirror { get; private set; }
    public static ArtifactAtomAsset CauldronFire { get; private set; }
    public static ArtifactAtomAsset Jade { get; private set; }
    public static ArtifactAtomAsset Crystal { get; private set; }
    public static ArtifactAtomAsset Iron { get; private set; }
    public static ArtifactAtomAsset Ember { get; private set; }
    public static ArtifactAtomAsset DarkGold { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.ArtifactAtom";

    protected override void OnInit()
    {
        Set(SwordEdge, "sword_edge", ArtifactAtomCategory.Shape, ["锋", "玄锋", "断岳"],
            ["sword_blade3d.long_thorn", "sword_guard3d.wing"], ["dark_steel", "black_gold"],
            r => Shape(r, ItemShapes.Bone, ItemShapes.Claw, ItemShapes.Tooth, ItemShapes.Horn, ItemShapes.Feather, ItemShapes.Wing),
            ItemShapes.Sword);

        Set(HeavySeal, "heavy_seal", ArtifactAtomCategory.Shape, ["镇山", "岳印", "玄岳"],
            ["seal_mountain3d.green", "seal_mountain3d.amber"], ["gold_jade", "dark_steel"],
            r => Shape(r, ItemShapes.Stone, ItemShapes.Shell, ItemShapes.Crystal),
            ItemShapes.Seal);

        Set(RobeWard, "robe_ward", ArtifactAtomCategory.Shape, ["护身", "云纹", "灵衣"],
            ["robe_panel3d.wide_blue", "robe_panel3d.split_green"], ["cold_crystal", "dark_steel"],
            r => Shape(r, ItemShapes.Fur, ItemShapes.Silk, ItemShapes.Bamboo, ItemShapes.Herb, ItemShapes.Flower),
            ItemShapes.Robe);

        Set(BrightMirror, "bright_mirror", ArtifactAtomCategory.Shape, ["明鉴", "照影", "灵鉴"],
            ["mirror3d.bronze_round", "mirror3d.jade_hex"], ["cold_crystal", "gold_jade"],
            r => Shape(r, ItemShapes.Eye, ItemShapes.Blood, ItemShapes.Liquid),
            ItemShapes.Mirror);

        Set(CauldronFire, "cauldron_fire", ArtifactAtomCategory.Shape, ["丹火", "炉心", "鼎元"],
            ["ding3d.blue_fire", "ding3d.copper_ember"], ["copper_ember", "black_gold"],
            r => Shape(r, ItemShapes.Wood, ItemShapes.Root, ItemShapes.Mushroom, ItemShapes.Fruit, ItemShapes.Lotus),
            ItemShapes.Ding);

        Set(Jade, "jade", ArtifactAtomCategory.Material, ["青玉", "碧玉", "灵玉"],
            ["sword_blade3d.jade", "mirror3d.jade_hex"], ["gold_jade"],
            r => Shape(r, ItemShapes.Lotus, ItemShapes.Herb, ItemShapes.Root, ItemShapes.Crystal));

        Set(Crystal, "crystal", ArtifactAtomCategory.Material, ["冰晶", "水晶", "清光"],
            ["sword_blade3d.crystal", "mirror3d.bronze_round"], ["cold_crystal"],
            r => Shape(r, ItemShapes.Crystal, ItemShapes.Liquid, ItemShapes.Eye));

        Set(Iron, "iron", ArtifactAtomCategory.Material, ["玄铁", "黑铁", "铁骨"],
            ["sword_blade3d.long_thorn", "sword_guard3d.bar"], ["dark_steel"],
            r => Shape(r, ItemShapes.Bone, ItemShapes.Stone, ItemShapes.Shell));

        Set(Ember, "ember", ArtifactAtomCategory.Finish, ["赤火", "赤炼", "离火"],
            ["ding3d.copper_ember"], ["copper_ember"],
            r => Shape(r, ItemShapes.Blood, ItemShapes.Horn, ItemShapes.Tooth) + Quality(r, 1));

        Set(DarkGold, "dark_gold", ArtifactAtomCategory.Finish, ["玄金", "乌金", "金纹"],
            ["sword_guard3d.crescent", "ding3d.blue_fire"], ["black_gold"],
            r => Quality(r, 1) + Shape(r, ItemShapes.Stone, ItemShapes.Crystal, ItemShapes.Shell));
    }

    private static void Set(
        ArtifactAtomAsset atom,
        string tag,
        ArtifactAtomCategory category,
        string[] stems,
        string[] variantBiases,
        string[] colorSchemeBiases,
        Func<ArtifactRecipeContext, float> score,
        ArtifactShapeAsset artifactShape = null)
    {
        atom.tag = tag;
        atom.category = category;
        atom.artifact_shape = artifactShape;
        atom.name_stems = stems;
        atom.variant_biases = variantBiases;
        atom.color_scheme_biases = colorSchemeBiases;
        atom.priority = 100;
        atom.ScoreRecipe = score;
    }

    private static float Shape(ArtifactRecipeContext recipe, params ItemShapeAsset[] shapes)
    {
        var score = 0f;
        for (int i = 0; i < shapes.Length; i++)
        {
            var shape = shapes[i];
            if (shape == null) continue;
            score += recipe.CountShape(shape) * 4f;
            if (recipe.main_material_shape_id == shape.id) score += 2f;
        }
        return score;
    }

    private static float Quality(ArtifactRecipeContext recipe, int minStage)
    {
        return recipe.quality_stage >= minStage ? 1.5f + recipe.quality_stage * 0.5f : 0f;
    }
}
