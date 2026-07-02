using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core.Libraries;
using NeoModLoader.General.Game.extensions;
using strings;
using UnityEngine;

namespace Cultiway.Content;
[Dependency(typeof(Actors), typeof(KingdomAssets))]
public partial class Buildings : ExtendLibrary<BuildingAsset, Buildings>
{
    class CommonBuildingSetupAttribute : Attribute
    {
    }
    [CloneSource(BuildingLibrary.TEMPLATE_CITY_COLORED_BUILDING)]
    public static BuildingAsset TrainStation { get; private set; }
    [CloneSource(SB.hall_human_0)]
    public static BuildingAsset SectHall { get; private set; }
    [CloneSource(SB.library_human)]
    public static BuildingAsset SectScripturePavilion { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        SetupFantasyBuildings();
        SetupEasternHumanBuildings();
        //SetupMingRaceBuildings();
        SetupTrainStation();
        SetupSectBuildings();
    }

    protected override void ActionAfterCreation(PropertyInfo prop, BuildingAsset asset)
    {
        if (prop.GetCustomAttribute<CommonBuildingSetupAttribute>() != null)
        {
        }
    }

    protected override void PostInit(BuildingAsset asset)
    {
        base.PostInit(asset);
        asset.atlas_asset = AssetManager.dynamic_sprites_library.get(asset.atlas_id);
        
        if (asset.step_action != null)
        {
            asset.has_step_action = true;
        }
        asset.has_biome_tags = asset.biome_tags_growth is { Count: > 0 };
        asset.has_biome_tags_spread = asset.biome_tags_spread is { Count: > 0 };
    }
    private void SetupTrainStation()
    {
        TrainStation.has_sprite_construction = false;
        TrainStation.build_place_batch = false;
        TrainStation.priority = 100;
        TrainStation.group = "train_station";
    }

    private void SetupSectBuildings()
    {
        SetupSectBuildingBase(SectHall, SectConst.BuildingTypeHall);
        SectHall.priority = 110;
        SectHall.cost = new ConstructionCost(10, 5, 0, 30);
        SectHall.base_stats["health"] = 300f;

        SetupSectBuildingBase(SectScripturePavilion, SectConst.BuildingTypeScripturePavilion);
        SectScripturePavilion.priority = 90;
        SectScripturePavilion.cost = new ConstructionCost(0, 12, 2, 50);
        SectScripturePavilion.base_stats["health"] = 350f;
    }

    private static void SetupSectBuildingBase(BuildingAsset asset, string type)
    {
        asset.AsSectBuilding(type);
        asset.main_path = "buildings/sects/";
        asset.kingdom = string.Empty;
        asset.civ_kingdom = string.Empty;
        asset.storage = false;
        asset.storage_only_food = false;
        asset.book_slots = 0;
        asset.can_units_live_here = false;
        asset.housing_slots = 0;
        asset.housing_happiness = 0;
        asset.max_houses = 0;
        asset.loot_generation = 0;
        asset.produce_biome_food = false;
        asset.can_be_upgraded = false;
        asset.upgrade_to = string.Empty;
        asset.upgraded_from = string.Empty;
        asset.upgrade_level = 0;
        asset.ignore_other_buildings_for_upgrade = false;
        asset.can_be_living_house = false;
        asset.can_be_living_plant = false;
    }

