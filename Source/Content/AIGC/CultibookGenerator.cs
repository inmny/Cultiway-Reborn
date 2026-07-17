using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Content.Events;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.Semantics;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Newtonsoft.Json;

namespace Cultiway.Content.AIGC;

public class CultibookGenerator
{
    public static CultibookGenerator Instance { get; } = new();

    private const float DefaultResponseSeconds = 0.01f;

    private class SkillPoolEntryDto
    {
        public long entityId { get; set; }  // Entity 的 id
        public float baseChance { get; set; }
        public float masteryThreshold { get; set; }
        public int levelRequirement { get; set; }
    }

    private class LlmResponse
    {
        public string name { get; set; }
        public string description { get; set; }
        public ElementRequirement elementReq { get; set; }
        public float elementAffinityThreshold { get; set; }
        public int minLevel { get; set; }
        public int maxLevel { get; set; }
        public string cultivateMethodId { get; set; }
        public List<SkillPoolEntryDto> skillPool { get; set; }
    }

    public void RequestGeneration(ActorExtend ae, string requestId)
    {
        if (ae == null || ae.Base == null || ae.Base.isRekt()) return;
        _ = GenerateAsync(ae, requestId);
    }

    public void RequestImprovement(ActorExtend ae, CultibookAsset originalCultibook, string requestId)
    {
        if (ae == null || ae.Base == null || ae.Base.isRekt() || originalCultibook == null) return;
        _ = ImproveAsync(ae, originalCultibook, requestId);
    }

    private async Task GenerateAsync(ActorExtend ae, string requestId)
    {
        var actor = ae.Base;
        var actorId = actor.data.id;
        var stopwatch = Stopwatch.StartNew();

        CultibookAsset draft = null;
        try
        {
            draft = await LLMBuildDraftAsync(ae);
            stopwatch.Stop();
        }
        catch (Exception e)
        {
            stopwatch.Stop();
            ModClass.LogErrorConcurrent(e.ToString());
        }
        if (draft == null)
        {
            draft = FallbackBuildDraft(ae);
        }

        var responseSeconds = (float)stopwatch.Elapsed.TotalSeconds;
        if (responseSeconds <= 0)
        {
            responseSeconds = DefaultResponseSeconds;
        }

        EventSystemHub.Publish(new CultibookGeneratedEvent
        {
            ActorId = actorId,
            RequestId = requestId,
            Draft = draft,
            ResponseSeconds = responseSeconds
        });
    }

    private async Task ImproveAsync(ActorExtend ae, CultibookAsset originalCultibook, string requestId)
    {
        var actor = ae.Base;
        var actorId = actor.data.id;
        var stopwatch = Stopwatch.StartNew();

        CultibookAsset improvedDraft = null;
        try
        {
            improvedDraft = await LLMBuildImprovedDraftAsync(ae, originalCultibook);
            stopwatch.Stop();
        }
        catch (Exception e)
        {
            stopwatch.Stop();
            ModClass.LogErrorConcurrent(e.ToString());

        }
        if (improvedDraft == null)
        {
            improvedDraft = FallbackBuildImprovedDraft(ae, originalCultibook);
        }

        var responseSeconds = (float)stopwatch.Elapsed.TotalSeconds;
        if (responseSeconds <= 0)
        {
            responseSeconds = DefaultResponseSeconds;
        }

        EventSystemHub.Publish(new CultibookImprovedEvent
        {
            ActorId = actorId,
            RequestId = requestId,
            OriginalCultibook = originalCultibook,
            ImprovedDraft = improvedDraft,
            ResponseSeconds = responseSeconds
        });
    }

    private static CultibookAsset FallbackBuildImprovedDraft(ActorExtend ae, CultibookAsset originalCultibook)
    {
        return CultibookRuleComposer.CreateImprovedDraft(originalCultibook, ae);
    }

