namespace Cultiway.Core.Libraries;

/// <summary>
/// 法器器形，承接 <see cref="ItemShapeAsset"/>：既是视觉形状（纹理/掉落/命名），
/// 又承载法器专属性质（用途倾向、槽位数等）。
/// 注册进 <c>ItemShapeLibrary</c>，故 <see cref="ItemShapeAsset"/> 的反查路径对该子类同样生效。
/// 第一阶段性质字段仅占位，不实现计算逻辑。
/// </summary>
public class ArtifactShapeAsset : ItemShapeAsset
{
    /// <summary>
    /// 该器形的用途倾向，决定其主动/被动作用的大方向。第一阶段仅占位。
    /// </summary>
    public ArtifactPurpose Purpose;

    /// <summary>
    /// 该器形默认可承载的附加效果槽位数。设计文档明确"材料不做硬限制"，
    /// 故这只是器形层面的默认值，实际成品仍由材料共同决定。第一阶段仅占位。
    /// </summary>
    public int SlotCount = 1;
}
