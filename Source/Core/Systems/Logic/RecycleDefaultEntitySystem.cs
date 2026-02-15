using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;

namespace Cultiway.Core.Systems.Logic;

public class RecycleDefaultEntitySystem : QuerySystem
{
    public RecycleDefaultEntitySystem()
    {
        Filter.AllTags(Tags.Get<TagRecycle>());
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
    }

    protected override void OnUpdate()
    {
        if (Query.Count == 0) return;
        foreach (Entity e in Query.Entities)
        {
            delete_entity(e);
        }
        CommandBuffer.Playback();

        return;
        [Hotfixable]
        void delete_entity(Entity e)
        {
            foreach (Entity child in e.ChildEntities)
            {
                if (child.IsNull)
                {
                    continue;
                }
                delete_entity(child);
            }
            CommandBuffer.DeleteEntity(e.Id);
        }
    }
}