    private static async Task<CultibookAsset> LLMBuildImprovedDraftAsync(ActorExtend ae, CultibookAsset originalCultibook)
    {
        var basePrompt = new StringBuilder();
        BuildImprovementPromptBase(ae, originalCultibook, basePrompt);
        var (prompt, clonedSkills) = PrepareSkillsForPrompt(ae, basePrompt);

        var system_prompt = GetImprovementSystemPrompt();
        var response = await Core.AIGCLib.Manager.RequestResponseContent(prompt, system_prompt, temperature: 0.7f);

        response = response.PostProcessForJSON();
        var dto = JsonConvert.DeserializeObject<LlmResponse>(response);
        if (dto == null)
        {
            // 清理未使用的 clone 技能
            foreach (var clonedEntity in clonedSkills.Values)
            {
                clonedEntity.RemoveTag<TagOccupied>();
            }
            return FallbackBuildImprovedDraft(ae, originalCultibook);
        }

        var skillPool = ConvertSkillPoolDtoToEntries(dto.skillPool ?? new List<SkillPoolEntryDto>(), clonedSkills);
        var draft = new CultibookAsset
        {
            id = Guid.NewGuid().ToString(),
            Name = dto.name,
            Description = dto.description,
            ElementReq = dto.elementReq,
            ElementAffinityThreshold = dto.elementAffinityThreshold,
            MinLevel = dto.minLevel,
            MaxLevel = dto.maxLevel,
            CultivateMethodId = dto.cultivateMethodId,
            SkillPool = skillPool,
            ConflictConditions = originalCultibook.ConflictConditions?.ToArray() ??
                                 Array.Empty<SemanticQueryExpression>(),
            SynergyConditions = originalCultibook.SynergyConditions?.ToArray() ??
                                Array.Empty<SemanticQueryExpression>()
        };
        return CultibookRuleComposer.NormalizeDraft(draft, ae, originalCultibook);
    }

    private static string GetImprovementSystemPrompt()
    {
        return
            "请根据原功法信息(例如玄火功)生成改进版功法的名称与简介，只输出 JSON，例如 {\"name\":\"玄火九转功\",\"description\":\"简介不超过60字，说明改进之处\",\"elementReq\":{\"iron\":0.2,\"wood\":0.3,\"water\":0.0,\"fire\":1.5,\"earth\":0.1,\"neg\":0.1,\"pos\":0.8,\"entropy\":0.5},\"elementAffinityThreshold\":0.3,\"minLevel\":1,\"maxLevel\":4,\"cultivateMethodId\":\"Cultiway.Standard\",\"skillPool\":[{\"entityId\":12345,\"baseChance\":0.05,\"masteryThreshold\":20,\"levelRequirement\":1}]}，不要输出其他内容。entityId 是技能实体的 id，从 prompt 中提供的候选技能中选择。改进版功法应该在原功法基础上有所提升，修炼要求有一定变化，不限制增长还是下降，不一定要保持相同的修炼方式。可选的修炼方式：" + string.Join(", ", Libraries.Manager.CultivateMethodLibrary.list.Select(m => $"\"{m.id.Localize()}\"({m.id})")) + "。";
    }

    private static void BuildImprovementPromptBase(ActorExtend ae, CultibookAsset originalCultibook, StringBuilder sb)
    {
        var actor = ae.Base;
        var level_value = ae.HasCultisys<Xian>() ? ae.GetCultisys<Xian>().CurrLevel : 0;
        var level_text = Cultisyses.Xian.GetLevelName(level_value);
        var element = ae.HasElementRoot() ? ae.GetElementRoot() : default;
        var element_name = ae.HasElementRoot() ? element.Type.GetName(Cultisyses.GetDisplayCultisys(ae)) : "无灵根";

        sb.Append("原功法信息：");
        sb.Append($"名称 {originalCultibook.Name}，");
        sb.Append($"简介 {originalCultibook.Description}，");
        sb.Append($"境界范围 {originalCultibook.MinLevel}-{originalCultibook.MaxLevel}，\n");
        sb.Append($"灵根需求 金{originalCultibook.ElementReq.MinIron}木{originalCultibook.ElementReq.MinWood}水{originalCultibook.ElementReq.MinWater}火{originalCultibook.ElementReq.MinFire}土{originalCultibook.ElementReq.MinEarth}阴{originalCultibook.ElementReq.MinNeg}阳{originalCultibook.ElementReq.MinPos}混沌{originalCultibook.ElementReq.MinEntropy}，\n");
        sb.Append($"灵根契合度阈值 {originalCultibook.ElementAffinityThreshold}，\n");
        var skillPoolInfo = new List<string>();
        foreach (var entry in originalCultibook.SkillPool)
        {
            if (entry.SkillContainer.IsNull || !entry.SkillContainer.HasComponent<SkillContainer>()) continue;
            var container = entry.SkillContainer.GetComponent<SkillContainer>();
            var skillId = container.SkillEntityAssetID;
            var skill_name = entry.SkillContainer.HasName ? entry.SkillContainer.Name.value : skillId.Localize();
            if (string.IsNullOrEmpty(skillId)) continue;
            skillPoolInfo.Add($"{skill_name}({entry.SkillContainer.Id})，概率{entry.BaseChance}，熟练度阈值{entry.MasteryThreshold}，等级要求{entry.LevelRequirement}");
        }
        if (skillPoolInfo.Count > 0)
        {
            sb.Append($"法术池 {string.Join(", ", skillPoolInfo)}\n");
        }
        sb.Append($"修炼方式 {originalCultibook.CultivateMethodId}。");
        sb.Append("\n");

        sb.Append("改进者信息：");
        sb.Append($"姓名 {actor.getName()}，境界 {level_text}({level_value})，灵根 {element_name.Replace("五行", "杂")}(");
        sb.Append("金");
        sb.Append(element.Iron);
        sb.Append("木");
        sb.Append(element.Wood);
        sb.Append("水");
        sb.Append(element.Water);
        sb.Append("火");
        sb.Append(element.Fire);
        sb.Append("土");
        sb.Append(element.Earth);
        sb.Append("阴");
        sb.Append(element.Neg);
        sb.Append("阳");
        sb.Append(element.Pos);
        sb.Append("混沌");
        sb.Append(element.Entropy);
        sb.Append(")。");
    }

