using Cultiway.Core;
using Cultiway.Core.BuildingComponents;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Extensions;

public static class BuildingAssetTools
{
    /// <summary>
    /// 设置属性，属性id从"S."下面找
    /// </summary>
    /// <param name="asset"></param>
    /// <param name="id"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static BuildingAsset Stats(this BuildingAsset asset, string id, float value)
    {
        asset.base_stats[id] = value;
        return asset;
    }
    /// <summary>
    /// 建筑能否放置在液体上
    /// </summary>
    public static BuildingAsset PlaceOnLiquid(this BuildingAsset asset, bool value)
    {
        asset.can_be_placed_on_liquid = true;
        asset.destroy_on_liquid = false;
        return asset;
    }
    /// <summary>
    /// 建筑被完全移除时执行的动作
    /// </summary>
    public static BuildingAsset ActionOnRemoved(this BuildingAsset asset, WorldAction action)
    {
        asset.GetExtend<BuildingAssetExtend>().action_on_removed += action;
        return asset;
    }
    /// <summary>
    /// 建筑变成废墟时执行的动作
    /// </summary>
    public static BuildingAsset ActionOnRuins(this BuildingAsset asset, WorldAction action)
    {
        asset.GetExtend<BuildingAssetExtend>().action_on_ruins += action;
        return asset;
    }

    /// <summary>
    /// 高级召唤中心化整体配置
    /// </summary>
    /// <param name="interval">召唤间隔</param>
    /// <param name="max_count">总召唤物上限</param>
    /// <param name="count_per_spawn">每次召唤的数量</param>
    /// <returns></returns>
    public static BuildingAsset SetAdvancedSpawnerCentralizedConfig(this BuildingAsset asset, float interval = 3.0f, int max_count = 5, int count_per_spawn = 1)
    {
        var bae = asset.GetExtend<BuildingAssetExtend>();
        if (!bae.advanced_unit_spawner)
        {
            bae.advanced_unit_spawner = true;
            bae.advanced_unit_spawner_config = new AdvancedUnitSpawnerConfig()
            {
                strategy = AdvancedUnitSpawnerConfig.SpawnStrategy.Centralized,
                spawn_config = new AdvancedUnitSpawnerConfig.CentralizedConfig()
            };
        }
        else if (bae.advanced_unit_spawner_config.strategy != AdvancedUnitSpawnerConfig.SpawnStrategy.Centralized)
        {
            ModClass.LogError($"{asset.id} already has a spawner config, but it's not centralized. Ignore it.");
            return asset;
        }

        var cfg =
            ((AdvancedUnitSpawnerConfig.CentralizedConfig)bae.advanced_unit_spawner_config.spawn_config);
        cfg.total_count = max_count;
        cfg.spawn_interval = interval;
        cfg.spawn_count_per_interval = count_per_spawn;
        return asset;
    }
    /// <summary>
    /// 添加一种召唤物(仅限中心化的召唤塔)
    /// </summary>
    /// <param name="unit_asset">目标生物的asset</param>
    /// <param name="weight">权重</param>
    /// <returns></returns>
    public static BuildingAsset AddAdvancedSpawnerCentralizedConfig(this BuildingAsset asset, ActorAsset unit_asset, float weight = 1.0f)
    {
        var bae = asset.GetExtend<BuildingAssetExtend>();
        if (!bae.advanced_unit_spawner)
        {
            bae.advanced_unit_spawner = true;
            bae.advanced_unit_spawner_config = new AdvancedUnitSpawnerConfig()
            {
                strategy = AdvancedUnitSpawnerConfig.SpawnStrategy.Centralized,
                spawn_config = new AdvancedUnitSpawnerConfig.CentralizedConfig()
            };
        }
        else if (bae.advanced_unit_spawner_config.strategy != AdvancedUnitSpawnerConfig.SpawnStrategy.Centralized)
        {
            ModClass.LogError($"{asset.id} already has a spawner config, but it's not centralized. Ignore it.");
            return asset;
        }
        ((AdvancedUnitSpawnerConfig.CentralizedConfig)bae.advanced_unit_spawner_config.spawn_config).single_configs.Add(new AdvancedUnitSpawnerConfig.CentralizedConfig.SingleConfig()
        {
            unit_asset_id = unit_asset.id,
            spawn_weight = weight
        });
        return asset;
    }
    /// <summary>
    /// 添加一种召唤物(仅限分布化)
    /// </summary>
    /// <param name="unit_asset">召唤物asset</param>
    /// <param name="max_count">该生物召唤上限</param>
    /// <param name="spawn_interval">召唤间隔</param>
    /// <param name="count_per_spawn">单次召唤数量</param>
    /// <returns></returns>
    public static BuildingAsset AddAdvancedSpawnerDistributedConfig(this BuildingAsset asset, ActorAsset unit_asset,
        int max_count = 5, float spawn_interval = 3.0f, int count_per_spawn = 1)
    {
        var bae = asset.GetExtend<BuildingAssetExtend>();
        if (!bae.advanced_unit_spawner)
        {
            bae.advanced_unit_spawner = true;
            bae.advanced_unit_spawner_config = new AdvancedUnitSpawnerConfig()
            {
                strategy = AdvancedUnitSpawnerConfig.SpawnStrategy.Distributed,
                spawn_config = new AdvancedUnitSpawnerConfig.DistributedConfig()
            };
        }
        else if (bae.advanced_unit_spawner_config.strategy != AdvancedUnitSpawnerConfig.SpawnStrategy.Distributed)
        {
            ModClass.LogError($"{asset.id} already has a spawner config, but it's not distributed. Ignore it.");
            return asset;
        }
        ((AdvancedUnitSpawnerConfig.DistributedConfig)bae.advanced_unit_spawner_config.spawn_config).single_configs.Add(new AdvancedUnitSpawnerConfig.DistributedConfig.SingleConfig()
        {
            unit_asset_id = unit_asset.id,
            spawn_count = max_count,
            spawn_count_per_interval = count_per_spawn,
            spawn_interval = spawn_interval
        });
        return asset;
    }
}