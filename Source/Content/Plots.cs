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
                foreach (var (tile, direction) in path)
                {
                    MapAction.terraformTop(tile, TopTileTypes.TrainTrack, WorldboxGame.Terraforms.Road, false);
                }
                any_built = true;
            }
            return any_built;
        };
    }

    enum TrainTrackDirection
    {
        LR,
        UD,
        LU,
        LD,
        RU,
        RD
    }

    private static List<(WorldTile, TrainTrackDirection)> GetTrainTrackDirection(
        WorldTile source_tile,
        WorldTile target_tile
    )
    {
        // 实现从source_tile到target_tile的路径, 返回每一步的WorldTile和对应的TrainTrackDirection
        var path = new List<(WorldTile, TrainTrackDirection)>();

        // 检查参数有效性
        if (source_tile == null || target_tile == null)
            return path;

        // 获取起点和终点坐标
        int x0 = source_tile.x;
        int y0 = source_tile.y;
        int x1 = target_tile.x;
        int y1 = target_tile.y;

        int dx = x1 - x0;
        int dy = y1 - y0;

        int signX = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
        int signY = dy == 0 ? 0 : (dy > 0 ? 1 : -1);

        // 判断主方向（横向或纵向主导）
        int px = x0;
        int py = y0;
        TrainTrackDirection dir;

        // 简单实现：每格横向走，再纵向走（或反之），每步都记录方向
        // 优化：斜向尽可能用转角（即LU、LD、RU、RD）
        while (px != x1 || py != y1)
        {
            int nx = px,
                ny = py;
            // 优先斜向移动
            if (px != x1 && py != y1)
            {
                nx = px + signX;
                ny = py + signY;
                if (signX > 0 && signY > 0)
                    dir = TrainTrackDirection.RD;
                else if (signX > 0 && signY < 0)
                    dir = TrainTrackDirection.RU;
                else if (signX < 0 && signY > 0)
                    dir = TrainTrackDirection.LD;
                else
                    dir = TrainTrackDirection.LU;
            }
            else if (px != x1)
            {
                nx = px + signX;
                ny = py;
                dir = TrainTrackDirection.LR;
            }
            else // py != y1
            {
                nx = px;
                ny = py + signY;
                dir = TrainTrackDirection.UD;
            }

            WorldTile nextTile = World.world.GetTile(nx, ny);
            if (nextTile == null)
                break;
            path.Add((nextTile, dir));
            px = nx;
            py = ny;
        }

        return path;
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
