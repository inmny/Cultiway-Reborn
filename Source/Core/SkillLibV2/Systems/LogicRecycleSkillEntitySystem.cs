using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicRecycleSkillEntitySystem : QuerySystem<SkillEntity>
{
    public LogicRecycleSkillEntitySystem()
    {
        Filter.AllTags(Tags.Get<TagRecycle>());
    }

    protected override void OnUpdate()
    {
        CommandBuffer cmd_buf = CommandBuffer;
        foreach (Entity e in Query.Entities)
        {
            cmd_buf.DeleteEntity(e.Id);
            // 把trigger之类的也一起删了
            foreach (Entity child in e.ChildEntities) cmd_buf.DeleteEntity(child.Id);
        }

        cmd_buf.Playback();
    }
}