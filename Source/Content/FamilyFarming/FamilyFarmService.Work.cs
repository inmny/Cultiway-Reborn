using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.VillageDuties;
using UnityEngine;
using Cultiway.Content.KingdomPolicies;
using Cultiway.Content.Railways;

namespace Cultiway.Content.FamilyFarming;

/// <summary>农民在田间、仓库和风车间实际移动和劳动，不在调度器里直接生成成品粮食。</summary>
public static partial class FamilyFarmService
{
    /// <summary>本轮农活，只在人物执行完动作后结算。</summary>
    internal enum FarmAction
    {
        /// <summary>清理允许移除的植被。</summary>
        Clear,
        /// <summary>把预留土地翻成田地。</summary>
        Till,
        /// <summary>在对应农时播种。</summary>
        Plant,
        /// <summary>收取成熟小麦。</summary>
        Harvest,
        /// <summary>从待加工仓位取原粮。</summary>
        CollectGrain,
        /// <summary>交付背包中的原粮。</summary>
        Deliver,
        /// <summary>在风车工位加工原粮。</summary>
        Mill,
        /// <summary>领取原版肥料。</summary>
        CollectFertilizer,
        /// <summary>对普通作物施肥。</summary>
        Fertilize
    }

    /// <summary>人物已经认领的一次动作和目标。</summary>
    internal sealed class FarmWork
    {
        /// <summary>领取时的村庄，防止搬迁后向旧村庄结算。</summary>
        internal long CityId;
        /// <summary>领取时的目标家庭，收割帮工不会改变土地归属。</summary>
        internal long Owner;
        /// <summary>这次执行的动作。</summary>
        internal FarmAction Action;
        /// <summary>动作地格。</summary>
        internal int Cell;
        /// <summary>需要保持有效的作物或仓库编号。</summary>
        internal long BuildingId;
    }

    /// <summary>每名人物至多认领一次动作，结束工作时释放。</summary>
    private static readonly Dictionary<long, FarmWork> works = new();

    /// <summary>结束、换工作或死亡时归还地格和加工工位。</summary>
    /// <param name="actor">持有农活目标的人物。</param>
    public static void Release(Actor actor)
    {
        if (!works.TryGetValue(actor.id, out var work)) return;
        works.Remove(actor.id);
        var tile = Tile(work.Cell);
        if (tile?.isTargetedBy(actor) == true) tile.cleanTargetedBy();
        if (actor.tile_target == tile) actor.clearTileTarget();
    }

