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
using Cultiway.Core.AIGCLib;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.AIGC;

public class CultibookGenerator
{
    public static CultibookGenerator Instance { get; } = new();

    private const float DefaultResponseSeconds = 0.01f;

    private class LlmResponse
    {
        public string name { get; set; }
        public string description { get; set; }
        public ElementRequirement elementReq { get; set; }
        public float elementAffinityThreshold { get; set; }
        public int minLevel { get; set; }
        public int maxLevel { get; set; }
        public string cultivateMethodId { get; set; }
        public List<SkillPoolEntry> skillPool { get; set; }
        public List<string> conflictTags { get; set; }
        public List<string> synergyTags { get; set; }
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
        // 使用现有的 CreateImprovedCultibook 方法作为后备
        return Extensions.BookManagerTools.CreateImprovedCultibook(originalCultibook, ae);
    }

    private static async Task<CultibookAsset> LLMBuildImprovedDraftAsync(ActorExtend ae, CultibookAsset originalCultibook)
    {
        var prompt = BuildImprovementPrompt(ae, originalCultibook);
        var system_prompt = GetImprovementSystemPrompt();
        var response = await Core.AIGCLib.Manager.RequestResponseContent(prompt, system_prompt, temperature: 0.7f);

        response = response.PostProcessForJSON();
        var dto = JsonConvert.DeserializeObject<LlmResponse>(response);
        if (dto == null)
        {
            return FallbackBuildImprovedDraft(ae, originalCultibook);
        }

        // 使用改进后的数据，但确保创建者能够满足要求
        var stats = GenerateStatsFromActor(ae.Base);
        // 如果 LLM 返回的属性为空，使用原功法的属性并提升
        if (dto.skillPool == null || dto.skillPool.Count == 0)
        {
            dto.skillPool = originalCultibook.SkillPool ?? new List<SkillPoolEntry>();
        }

        // 确保灵根需求不超过创建者的灵根值
        if (ae.HasElementRoot())
        {
            var elementRoot = ae.GetElementRoot();
            var elementReq = dto.elementReq;
            elementReq.MinIron = Mathf.Min(elementReq.MinIron, elementRoot.Iron);
            elementReq.MinWood = Mathf.Min(elementReq.MinWood, elementRoot.Wood);
            elementReq.MinWater = Mathf.Min(elementReq.MinWater, elementRoot.Water);
            elementReq.MinFire = Mathf.Min(elementReq.MinFire, elementRoot.Fire);
            elementReq.MinEarth = Mathf.Min(elementReq.MinEarth, elementRoot.Earth);
            elementReq.MinNeg = Mathf.Min(elementReq.MinNeg, elementRoot.Neg);
            elementReq.MinPos = Mathf.Min(elementReq.MinPos, elementRoot.Pos);
            elementReq.MinEntropy = Mathf.Min(elementReq.MinEntropy, elementRoot.Entropy);
            dto.elementReq = elementReq;
        }

        // 确保境界范围包含创建者的境界
        int creatorLevel = 0;
        if (ae.HasCultisys<Xian>())
        {
            creatorLevel = ae.GetCultisys<Xian>().CurrLevel;
        }
        dto.minLevel = Mathf.Max(0, Mathf.Min(dto.minLevel, creatorLevel));
        dto.maxLevel = Mathf.Max(Mathf.Max(creatorLevel, dto.maxLevel), dto.minLevel);
        dto.maxLevel = Mathf.Min(dto.maxLevel, 20);

        var item_level = CalculateCultibookLevel(stats, dto.skillPool, dto.minLevel, dto.maxLevel, dto.cultivateMethodId, ae);

        return new CultibookAsset()
        {
            id = Guid.NewGuid().ToString(),
            Name = string.IsNullOrEmpty(dto.name) ? $"{originalCultibook.Name}（改进版）" : dto.name,
            Description = string.IsNullOrEmpty(dto.description) ? $"{originalCultibook.Description}（改进版）" : dto.description,
            FinalStats = stats,
            Level = item_level,
            ElementReq = dto.elementReq,
            ElementAffinityThreshold = dto.elementAffinityThreshold,
            MinLevel = dto.minLevel,
            MaxLevel = dto.maxLevel,
            CultivateMethodId = dto.cultivateMethodId,
            SkillPool = dto.skillPool,
            ConflictTags = originalCultibook.ConflictTags?.ToArray() ?? Array.Empty<string>(),
            SynergyTags = originalCultibook.SynergyTags?.ToArray() ?? Array.Empty<string>()
        };
    }

