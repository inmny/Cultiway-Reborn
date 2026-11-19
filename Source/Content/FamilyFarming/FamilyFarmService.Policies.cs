using System;
using System.Linq;
using Cultiway.Content.KingdomPolicies;
using Cultiway.Content.Railways;
using Cultiway.Content.VillageDuties;

namespace Cultiway.Content.FamilyFarming;

/// <summary>公有原版农务与家庭农务共用原粮、农时和制度切换入口。</summary>
public static partial class FamilyFarmService
{
    /// <summary>当前村庄是否按家庭经营土地。</summary>
    /// <param name="city">农田所属村庄。</param>
    public static bool IsPrivate(City city) => KingdomPolicyService.Has(city, KingdomPolicyCard.PrivateFarms);

    /// <summary>家庭农务总是使用新任务，公有制只借该任务完成原粮运输与加工。</summary>
    /// <param name="actor">原版农民。</param>
    public static bool NeedsFamilyTask(Actor actor) => actor.city != null && (IsPrivate(actor.city) ||
        actor.inventory.getResource(RawGrainId) > 0 || actor.inventory.getResource("wheat") > 0 || actor.city.getResourcesAmount(RawGrainId) > 0);

    /// <summary>关闭原粮制度后直接获得麦粮；已存在的原粮仍能按原流程加工。</summary>
    /// <param name="city">实际采收人物的村庄。</param>
    public static string HarvestResource(City city) => KingdomPolicyService.Has(city, KingdomPolicyCard.GrainMilling) ? RawGrainId : "wheat";

    /// <summary>两种制度共用真实作物移除与基础收成发放，加成由共同采收入口结算。</summary>
    /// <param name="actor">实际采收人物。</param>
    /// <param name="crop">已经通过归属和农时检查的作物。</param>
    internal static void HarvestGrain(Actor actor, Building crop)
    {
        int amount = crop.asset.resources_given.Where(r => r.id == RawGrainId || r.id == "wheat").Sum(r => r.amount);
        crop.extractResources(actor); actor.addToInventory(HarvestResource(actor.city), amount);
    }

    /// <summary>产权切换保留在田作物，停止季节制时从当前外观继续原版生长。</summary>
    /// <param name="city">采用新政策的村庄。</param>
    /// <param name="previous">原来的政策。</param>
    /// <param name="next">即将生效的政策。</param>
    public static void PolicyChanged(City city, KingdomPolicyData previous, KingdomPolicyData next)
    {
        var state = State(city);
        bool becamePrivate = !previous.Has(KingdomPolicyCard.PrivateFarms) && next.Has(KingdomPolicyCard.PrivateFarms);
        if (!next.Has(KingdomPolicyCard.PrivateFarms)) foreach (var plot in state.Data.Plots) plot.Owner = 0;
        if (becamePrivate || previous.Has(KingdomPolicyCard.SeasonalFarming) != next.Has(KingdomPolicyCard.SeasonalFarming))
        {
            foreach (var crop in city.zones.SelectMany(z => z.buildings_all).Where(b => b.asset.wheat && b.isUsable()).Distinct())
            {
                var tile = crop.current_tile;
                if (becamePrivate && !CanFarm(state, tile))
                {
                    int cell = Cell(tile);
                    state.Data.Retiring[cell] = crop.id;
                    if (!state.Data.Grounds.ContainsKey(cell)) state.Data.Grounds.Add(cell, null);
                }
                if (!next.Has(KingdomPolicyCard.SeasonalFarming))
                    crop.data.grow_time = Math.Max(crop.data.grow_time, crop.component_wheat._current_level * crop.asset.growth_time);
            }
        }
        state.NextExpansion.Clear(); state.Sites = null; state.InnerDirty = true; Save(city);
    }

    /// <summary>公有制仍用原版风车缓存，只补入切换前仍在田里的作物。</summary>
    /// <param name="city">已经完成原版缓存扫描的村庄。</param>
    public static void AppendPublicCrops(City city)
    {
        var state = State(city);
        foreach (int cell in state.Land.Keys.Concat(state.Data.Retiring.Keys).Distinct())
        {
            var tile = Tile(cell);
            if (tile.zone?.city != city || tile.building?.asset.wheat != true || !tile.building.isUsable()) continue;
            city.calculated_crops.Add(tile);
            if (tile.building.component_wheat.isMaxLevel()) city.calculated_grown_wheat.Add(tile);
        }
        foreach (var tile in city.calculated_grown_wheat.ToArray())
            if (!state.Data.Retiring.ContainsKey(Cell(tile)) && !FamilyCropCalendar.Ready(tile.building)) city.calculated_grown_wheat.Remove(tile);
        city.calculated_crops.checkAddRemove(); city.calculated_grown_wheat.checkAddRemove();
    }

    /// <summary>公有制可采收全村田块，施肥跳过已施过的作物，不套用家庭归属。</summary>
    /// <param name="actor">寻找实际劳动目标的人物。</param>
    /// <param name="action">收割或施肥。</param>
    internal static WorldTile FindPublicTarget(Actor actor, FarmAction action)
    {
        bool harvest = action == FarmAction.Harvest;
        if (!VillageDutyService.IsResident(actor) || !actor.canWork() || !VillageDutyService.CanTakeCivilianJob(actor) &&
            (!harvest || !KingdomPolicyService.Has(actor.city, KingdomPolicyCard.SeasonalFarming))) return null;
        var state = State(actor.city);
        var cells = (harvest ? actor.city.calculated_grown_wheat : actor.city.calculated_crops).Select(Cell);
        if (harvest) cells = cells.Concat(state.Data.Retiring.Keys);
        WorldTile best = null; int distance = int.MaxValue;
        foreach (int cell in cells.Distinct())
        {
            var tile = Tile(cell); var crop = tile.building;
            if (tile.zone?.city != actor.city || crop?.asset.wheat != true || !crop.isUsable() ||
                !tile.isSameIsland(actor.current_tile) || tile.isTargeted() && !tile.isTargetedBy(actor) ||
                RailTrackRoutePlanner.IsReservedForTrack(tile)) continue;
            bool retired = state.Data.Retiring.TryGetValue(cell, out long id) && id == crop.id;
            if (harvest ? !(retired ? crop.component_wheat.isMaxLevel() : FamilyCropCalendar.Ready(crop)) :
                retired || !FamilyCropFertilizer.CanFertilize(crop)) continue;
            int value = Toolbox.SquaredDistTile(actor.current_tile, tile);
            if (value >= distance) continue;
            distance = value; best = tile;
        }
        return best;
    }
}

/// <summary>只在原版农民工作中按制度选择任务，不更改其他职业的任务表。</summary>
internal sealed class CondFamilyFarmPolicy : BehaviourActorCondition
{
    /// <summary>是否只判断私有制，供家庭任务结束步骤使用。</summary>
    private readonly bool privateOnly;
    /// <summary>建立原版任务条件。</summary>
    /// <param name="privateOnly">只判断私有制而不包含公有制原粮收尾。</param>
    internal CondFamilyFarmPolicy(bool privateOnly = false) => this.privateOnly = privateOnly;
    /// <inheritdoc />
    public override bool check(Actor actor) => privateOnly ? FamilyFarmService.IsPrivate(actor.city) : FamilyFarmService.NeedsFamilyTask(actor);
}
