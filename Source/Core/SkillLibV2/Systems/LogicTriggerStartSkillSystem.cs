using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.Triggers;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicTriggerStartSkillSystem : QuerySystem<StartSkillTrigger, StartSkillContext>
{
    public LogicTriggerStartSkillSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
    }

    protected override void OnUpdate()
    {
        Query.ForEachComponents((ref StartSkillTrigger trigger, ref StartSkillContext context) => { });
        CommandBuffer cmd_buf = CommandBuffer;
        foreach (Entity e in Query.Entities) cmd_buf.DeleteEntity(e.Id);

        cmd_buf.Playback();
    }
}