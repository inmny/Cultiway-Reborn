using Cultiway.Core.SkillLib.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLib.Systems.Logic;

public class SkillEntityRecycleSystem : QuerySystem<SkillInfo>
{
    protected override void OnUpdate()
    {
        Query.AllTags(Tags.Get<RecycleTag>());
        Query.WithoutAllTags(Tags.Get<PrefabTag>());

        Query.ForEachEntity(((ref SkillInfo _, Entity e) => { CommandBuffer.DeleteEntity(e.Id); }));
        CommandBuffer.Playback();
    }
}