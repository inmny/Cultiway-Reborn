using System;
using System.Linq;

namespace Cultiway.Content.KingdomPolicies;

/// <summary>国家可选择的制度，产权两张卡必须二选一。</summary>
[Flags]
public enum KingdomPolicyCard
{
    /// <summary>村庄围绕风车统一经营。</summary>
    PublicFarms = 1,
    /// <summary>家庭经营矩形田块。</summary>
    PrivateFarms = 2,
    /// <summary>达到原版年龄门槛的孩童可生产。</summary>
    ChildLabor = 4,
    /// <summary>工作月与休息月。</summary>
    WorkCalendar = 8,
    /// <summary>自动或手动职业定员。</summary>
    OccupationPlanning = 16,
    /// <summary>原版Army下的三级指挥与日常勤务。</summary>
    ArmyOrganization = 32,
    /// <summary>春麦和冬麦农时。</summary>
    SeasonalFarming = 64,
    /// <summary>原粮必须经过风车加工。</summary>
    GrainMilling = 128,
    /// <summary>单株施肥提高收成。</summary>
    FertilizerYield = 256,
    /// <summary>允许城市建筑利用农田地表。</summary>
    ConstructionPriority = 512,
    /// <summary>适龄孩童空闲时到图书馆阅读。</summary>
    Education = 1024,
    /// <summary>无住房居民可以在本城空房临时过夜。</summary>
    HomelessRelief = 2048
}

/// <summary>随国家保存的一套政策，不重复保存村庄的具体定员与田块。</summary>
public sealed class KingdomPolicyData
{
    /// <summary>现有制度默认启用，童工、教育与流浪救助由玩家主动开启。</summary>
    public KingdomPolicyCard Cards = KingdomPolicyCard.PrivateFarms | KingdomPolicyCard.WorkCalendar |
        KingdomPolicyCard.OccupationPlanning | KingdomPolicyCard.ArmyOrganization | KingdomPolicyCard.SeasonalFarming |
        KingdomPolicyCard.GrainMilling | KingdomPolicyCard.FertilizerYield | KingdomPolicyCard.ConstructionPriority;

    /// <summary>本国儿童入学年龄；成年后不再自动安排儿童课程。</summary>
    public int SchoolAdmissionAge = 6;

    /// <summary>启用期间的城市忠诚度固定修正，关闭后移除，不逐月累加。</summary>
    /// <param name="card">需要查看影响的单张政策。</param>
    public static int LoyaltyImpact(KingdomPolicyCard card) => card switch {
        KingdomPolicyCard.ChildLabor => -10,
        KingdomPolicyCard.HomelessRelief => -5,
        KingdomPolicyCard.Education => 10,
        _ => 0
    };

    /// <summary>当前政策组合的忠诚度修正，仅计算已启用卡片。</summary>
    [Newtonsoft.Json.JsonIgnore]
    public int LoyaltyTotal => Enum.GetValues(typeof(KingdomPolicyCard)).Cast<KingdomPolicyCard>().Where(Has).Sum(LoyaltyImpact);

    /// <summary>检查单张卡的启用状态。</summary>
    /// <param name="card">需要读取的制度。</param>
    public bool Has(KingdomPolicyCard card) => (Cards & card) == card;

    /// <summary>修改草稿，产权切换时同时关闭另一张卡。</summary>
    /// <param name="card">点击的卡片。</param>
    /// <param name="enabled">玩家选择的状态。</param>
    public void Set(KingdomPolicyCard card, bool enabled)
    {
        if (card is KingdomPolicyCard.PublicFarms or KingdomPolicyCard.PrivateFarms)
        {
            if (!enabled) return;
            Cards &= ~(KingdomPolicyCard.PublicFarms | KingdomPolicyCard.PrivateFarms);
        }
        if (enabled) Cards |= card; else Cards &= ~card;
    }

    /// <summary>输入边界只允许已定义卡片和一种产权。</summary>
    public bool IsValid() => ((int)Cards & ~4095) == 0 && SchoolAdmissionAge is >= 1 and <= 100 &&
        Has(KingdomPolicyCard.PublicFarms) != Has(KingdomPolicyCard.PrivateFarms);

    /// <summary>创建独立编辑草稿。</summary>
    public KingdomPolicyData Copy() => new() { Cards = Cards, SchoolAdmissionAge = SchoolAdmissionAge };
}
