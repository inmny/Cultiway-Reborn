using System;
using System.Collections.Generic;
using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway.Content;

[Dependency(typeof(PlotCategories))]
public class Plots : ExtendLibrary<PlotAsset, Plots>
{
    [CloneSource(S_Plot.new_book)]
    public static PlotAsset NewCultibook { get; private set; }

    [CloneSource(S_Plot.new_language)]
    public static PlotAsset NewSect { get; private set; }

    [CloneSource(S_Plot.alliance_create)]
    public static PlotAsset BuildTrainStation { get; private set; }

    [CloneSource(S_Plot.alliance_create)]
    public static PlotAsset BuildTrainTrack { get; private set; }

    // 城市领袖/国王自主谋划：围绕城市修筑一圈城墙（方形/圆形，预留出口）
    [CloneSource(S_Plot.new_book)]
    public static PlotAsset BuildCityWall { get; private set; }

    // 召唤恶魔谋划：依据发起者特质，有几率召唤对应的混沌魔塔
    [CloneSource(S_Plot.new_language)]
    public static PlotAsset SummonKhorne { get; private set; }
    [CloneSource(S_Plot.new_language)]
    public static PlotAsset SummonSlaanesh { get; private set; }
    [CloneSource(S_Plot.new_language)]
    public static PlotAsset SummonTzeentch { get; private set; }
    [CloneSource(S_Plot.new_language)]
    public static PlotAsset SummonNurgle { get; private set; }

    // 召唤恶魔谋划的触发阈值与召唤几率
    private const int   DEMON_KILLS_THRESHOLD = 20;    // 杀人数门槛 -> 恐虐
    private const int   DEMON_CHARM_THRESHOLD = 15;    // 魅力(外交)门槛 -> 色孽
    private const int   DEMON_INT_THRESHOLD   = 15;    // 智力门槛 -> 奸奇
    private const float DEMON_SUMMON_CHANCE   = 0.4f;  // 召唤成功率

    protected override bool AutoRegisterAssets() => true;