    private void SetupEasternHumanBuildings()
    {
        void CloneHuman(string building_id)
        {   
            var asset = Clone(building_id.Replace(SA.human, Actors.EasternHuman.id), building_id);
            asset.main_path = $"buildings/civ_main/{Actors.EasternHuman.id}/";
            asset.group = Actors.EasternHuman.id;
            asset.kingdom = KingdomAssets.NoMadsEasternHuman.id;
            asset.civ_kingdom = KingdomAssets.EasternHuman.id;
            asset.upgrade_to = asset.upgrade_to.Replace(SA.human, Actors.EasternHuman.id);
            asset.upgraded_from = asset.upgraded_from.Replace(SA.human, Actors.EasternHuman.id);
            if (asset.docks)
            {
                PortalLibrary.Dock.Buildings.Add(asset);
            }
        }

        void CloneList(params string[] building_ids)
        {
            foreach (var building_id in building_ids)
                CloneHuman(building_id);
        }
        CloneList(
            SB.watch_tower_human, SB.fishing_docks_human, SB.docks_human, SB.barracks_human, SB.temple_human, 
            SB.windmill_human_0, SB.windmill_human_1, 
            SB.tent_human, SB.house_human_0, SB.house_human_1, SB.house_human_2, SB.house_human_3, SB.house_human_4, SB.house_human_5, 
            SB.hall_human_0, SB.hall_human_1, SB.hall_human_2
        );
/*
        AssetManager.buildings.get($"tent_{Races.Ming.id}").fundament = new BuildingFundament(1,        1, 1,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_0").fundament = new BuildingFundament(3,     3, 4,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_1").fundament = new BuildingFundament(3,     3, 4,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_2").fundament = new BuildingFundament(3,     3, 4,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_3").fundament = new BuildingFundament(4,     4, 6,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_4").fundament = new BuildingFundament(5,     5, 9,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_5").fundament = new BuildingFundament(5,     5, 9,  0);
        AssetManager.buildings.get($"hall_{Races.Ming.id}_0").fundament = new BuildingFundament(4,      4, 7,  0);
        AssetManager.buildings.get($"hall_{Races.Ming.id}_1").fundament = new BuildingFundament(5,      5, 9,  0);
        AssetManager.buildings.get($"hall_{Races.Ming.id}_2").fundament = new BuildingFundament(8,      8, 14, 0);
        AssetManager.buildings.get($"temple_{Races.Ming.id}").fundament = new BuildingFundament(3,      3, 5,  0);
        AssetManager.buildings.get($"barracks_{Races.Ming.id}").fundament = new BuildingFundament(3,    3, 7,  0);
        AssetManager.buildings.get($"windmill_{Races.Ming.id}_0").fundament = new BuildingFundament(2,  1, 2,  0);
        AssetManager.buildings.get($"windmill_{Races.Ming.id}_1").fundament = new BuildingFundament(2,  2, 2,  0);
        AssetManager.buildings.get($"watch_tower_{Races.Ming.id}").fundament = new BuildingFundament(2, 2, 3,  0);
        */
    }
/*
    private void SetupMingRaceBuildings()
    {
        Clone($"bonfire_{Actors.Ming.id}", SB.bonfire);
        t.main_path = "buildings/civ_main/ming";
        t.group = Actors.Ming.id;
        t.civ_kingdom = KingdomAssets.Ming.id;

        
        void CloneHuman(string building_id)
        {
            var asset = Clone(building_id.Replace(SA.human, Actors.Ming.id), building_id);
            asset.main_path = "buildings/civ_main/ming";
            asset.group = Actors.Ming.id;
            asset.civ_kingdom = KingdomAssets.Ming.id;
            asset.upgrade_to = asset.upgrade_to.Replace(SA.human, Actors.Ming.id);
            asset.upgraded_from = asset.upgraded_from.Replace(SA.human, Actors.Ming.id);
        }

        void CloneList(params string[] building_ids)
        {
            foreach (var building_id in building_ids)
                CloneHuman(building_id);
        }
        CloneList(
            SB.watch_tower_human, SB.fishing_docks_human, SB.docks_human, SB.barracks_human, SB.temple_human, 
            SB.windmill_human_0, SB.windmill_human_1, 
            SB.tent_human, SB.house_human_0, SB.house_human_1, SB.house_human_2, SB.house_human_3, SB.house_human_4, SB.house_human_5, 
            SB.hall_human_0, SB.hall_human_1, SB.hall_human_2
        );
/*
        AssetManager.buildings.get($"tent_{Races.Ming.id}").fundament = new BuildingFundament(1,        1, 1,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_0").fundament = new BuildingFundament(3,     3, 4,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_1").fundament = new BuildingFundament(3,     3, 4,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_2").fundament = new BuildingFundament(3,     3, 4,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_3").fundament = new BuildingFundament(4,     4, 6,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_4").fundament = new BuildingFundament(5,     5, 9,  0);
        AssetManager.buildings.get($"house_{Races.Ming.id}_5").fundament = new BuildingFundament(5,     5, 9,  0);
        AssetManager.buildings.get($"hall_{Races.Ming.id}_0").fundament = new BuildingFundament(4,      4, 7,  0);
        AssetManager.buildings.get($"hall_{Races.Ming.id}_1").fundament = new BuildingFundament(5,      5, 9,  0);
        AssetManager.buildings.get($"hall_{Races.Ming.id}_2").fundament = new BuildingFundament(8,      8, 14, 0);
        AssetManager.buildings.get($"temple_{Races.Ming.id}").fundament = new BuildingFundament(3,      3, 5,  0);
        AssetManager.buildings.get($"barracks_{Races.Ming.id}").fundament = new BuildingFundament(3,    3, 7,  0);
        AssetManager.buildings.get($"windmill_{Races.Ming.id}_0").fundament = new BuildingFundament(2,  1, 2,  0);
        AssetManager.buildings.get($"windmill_{Races.Ming.id}_1").fundament = new BuildingFundament(2,  2, 2,  0);
        AssetManager.buildings.get($"watch_tower_{Races.Ming.id}").fundament = new BuildingFundament(2, 2, 3,  0);
    }*/
}
