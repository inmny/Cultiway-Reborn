using Cultiway.Core.SkillLib.Components;
using Cultiway.Core.SkillLib.Components.Triggers;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLib.Systems.Logic;

public class AnimLoopTriggerSystem : QuerySystem<AnimLoopEndTrigger, SkillEntityComponent>
{
    public AnimLoopTriggerSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<PrefabTag>());
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref AnimLoopEndTrigger trigger, ref SkillEntityComponent skill_entity_asset,
                             Entity                 skill_entity) =>
        {
            var anim_setting = skill_entity_asset.asset.anim_setting;
            if (anim_setting       == null) return;
            if (trigger.loop_times != trigger.target_loop_times) return;
            var action_entity = trigger.ActionContainer;
            action_entity.GetComponent<AnimLoopEndActionContainerInfo>().Meta
                         .action(ref trigger, ref skill_entity, ref action_entity);
        });
    }
}