    /// <summary>优先运走背包原粮，缺粮时转加工，再收割、补种和开新田。</summary>
    /// <param name="actor">已经自主或经村庄安排领取农业职业的人物。</param>
    public static bool Prepare(Actor actor)
    {
        // 先核对作息与农时，再安排收粮和加工，最后选择田间工作。
        Release(actor);
        if (!VillageDutyService.IsResident(actor)) return false;
        bool carrying = actor.inventory.getResource(RawGrainId) > 0 || actor.inventory.getResource("wheat") > 0;
        if (!actor.canWork() && !carrying) return false;
        bool regular = VillageDutyService.CanTakeCivilianJob(actor);
        if (!regular && !CanSeasonalWork(actor)) return false;
        var state = State(actor.city); long owner = OwnerOf(actor);
        int grain = actor.inventory.getResource(RawGrainId) + actor.inventory.getResource("wheat");
        if (IsPrivate(actor.city) && actor.canWork() && grain is > 0 and < 12)
        {
            var nearbyHarvest = FindFieldTarget(actor, FarmAction.Harvest);
            if (nearbyHarvest != null) return Claim(actor, nearbyHarvest, FarmAction.Harvest, nearbyHarvest.building);
        }
        if (carrying)
        {
            var storage = FindGrainStorage(actor);
            if (storage == null) { state.Reasons[owner] = "WaitingMill"; return false; }
            return Claim(actor, storage.current_tile, FarmAction.Deliver, storage);
        }
        if (!IsPrivate(actor.city)) return regular && TryMilling(actor);
        bool shortage = actor.city.getTotalFood() < Math.Max(40, actor.city.status.population * 2);
        if (regular && shortage && TryMilling(actor)) return true;
        var tile = FindFieldTarget(actor, FarmAction.Harvest);
        if (tile != null) return Claim(actor, tile, FarmAction.Harvest, tile.building);
        if (regular && TryMilling(actor)) return true;
        tile = FindFieldTarget(actor, FarmAction.Plant);
        if (tile != null) return Claim(actor, tile, tile.hasBuilding() ? FarmAction.Clear : FarmAction.Plant, tile.building);
        if (!regular) return false;
        tile = FindFieldTarget(actor, FarmAction.Fertilize);
        if (tile != null)
        {
            if (actor.inventory.getResource("fertilizer") > 0) return Claim(actor, tile, FarmAction.Fertilize, tile.building);
            var storage = actor.city.storages.Where(b => UsableStorage(b, actor) && b.getResourcesAmount("fertilizer") > 0)
                .OrderBy(b => Toolbox.SquaredDistTile(actor.current_tile, b.current_tile)).FirstOrDefault();
            if (storage != null) return Claim(actor, storage.current_tile, FarmAction.CollectFertilizer, storage);
        }
        tile = FindFieldTarget(actor, FarmAction.Till);
        if (tile != null) return Claim(actor, tile, tile.hasBuilding() ? FarmAction.Clear : FarmAction.Till, tile.building);
        state.Reasons[owner] = actor.city.hasBuildingType("type_windmill") ? "WaitingGrowth" : "WaitingMill";
        return false;
    }

    /// <summary>开垦和播种只选本户田块；收割先顾本户，再帮助同村家庭和退耕田收尾。</summary>
    /// <param name="actor">寻找实际劳动目标的人物。</param>
    /// <param name="action">要找的开垦、播种、施肥或收割目标。</param>
    internal static WorldTile FindFieldTarget(Actor actor, FarmAction action)
    {
        if (!VillageDutyService.IsResident(actor)) return null;
        if (!IsPrivate(actor.city)) return action is FarmAction.Harvest or FarmAction.Fertilize ? FindPublicTarget(actor, action) : null;
        bool regular = VillageDutyService.CanTakeCivilianJob(actor);
        if (!regular && (!KingdomPolicyService.Has(actor.city, KingdomPolicyCard.SeasonalFarming) ||
            action is not (FarmAction.Plant or FarmAction.Harvest))) return null;
        var state = State(actor.city); long owner = OwnerOf(actor);
        WorldTile best = null; int distance = int.MaxValue, priority = int.MaxValue;
        if (action == FarmAction.Harvest && KingdomPolicyService.Has(actor.city, KingdomPolicyCard.SeasonalFarming) &&
            !FarmSeasonRules.HarvestMonth(Date.getCurrentMonth()) && state.Data.Retiring.Count == 0) return null;
        var sites = Sites(state);
        var cells = state.Data.Plots.Where(p => action == FarmAction.Harvest || p.Owner == owner && sites.Contains(p)).SelectMany(p => p.Cells());
        if (action == FarmAction.Harvest) cells = cells.Concat(state.Data.Retiring.Keys);
        foreach (int cell in cells)
        {
            var tile = Tile(cell);
            if (!tile.isSameIsland(actor.current_tile) || tile.isTargeted() && !tile.isTargetedBy(actor)) continue;
            bool retired = state.Data.Retiring.TryGetValue(cell, out long finalCrop);
            var crop = tile.building;
            bool ripe = crop?.asset.wheat == true && crop.isUsable() && crop.component_wheat.isMaxLevel();
            bool match = action switch {
                FarmAction.Harvest => ripe && (retired ? finalCrop == crop.id : FamilyCropCalendar.Ready(crop)),
                FarmAction.Plant => !retired && (!KingdomPolicyService.Has(actor.city, KingdomPolicyCard.SeasonalFarming) ||
                    FarmSeasonRules.SowingSeason(Date.getCurrentMonth()) != 0) && tile.Type.farm_field &&
                    (!tile.hasBuilding() || regular && !tile.building.asset.wheat && tile.building.canRemoveForFarms()),
                FarmAction.Till => !retired && !tile.Type.farm_field,
                FarmAction.Fertilize => !retired && FamilyCropFertilizer.CanFertilize(crop),
                _ => false };
            if (!match || tile.zone?.city != actor.city || !retired && !CanFarm(state, tile)) continue;
            int rank = retired || state.Land[cell].Owner == owner ? 0 : 1;
            int d = Toolbox.SquaredDistTile(actor.current_tile, tile);
            if (rank > priority || rank == priority && d >= distance) continue;
            priority = rank; distance = d; best = tile;
        }
        return best;
    }

