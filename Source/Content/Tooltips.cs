using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.UI.Prefab;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using strings;

namespace Cultiway.Content;

public class Tooltips : ExtendLibrary<TooltipAsset, Tooltips>
{
    [CloneSource(S_Tooltip.book)]
    public static TooltipAsset Cultibook { get; private set; }

    [CloneSource("tip")]
    public static TooltipAsset WanfaSkill { get; private set; }
    
    protected override bool AutoRegisterAssets() => true;
    
    protected override void OnInit()
    {
        // 设置功法书 Tooltip
        Cultibook.prefab_id = "tooltips/tooltip_cultiway_cultibook";
        Cultibook.callback = ShowCultibookTooltip;
        CultibookTooltip.PatchTo<Tooltip>(Cultibook.prefab_id);

        WanfaSkill.prefab_id = "tooltips/tooltip_cultiway_wanfa_skill";
        WanfaSkill.callback = ShowWanfaSkillTooltip;
        WanfaSkillTooltip.PatchTo<Tooltip>(WanfaSkill.prefab_id);

        WorldboxGame.Tooltips.Actor.callback += ShowActorCultiwayInfo;
        WorldboxGame.Tooltips.ActorKing.callback += ShowActorCultiwayInfo;
        WorldboxGame.Tooltips.ActorLeader.callback += ShowActorCultiwayInfo;
    }

    private static void ShowActorCultiwayInfo(Tooltip tooltip, string type, TooltipData data)
    {
        var ae = data.actor.GetExtend();
        InsertSectAndMasterInfo(tooltip, ae);

        if (ae.HasElementRoot())
        {
            var er = ae.GetElementRoot();
            var cultisys = Cultisyses.GetDisplayCultisys(ae);
            var style = cultisys?.DisplayStyle;
            var label = style != null ? LM.Get(style.category_label_key) : "灵根";
            tooltip.addLineText(label, er.Type.GetName(cultisys), pLocalize: false);
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

    private static void InsertSectAndMasterInfo(Tooltip tooltip, ActorExtend ae)
    {
        string insertAfter = "kingdom";
        if (ae.sect != null && !ae.sect.isRekt())
        {
            tooltip.InsertLineAfter(insertAfter, "Sect", ae.sect.name, ae.sect.getColor().color_text);
            insertAfter = "Sect";
        }

        Actor master = ae.GetMaster();
        if (master != null && !master.isRekt())
        {
            tooltip.InsertLineAfter(insertAfter, "Masters", master.getName(), master.kingdom?.getColor()?.color_text);
        }
    }

    private static void ShowCultibookTooltip(Tooltip tooltip, string type, TooltipData data)
    {
        var book = data.book;
        if (book == null || book.getAsset() != BookTypes.Cultibook) return;
        
        var cultibookTooltip = tooltip.GetComponent<CultibookTooltip>();
        cultibookTooltip?.Setup(book);
    }

    private static void ShowWanfaSkillTooltip(Tooltip tooltip, string type, TooltipData data)
    {
        tooltip.GetComponent<WanfaSkillTooltip>().SetupPending();
    }
}
