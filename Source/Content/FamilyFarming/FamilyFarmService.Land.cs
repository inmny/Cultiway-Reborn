using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Railways;
using Cultiway.Content.VillageDuties;

namespace Cultiway.Content.FamilyFarming;

/// <summary>家庭矩形选址、无主田认领和内城退耕。</summary>
public static partial class FamilyFarmService
{
    /// <summary>土地必须在本村并可耕作，铁路预留带和内城不允许新农活。</summary>
    /// <param name="state">本村土地索引。</param>
    /// <param name="tile">待检查地格。</param>
    internal static bool CanFarm(FarmState state, WorldTile tile) => tile != null && tile.zone?.city == state.City &&
        (tile.Type.can_be_farm || tile.Type.farm_field) && !tile.Type.road && !tile.Type.block &&
        !tile.isTemporaryFrozen() && !IsInner(state, tile) && !state.Data.Retiring.ContainsKey(Cell(tile)) &&
        !RailTrackRoutePlanner.IsReservedForTrack(tile) &&
        (!tile.hasBuilding() || tile.building.asset.wheat || tile.building.canRemoveForFarms());

    /// <summary>检查完整矩形及四周一格间隔，已有本户田块必须完整包含才能合并。</summary>
    /// <param name="state">村庄土地索引。</param>
    /// <param name="plot">准备申请的矩形。</param>
    /// <param name="actor">实际能够走到该岛屿的农民。</param>
    private static bool CanReserve(FarmState state, FamilyFarmPlot plot, Actor actor)
    {
        var overlaps = new HashSet<FamilyFarmPlot>();
        var neighbors = new HashSet<FamilyFarmPlot>();
        for (int y = plot.Y - 1; y <= plot.Y + plot.Height; y++)
        for (int x = plot.X - 1; x <= plot.X + plot.Width; x++)
        {
            var tile = World.world.GetTile(x, y);
            if (tile == null) return false;
            bool inside = x >= plot.X && x < plot.X + plot.Width && y >= plot.Y && y < plot.Y + plot.Height;
            if (inside && (!CanFarm(state, tile) || !tile.isSameIsland(actor.current_tile))) return false;
            if (!state.Land.TryGetValue(Cell(tile), out var other)) continue;
            if (other.Owner != plot.Owner) return false;
            if (inside) overlaps.Add(other);
            else neighbors.Add(other);
        }
        if (neighbors.Any(p => !overlaps.Contains(p))) return false;
        foreach (var other in overlaps)
            if (other.X < plot.X || other.Y < plot.Y || other.X + other.Width > plot.X + plot.Width ||
                other.Y + other.Height > plot.Y + plot.Height) return false;
        int retained = state.Data.Plots.Where(p => p.Owner == plot.Owner && !overlaps.Contains(p)).Sum(p => p.Area);
        return retained + plot.Area <= state.Data.MaxArea;
    }

    /// <summary>登记矩形并保存原地表；合并时只替换完整包住的旧田块。</summary>
    /// <param name="state">村庄土地索引。</param>
    /// <param name="plot">已经通过完整检查的矩形。</param>
    private static void AddPlot(FarmState state, FamilyFarmPlot plot)
    {
        var replaced = plot.Cells().Where(state.Land.ContainsKey).Select(c => state.Land[c]).Distinct().ToArray();
        foreach (var old in replaced)
        {
            state.Data.Plots.Remove(old);
            foreach (int cell in old.Cells()) state.Land.Remove(cell);
        }
        plot.Id = state.Data.NextId++; state.Data.Plots.Add(plot);
        foreach (int cell in plot.Cells())
        {
            state.Land.Add(cell, plot);
            if (!state.Data.Grounds.ContainsKey(cell))
            {
                var tile = Tile(cell);
                state.Data.Grounds.Add(cell, tile.Type.farm_field ? null : tile.top_type?.id);
            }
        }
    }

    /// <summary>城区变化时重排候选，其他时候全村共用由近到远的进度。</summary>
    /// <param name="state">本村土地索引。</param>
    private static FamilyFarmSites Sites(FarmState state)
    {
        var inner = Plots.GetInnerBounds(state.City);
        var bounds = inner.HasValue ? (inner.Value.cx, inner.Value.cy, inner.Value.hx, inner.Value.hy) :
            ((int)state.City.city_center.x, (int)state.City.city_center.y, 3, 3);
        if (state.Sites == null || state.Sites.Bounds != bounds) state.Sites = new FamilyFarmSites(bounds);
        return state.Sites;
    }

