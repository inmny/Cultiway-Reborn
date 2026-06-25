using System;
using System.Collections.Generic;
using ai.behaviours;
using Cultiway.Abstract;
using Cultiway.Content.Components;
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
