using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 让魔法师按智力、元素亲和与法术难度持续研究一个魔网条目。
/// </summary>
public sealed class BehStudyMagicWeb : BehCityActor
{
    private const float TickInterval = 1f;

    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var actor = pObject.GetExtend();
        if (!actor.HasCultisys<Magic>()) return BehResult.Continue;
        ref var magic = ref actor.GetCultisys<Magic>();
        ref var state = ref actor.GetOrAddComponent<MagicStudyState>();
        var now = World.world.map_stats.world_time;
        if (now < state.NextStudyWorldTime) return BehResult.Continue;

        if (!MagicLearningRules.TryResolveStudy(actor, state, out _, out var affinity, out var difficulty))
        {
            MagicLearningRules.ClearCandidate(ref state);
            if (!MagicLearningRules.TrySelectStudyCandidate(actor, out var candidate))
            {
                state.NextStudyWorldTime = now +
                                           MagicSetting.MagicStudyNoCandidateBackoffYears * TimeScales.SecPerYear;
                return BehResult.Continue;
            }

            state.Candidate = candidate.Container;
            state.Replacement = candidate.Replacement;
            affinity = candidate.Affinity;
            difficulty = candidate.Difficulty;
        }

        if (state.SessionRemaining <= 0f)
        {
            state.SessionRemaining = (magic.CurrLevel + 1) *
                                     Randy.randomFloat(MagicSetting.MeditateSessionMinMonths,
                                         MagicSetting.MeditateSessionMaxMonths) * TimeScales.SecPerMonth;
        }

        var intelligence = Mathf.Max(0f, pObject.stats[S.intelligence]);
        state.Progress += intelligence * affinity * TickInterval / TimeScales.SecPerYear;
        state.SessionRemaining -= TickInterval;
        if (state.Progress >= difficulty)
        {
            var learned = MagicLearningRules.CompleteStudy(actor, ref state);
            MagicLearningRules.ClearCandidate(ref state);
            state.NextStudyWorldTime = now + (learned
                ? MagicSetting.MagicStudySuccessCooldownYears
                : MagicSetting.MagicStudyNoCandidateBackoffYears) * TimeScales.SecPerYear;
            return BehResult.Continue;
        }

        if (state.SessionRemaining <= 0f)
        {
            state.SessionRemaining = 0f;
            state.NextStudyWorldTime = now + MagicSetting.MagicStudyRetryYears * TimeScales.SecPerYear;
            return BehResult.Continue;
        }

        pObject.timer_action = TickInterval;
        return BehResult.RepeatStep;
    }
}
