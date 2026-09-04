using System;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>
///     消化完成的结算器：把精华结算为妖力、短时适应或器官候选，
///     并在候选就绪后自动执行一次炼化。
/// </summary>
public static class YaoOrganCandidateService
{
    /// <summary>结算一份消化完成的精华；按妖兽状态自行选择结果，不提供手动菜单。</summary>
    public static void ResolveCompleted(
        ActorExtend actor, ref YaoDigestion digestion, ref Yao yao, YaoDigestionEntry entry)
    {
        digestion.EnsureInitialized();
        MarkEntryResolved(ref digestion, entry);

        // 结算优先级：正在凝丹准备时优先妖力；饥饿与低稳定度时优先恢复；其余生成器官候选。
        float healthRatio = actor.Base.getHealthRatio();
        if (!string.IsNullOrEmpty(yao.CorePreparationPatternId) || healthRatio < 0.4f)
        {
            YaoResourceService.Gain(actor, ref yao, 8f * entry.Strength);
            return;
        }

        if (!YaoDigestionService.TryGetFragmentOrgan(entry.FragmentId, out string organId, out int rank)) return;

        // 身体稳定度过低时直接炼化会被拒绝，这里同样不生成候选。
        if (yao.BodyStability < YaoSetting.BodyStabilityLowThreshold)
        {
            YaoResourceService.Gain(actor, ref yao, 4f * entry.Strength);
            return;
        }

        AddCandidate(ref digestion, organId, rank);
        TryRefineBestCandidate(actor, ref digestion, ref yao);
    }

    /// <summary>登记一个器官候选；候选满员时淘汰评分最低的一个。</summary>
    private static void AddCandidate(ref YaoDigestion digestion, string organId, int rank)
    {
        var candidate = new YaoOrganCandidate
        {
            OrganId = organId,
            Rank = rank,
            SlotId = ResolveSlot(organId),
            Score = 1f,
            Reason = "yao.digestion.match",
        };
        int weakest = -1;
        float weakestScore = float.MaxValue;
        for (int i = 0; i < YaoDigestion.CandidateSize; i++)
        {
            if (digestion.Candidates[i].OrganId == null || digestion.Candidates[i].Used)
            {
                digestion.Candidates[i] = candidate;
                return;
            }

            if (digestion.Candidates[i].Score < weakestScore)
            {
                weakestScore = digestion.Candidates[i].Score;
                weakest = i;
            }
        }

        if (weakest >= 0) digestion.Candidates[weakest] = candidate;
    }

    /// <summary>
    ///     执行一次炼化：直接替换目标槽位器官。
    ///     成功后器官写入身体总表；失败进入有限失败记忆并付出稳定度与妖力代价。
    /// </summary>
    public static void TryRefineBestCandidate(ActorExtend actor, ref YaoDigestion digestion, ref Yao yao)
    {
        digestion.EnsureInitialized();
        int best = FindBestCandidate(ref digestion);
        if (best < 0) return;

        YaoOrganCandidate candidate = digestion.Candidates[best];
        ref Yao yaoRef = ref actor.GetCultisys<Yao>();

        // 失败判定：变异承受力与身体稳定度共同决定成功率，失败历史持续压制。
        float memoryPenalty = CountFailureMemory(ref digestion, candidate.OrganId) * 0.15f;
        float successChance = Mathf.Clamp01(
            0.55f + yaoRef.MutationTolerance * 0.4f + yaoRef.BodyStability / 400f - memoryPenalty);

        if (Randy.randomFloat(0f, 1f) <= successChance)
        {
            bool refined = YaoFormPlanService.TryReplaceOrgan(
                actor, YaoFormIds.TrueForm, candidate.SlotId, candidate.OrganId, candidate.Rank,
                YaoOrganOrigin.Digested);
            if (refined)
            {
                candidate.Used = true;
                digestion.Candidates[best] = candidate;
                yaoRef.BodyStability = Mathf.Max(0f, yaoRef.BodyStability - 5f);
                actor.GetCultisys<Yao>() = yaoRef;
                YaoWorldLog.OrganDigested(actor, candidate.OrganId);
                return;
            }
        }

        // 失败：样本损失、妖力下降与有限失败记忆。
        RecordFailure(ref digestion, candidate.OrganId);
        YaoResourceService.Spend(actor, ref yaoRef, 5f);
        yaoRef.BodyStability = Mathf.Max(0f, yaoRef.BodyStability - 8f);
        actor.GetCultisys<Yao>() = yaoRef;
        YaoWorldLog.OrganRejected(actor, candidate.OrganId);
    }

    /// <summary>把条目标记为已结算，释放队列格位。</summary>
    private static void MarkEntryResolved(ref YaoDigestion digestion, YaoDigestionEntry entry)
    {
        for (int i = 0; i < YaoDigestion.QueueSize; i++)
        {
            if (digestion.Queue[i].SourceDeathSequence != entry.SourceDeathSequence) continue;
            if (digestion.Queue[i].FragmentId != entry.FragmentId) continue;
            entry.Phase = YaoDigestionPhase.Resolved;
            digestion.Queue[i] = entry;
            return;
        }
    }

    private static int FindBestCandidate(ref YaoDigestion digestion)
    {
        int best = -1;
        float bestScore = float.MinValue;
        for (int i = 0; i < YaoDigestion.CandidateSize; i++)
        {
            YaoOrganCandidate candidate = digestion.Candidates[i];
            if (candidate.OrganId == null || candidate.Used) continue;
            if (candidate.Score <= bestScore) continue;
            bestScore = candidate.Score;
            best = i;
        }

        return best;
    }

    private static int CountFailureMemory(ref YaoDigestion digestion, string organId)
    {
        int count = 0;
        float now = YaoTime.Now;
        for (int i = 0; i < YaoDigestion.MemorySize; i++)
        {
            YaoFailureMemory memory = digestion.Memories[i];
            if (memory.IsEmpty || now > memory.Until) continue;
            if (memory.OrganId == organId) count++;
        }

        return count;
    }

    private static void RecordFailure(ref YaoDigestion digestion, string organId)
    {
        float now = YaoTime.Now;
        for (int i = 0; i < YaoDigestion.MemorySize; i++)
        {
            if (!digestion.Memories[i].IsEmpty && now <= digestion.Memories[i].Until) continue;
            digestion.Memories[i] = new YaoFailureMemory
            {
                OrganId = organId,
                FailureKind = "yao.rejection.standard",
                Until = now + 120f,
            };
            return;
        }
    }

    /// <summary>按器官的槽位要求推断替换位置。</summary>
    private static string ResolveSlot(string organId)
    {
        CreatureCompositions.Libraries.CreatureOrganAsset asset =
            Content.Libraries.Manager.CreatureOrganLibrary.get(organId);
        if (asset?.SlotRequirements is { Length: > 0 }) return asset.SlotRequirements[0].SlotId;

        // 器官没有显式槽位要求时按主类别匹配第一个可用槽位。
        foreach (var pair in Content.Libraries.Manager.CreatureBodySlotLibrary.dict)
        {
            if ((pair.Value.AcceptedCategoryMask & asset.Category) == asset.Category)
                return pair.Key;
        }

        return null;
    }
}
