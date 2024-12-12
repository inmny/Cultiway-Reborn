using Cultiway.Content.CultisysComponents;
using Cultiway.Core;

namespace Cultiway.Content.Libraries;

public class JindanLibrary : AssetLibrary<JindanAsset>
{
    public static JindanAsset Common { get; private set; }

    public override void init()
    {
        Common = add(new JindanAsset
        {
            id = "Cultiway.Jindan.Common"
        });
    }

    public JindanAsset GetJindan(ActorExtend ae, ref XianBase xian_base)
    {
        return Common;
    }
}