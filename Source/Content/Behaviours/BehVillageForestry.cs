using ai.behaviours;
using Cultiway.Content.Railways;
using Cultiway.Content.VillageDuties;

namespace Cultiway.Content.Behaviours;

/// <summary>在本村附近寻找补种位置，每次只检查有限个地块。</summary>
public sealed class BehFindVillageTreeSite : BehaviourActionActor
{
    /// <summary>补种只占用安全空地，不进入道路、农田或铁路预留范围。</summary>
    /// <param name="tile">准备补种树木的地块。</param>
    /// <param name="actor">负责林业的人物。</param>
    internal static bool CanPlant(WorldTile tile, Actor actor) => tile != null && tile.zone?.city == actor.city &&
        tile.Type.ground && tile.Type.biome_asset?.grow_vegetation_auto == true &&
        tile.Type.biome_asset.grow_type_selector_trees != null && !tile.hasBuilding() && !tile.isTemporaryFrozen() &&
        !tile.Type.road && !tile.Type.farm_field && !tile.Type.block && tile.canGrow() &&
        tile.isSameIsland(actor.current_tile) && !RailTrackRoutePlanner.IsReservedForTrack(tile) && !FamilyFarming.FamilyFarmService.IsReserved(tile);

    /// <inheritdoc />
    public override BehResult execute(Actor actor)
    {
        if (actor.citizen_job != CitizenJobLibrary.woodcutter ||
            !VillageOccupationService.CanTakeJob(actor, CitizenJobLibrary.woodcutter) ||
            VillageDutyService.Classify(actor) != VillageResidentKind.Worker) return BehResult.Stop;
        for (int i = 0; i < 32; i++)
        {
            var tile = World.world.GetTile(actor.current_tile.x + Randy.randomInt(-8, 8), actor.current_tile.y + Randy.randomInt(-8, 8));
            if (!CanPlant(tile, actor)) continue;
            actor.beh_tile_target = tile;
            return BehResult.Continue;
        }
        return BehResult.Stop;
    }
}

/// <summary>走到选定地块后，按原版生态与密度限制补种一棵树。</summary>
public sealed class BehPlantVillageTree : BehaviourActionActor
{
    /// <inheritdoc />
    public override void setupErrorChecks() { base.setupErrorChecks(); null_check_tile_target = true; }

    /// <inheritdoc />
    public override BehResult execute(Actor actor)
    {
        var tile = actor.beh_tile_target;
        if (!VillageOccupationService.CanTakeJob(actor, CitizenJobLibrary.woodcutter) ||
            !BehFindVillageTreeSite.CanPlant(tile, actor) || Toolbox.SquaredDistTile(actor.current_tile, tile) > 2)
            return BehResult.Stop;
        actor.punchTargetAnimation(tile.posV3);
        BuildingActions.tryGrowVegetationRandom(tile, VegetationType.Trees, pCheckLimit: true, pCheckRandom: false);
        return BehResult.Continue;
    }
}
