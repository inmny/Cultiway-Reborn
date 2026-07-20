using System.Text;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.UI.CreatureInfoPages;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.UI.CreatureInfoPages;

public class YuanyingPage : MonoBehaviour
{
    /// <summary>金丹与元婴共用的紧凑详情布局。</summary>
    private CoreFormationDetailView detailView;

    /// <summary>在角色信息页上创建元婴详情文本组件并应用基础字体样式。</summary>
    public static void Setup(CreatureInfoPage page)
    {
        var thisPage = page.gameObject.AddComponent<YuanyingPage>();
        thisPage.detailView = CoreFormationDetailView.Create(page);
    }

    /// <summary>根据当前角色的组合快照刷新元婴、金丹谱系、原子、组成和代表法术。</summary>
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        ActorExtend ae = actor.GetExtend();
        var header = new StringBuilder();
        var footer = new StringBuilder();

        ref Yuanying yuanying = ref ae.GetYuanying();
        header.AppendLine(string.Format("Cultiway.CoreFormation.Page.Yuanying.Title".Localize(),
            yuanying.GetName()));
        header.AppendLine(string.Format("Cultiway.CoreFormation.Page.Yuanying.Strength".Localize(),
            yuanying.strength));
        header.AppendLine(string.Format("Cultiway.CoreFormation.Page.Yuanying.Lineage".Localize(),
            yuanying.source_jindan_name));
        string atoms = string.Format("Cultiway.CoreFormation.Page.Atoms".Localize(),
            string.Join("、", CoreFormationComposer.GetActiveAtomNames(yuanying.formation, yuanying.stage)));
        JindanPage.AppendComposition(footer, yuanying.formation.composition);
        var skill = yuanying.formation.representative_skill_id;
        if (!string.IsNullOrEmpty(skill))
            footer.AppendLine(string.Format("Cultiway.CoreFormation.Page.Skill".Localize(),
                LM.Has(skill) ? LM.Get(skill) : skill));

        var thisPage = page.GetComponent<YuanyingPage>();
        thisPage.detailView.SetContent(ae, header.ToString(), atoms, footer.ToString());
    }
}
