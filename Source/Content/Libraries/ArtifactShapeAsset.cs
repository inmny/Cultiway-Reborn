using Cultiway.Core.Libraries;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 法器器形。沿用通用物品器形能力，并提供法器内容所需的用途元数据。
/// </summary>
public class ArtifactShapeAsset : ItemShapeAsset
{
    public ArtifactPurpose Purpose;

    /// <summary>
    /// 器形的默认结构规模，仅作为内容元数据，不限制材料或 atom 数量。
    /// </summary>
    public int SlotCount = 1;
}
