using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Systems.Logic;

public class EntityRecycleSystem : QuerySystem
{
    public EntityRecycleSystem()
    {
        Filter.AllTags(Tags.Get<TagRecycle>());
        Filter.WithoutAllTags(Tags.Get<TagPrefab>());
    }
    protected override void OnUpdate()
    {
        foreach (Entity e in Query.Entities)
        {
            delete_entity(e);
        }

        return;

        void delete_entity(Entity e)
        {
            foreach (Entity child in e.ChildEntities) delete_entity(child);
            e.DeleteEntity();
        }
    }
}