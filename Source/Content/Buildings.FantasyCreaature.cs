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
    [SetupButton, CloneSource(SB.flame_tower)]
    public static BuildingAsset VampireTower { get; private set; }
    [SetupButton, CloneSource(SB.flame_tower)]
    public static BuildingAsset AdvancedVampireTower { get; private set; }
    private void SetupFantasyBuildings()
    {
        VampireTower.tower = false;
        VampireTower.spawn_units_asset = Actors.Bloodsucker.id;
        VampireTower.kingdom = KingdomAssets.Vampire.id;


        AdvancedVampireTower.tower = false;
        AdvancedVampireTower.kingdom = KingdomAssets.Vampire.id;
        // 这边配置高级召唤塔, 分为中心化和分布化两种策略
        // 中心化，所有生物共享同一个召唤上限/间隔/单次召唤数量，使用权重控制每个生物的生成概率
        // 分布化，每种生物单独使用一个召唤上线/间隔/单词召唤数量
        // 只能二选一
/*
        AdvancedVampireTower.SetAdvancedSpawnerCentralizedConfig(1, 10, 3)
                        .AddAdvancedSpawnerCentralizedConfig(Actors.Bloodsucker, 10)
                        .AddAdvancedSpawnerCentralizedConfig(Actors.Anubis, 1); // 中心化的
                        */
        AdvancedVampireTower.AddAdvancedSpawnerDistributedConfig(Actors.Bloodsucker, 10, 1, 3)
                        .AddAdvancedSpawnerDistributedConfig(Actors.Anubis, 3, 10, 1); // 分布化的
    }
}