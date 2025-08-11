using System;
using System.Text;
using Cultiway.Const;

namespace Cultiway.Core.BuildingComponents;

public class AdvancedUnitSpawner : BaseBuildingComponent
{
    private AdvancedUnitSpawnerConfig _config;
    public void Setup(AdvancedUnitSpawnerConfig config)
    {
        _config = config;
    }

    private int CheckCentralizedAliveCount()
    {
        building.data.get(BuildingDataKeys.AdvancedSpawnerCentralizedAliveList_str, out string list);
        var sb = new StringBuilder();
        int count = 0;
        int old_count = 0;
        foreach (var raw_id in list.Split(','))
        {
            if (string.IsNullOrEmpty(raw_id)) continue;
            var id = long.Parse(raw_id);
            var unit = World.world.units.get(id);

            old_count++;
            if (unit == null) continue;
            if (unit.isAlive())
            {
                sb.Append(id).Append(',');
                count++;
            }
        }
        if (count == old_count) return count;
        list = sb.ToString();
        building.data.set(BuildingDataKeys.AdvancedSpawnerCentralizedAliveList_str, list);
        return count;
    }

    private bool CheckDistributedInterval(int idx, float elapsed, float interval)
    {
        var key = BuildingDataKeys.AdvancedSpawnerDistributedTimerPrefix_float + idx;
        building.data.get(key, out float timer);
        bool res = timer <= 0f;
        if (res)
        {
            timer = interval;
        }
        building.data.set(key, timer - elapsed);
        return res;
    }

    private int CheckDistributedAliveCount(int idx)
    {
        var key = BuildingDataKeys.AdvancedSpawnerDistributedAliveListPrefix_str + idx;
        building.data.get(key, out string list);
        var sb = new StringBuilder();
        int count = 0;
        int old_count = 0;
        foreach (var raw_id in list.Split(','))
        {
            if (string.IsNullOrEmpty(raw_id)) continue;
            var id = long.Parse(raw_id);
            var unit = World.world.units.get(id);

            old_count++;
            if (unit == null) continue;
            if (unit.isAlive())
            {
                sb.Append(id).Append(',');
                count++;
            }
        }
        if (count == old_count) return count;
        list = sb.ToString();
        building.data.set(key, list);
        return count;
    }
    
    private void CentralizedSpawnUnit(string id)
    {
        var unit = World.world.units.spawnNewUnit(id, building.door_tile);
        building.data.get(BuildingDataKeys.AdvancedSpawnerCentralizedAliveList_str, out string list);
        list += unit.data.id.ToString() + ',';
        building.data.set(BuildingDataKeys.AdvancedSpawnerCentralizedAliveList_str, list);
    }
    private void DistributedSpawnUnit(int idx, string id)
    {
        var key = BuildingDataKeys.AdvancedSpawnerDistributedAliveListPrefix_str + idx;
        var unit = World.world.units.spawnNewUnit(id, building.door_tile);
        building.data.get(key, out string list);
        list += unit.data.id.ToString() + ',';
        building.data.set(key, list);
    }
    public override void update(float pElapsed)
    {
        base.update(pElapsed);
        var spawner = building;
        switch (_config.strategy)
        {
            case AdvancedUnitSpawnerConfig.SpawnStrategy.Centralized:
            {
                var spawn_config = (AdvancedUnitSpawnerConfig.CentralizedConfig)_config.spawn_config;
                spawner.data.get(BuildingDataKeys.AdvancedSpawnerCentralizedTimer_float, out float timer);
                if (timer <= 0f)
                {
                    var current_count = CheckCentralizedAliveCount();
                    var count_to_spawn = Math.Min(spawn_config.spawn_count_per_interval, spawn_config.total_count - current_count);
                    if (count_to_spawn > 0)
                    {
                        float total_weight = 0f;
                        foreach (var cfg in spawn_config.single_configs)
                        {
                            total_weight += cfg.spawn_weight;
                        }
                        for (int i = 0; i < count_to_spawn; i++)
                        {
                            var val = Randy.randomFloat(0, total_weight);
                            var accum_weight = 0f;
                            foreach (var cfg in spawn_config.single_configs)
                            {
                                accum_weight += cfg.spawn_weight;
                                if (val < accum_weight)
                                {
                                    CentralizedSpawnUnit(cfg.unit_asset_id);
                                    break;
                                }
                            }
                        }
                    }
                    timer = spawn_config.spawn_interval;
                }
                spawner.data.set(BuildingDataKeys.AdvancedSpawnerCentralizedTimer_float, timer - pElapsed);
                break;
            }
            case AdvancedUnitSpawnerConfig.SpawnStrategy.Distributed:
            {
                var spawn_config = (AdvancedUnitSpawnerConfig.DistributedConfig)_config.spawn_config;
                for (int i = 0; i < spawn_config.single_configs.Count; i++)
                {
                    var cfg = spawn_config.single_configs[i];
                    if (CheckDistributedInterval(i, pElapsed, cfg.spawn_interval))
                    {
                        var current_count = CheckDistributedAliveCount(i);
                        var count_to_spawn = Math.Min(cfg.spawn_count_per_interval, cfg.spawn_count - current_count);
                        for (int j = 0; j < count_to_spawn; j++)
                        {
                            DistributedSpawnUnit(i, cfg.unit_asset_id);
                        }
                    }
                }
                break;
            }
        }
    }
}