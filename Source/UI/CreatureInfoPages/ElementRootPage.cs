using Cultiway.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.UI.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using MathNet.Numerics.Distributions;
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

    private const float Q = 0.9f;

    /// <summary>按档位数缓存的强度阈值表（每档覆盖等分正态 CDF 区间）。</summary>
    private static readonly Dictionary<int, float[]> _edgeValueCache = new();

    /// <summary>计算指定档位数的强度阈值表，等价于原 36 档逻辑的泛化。</summary>
    private static float[] GetEdgeValues(int count)
    {
        if (_edgeValueCache.TryGetValue(count, out var cached)) return cached;

        var values = new float[count];
        float p = 1f / count;
        bool q_is_one = Mathf.Approximately(Q, 1);
        if (!q_is_one)
        {
            p = (1 - Q) / (1 - Mathf.Pow(Q, count));
        }

        for (var i = 0; i < values.Length; i++)
        {
            var s_i = q_is_one ? p * i : p * (1 - Mathf.Pow(Q, i)) / (1 - Q);
            var cdf = 0.5 + s_i / 2;
            values[i] = (float)Normal.InvCDF(0, 1, cdf);
        }

        _edgeValueCache[count] = values;
        return values;
    }

    /// <summary>
    /// 把单通道强度映射为档位序号(0..count-1)，strength 越大档位越高。
    /// </summary>
    private static int GetStrengthIndex(float strength, int count)
    {
        var edges = GetEdgeValues(count);
        for (int i = 0; i < edges.Length; i++)
        {
            if (strength <= edges[i]) return i;
        }
        return edges.Length - 1;
    }

    /// <summary>
    /// 返回强度对应的等级名（组合式 stage×level）。
    /// style 为 null 时兜底用 36 档 + 仙道默认 key（仅防御，正常情况都有风格）。
    /// </summary>
    private static string GetLevelName(float strength, ElementRootDisplayStyle style)
    {
        int count = style?.TotalLevelCount ?? 36;
        int idx = GetStrengthIndex(strength, count);

        int level_per_stage = style?.level_per_stage ?? 9;
        int stage_idx = idx / level_per_stage;
        int level_idx = idx % level_per_stage;

        if (style == null)
        {
            // 兜底：仙道默认 key
            return LM.Get($"Cultiway.Stage.{stage_idx}") + "阶" + LM.Get($"Cultiway.Level.{level_idx}");
        }

        string stage_name = LM.Get(style.stage_name_keys[stage_idx]);
        string level_name = LM.Get(style.level_name_keys[level_idx]);
        return style.level_format
            .Replace("{stage}", stage_name)
            .Replace("{level}", level_name);
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
            sb.AppendLine($"\t{LM.Get(ElementIndex.ElementNames[i])}: {GetLevelName(er[i], style)}");

        var overallLevel = GetLevelName(Mathf.Log(er.GetStrength()), style);
        sb.AppendLine($"{overall_lbl}: {overallLevel}");

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