    /// <summary>只向已完成、同村同岛的原版仓储交付材料。</summary>
    /// <param name="building">候选仓库或风车。</param>
    /// <param name="actor">实际搬运的人物。</param>
    private static bool UsableStorage(Building building, Actor actor) => building != null && building.isUsable() &&
        !building.isUnderConstruction() && building.asset.storage && building.city == actor.city &&
        building.current_tile.isSameIsland(actor.current_tile);

    /// <summary>原粮优先送有空间的风车，没有风车时存进普通待加工仓位。</summary>
    /// <param name="actor">背包中持有原粮的人物。</param>
    public static Building FindGrainStorage(Actor actor)
    {
        bool raw = actor.inventory.getResource(RawGrainId) > 0;
        var resource = raw ? FamilyFarmContent.RawGrain : ResourceLibrary.wheat;
        return actor.city.storages.Where(b => UsableStorage(b, actor) &&
            (!raw || b.asset.type == "type_windmill" || !b.asset.storage_only_food) && b.hasSpaceForResource(resource))
            .OrderBy(b => raw && b.asset.type == "type_windmill" ? 0 : 1)
            .ThenBy(b => Toolbox.SquaredDistTile(actor.current_tile, b.current_tile)).FirstOrDefault();
    }

    /// <summary>风车每处两个实际工位，已满时让其余人员继续农活。</summary>
    /// <param name="actor">准备参与加工或搬运原粮的农民。</param>
    private static bool TryMilling(Actor actor)
    {
        var mills = actor.city.storages.Where(b => UsableStorage(b, actor) && b.asset.type == "type_windmill" &&
            b.hasSpaceForResource(ResourceLibrary.wheat)).OrderBy(b => Toolbox.SquaredDistTile(actor.current_tile, b.current_tile));
        foreach (var mill in mills)
        {
            if (mill.getResourcesAmount(RawGrainId) > 0 && works.Values.Count(w => w.Action == FarmAction.Mill && w.BuildingId == mill.id) < 2)
                return Claim(actor, mill.current_tile, FarmAction.Mill, mill);
            if (!mill.hasSpaceForResource(FamilyFarmContent.RawGrain)) continue;
            if (works.Values.Count(w => w.CityId == actor.city.id && w.Action == FarmAction.CollectGrain) >= 2) continue;
            var source = actor.city.storages.Where(b => b != mill && UsableStorage(b, actor) && b.getResourcesAmount(RawGrainId) >
                    works.Values.Count(w => w.Action == FarmAction.CollectGrain && w.BuildingId == b.id) * 8)
                .OrderBy(b => Toolbox.SquaredDistTile(actor.current_tile, b.current_tile)).FirstOrDefault();
            if (source != null) return Claim(actor, source.current_tile, FarmAction.CollectGrain, source);
        }
        return false;
    }

