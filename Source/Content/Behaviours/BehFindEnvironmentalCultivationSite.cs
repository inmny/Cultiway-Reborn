using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>根据主修功法的环境规则选定附近修炼地点。</summary>
public sealed class BehFindEnvironmentalCultivationSite : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        var actor = pActor.GetExtend();
        var method = actor.GetMainCultibook()?.GetCultivateMethod();
        pActor.beh_tile_target = CultivationEnvironmentService.ResolveSite(actor, method) ?? pActor.current_tile;
        return BehResult.Continue;
    }
}
