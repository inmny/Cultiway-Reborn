using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public class RandomEvents : ExtendLibrary<RandomEventAsset, RandomEvents>
{
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
    }
}