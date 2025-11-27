using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.UI.Prefab;

/// <summary>
/// 功法书 Tooltip 显示
/// </summary>
public class CultibookTooltip : APrefabPreview<CultibookTooltip>
{
    public Tooltip Tooltip { get; private set; }

    protected override void OnInit()
    {
        Tooltip = GetComponent<Tooltip>();
    }

    [Hotfixable]
    public void Setup(Book book)
    {
        Init();

        var be = book?.GetExtend();
        if (be == null || !be.HasComponent<Cultibook>()) return;
        var cultibook = be.GetComponent<Cultibook>().Asset;
        if (cultibook == null) return;

        Tooltip.name.text = $"《{cultibook.Name}》";

        AppendDescription(cultibook);
        AppendItemLevel(cultibook.Level);
        AppendElementRequirements(cultibook.ElementReq);
        AppendLevelRequirements(cultibook);
        AppendCultivateMethod(cultibook);
        AppendFullMasteryStats(cultibook.FinalStats);
        AppendSkillPool(cultibook);
        AppendBottomInfo(book);
    }

    /// <summary>
    /// 追加功法书简介。
    /// </summary>
    private void AppendDescription(CultibookAsset cultibook)
    {
        if (string.IsNullOrEmpty(cultibook.Description)) return;
        Tooltip.addDescription(cultibook.Description);
    }

    /// <summary>
    /// 追加品阶与星级。
    /// </summary>
    private void AppendItemLevel(ItemLevel level)
    {
        var levelText = level.GetName();
        var stars = Mathf.Clamp(level.Stage, 0, 3);
        if (stars > 0)
        {
            levelText += $" {new string('★', stars)}";
        }

        Tooltip.addLineText("品阶", levelText, pLocalize: false);
    }

    /// <summary>
    /// 追加灵根需求。
    /// </summary>
    private void AppendElementRequirements(ElementRequirement elementReq)
    {
        var lines = new List<string>();

        AppendElementReqIfNeeded(lines, ElementIndex.Iron, elementReq.MinIron);
        AppendElementReqIfNeeded(lines, ElementIndex.Wood, elementReq.MinWood);
        AppendElementReqIfNeeded(lines, ElementIndex.Water, elementReq.MinWater);
        AppendElementReqIfNeeded(lines, ElementIndex.Fire, elementReq.MinFire);
        AppendElementReqIfNeeded(lines, ElementIndex.Earth, elementReq.MinEarth);
        AppendElementReqIfNeeded(lines, ElementIndex.Neg, elementReq.MinNeg);
        AppendElementReqIfNeeded(lines, ElementIndex.Pos, elementReq.MinPos);
        AppendElementReqIfNeeded(lines, ElementIndex.Entropy, elementReq.MinEntropy);

        AddLineGroup("灵根需求", lines);
    }

    /// <summary>
    /// 追加境界限制。
    /// </summary>
    private void AppendLevelRequirements(CultibookAsset cultibook)
    {
        if (cultibook.MinLevel <= 0 && cultibook.MaxLevel >= 20) return;

        var parts = new List<string>();
        if (cultibook.MinLevel > 0)
        {
            parts.Add($"{Cultisyses.Xian.GetLevelName(cultibook.MinLevel)}以上");
        }

        if (cultibook.MaxLevel < 20)
        {
            parts.Add($"不高于{Cultisyses.Xian.GetLevelName(cultibook.MaxLevel)}");
        }

        if (parts.Count == 0) return;
        Tooltip.addLineText("境界要求", string.Join("，", parts), pLocalize: false);
    }

    /// <summary>
    /// 追加修炼方式。
    /// </summary>
    private void AppendCultivateMethod(CultibookAsset cultibook)
    {
        if (string.IsNullOrEmpty(cultibook.CultivateMethodId)) return;

        var method = cultibook.GetCultivateMethod();
        if (method == null) return;

        var methodName = LMTools.Has(method.id) ? LM.Get(method.id) : method.id;
        Tooltip.addLineText("修炼方式", methodName, pLocalize: false);

        var methodInfoKey = $"{method.id}.Info";
        if (LMTools.Has(methodInfoKey))
        {
            Tooltip.addDescription(LM.Get(methodInfoKey));
        }
    }