    /// <summary>远处旧田收完后归还土地；仍有作物的矩形等待最后一轮收获。</summary>
    /// <param name="state">本村土地索引。</param>
    /// <param name="owner">正在整理田地的家庭。</param>
    private static void ReleaseDistantPlots(FarmState state, long owner)
    {
        var sites = Sites(state);
        foreach (var plot in state.Data.Plots.Where(p => p.Owner == owner && !sites.Contains(p)).ToArray())
        {
            if (plot.Cells().Any(c => Tile(c).building?.asset.wheat == true)) continue;
            state.Data.Plots.Remove(plot);
            foreach (int cell in plot.Cells()) { state.Land.Remove(cell); RetireCell(state, cell); }
            sites.Reset();
        }
    }

    /// <summary>先收拢远处空田和认领近处无主田，再从城区向外查找，田块之间留一格。</summary>
    /// <param name="state">当前村庄的土地与找地进度。</param>
    /// <param name="owner">实际农户编号。</param>
    /// <param name="farmers">本户参加农业的人物。</param>
    private static void EnsureLand(FarmState state, long owner, List<Actor> farmers)
    {
        double now = World.world.getCurWorldTime();
        if (state.NextExpansion.TryGetValue(owner, out double next) && next > now) return;
        state.NextExpansion[owner] = now + 5;
        ReleaseDistantPlots(state, owner);
        var sites = Sites(state);
        int area = state.Data.Plots.Where(p => p.Owner == owner).Sum(p => p.Area);
        var size = FamilyFarmGeometry.Size(state.Data, farmers.Count);
        int desired = size.width * size.height;
        if (area >= desired) { state.Reasons.Remove(owner); return; }
        var actor = farmers[0];
        foreach (var plot in state.Data.Plots.Where(p => p.Owner == 0 && sites.Contains(p)).OrderBy(sites.Distance))
        {
            if (area + plot.Area > desired || !Tile(plot.Cells().First()).isSameIsland(actor.current_tile)) continue;
            plot.Owner = owner; area += plot.Area;
            if (area >= desired) { state.Reasons.Remove(owner); return; }
        }
        if (area > 0)
        {
            int cultivated = state.Data.Plots.Where(p => p.Owner == owner).SelectMany(p => p.Cells()).Count(c => Tile(c).Type.farm_field);
            if (cultivated * 4 < area * 3) return;
            if (state.City.getResourcesAmount(RawGrainId) > Math.Max(20, state.City.status.population / 4))
            { state.Reasons[owner] = "WaitingMill"; return; }
        }
        var old = state.Data.Plots.FirstOrDefault(p => p.Owner == owner);
        if (old != null)
        {
            var expanded = new FamilyFarmPlot { Owner = owner, X = old.X, Y = old.Y,
                Width = size.width, Height = size.height };
            if (sites.Contains(expanded) && CanReserve(state, expanded, actor))
            {
                AddPlot(state, expanded); state.Reasons.Remove(owner); return;
            }
        }
        int remaining = desired - area;
        int w = Math.Min(size.width, remaining), h = Math.Min(size.height, Math.Max(1, remaining / w));
        foreach (var position in sites.Next(w, h))
        {
            var plot = new FamilyFarmPlot { Owner = owner, X = position.x, Y = position.y, Width = w, Height = h };
            if (!CanReserve(state, plot, actor)) continue;
            AddPlot(state, plot); state.Reasons.Remove(owner); return;
        }
        if (!state.Reasons.TryGetValue(owner, out string reason) || reason != "InnerRetirement") state.Reasons[owner] = "NoLand";
    }

