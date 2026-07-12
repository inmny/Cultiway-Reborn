using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Blueprints;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 负责将法术的实际使用积累转化为个人改进，并把改进结果上传魔网。
/// </summary>
public static class MagicSpellProgressionService
{
    /// <summary>
    /// 记录一次由施法者自身支付资源并完成的施法序列；齐射、连射和散射产生的多发弹体仍只计作一次使用。
    /// 卷轴、符箓等预付载体不代表施法者掌握或练习了该法术，因此不进入个人使用积累。
    /// </summary>
    public static void RecordCompletedCast(ActorExtend actor, Entity castContainer, int emittedCount,
        SkillCastFundingSource fundingSource)
    {
        if (fundingSource == SkillCastFundingSource.Prepaid) return;

        // 只处理活着的魔法师实际完成的施法序列；空序列和非 mana 技能不产生使用积累。
        if (actor == null || actor.Base.isRekt() || emittedCount <= 0 || !actor.HasCultisys<Magic>()) return;
        if (!SkillCastResourceResolver.UsesResource(castContainer, SkillCastResources.Mana)) return;

        // 先同步技能所有权和知识关系。若旧版本刚被替换，则按法术族找到施法者当前掌握的版本。
        MagicKnowledgeService.Synchronize(actor);
        var trackedContainer = ResolveTrackedContainer(actor, castContainer);
        if (trackedContainer.IsNull || !MagicKnowledgeService.Ensure(actor, trackedContainer)) return;

        // 档案提供环位和法术族信息；无有效档案的容器不能进入改进流程。
        var profile = MagicSpellProfile.Resolve(trackedContainer);
        if (profile == null) return;

        // 每个完成的施法序列固定增加一点进度，多发弹体只通过 emittedCount 判断施法是否有效。
        var now = GetWorldTime();
        ref var knowledge = ref actor.E.GetRelation<MagicSpellKnowledgeRelation, Entity>(trackedContainer);
        knowledge.TotalCastCount++;
        knowledge.ImprovementProgress += 1f;
        knowledge.LastUsedWorldTime = now;

        // 未到重试时间或使用积累不足时，仅保存本次使用记录，不生成临时候选。
        if (now < knowledge.NextImprovementAttemptWorldTime) return;
        var threshold = ResolveImprovementThreshold(actor, profile, knowledge.ImprovementCount);
        if (knowledge.ImprovementProgress < threshold) return;

        // 改进会替换知识关系，因此先复制旧状态；失败时保留全部进度并设置退避时间。
        var snapshot = knowledge;
        if (TryImproveAndPublish(actor, trackedContainer, profile, snapshot, threshold, now)) return;

        ref var current = ref actor.E.GetRelation<MagicSpellKnowledgeRelation, Entity>(trackedContainer);
        current.NextImprovementAttemptWorldTime = now +
                                                  MagicSetting.MagicSpellImprovementRetryYears *
                                                  TimeScales.SecPerYear;
    }

    /// <summary>
    /// 根据法术环位、既有改进次数和施法者智力计算下一次改进所需的使用积累。
    /// </summary>
    public static float ResolveImprovementThreshold(ActorExtend actor, MagicSpellProfile profile,
        int improvementCount)
    {
        var ringFactor = 1f + Mathf.Max(0, profile?.Ring ?? 0) *
            MagicSetting.MagicSpellImprovementRingUseFactor;
        var improvementFactor = 1f + Mathf.Max(0, improvementCount) *
            MagicSetting.MagicSpellImprovementCountUseFactor;
        var intelligence = actor == null ? 0f : Mathf.Max(0f, actor.GetStat(S.intelligence));
        var intelligenceFactor = Mathf.Sqrt(1f + intelligence /
            MagicSetting.MagicSpellImprovementIntelligenceScale);
        return Mathf.Max(1f, MagicSetting.MagicSpellImprovementBaseUses * ringFactor * improvementFactor /
                             intelligenceFactor);
    }

