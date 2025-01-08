using System;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;

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
            Query.ForEachComponents([Hotfixable](ref StartSkillTrigger trigger, ref StartSkillContext context) =>
            {
                if (context.user== null || context.user.E.IsNull || context.user.Base == null || !context.user.Base.isAlive())
                {
                    return;
                }
                var action_meta = trigger.TriggerActionMeta;
                action_meta.Action(ref trigger, ref context, default,
                    context.user.GetSkillActionModifiers(action_meta.id, action_meta.default_modifier_container), default);
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