    private static string GetImprovementSystemPrompt()
    {
        return
            "请根据原功法信息生成改进版功法的名称与简介，只输出 JSON，例如 {\\\"name\\\":\\\"玄火九转功·改进版\\\",\\\"description\\\":\\\"简介不超过60字，说明改进之处\\\",\\\"elementReq\\\":{\\\"iron\\\":0.2,\\\"wood\\\":0.3,\\\"water\\\":0.0,\\\"fire\\\":1.5,\\\"earth\\\":0.1,\\\"neg\\\":0.1,\\\"pos\\\":0.8,\\\"entropy\\\":0.5},\\\"elementAffinityThreshold\\\":0.3,\\\"minLevel\\\":1,\\\"maxLevel\\\":4,\\\"cultivateMethodId\\\":\\\"Cultiway.Standard\\\",\\\"skillPool\\\":[{\\\"skillEntityAssetId\\\":\\\"Cultiway.Fireball\\\",\\\"baseChance\\\":0.05,\\\"masteryThreshold\\\":20,\\\"levelRequirement\\\":1}]}，不要输出其他内容。改进版功法应该在原功法基础上有所提升，修炼要求有一定变化，不限制增长还是下降，不一定要保持相同的修炼方式。可选的修炼方式：" + string.Join(", ", Libraries.Manager.CultivateMethodLibrary.list.Select(m => $"\\\"{m.id.Localize()}\\\"({m.id})")) + "。";
    }

    private static string BuildImprovementPrompt(ActorExtend ae, CultibookAsset originalCultibook)
    {
        var actor = ae.Base;
        var level_value = ae.HasCultisys<Xian>() ? ae.GetCultisys<Xian>().CurrLevel : 0;
        var level_text = Cultisyses.Xian.GetLevelName(level_value);
        var element = ae.HasElementRoot() ? ae.GetElementRoot() : default;
        var element_name = ae.HasElementRoot() ? element.Type.GetName() : "无灵根";

        var sb = new StringBuilder();
        sb.Append("原功法信息：");
        sb.Append($"名称 {originalCultibook.Name}，");
        sb.Append($"简介 {originalCultibook.Description}，");
        sb.Append($"境界范围 {originalCultibook.MinLevel}-{originalCultibook.MaxLevel}，\n");
        sb.Append($"灵根需求 金{originalCultibook.ElementReq.MinIron}木{originalCultibook.ElementReq.MinWood}水{originalCultibook.ElementReq.MinWater}火{originalCultibook.ElementReq.MinFire}土{originalCultibook.ElementReq.MinEarth}阴{originalCultibook.ElementReq.MinNeg}阳{originalCultibook.ElementReq.MinPos}混沌{originalCultibook.ElementReq.MinEntropy}，\n");
        sb.Append($"灵根契合度阈值 {originalCultibook.ElementAffinityThreshold}，\n");
        sb.Append($"法术池 {string.Join(", ", originalCultibook.SkillPool.Select(s => $"{s.SkillEntityAssetId.Localize()}({s.SkillEntityAssetId})，概率{s.BaseChance}，熟练度阈值{s.MasteryThreshold}，等级要求{s.LevelRequirement}"))}\n");
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

        var skills = ae.all_skills?.Where(s => s.HasComponent<SkillContainer>())
            .Select(s => s.GetComponent<SkillContainer>().SkillEntityAssetID)
            .Where(id => !string.IsNullOrEmpty(id))
            .Take(3)
            .Select(id => $"{id.Localize()}({id})")
            .ToArray() ?? Array.Empty<string>();
        if (skills.Length > 0)
        {
            sb.Append(" 常用法术：");
            sb.Append(string.Join("、", skills));
            sb.Append('。');
        }
        sb.Append(" 请给出改进版功法的名称与简介，简介需说明改进之处，并保持与原功法风格一致。");
        return sb.ToString();
    }
    private static CultibookAsset FallbackBuildDraft(ActorExtend ae)
    {
        var stats = GenerateStatsFromActor(ae.Base);
        var elementReq = GenerateElementRequirement(ae);
        GenerateLevelRange(ae, out var minLevel, out var maxLevel);
        var cultivateMethodId = DetermineCultivateMethodId(ae);
        var skillPool = GenerateSkillPool(ae, minLevel);
        ItemLevel itemLevel =
            CalculateCultibookLevel(stats, skillPool, minLevel, maxLevel, cultivateMethodId, ae);

        return new CultibookAsset()
        {
            id = Guid.NewGuid().ToString(),
            FinalStats = stats,
            Level = itemLevel,
            ElementReq = elementReq,
            ElementAffinityThreshold = 0.3f,
            MinLevel = minLevel,
            MaxLevel = maxLevel,
            CultivateMethodId = cultivateMethodId,
            SkillPool = skillPool,
            ConflictTags = Array.Empty<string>(),
            SynergyTags = Array.Empty<string>()
        };
    }
    private static async Task<CultibookAsset> LLMBuildDraftAsync(ActorExtend ae)
    {
        var prompt = BuildPrompt(ae);
        var system_prompt = GetSystemPrompt();
        var response = await Core.AIGCLib.Manager.RequestResponseContent(prompt, system_prompt, temperature: 0.7f);

        response = response.PostProcessForJSON();
        var dto = JsonConvert.DeserializeObject<LlmResponse>(response);
        if (dto == null)
        {
            return null;
        }
        var stats = GenerateStatsFromActor(ae.Base);
        var item_level = CalculateCultibookLevel(stats, dto.skillPool, dto.minLevel, dto.maxLevel, dto.cultivateMethodId, ae);  

        return new CultibookAsset()
        {
            id = Guid.NewGuid().ToString(),
            Name = dto.name,
            Description = dto.description,
            FinalStats = stats,
            Level = item_level,
            ElementReq = dto.elementReq,
            ElementAffinityThreshold = dto.elementAffinityThreshold,
            MinLevel = dto.minLevel,
            MaxLevel = dto.maxLevel,
            CultivateMethodId = dto.cultivateMethodId,
            SkillPool = dto.skillPool,
            ConflictTags = Array.Empty<string>(),
            SynergyTags = Array.Empty<string>()
        };
    }

