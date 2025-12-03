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
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        NewCultibook.path_icon = "books/custom_book_covers/cultibook/31";
        NewCultibook.check_is_possible = (Actor actor) => actor.hasCity() && actor.hasCulture() && actor.hasLanguage() && actor.city.hasBookSlots() && actor.GetExtend().HasCultisys<Xian>();
        NewCultibook.check_should_continue = (Actor actor) => actor.hasCity() && actor.hasCulture() && actor.hasLanguage() && actor.city.hasBookSlots() && actor.GetExtend().HasCultisys<Xian>();
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
            if (!a.GetExtend().HasCultibook()) return false;
            if (a.HasSect()) return false;

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
                if (target_city.leader.isRekt()) continue;
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
                if (aa.city.isRekt()) continue;
                if (aa.kingdom != a.kingdom && !aa.kingdom.isOpinionTowardsKingdomGood(a.kingdom)) continue;
                if (aa != aa.city.leader) continue;
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
    }
    private static WorldTile FindTileForTrainStation(City city)
    {
        var zones = new List<TileZone>();
        CityBehBuild.fillPossibleZones(Buildings.TrainStation, city, zones);
        if (zones.Count == 0) return null;
        return CityBehBuild.tryToBuildInZones(zones, Buildings.TrainStation, city, false);
    }
    private static List<City> GetTrainTargets(City city)
    {
        var targets = new List<City>();
        foreach (var target_city in city.neighbours_cities)
        {
            if (target_city.kingdom != city.kingdom && !target_city.kingdom.isOpinionTowardsKingdomGood(city.kingdom)) continue;

            var tile = FindTileForTrainStation(target_city);
            if (tile == null) continue;
            targets.Add(target_city);
        }
        return targets;
    }
}