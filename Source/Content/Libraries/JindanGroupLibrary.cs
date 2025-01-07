using System.Collections.Generic;
using Cultiway.Utils;

namespace Cultiway.Content.Libraries;

public class JindanGroupLibrary : AssetLibrary<JindanGroupAsset>
{
    public PriorityQueuePreview<JindanGroupAsset> jindanGroups = new(16, new JindanGroupPriorityComparer());

    public override JindanGroupAsset add(JindanGroupAsset pAsset)
    {
        jindanGroups.Enqueue(pAsset);
        return base.add(pAsset);
    }

    private class JindanGroupPriorityComparer : IComparer<JindanGroupAsset>
    {
        public int Compare(JindanGroupAsset x, JindanGroupAsset y)
        {
            return y.prior - x.prior;
        }
    }
}