    protected override void OnInit()
    {
        NewCultibook.path_icon = "books/custom_book_covers/cultibook/31";
        NewCultibook.check_is_possible = (Actor actor) =>
            actor.hasCity()
            && actor.hasCulture()
            && actor.hasLanguage()
            && actor.city.hasBookSlots()
            && actor.GetExtend().HasCultisys<Xian>();
        NewCultibook.check_should_continue = (Actor actor) =>
            actor.hasCity()
            && actor.hasCulture()
            && actor.hasLanguage()
            && actor.city.hasBookSlots()
            && actor.GetExtend().HasCultisys<Xian>();
        NewCultibook.action = (Actor actor) =>
        {
            if (!actor.hasCity())
            {
                return false;
            }

            if (!actor.city.hasBookSlots())
            {
                return false;
            }

            var book = World.world.books.CreateNewCultibook(actor);
            return book != null;
        };

        NewSect.path_icon = "books/custom_book_covers/cultibook/31";
        NewSect.group_id = PlotCategories.Sect.id;
        NewSect.check_is_possible = a => a.GetExtend().HasCultibook() && !a.HasSect();
        NewSect.check_should_continue = a => a.GetExtend().HasCultibook() && !a.HasSect();
        NewSect.action = a =>
        {
            if (!a.GetExtend().HasCultibook())
                return false;
            if (a.HasSect())
                return false;

            a.GetExtend().SetSect(WorldboxGame.I.Sects.BuildSect(a));
            return true;
        };

        BuildTrainStation.path_icon = "cultiway/icons/plots/iconTrainStation";
        BuildTrainStation.group_id = PlotCategories.Others.id;
        BuildTrainStation.can_be_done_by_king = false;
        BuildTrainStation.can_be_done_by_leader = true;
        BuildTrainStation.requires_diplomacy = false;
        BuildTrainStation.check_is_possible = delegate(Actor a)
        {
            return a.hasCity()
                && !World.world.plots.isPlotTypeAlreadyRunning(a, BuildTrainStation)
                && !a.city.hasBuildingType(Buildings.TrainStation.type)
                && FindTileForTrainStation(a.city) != null;
        };
        BuildTrainStation.try_to_start_advanced = delegate(Actor a, PlotAsset asset, bool force)
        {
            var plot = World.world.plots.newPlot(a, asset, force);
            if (plot == null || !plot.checkInitiatorAndTargets())
            {
                return false;
            }
            var targets = GetTrainTargets(a.city);
            if (targets.Count == 0)
            {
                if (FindTileForTrainStation(a.city) == null)
                {
                    return false;
                }
            }
            foreach (var target_city in targets)
            {
                if (target_city.leader.isRekt())
                    continue;
                target_city.leader.setPlot(plot);
            }
            return true;
        };
        BuildTrainStation.check_should_continue = a =>
        {
            foreach (var aa in a.plot.units)
            {
                if (aa.city.isRekt())
                {
                    return false;
                }
                if (aa.kingdom != a.kingdom && !aa.kingdom.isOpinionTowardsKingdomGood(a.kingdom))
                {
                    return false;
                }
            }
            return true;
        };
        BuildTrainStation.check_can_be_forced = a =>
        {
            return a.hasCity() && FindTileForTrainStation(a.city) != null;
        };
        BuildTrainStation.action = a =>
        {
            var plot = a.plot;

            var cities = new HashSet<City>();
            foreach (var aa in plot.units)
            {
                if (aa.city.isRekt())
                    continue;
                if (aa.kingdom != a.kingdom && !aa.kingdom.isOpinionTowardsKingdomGood(a.kingdom))
                    continue;
                if (aa != aa.city.leader)
                    continue;
                cities.Add(aa.city);
            }

            foreach (var aa in plot.units)
            {
                aa.leavePlot();
            }
            if (cities.Count == 0)
            {
                return false;
            }
            var actually_built = 0;
            foreach (var city in cities)
            {
                if (city.hasBuildingType(Buildings.TrainStation.type))
                {
                    continue;
                }
                var tile = FindTileForTrainStation(city);
                if (tile == null)
                {
                    continue;
                }
                World.world.buildings.addBuilding(Buildings.TrainStation.id, tile);
                actually_built++;
            }
            return actually_built > 0;
        };

        BuildTrainTrack.path_icon = "cultiway/icons/plots/iconTrainNet";
        BuildTrainTrack.group_id = PlotCategories.Others.id;
        BuildTrainTrack.can_be_done_by_king = false;
        BuildTrainTrack.can_be_done_by_leader = true;
        BuildTrainTrack.requires_diplomacy = false;
        BuildTrainTrack.check_is_possible = delegate(Actor a)
        {
            return a.hasCity() && GetTrainTrackTargets(a.city).Count > 0;
        };
        BuildTrainTrack.check_can_be_forced = delegate(Actor a)
        {
            return a.hasCity() && GetTrainTrackTargets(a.city).Count > 0;
        };
        BuildTrainTrack.try_to_start_advanced = delegate(Actor a, PlotAsset asset, bool force)
        {
            var targets = GetTrainTrackTargets(a.city);
            if (targets.Count == 0)
            {
                return false;
            }

            var plot = World.world.plots.newPlot(a, asset, force);
            if (plot == null || !plot.checkInitiatorAndTargets())
            {
                return false;
            }
            foreach (var target_city in targets)
            {
                target_city.leader.setPlot(plot);
            }
            return true;
        };
        BuildTrainTrack.check_should_continue = delegate(Actor a)
        {
            foreach (var aa in a.plot.units)
            {
                if (aa.city.isRekt())
                    return false;
                if (aa.kingdom != a.kingdom && !aa.kingdom.isOpinionTowardsKingdomGood(a.kingdom))
                    return false;
                if (aa != aa.city.leader)
                    return false;
                if (!aa.city.hasBuildingType(Buildings.TrainStation.type))
                    return false;
            }
            return true;
        };
        BuildTrainTrack.action = delegate(Actor a)
        {
            var source_city = a.city;
            var targets = new List<City>();
            foreach (var aa in a.plot.units)
            {
                if (aa == a)
                    continue;
                targets.Add(aa.city);
                aa.leavePlot();
            }
            if (targets.Count == 0)
            {
                return false;
            }
            var source_station = source_city.getBuildingOfType(Buildings.TrainStation.type);
            var any_built = false;
            foreach (var target_city in targets)
            {
                var target_station = target_city.getBuildingOfType(Buildings.TrainStation.type);
                if (target_station == null)
                    continue;
                var source_tile = source_station.current_tile;
                var target_tile = target_station.current_tile;
                if (source_tile == null || target_tile == null)
                    continue;
                var path = GetTrainTrackDirection(source_tile, target_tile);
                if (path.Count == 0)
                    continue;
                foreach (var tile in path)
                {
                    MapAction.terraformTop(tile, TopTileTypes.TrainTrack, Terraforms.TrainTrack, false);
                }
                TrainTrackRepairSystem.RegisterLink(source_station, target_station, path);
                any_built = true;
            }
            return any_built;
        };

        // 召唤恶魔：单位依据自身特质，有几率在所属城市召唤对应的混沌魔塔
        SetupDemonSummonPlot(SummonKhorne,   "cultiway/icons/Khorne",
            a => a.data.kills >= DEMON_KILLS_THRESHOLD, Buildings.KhorneTower,   DEMON_SUMMON_CHANCE);
        SetupDemonSummonPlot(SummonSlaanesh, "cultiway/icons/Slaanesh",
            a => a.diplomacy >= DEMON_CHARM_THRESHOLD,  Buildings.SlaaneshTower, DEMON_SUMMON_CHANCE);
        SetupDemonSummonPlot(SummonTzeentch, "cultiway/icons/Tzeentch",
            a => a.intelligence >= DEMON_INT_THRESHOLD, Buildings.TzeentchTower, DEMON_SUMMON_CHANCE);
        SetupDemonSummonPlot(SummonNurgle,   "cultiway/icons/Nurgle",
            a => a.hasTrait(S_Trait.plague),            Buildings.NurgleTower,   DEMON_SUMMON_CHANCE);

        // 修建城墙：城市领袖/国王在城市达到一定规模后自主谋划，围绕城市筑一圈城墙
        SetupBuildCityWallPlot(BuildCityWall, "cultiway/icons/Tower");
    }

