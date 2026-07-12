using System;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 描述一次已经通过环位、亲和、容量和版本检查的卷轴学习目标。
/// </summary>
public readonly struct MagicScrollStudyCandidate
{
    public readonly Entity Scroll;
    public readonly Entity SkillContainer;
    public readonly Entity Replacement;
    public readonly MagicSpellProfile Profile;
    public readonly float Affinity;
    public readonly float Difficulty;

    public MagicScrollStudyCandidate(Entity scroll, Entity skillContainer, Entity replacement,
        MagicSpellProfile profile, float affinity, float difficulty)
    {
        Scroll = scroll;
        SkillContainer = skillContainer;
        Replacement = replacement;
        Profile = profile;
        Affinity = affinity;
        Difficulty = difficulty;
    }
}

/// <summary>
/// 负责判定卷轴能否被指定魔法师学习，并原子更新技能所有权、知识关系和卷轴生命周期。
/// </summary>
public static class MagicScrollStudyService
{
    /// <summary>
    /// 判断魔法师当前是否持有可学习的卷轴；已有阅读目标有效时优先继续该目标。
    /// </summary>
    public static bool ShouldStudy(ActorExtend actor)
    {
        if (!CanStartStudy(actor)) return false;
        if (actor.TryGetComponent(out MagicScrollStudyState state) &&
            TryResolveStudy(actor, state, out _)) return true;

        foreach (var item in actor.GetItems())
        {
            if (TryResolveCandidate(actor, item, out _)) return true;
        }
        return false;
    }

    /// <summary>
    /// 从魔法师个人物品中随机选择一份当前可以学习的卷轴。
    /// </summary>
    public static bool TrySelectStudyCandidate(ActorExtend actor, out MagicScrollStudyCandidate selected)
    {
        selected = default;
        if (!CanStartStudy(actor)) return false;

        using var candidates = new ListPool<MagicScrollStudyCandidate>();
        foreach (var item in actor.GetItems())
        {
            if (TryResolveCandidate(actor, item, out var candidate)) candidates.Add(candidate);
        }
        if (!candidates.Any()) return false;

        selected = candidates.GetRandom();
        return true;
    }

    /// <summary>
    /// 判断一份指定卷轴能否由该魔法师学习，供城市分配和任务条件复用。
    /// </summary>
    public static bool CanLearn(ActorExtend actor, Entity scroll)
    {
        return CanStartStudy(actor) && TryResolveCandidate(actor, scroll, out _);
    }

    /// <summary>
    /// 重新校验正在阅读的卷轴，确保学习期间发生的所有权或等级变化不会产生错误结算。
    /// </summary>
    public static bool TryResolveStudy(ActorExtend actor, in MagicScrollStudyState state,
        out MagicScrollStudyCandidate candidate)
    {
        if (!OwnsScroll(actor, state.Scroll))
        {
            candidate = default;
            return false;
        }
        if (!TryResolveCandidate(actor, state.Scroll, out candidate)) return false;
        return candidate.Replacement == state.Replacement;
    }

    /// <summary>
    /// 学会卷轴中的精确法术版本。只有技能所有权和知识关系更新成功后才消耗卷轴。
    /// </summary>
    public static bool CompleteStudy(ActorExtend actor, in MagicScrollStudyState state)
    {
        if (!TryResolveStudy(actor, state, out var candidate)) return false;

        var now = GetWorldTime();
        if (candidate.Replacement.IsNull)
        {
            if (SkillOwnershipService.Learn(actor, candidate.SkillContainer) != SkillOwnershipResult.Added)
                return false;

            actor.E.AddRelation(new MagicSpellKnowledgeRelation
            {
                SkillContainer = candidate.SkillContainer,
                LearnedWorldTime = now,
                Source = MagicSpellKnowledgeSource.Scroll
            });
        }
        else
        {
            var previous = actor.E.GetRelation<MagicSpellKnowledgeRelation, Entity>(candidate.Replacement);
            if (SkillOwnershipService.Replace(actor, candidate.Replacement, candidate.SkillContainer) !=
                SkillOwnershipResult.Replaced) return false;

            actor.E.RemoveRelation<MagicSpellKnowledgeRelation>(candidate.Replacement);
            actor.E.AddRelation(new MagicSpellKnowledgeRelation
            {
                SkillContainer = candidate.SkillContainer,
                LearnedWorldTime = now,
                Source = MagicSpellKnowledgeSource.Scroll,
                TotalCastCount = previous.TotalCastCount,
                ImprovementProgress = previous.ImprovementProgress,
                ImprovementCount = previous.ImprovementCount,
                LastUsedWorldTime = previous.LastUsedWorldTime,
                LastImprovedWorldTime = previous.LastImprovedWorldTime,
                NextImprovementAttemptWorldTime = previous.NextImprovementAttemptWorldTime
            });
        }

        candidate.Scroll.DeleteEntity();
        return true;
    }

