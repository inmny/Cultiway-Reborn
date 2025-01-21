using Cultiway.Core.Components;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Systems.Logic;

public class DisposeActorExtendSystem : QuerySystem<ActorBinder>
{
    protected override void OnUpdate()
    {
        Query.ForEach(((binders, entities) =>
        {
            for (int i = 0; i < binders.Length; i++)
            {
                if (binders[i].Actor == null)
                {
                    binders[i].AE.Dispose();
                }
            }
        })).RunParallel();
    }
}