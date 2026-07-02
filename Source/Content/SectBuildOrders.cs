using Cultiway.Abstract;
using Cultiway.Core.Libraries;
using NeoModLoader.api.attributes;

namespace Cultiway.Content;

[Dependency(typeof(Buildings))]
public class SectBuildOrders : ExtendLibrary<SectBuildOrderAsset, SectBuildOrders>
{
    public static SectBuildOrderAsset Classic { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.Sect.BuildOrder";

    protected override void OnInit()
    {
    }
}