    /// <summary>分批扫描玩家直接制造或尚未登记的田地，形成无主矩形供认领。</summary>
    /// <param name="state">本村调查进度。</param>
    private static void SurveyUnowned(FarmState state)
    {
        if (state.City.zones.Count == 0) return;
        for (int n = 0; n < 64; n++)
        {
            var zone = state.City.zones[state.ZoneCursor % state.City.zones.Count];
            if (state.TileCursor >= zone.tiles.Length) { state.TileCursor = 0; state.ZoneCursor++; continue; }
            var tile = zone.tiles[state.TileCursor++];
            int cell = Cell(tile);
            if (!tile.Type.farm_field || state.Land.ContainsKey(cell) || state.Data.Retiring.ContainsKey(cell)) continue;
            if (!CanFarm(state, tile)) { RetireCell(state, cell); continue; }
            int w = 1, h = 1;
            bool Free(int x, int y)
            {
                var t = World.world.GetTile(x, y);
                return t != null && t.Type.farm_field && CanFarm(state, t) && !state.Land.ContainsKey(Cell(t));
            }
            while (w < state.Data.Width && Free(tile.x + w, tile.y)) w++;
            while (h < state.Data.Height)
            {
                bool row = true;
                for (int dx = 0; dx < w; dx++) if (!Free(tile.x + dx, tile.y + h)) { row = false; break; }
                if (!row) break;
                h++;
            }
            AddPlot(state, new FamilyFarmPlot { X = tile.x, Y = tile.y, Width = w, Height = h });
        }
    }

    /// <summary>每轮检查至多四块田，把内城、建筑或面积上限覆盖的部分拆成剩余矩形。</summary>
    /// <param name="state">本村田块记录。</param>
    private static void ValidatePlots(FarmState state)
    {
        for (int n = 0, count = Math.Min(4, state.Data.Plots.Count); n < count && state.Data.Plots.Count > 0; n++)
        {
            var plot = state.Data.Plots[state.PlotCursor++ % state.Data.Plots.Count];
            if (!IsPrivate(state.City))
            {
                if (plot.Cells().Any(c => Tile(c).building?.asset.wheat == true && Tile(c).building.isUsable())) continue;
                state.Data.Plots.Remove(plot);
                foreach (int cell in plot.Cells()) { state.Land.Remove(cell); state.Data.Grounds.Remove(cell); }
                continue;
            }
            int allowed = plot.Owner == 0 ? plot.Area : Math.Max(0, state.Data.MaxArea -
                state.Data.Plots.Where(p => p.Owner == plot.Owner && p != plot).Sum(p => p.Area));
            var removed = new HashSet<int>(); int seen = 0;
            foreach (int cell in plot.Cells())
            {
                if (seen++ < allowed && CanFarm(state, Tile(cell))) continue;
                removed.Add(cell);
                if (IsInner(state, Tile(cell))) state.Reasons[plot.Owner] = "InnerRetirement";
                RetireCell(state, cell);
            }
            if (removed.Count == 0) continue;
            state.Sites?.Reset();
            state.Data.Plots.Remove(plot);
            foreach (int cell in plot.Cells()) state.Land.Remove(cell);
            var parts = FamilyFarmGeometry.Split(plot, removed);
            foreach (var part in parts)
            {
                part.Id = state.Data.NextId++; state.Data.Plots.Add(part);
                foreach (int cell in part.Cells()) state.Land.Add(cell, part);
            }
        }
    }

    /// <summary>立即停止新农活；成熟作物只保留当前这一株的最后收获机会。</summary>
    /// <param name="state">本村退耕记录。</param>
    /// <param name="cell">已经失效的地格。</param>
    private static void RetireCell(FarmState state, int cell)
    {
        var tile = Tile(cell);
        if (tile.zone?.city != state.City) { state.Data.Grounds.Remove(cell); return; }
        if (state.Data.Retiring.ContainsKey(cell)) return;
        var crop = tile.building;
        bool ripe = crop?.asset.wheat == true && crop.isUsable() && crop.component_wheat.isMaxLevel();
        state.Data.Retiring.Add(cell, ripe ? crop.id : 0);
        if (!ripe && crop?.asset.wheat == true) crop.startDestroyBuilding();
        CleanRetired(state, cell);
    }

    /// <summary>成熟作物消失或完成最后收获后恢复原地表，不影响新建筑。</summary>
    /// <param name="state">本村退耕记录。</param>
    /// <param name="cell">等待清理的地格。</param>
    private static void CleanRetired(FarmState state, int cell)
    {
        var tile = Tile(cell); long cropId = state.Data.Retiring[cell];
        var crop = tile.building;
        if (cropId != 0 && crop?.id == cropId && crop.isUsable()) return;
        if (tile.zone?.city == state.City && tile.Type.farm_field)
        {
            state.Data.Grounds.TryGetValue(cell, out string ground);
            tile.setTopTileType(ground == null ? null : AssetManager.top_tiles.get(ground));
        }
        state.Data.Retiring.Remove(cell); state.Data.Grounds.Remove(cell);
    }

