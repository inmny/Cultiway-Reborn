using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

[Dependency(typeof(BaseStatses))]
public partial class Cultisyses : ExtendLibrary<BaseCultisysAsset, Cultisyses>
{
    protected override bool AutoRegisterAssets() => false;

    protected override void OnInit()
    {
        InitXian();
        InitMagic();
    }

    public override void OnReload()
    {
        LoadStatsForXian();
    }
}