    private static List<WorldTile> GetTrainTrackDirection(
        WorldTile source_tile,
        WorldTile target_tile
    )
    {
        return TrainTrackPathHelper.BuildPath(source_tile, target_tile);
    }

    private static List<City> GetTrainTrackTargets(City city)
    {
        if (!city.hasBuildingType(Buildings.TrainStation.type))
            return new List<City>();
        var targets = new List<City>();
        foreach (var target_city in city.neighbours_cities)
        {
            if (
                target_city.kingdom != city.kingdom
                && !target_city.kingdom.isOpinionTowardsKingdomGood(city.kingdom)
            )
                continue;
            if (!target_city.hasBuildingType(Buildings.TrainStation.type))
                continue;
            targets.Add(target_city);
        }
        return targets;
    }

    private static WorldTile FindTileForTrainStation(City city)
    {
        var zones = new List<TileZone>();
        CityBehBuild.fillPossibleZones(Buildings.TrainStation, city, zones);
        if (zones.Count == 0)
            return null;
        return CityBehBuild.tryToBuildInZones(zones, Buildings.TrainStation, city, false);
    }

    /// <summary>
    /// 配置一个召唤恶魔的谋划：满足 <paramref name="condition"/> 的城市领袖/国王发起后，
    /// 有 <paramref name="chance"/> 的几率在城市范围内召唤出 <paramref name="tower"/>。
    /// </summary>
    private static void SetupDemonSummonPlot(
        PlotAsset plot, string icon, Func<Actor, bool> condition,
        BuildingAsset tower, float chance)
    {
        plot.path_icon = icon;
        plot.group_id = PlotCategories.Others.id;
        plot.is_basic_plot = true;          // 允许 AI 单位自主发动
        plot.can_be_done_by_king = true;
        plot.can_be_done_by_leader = true;
        plot.requires_diplomacy = false;
        plot.check_is_possible = a => a.hasCity()
                                     && condition(a)
                                     && !a.city.hasBuildingType(tower.type)
                                     && !World.world.plots.isPlotTypeAlreadyRunning(a, plot);
        plot.check_can_be_forced = plot.check_is_possible;
        plot.check_should_continue = a => a.hasCity() && !a.city.hasBuildingType(tower.type);
        plot.action = a =>
        {
            if (!a.hasCity() || a.city.hasBuildingType(tower.type))
                return false;
            if (!Randy.randomChance(chance))
                return false;
            var tile = FindTileForTower(a.city, tower);
            if (tile == null)
                return false;
            World.world.buildings.addBuilding(tower.id, tile);
            return true;
        };
    }