    private static CultibookAsset FallbackBuildDraft(ActorExtend ae)
    {
        return CultibookRuleComposer.CreateDraft(ae);
    }
    /// <summary>
    /// Clone 候选技能并构建包含技能信息的 prompt
    /// </summary>
    private static (string prompt, Dictionary<long, Entity> clonedSkills) PrepareSkillsForPrompt(ActorExtend ae, StringBuilder basePrompt)
    {
        var clonedSkills = new Dictionary<long, Entity>();
        var skillInfoList = new List<string>();

        if (ae.all_skills != null && ae.all_skills.Count > 0)
        {
            foreach (var skillEntity in ae.all_skills)
            {
                if (!skillEntity.HasComponent<SkillContainer>()) continue;
                var container = skillEntity.GetComponent<SkillContainer>();
                if (string.IsNullOrEmpty(container.SkillEntityAssetID)) continue;

                // Clone 技能实体
                var clonedEntity = skillEntity.Store.CloneEntity(skillEntity);
                clonedEntity.AddTag<TagOccupied>();
                var entityId = clonedEntity.Id;
                clonedSkills[entityId] = clonedEntity;

                // 构建技能信息字符串
                var skillName = clonedEntity.HasName ? clonedEntity.Name.value : container.SkillEntityAssetID.Localize();
                skillInfoList.Add($"{skillName}({entityId})");
            }
        }

        if (skillInfoList.Count > 0)
        {
            basePrompt.Append(" 候选法术：");
            basePrompt.Append(string.Join("、", skillInfoList));
            basePrompt.Append('。');
        }

        return (basePrompt.ToString(), clonedSkills);
    }

    private static async Task<CultibookAsset> LLMBuildDraftAsync(ActorExtend ae)
    {
        var basePrompt = new StringBuilder();
        BuildPromptBase(ae, basePrompt);
        var (prompt, clonedSkills) = PrepareSkillsForPrompt(ae, basePrompt);

        var system_prompt = GetSystemPrompt();
        var response = await Core.AIGCLib.Manager.RequestResponseContent(prompt, system_prompt, temperature: 0.7f);

        response = response.PostProcessForJSON();
        var dto = JsonConvert.DeserializeObject<LlmResponse>(response);
        if (dto == null)
        {
            // 清理未使用的 clone 技能
            foreach (var clonedEntity in clonedSkills.Values)
            {
                clonedEntity.RemoveTag<TagOccupied>();
            }
            return null;
        }

        var skillPool = ConvertSkillPoolDtoToEntries(dto.skillPool ?? new List<SkillPoolEntryDto>(), clonedSkills);
        var draft = new CultibookAsset
        {
            id = Guid.NewGuid().ToString(),
            Name = dto.name,
            Description = dto.description,
            ElementReq = dto.elementReq,
            ElementAffinityThreshold = dto.elementAffinityThreshold,
            MinLevel = dto.minLevel,
            MaxLevel = dto.maxLevel,
            CultivateMethodId = dto.cultivateMethodId,
            SkillPool = skillPool,
            ConflictConditions = Array.Empty<SemanticQueryExpression>(),
            SynergyConditions = Array.Empty<SemanticQueryExpression>()
        };
        return CultibookRuleComposer.NormalizeDraft(draft, ae);
    }

