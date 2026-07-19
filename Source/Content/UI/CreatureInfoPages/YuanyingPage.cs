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
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

public class YuanyingPage : MonoBehaviour
{
    public Text Text { get; private set; }

    /// <summary>在角色信息页上创建元婴详情文本组件并应用基础字体样式。</summary>
    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<YuanyingPage>();
        var text = page.gameObject.AddComponent<Text>();

        text.font = Cultiway.UI.UiTheme.Current.Font;
        text.fontSize = 8;

        this_page.Text = text;
    }

    /// <summary>根据当前角色的组合快照刷新元婴、金丹谱系、原子、组成和代表法术。</summary>
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        ActorExtend ae = actor.GetExtend();
        var sb = new StringBuilder();

        ref Yuanying yuanying = ref ae.GetYuanying();
        sb.AppendLine(string.Format("Cultiway.CoreFormation.Page.Yuanying.Title".Localize(), yuanying.GetName()));
        sb.AppendLine(string.Format("Cultiway.CoreFormation.Page.Yuanying.Strength".Localize(),
            yuanying.strength));
        sb.AppendLine(string.Format("Cultiway.CoreFormation.Page.Yuanying.Lineage".Localize(),
            yuanying.source_jindan_name));
        sb.AppendLine(string.Format("Cultiway.CoreFormation.Page.Atoms".Localize(),
            string.Join("、", CoreFormationComposer.GetActiveAtomNames(yuanying.formation, yuanying.stage))));
        JindanPage.AppendComposition(sb, yuanying.formation.composition);
        var skill = yuanying.formation.representative_skill_id;
        if (!string.IsNullOrEmpty(skill))
            sb.AppendLine(string.Format("Cultiway.CoreFormation.Page.Skill".Localize(),
                LM.Has(skill) ? LM.Get(skill) : skill));

        var this_page = page.GetComponent<YuanyingPage>();
        this_page.Text.text = sb.ToString();
    }
}
