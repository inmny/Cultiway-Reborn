using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Systems.Logic;

internal class RecycleAnimRendererSystem : QuerySystem<AnimBindRenderer>
{
    public RecycleAnimRendererSystem()
    {
        Filter.AllTags(Tags.Get<TagRecycle>());
    }

    protected override void OnUpdate()
    {
        Query.ForEachComponents((ref AnimBindRenderer binder) =>
        {
            if (binder.value != null)
            {
                binder.value.Return();
                binder.value = null;
            }
        });
    }
}