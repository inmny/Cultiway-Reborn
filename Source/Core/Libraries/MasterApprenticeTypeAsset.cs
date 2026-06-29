using NeoModLoader.General;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 师徒关系类型资产；集中描述关系层级、亲密度门槛、教学系数和宗门角色映射。
/// </summary>
public class MasterApprenticeTypeAsset : Asset
{
    /// <summary>
    /// 关系层级排序值，数值越大代表关系越亲近。
    /// </summary>
    public int rank;

    /// <summary>
    /// 自动升级到该关系类型需要的最低亲密度。
    /// </summary>
    public float minIntimacy;

    /// <summary>
    /// 该关系类型对师父传授效率的倍率。
    /// </summary>
    public float teachEfficiencyMultiplier = 1f;

    /// <summary>
    /// 是否需要额外的衣钵传人标记才能自动升级到该关系类型。
    /// </summary>
    public bool requiresSuccessorFlag;

    /// <summary>
    /// 关系类型名称的本地化 key。
    /// </summary>
    public string nameKey;

    /// <summary>
    /// 关系类型说明的本地化 key。
    /// </summary>
    public string descriptionKey;

    /// <summary>
    /// 该关系类型自动映射的宗门门阶角色 id。
    /// </summary>
    public string sectGradeRoleId;

    /// <summary>
    /// 该关系类型自动映射的宗门头衔角色 id。
    /// </summary>
    public string sectTitleRoleId;

    /// <summary>
    /// 获取关系类型的本地化名称。
    /// </summary>
    public string GetName()
    {
        return (string.IsNullOrEmpty(nameKey) ? id : nameKey).Localize();
    }

    /// <summary>
    /// 获取关系类型的本地化说明。
    /// </summary>
    public string GetDescription()
    {
        return (string.IsNullOrEmpty(descriptionKey) ? $"{id}.Info" : descriptionKey).Localize();
    }
}