    /// <summary>
    /// 生成一个合法改进版本、上传魔网取得规范容器，并替换施法者当前掌握的源版本。
    /// </summary>
    private static bool TryImproveAndPublish(ActorExtend actor, Entity source, MagicSpellProfile sourceProfile,
        MagicSpellKnowledgeRelation knowledge, float threshold, double now)
    {
        // 当前魔法等级决定候选允许达到的最高环位，避免改进出施法者无法理解的版本。
        ref var magic = ref actor.GetCultisys<Magic>();
        var candidate = MagicSpellImprovementPlanner.SelectCandidate(source, sourceProfile,
            Cultisyses.GetMaxSpellRing(magic.CurrLevel));
        if (candidate.IsNull) return false;

        // 若施法者已经掌握结构完全相同的其他技能，则该候选不能替换源技能。
        var candidateSignature = SkillContainerSignature.Build(candidate);
        if (HasOwnedDuplicate(actor, source, candidateSignature))
        {
            SkillBlueprintCompiler.Recycle(candidate);
            return false;
        }

        // 先交给魔网进行全世界结构去重；新增时候选成为规范容器，重复时改用已有规范容器。
        var manager = MagicWebManager.Instance;
        var publish = manager?.TryPublish(candidate) ??
                      new MagicWebPublishResult(MagicWebPublishStatus.Unavailable);
        if (!publish.Success)
        {
            SkillBlueprintCompiler.Recycle(candidate);
            return false;
        }

        var canonical = publish.Container;
        if (publish.Status == MagicWebPublishStatus.Duplicate)
        {
            // 重复候选未被魔网接管，后续替换直接使用魔网返回的规范容器。
            SkillBlueprintCompiler.Recycle(candidate);
        }

        // 上传成功后原子替换施法者的 SkillMasterRelation；失败时不修改旧知识关系。
        var replace = SkillOwnershipService.Replace(actor, source, canonical);
        if (replace != SkillOwnershipResult.Replaced) return false;

        // 将累计使用、剩余进度和改进代数迁移到新版本，并把来源标记为自行改进。
        actor.E.RemoveRelation<MagicSpellKnowledgeRelation>(source);
        actor.E.AddRelation(new MagicSpellKnowledgeRelation
        {
            SkillContainer = canonical,
            LearnedWorldTime = now,
            Source = MagicSpellKnowledgeSource.SelfCreated,
            TotalCastCount = knowledge.TotalCastCount,
            ImprovementProgress = Mathf.Max(0f, knowledge.ImprovementProgress - threshold),
            ImprovementCount = knowledge.ImprovementCount + 1,
            LastUsedWorldTime = knowledge.LastUsedWorldTime,
            LastImprovedWorldTime = now,
            NextImprovementAttemptWorldTime = now +
                                              MagicSetting.MagicSpellImprovementRetryYears *
                                              TimeScales.SecPerYear
        });
        // 本次上传和采用都属于明确访问，刷新动态魔网条目的存活时间。
        manager.Touch(canonical);
        ModClass.LogInfo($"[{actor}] 改进并上传法术: {source.Id} -> {canonical.Id} ({publish.Status})");
        return true;
    }

    /// <summary>
    /// 判断施法者除待替换源技能外，是否已经掌握结构签名相同的技能。
    /// </summary>
    private static bool HasOwnedDuplicate(ActorExtend actor, Entity source, string signature)
    {
        foreach (var owned in actor.GetLearnedSkillsInOrder())
        {
            if (owned.IsNull || owned == source) continue;
            if (SkillContainerSignature.Build(owned) == signature) return true;
        }
        return false;
    }

    /// <summary>
    /// 取得本次使用应累计到的当前技能版本；旧版本刚被替换时按法术族映射到新版本。
    /// </summary>
    private static Entity ResolveTrackedContainer(ActorExtend actor, Entity castContainer)
    {
        if (actor.OwnsLearnedSkill(castContainer)) return castContainer;
        var castProfile = MagicSpellProfile.Resolve(castContainer);
        if (castProfile == null) return default;

        foreach (var relation in actor.E.GetRelations<MagicSpellKnowledgeRelation>())
        {
            var known = relation.SkillContainer;
            if (known.IsNull || !actor.OwnsLearnedSkill(known)) continue;
            var knownProfile = MagicSpellProfile.Resolve(known);
            if (knownProfile?.FamilySignature == castProfile.FamilySignature) return known;
        }
        return default;
    }

    /// <summary>
    /// 返回当前世界时间；尚未进入世界时返回零。
    /// </summary>
    private static double GetWorldTime()
    {
        return World.world?.map_stats?.world_time ?? 0d;
    }
}
