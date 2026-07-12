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
/// 让魔法师按智力和元素亲和持续研读一份实体魔法卷轴。
/// </summary>
public sealed class BehStudyMagicScroll : BehaviourActionActor
{
    private const float TickInterval = 1f;

    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        var actor = pObject.GetExtend();
        if (!actor.HasCultisys<Magic>()) return BehResult.Continue;

        ref var magic = ref actor.GetCultisys<Magic>();
        ref var state = ref actor.GetOrAddComponent<MagicScrollStudyState>();
        if (!MagicScrollLearningRules.TryResolveStudy(actor, state, out var candidate))
        {
            MagicScrollLearningRules.ClearState(ref state);
            if (!MagicScrollLearningRules.TrySelectStudyCandidate(actor, out candidate))
                return BehResult.Continue;

            state.Scroll = candidate.Scroll;
            state.Replacement = candidate.Replacement;
        }

        if (state.SessionRemaining <= 0f)
        {
            state.SessionRemaining = (magic.CurrLevel + 1) *
                                     Randy.randomFloat(MagicSetting.MeditateSessionMinMonths,
                                         MagicSetting.MeditateSessionMaxMonths) * TimeScales.SecPerMonth;
        }

        var intelligence = Mathf.Max(0f, pObject.stats[S.intelligence]);
        state.Progress += intelligence * candidate.Affinity * TickInterval / TimeScales.SecPerYear;
        state.SessionRemaining -= TickInterval;
        if (state.Progress >= candidate.Difficulty)
        {
            MagicScrollLearningRules.CompleteStudy(actor, state);
            MagicScrollLearningRules.ClearState(ref state);
            return BehResult.Continue;
        }

        if (state.SessionRemaining <= 0f)
        {
            state.SessionRemaining = 0f;
            return BehResult.Continue;
        }

        pObject.timer_action = TickInterval;
        return BehResult.RepeatStep;
    }
}
