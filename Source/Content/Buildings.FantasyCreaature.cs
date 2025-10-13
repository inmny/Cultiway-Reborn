using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Attributes;
using Cultiway.Content.Extensions;
using NeoModLoader.General.Game.extensions;
using strings;
using UnityEngine;

namespace Cultiway.Content;

public partial class Buildings
{
   // [SetupButton, CloneSource(SB.flame_tower)]
   // public static BuildingAsset VampireTower { get; private set; }
   // [SetupButton, CloneSource(SB.flame_tower)]
   // public static BuildingAsset AdvancedVampireTower { get; private set; }
    [SetupButton, CloneSource(SB.flame_tower)]
    public static BuildingAsset BloodCastle { get; private set; }
    [SetupButton, CloneSource(SB.flame_tower)]
    public static BuildingAsset FishPeopleTower { get; private set; }
    [SetupButton, CloneSource(SB.flame_tower)]
    public static BuildingAsset GoblinTower { get; private set; }
    [SetupButton, CloneSource(SB.flame_tower)]
    public static BuildingAsset MagicTower { get; private set; }
    [SetupButton, CloneSource(SB.flame_tower)]
    public static BuildingAsset Pyramid { get; private set; }
    [SetupButton, CloneSource(SB.flame_tower)]
    public static BuildingAsset RobotTower { get; private set; }
    [SetupButton, CloneSource(SB.flame_tower)]
    public static BuildingAsset SpriteTower { get; private set; }
    private void SetupFantasyBuildings()
    {
        //VampireTower.tower = false;
        //VampireTower.spawn_units_asset = Actors.Bloodsucker.id;
        //VampireTower.kingdom = KingdomAssets.Vampire.id;


        //AdvancedVampireTower.tower = false;
       // AdvancedVampireTower.kingdom = KingdomAssets.Vampire.id;
        // 这边配置高级召唤塔, 分为中心化和分布化两种策略
        // 中心化，所有生物共享同一个召唤上限/间隔/单次召唤数量，使用权重控制每个生物的生成概率
        // 分布化，每种生物单独使用一个召唤上线/间隔/单词召唤数量
        // 只能二选一
        /*
                AdvancedVampireTower.SetAdvancedSpawnerCentralizedConfig(1, 10, 3)
                                .AddAdvancedSpawnerCentralizedConfig(Actors.Bloodsucker, 10)
                                .AddAdvancedSpawnerCentralizedConfig(Actors.Anubis, 1); // 中心化的
                                */
       // AdvancedVampireTower.AddAdvancedSpawnerDistributedConfig(Actors.Bloodsucker, 10, 1, 3)
                       // .AddAdvancedSpawnerDistributedConfig(Actors.Anubis, 3, 10, 1); // 分布化的

        BloodCastle.tower = false;
        BloodCastle.spawn_units_asset = null;
        BloodCastle.kingdom = KingdomAssets.Vampire.id;
        BloodCastle.AddAdvancedSpawnerDistributedConfig(Actors.Bloodsucker, 1, 6, 1)
                   .AddAdvancedSpawnerDistributedConfig(Actors.BloodBeast, 3, 4, 1)
                   .AddAdvancedSpawnerDistributedConfig(Actors.Bloodthirsty, 6, 2, 2);
        FishPeopleTower.tower = false;
        FishPeopleTower.spawn_units_asset = null;
        FishPeopleTower.kingdom = KingdomAssets.FishPeople.id;
        FishPeopleTower.AddAdvancedSpawnerDistributedConfig(Actors.FishPeopleShaman, 1, 6, 1)
                   .AddAdvancedSpawnerDistributedConfig(Actors.FishPeopleWarrior, 3, 4, 1)
                   .AddAdvancedSpawnerDistributedConfig(Actors.FishPeopleSoldiers, 6, 2, 2);
        RobotTower.tower = true;
        RobotTower.spawn_units_asset = null;
        RobotTower.kingdom = KingdomAssets.Robot.id;
        RobotTower.AddAdvancedSpawnerDistributedConfig(Actors.DestroyRobot, 1, 6, 1)
                   .AddAdvancedSpawnerDistributedConfig(Actors.TankRobot, 3, 4, 1)
                   .AddAdvancedSpawnerDistributedConfig(Actors.FortRobot, 6, 2, 2);
        SpriteTower.tower = false;
        SpriteTower.spawn_units_asset = null;
        SpriteTower.kingdom = KingdomAssets.Fairy.id;
        SpriteTower.AddAdvancedSpawnerDistributedConfig(Actors.FairyDruid, 1, 6, 1)
                   .AddAdvancedSpawnerDistributedConfig(Actors.FairyWarrior, 3, 4, 1)
                   .AddAdvancedSpawnerDistributedConfig(Actors.FairyRanger, 6, 2, 2);
        GoblinTower.tower = false;
        GoblinTower.spawn_units_asset = null;
        GoblinTower.kingdom = KingdomAssets.Goblin.id;
        GoblinTower.AddAdvancedSpawnerDistributedConfig(Actors.GoblinShaman, 1, 6, 1)
                   .AddAdvancedSpawnerDistributedConfig(Actors.GoblinKnight, 3, 4, 1)
                   .AddAdvancedSpawnerDistributedConfig(Actors.GoblinWarrior, 6, 2, 2);
        Pyramid.tower = false;
        Pyramid.spawn_units_asset = null;
        Pyramid.kingdom = KingdomAssets.Undead.id;
        Pyramid.AddAdvancedSpawnerDistributedConfig(Actors.Pharaoh, 1, 6, 1)
                   .AddAdvancedSpawnerDistributedConfig(Actors.SkeletonKnight, 4, 4, 1)
                   .AddAdvancedSpawnerDistributedConfig(Actors.Mummy, 6, 2, 2);
        MagicTower.tower = false;
        MagicTower.spawn_units_asset = null;
        MagicTower.kingdom = KingdomAssets.Superman.id;
        MagicTower.AddAdvancedSpawnerDistributedConfig(Actors.Sorcerer, 1, 6, 1)
                   .AddAdvancedSpawnerDistributedConfig(Actors.GriffinKnight, 4, 4, 1) 
                   .AddAdvancedSpawnerDistributedConfig(Actors.GuardKnight, 6, 2, 2);
    }
}