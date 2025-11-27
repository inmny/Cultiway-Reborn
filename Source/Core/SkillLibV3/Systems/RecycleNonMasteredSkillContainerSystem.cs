using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

public class RecycleNonMasteredSkillContainerSystem : QuerySystem<SkillContainer>
{
    public RecycleNonMasteredSkillContainerSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagOccupied>());
    }
    protected override void OnUpdate()
    {
        Query.ForEachEntity(((ref SkillContainer container, Entity entity) =>
        {
            if (entity.GetIncomingLinks<SkillMasterRelation>().Count == 0)
            {
                CommandBuffer.AddTag<TagRecycle>(entity.Id);
            }
        }));       
        CommandBuffer.Playback();
    }
}