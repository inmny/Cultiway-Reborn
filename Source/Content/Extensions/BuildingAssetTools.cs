using Cultiway.Core;
using Cultiway.Core.BuildingComponents;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Extensions;

public static class BuildingAssetTools
{
    public static BuildingAsset SetAdvanceSpawnerStrategy(this BuildingAsset asset,
        AdvancedUnitSpawnerConfig.SpawnStrategy strategy)
    {
        var bae = asset.GetExtend<BuildingAssetExtend>();
        bae.advanced_unit_spawner = true;
        bae.advanced_unit_spawner_config = new AdvancedUnitSpawnerConfig()
        {
            strategy = strategy,
            spawn_config = strategy switch
            {
                AdvancedUnitSpawnerConfig.SpawnStrategy.Centralized =>
                    new AdvancedUnitSpawnerConfig.CentralizedConfig(),
                AdvancedUnitSpawnerConfig.SpawnStrategy.Distributed =>
                    new AdvancedUnitSpawnerConfig.DistributedConfig(),
            }
        };
        return asset;
    }

    public static BuildingAsset SetAdvancedSpawnerConfig(this BuildingAsset asset, float interval = 3.0f, int max_count = 5, int count_per_spawn = 1)
    {
        var bae = asset.GetExtend<BuildingAssetExtend>();
        if (!bae.advanced_unit_spawner)
        {
            bae.advanced_unit_spawner = true;
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
    public static BuildingAsset AddAdvancedSpawnerCentralizedConfig(this BuildingAsset asset, ActorAsset unit_asset, float weight = 1.0f)
    {
        var bae = asset.GetExtend<BuildingAssetExtend>();
        if (!bae.advanced_unit_spawner)
        {
            bae.advanced_unit_spawner = true;
        }
        else if (bae.advanced_unit_spawner_config.strategy != AdvancedUnitSpawnerConfig.SpawnStrategy.Centralized)
        {
            ModClass.LogError($"{asset.id} already has a spawner config, but it's not centralized. Ignore it.");
            return asset;
        }
        ((AdvancedUnitSpawnerConfig.CentralizedConfig)bae.advanced_unit_spawner_config.spawn_config).single_configs.Add(new AdvancedUnitSpawnerConfig.CentralizedConfig.SingleConfig()
        {
            unit_asset_id = unit_asset.id, spawn_weight = weight
        });
        return asset;
    }

    public static BuildingAsset AddAdvancedSpawnerDistributedConfig(this BuildingAsset asset, ActorAsset unit_asset,
        int max_count = 5, float spawn_interval = 3.0f, int count_per_spawn = 1)
    {
        var bae = asset.GetExtend<BuildingAssetExtend>();
        if (!bae.advanced_unit_spawner)
        {
            bae.advanced_unit_spawner = true;
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