    /// <summary>保留目标和真实建筑编号，农田使用原版地格认领。</summary>
    /// <param name="actor">领取一次农活的人物。</param>
    /// <param name="tile">本轮目标。</param>
    /// <param name="action">本轮动作。</param>
    /// <param name="building">需在结算时再次确认的目标建筑。</param>
    private static bool Claim(Actor actor, WorldTile tile, FarmAction action, Building building)
    {
        var state = State(actor.city);
        long owner = action == FarmAction.Harvest && state.Land.TryGetValue(Cell(tile), out var plot) ? plot.Owner : OwnerOf(actor);
        works.Add(actor.id, new FarmWork { CityId = actor.city.id, Owner = owner, Action = action,
            Cell = Cell(tile), BuildingId = building?.id ?? 0 });
        actor.beh_tile_target = tile; actor.beh_building_target = building;
        if (action is FarmAction.Clear or FarmAction.Till or FarmAction.Plant or FarmAction.Harvest or FarmAction.Fertilize)
            actor.setTileTarget(tile);
        State(actor.city).Reasons.Remove(OwnerOf(actor)); return true;
    }

    /// <summary>移动失败时归还目标，并显示可达性原因。</summary>
    /// <param name="actor">未能到达目标的人物。</param>
    public static void CannotReach(Actor actor)
    { if (actor.hasCity()) State(actor.city).Reasons[OwnerOf(actor)] = "NoPath"; Release(actor); }

    /// <summary>动作结束后一次性结算；先移除本轮认领，重复回调不能再次产粮。</summary>
    /// <param name="actor">已经实际走到目标并完成劳动动作的人物。</param>
    public static bool Complete(Actor actor)
    {
        // 先移除一次性认领，再核对人物与目标，最后只在对应分支结算一次材料。
        if (!works.TryGetValue(actor.id, out var work)) return false;
        Release(actor);
        if (actor.city?.id != work.CityId || !actor.isAlive() || !VillageDutyService.CanChangeAssignment(actor)) return false;
        var tile = Tile(work.Cell); var state = State(actor.city);
        if (tile.zone?.city != actor.city || Toolbox.SquaredDistTile(actor.current_tile, tile) > 4) return false;
        bool finishing = work.Action is FarmAction.Mill or FarmAction.Deliver;
        if (!finishing && !VillageDutyService.CanTakeCivilianJob(actor) &&
            (!KingdomPolicyService.Has(actor.city, KingdomPolicyCard.SeasonalFarming) || work.Action is not (FarmAction.Plant or FarmAction.Harvest))) return false;
        var building = tile.building;
        // 人物走动期间可能换家庭、退耕或出现新建筑，实际落地前重新检查。
        if (work.BuildingId != 0 && (building?.id != work.BuildingId || !building.isUsable())) return false;
        bool field = work.Action is FarmAction.Clear or FarmAction.Till or FarmAction.Plant or FarmAction.Fertilize;
        if (field && (OwnerOf(actor) != work.Owner || !state.Land.TryGetValue(work.Cell, out var plot) ||
            plot.Owner != work.Owner || !Sites(state).Contains(plot) || !CanFarm(state, tile))) return false;
        switch (work.Action)
        {
            case FarmAction.Clear:
                if (building == null || !building.canRemoveForFarms() || building.asset.wheat) return false;
                building.startDestroyBuilding(); break;
            case FarmAction.Till:
                if (tile.hasBuilding() || tile.Type.farm_field || !tile.Type.can_be_farm) return false;
                MapAction.terraformTop(tile, TopTileLibrary.field); actor.addLoot(SimGlobals.m.coins_for_field); break;
            case FarmAction.Plant:
                if (tile.hasBuilding() || !tile.Type.farm_field || KingdomPolicyService.Has(actor.city, KingdomPolicyCard.SeasonalFarming) &&
                    FarmSeasonRules.SowingSeason(Date.getCurrentMonth()) == 0) return false;
                FamilyCropCalendar.Register(World.world.buildings.addBuilding("wheat", tile)); actor.addLoot(SimGlobals.m.coins_for_planting); break;
            case FarmAction.Harvest:
                if (building?.asset.wheat != true || !building.component_wheat.isMaxLevel()) return false;
                bool retired = state.Data.Retiring.TryGetValue(work.Cell, out long finalCrop);
                if (!retired && !FamilyCropCalendar.Ready(building)) return false;
                if (retired ? finalCrop != building.id : !CanFarm(state, tile) ||
                    !state.Land.TryGetValue(work.Cell, out var owned) || owned.Owner != work.Owner) return false;
                HarvestGrain(actor, building);
                if (retired) CleanRetired(state, work.Cell);
                break;
            case FarmAction.Fertilize:
                if (!FamilyCropFertilizer.Apply(actor, building)) return false;
                break;
            case FarmAction.CollectFertilizer:
            case FarmAction.CollectGrain:
                if (!UsableStorage(building, actor)) return false;
                string resource = work.Action == FarmAction.CollectGrain ? RawGrainId : "fertilizer";
                int amount = Math.Min(work.Action == FarmAction.CollectGrain ? 8 : 1, building.getResourcesAmount(resource));
                if (amount <= 0) return false;
                building.takeResource(resource, amount); actor.addToInventory(resource, amount); break;
            case FarmAction.Deliver:
                string delivered = actor.inventory.getResource(RawGrainId) > 0 ? RawGrainId : "wheat";
                if (!UsableStorage(building, actor) || delivered == RawGrainId && building.asset.storage_only_food && building.asset.type != "type_windmill") return false;
                int carried = Math.Min(actor.inventory.getResource(delivered), AssetManager.resources.get(delivered).storage_max - building.getResourcesAmount(delivered));
                if (carried <= 0) return false;
                building.addResources(delivered, carried); actor.takeFromInventory(delivered, carried); break;
            case FarmAction.Mill:
                if (!UsableStorage(building, actor) || building.asset.type != "type_windmill") return false;
                int batch = Math.Min(4, Math.Min(building.getResourcesAmount(RawGrainId), ResourceLibrary.wheat.storage_max - building.getResourcesAmount("wheat")));
                if (batch <= 0) return false;
                building.takeResource(RawGrainId, batch); building.addResources("wheat", batch); break;
        }
        actor.city._storage_version++; return true;
    }

