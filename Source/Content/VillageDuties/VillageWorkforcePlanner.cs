using System;
using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Content.VillageDuties;

/// <summary>按普通劳动力、人口库存和真实工作条件计算各职业人数。</summary>
internal static class VillageWorkforcePlanner
{
    /// <summary>一种自动职业能容纳的人数及当前缺货权重。</summary>
    private sealed class Demand
    {
        /// <summary>原版职业编号。</summary>
        public string Id;
        /// <summary>当前资源点或设施允许安排的人数。</summary>
        public int Capacity;
        /// <summary>库存越紧缺，分配权重越高。</summary>
        public double Weight;
    }

    /// <summary>先预留手动人数，再按缺货权重分配剩余劳动力，不把田块数当作员工数。</summary>
    /// <param name="city">提供人口、仓库和实际任务条件的村庄。</param>
    /// <param name="workers">具有劳动资格的普通工人数。</param>
    /// <param name="manual">玩家手动填写的职业目标。</param>
    internal static Dictionary<string, int> Create(City city, int workers, IReadOnlyDictionary<string, int> manual)
    {
        var targets = new Dictionary<string, int>();
        var demands = new List<Demand>();
        int population = Math.Max(1, city.status.population);
        double food = Urgency(city.getTotalFood(), Math.Max(40, population * 2));
        double wood = Urgency(city.getResourcesAmount("wood"), Math.Max(30, population / 3));
        double stone = Urgency(city.getResourcesAmount("stone"), Math.Max(20, population / 4));
        bool storage = city.hasStorageBuilding();
        foreach (var job in VillageOccupationService.Jobs)
        {
            if (manual.TryGetValue(job.id, out int fixedCount)) { targets[job.id] = fixedCount; continue; }
            int capacity = city.jobs.countCurrentJobs(job);
            double weight = 1;
            switch (job.id)
            {
                case "builder": capacity = city.hasBuildingToBuild() ? 3 : 0; weight = 2; break;
                case "gatherer_bushes": capacity = storage ? city.tasks.bushes : 0; weight = food; break;
                case "gatherer_herbs":
                    capacity = storage ? city.tasks.plants : 0;
                    weight = Urgency(city.getResourcesAmount("herbs"), Math.Max(10, population / 20)); break;
                case "gatherer_honey": capacity = storage ? city.tasks.hives : 0; weight = food; break;
                case "farmer":
                    capacity = FamilyFarming.FamilyFarmService.WorkCapacity(city, workers);
                    if (food == 1) capacity = Math.Min(capacity, Math.Max(1, workers / 5));
                    weight = food * 2; break;
                case "hunter": capacity = storage ? Math.Max(1, Math.Min(4, workers / 50)) : 0; weight = food; break;
                case "woodcutter":
                    capacity = storage && city.hasSpaceForResourceInStockpile(ResourceLibrary.wood)
                        ? Math.Max(city.tasks.trees, Math.Max(1, workers / 10)) : 0;
                    if (wood == 1) capacity = Math.Min(capacity, Math.Max(1, workers / 30));
                    weight = wood * 1.5; break;
                case "miner":
                    capacity = city.countBuildingsType("type_mine") * 10;
                    if (stone == 1) capacity = Math.Min(capacity, Math.Max(1, workers / 40));
                    weight = stone * 1.5; break;
                case "miner_deposit":
                    capacity = storage ? city.tasks.minerals * 2 : 0;
                    if (stone == 1) capacity = Math.Min(capacity, Math.Max(1, workers / 40));
                    weight = stone; break;
                case "road_builder": capacity = Math.Min(city.road_tiles_to_build.Count, Math.Max(1, workers / 25)); weight = .5; break;
                case "cleaner": capacity = Math.Min(city.tasks.ruins, Math.Max(1, workers / 50)); weight = .5; break;
                case "manure_cleaner": capacity = storage ? Math.Min(city.tasks.poops, Math.Max(1, workers / 40)) : 0; weight = 1; break;
            }
            targets[job.id] = 0;
            if (capacity > 0 && workers > 0) demands.Add(new Demand { Id = job.id, Capacity = Math.Min(workers, capacity), Weight = weight });
        }
        int remaining = (int)Math.Max(0L, workers - manual.Values.Sum(n => (long)n));
        // 可用职业先保留一个位置，再按权重分配，达到设施容量后让出余量。
        foreach (var demand in demands.OrderByDescending(d => d.Weight))
        {
            if (remaining == 0) break;
            targets[demand.Id] = 1; remaining--;
        }
        while (remaining > 0)
        {
            var open = demands.Where(d => targets[d.Id] < d.Capacity).ToArray();
            if (open.Length == 0) break;
            double total = open.Sum(d => d.Weight);
            int budget = remaining;
            foreach (var demand in open)
            {
                int share = Math.Min(demand.Capacity - targets[demand.Id], (int)Math.Floor(budget * demand.Weight / total));
                targets[demand.Id] += share; remaining -= share;
            }
            if (remaining == budget)
            {
                var next = open.OrderByDescending(d => d.Weight / (targets[d.Id] + 1d)).First();
                targets[next.Id]++; remaining--;
            }
        }
        return targets;
    }

    /// <summary>使用库存档位避免少量资源进出就反复调换员工。</summary>
    /// <param name="stock">当前库存。</param>
    /// <param name="desired">随人口增长的库存目标。</param>
    private static double Urgency(int stock, int desired) => stock < desired / 4 ? 6 : stock < desired ? 3 : 1;
}