    /// <summary>作物更新时即刻拒绝在内城继续生长，地表整理仍分批完成。</summary>
    /// <param name="crop">原版麦类作物。</param>
    public static bool CanGrow(Building crop)
    {
        var city = crop.current_tile?.zone?.city ?? crop.city;
        if (city == null) return true;
        var state = State(city); int cell = Cell(crop.current_tile);
        if (state.Data.Retiring.TryGetValue(cell, out long finalCrop))
            return finalCrop == crop.id && !crop.component_wheat.isMaxLevel() && FamilyCropCalendar.Update(crop);
        if (!IsPrivate(city)) return FamilyCropCalendar.Update(crop);
        if (IsInner(state, crop.current_tile)) { RetireCell(state, cell); return false; }
        return FamilyCropCalendar.Update(crop);
    }

    /// <summary>以已登记家庭田块替换原版风车半径缓存，保留其他系统读取原版列表的能力。</summary>
    /// <param name="city">准备刷新农务缓存的村庄。</param>
    public static void RefreshFarmCache(City city)
    {
        var state = State(city);
        city.calculated_place_for_farms.Clear(); city.calculated_farm_fields.Clear();
        city.calculated_crops.Clear(); city.calculated_grown_wheat.Clear();
        foreach (int cell in state.Land.Keys)
        {
            var tile = Tile(cell);
            if (!CanFarm(state, tile)) continue;
            if (!tile.Type.farm_field) city.calculated_place_for_farms.Add(tile);
            else
            {
                city.calculated_farm_fields.Add(tile);
                if (tile.building?.asset.wheat == true)
                {
                    city.calculated_crops.Add(tile);
                    if (tile.building.component_wheat.isMaxLevel()) city.calculated_grown_wheat.Add(tile);
                }
            }
        }
        foreach (int cell in state.Data.Retiring.Keys)
            if (Tile(cell).building?.id == state.Data.Retiring[cell]) city.calculated_grown_wheat.Add(Tile(cell));
        city.calculated_place_for_farms.checkAddRemove(); city.calculated_farm_fields.checkAddRemove();
        city.calculated_crops.checkAddRemove(); city.calculated_grown_wheat.checkAddRemove();
    }

    /// <summary>原版清理周期直接查询家庭经营记录，不因缓存刷新间隔把新田或已播种田误删。</summary>
    public static void UpdateDecay()
    {
        if (WorldBehaviourActions._last_used_world_id != MapBox.current_world_seed_id)
        {
            WorldBehaviourActions._last_used_world_id = MapBox.current_world_seed_id;
            WorldBehaviourActions._temp_wheat_list.Clear();
        }
        var pending = WorldBehaviourActions._temp_wheat_list;
        if (pending.Count == 0) pending.AddRange(TopTileLibrary.field.hashset);
        for (int n = 0; n < 10 && pending.Count > 0; n++)
        {
            var tile = pending[pending.Count - 1]; pending.RemoveAt(pending.Count - 1);
            if (!tile.Type.farm_field) continue;
            var city = tile.zone?.city;
            if (city == null) { MapAction.decreaseTile(tile, pDamage: false); continue; }
            var state = State(city); int cell = Cell(tile);
            if (state.Data.Retiring.ContainsKey(cell)) { CleanRetired(state, cell); continue; }
            if (!IsPrivate(city))
            {
                if (state.Land.ContainsKey(cell) && tile.building?.asset.wheat == true && tile.building.isUsable() ||
                    city.calculated_farm_fields.Contains(tile) || city.calculated_place_for_farms.Contains(tile)) continue;
                if (tile.building?.asset.wheat == true) tile.building.startDestroyBuilding();
                MapAction.decreaseTile(tile, pDamage: false); continue;
            }
            if (!CanFarm(state, tile)) { RetireCell(state, cell); continue; }
            if (!state.Land.ContainsKey(cell))
                AddPlot(state, new FamilyFarmPlot { X = tile.x, Y = tile.y, Width = 1, Height = 1 });
        }
    }
}
