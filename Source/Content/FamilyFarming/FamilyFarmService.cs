using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Content.Const;
using Cultiway.Content.VillageDuties;
using Cultiway.Content.VillageMilitary;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.FamilyFarming;

/// <summary>以原版家庭为经营单位维护农田，粮食只保存在人物背包和原版仓库。</summary>
public static partial class FamilyFarmService
{
    /// <summary>村庄存档内唯一的家庭农田记录。</summary>
    public const string SaveKey = "cw.family.farms";
    /// <summary>必须经过风车加工的不可食用原粮。</summary>
    public const string RawGrainId = "Cultiway.RawGrain";
    /// <summary>本世界已访问的村庄。</summary>
    private static readonly Dictionary<long, FarmState> states = new();
    /// <summary>运行缓存所属的原版世界。</summary>
    private static MapStats world;

    /// <summary>不进入存档的土地索引、劳动力、找地进度和内城范围。</summary>
    internal sealed class FarmState
    {
        /// <summary>当前村庄。</summary>
        internal City City;
        /// <summary>唯一保存记录。</summary>
        internal FamilyFarmData Data;
        /// <summary>地格对应的矩形，防止重复占地。</summary>
        internal readonly Dictionary<int, FamilyFarmPlot> Land = new();
        /// <summary>各农户实际参加农业的人员。</summary>
        internal Dictionary<long, List<Actor>> Farmers = new();
        /// <summary>无地或等待设施等可显示的原因。</summary>
        internal readonly Dictionary<long, string> Reasons = new();
        /// <summary>轮流检查田块的位置。</summary>
        internal int PlotCursor;
        /// <summary>轮流给家庭找地的位置。</summary>
        internal int OwnerCursor;
        /// <summary>扫描无主农田的区域位置。</summary>
        internal int ZoneCursor;
        /// <summary>当前区域中扫描到的地格位置。</summary>
        internal int TileCursor;
        /// <summary>下次允许扩大田地的世界时间。</summary>
        internal readonly Dictionary<long, double> NextExpansion = new();
        /// <summary>全村共用的城区周边矩形候选和找地进度。</summary>
        internal FamilyFarmSites Sites;
        /// <summary>内城和领地发生变化后重新建立范围。</summary>
        internal bool InnerDirty = true;
        /// <summary>本帧已经核对过内城记录。</summary>
        internal int InnerFrame = -1;
        /// <summary>内城记录和墙宽组成的缓存标记。</summary>
        internal (int cx, int cy, int hx, int hy, int width) InnerKey;
        /// <summary>仅在内城边界变化时重建的实际内部范围。</summary>
        internal WallShapeHelper.WallComputationContext InnerContext;
    }

    /// <summary>换世界后丢弃地块、人物和临时加工引用。</summary>
    public static void Clear() { states.Clear(); works.Clear(); world = null; }

    /// <summary>首次读取时从本村存档恢复索引。</summary>
    /// <param name="city">当前世界中的村庄。</param>
    internal static FarmState State(City city)
    {
        if (!ReferenceEquals(world, World.world.map_stats)) { Clear(); world = World.world.map_stats; }
        if (states.TryGetValue(city.id, out var state)) return state;
        city.data.get(SaveKey, out string json, null);
        var data = json == null ? new FamilyFarmData() : JsonConvert.DeserializeObject<FamilyFarmData>(json);
        if (data == null || !FamilyFarmData.ValidSettings(data.Width, data.Height, data.MaxArea) ||
            data.Plots == null || data.Grounds == null || data.Retiring == null || data.NextId < 1)
            throw new InvalidDataException("家庭农田存档无效。");
        state = new FarmState { City = city, Data = data };
        foreach (var plot in data.Plots)
        {
            if (plot.Width is < 1 or > 12 || plot.Height is < 1 or > 12 || plot.X < 0 || plot.Y < 0 ||
                plot.X + plot.Width > MapBox.width || plot.Y + plot.Height > MapBox.height)
                throw new InvalidDataException("家庭田块范围无效。");
            foreach (int cell in plot.Cells()) state.Land.Add(cell, plot);
        }
        states.Add(city.id, state); return state;
    }

