using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

/// <summary>
/// 游戏中特殊物品的通用分类集合。
/// </summary>
public class SpecialItemCategories : ExtendLibrary<SpecialItemCategoryAsset, SpecialItemCategories>
{
    public static SpecialItemCategoryAsset Elixir { get; private set; }
    public static SpecialItemCategoryAsset Talisman { get; private set; }
    public static SpecialItemCategoryAsset MagicScroll { get; private set; }
    public static SpecialItemCategoryAsset Ingredient { get; private set; }
    public static SpecialItemCategoryAsset Artifact { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SpecialItem.Category";

    protected override void OnInit()
    {
        Setup(
            Elixir,
            10,
            "cultiway/icons/iconElixirCauldron",
            2,
            1,
            0f,
            SpecialItemWithdrawalMode.Transfer,
            item => item.HasComponent<Elixir>());
        Setup(
            Talisman,
            20,
            "cultiway/icons/item_shapes/talisman/huofl",
            3,
            1,
            0f,
            SpecialItemWithdrawalMode.Transfer,
            item => item.HasComponent<Talisman>());
        Setup(
            MagicScroll,
            25,
            "cultiway/icons/item_shapes/magic_scroll/arcane",
            3,
            1,
            0f,
            SpecialItemWithdrawalMode.Transfer,
            item => item.HasComponent<MagicScroll>());
        Setup(
            Ingredient,
            40,
            "cultiway/icons/item_shapes/herb/三化草",
            1,
            1,
            0f,
            SpecialItemWithdrawalMode.Transfer,
            item => item.Tags.Has<TagIngredient>());
        Setup(
            Artifact,
            30,
            "cultiway/icons/item_shapes/artifact_shapes/sword/青锋剑",
            8,
            4,
            0.25f,
            SpecialItemWithdrawalMode.Loan,
            item => item.HasComponent<Artifact>());
    }

    private static void Setup(
        SpecialItemCategoryAsset asset,
        int order,
        string iconPath,
        int baseContributionCost,
        int storageWeight,
        float permissionCostMultiplier,
        SpecialItemWithdrawalMode withdrawalMode,
        System.Func<Friflo.Engine.ECS.Entity, bool> matches)
    {
        asset.nameKey = asset.id;
        asset.descriptionKey = $"{asset.id}.Info";
        asset.order = order;
        asset.iconPath = iconPath;
        asset.baseContributionCost = baseContributionCost;
        asset.storageWeight = storageWeight;
        asset.permissionCostMultiplier = permissionCostMultiplier;
        asset.withdrawalMode = withdrawalMode;
        asset.matches = matches;
    }
}
