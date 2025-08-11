using System.Collections.Generic;

namespace Cultiway.Core;

public class BuildingAssetExtend
{
    public bool advanced_unit_spawner;
    public AdvancedUnitSpawnerConfig advanced_unit_spawner_config;
}

public class AdvancedUnitSpawnerConfig
{
    public enum SpawnStrategy
    {
        Centralized,
        Distributed
    }

    public SpawnStrategy strategy;
    public SpawnConfig spawn_config;
    public abstract class SpawnConfig
    {
        
    }
    public class DistributedConfig : SpawnConfig
    {
        public List<SingleConfig> single_configs;
        public class SingleConfig
        {
            public string unit_asset_id;
            public int spawn_count;
            public float spawn_interval;
            public int spawn_count_per_interval;
        }
    }

    public class CentralizedConfig : SpawnConfig
    {
        public List<SingleConfig> single_configs;
        public int total_count;
        public float spawn_interval;
        public int spawn_count_per_interval;
        public class SingleConfig
        {
            public string unit_asset_id;
            public float spawn_weight;
        }
    }

}