    /// <summary>供界面读取唯一的保存设置和矩形记录。</summary>
    /// <param name="city">所属村庄。</param>
    public static FamilyFarmData Get(City city) => State(city).Data;

    /// <summary>补种树木等活动不占用已登记但尚未翻土的家庭田地。</summary>
    /// <param name="tile">待使用的地格。</param>
    public static bool IsReserved(WorldTile tile) => tile?.zone?.city != null && IsPrivate(tile.zone.city) && State(tile.zone.city).Land.ContainsKey(Cell(tile));

    /// <summary>原版村庄保存时写入经营归属和退耕收尾状态。</summary>
    /// <param name="city">正在保存的村庄。</param>
    public static void Save(City city)
    {
        if (world == World.world.map_stats && states.TryGetValue(city.id, out var state))
            city.data.set(SaveKey, JsonConvert.SerializeObject(state.Data));
    }

    /// <summary>提交本村尺寸，不改动家庭身份和人物职业。</summary>
    /// <param name="city">设置所属村庄。</param>
    /// <param name="width">起步宽度。</param>
    /// <param name="height">起步高度。</param>
    /// <param name="maxArea">每户面积上限。</param>
    public static bool Apply(City city, int width, int height, int maxArea)
    {
        if (!FamilyFarmData.ValidSettings(width, height, maxArea)) return false;
        var state = State(city); state.Data.Width = width; state.Data.Height = height; state.Data.MaxArea = maxArea;
        state.NextExpansion.Clear(); state.PlotCursor = 0; state.Sites = null; Save(city); return true;
    }

    /// <summary>有原版家庭时复用其编号，否则采用负数人物编号形成单人农户。</summary>
    /// <param name="actor">实际参加耕作的人物。</param>
    public static long OwnerOf(Actor actor) => actor.family?.id ?? -actor.id;

    /// <summary>把地格坐标转成当前世界内的索引。</summary>
    /// <param name="tile">目标地格。</param>
    internal static int Cell(WorldTile tile) => tile.x + tile.y * MapBox.width;

    /// <summary>读取已保存编号对应的当前地格。</summary>
    /// <param name="cell">当前地图内的地格编号。</param>
    internal static WorldTile Tile(int cell) => World.world.GetTile(cell % MapBox.width, cell / MapBox.width);

    /// <summary>领地变化使内城实际轮廓失效，下一次查地时重建一次。</summary>
    /// <param name="city">领地发生变化的村庄。</param>
    public static void InvalidateLand(City city)
    {
        if (city != null && states.TryGetValue(city.id, out var state)) { state.InnerDirty = true; state.InnerFrame = -1; }
    }

    /// <summary>复用城墙实际内部范围，不按每个农民重新扫描全城。</summary>
    /// <param name="state">本村运行索引。</param>
    /// <param name="tile">需要判断的地格。</param>
    internal static bool IsInner(FarmState state, WorldTile tile)
    {
        if (state.InnerFrame != Time.frameCount)
        {
            state.InnerFrame = Time.frameCount;
            var bounds = Plots.GetInnerBounds(state.City);
            state.City.data.get(ContentCityDataKeys.CityWallStage_int, out int stage, 0);
            var key = bounds.HasValue ? (bounds.Value.cx, bounds.Value.cy, bounds.Value.hx, bounds.Value.hy, stage >= 2 ? 2 : 1) : default;
            if (state.InnerDirty || state.InnerKey != key)
            {
                state.InnerKey = key; state.InnerDirty = false;
                state.InnerContext = bounds.HasValue ? WallShapeHelper.CreateContext(state.City) : null;
            }
        }
        var b = state.InnerKey;
        return state.InnerContext != null && WallShapeHelper.IsInsideWall(
            new WallShapeHelper.Bounds { cx = b.cx, cy = b.cy, hx = b.hx, hy = b.hy }, b.width, state.InnerContext, tile.x, tile.y);
    }

