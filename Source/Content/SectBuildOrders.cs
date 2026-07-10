using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Core.Libraries;
using NeoModLoader.api.attributes;

namespace Cultiway.Content;

[Dependency(typeof(Buildings))]
public class SectBuildOrders : ExtendLibrary<SectBuildOrderAsset, SectBuildOrders>
{
    public static SectBuildOrderAsset Classic { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Sect.BuildOrder";

    protected override void OnInit()
    {
        InitClassic();
    }

    private static void InitClassic()
    {
        Classic.AddBuilding(Buildings.SectHall, maxPerSect: 1, requiredResidenceZones: 1);

        SectBuildOrder scripturePavilion = Classic.AddBuilding(
            Buildings.SectScripturePavilion,
            maxPerSect: 1,
            requiredMembers: 3,
            requiredResidenceZones: 1);
        scripturePavilion.requirementsTypes = [SectConst.BuildingTypeHall];

        SectBuildOrder treasurePavilion = Classic.AddBuilding(
            Buildings.SectTreasurePavilion,
            maxPerSect: 1,
            requiredMembers: 5,
            requiredResidenceZones: 1);
        treasurePavilion.requirementsTypes = [SectConst.BuildingTypeHall];
    }
}
