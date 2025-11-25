using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public class ElementRoots : ExtendLibrary<ElementRootAsset, ElementRoots>
{
    public static ElementRootAsset Fire  { get; private set; }
    public static ElementRootAsset Water { get; private set; }
    public static ElementRootAsset Wood  { get; private set; }
    public static ElementRootAsset Earth { get; private set; }
    public static ElementRootAsset Iron  { get; private set; }

    protected override bool AutoRegisterAssets() => false;
    protected override void OnInit()
    {
        Fire = Add(new ElementRootAsset(nameof(Fire), new ElementComposition()
        {
            fire = 1
        }));
        Water = Add(new ElementRootAsset(nameof(Water), new ElementComposition()
        {
            water = 1
        }));
        Wood = Add(new ElementRootAsset(nameof(Wood), new ElementComposition()
        {
            wood = 1
        }));
        Earth = Add(new ElementRootAsset(nameof(Earth), new ElementComposition
        {
            earth = 1
        }));
        Iron = Add(new ElementRootAsset(nameof(Iron), new ElementComposition
        {
            iron = 1
        }));
    }
}