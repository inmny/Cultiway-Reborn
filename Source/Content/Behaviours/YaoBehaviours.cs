using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Content.Const;
using Cultiway.Content.YaoBeasts;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

/// <summary>妖兽专用进阶行为：直接进入指定妖修体系，不重选体系。</summary>
public sealed class BehYaoProgression : BehaviourActionActor
{
    /// <summary>按当前境界推进妖修候选过渡。</summary>
    public override BehResult execute(Actor pObject)
    {
        ActorExtend extend = pObject.GetExtend();
        if (!extend.HasCultisys<Yao>()) return BehResult.Stop;

        var result = Cultisyses.Yao.TryAdvanceNaturally(extend);
        if (result.Code == ProgressionResultCode.MajorAdvanced)
        {
            pObject.changeHappiness(HappinessAssets.LevelUp.id);
        }

        return result.Code is ProgressionResultCode.PreparationStarted
            or ProgressionResultCode.ChallengeStarted
            or ProgressionResultCode.MinorAdvanced
            or ProgressionResultCode.MajorAdvanced
            ? BehResult.Continue
            : BehResult.Stop;
    }
}

/// <summary>寻找可领取的尸体精华并记录目标位置。</summary>
public sealed class BehYaoFindDeposit : BehaviourActionActor
{
    /// <summary>按距离寻找精华；找到后设置寻路目标，找不到则结束任务。</summary>
    public override BehResult execute(Actor pActor)
    {
        if (!YaoDigestionService.TryFindDeposit(
                new Vector2(pActor.current_position.x, pActor.current_position.y), 25f, out YaoDigestionService.YaoEssenceDeposit deposit))
            return BehResult.Stop;

        WorldTile tile = World.world.GetTile((int)deposit.Position.x, (int)deposit.Position.y);
        if (tile == null) return BehResult.Stop;
        pActor.beh_tile_target = tile;
        return BehResult.Continue;
    }
}

/// <summary>走到精华附近完成吞食，把精华压入消化队列。</summary>
public sealed class BehYaoDevour : BehaviourActionActor
{
    /// <summary>在精华附近按来源键领取；走丢或被抢先则结束任务。</summary>
    public override BehResult execute(Actor pActor)
    {
        ActorExtend extend = pObjectToExtend(pActor);
        if (extend == null || !YaoDigestionService.TryFindDeposit(
                new Vector2(pActor.current_position.x, pActor.current_position.y), 3f,
                out YaoDigestionService.YaoEssenceDeposit deposit))
            return BehResult.Stop;

        return YaoDigestionService.TryClaim(extend, deposit.SourceActorId, deposit.SourceDeathSequence)
            ? BehResult.Continue
            : BehResult.Stop;
    }

    private static ActorExtend pObjectToExtend(Actor pActor)
    {
        return pActor.GetExtend();
    }
}

/// <summary>灵气地点休整：停留在原地恢复妖力。</summary>
public sealed class BehYaoMeditate : BehaviourActionActor
{
    /// <summary>每次执行恢复少量妖力；妖力充足或灵气耗尽后结束休整。</summary>
    public override BehResult execute(Actor pActor)
    {
        ActorExtend extend = pActor.GetExtend();
        if (!extend.HasCultisys<Yao>()) return BehResult.Stop;
        ref Yao yao = ref extend.GetCultisys<Yao>();

        float maximum = pActor.stats[BaseStatses.MaxYaoPower.id];
        if (yao.yao_power >= maximum * 0.95f) return BehResult.Stop;

        YaoResourceService.Gain(extend, ref yao, maximum * 0.05f);
        return BehResult.Continue;
    }
}
