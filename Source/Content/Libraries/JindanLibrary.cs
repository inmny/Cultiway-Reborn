using Cultiway.Content.Components;
using Cultiway.Core;

namespace Cultiway.Content.Libraries;

public class JindanLibrary : AssetLibrary<JindanAsset>
{
    public override void init()
    {
    }

    public JindanAsset GetJindan(ActorExtend ae, ref XianBase xian_base)
    {
        foreach (JindanGroupAsset group in Manager.JindanGroupLibrary.jindanGroups)
            if (group.jindans.Count > 0 && (group.check?.Invoke(ae, ref xian_base) ?? false))
                return group.jindans.GetRandom();

        return Jindans.Common;
    }
}