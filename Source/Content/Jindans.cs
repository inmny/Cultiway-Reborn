using Cultiway.Abstract;
using Cultiway.Content.Libraries;

namespace Cultiway.Content;

public class Jindans : ExtendLibrary<JindanAsset, Jindans>
{
    public static JindanAsset Common { get; private set; }

    public static JindanAsset Iron  { get; private set; }
    public static JindanAsset Wood  { get; private set; }
    public static JindanAsset Water { get; private set; }
    public static JindanAsset Fire  { get; private set; }
    public static JindanAsset Earth { get; private set; }

    /// <summary>
    ///     润土金丹
    /// </summary>
    public static JindanAsset Bentonite { get; private set; }

    /// <summary>
    ///     凝元金丹
    /// </summary>
    public static JindanAsset Condensed { get; private set; }

    public static JindanAsset Phantom { get; private set; } // 幻影

    public static JindanAsset Dragon { get; private set; } // 恶龙(蜥蜴)

    protected override void OnInit()
    {
        RegisterAssets("Cultiway.Jindan");
        Common.Group = JindanGroups.Common;

        Iron.Group = JindanGroups.Element;
        Wood.Group = JindanGroups.Element;
        Water.Group = JindanGroups.Element;
        Fire.Group = JindanGroups.Element;
        Earth.Group = JindanGroups.Element;
        Bentonite.Group = JindanGroups.Element;

        Condensed.Group = JindanGroups.Special;
        Phantom.Group = JindanGroups.Special;

        Dragon.Group = JindanGroups.External;
    }
}