    private static string GetSystemPrompt()
    {
        return
            "请根据修仙者的背景生成功法名称与简介，只输出 JSON，例如 {\\\"name\\\":\\\"玄火九转功\\\",\\\"description\\\":\\\"简介不超过60字\\\",\\\"elementReq\\\":{\\\"iron\\\":0.2,\\\"wood\\\":0.3,\\\"water\\\":0.0,\\\"fire\\\":1.5,\\\"earth\\\":0.1,\\\"neg\\\":0.1,\\\"pos\\\":0.8,\\\"entropy\\\":0.5},\\\"elementAffinityThreshold\\\":0.3,\\\"minLevel\\\":1,\\\"maxLevel\\\":4,\\\"cultivateMethodId\\\":\\\"Cultiway.Standard\\\",\\\"skillPool\\\":[{\\\"skillEntityAssetId\\\":\\\"Cultiway.Fireball\\\",\\\"baseChance\\\":0.05,\\\"masteryThreshold\\\":20,\\\"levelRequirement\\\":1},{\\\"skillEntityAssetId\\\":\\\"Cultiway.FireBlade\\\",\\\"baseChance\\\":0.02,\\\"masteryThreshold\\\":80,\\\"levelRequirement\\\":3}]}，不要输出其他内容。可选的修炼方式：" + string.Join(", ", Libraries.Manager.CultivateMethodLibrary.list.Select(m => $"\\\"{m.id.Localize()}\\\"({m.id})")) + "。";
    }

    private static string BuildPrompt(ActorExtend ae)
    {
        var actor = ae.Base;
        var level_value = ae.HasCultisys<Xian>() ? ae.GetCultisys<Xian>().CurrLevel : 0;
        var level_text = Cultisyses.Xian.GetLevelName(level_value);
        var element = ae.HasElementRoot() ? ae.GetElementRoot() : default;
        var element_name = ae.HasElementRoot() ? element.Type.GetName() : "无灵根";

        var method_value = ae.GetMainCultibook()?.GetCultivateMethod()?.id ?? CultivateMethods.Standard.id;
        var method_text = method_value.Localize();


        var sb = new StringBuilder();
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
        

        var skills = ae.all_skills?.Where(s => s.HasComponent<SkillContainer>())
            .Select(s => s.GetComponent<SkillContainer>().SkillEntityAssetID)
            .Where(id => !string.IsNullOrEmpty(id))
            .Take(3)
            .Select(id => $"{id.Localize()}({id})")
            .ToArray() ?? Array.Empty<string>();
        if (skills.Length > 0)
        {
            sb.Append(" 常用法术：");
            sb.Append(string.Join("、", skills));
            sb.Append('。');
        }
        sb.Append(" 请给出贴合以上设定的功法名称与简介，简介需点出灵根或战斗风格。");
        return sb.ToString();
    }

