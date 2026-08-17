using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.EventSystem;
using Cultiway.Utils.Extension;
using Newtonsoft.Json;

namespace Cultiway.Content.AIGC;

/// <summary>只处理不可变 Prompt 快照与纯 DTO，不在异步线程访问世界或 ECS。</summary>
public sealed class CultibookGenerator
{
    public static CultibookGenerator Instance { get; } = new();

    private const float DefaultResponseSeconds = 0.01f;

    private sealed class SkillPoolEntryDto
    {
        public int entityId { get; set; }
        public float baseChance { get; set; }
        public float masteryThreshold { get; set; }
        public int levelRequirement { get; set; }
    }

    private sealed class LlmResponse
    {
        public string name { get; set; }
        public string description { get; set; }
        public ElementComposition elementReq { get; set; }
        public float elementAffinityThreshold { get; set; }
        public int minLevel { get; set; }
        public int maxLevel { get; set; }
        public string cultivateMethodId { get; set; }
        public List<SkillPoolEntryDto> skillPool { get; set; }
    }

    internal void RequestGeneration(CultibookPromptSnapshot snapshot, string requestId,
        long actorId, long orderId, long worldSessionId, CancellationToken cancellationToken)
    {
        _ = GenerateAsync(snapshot, requestId, actorId, orderId, worldSessionId, cancellationToken);
    }

    internal void RequestImprovement(CultibookPromptSnapshot snapshot, string requestId,
        long actorId, long orderId, long worldSessionId, CancellationToken cancellationToken)
    {
        _ = ImproveAsync(snapshot, requestId, actorId, orderId, worldSessionId, cancellationToken);
    }