    /// <summary>按劳动力和经营面积给农业提供人数容量，无风车也可先建立田地。</summary>
    /// <param name="city">参与自动派工的村庄。</param>
    /// <param name="workers">可分配的普通工人数。</param>
    public static int WorkCapacity(City city, int workers)
    {
        var data = Get(city);
        int upkeep = (data.Plots.Sum(p => p.Area) + data.Width * data.Height - 1) / (data.Width * data.Height);
        int milling = city.getResourcesAmount(RawGrainId) > 0 ? city.countBuildingsType("type_windmill") * 2 : 0;
        return Math.Min(workers, Math.Max(Math.Max(1, workers / 3), upkeep + milling));
    }

    /// <summary>先更新实际农户，再检查田块和退耕，最后按预算寻找新田。</summary>
    /// <param name="city">本轮允许处理的村庄。</param>
    public static void Update(City city)
    {
        // 先清理失效农活并统计本村农民，再整理土地，最后按预算给农户找地。
        var state = State(city);
        var occupations = VillageMilitaryService.Get(city).Occupations;
        var present = new HashSet<long>();
        var farmers = new Dictionary<long, List<Actor>>();
        var personalPlots = new HashSet<long>(state.Data.Plots.Where(p => p.Owner < 0).Select(p => p.Owner));
        foreach (long id in works.Where(p => p.Value.CityId == city.id).Select(p => p.Key).ToArray())
        {
            var actor = World.world.units.get(id);
            if (actor == null) works.Remove(id);
            else if (!actor.isAlive() || actor.city != city || actor.ai.task != ActorTasks.FamilyFarmWork) Release(actor);
        }
        foreach (var actor in city.units)
        {
            if (!VillageDutyService.IsResident(actor) || actor.city != city) continue;
            long owner = OwnerOf(actor); present.Add(owner);
            if (owner > 0 && personalPlots.Contains(-actor.id))
                foreach (var plot in state.Data.Plots.Where(p => p.Owner == -actor.id)) plot.Owner = owner;
            bool farming = actor.citizen_job == CitizenJobLibrary.farmer ||
                !actor.isWarrior() && VillageDutyService.Classify(actor) == VillageResidentKind.Worker &&
                occupations.TryGetValue(actor.id, out string role) && role == "farmer";
            if (!farming || !actor.canWork()) continue;
            if (!farmers.TryGetValue(owner, out var members)) farmers.Add(owner, members = new List<Actor>());
            members.Add(actor);
        }
        state.Farmers = farmers;
        foreach (var plot in state.Data.Plots) if (plot.Owner != 0 && !present.Contains(plot.Owner)) plot.Owner = 0;
        foreach (long owner in state.Reasons.Keys.Where(o => !present.Contains(o)).ToArray()) state.Reasons.Remove(owner);
        // 覆盖和退耕按有限田块、地格数量分批处理。
        ValidatePlots(state);
        foreach (int cell in state.Data.Retiring.Keys.Take(128).ToArray()) CleanRetired(state, cell);
        if (!IsPrivate(city)) return;
        SurveyUnowned(state);
        if (farmers.Count == 0) return;
        var groups = farmers.ToArray();
        // 每村每轮最多尝试两个家庭，不为每个人扫描全城。
        for (int n = 0; n < Math.Min(2, groups.Length); n++)
        {
            var pair = groups[state.OwnerCursor++ % groups.Length];
            if (!VillageDutyService.CanTakeCivilianJob(pair.Value[0])) continue;
            EnsureLand(state, pair.Key, pair.Value);
        }
    }

    /// <summary>村庄消失时释放临时田块与加工引用。</summary>
    /// <param name="city">正在消亡的村庄。</param>
    public static void Remove(City city)
    {
        foreach (long id in works.Where(p => p.Value.CityId == city.id).Select(p => p.Key).ToArray()) works.Remove(id);
        states.Remove(city.id);
    }
}
