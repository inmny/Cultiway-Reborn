using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public class MaterialShapes : ExtendLibrary<MaterialShapeAsset, MaterialShapes>
{
    public static MaterialShapeAsset Ball { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets("Cultiway.MaterialShape");
    }
}