using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>独立于 AI 工作检查元婴寿命，避免停工或控制状态绕过消亡。</summary>
public sealed class YuanyingSoulLifetimeSystem : QuerySystem<ActorBinder, YuanyingSoulState>
{
    private readonly List<Actor> expired = new();

    protected override void OnUpdate()
    {
        double now = World.world.getCurWorldTime();
        expired.Clear();
        Query.ForEachEntity((ref ActorBinder binder, ref YuanyingSoulState state, Entity _) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || actor.isRekt()) return;
            if (now >= state.expires_at || actor.asset != Actors.YuanyingSoul)
                expired.Add(actor);
        });

        for (var i = 0; i < expired.Count; i++)
            YuanyingPossessionService.TerminateSoul(expired[i], "lifetime_or_form_invalid");
    }
}