    private static BaseStats GenerateStatsFromActor(Actor creator)
    {
        var stats = new BaseStats();

        void SoftmaxStats(IList<string> statIds)
        {
            (string id, float value)[] tempStats = new (string, float)[statIds.Count];
            var expSum = 0f;
            var maxVal = 0f;
            for (int i = 0; i < tempStats.Length; i++)
            {
                var statId = statIds[i];
                var statValue = creator.stats[statId];
                maxVal = Mathf.Max(maxVal, statValue);
                tempStats[i] = (statId, statValue);
            }

            for (int i = 0; i < tempStats.Length; i++)
            {
                var statValue = tempStats[i].value;
                var expValue = Mathf.Exp(statValue - maxVal);
                tempStats[i].value = expValue;
                expSum += expValue;
            }

            for (int i = 0; i < tempStats.Length; i++)
            {
                tempStats[i].value /= expSum;
            }

            Array.Sort(tempStats, (a, b) => b.value.CompareTo(a.value));
            var accum = 0f;
            var stopIndex = tempStats.Length - 1;
            for (int i = 0; i < tempStats.Length; i++)
            {
                accum += tempStats[i].value;
                if (accum > 0.9f)
                {
                    stopIndex = i;
                    break;
                }
            }

            for (int i = 0; i <= stopIndex; i++)
            {
                var statId = tempStats[i].id;
                var statValue = tempStats[i].value / accum;
                stats[WorldboxGame.BaseStats.StatsToModStats[statId]] = statValue;
            }
        }

        SoftmaxStats(WorldboxGame.BaseStats.ArmorStats);
        SoftmaxStats(WorldboxGame.BaseStats.MasterStats);

        return stats;
    }
    private static ElementRequirement GenerateElementRequirement(ActorExtend ae)
    {
        ElementRequirement elementReq = new();
        if (!ae.HasElementRoot()) return elementReq;

        var elementRoot = ae.GetElementRoot();
        elementReq.MinIron = Mathf.Max(0f, elementRoot.Iron * 0.7f);
        elementReq.MinWood = Mathf.Max(0f, elementRoot.Wood * 0.7f);
        elementReq.MinWater = Mathf.Max(0f, elementRoot.Water * 0.7f);
        elementReq.MinFire = Mathf.Max(0f, elementRoot.Fire * 0.7f);
        elementReq.MinEarth = Mathf.Max(0f, elementRoot.Earth * 0.7f);
        elementReq.MinNeg = Mathf.Max(0f, elementRoot.Neg * 0.7f);
        elementReq.MinPos = Mathf.Max(0f, elementRoot.Pos * 0.7f);
        elementReq.MinEntropy = Mathf.Max(0f, elementRoot.Entropy * 0.7f);
        return elementReq;
    }

