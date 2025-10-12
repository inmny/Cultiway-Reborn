using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.Systems.Logic;

public class RemoveDirtyTagSystem : QuerySystem
{
    public RemoveDirtyTagSystem()
    {
        Filter.AllTags(Tags.Get<TagDirty>());
    }
    protected override void OnUpdate()
    {
        if (Query.Count == 0) return;
        foreach (var e in Query.Entities)
        {
            CommandBuffer.RemoveTag<TagDirty>(e.Id);
        }
        CommandBuffer.Playback();
    }
}