using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>新构造进入已有功法编写、改进和活动领悟，引用确定版本而不复制运行时对象。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>编写和改进共用，保留已有传承，再从实际已学中补充，最多八项。</summary>
    /// <param name="draft">实际功法草稿。</param>
    /// <param name="creator">真正编写或改进者。</param>
    /// <param name="original">改进所依据的原功法。</param>
    public static void ComposeInheritance(CultibookAsset draft, ActorExtend creator, CultibookAsset original = null)
    {
        var entries = new List<ConstructionInheritance>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (ConstructionInheritance entry in (draft.ConstructionPool ?? new()).Concat(original?.ConstructionPool ??
                     new()))
            if (entry != null && Contains(entry.AbilityId) && seen.Add(entry.AbilityId) && entries.Count < 8)
                entries.Add(new ConstructionInheritance
                {
                    AbilityId = entry.AbilityId, BaseChance = entry.BaseChance,
                    MasteryThreshold = entry.MasteryThreshold, LevelRequirement = entry.LevelRequirement
                });
        if (creator.E.TryGetComponent(out ConstructedKnowledge known))
            foreach (string id in known.Ids)
                if (entries.Count < 8 && Contains(id) && seen.Add(id))
                    entries.Add(new ConstructionInheritance
                        { AbilityId = id, LevelRequirement = draft.MinLevel });
        draft.ConstructionPool = entries;
    }

    /// <summary>使用同一主修掌握、智力和境界条件，失败积累进度，成功后只授予确定版本。</summary>
    /// <param name="actor">实际领悟者。</param>
    /// <param name="book">实际主修功法。</param>
    /// <param name="state">既有主修状态及新增稳定进度。</param>
    internal static bool TryComprehend(ActorExtend actor, CultibookAsset book, ref ActorCultibookState state)
    {
        if (book.ConstructionPool == null || book.ConstructionPool.Count == 0) return false;
        var eligible = new HashSet<string>(book.ConstructionPool
            .Where(entry => entry != null && entry.AbilityId != null)
            .Select(entry => entry.AbilityId), StringComparer.Ordinal);
        foreach (string id in state.ConstructionProgress.Keys.Where(id => !eligible.Contains(id) || Knows(actor, id))
                     .ToArray())
            state.ConstructionProgress.Remove(id);
        foreach (ConstructionInheritance entry in book.ConstructionPool)
        {
            if (entry == null || !CanLearn(actor, entry.AbilityId) || Knows(actor, entry.AbilityId) ||
                state.MainMastery < entry.MasteryThreshold || actor.HasCultisys<Xian>() &&
                actor.GetCultisys<Xian>().CurrLevel < entry.LevelRequirement) continue;
            state.ConstructionProgress.TryGetValue(entry.AbilityId, out float progress);
            float chance = entry.BaseChance * (state.MainMastery / 100f) * (actor.GetStat("intelligence") / 50f) +
                           progress * .1f;
            if (Randy.randomChance(chance))
            {
                state.ConstructionProgress.Remove(entry.AbilityId);
                Learn(actor, entry.AbilityId);
                NotifyCultivation(actor);
                return true;
            }

            state.ConstructionProgress[entry.AbilityId] = Mathf.Min(10f, progress + entry.BaseChance * .1f);
        }

        return false;
    }
}