    private static void BuildPromptBase(ActorExtend ae, StringBuilder sb)
    {
        var actor = ae.Base;
        var level_value = ae.HasCultisys<Xian>() ? ae.GetCultisys<Xian>().CurrLevel : 0;
        var level_text = Cultisyses.Xian.GetLevelName(level_value);
        var element = ae.HasElementRoot() ? ae.GetElementRoot() : default;
        var element_name = ae.HasElementRoot() ? element.Type.GetName(Cultisyses.GetDisplayCultisys(ae)) : "无灵根";

        var method_value = ae.GetMainCultibook()?.GetCultivateMethod()?.id ?? CultivateMethods.Standard.id;
        var method_text = method_value.Localize();

        sb.Append("角色信息：");
        sb.Append($"姓名 {actor.getName()}，境界 {level_text}({level_value})，灵根 {element_name.Replace("五行","杂")}(");
        sb.Append("金");
        sb.Append(element.Iron);
        sb.Append("木");
        sb.Append(element.Wood);
        sb.Append("水");
        sb.Append(element.Water);
        sb.Append("火");
        sb.Append(element.Fire);
        sb.Append("土");
        sb.Append(element.Earth);
        sb.Append("阴");
        sb.Append(element.Neg);
        sb.Append("阳");
        sb.Append(element.Pos);
        sb.Append("混沌");
        sb.Append(element.Entropy);
        sb.Append($")，修炼方式 {method_text}({method_value})。");
    }

    private static string GetSystemPrompt()
    {
        return
            "请根据修仙者的背景生成功法名称与简介，只输出 JSON，例如 {\"name\":\"玄火九转功\",\"description\":\"简介不超过60字\",\"elementReq\":{\"iron\":0.2,\"wood\":0.3,\"water\":0.0,\"fire\":1.5,\"earth\":0.1,\"neg\":0.1,\"pos\":0.8,\"entropy\":0.5},\"elementAffinityThreshold\":0.3,\"minLevel\":1,\"maxLevel\":4,\"cultivateMethodId\":\"Cultiway.Standard\",\"skillPool\":[{\"entityId\":12345,\"baseChance\":0.05,\"masteryThreshold\":20,\"levelRequirement\":1},{\"entityId\":12346,\"baseChance\":0.02,\"masteryThreshold\":80,\"levelRequirement\":3}]}，不要输出其他内容。entityId 是技能实体的 id，从 prompt 中提供的候选技能中选择。可选的修炼方式：" + string.Join(", ", Libraries.Manager.CultivateMethodLibrary.list.Select(m => $"\"{m.id.Localize()}\"({m.id})")) + "。";
    }

    /// <summary>
    /// 将技能池 DTO 转换为使用 Entity 的 SkillPoolEntry，并清理未选中的 Entity
    /// </summary>
    private static List<SkillPoolEntry> ConvertSkillPoolDtoToEntries(List<SkillPoolEntryDto> dtoList, Dictionary<long, Entity> clonedSkills)
    {
        var result = new List<SkillPoolEntry>();
        var selectedEntityIds = new HashSet<long>();

        if (dtoList != null && dtoList.Count > 0)
        {
            foreach (var dto in dtoList)
            {
                if (dto.entityId == 0) continue;
                if (!clonedSkills.TryGetValue(dto.entityId, out var skillContainer)) continue;

                selectedEntityIds.Add(dto.entityId);
                result.Add(new SkillPoolEntry
                {
                    SkillContainer = skillContainer,
                    BaseChance = dto.baseChance,
                    MasteryThreshold = dto.masteryThreshold,
                    LevelRequirement = dto.levelRequirement
                });
            }
        }

        // 删除未选中的 clone 技能
        foreach (var kvp in clonedSkills)
        {
            if (!selectedEntityIds.Contains(kvp.Key))
            {
                kvp.Value.RemoveTag<TagOccupied>();
            }
        }

        return result;
    }
}
