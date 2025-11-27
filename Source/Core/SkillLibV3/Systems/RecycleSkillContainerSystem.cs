using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

public class RecycleSkillContainerSystem : QuerySystem<SkillContainer>
{
    public RecycleSkillContainerSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagOccupied>());
    }
    protected override void OnUpdate()
    {
        Query.ForEach(((skills, entities) =>
        {
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities.EntityAt(i);
                if (e.GetIncomingLinks<SkillMasterRelation>().Count == 0)
                {
                    CommandBuffer.AddTag<TagRecycle>(e.Id);
                }
            }
        }));
    }
}