    /// <summary>向界面提供实际任务，未领取农活时显示空状态。</summary>
    /// <param name="actorId">当前人物编号。</param>
    public static string CurrentAction(long actorId) => works.TryGetValue(actorId, out var work) ? work.Action.ToString() :
        VillageOccupationService.TemporaryJob(actorId) != null ? "Supporting" : "Idle";

    /// <summary>当前村庄已经认领的风车加工工位数。</summary>
    /// <param name="city">需要展示加工状态的村庄。</param>
    public static int MillingWorkers(City city) => works.Values.Count(w => w.CityId == city.id && w.Action == FarmAction.Mill);

    /// <summary>供村庄在农时召回普通农民，已被他人认领的作物不重复派人。</summary>
    /// <param name="actor">具有农民职业归属的人物。</param>
    public static bool HasHarvest(Actor actor) => FindFieldTarget(actor, FarmAction.Harvest) != null;

    /// <summary>实际农民可在休息月执行当季播收和本次收粮收尾，不开放其他农活。</summary>
    /// <param name="actor">准备响应农时的人物。</param>
    public static bool CanSeasonalWork(Actor actor)
    {
        if (!VillageDutyService.IsResident(actor) || actor.isWarrior()) return false;
        if (actor.inventory.getResource(RawGrainId) > 0 || actor.inventory.getResource("wheat") > 0) return true;
        if (!actor.canWork() || !KingdomPolicyService.Has(actor.city, KingdomPolicyCard.SeasonalFarming)) return false;
        bool farmer = actor.citizen_job == CitizenJobLibrary.farmer ||
            VillageMilitary.VillageMilitaryService.Get(actor.city).Occupations.TryGetValue(actor.id, out string role) && role == "farmer";
        if (!farmer) return false;
        if (!IsPrivate(actor.city)) return FindPublicTarget(actor, FarmAction.Harvest) != null ||
            FarmSeasonRules.SowingSeason(Date.getCurrentMonth()) != 0 && actor.city.calculated_farm_fields.Any(t =>
                !t.hasBuilding() && t.zone?.city == actor.city && t.isSameIsland(actor.current_tile));
        return actor.inventory.getResource(RawGrainId) > 0 && FarmSeasonRules.HarvestMonth(Date.getCurrentMonth()) ||
            FindFieldTarget(actor, FarmAction.Harvest) != null || FindFieldTarget(actor, FarmAction.Plant) != null;
    }