    /// <summary>
    /// 追加满掌握属性加成。
    /// </summary>
    private void AppendFullMasteryStats(BaseStats finalStats)
    {
        var statLines = BuildStatLines(finalStats);
        AddLineGroup("满掌握属性加成", statLines);
    }

    /// <summary>
    /// 追加可领悟法术列表。
    /// </summary>
    private void AppendSkillPool(CultibookAsset cultibook)
    {
        if (cultibook.SkillPool == null || cultibook.SkillPool.Count == 0) return;

        var lines = new List<string>();
        foreach (var skillEntry in cultibook.SkillPool)
        {
            if (skillEntry.SkillContainer.IsNull || !skillEntry.SkillContainer.HasComponent<SkillContainer>()) continue;
            var container = skillEntry.SkillContainer.GetComponent<SkillContainer>();
            var skillId = container.SkillEntityAssetID;
            if (string.IsNullOrEmpty(skillId)) continue;
            
            var skillName = skillEntry.SkillContainer.HasName ? skillEntry.SkillContainer.Name.value : skillId.Localize();

            var requirements = new List<string>();
            if (skillEntry.MasteryThreshold > 0)
            {
                requirements.Add($"需{skillEntry.MasteryThreshold:F0}%掌握");
            }

            if (skillEntry.LevelRequirement > 0)
            {
                requirements.Add(Cultisyses.Xian.GetLevelName(skillEntry.LevelRequirement));
            }

            var suffix = requirements.Count > 0 ? $" ({string.Join("，", requirements)})" : string.Empty;
            lines.Add($"- {skillName}{suffix}");
        }

        AddLineGroup("可领悟法术", lines);
    }

    /// <summary>
    /// 追加底部附加信息。
    /// </summary>
    private void AppendBottomInfo(Book book)
    {
        var bottomLines = new List<string>();
        if (!string.IsNullOrEmpty(book.data.author_name))
        {
            bottomLines.Add($"抄录者: {book.data.author_name}");
        }

        if (bottomLines.Count == 0) return;
        Tooltip.addBottomDescription(string.Join("\n", bottomLines));
    }

    /// <summary>
    /// 灵根需求格式化辅助。
    /// </summary>
    private static void AppendElementReqIfNeeded(ICollection<string> lines, int elementIndex, float minValue)
    {
        if (minValue <= 0) return;
        var elementName = LM.Get(ElementIndex.ElementNames[elementIndex]);
        lines.Add($"{elementName} ≥ {minValue:F1}");
    }

    /// <summary>
    /// 将属性加成转为展示行。
    /// </summary>
    private static List<string> BuildStatLines(BaseStats finalStats)
    {
        var lines = new List<string>();
        if (finalStats == null) return lines;

        if (finalStats._stats_list is not IList<BaseStatsContainer> statsList || statsList.Count == 0)
        {
            return lines;
        }

        foreach (var statContainer in statsList)
        {
            var value = statContainer.value;
            if (Mathf.Abs(value) < 0.01f) continue;

            var statAsset = AssetManager.base_stats_library.get(statContainer.id);
            if (statAsset == null) continue;

            var sign = value >= 0 ? "+" : string.Empty;
            var visualValue = value * statAsset.tooltip_multiply_for_visual_number;
            var valueText = statAsset.show_as_percents ? $"{visualValue:F1}%" : $"{visualValue:F1}";
            lines.Add($"{statAsset.translation_key.Localize()}: {sign}{valueText}");
        }

        return lines;
    }

    /// <summary>
    /// 渲染多行分组，首行展示标题。
    /// </summary>
    private void AddLineGroup(string title, List<string> lines)
    {
        if (lines == null || lines.Count == 0) return;

        Tooltip.addLineText(title, lines[0], pLocalize: false);
        for (var i = 1; i < lines.Count; i++)
        {
            Tooltip.addLineText(string.Empty, lines[i], pLocalize: false);
        }
    }

    private static void _init()
    {
        GameObject obj = Instantiate(Resources.Load<GameObject>(WorldboxGame.Tooltips.Tip.prefab_id),
            ModClass.I.PrefabLibrary);
        Prefab = obj.AddComponent<CultibookTooltip>();
    }
}
