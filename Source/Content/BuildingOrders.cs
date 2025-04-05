using Cultiway.Abstract;

namespace Cultiway.Content;

public class BuildingOrders : ExtendLibrary<CityBuildOrderAsset, BuildingOrders>
{
    public static CityBuildOrderAsset Classic { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        
        InitClassic();
        
    }

    private void InitClassic()
    {
        t = Classic;
        addBuilding(S_BuildOrder.order_bonfire, 1);
        addBuilding(S_BuildOrder.order_stockpile, 1);
        addBuilding(S_BuildOrder.order_house_0, pCheckHouseLimit: true);
        b.requirements_orders = [S_BuildOrder.order_bonfire];
        addBuilding(S_BuildOrder.order_tent, pCheckHouseLimit: true);
        addUpgrade(S_BuildOrder.order_tent);
        b.requirements_orders = [S_BuildOrder.order_tent];
        addUpgrade(S_BuildOrder.order_house_0);
        b.requirements_orders = [S_BuildOrder.order_hall_0, S_BuildOrder.order_house_0];
        addUpgrade(S_BuildOrder.order_house_1);
        b.requirements_orders = [S_BuildOrder.order_hall_1, S_BuildOrder.order_house_1];
        addUpgrade(S_BuildOrder.order_house_2);
        b.requirements_orders = [S_BuildOrder.order_hall_1, S_BuildOrder.order_house_2];
        addUpgrade(S_BuildOrder.order_house_3);
        b.requirements_orders = [S_BuildOrder.order_hall_2, S_BuildOrder.order_house_3];
        addUpgrade(S_BuildOrder.order_house_4);
        b.requirements_orders = [S_BuildOrder.order_hall_2, S_BuildOrder.order_house_4];
        
        addBuilding(S_BuildOrder.order_hall_0, 1);
        b.requirements_orders = [S_BuildOrder.order_bonfire];
        addUpgrade(S_BuildOrder.order_hall_0,0, 30, 8);
        b.requirements_orders = [S_BuildOrder.order_house_1];
        addUpgrade(S_BuildOrder.order_hall_1,0, 100, 20);
        b.requirements_orders = [S_BuildOrder.order_statue, S_BuildOrder.order_mine, S_BuildOrder.order_barracks];
        
        
        addBuilding(S_BuildOrder.order_watch_tower, 1, 30, 10);
        b.requirements_orders = [S_BuildOrder.order_bonfire, S_BuildOrder.order_hall_0];
        addBuilding(S_BuildOrder.order_temple, 1, 90, 20, pMinZones: 20);
        b.requirements_orders = [S_BuildOrder.order_bonfire, S_BuildOrder.order_hall_1, S_BuildOrder.order_statue];
        addBuilding(S_BuildOrder.order_statue, 1, 70, 15);
        b.requirements_orders = [S_BuildOrder.order_hall_1];
        addBuilding(S_BuildOrder.order_well, 1, 20, 10);
        b.requirements_types = [S_BuildingType.type_hall];
        addBuilding(S_BuildOrder.order_mine, 1, 20, 10);
        b.requirements_orders = [S_BuildOrder.order_bonfire, S_BuildOrder.order_hall_0];
        
        addBuilding(S_BuildOrder.order_docks_0, 5, 0, 2);
        b.requirements_orders = [S_BuildOrder.order_bonfire];
        addUpgrade(S_BuildOrder.order_docks_0);
        b.requirements_orders = [S_BuildOrder.order_docks_0];

        addBuilding(S_BuildOrder.order_windmill_0, 1, 6, 5);
        b.requirements_orders = [S_BuildOrder.order_bonfire];
        addUpgrade(S_BuildOrder.order_windmill_0);

        addBuilding(S_BuildOrder.order_barracks, 1, 50, 16, pMinZones: 20);
        b.requirements_orders = [S_BuildOrder.order_hall_1];
        
        
        Classic.prepareForAssetGeneration();
    }
    private void addUpgrade(string pID, int pLimitType = 0, int pPop = 0, int pBuildings = 0, bool pCheckFullVillage = false, bool pZonesCheck = false, int pMinZones = 0)
    {
        addBuilding(pID, pLimitType, pPop, pBuildings, pCheckFullVillage, pZonesCheck, pMinZones).upgrade = true;
    }
    private BuildOrder addBuilding(string pID, int pLimitType = 0, int pPop = 0, int pBuildings = 0, bool pCheckFullVillage = false, bool pCheckHouseLimit = false, int pMinZones = 0)
    {
        BuildOrder order = new BuildOrder
        {
            id = pID,
            limit_type = pLimitType,
            required_pop = pPop,
            required_buildings = pBuildings,
            check_full_village = pCheckFullVillage,
            check_house_limit = pCheckHouseLimit,
            min_zones = pMinZones
        };
        t.list.Add(order);
        b = order;
        return order;
    }

    private BuildOrder b;
}