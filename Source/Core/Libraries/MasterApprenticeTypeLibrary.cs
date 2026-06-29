namespace Cultiway.Core.Libraries;

/// <summary>
/// 师徒关系类型资产库，提供默认关系、id 解析和按亲密度自动匹配关系类型的能力。
/// </summary>
public class MasterApprenticeTypeLibrary : AssetLibrary<MasterApprenticeTypeAsset>
{
    /// <summary>
    /// 获取默认关系类型，即 rank 最低的关系资产。
    /// </summary>
    public MasterApprenticeTypeAsset GetDefault()
    {
        MasterApprenticeTypeAsset result = null;
        for (int i = 0; i < list.Count; i++)
        {
            MasterApprenticeTypeAsset asset = list[i];
            if (result == null || asset.rank < result.rank)
            {
                result = asset;
            }
        }

        return result;
    }

    /// <summary>
    /// 根据 id 获取关系类型，缺失时返回默认关系类型。
    /// </summary>
    public MasterApprenticeTypeAsset GetOrDefault(string id)
    {
        if (!string.IsNullOrEmpty(id) && has(id)) return get(id);
        return GetDefault();
    }

    /// <summary>
    /// 根据亲密度和衣钵标记匹配当前应处于的最高关系类型。
    /// </summary>
    public MasterApprenticeTypeAsset GetByIntimacy(float intimacy, bool isSuccessor)
    {
        MasterApprenticeTypeAsset result = null;
        for (int i = 0; i < list.Count; i++)
        {
            MasterApprenticeTypeAsset asset = list[i];
            if (asset.requiresSuccessorFlag && !isSuccessor) continue;
            if (intimacy < asset.minIntimacy) continue;
            if (result == null || asset.rank > result.rank)
            {
                result = asset;
            }
        }

        return result ?? GetDefault();
    }
}
