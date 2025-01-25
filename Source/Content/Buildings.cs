using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using NeoModLoader.General.Game.extensions;
using UnityEngine;

namespace Cultiway.Content;
[Dependency(typeof(Races))]
public class Buildings : ExtendLibrary<BuildingAsset, Buildings>
{
    protected override void OnInit()
    {
        RegisterAssets("Cultiway.Building");
        SetupMingRaceBuildings();
    }

    private void SetupMingRaceBuildings()
    {
        clone_human_buildings(Races.Ming.id);
        
        
        BuildingAsset bonfire = AssetManager.buildings.clone($"bonfire_{Races.Ming.id}", "bonfire");
        bonfire.race = Races.Ming.id;
        AssetManager.buildings.loadSprites(bonfire);

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
    }
    
    
    private void clone_human_buildings(string race)
    {
        var building_assets = AssetManager.buildings.list.Where(x => x.race == SK.human).ToList();
        foreach (var building_asset in building_assets)
        {
            var test_path = building_asset.sprite_path;
            if (string.IsNullOrEmpty(test_path))
            {
                test_path = "buildings/" + building_asset.id.Replace(SK.human, race);
                if (SpriteTextureLoader.getSpriteList(test_path) is not { Length: > 0 })
                {
                    test_path = $"buildings/{building_asset.id}";
                }
            }

            BuildingAsset new_building =
                Clone(building_asset.id.Replace(SK.human, race),
                    building_asset.id);
            ModClass.LogInfo($"({GetType().Name}) Initializes {new_building.id}");

            new_building.race = race;
            new_building.sprite_path = test_path;

            if (building_asset.canBeUpgraded)
                new_building.upgradeTo = new_building.upgradeTo.Replace(SK.human, race);

            if (!string.IsNullOrEmpty(new_building.upgradedFrom))
                new_building.upgradedFrom =
                    new_building.upgradedFrom.Replace(SK.human, race);

            AssetManager.buildings.loadSprites(new_building);
        }/*
        AssetManager.buildings.ForEach<BuildingAsset, BuildingLibrary>(building_asset =>
        {
            if (building_asset.race != SK.human) return;
            
            var test_path = building_asset.sprite_path;
            if (string.IsNullOrEmpty(test_path))
            {
                test_path = "buildings/" + building_asset.id.Replace(SK.human, race);
                if (SpriteTextureLoader.getSpriteList(test_path) is not { Length: > 0 })
                {
                    test_path = $"buildings/{building_asset.id}";
                }
            }

            BuildingAsset new_building =
                AssetManager.buildings.clone(building_asset.id.Replace(SK.human, race),
                    building_asset.id);

            new_building.race = race;
            new_building.sprite_path = test_path;

            if (building_asset.canBeUpgraded)
                new_building.upgradeTo = new_building.upgradeTo.Replace(SK.human, race);

            if (!string.IsNullOrEmpty(new_building.upgradedFrom))
                new_building.upgradedFrom =
                    new_building.upgradedFrom.Replace(SK.human, race);

            AssetManager.buildings.loadSprites(new_building);
        });*/
    }
}