    // ========== 修建城墙谋划（内墙 → 外墙 → 要塞 三阶段渐进） ==========
    private const int   WALL_MIN_ZONES             = 6;    // 城市至少 6 个 zone 才考虑修墙
    private const int   WALL_CIRCLE_ZONE_THRESHOLD = 300;  // zone 数 ≥ 300 的大城用圆形，否则方形
    private const float WALL_REBUILD_RATIO         = 0.3f; // 现存城墙低于完整圈的 30% 视为"被摧毁"，允许重修
    private const int   WALL_TOWER_INTERVAL        = 10;   // 沿城墙每隔多少格放置一座防御箭塔

    private const int WALL_STAGE_NONE     = 0; // 无墙
    private const int WALL_STAGE_INNER    = 1; // 仅内墙（木墙，宽1）
    private const int WALL_STAGE_BOTH     = 2; // 内墙（石墙，宽2）+ 外墙（木墙，宽1）+ 出入口箭塔
    private const int WALL_STAGE_FORTRESS = 3; // 内墙（古城墙，宽2）+ 外墙（石墙，宽2）

    /// <summary>
    /// 配置"修筑城墙"谋划：城市领袖/国王在城市达到一定规模后自主发起，分三阶段修筑：
    /// 1) 初次：生成一圈<b>木墙</b>包围城市全部领土（宽度1）；
    /// 2) 二次（已有内墙）：把内墙<b>原位替换为石墙、宽度2</b>，并在距内墙一个内墙直径处
    ///    （r_outer = 3×r_inner）再加一圈<b>木墙外墙</b>，内外墙每个出入口各放一座<b>种族箭塔</b>守门；
    /// 3) 三次（已有内外墙）：内墙替换为<b>古城墙</b>（宽2），外墙替换为<b>石墙、宽度2</b>。
    /// 最终阶段任一城墙被摧毁大半后可重新谋划重建。
    /// </summary>
    private static void SetupBuildCityWallPlot(PlotAsset plot, string icon)
    {
        plot.path_icon             = icon;
        plot.group_id              = PlotCategories.Others.id;
        plot.is_basic_plot         = true;   // 加入 AI 自主发动候选池（PlotsLibrary.basic_plots）
        plot.can_be_done_by_king   = true;
        plot.can_be_done_by_leader = true;
        plot.requires_diplomacy    = false;
        plot.money_cost            = 40;

        // 可行：有城 + 规模达标 + 可推进阶段(未满 或 已满但被摧毁) + 没有同类 plot 在跑
        plot.check_is_possible = a => a.hasCity()
                                     && a.city.zones.Count >= WALL_MIN_ZONES
                                     && CanAdvanceWallStage(a.city)
                                     && !World.world.plots.isPlotTypeAlreadyRunning(a, plot);
        plot.check_can_be_forced   = plot.check_is_possible;
        plot.check_should_continue = a => a.hasCity()
                                         && a.city.zones.Count >= WALL_MIN_ZONES
                                         && CanAdvanceWallStage(a.city);

        plot.action = a =>
        {
            if (!a.hasCity()) return false;
            var city    = a.city;
            int stage   = GetWallStage(city);
            bool circle = city.zones.Count >= WALL_CIRCLE_ZONE_THRESHOLD;
            int rInner;
            int rOuter;
            if (stage == WALL_STAGE_NONE)
            {
                // 初次：一圈木墙包围全部领土（宽度1，半径=全领地）；记录半径，此后固定不随城市扩张变化
                rInner = WallShapeHelper.InnerRadius(city);
                PlaceWallRing(a, circle, rInner, 1, TopTileLibrary.wall_wild, WALL_TOWER_INTERVAL);
                SetInnerRadius(city, rInner);
                SetWallStage(city, WALL_STAGE_INNER);
            }
            else
            {
                // 内墙半径用建墙时记录的值（原位，不随城市扩张漂移）
                rInner = GetCurrentInnerRadius(city);
                rOuter = WallShapeHelper.OuterRadiusFromInner(rInner);
                if (stage == WALL_STAGE_INNER)
                {
                    // 二次：内墙原位替换为石墙、宽度2；外墙(3r)木墙宽1；内外墙出入口各放一座种族箭塔守门
                    PlaceWallRing(a, circle, rInner, 2, TopTileLibrary.wall_order, WALL_TOWER_INTERVAL);
                    PlaceWallRing(a, circle, rOuter, 1, TopTileLibrary.wall_wild, WALL_TOWER_INTERVAL);
                    PlaceExitTowers(a, circle, rInner);
                    PlaceExitTowers(a, circle, rOuter);
                    SetWallStage(city, WALL_STAGE_BOTH);
                }
                else
                {
                    // 三次(stage 2→3) 或 被毁重建(stage 3)：内墙替换为古城墙、宽度2；外墙替换为石墙、宽度2
                    PlaceWallRing(a, circle, rInner, 2, TopTileLibrary.wall_ancient, WALL_TOWER_INTERVAL);
                    PlaceWallRing(a, circle, rOuter, 2, TopTileLibrary.wall_order, WALL_TOWER_INTERVAL);
                    SetWallStage(city, WALL_STAGE_FORTRESS);
                }
            }
            return true;
        };
    }

