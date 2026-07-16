using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>维持法器召唤物，并在活动终止或寿命耗尽后回收真实生物。</summary>
public sealed class ArtifactSummonSystem : QuerySystem<ActorBinder, ArtifactSummon>
{
    private readonly List<Actor> expired = new();

    protected override void OnUpdate()
    {
        double now = World.world.getCurWorldTime();
        expired.Clear();
        Query.ForEachEntity((ref ActorBinder binder, ref ArtifactSummon summon, Entity _) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || actor.isRekt()) return;
            if (now >= summon.expires_at || !ArtifactSummonService.IsMaintained(summon))
            {
                expired.Add(actor);
                return;
            }

            Actor owner = summon.controller.GetComponent<ActorBinder>().Actor;
            if (actor.kingdom != owner.kingdom) actor.joinKingdom(owner.kingdom);
        });

        for (int i = 0; i < expired.Count; i++)
        {
            if (!expired[i].isRekt()) expired[i].dieAndDestroy(AttackType.None);
        }
    }
}
