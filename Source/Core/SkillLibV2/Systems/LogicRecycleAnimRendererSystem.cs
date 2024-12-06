using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2.Systems;

internal class LogicRecycleAnimRendererSystem : QuerySystem<AnimBindRenderer>
{
    public LogicRecycleAnimRendererSystem()
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