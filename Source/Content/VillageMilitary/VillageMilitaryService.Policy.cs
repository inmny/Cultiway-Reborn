using System.Collections.Generic;
using System.IO;
using Cultiway.Content.VillageDuties;
using Newtonsoft.Json;

namespace Cultiway.Content.VillageMilitary;

/// <summary>国家统一编制设置，村庄只保存月历和实际落地的名册。</summary>
public static partial class VillageMilitaryService
{
    /// <summary>随原版国家数据保存的唯一编制设置。</summary>
    public const string KingdomPolicySaveKey = "cw.kingdom.military.policy";
    /// <summary>当前世界每个国家唯一的设置对象，替换对象表示玩家提交了新设置。</summary>
    private static readonly Dictionary<long, KingdomMilitaryPolicy> kingdomPolicies = new();

    /// <summary>国家设置随世界切换一起清空，不跨存档复用编号。</summary>
    private static void EnsureWorld()
    {
        if (world == World.world.map_stats) return;
        Clear(); world = World.world.map_stats;
    }

    /// <summary>读取国家的统一设置，未配置的国家使用默认编制。</summary>
    /// <param name="kingdom">村庄当前所属国家。</param>
    public static KingdomMilitaryPolicy GetKingdomPolicy(Kingdom kingdom)
    {
        EnsureWorld();
        if (kingdomPolicies.TryGetValue(kingdom.id, out var policy)) return policy;
        kingdom.data.get(KingdomPolicySaveKey, out string json, null);
        policy = json == null ? new KingdomMilitaryPolicy() : JsonConvert.DeserializeObject<KingdomMilitaryPolicy>(json);
        if (policy == null || !policy.IsValid()) throw new InvalidDataException("国家军事编制设置无效。");
        kingdomPolicies.Add(kingdom.id, policy);
        return policy;
    }

    /// <summary>提交国家统一编制和本村月历，各村仍按原有预算在安全时机重编。</summary>
    /// <param name="city">编辑入口所在村庄，只有该村修改月份。</param>
    /// <param name="policy">国家编制字段与本村月份组成的完整草稿。</param>
    public static bool Apply(City city, VillageDutyPolicy policy)
    {
        if (city == null || !city.isAlive() || city.kingdom == null || policy == null || !policy.IsValid()) return false;
        var state = Get(city);
        state.Policy.WorkMonths = policy.WorkMonths;
        state.Policy.OccupationTargets = new Dictionary<string, int>(policy.OccupationTargets);
        if (state.PendingPolicy != null)
        {
            state.PendingPolicy.WorkMonths = policy.WorkMonths;
            state.PendingPolicy.OccupationTargets = new Dictionary<string, int>(policy.OccupationTargets);
        }
        VillageOccupationService.ApplyJobTargets(city);

        // 先保存国家唯一来源，再唤醒各村；这里只排队，不遍历和调动全国士兵。
        var shared = new KingdomMilitaryPolicy();
        shared.CopyOrganizationFrom(policy);
        kingdomPolicies[city.kingdom.id] = shared;
        city.kingdom.data.set(KingdomPolicySaveKey, JsonConvert.SerializeObject(shared));
        foreach (var member in World.world.cities.list)
        {
            if (member.kingdom != city.kingdom) continue;
            Get(member);
            Save(member);
        }
        return true;
    }

    /// <summary>仅在国家设置变化或村庄易主时排队，不在每次人员查询时重新编团。</summary>
    /// <param name="city">需要采用当前国家设置的村庄。</param>
    /// <param name="state">本村仍在使用的编制快照。</param>
    private static void SynchronizeKingdomPolicy(City city, VillageMilitaryData state)
    {
        var shared = GetKingdomPolicy(city.kingdom);
        if (ReferenceEquals(state.PolicySource, shared)) return;
        var target = state.Policy.Copy();
        target.CopyOrganizationFrom(shared);
        if (state.Policy.HasSameStructure(shared))
        {
            state.Policy = target;
            state.PendingPolicy = null;
        }
        else state.PendingPolicy = target;
        state.PolicySource = shared;
        state.NextUpdate = 0;
    }
}
