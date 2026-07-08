using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public partial class ItemShapes
{
    public static ItemShapeAsset Sword { get; private set; }
    public static ItemShapeAsset Seal { get; private set; }
    public static ItemShapeAsset Robe { get; private set; }
    public static ItemShapeAsset Mirror { get; private set; }
    public static ItemShapeAsset Ding { get; private set; }

    private void SetupArtifactShapes()
    {
        SetArtifactShape(Sword, "artifact_shapes/sword", ["剑"]);
        SetArtifactShape(Seal, "artifact_shapes/seal", ["印"]);
        SetArtifactShape(Robe, "artifact_shapes/robe", ["袍"]);
        SetArtifactShape(Mirror, "artifact_shapes/mirror", ["镜"]);
        SetArtifactShape(Ding, "artifact_shapes/ding", ["鼎"]);
    }

    private void SetArtifactShape(ItemShapeAsset asset, string folder, string[] nameCandidates)
    {
        SetFolder(asset, folder);
        if (asset == null) return;
        asset.ingredient_name_candidates = nameCandidates;
    }
}
