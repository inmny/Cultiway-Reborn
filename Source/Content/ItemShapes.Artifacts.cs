using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Libraries;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

/// <summary>
/// 法器器形（剑/印/袍/镜/鼎）。注册为 <see cref="ArtifactShapeAsset"/>（继承自 ItemShapeAsset），
/// 既保留视觉形状的纹理/命名能力，又引用可独立扩展的组合外观与世界表现方案。
/// 因 <see cref="Abstract.ExtendLibrary{TAsset, T}"/> 的自动注册按属性精确类型匹配，
/// ArtifactShapeAsset 类型的属性不会被 <see cref="ItemShapes"/>(ExtendLibrary&lt;ItemShapeAsset&gt;) 自动注册，
/// 故在此手动 <c>Add</c> 进 ItemShapeLibrary，与普通 ItemShapeAsset 共库、共反查路径。
/// </summary>
[Dependency(typeof(ArtifactPresentations))]
public partial class ItemShapes
{
    public static ArtifactShapeAsset Sword  { get; private set; }
    public static ArtifactShapeAsset Seal   { get; private set; }
    public static ArtifactShapeAsset Robe   { get; private set; }
    public static ArtifactShapeAsset Mirror { get; private set; }
    public static ArtifactShapeAsset Ding   { get; private set; }

    private void SetupArtifactShapes()
    {
        Sword = AddArtifactShape(nameof(Sword), ["剑"], "sword", ArtifactPresentations.Sword);
        Seal = AddArtifactShape(nameof(Seal), ["印"], "seal", ArtifactPresentations.Seal);
        Robe = AddArtifactShape(nameof(Robe), ["袍"], "robe", ArtifactPresentations.Robe);
        Mirror = AddArtifactShape(nameof(Mirror), ["镜"], "mirror", ArtifactPresentations.Mirror);
        Ding = AddArtifactShape(nameof(Ding), ["鼎"], "ding", ArtifactPresentations.Ding);
    }

    private ArtifactShapeAsset AddArtifactShape(string id_suffix, string[] nameCandidates,
        string appearanceFamily, ArtifactPresentationAsset presentation)
    {
        var asset = new ArtifactShapeAsset
        {
            id = $"{Prefix()}.{id_suffix}",
            ingredient_name_candidates = nameCandidates,
            appearance_family = appearanceFamily,
            presentation = presentation,
            GetIcon = ArtifactAppearanceRenderer.GetIconSprite,
            GetWorldSprite = ArtifactAppearanceRenderer.GetWorldSprite,
        };
        return (ArtifactShapeAsset)Add(asset);
    }
}
