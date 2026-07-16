using System;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

[Dependency(typeof(ItemShapes))]
public partial class ArtifactAtoms : ExtendLibrary<ArtifactAtomAsset, ArtifactAtoms>
{
    /// <summary>基础剑形 Atom；提供锋锐、机动和御剑穿刺语义，倾向生成直接进攻型飞剑。</summary>
    public static ArtifactAtomAsset SwordEdge { get; private set; }
    /// <summary>基础印形 Atom；提供坚硬、镇压、稳定和法域投射语义，倾向生成重压封禁型法印。</summary>
    public static ArtifactAtomAsset HeavySeal { get; private set; }
    /// <summary>基础袍形 Atom；提供柔韧、守护、机动和护主语义，倾向生成贴身防护法袍。</summary>
    public static ArtifactAtomAsset RobeWard { get; private set; }
    /// <summary>基础镜形 Atom；提供反射、感知、灵性和洞察语义，倾向生成观照与反制法镜。</summary>
    public static ArtifactAtomAsset BrightMirror { get; private set; }
    /// <summary>基础鼎形 Atom；提供容量、炼丹、稳定和蓄灵语义，倾向生成火炼与生产辅助法鼎。</summary>
    public static ArtifactAtomAsset CauldronFire { get; private set; }
    /// <summary>玉质材料 Atom；提供灵性、守护、稳定和净化语义，适合均衡防护型法器。</summary>
    public static ArtifactAtomAsset Jade { get; private set; }
    /// <summary>晶质材料 Atom；提供坚硬、反射、灵性和洞察语义，适合感知与镜面效果。</summary>
    public static ArtifactAtomAsset Crystal { get; private set; }
    /// <summary>铁质材料 Atom；提供坚硬、锋锐、镇压和束缚语义，适合攻击与禁制效果。</summary>
    public static ArtifactAtomAsset Iron { get; private set; }
    /// <summary>赤火饰面 Atom；提供火行、易变、炼制和增幅语义，适合爆发与火炼效果。</summary>
    public static ArtifactAtomAsset Ember { get; private set; }
    /// <summary>玄金饰面 Atom；提供坚硬、灵性、稳定和共鸣语义，适合稳固的高阶法器。</summary>
    public static ArtifactAtomAsset DarkGold { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.ArtifactAtom";

    protected override void OnInit()
    {
        Set(SwordEdge, "sword_edge", ArtifactAtomCategory.Shape, ["锋", "玄锋", "断岳"],
            ["sword_blade3d.long_thorn", "sword_guard3d.wing"], ["dark_steel", "black_gold"],
            r => Shape(r, ItemShapes.Bone, ItemShapes.Claw, ItemShapes.Tooth, ItemShapes.Horn, ItemShapes.Feather, ItemShapes.Wing),
            [
                Trait(ArtifactMaterialTraits.Edge, 1f),
                Trait(ArtifactMaterialTraits.Mobility, 0.45f),
                Trait(ArtifactMaterialTraits.PiercingFlight, 1f),
            ],
            ItemShapes.Sword);

        Set(HeavySeal, "heavy_seal", ArtifactAtomCategory.Shape, ["镇山", "岳印", "玄岳"],
            ["seal_mountain3d.green", "seal_mountain3d.amber"], ["gold_jade", "dark_steel"],
            r => Shape(r, ItemShapes.Stone, ItemShapes.Shell, ItemShapes.Crystal),
            [
                Trait(ArtifactMaterialTraits.Hardness, 0.85f),
                Trait(ArtifactMaterialTraits.Suppression, 1f),
                Trait(ArtifactMaterialTraits.Stability, 0.45f),
                Trait(ArtifactMaterialTraits.FieldProjection, 1f),
            ],
            ItemShapes.Seal);

        Set(RobeWard, "robe_ward", ArtifactAtomCategory.Shape, ["护身", "云纹", "灵衣"],
            ["robe_panel3d.wide_blue", "robe_panel3d.split_green"], ["cold_crystal", "dark_steel"],
            r => Shape(r, ItemShapes.Fur, ItemShapes.Silk, ItemShapes.Bamboo, ItemShapes.Herb, ItemShapes.Flower),
            [
                Trait(ArtifactMaterialTraits.Flexibility, 1f),
                Trait(ArtifactMaterialTraits.Ward, 0.9f),
                Trait(ArtifactMaterialTraits.Mobility, 0.25f),
                Trait(ArtifactMaterialTraits.GuardianWard, 1f),
            ],
            ItemShapes.Robe);

        Set(BrightMirror, "bright_mirror", ArtifactAtomCategory.Shape, ["明鉴", "照影", "灵鉴"],
            ["mirror3d.bronze_round", "mirror3d.jade_hex"], ["cold_crystal", "gold_jade"],
            r => Shape(r, ItemShapes.Eye, ItemShapes.Blood, ItemShapes.Liquid),
            [
                Trait(ArtifactMaterialTraits.Reflection, 1f),
                Trait(ArtifactMaterialTraits.Perception, 0.9f),
                Trait(ArtifactMaterialTraits.Spirituality, 0.25f),
                Trait(ArtifactMaterialTraits.Insight, 1f),
            ],
            ItemShapes.Mirror);

        Set(CauldronFire, "cauldron_fire", ArtifactAtomCategory.Shape, ["丹火", "炉心", "鼎元"],
            ["ding3d.blue_fire", "ding3d.copper_ember"], ["copper_ember", "black_gold"],
            r => Shape(r, ItemShapes.Wood, ItemShapes.Root, ItemShapes.Mushroom, ItemShapes.Fruit, ItemShapes.Lotus),
            [
                Trait(ArtifactMaterialTraits.Capacity, 1f),
                Trait(ArtifactMaterialTraits.Alchemy, 1f),
                Trait(ArtifactMaterialTraits.AlchemyVessel, 1f),
                Trait(ArtifactMaterialTraits.Stability, 0.35f),
                Trait(ArtifactMaterialTraits.SpiritReservoir, 0.45f),
            ],
            ItemShapes.Ding);

        Set(Jade, "jade", ArtifactAtomCategory.Material, ["青玉", "碧玉", "灵玉"],
            ["sword_blade3d.jade", "mirror3d.jade_hex"], ["gold_jade"],
            r => Shape(r, ItemShapes.Lotus, ItemShapes.Herb, ItemShapes.Root, ItemShapes.Crystal) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 1.5f),
            [
                Trait(ArtifactMaterialTraits.Spirituality, 0.45f),
                Trait(ArtifactMaterialTraits.Ward, 0.25f),
                Trait(ArtifactMaterialTraits.Stability, 0.55f),
                Trait(ArtifactMaterialTraits.Purification, 0.35f),
            ]);

        Set(Crystal, "crystal", ArtifactAtomCategory.Material, ["冰晶", "水晶", "清光"],
            ["sword_blade3d.crystal", "mirror3d.bronze_round"], ["cold_crystal"],
            r => Shape(r, ItemShapes.Crystal, ItemShapes.Liquid, ItemShapes.Eye) +
                 Semantic(r, ArtifactMaterialTraits.Water, 1.25f) +
                 Semantic(r, ArtifactMaterialTraits.Pos, 0.75f),
            [
                Trait(ArtifactMaterialTraits.Hardness, 0.4f),
                Trait(ArtifactMaterialTraits.Reflection, 0.55f),
                Trait(ArtifactMaterialTraits.Spirituality, 0.35f),
                Trait(ArtifactMaterialTraits.Insight, 0.25f),
            ]);

        Set(Iron, "iron", ArtifactAtomCategory.Material, ["玄铁", "黑铁", "铁骨"],
            ["sword_blade3d.long_thorn", "sword_guard3d.bar"], ["dark_steel"],
            r => Shape(r, ItemShapes.Bone, ItemShapes.Stone, ItemShapes.Shell) +
                 Semantic(r, ArtifactMaterialTraits.Iron, 1.5f),
            [
                Trait(ArtifactMaterialTraits.Hardness, 0.7f),
                Trait(ArtifactMaterialTraits.Edge, 0.35f),
                Trait(ArtifactMaterialTraits.Suppression, 0.2f),
                Trait(ArtifactMaterialTraits.Binding, 0.35f),
            ]);

        Set(Ember, "ember", ArtifactAtomCategory.Finish, ["赤火", "赤炼", "离火"],
            ["ding3d.copper_ember"], ["copper_ember"],
            r => Shape(r, ItemShapes.Blood, ItemShapes.Horn, ItemShapes.Tooth) + Quality(r, 1) +
                 Semantic(r, ArtifactMaterialTraits.Fire, 1.5f),
            [
                Trait(ArtifactMaterialTraits.Fire, 0.45f),
                Trait(ArtifactMaterialTraits.Volatility, 0.65f),
                Trait(ArtifactMaterialTraits.Alchemy, 0.25f),
                Trait(ArtifactMaterialTraits.Amplification, 0.25f),
            ]);

        Set(DarkGold, "dark_gold", ArtifactAtomCategory.Finish, ["玄金", "乌金", "金纹"],
            ["sword_guard3d.crescent", "ding3d.blue_fire"], ["black_gold"],
            r => Quality(r, 1) + Shape(r, ItemShapes.Stone, ItemShapes.Crystal, ItemShapes.Shell) +
                 Semantic(r, ArtifactMaterialTraits.Iron, 0.8f) +
                 Semantic(r, ArtifactMaterialTraits.Earth, 0.55f),
            [
                Trait(ArtifactMaterialTraits.Hardness, 0.55f),
                Trait(ArtifactMaterialTraits.Spirituality, 0.2f),
                Trait(ArtifactMaterialTraits.Stability, 0.3f),
                Trait(ArtifactMaterialTraits.Resonance, 0.25f),
            ]);

        ConfigureShapeAtoms();
        ConfigureMaterialAtoms();
        ConfigureFinishAtoms();
    }

    private static void Set(
        ArtifactAtomAsset atom,
        string tag,
        ArtifactAtomCategory category,
        string[] stems,
        string[] variantBiases,
        string[] colorSchemeBiases,
        Func<ArtifactRecipeContext, float> score,
        ArtifactMaterialTrait[] semanticTraits,
        ArtifactShapeAsset artifactShape = null)
    {
        atom.tag = tag;
        atom.category = category;
        atom.artifact_shape = artifactShape;
        atom.name_stems = stems;
        atom.variant_biases = variantBiases;
        atom.color_scheme_biases = colorSchemeBiases;
        atom.semantic_traits = semanticTraits;
        atom.minimum_score = 1f;
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

    private static float Semantic(ArtifactRecipeContext recipe, string key, float multiplier)
    {
        return recipe.GetTrait(key) * multiplier;
    }

    private static ArtifactMaterialTrait Trait(string key, float value)
    {
        return new ArtifactMaterialTrait { key = key, value = value };
    }
}
