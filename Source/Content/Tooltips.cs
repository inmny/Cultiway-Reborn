using System;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Content;

public class Tooltips : ExtendLibrary<TooltipAsset, Tooltips>
{
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        WorldboxGame.Tooltips.Book.callback += ShowCultibookStats;

        WorldboxGame.Tooltips.Actor.callback += ShowActorCultiwayInfo;
    }

    private void ShowActorCultiwayInfo(Tooltip tooltip, string type, TooltipData data)
    {
        var ae = data.actor.GetExtend();
        if (ae.HasElementRoot())
        {
            var er = ae.GetElementRoot();
            tooltip.addLineText("灵根", er.Type.GetName(), pLocalize: false);
        }
        if (ae.HasComponent<Jindan>())
        {
            ref Jindan jindan = ref ae.GetComponent<Jindan>();
            tooltip.addLineText("金丹", jindan.Type.GetName(), pLocalize: false);
        }
        if (ae.HasComponent<Yuanying>())
        {
            ref Yuanying yuanying = ref ae.GetComponent<Yuanying>();
            tooltip.addLineText("元婴", yuanying.Type.GetName(), pLocalize: false);
        }
    }

    private void ShowCultibookStats(Tooltip tooltip, string type, TooltipData data)
    {
        var book = data.book;
        if (book.getAsset() != BookTypes.Cultibook) return;
        var be = book.GetExtend();
        var cultibook = be.GetComponent<Cultibook>();
        tooltip.addLineText(Toolbox.coloredText("Cultiway.Book.ReadAction.FullyMaster", "#FFFFFF", true), "", pLocalize:false);
        BaseStatsHelper.showBaseStats(tooltip.stats_description, tooltip.stats_values, cultibook.Asset.FinalStats);
    }
}