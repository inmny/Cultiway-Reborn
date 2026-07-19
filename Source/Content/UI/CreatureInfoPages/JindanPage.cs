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

public class JindanPage : MonoBehaviour
{
    public Text Text { get; private set; }

    /// <summary>在角色信息页上创建金丹详情文本组件并应用基础字体样式。</summary>
    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<JindanPage>();
        var text = page.gameObject.AddComponent<Text>();

        text.font = Cultiway.UI.UiTheme.Current.Font;
        text.fontSize = 8;

        this_page.Text = text;
    }

    /// <summary>根据当前角色的组合快照刷新金丹名称、转数、原子、组成和代表法术。</summary>
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        ActorExtend ae = actor.GetExtend();
        var sb = new StringBuilder();

        ref Jindan jindan = ref ae.GetJindan();
        sb.AppendLine(string.Format("Cultiway.CoreFormation.Page.Jindan.Title".Localize(), jindan.GetName()));
        sb.AppendLine(string.Format("Cultiway.CoreFormation.Page.Jindan.Strength".Localize(), jindan.strength));
        sb.AppendLine(string.Format("Cultiway.CoreFormation.Page.Jindan.Stage".Localize(), jindan.stage));
        sb.AppendLine(string.Format("Cultiway.CoreFormation.Page.Atoms".Localize(),
            string.Join("、", CoreFormationComposer.GetActiveAtomNames(jindan.formation, jindan.stage))));
        AppendComposition(sb, jindan.formation.composition);
        var skill = jindan.formation.representative_skill_id;
        if (!string.IsNullOrEmpty(skill))
            sb.AppendLine(string.Format("Cultiway.CoreFormation.Page.Skill".Localize(),
                LM.Has(skill) ? LM.Get(skill) : skill));
        var nextStage = CoreFormationComposer.GetNextEvolutionStage(jindan.stage);
        if (nextStage > 0)
            sb.AppendLine(string.Format("Cultiway.CoreFormation.Page.Jindan.NextEvolution".Localize(), nextStage));

        var this_page = page.GetComponent<JindanPage>();
        this_page.Text.text = sb.ToString();
    }

    /// <summary>将归一化后的五行、阴阳与混沌组成追加为一行百分比文本。</summary>
    internal static void AppendComposition(StringBuilder sb, ElementComposition composition)
    {
        composition.Normalize();
        var values = composition.AsArray();
        var parts = new string[values.Length];
        for (var i = 0; i < values.Length; i++)
            parts[i] = $"{ElementIndex.ElementNames[i].Localize()}{values[i]:P0}";
        sb.AppendLine(string.Format("Cultiway.CoreFormation.Page.Elements".Localize(), string.Join(" ", parts)));
    }
}
