using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cultiway.Abstract;
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

    protected override void OnInit()
    {
        RegisterAssets();
        SetupFantasyBuildings();
        SetupEasternHumanBuildings();
        //SetupMingRaceBuildings();
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
    private void SetupEasternHumanBuildings()
    {
        void CloneHuman(string building_id)
        {   
            var asset = Clone(building_id.Replace(SA.human, Actors.EasternHuman.id), building_id);
            asset.main_path = $"buildings/civ_main/{Actors.EasternHuman.id}/";
            asset.group = Actors.EasternHuman.id;
            asset.civ_kingdom = KingdomAssets.EasternHuman.id;
            asset.upgrade_to = asset.upgrade_to.Replace(SA.human, Actors.EasternHuman.id);
            asset.upgraded_from = asset.upgraded_from.Replace(SA.human, Actors.EasternHuman.id);
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