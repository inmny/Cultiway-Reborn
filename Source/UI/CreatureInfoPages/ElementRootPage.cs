using System.Text;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.UI.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.CreatureInfoPages;

public class ElementRootPage : MonoBehaviour
{
    public ElementRootDiagram Diagram { get; private set; }
    public Text Text { get; private set; }

    public static void Setup(CreatureInfoPage page)
    {
        var er_page = page.gameObject.AddComponent<ElementRootPage>();
        var content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        content.transform.SetParent(page.transform, false);
        UiLayout.Stretch(content.GetComponent<RectTransform>());

        var layout = content.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.spacing = 4f;

        // 详情页同一时刻只展示一个灵根图，启用完整动态不会形成列表级刷新负担。
        er_page.Diagram = ElementRootDiagram.Create(content.transform, "Element Root Diagram", 74f,
            ElementRootDiagramDetail.Large);

        var textObject = new GameObject("Details", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text),
            typeof(LayoutElement));
        textObject.transform.SetParent(content.transform, false);
        UiLayout.SetSize(textObject.transform, 168f, 210f);
        var text = textObject.GetComponent<Text>();

        text.font = Cultiway.UI.UiTheme.Current.Font;
        text.fontSize = 8;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        er_page.Text = text;
    }

    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        ActorExtend ae = actor.GetExtend();
        var sb = new StringBuilder();

        ElementRoot er = ae.GetElementRoot();

        // 按优先级取生物拥有的主体系；无体系时 style=null 走仙道默认风格兜底
        var cultisys = Cultisyses.GetDisplayCultisys(ae);
        var style = cultisys?.DisplayStyle;

        string cat_label   = style != null ? LM.Get(style.category_label_key)   : "灵根类别";
        string comp_label  = style != null ? LM.Get(style.components_label_key) : "各组分强度";
        string overall_lbl = style != null ? LM.Get(style.overall_label_key)    : "综合评价";

        sb.AppendLine($"{cat_label}: {er.Type.GetName(cultisys)}");
        sb.AppendLine($"\t{er.Type.GetDescription(cultisys)}");
        sb.AppendLine($"{comp_label}:");
        for (var i = 0; i < ElementIndex.ElementNames.Count; i++)
        {
            string levelName = StrengthLevelFormatter.GetLevelName(er[i], style);
            sb.AppendLine($"\t{LM.Get(ElementIndex.ElementNames[i])}: {levelName}");
        }

        var overallLevel = StrengthLevelFormatter.GetLevelName(Mathf.Log(er.GetStrength()), style);
        sb.AppendLine($"{overall_lbl}: {overallLevel}");

        if (ae.HasCultisys<Xian>())
        {
            var efficiency = CultivationEfficiencyResolver.Resolve(ae);
            var mainCultibook = ae.GetMainCultibook();
            sb.AppendLine();
            sb.AppendLine($"{LM.Get("Cultiway.ER.Aptitude.Intensity")}: {efficiency.Intensity:P0}");
            sb.AppendLine($"{LM.Get("Cultiway.ER.Aptitude.Purity")}: {efficiency.Purity:P0}");
            var affinity = mainCultibook == null
                ? LM.Get("Cultiway.ER.Aptitude.Undetermined")
                : efficiency.MainCultibookAffinity.ToString("P0");
            sb.AppendLine($"{LM.Get("Cultiway.ER.Aptitude.Affinity")}: {affinity}");
            sb.AppendLine(
                $"{LM.Get("Cultiway.ER.Aptitude.Multiplier")}: ×{efficiency.AptitudeMultiplier:0.##}");
            sb.AppendLine(
                $"{LM.Get("Cultiway.ER.Aptitude.MethodMultiplier")}: ×{efficiency.MethodMultiplier:0.##}");
            sb.AppendLine($"{LM.Get("Cultiway.ER.Aptitude.FinalMultiplier")}: ×{efficiency.FinalMultiplier:0.##}");
        }

        var er_page = page.GetComponent<ElementRootPage>();
        er_page.Text.text = sb.ToString();
        er_page.Diagram.SetElementRoot(er, er.Type.GetName(cultisys),
            $"{overall_lbl}: {overallLevel}");
    }

    /// <summary>
    /// 页面标题：按生物主体系风格的 page_title_key 查名。
    /// 无体系或未配置时回退 ui.csv 的 ElementRootPage key（仙道"灵根详解"）。
    /// </summary>
    public static string GetTitle(ActorExtend ae)
    {
        var style = Cultisyses.GetDisplayCultisys(ae)?.DisplayStyle;
        var title_key = !string.IsNullOrEmpty(style?.page_title_key) ? style.page_title_key : nameof(ElementRootPage);
        return LM.Get(title_key);
    }
}
