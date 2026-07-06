using System;
using System.Collections.Generic;
using Cultiway.Core;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 宗门特质资产；用于声明宗门的长期风格、策略和规则偏好。
/// </summary>
[Serializable]
public class SectTrait : BaseTrait<SectTrait>
{
    /// <summary>
    /// 是否作为建宗时可选的驻地策略。
    /// </summary>
    public bool isResidenceStrategy;

    /// <summary>
    /// 建宗时随机选中该驻地策略的权重。
    /// </summary>
    public int foundingWeight = 1;

    /// <summary>
    /// 是否允许驻地包含城市 zone。
    /// </summary>
    public bool allowCityResidenceZones;

    /// <summary>
    /// 是否把靠近城市视为正面选址因素。
    /// </summary>
    public bool preferCityProximity;

    /// <summary>
    /// 灵气评分权重。
    /// </summary>
    public float residenceWakanScoreWeight = 1f;

    /// <summary>
    /// 地形评分权重。
    /// </summary>
    public float residenceTerrainScoreWeight = 1f;

    /// <summary>
    /// 城市距离评分权重。
    /// </summary>
    public float residenceCityDistanceScoreWeight = 1f;

    /// <summary>
    /// 建筑空间评分权重。
    /// </summary>
    public float residenceBuildSpaceScoreWeight = 1f;

    /// <summary>
    /// 与其他宗门距离评分权重。
    /// </summary>
    public float residenceSectDistanceScoreWeight = 1f;

    /// <summary>
    /// 是否为建宗时自动抽取的制度特质；同组制度特质互斥。
    /// </summary>
    public bool isFoundingPolicy;

    /// <summary>
    /// 建宗时随机选中该制度特质的权重。
    /// </summary>
    public int policyFoundingWeight = 1;

    public override HashSet<string> progress_elements => _progress_data?.unlocked_traits_kingdom;

    public override string typed_id => "sect_trait";

    public override IEnumerable<ITraitsOwner<SectTrait>> getRelatedMetaList()
    {
        SectManager manager = WorldboxGame.I?.Sects;
        if (manager == null) yield break;

        foreach (Sect sect in manager)
        {
            yield return sect;
        }
    }

    public override BaseCategoryAsset getGroup()
    {
        return ModClass.L.SectTraitGroupLibrary.get(group_id);
    }
}
