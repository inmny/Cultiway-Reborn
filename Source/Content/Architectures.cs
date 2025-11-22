using Cultiway.Abstract;
using strings;

namespace Cultiway.Content;
[Dependency(typeof(BuildingOrders))]
public class Architectures : ExtendLibrary<ArchitectureAsset, Architectures>
{
    public static ArchitectureAsset Ming { get; private set; }
    public static ArchitectureAsset EasternHuman { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();

        Ming.styled_building_orders =
        [
            S_BuildOrder.order_tent, S_BuildOrder.order_house_0, S_BuildOrder.order_house_1, S_BuildOrder.order_house_2,
            S_BuildOrder.order_house_3, S_BuildOrder.order_house_4, S_BuildOrder.order_house_5,
            S_BuildOrder.order_hall_0, S_BuildOrder.order_hall_1, S_BuildOrder.order_hall_2,
            S_BuildOrder.order_windmill_0, S_BuildOrder.order_windmill_1,
            S_BuildOrder.order_watch_tower, S_BuildOrder.order_temple, S_BuildOrder.order_barracks, S_BuildOrder.order_bonfire,
            S_BuildOrder.order_docks_0, S_BuildOrder.order_docks_1
        ];
        Ming.shared_building_orders =
        [
            new(S_BuildOrder.order_statue, SB.statue),
            new(S_BuildOrder.order_well, SB.well),
            new(S_BuildOrder.order_stockpile, SB.stockpile),
            new(S_BuildOrder.order_mine, SB.mine),
            new(S_BuildOrder.order_training_dummy, SB.training_dummy)
        ];

        EasternHuman.styled_building_orders =
        [
            S_BuildOrder.order_tent, S_BuildOrder.order_house_0, S_BuildOrder.order_house_1, S_BuildOrder.order_house_2,
            S_BuildOrder.order_house_3, S_BuildOrder.order_house_4, S_BuildOrder.order_house_5,
            S_BuildOrder.order_hall_0, S_BuildOrder.order_hall_1, S_BuildOrder.order_hall_2,
            S_BuildOrder.order_windmill_0, S_BuildOrder.order_windmill_1,
            S_BuildOrder.order_watch_tower, S_BuildOrder.order_temple, S_BuildOrder.order_barracks, S_BuildOrder.order_bonfire,
            S_BuildOrder.order_docks_0, S_BuildOrder.order_docks_1
        ];
        EasternHuman.shared_building_orders =
        [
            new(S_BuildOrder.order_statue, SB.statue),
            new(S_BuildOrder.order_well, SB.well),
            new(S_BuildOrder.order_stockpile, SB.stockpile),
            new(S_BuildOrder.order_mine, SB.mine),
            new(S_BuildOrder.order_training_dummy, SB.training_dummy)
        ];
    }

    protected override void PostInit(ArchitectureAsset asset)
    {
        base.PostInit(asset);
        if (asset.isTemplateAsset()) return;
        AssetManager.architecture_library.loadAutoBuildingsForAsset(asset);
        foreach (var shared_building in asset.shared_building_orders)
        {
            asset.addBuildingOrderKey(shared_building.Item1, shared_building.Item2);
        }

        if (asset == Ming)
        {
            asset.addBuildingOrderKey(S_BuildOrder.order_bonfire, $"bonfire_{Ming.id}");
        }
    }
}