using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Extensions;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

/// <summary>
/// 宗门特质集合。
/// </summary>
[Dependency(typeof(SectTraitGroups))]
public class SectTraits : ExtendLibrary<SectTrait, SectTraits>
{
    /// <summary>
    /// 隐世山门：偏好山地和远离城市的传统宗门驻地。
    /// </summary>
    public static SectTrait SecludedMountainGate { get; private set; }

    /// <summary>
    /// 城坊别院：允许依附城市周边建立驻地。
    /// </summary>
    public static SectTrait CityAttachedBranch { get; private set; }

    /// <summary>
    /// 逐灵择地：优先追逐高灵气区域。
    /// </summary>
    public static SectTrait ResourceSeekingGate { get; private set; }

    /// <summary>
    /// 开疆立派：偏好宽阔空间并远离其他宗门。
    /// </summary>
    public static SectTrait TerritorialGate { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Sect.Trait";

    protected override void OnInit()
    {
        SetupResidenceStrategy(
            SecludedMountainGate,
            iconPath: "cultiway/icons/sect_traits/secluded_mountain_gate",
            foundingWeight: 45,
            wakanWeight: 1.2f,
            terrainWeight: 1.35f,
            cityDistanceWeight: 1.15f,
            buildSpaceWeight: 0.85f,
            sectDistanceWeight: 1f);

        SetupResidenceStrategy(
            CityAttachedBranch,
            iconPath: "cultiway/icons/sect_traits/city_attached_branch",
            foundingWeight: 15,
            wakanWeight: 0.8f,
            terrainWeight: 0.55f,
            cityDistanceWeight: 1f,
            buildSpaceWeight: 1.15f,
            sectDistanceWeight: 0.85f,
            allowCityZones: true,
            preferCityProximity: true);

        SetupResidenceStrategy(
            ResourceSeekingGate,
            iconPath: "cultiway/icons/sect_traits/resource_seeking_gate",
            foundingWeight: 30,
            wakanWeight: 1.7f,
            terrainWeight: 0.8f,
            cityDistanceWeight: 0.65f,
            buildSpaceWeight: 1f,
            sectDistanceWeight: 0.9f);

        SetupResidenceStrategy(
            TerritorialGate,
            iconPath: "cultiway/icons/sect_traits/territorial_gate",
            foundingWeight: 10,
            wakanWeight: 1f,
            terrainWeight: 1f,
            cityDistanceWeight: 0.85f,
            buildSpaceWeight: 1.35f,
            sectDistanceWeight: 1.8f);
    }

    private static void SetupResidenceStrategy(
        SectTrait trait,
        string iconPath,
        int foundingWeight,
        float wakanWeight,
        float terrainWeight,
        float cityDistanceWeight,
        float buildSpaceWeight,
        float sectDistanceWeight,
        bool allowCityZones = false,
        bool preferCityProximity = false)
    {
        trait.group_id = SectTraitGroups.ResidenceStrategy.id;
        trait.path_icon = iconPath;
        trait.needs_to_be_explored = false;
        trait.show_in_knowledge_window = false;
        trait.isResidenceStrategy = true;
        trait.foundingWeight = foundingWeight;
        trait.allowCityResidenceZones = allowCityZones;
        trait.preferCityProximity = preferCityProximity;
        trait.residenceWakanScoreWeight = wakanWeight;
        trait.residenceTerrainScoreWeight = terrainWeight;
        trait.residenceCityDistanceScoreWeight = cityDistanceWeight;
        trait.residenceBuildSpaceScoreWeight = buildSpaceWeight;
        trait.residenceSectDistanceScoreWeight = sectDistanceWeight;
    }

    /// <summary>
    /// 判断指定建宗者是否至少存在一种可用的驻地策略。
    /// </summary>
    public static bool HasFoundingResidenceStrategy(Actor founder)
    {
        List<SectTrait> strategies = ModClass.L.SectTraitLibrary.GetResidenceStrategies();
        for (int i = 0; i < strategies.Count; i++)
        {
            SectTrait strategy = strategies[i];
            if (strategy.foundingWeight <= 0) continue;
            if (SectResidencePlanner.HasFoundingSite(founder, strategy))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 从所有当前可用的驻地策略中按权重抽取一个建宗策略。
    /// </summary>
    public static bool TryPickFoundingResidenceStrategy(Actor founder, out SectTrait trait)
    {
        trait = null;
        List<SectTrait> candidates = GetViableResidenceStrategies(founder);
        if (candidates.Count == 0) return false;

        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += candidates[i].foundingWeight;
        }

        int roll = Randy.randomInt(0, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            SectTrait candidate = candidates[i];
            roll -= candidate.foundingWeight;
            if (roll < 0)
            {
                trait = candidate;
                return true;
            }
        }

        trait = candidates[candidates.Count - 1];
        return true;
    }

    private static List<SectTrait> GetViableResidenceStrategies(Actor founder)
    {
        List<SectTrait> result = new();
        List<SectTrait> strategies = ModClass.L.SectTraitLibrary.GetResidenceStrategies();
        for (int i = 0; i < strategies.Count; i++)
        {
            SectTrait strategy = strategies[i];
            if (strategy.foundingWeight <= 0) continue;
            if (SectResidencePlanner.HasFoundingSite(founder, strategy))
            {
                result.Add(strategy);
            }
        }

        return result;
    }
}
