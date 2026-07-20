using Cultiway.Content.Semantics;
using Cultiway.Core.Semantics;

namespace Cultiway.Content;

public partial class ItemShapes
{
    private static void ConfigureIngredientSemantics()
    {
        Blood.semantics = DescribeIngredient(
            CultivationSemantics.Resource.Vitality,
            CultivationSemantics.Form.Body,
            CultivationSemantics.Material.Volatility);
        Bone.semantics = DescribeIngredient(
            CultivationSemantics.Material.Hardness,
            CultivationSemantics.Material.Stability,
            CultivationSemantics.Form.Body);
        Claw.semantics = DescribeIngredient(
            CultivationSemantics.Form.Blade,
            CultivationSemantics.Material.Hardness,
            CultivationSemantics.Effect.ArmorBreak);
        Horn.semantics = DescribeIngredient(
            CultivationSemantics.Form.Blade,
            CultivationSemantics.Material.Hardness,
            CultivationSemantics.Effect.ArmorBreak);
        Tooth.semantics = DescribeIngredient(
            CultivationSemantics.Form.Blade,
            CultivationSemantics.Material.Hardness,
            CultivationSemantics.Effect.ArmorBreak);
        Hoof.semantics = DescribeIngredient(
            CultivationSemantics.Effect.Impact,
            CultivationSemantics.Material.Hardness,
            CultivationSemantics.Material.Stability);
        Eye.semantics = DescribeIngredient(
            CultivationSemantics.Effect.Perception,
            CultivationSemantics.Effect.Revealing,
            CultivationSemantics.Resource.Spirituality);
        Fur.semantics = DescribeIngredient(
            CultivationSemantics.Material.Flexibility,
            CultivationSemantics.Effect.Ward,
            CultivationSemantics.Effect.Concealment);
        Feather.semantics = DescribeIngredient(
            CultivationSemantics.Material.Lightweight,
            CultivationSemantics.Effect.Mobility,
            CultivationSemantics.Material.Flexibility);
        Wing.semantics = DescribeIngredient(
            CultivationSemantics.Material.Lightweight,
            CultivationSemantics.Effect.Mobility,
            CultivationSemantics.Material.Flexibility);
        Shell.semantics = DescribeIngredient(
            CultivationSemantics.Material.Hardness,
            CultivationSemantics.Effect.Ward,
            CultivationSemantics.Material.Stability);
        Silk.semantics = DescribeIngredient(
            CultivationSemantics.Material.Flexibility,
            CultivationSemantics.Effect.Binding,
            CultivationSemantics.Effect.Concealment);
        Crystal.semantics = DescribeIngredient(
            CultivationSemantics.Resource.Spirituality,
            CultivationSemantics.Effect.Resonance,
            CultivationSemantics.Material.Brittle,
            CultivationSemantics.Resource.Reserve);
        Stone.semantics = DescribeIngredient(
            CultivationSemantics.Material.Hardness,
            CultivationSemantics.Material.Immoveable,
            CultivationSemantics.Material.Stability,
            CultivationSemantics.Material.Brittle);
        Liquid.semantics = DescribeIngredient(
            CultivationSemantics.Effect.Transformation,
            CultivationSemantics.Craft.Alchemy,
            CultivationSemantics.Resource.Reserve,
            CultivationSemantics.Material.Volatility);
        Bamboo.semantics = DescribeIngredient(
            CultivationSemantics.Material.Flexibility,
            CultivationSemantics.Material.Lightweight,
            CultivationSemantics.Material.Stability);
        Flower.semantics = DescribeIngredient(
            CultivationSemantics.Effect.Purification,
            CultivationSemantics.Resource.Vitality,
            CultivationSemantics.Craft.Alchemy);
        Fruit.semantics = DescribeIngredient(
            CultivationSemantics.Resource.Vitality,
            CultivationSemantics.Resource.Reserve,
            CultivationSemantics.Craft.Alchemy);
        Herb.semantics = DescribeIngredient(
            CultivationSemantics.Craft.Alchemy,
            CultivationSemantics.Effect.Recovery,
            CultivationSemantics.Resource.Vitality);
        Lotus.semantics = DescribeIngredient(
            CultivationSemantics.Effect.Purification,
            CultivationSemantics.Resource.Spirituality,
            CultivationSemantics.Craft.Alchemy);
        Mushroom.semantics = DescribeIngredient(
            CultivationSemantics.Effect.Transformation,
            CultivationSemantics.Material.Volatility,
            CultivationSemantics.Craft.Alchemy);
        Root.semantics = DescribeIngredient(
            CultivationSemantics.Resource.Vitality,
            CultivationSemantics.Material.Stability,
            CultivationSemantics.Effect.Binding);
        Vine.semantics = DescribeIngredient(
            CultivationSemantics.Material.Flexibility,
            CultivationSemantics.Effect.Binding,
            CultivationSemantics.Resource.Vitality);
        Wood.semantics = DescribeIngredient(
            CultivationSemantics.Material.Stability,
            CultivationSemantics.Resource.Vitality,
            CultivationSemantics.Material.Flexibility);
        ElementRoot.semantics = DescribeIngredient(
            CultivationSemantics.Trait.ElementRoot,
            CultivationSemantics.Theme.Elemental,
            CultivationSemantics.Resource.Spirituality);
        Ball.semantics = DescribeIngredient(
            CultivationSemantics.Resource.Reserve,
            CultivationSemantics.Resource.Spirituality,
            CultivationSemantics.Material.Stability,
            CultivationSemantics.Effect.Resonance);
    }

    /// <summary>
    /// 按从主特征到次要特征的顺序生成材料形态语义。
    /// </summary>
    private static SemanticDescriptor DescribeIngredient(params SemanticAsset[] semantics)
    {
        var contributions = new SemanticContribution[semantics.Length];
        for (var i = 0; i < semantics.Length; i++)
        {
            var strength = i switch
            {
                0 => 1f,
                1 => 0.65f,
                2 => 0.35f,
                _ => 0.25f
            };
            contributions[i] = new SemanticContribution(semantics[i], strength);
        }
        return SemanticDescriptor.Weighted(contributions);
    }
}
