using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Systems.Logic;

public class FlyCancelSystem : QuerySystem<ActorBinder, Xian>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity([Hotfixable](ref ActorBinder actor_binder, ref Xian xian, Entity e) =>
        {
            if (!actor_binder.Actor.isFollowingLocalPath() && !actor_binder.Actor.is_moving &&
                actor_binder.Actor.data.hasFlag(ContentActorDataKeys.IsFlying_flag))
            {
                actor_binder.Actor.data.removeFlag(ContentActorDataKeys.IsFlying_flag);
                actor_binder.Actor.flying = false;
            }
        });
    }
}