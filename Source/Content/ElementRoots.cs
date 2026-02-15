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
    public static ElementRootAsset Neg   { get; private set; }
    public static ElementRootAsset Pos   { get; private set; }

    protected override bool AutoRegisterAssets() => false;
    protected override void OnInit()
    {
        Fire = Add(new ElementRootAsset(nameof(Fire), new ElementComposition()
        {
            fire = 1, neg = 0.5f, pos = 0.5f, entropy = 1f
        }));
        Fire.icon_path = "fire";
        Water = Add(new ElementRootAsset(nameof(Water), new ElementComposition()
        {
            water = 1, neg = 0.5f, pos = 0.5f, entropy = 1f
        }));
        Water.icon_path = "water";
        Wood = Add(new ElementRootAsset(nameof(Wood), new ElementComposition()
        {
            wood = 1, neg = 0.5f, pos = 0.5f, entropy = 1f
        }));
        Wood.icon_path = "wood";
        Earth = Add(new ElementRootAsset(nameof(Earth), new ElementComposition
        {
            earth = 1, neg = 0.5f, pos = 0.5f, entropy = 1f
        }));
        Earth.icon_path = "earth";
        Iron = Add(new ElementRootAsset(nameof(Iron), new ElementComposition
        {
            iron = 1, neg = 0.5f, pos = 0.5f, entropy = 1f
        }));
        Iron.icon_path = "iron";
        Neg = Add(new ElementRootAsset(nameof(Neg), new ElementComposition
        {
            neg = 1, entropy = 1f
        }));
        Neg.icon_path = "neg";
        Pos = Add(new ElementRootAsset(nameof(Pos), new ElementComposition
        {
            pos = 1, entropy = 1f
        }));
        Pos.icon_path = "pos";
    }
}