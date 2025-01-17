using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public class ItemShapes : ExtendLibrary<ItemShapeAsset, ItemShapes>
{
    public static ItemShapeAsset Ball { get; private set; }
    public static ItemShapeAsset Talisman { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets("Cultiway.ItemShape");
        Ball.major_texture_folder = "cultiway/icons/item_shapes/ball";
        Talisman.major_texture_folder = "cultiway/icons/item_shapes/talisman";
    }

    protected override void PostInit(ItemShapeAsset asset)
    {
        asset.LoadTextures();
    }
}