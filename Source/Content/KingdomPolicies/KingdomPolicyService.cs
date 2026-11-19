using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Content.FamilyFarming;
using Cultiway.Content.VillageDuties;
using Cultiway.Content.VillageMilitary;
using Newtonsoft.Json;

namespace Cultiway.Content.KingdomPolicies;

/// <summary>每国唯一政策来源，查询不遍历全国，提交时只通知所属村庄。</summary>
public static class KingdomPolicyService
{
    /// <summary>国家存档内的政策记录。</summary>
    public const string SaveKey = "cw.kingdom.policies";
    /// <summary>没有国家归属的对象沿用当前玩法默认。</summary>
    private static readonly KingdomPolicyData defaults = new();
    /// <summary>只属于当前世界的国家政策缓存。</summary>
    private static readonly Dictionary<long, KingdomPolicyData> policies = new();
    /// <summary>缓存所属世界。</summary>
    private static MapStats world;

    /// <summary>读取国家唯一政策，首次访问才解析保存记录。</summary>
    /// <param name="kingdom">政策所属国家。</param>
    public static KingdomPolicyData Get(Kingdom kingdom)
    {
        if (kingdom == null) return defaults;
        if (world != World.world.map_stats) { policies.Clear(); world = World.world.map_stats; }
        if (policies.TryGetValue(kingdom.id, out var data)) return data;
        kingdom.data.get(SaveKey, out string json, null);
        data = json == null ? new KingdomPolicyData() : JsonConvert.DeserializeObject<KingdomPolicyData>(json);
        if (data == null || !data.IsValid()) throw new InvalidDataException("国家政策存档无效。");
        policies.Add(kingdom.id, data); return data;
    }

    /// <summary>读取村庄目前所属国家的政策，易主后立即采用新国家。</summary>
    /// <param name="city">需要执行制度的村庄。</param>
    /// <param name="card">需要检查的卡片。</param>
    public static bool Has(City city, KingdomPolicyCard card) => Get(city?.kingdom).Has(card);

    /// <summary>作物采用当前所在地的制度。</summary>
    /// <param name="crop">原版作物建筑。</param>
    /// <param name="card">需要检查的卡片。</param>
    public static bool Has(Building crop, KingdomPolicyCard card) => Has(crop.current_tile?.zone?.city ?? crop.city, card);

    /// <summary>提交独立草稿并保留正在运输的材料、在田作物与原版Army。</summary>
    /// <param name="kingdom">玩家编辑的国家。</param>
    /// <param name="draft">尚未污染已保存设置的完整草稿。</param>
    public static bool Apply(Kingdom kingdom, KingdomPolicyData draft)
    {
        if (kingdom == null || !kingdom.isAlive() || draft == null || !draft.IsValid()) return false;
        var previous = Get(kingdom); var data = draft.Copy();
        policies[kingdom.id] = data;
        kingdom.data.set(SaveKey, JsonConvert.SerializeObject(data));
        foreach (var city in kingdom.cities)
        {
            FamilyFarmService.PolicyChanged(city, previous, data);
            VillageOccupationService.PolicyChanged(city);
            VillageMilitaryService.Invalidate(city);
            CityBehCheckFarms.check(city);
            city.getLoyalty(pForceRecalc: true);
        }
        return true;
    }

    /// <summary>生产年龄独立于征兵，开启童工仍需达到原版工作年龄。</summary>
    /// <param name="actor">本国智慧居民。</param>
    public static bool CanProduce(Actor actor) => actor.isAdult() ||
        Has(actor.city, KingdomPolicyCard.ChildLabor) && actor.getAge() >= SimGlobals.m.child_work_age;
}