    private static async Task GenerateAsync(CultibookPromptSnapshot snapshot, string requestId,
        long actorId, long orderId, long worldSessionId, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        CultibookDraftDto draft = null;
        string error = string.Empty;
        try
        {
            draft = await RequestDraftAsync(snapshot, false, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            ModClass.LogErrorConcurrent(exception.ToString());
        }
        finally
        {
            stopwatch.Stop();
        }

        if (cancellationToken.IsCancellationRequested) return;
        EventSystemHub.Publish(new CultibookGeneratedEvent
        {
            WorldSessionId = worldSessionId,
            ActorId = actorId,
            OrderId = orderId,
            RequestId = requestId,
            Draft = draft,
            UsedFallback = draft == null,
            GeneratorError = error,
            ResponseSeconds = ResponseSeconds(stopwatch),
        });
    }

    private static async Task ImproveAsync(CultibookPromptSnapshot snapshot, string requestId,
        long actorId, long orderId, long worldSessionId, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        CultibookDraftDto draft = null;
        string error = string.Empty;
        try
        {
            draft = await RequestDraftAsync(snapshot, true, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            ModClass.LogErrorConcurrent(exception.ToString());
        }
        finally
        {
            stopwatch.Stop();
        }

        if (cancellationToken.IsCancellationRequested) return;
        EventSystemHub.Publish(new CultibookImprovedEvent
        {
            WorldSessionId = worldSessionId,
            ActorId = actorId,
            OrderId = orderId,
            RequestId = requestId,
            OriginalCultibookId = snapshot.Original?.Id,
            ImprovedDraft = draft,
            UsedFallback = draft == null,
            GeneratorError = error,
            ResponseSeconds = ResponseSeconds(stopwatch),
        });
    }

    private static async Task<CultibookDraftDto> RequestDraftAsync(
        CultibookPromptSnapshot snapshot, bool improve, CancellationToken cancellationToken)
    {
        string prompt = improve ? BuildImprovementPrompt(snapshot) : BuildCreationPrompt(snapshot);
        string systemPrompt = improve
            ? GetImprovementSystemPrompt(snapshot.AllowedCultivateMethods)
            : GetCreationSystemPrompt(snapshot.AllowedCultivateMethods);
        string response = await Core.AIGCLib.Manager.RequestResponseContent(
            prompt, systemPrompt, temperature: 0.7f, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(response)) return null;

        LlmResponse dto = JsonConvert.DeserializeObject<LlmResponse>(response.PostProcessForJSON());
        if (dto == null) return null;
        return new CultibookDraftDto
        {
            Name = dto.name,
            Description = dto.description,
            ElementRequirement = ElementRequirement.FromComposition(dto.elementReq),
            ElementAffinityThreshold = dto.elementAffinityThreshold,
            MinLevel = dto.minLevel,
            MaxLevel = dto.maxLevel,
            CultivateMethodId = dto.cultivateMethodId,
            SkillPool = (dto.skillPool ?? new List<SkillPoolEntryDto>())
                .Where(entry => entry != null && entry.entityId > 0)
                .Select(entry => new CultibookSkillDraftDto
                {
                    EntityId = entry.entityId,
                    BaseChance = entry.baseChance,
                    MasteryThreshold = entry.masteryThreshold,
                    LevelRequirement = entry.levelRequirement,
                })
                .ToList(),
        };
    }

    private static string BuildCreationPrompt(CultibookPromptSnapshot snapshot)
    {
        var prompt = new StringBuilder();
        AppendActor(snapshot, prompt);
        AppendSkills(snapshot, prompt);
        return prompt.ToString();
    }

    private static string BuildImprovementPrompt(CultibookPromptSnapshot snapshot)
    {
        CultibookOriginalPromptDto original = snapshot.Original;
        var prompt = new StringBuilder();
        if (original != null)
        {
            ElementRequirement req = original.ElementRequirement;
            prompt.Append("原功法信息：");
            prompt.Append($"名称 {original.Name}，简介 {original.Description}，境界范围 {original.MinLevel}-{original.MaxLevel}，\n");
            prompt.Append($"灵根需求 金{req.MinIron}木{req.MinWood}水{req.MinWater}火{req.MinFire}土{req.MinEarth}阴{req.MinNeg}阳{req.MinPos}混沌{req.MinEntropy}，\n");
            prompt.Append($"灵根契合度阈值 {original.ElementAffinityThreshold}，修炼方式 {original.CultivateMethodId}。");
            if (!string.IsNullOrEmpty(original.SkillPoolDescription))
                prompt.Append($"法术池 {original.SkillPoolDescription}。\n");
        }
        prompt.Append("改进者信息：");
        AppendActor(snapshot, prompt);
        AppendSkills(snapshot, prompt);
        return prompt.ToString();
    }

    private static void AppendActor(CultibookPromptSnapshot snapshot, StringBuilder prompt)
    {
        prompt.Append($"姓名 {snapshot.ActorName}，境界 {snapshot.ActorLevelName}({snapshot.ActorLevel})，");
        prompt.Append($"灵根 {snapshot.ElementName}({snapshot.ElementDescription})，");
        prompt.Append($"修炼方式 {snapshot.CultivateMethodName}({snapshot.CultivateMethodId})。");
    }

    private static void AppendSkills(CultibookPromptSnapshot snapshot, StringBuilder prompt)
    {
        if (snapshot.Skills == null || snapshot.Skills.Count == 0) return;
        prompt.Append(" 候选法术：");
        prompt.Append(string.Join("、", snapshot.Skills.Select(skill => $"{skill.Name}({skill.EntityId})")));
        prompt.Append('。');
    }

    private static string GetCreationSystemPrompt(string methods)
    {
        return "请根据修仙者背景生成功法名称与简介，只输出 JSON，例如 {\"name\":\"玄火九转功\",\"description\":\"简介不超过60字\",\"elementReq\":{\"iron\":0.2,\"wood\":0.3,\"water\":0.0,\"fire\":1.5,\"earth\":0.1,\"neg\":0.1,\"pos\":0.8,\"entropy\":0.5},\"elementAffinityThreshold\":0.3,\"minLevel\":1,\"maxLevel\":4,\"cultivateMethodId\":\"Cultiway.Standard\",\"skillPool\":[{\"entityId\":12345,\"baseChance\":0.05,\"masteryThreshold\":20,\"levelRequirement\":1}]}。entityId 只能从 prompt 的候选法术中选择，不要输出其他内容。可选修炼方式：" + methods + "。";
    }

    private static string GetImprovementSystemPrompt(string methods)
    {
        return "请根据原功法和改进者信息生成改进版功法，只输出与新功法相同结构的 JSON。名称与简介应说明改进之处，entityId 只能从 prompt 的候选法术中选择，不要输出其他内容。可选修炼方式：" + methods + "。";
    }

    private static float ResponseSeconds(Stopwatch stopwatch)
    {
        float seconds = (float)stopwatch.Elapsed.TotalSeconds;
        return seconds > 0f ? seconds : DefaultResponseSeconds;
    }
}
