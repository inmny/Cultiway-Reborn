using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.AnimOverwrite;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicAnimFrameUpdateSystem : QuerySystem<AnimData, AnimController>
{
    private readonly ArchetypeQuery<AnimData, AnimFrameInterval> interval_query;
    private readonly ArchetypeQuery<AnimData, AnimLoop>          loop_query;

    public LogicAnimFrameUpdateSystem(EntityStore world)
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
        Filter.WithoutAnyComponents(ComponentTypes.Get<AnimFrameInterval, AnimLoop>());

        var single_filter = new QueryFilter();
        single_filter.WithoutAnyTags(Tags.Get<TagPrefab>());
        interval_query = world.Query<AnimData, AnimFrameInterval>(single_filter);
        loop_query = world.Query<AnimData, AnimLoop>(single_filter);
    }

    protected override void OnUpdate()
    {
        var i = 0;
        var time = Tick.time;
        Query.ForEachComponents((ref AnimData anim_data, ref AnimController controller) =>
        {
            var delta_time = time - anim_data.next_frame_time;
            if (delta_time < 0) return;
            AnimControllerMeta meta = controller.meta;
            var delta_frame_nr = Mathf.FloorToInt(delta_time / meta.frame_interval) + 1;

            anim_data.frame_idx += delta_frame_nr;
            anim_data.next_frame_time += delta_frame_nr * meta.frame_interval;
            var len = anim_data.frames.Length;
            if (meta.loop)
                anim_data.frame_idx %= len;
            else if (anim_data.frame_idx >= len) anim_data.frame_idx = len - 1;
        });
        interval_query.ForEachComponents((ref AnimData anim_data, ref AnimFrameInterval interval) =>
        {
            var delta_time = time - anim_data.next_frame_time;
            if (delta_time < 0) return;
            var delta_frame_nr = Mathf.FloorToInt(delta_time / interval.value) + 1;

            anim_data.frame_idx += delta_frame_nr;
            anim_data.next_frame_time += delta_frame_nr * interval.value;
        });
        loop_query.ForEachComponents((ref AnimData anim_data, ref AnimLoop loop) =>
        {
            var len = anim_data.frames.Length;
            if (loop.value)
                anim_data.frame_idx %= len;
            else if (anim_data.frame_idx >= len) anim_data.frame_idx = len - 1;
        });
    }
}