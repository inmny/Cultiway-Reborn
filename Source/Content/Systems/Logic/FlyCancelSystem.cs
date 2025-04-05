using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Systems.Logic;

public class FlyCancelSystem : QuerySystem<ActorBinder>
{
    public FlyCancelSystem()
    {
        Filter.AllComponents(ComponentTypes.Get<Xian>());
    }
    protected override void OnUpdate()
    {
        Query.ForEach(((binders, entities) =>
        {
            for (int i = 0; i < entities.Length; i++)
            {
                var a = binders[i].Actor;
                if (a == null || !a.isAlive()) continue;
                if (a.is_moving || a.isFollowingLocalPath()) continue;
                if (a.data.hasFlag(ContentActorDataKeys.IsFlying_flag))
                {
                    a.data.removeFlag(ContentActorDataKeys.IsFlying_flag);
                    a.setFlying(false);
                    a.precalcMovementSpeed(true);
                }
            }
        })).RunParallel();
    }
}