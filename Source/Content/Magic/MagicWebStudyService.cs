using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

/// <summary>
/// 完成魔网研习产生的技能所有权和知识关系变更。
/// </summary>
public static class MagicWebStudyService
{
    /// <summary>
    /// 完成学习或替换，并同步 SkillMasterRelation 与 MagicSpellKnowledgeRelation。
    /// </summary>
    public static bool Complete(ActorExtend actor, ref MagicStudyState state)
    {
        MagicKnowledgeService.Synchronize(actor);
        if (!MagicWebStudyPlanner.TryResolve(actor, state, out var profile, out _, out _)) return false;
        ref var magic = ref actor.GetCultisys<Magic>();
        if (profile.Ring > Cultisyses.GetMaxSpellRing(magic.CurrLevel)) return false;

        SkillOwnershipResult result;
        if (state.Replacement.IsNull)
        {
            if (actor.E.GetRelations<MagicSpellKnowledgeRelation>().Length >=
                Cultisyses.GetKnownSpellCapacity(magic.CurrLevel)) return false;
            result = MagicWebManager.Instance.Learn(actor, state.Candidate);
        }
        else
        {
            result = SkillOwnershipService.Replace(actor, state.Replacement, state.Candidate);
            if (result == SkillOwnershipResult.Replaced)
                actor.E.RemoveRelation<MagicSpellKnowledgeRelation>(state.Replacement);
        }

        if (result is not (SkillOwnershipResult.Added or SkillOwnershipResult.Replaced or
            SkillOwnershipResult.Duplicate)) return false;
        if (result == SkillOwnershipResult.Duplicate && !actor.OwnsLearnedSkill(state.Candidate)) return false;

        actor.E.AddRelation(new MagicSpellKnowledgeRelation
        {
            SkillContainer = state.Candidate,
            LearnedWorldTime = GetWorldTime(),
            Source = MagicSpellKnowledgeSource.MagicWeb
        });
        MagicWebManager.Instance.Touch(state.Candidate);
        return true;
    }

    /// <summary>
    /// 清除当前研究对象，但保留下次允许研究的世界时间。
    /// </summary>
    public static void ClearCandidate(ref MagicStudyState state)
    {
        state.Candidate = default;
        state.Replacement = default;
        state.Progress = 0f;
        state.SessionRemaining = 0f;
    }

    private static double GetWorldTime()
    {
        return World.world?.map_stats?.world_time ?? 0d;
    }
}