    /// <summary>
    /// 放置一圈城墙：生成 ring → 直接设置城墙 top_tile（不拆除建筑、不改变城市 zones）。
    /// 同时沿城墙每隔 <paramref name="towerInterval"/> 格放置一座<b>种族箭塔</b>用于防御。
    /// </summary>
    private static void PlaceWallRing(Actor actor, bool circle, int radius, int width, TopTileType wall, int towerInterval)
    {
        var city = actor.city;
        var ring = WallShapeHelper.ComputeWallRing(city, circle, radius, width);
        if (ring == null) return;
        string tower_id = GetWatchTowerId(city);
        for (int i = 0; i < ring.Count; i++)
        {
            var tile = ring[i];
            // 直接设置城墙 top_tile；不用 MapAction.terraformTop（它会摧毁路径建筑，导致城市 zone 被放弃），
            // 也不主动拆除建筑——城墙与现有建筑共存，不影响城市 zones
            tile.setTopTileType(wall);
            // 沿城墙每隔 towerInterval 格放一座种族箭塔（防御）；跳过已有建筑的 tile
            if (towerInterval > 0 && i % towerInterval == 0 && tile.building == null)
            {
                World.world.buildings.addBuilding(tower_id, tile);
            }
        }
    }

    /// <summary>在城墙出入口处放置种族箭塔守门；箭塔放在缺口<b>外侧</b>（背离圆心方向），让两格缺口保持畅通。</summary>
    private static void PlaceExitTowers(Actor actor, bool circle, int radius)
    {
        var city = actor.city;
        var center = city.getTile();
        var exits = WallShapeHelper.ComputeExitTiles(city, circle, radius);
        if (exits == null) return;
        string tower_id = GetWatchTowerId(city);
        foreach (var tile in exits)
        {
            if (tile == null) continue;
            var spot = FindOutwardLand(tile, center);
            if (spot == null || spot.building != null) continue; // 已有建筑则跳过（不拆除，避免改变城市 zones）
            World.world.buildings.addBuilding(tower_id, spot);
        }
    }

