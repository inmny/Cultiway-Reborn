using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

public class Tooltips : ExtendLibrary<TooltipAsset, Tooltips>
{
    protected override void OnInit()
    {
        WorldboxGame.Tooltips.Book.callback += ShowCultibookStats;
    }

    private void ShowCultibookStats(Tooltip tooltip, string type, TooltipData data)
    {
        var book = data.book;
        if (book.getAsset() != BookTypes.Cultibook) return;
        var be = book.GetExtend();
        var cultibook = be.GetComponent<Cultibook>();
        tooltip.addLineText("Cultiway.Book.ReadAction.FullyMaster", "", "#FFFFFF");
        BaseStatsHelper.showBaseStats(tooltip.stats_description, tooltip.stats_values, cultibook.Asset.FinalStats);
    }
}