    /// <summary>有小麦等待生长且没有可执行农活时，保留农籍临时支援其他职业。</summary>
    /// <param name="actor">保留家庭田地的普通农民。</param>
    public static bool CanSupportOtherWork(Actor actor) => IsPrivate(actor.city) && VillageDutyService.IsResident(actor) &&
        VillageDutyService.CanTakeCivilianJob(actor) && VillageDutyService.Classify(actor) == VillageResidentKind.Worker &&
        actor.inventory.getResource(RawGrainId) == 0 && !MillingAvailable(actor) &&
        State(actor.city).Data.Plots.Where(p => p.Owner == OwnerOf(actor)).SelectMany(p => p.Cells())
            .Any(c => Tile(c).building?.asset.wheat == true) &&
        FindFieldTarget(actor, FarmAction.Harvest) == null && FindFieldTarget(actor, FarmAction.Plant) == null &&
        FindFieldTarget(actor, FarmAction.Till) == null &&
        ((actor.inventory.getResource("fertilizer") == 0 && !actor.city.storages.Any(b =>
                UsableStorage(b, actor) && b.getResourcesAmount("fertilizer") > 0)) ||
            FindFieldTarget(actor, FarmAction.Fertilize) == null);

    /// <summary>存在可处理的原粮和空闲风车时优先回农业，不把加工人员全部借走。</summary>
    /// <param name="actor">准备支援其他工作的农民。</param>
    private static bool MillingAvailable(Actor actor) => actor.city.getResourcesAmount(RawGrainId) > 0 &&
        actor.city.storages.Any(b => UsableStorage(b, actor) && b.asset.type == "type_windmill" &&
            b.hasSpaceForResource(ResourceLibrary.wheat) && works.Values.Count(w => w.Action == FarmAction.Mill && w.BuildingId == b.id) < 2);

    /// <summary>原版直接调用农活动作时也要服从家庭归属、作息和内城限制。</summary>
    /// <param name="actor">执行原版农活的人物。</param>
    /// <param name="tile">实际准备处理的地格。</param>
    /// <param name="harvest">是否允许收取退耕成熟作物。</param>
    internal static bool CanUseTarget(Actor actor, WorldTile tile, bool harvest = false)
    {
        if (!VillageDutyService.IsResident(actor) || tile == null || tile.zone?.city != actor.city ||
            !VillageDutyService.CanTakeCivilianJob(actor) && !CanSeasonalWork(actor)) return false;
        var state = State(actor.city); int cell = Cell(tile);
        if (harvest && state.Data.Retiring.TryGetValue(cell, out long crop)) return tile.building?.id == crop && tile.building.component_wheat.isMaxLevel();
        if (!IsPrivate(actor.city)) return !RailTrackRoutePlanner.IsReservedForTrack(tile) &&
            (tile.Type.farm_field || tile.Type.can_be_farm) && !tile.Type.road && !tile.Type.block &&
            (harvest ? FamilyCropCalendar.Ready(tile.building) : !tile.hasBuilding() || tile.building.asset.wheat || tile.building.canRemoveForFarms());
        return CanFarm(state, tile) && state.Land.TryGetValue(cell, out var plot) &&
            (harvest ? FamilyCropCalendar.Ready(tile.building) : plot.Owner == OwnerOf(actor) && Sites(state).Contains(plot));
    }
}