    /// <summary>取 tile 最背离圆心的陆地邻居（用于把箭塔放到缺口外侧、不阻挡通行）；无合适邻居则返回原 tile。</summary>
    private static WorldTile FindOutwardLand(WorldTile t, WorldTile center)
    {
        if (t == null) return null;
        if (center == null) return t;
        int cx = center.x, cy = center.y;
        WorldTile best = t;
        int bestDist = (t.x - cx) * (t.x - cx) + (t.y - cy) * (t.y - cy);
        foreach (var n in t.neighbours)
        {
            if (n == null || n.IsWater()) continue;
            int d = (n.x - cx) * (n.x - cx) + (n.y - cy) * (n.y - cy);
            if (d > bestDist) { bestDist = d; best = n; }
        }
        return best;
    }

    /// <summary>按城市种族选取箭塔 id（watch_tower_human/orc/elf/dwarf），无对应样式则回退人类。</summary>
    private static string GetWatchTowerId(City city)
    {
        const string fallback = "watch_tower_human";
        string race = city?.data?.original_actor_asset;
        if (string.IsNullOrEmpty(race)) return fallback;
        string id = "watch_tower_" + race;
        return AssetManager.buildings.get(id) != null ? id : fallback;
    }

    /// <summary>是否可以推进城墙阶段：未到 FORTRESS 总可推进；到 FORTRESS 后只有墙被毁才允许重建。</summary>
    private static bool CanAdvanceWallStage(City city)
    {
        if (city == null) return false;
        int stage = GetWallStage(city);
        if (stage < WALL_STAGE_FORTRESS) return true;
        return !WallsIntact(city); // 已建成要塞：任一城墙被摧毁大半即可重建
    }

    /// <summary>内墙与外墙是否都基本完好（现存比例均 ≥ 阈值）。</summary>
    private static bool WallsIntact(City city)
    {
        if (city == null) return false;
        bool circle = city.zones.Count >= WALL_CIRCLE_ZONE_THRESHOLD;
        int rInner  = GetCurrentInnerRadius(city);
        int rOuter  = WallShapeHelper.OuterRadiusFromInner(rInner);
        float inner = WallShapeHelper.ExistingWallRatio(city, circle, rInner, 2);
        float outer = WallShapeHelper.ExistingWallRatio(city, circle, rOuter, 2);
        return inner >= WALL_REBUILD_RATIO && outer >= WALL_REBUILD_RATIO;
    }

    private static int GetWallStage(City city)
    {
        if (city?.data == null) return WALL_STAGE_NONE;
        city.data.get(ContentCityDataKeys.CityWallStage_int, out int stage, WALL_STAGE_NONE);
        return stage;
    }

    private static void SetWallStage(City city, int stage)
    {
        if (city?.data == null) return;
        city.data.set(ContentCityDataKeys.CityWallStage_int, stage);
    }

    /// <summary>
    /// 取得当前应使用的内墙半径：已建过墙(stage≥1)则用建墙时记录的值（固定，不随城市扩张变化），否则按当前规模计算。
    /// </summary>
    private static int GetCurrentInnerRadius(City city)
    {
        if (city?.data != null)
        {
            city.data.get(ContentCityDataKeys.CityWallInnerRadius_int, out int stored, 0);
            if (stored > 0) return stored;
        }
        return WallShapeHelper.InnerRadius(city);
    }

    private static void SetInnerRadius(City city, int radius)
    {
        if (city?.data == null) return;
        city.data.set(ContentCityDataKeys.CityWallInnerRadius_int, radius);
    }

    private static WorldTile FindTileForTower(City city, BuildingAsset asset)
    {
        var zones = new List<TileZone>();
        CityBehBuild.fillPossibleZones(asset, city, zones);
        if (zones.Count == 0)
            return null;
        return CityBehBuild.tryToBuildInZones(zones, asset, city, false);
    }

    private static List<City> GetTrainTargets(City city)
    {
        var targets = new List<City>();
        foreach (var target_city in city.neighbours_cities)
        {
            if (
                target_city.kingdom != city.kingdom
                && !target_city.kingdom.isOpinionTowardsKingdomGood(city.kingdom)
            )
                continue;

            var tile = FindTileForTrainStation(target_city);
            if (tile == null)
                continue;
            targets.Add(target_city);
        }
        return targets;
    }
}
