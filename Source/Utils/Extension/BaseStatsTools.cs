using System.Collections.Generic;

namespace Cultiway.Utils.Extension;

public static class BaseStatsTools
{
    public static void MergeStats(this BaseStats a, BaseStats b, float r)
    {
        var b_stats_list = b.stats_list as IList<BaseStatsContainer>;
        for (int i = 0; i < b_stats_list.Count; i++)
        {
            BaseStatsContainer container = b_stats_list[i];
            a[container.id] += container.value * r;
        }
    }
}