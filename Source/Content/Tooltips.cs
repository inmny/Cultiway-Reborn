using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.UI.Prefab;
using Cultiway.Utils.Extension;
using strings;

namespace Cultiway.Content;

public class Tooltips : ExtendLibrary<TooltipAsset, Tooltips>
{
    [CloneSource(S_Tooltip.book)]
    public static TooltipAsset Cultibook { get; private set; }
    
    protected override bool AutoRegisterAssets() => true;
    
    protected override void OnInit()
    {
        // 设置功法书 Tooltip
        Cultibook.prefab_id = "tooltips/tooltip_cultiway_cultibook";
        Cultibook.callback = ShowCultibookTooltip;
        CultibookTooltip.PatchTo<Tooltip>(Cultibook.prefab_id);

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

    private static void ShowCultibookTooltip(Tooltip tooltip, string type, TooltipData data)
    {
        var book = data.book;
        if (book == null || book.getAsset() != BookTypes.Cultibook) return;
        
        var cultibookTooltip = tooltip.GetComponent<CultibookTooltip>();
        cultibookTooltip?.Setup(book);
    }
}