    private static void GenerateLevelRange(ActorExtend ae, out int minLevel, out int maxLevel)
    {
        minLevel = 0;
        maxLevel = 20;
        if (!ae.HasCultisys<Xian>()) return;

        ref var xian = ref ae.GetCultisys<Xian>();
        minLevel = Mathf.Max(0, xian.CurrLevel - 2);
        maxLevel = Mathf.Min(20, xian.CurrLevel + 5);
    }
    /// <summary>
    /// 根据功法信息计算品阶
    /// </summary>
    private static ItemLevel CalculateCultibookLevel(
        BaseStats finalStats, 
        List<SkillPoolEntry> skillPool, 
        int minLevel, 
        int maxLevel, 
        string cultivateMethodId,
        ActorExtend creator)
    {
        // 计算属性总值（所有属性值的绝对值之和）
        float statsValue = 0f;
        if (finalStats != null && finalStats._stats_list is IList<BaseStatsContainer> statsList)
        {
            foreach (var container in statsList)
            {
                statsValue += Mathf.Abs(container.value);
            }
        }
        
        // 法术池数量
        int skillCount = skillPool?.Count ?? 0;
        
        // 创建者境界
        int creatorLevel = 0;
        if (creator.HasCultisys<Xian>())
        {
            creatorLevel = creator.GetCultisys<Xian>().CurrLevel;
        }
        
        // 修炼方式特殊性（特殊修炼方式提升品阶）
        // 判断是否为标准修炼方式（Cultiway.Standard 或 Standard）
        bool isSpecialMethod = !string.IsNullOrEmpty(cultivateMethodId) && 
                               cultivateMethodId != CultivateMethods.Standard.id;
        
        // 境界范围（范围越广，品阶可能越高）
        int levelRange = maxLevel - minLevel;
        
        // 综合评分系统
        float score = 0f;
        
        // 1. 属性强度评分（0-40分）
        // 属性总值每1.0分对应约10分，最高40分
        score += Mathf.Min(statsValue * 10f, 40f);
        
        // 2. 法术池数量评分（0-20分）
        // 每个法术5分，最高20分
        score += Mathf.Min(skillCount * 5f, 20f);
        
        // 3. 创建者境界评分（0-20分）
        // 境界越高，能创造的功法品阶越高
        // 筑基期(1) = 0分，金丹期(2) = 5分，元婴期(3) = 10分，更高境界 = 20分
        if (creatorLevel >= 6) score += 20f;
        else if (creatorLevel >= 3) score += 10f + (creatorLevel - 3) * 3.33f;
        else if (creatorLevel >= 2) score += 5f;
        
        // 4. 特殊修炼方式奖励（0-10分）
        if (isSpecialMethod) score += 10f;
        
        // 5. 境界范围奖励（0-10分）
        // 范围越广，说明功法适用性越高
        if (levelRange >= 15) score += 10f;
        else if (levelRange >= 10) score += 5f;
        else if (levelRange >= 5) score += 2f;
        
        // 根据总评分确定品阶
        // 人级: 0-30分
        // 地级: 30-60分
        // 天级: 60-85分
        // 仙级: 85分以上
        
        int stage = 0;
        int level = 0;
        
        if (score >= 85f)
        {
            // 仙级 (Stage 3)
            stage = 3;
            // 在85-100分之间，level 0-4；100分以上，level 5-8
            level = score >= 100f ? Mathf.Clamp(Mathf.RoundToInt((score - 100f) / 15f * 3f) + 5, 5, 8) 
                                   : Mathf.Clamp(Mathf.RoundToInt((score - 85f) / 15f * 4f), 0, 4);
        }
        else if (score >= 60f)
        {
            // 天级 (Stage 2)
            stage = 2;
            // 在60-85分之间，level 0-8
            level = Mathf.Clamp(Mathf.RoundToInt((score - 60f) / 25f * 8f), 0, 8);
        }
        else if (score >= 30f)
        {
            // 地级 (Stage 1)
            stage = 1;
            // 在30-60分之间，level 0-8
            level = Mathf.Clamp(Mathf.RoundToInt((score - 30f) / 30f * 8f), 0, 8);
        }
        else
        {
            // 人级 (Stage 0)
            stage = 0;
            // 在0-30分之间，level 0-8
            level = Mathf.Clamp(Mathf.RoundToInt(score / 30f * 8f), 0, 8);
        }
        
        return new ItemLevel
        {
            Stage = stage,
            Level = level
        };
    }

    private static string DetermineCultivateMethodId(ActorExtend ae)
    {
        var mainCultibook = ae.GetMainCultibook();
        if (mainCultibook != null && !string.IsNullOrEmpty(mainCultibook.CultivateMethodId))
        {
            return mainCultibook.CultivateMethodId;
        }

        var library = Libraries.Manager.CultivateMethodLibrary;
        var candidateMethods = new List<CultivateMethodAsset>();
        var accumWeights = new List<float>();
        float totalWeight = 0f;

        foreach (var method in library.list)
        {
            if (method == null) continue;
            if (method.CanCultivate != null && !method.CanCultivate(ae)) continue;

            float efficiency = 1f;
            if (method.GetEfficiency != null)
            {
                efficiency = method.GetEfficiency(ae);
            }
            if (efficiency <= 0f) continue;

            candidateMethods.Add(method);
            totalWeight += efficiency;
            accumWeights.Add(totalWeight);
        }

        if (candidateMethods.Count == 0)
        {
            return CultivateMethods.Standard.id;
        }

        var selectedIndex = RdUtils.RandomIndexWithAccumWeight(accumWeights);
        return candidateMethods[selectedIndex].id;
    }

    private static List<SkillPoolEntry> GenerateSkillPool(ActorExtend ae, int minLevel)
    {
        var result = new List<SkillPoolEntry>();
        if (ae.all_skills == null || ae.all_skills.Count == 0) return result;

        var skillsToAdd = Mathf.Min(3, ae.all_skills.Count);
        var skillList = new List<Entity>(ae.all_skills);
        skillList.Shuffle();

        int addedCount = 0;
        for (int i = 0; i < skillList.Count && addedCount < skillsToAdd; i++)
        {
            var skillEntity = skillList[i];
            if (!skillEntity.HasComponent<SkillContainer>()) continue;

            ref var skillContainer = ref skillEntity.GetComponent<SkillContainer>();
            if (string.IsNullOrEmpty(skillContainer.SkillEntityAssetID)) continue;

            result.Add(new SkillPoolEntry
            {
                SkillEntityAssetId = skillContainer.SkillEntityAssetID,
                BaseChance = 0.05f + addedCount * 0.02f,
                MasteryThreshold = 20f + addedCount * 20f,
                LevelRequirement = minLevel + addedCount * 2
            });
            addedCount++;
        }

        return result;
    }
}
