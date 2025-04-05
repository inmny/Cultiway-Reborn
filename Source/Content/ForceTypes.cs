using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public class ForceTypes : ExtendLibrary<ForceTypeAsset, ForceTypes>
{
    public static ForceTypeAsset Sect { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
    }
}