    /// <summary>
    /// 清除失效或已经完成的卷轴阅读目标及其累计进度。
    /// </summary>
    public static void ClearState(ref MagicScrollStudyState state)
    {
        state = default;
    }

    private static bool TryResolveCandidate(ActorExtend actor, Entity scroll,
        out MagicScrollStudyCandidate candidate)
    {
        candidate = default;
        if (scroll.IsNull || !scroll.HasComponent<MagicScroll>()) return false;

        var skillContainer = scroll.GetComponent<MagicScroll>().SkillContainer;
        if (!SkillCastResourceResolver.UsesResource(skillContainer, SkillCastResources.Mana)) return false;
        var profile = MagicSpellProfile.Resolve(skillContainer);
        if (profile == null || string.IsNullOrEmpty(profile.FamilySignature)) return false;

        ref var magic = ref actor.GetCultisys<Magic>();
        if (profile.Ring > Cultisyses.GetMaxSpellRing(magic.CurrLevel)) return false;

        var affinity = profile.ElementRequirement.GetWeightedAffinity(actor.GetElementRoot());
        if (affinity < MagicSetting.MagicStudyAffinityThreshold) return false;

        MagicKnowledgeService.Synchronize(actor);
        var candidateSignature = SkillContainerSignature.Build(skillContainer);
        if (string.IsNullOrEmpty(candidateSignature)) return false;
        Entity replacement = default;
        foreach (var known in actor.GetLearnedSkillsInOrder())
        {
            if (known.IsNull) continue;
            if (SkillContainerSignature.Build(known) == candidateSignature) return false;

            var knownProfile = MagicSpellProfile.Resolve(known);
            if (knownProfile?.FamilySignature != profile.FamilySignature) continue;
            if (replacement.IsNull || GetItemLevelValue(known) > GetItemLevelValue(replacement))
                replacement = known;
        }

        if (!replacement.IsNull && !IsStrictUpgrade(skillContainer, replacement)) return false;

        if (replacement.IsNull && actor.E.GetRelations<MagicSpellKnowledgeRelation>().Length >=
            Cultisyses.GetKnownSpellCapacity(magic.CurrLevel)) return false;

        var difficulty = MagicSetting.MagicStudyBaseDifficulty * Mathf.Pow(profile.Ring + 1f, 2f) *
                         MagicSetting.MagicScrollStudyDifficultyFactor;
        candidate = new MagicScrollStudyCandidate(scroll, skillContainer, replacement, profile, affinity, difficulty);
        return true;
    }

    private static bool CanStartStudy(ActorExtend actor)
    {
        return actor != null && !actor.Base.isRekt() && actor.HasCultisys<Magic>() && actor.HasElementRoot();
    }

    private static bool OwnsScroll(ActorExtend actor, Entity scroll)
    {
        if (actor == null || scroll.IsNull) return false;
        foreach (var item in actor.GetItems())
        {
            if (item == scroll) return true;
        }
        return false;
    }

    private static bool IsStrictUpgrade(Entity candidate, Entity known)
    {
        return GetItemLevelValue(candidate) > GetItemLevelValue(known);
    }

    private static int GetItemLevelValue(Entity container)
    {
        if (!container.HasComponent<ItemLevel>() && !SkillContainerEvaluator.Refresh(container)) return -1;
        return container.GetComponent<ItemLevel>();
    }

    private static double GetWorldTime()
    {
        return World.world?.map_stats?.world_time ?? 0d;
    }
}
