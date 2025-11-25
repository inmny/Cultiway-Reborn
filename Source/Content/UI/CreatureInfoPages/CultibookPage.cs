using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.UI.Prefab;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.CreatureInfoPages;

public class CultibookPage : MonoBehaviour
{
    public Text Text { get; private set; }
    public static void Setup(CreatureInfoPage page)
    {
        var this_page = page.gameObject.AddComponent<CultibookPage>();
        var text = page.gameObject.AddComponent<Text>();

        text.font = LocalizedTextManager.current_font;
        text.fontSize = 8;

        this_page.Text = text;
    }
    [Hotfixable]
    public static void Show(CreatureInfoPage page, Actor actor)
    {
        ActorExtend ae = actor.GetExtend();
        var sb = new StringBuilder();

        // 显示主修功法
        var mainCultibook = ae.GetMainCultibook();
        if (mainCultibook != null)
        {
            sb.AppendLine("★ 主修功法");
            sb.AppendLine($"名称: {mainCultibook.Name}");
            var levelName = mainCultibook.Level.GetName();
            if (!string.IsNullOrEmpty(levelName))
            {
                sb.AppendLine($"品阶: {levelName}");
            }

            var mastery = ae.GetMainCultibookMastery();
            sb.AppendLine($"掌握程度: {mastery:F1}%");
            
            // 显示掌握程度进度条
            var progressBar = GetMasteryProgressBar(mastery);
            sb.AppendLine($"\t{progressBar}");

            // 显示修炼方式
            if (!string.IsNullOrEmpty(mainCultibook.CultivateMethodId))
            {
                sb.AppendLine($"修炼方式: {mainCultibook.CultivateMethodId}");
            }

            // 显示属性加成（根据掌握程度）
            if (mainCultibook.FinalStats != null)
            {
                sb.AppendLine("属性加成:");
                var masteryRatio = mastery / 100f;
                AppendStatsInfo(sb, mainCultibook.FinalStats, masteryRatio);
            }

            // 显示可领悟法术
            if (mainCultibook.SkillPool != null && mainCultibook.SkillPool.Count > 0)
            {
                sb.AppendLine("可领悟法术:");
                foreach (var skillEntry in mainCultibook.SkillPool)
                {
                    var hasSkill = CheckHasSkill(ae, skillEntry.SkillEntityAssetId);
                    var mark = hasSkill ? "✓" : "○";
                    var status = hasSkill ? " (已领悟)" : $" (需{skillEntry.MasteryThreshold}%掌握)";
                    
                    var skillAsset = ModClass.I.SkillV3.SkillLib.get(skillEntry.SkillEntityAssetId);
                    var skillName = skillAsset != null ? skillAsset.id.Localize() : skillEntry.SkillEntityAssetId;
                    sb.AppendLine($"\t{mark} {skillName}{status}");
                }
            }

            sb.AppendLine();
        }

        // 显示了解功法（排除主修功法）
        var knownCultibooks = ae.GetAllMaster<CultibookAsset>()
            .Where(cb => mainCultibook == null || cb.Item1.id != mainCultibook.id)
            .ToList();

        if (knownCultibooks.Count > 0)
        {
            sb.AppendLine("○ 了解功法");
            foreach (var cultibook_master in knownCultibooks)
            {
                var cultibook = cultibook_master.Item1;
                var knowledgeLevel = cultibook_master.Item2;
                sb.AppendLine($"- {cultibook.Name} ({knowledgeLevel:F1}%)");
            }
        }

        var this_page = page.GetComponent<CultibookPage>();
        if (this_page == null || this_page.Text == null) return;
        this_page.Text.text = sb.ToString();
    }

    /// <summary>
    /// 生成掌握程度进度条（文本形式）
    /// </summary>
    private static string GetMasteryProgressBar(float mastery)
    {
        const int barLength = 10;
        var filled = Mathf.RoundToInt(mastery / 100f * barLength);
        filled = Mathf.Clamp(filled, 0, barLength);
        var empty = barLength - filled;
        return new string('█', filled) + new string('░', empty);
    }

    /// <summary>
    /// 检查角色是否已学习某个技能
    /// </summary>
    private static bool CheckHasSkill(ActorExtend ae, string skillEntityAssetId)
    {
        if (ae.all_skills == null || ae.all_skills.Count == 0) return false;
        
        foreach (var skillEntity in ae.all_skills)
        {
            if (!skillEntity.HasComponent<SkillContainer>()) continue;
            ref var skillContainer = ref skillEntity.GetComponent<SkillContainer>();
            if (skillContainer.SkillEntityAssetID == skillEntityAssetId)
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// 追加属性加成信息
    /// </summary>
    private static void AppendStatsInfo(StringBuilder sb, BaseStats finalStats, float ratio)
    {
        if (finalStats == null) return;
        
        var statsList = finalStats._stats_list as IList<BaseStatsContainer>;
        if (statsList == null || statsList.Count == 0) return;

        foreach (var statContainer in statsList)
        {
            var value = statContainer.value * ratio;
            if (Mathf.Abs(value) < 0.01f) continue; // 忽略过小的值

            var statAsset = AssetManager.base_stats_library.get(statContainer.id);
            if (statAsset == null) continue;

            var sign = value >= 0 ? "+" : "";
            sb.AppendLine($"\t- {statContainer.id}: {sign}{value:F1}");
        }
    }
}