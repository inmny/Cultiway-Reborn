using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public class ItemShapes : ExtendLibrary<ItemShapeAsset, ItemShapes>
{
    public static ItemShapeAsset Ball { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets("Cultiway.ItemShape");
        Ball.major_texture_folder = "cultiway/icons/item_shapes/ball";
    }

    protected override void PostInit(ItemShapeAsset asset)
    {
        asset.LoadTextures();
    }
}