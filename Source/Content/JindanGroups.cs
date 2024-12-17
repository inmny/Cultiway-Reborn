using Cultiway.Abstract;
using Cultiway.Content.CultisysComponents;
using Cultiway.Content.Libraries;
using Cultiway.Core;

namespace Cultiway.Content;

public class JindanGroups : ExtendLibrary<JindanGroupAsset, JindanGroups>
{
    public static JindanGroupAsset Common   { get; private set; }
    public static JindanGroupAsset Element  { get; private set; }
    public static JindanGroupAsset Special  { get; private set; }
    public static JindanGroupAsset External { get; private set; }

    protected override void OnInit()
    {
        RegisterAssets("Cultiway.JindanGroup");
        Common.check = (ActorExtend ae, ref XianBase @base) => true;
    }
}