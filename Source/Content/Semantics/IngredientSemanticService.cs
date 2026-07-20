using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Semantics;

/// <summary>
/// 从材料实体的固有组件和来源资产声明即时构建语义档案，不持有独立缓存或存档数据。
/// </summary>
public static class IngredientSemanticService
{
    private const string ShapeContributorId = "content.ingredient.shape";
    private const string SourceSpeciesContributorId = "content.ingredient.source_species";
    private const string ElementRootContributorId = "content.ingredient.element_root";
    private const string XianBaseContributorId = "content.ingredient.xian_base";
    private const string JindanContributorId = "content.ingredient.jindan";
    private const string QualityContributorId = "content.ingredient.quality";

    /// <summary>
    /// 构建材料当前的语义档案。无效、已回收或缺少形态的实体返回空档案。
    /// </summary>
    public static SemanticProfile Build(Entity ingredient)
    {
        var builder = new SemanticProfileBuilder(ModClass.L.SemanticLibrary);
        if (ingredient.IsNull ||
            ingredient.Tags.Has<TagRecycle>() ||
            !ingredient.Tags.Has<TagIngredient>() ||
            !ingredient.TryGetComponent(out ItemShape itemShape))
        {
            return builder.Build();
        }

        AddShape(builder, ingredient, itemShape);
        AddSourceSpecies(builder, ingredient);
        AddElementRoot(builder, ingredient);
        AddXianBase(builder, ingredient);
        AddJindan(builder, ingredient);
        AddQuality(builder, ingredient);
        return builder.Build();
    }

    private static void AddShape(SemanticProfileBuilder builder, Entity ingredient, ItemShape itemShape)
    {
        var shape = itemShape.Type;
        if (shape == null) return;

        builder.Add(shape.semantics, 1f, SemanticScope.Intrinsic,
            new SemanticSourceRef(ShapeContributorId, ingredient, shape.id));
    }

    private static void AddSourceSpecies(SemanticProfileBuilder builder, Entity ingredient)
    {
        if (!ingredient.TryGetComponent(out ItemCreation creation) ||
            string.IsNullOrEmpty(creation.creator_asset_id)) return;

        var sourceAsset = AssetManager.actor_library.get(creation.creator_asset_id);
        if (sourceAsset == null) return;

        builder.Add(sourceAsset.GetExtend<ActorAssetExtend>().semantics, 1f, SemanticScope.Historical,
            new SemanticSourceRef(SourceSpeciesContributorId, ingredient, sourceAsset.id));
    }

    private static void AddElementRoot(SemanticProfileBuilder builder, Entity ingredient)
    {
        if (!ingredient.TryGetComponent(out ElementRoot root)) return;

        var rootType = root.Type;
        var source = new SemanticSourceRef(ElementRootContributorId, ingredient, rootType?.id);
        var multiplier = 1f + Mathf.Log(1f + root.GetStrength(), 2f) * 0.25f;
        if (rootType != null) builder.Add(rootType.Semantics, multiplier, SemanticScope.Intrinsic, source);
        SemanticContributorTools.AddElements(builder,
            new ElementComposition(root.Iron, root.Wood, root.Water, root.Fire, root.Earth, root.Neg, root.Pos,
                root.Entropy),
            multiplier, SemanticScope.Intrinsic, source);
    }

    private static void AddXianBase(SemanticProfileBuilder builder, Entity ingredient)
    {
        if (!ingredient.TryGetComponent(out XianBase xianBase)) return;

        var source = new SemanticSourceRef(XianBaseContributorId, ingredient);
        var elementTotal = Mathf.Max(0f, xianBase.iron)
                           + Mathf.Max(0f, xianBase.wood)
                           + Mathf.Max(0f, xianBase.water)
                           + Mathf.Max(0f, xianBase.fire)
                           + Mathf.Max(0f, xianBase.earth);
        if (elementTotal > 0f)
        {
            SemanticContributorTools.AddElements(builder,
                new ElementComposition(xianBase.iron, xianBase.wood, xianBase.water, xianBase.fire,
                    xianBase.earth),
                1f, SemanticScope.Intrinsic, source);
        }

        var jing = Mathf.Max(0f, xianBase.jing);
        var qi = Mathf.Max(0f, xianBase.qi);
        var shen = Mathf.Max(0f, xianBase.shen);
        var threeHuaTotal = jing + qi + shen;
        if (threeHuaTotal <= 0f) return;

        builder.Add(CultivationSemantics.Resource.Vitality, (jing + qi) / threeHuaTotal,
            SemanticScope.Intrinsic, source);
        builder.Add(CultivationSemantics.Resource.Spirituality, shen / threeHuaTotal,
            SemanticScope.Intrinsic, source);
    }

    private static void AddJindan(SemanticProfileBuilder builder, Entity ingredient)
    {
        if (!ingredient.TryGetComponent(out Jindan jindan)) return;

        var multiplier = 1f + Mathf.Log(1f + Mathf.Max(0f, jindan.strength), 2f) * 0.25f
                         + jindan.stage * 0.2f;
        builder.Add(SemanticDescriptor.Weighted(jindan.formation.semantics), multiplier,
            SemanticScope.Intrinsic,
            new SemanticSourceRef(JindanContributorId, ingredient, jindan.formation.signature));
    }

    private static void AddQuality(SemanticProfileBuilder builder, Entity ingredient)
    {
        if (!ingredient.TryGetComponent(out ItemLevel level)) return;

        builder.Add(CultivationSemantics.Material.Quality, (int)level / 9f,
            SemanticScope.Intrinsic, new SemanticSourceRef(QualityContributorId, ingredient));
    }
}
