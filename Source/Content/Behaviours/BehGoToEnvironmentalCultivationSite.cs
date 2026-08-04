using ai;
using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>按环境规则声明的水域、山体和熔岩通行能力前往修炼地点。</summary>
public sealed class BehGoToEnvironmentalCultivationSite : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        if (pActor.beh_tile_target == null || pActor.beh_tile_target == pActor.current_tile)
            return BehResult.Continue;

        var actor = pActor.GetExtend();
        var rule = actor.GetMainCultibook()?.GetCultivateMethod()?.EnvironmentRule;
        if (rule == null) return BehResult.Continue;

        ExecuteEvent result = pActor.goTo(
            pActor.beh_tile_target,
            rule.WalkOnWater,
            rule.WalkOnBlocks,
            rule.WalkOnLava);
        if (result != ExecuteEvent.False) return BehResult.Continue;

        pActor.beh_tile_target = pActor.current_tile;
        return BehResult.Continue;
    }
}
