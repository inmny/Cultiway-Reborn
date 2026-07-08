using Cultiway.Core.Libraries;

namespace Cultiway.Content;

/// <summary>
/// 法器器形（剑/印/袍/镜/鼎）。注册为 <see cref="ArtifactShapeAsset"/>（继承自 ItemShapeAsset），
/// 既保留视觉形状的纹理/命名能力，又承载法器专属性质（用途倾向、槽位数）。
/// 因 <see cref="Abstract.ExtendLibrary{TAsset, T}"/> 的自动注册按属性精确类型匹配，
/// ArtifactShapeAsset 类型的属性不会被 <see cref="ItemShapes"/>(ExtendLibrary&lt;ItemShapeAsset&gt;) 自动注册，
/// 故在此手动 <c>Add</c> 进 ItemShapeLibrary，与普通 ItemShapeAsset 共库、共反查路径。
/// </summary>
public partial class ItemShapes
{
    public static ArtifactShapeAsset Sword  { get; private set; }
    public static ArtifactShapeAsset Seal   { get; private set; }
    public static ArtifactShapeAsset Robe   { get; private set; }
    public static ArtifactShapeAsset Mirror { get; private set; }
    public static ArtifactShapeAsset Ding   { get; private set; }

    private void SetupArtifactShapes()
    {
        Sword  = AddArtifactShape(nameof(Sword),  "artifact_shapes/sword",  ["剑"], ArtifactPurpose.Offensive,  2);
        Seal   = AddArtifactShape(nameof(Seal),   "artifact_shapes/seal",   ["印"], ArtifactPurpose.Support,    2);
        Robe   = AddArtifactShape(nameof(Robe),   "artifact_shapes/robe",   ["袍"], ArtifactPurpose.Defensive,  2);
        Mirror = AddArtifactShape(nameof(Mirror), "artifact_shapes/mirror", ["镜"], ArtifactPurpose.Support,    1);
        Ding   = AddArtifactShape(nameof(Ding),   "artifact_shapes/ding",   ["鼎"], ArtifactPurpose.Production, 3);
    }

    private ArtifactShapeAsset AddArtifactShape(string id_suffix, string folder, string[] nameCandidates,
        ArtifactPurpose purpose, int slotCount)
    {
        var asset = new ArtifactShapeAsset
        {
            id = $"{Prefix()}.{id_suffix}",
            ingredient_name_candidates = nameCandidates,
            Purpose = purpose,
            SlotCount = slotCount,
        };
        SetFolder(asset, folder);
        return (ArtifactShapeAsset)Add(asset);
    }
}
