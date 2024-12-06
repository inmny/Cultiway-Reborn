using System;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Cultiway.Utils;
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
        try
        {
            Query.ForEachComponents((ref StartSkillTrigger trigger, ref StartSkillContext context) =>
            {
                var action_meta = trigger.TriggerActionMeta;
                action_meta.Action(ref trigger, ref context, default,
                    context.user.GetSkillActionEntity(action_meta.id, action_meta.default_modifier_container));
            });
        }
        catch (Exception e)
        {
            ModClass.LogError(SystemUtils.GetFullExceptionMessage(e));
        }

        CommandBuffer cmd_buf = CommandBuffer;
        foreach (Entity e in Query.Entities) cmd_buf.DeleteEntity(e.Id);

        cmd_buf.Playback();
    }
}