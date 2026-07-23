using System;
using Cultiway.Core.Components;
using Cultiway.Core.Components.AnimOverwrite;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.Systems.Logic;

public class AnimFrameUpdateSystem : QuerySystem<AnimData, AnimController>
{
    private readonly ArchetypeQuery<AnimData, AnimFrameInterval, AnimController> interval_query;
    private readonly ArchetypeQuery<AnimData, AnimLoop, AnimController>          loop_query;
    private readonly ArchetypeQuery<AnimData, AnimFrameInterval, AnimLoop>       interval_loop_query;

    public AnimFrameUpdateSystem(EntityStore world)
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
        Filter.WithoutAnyComponents(ComponentTypes.Get<AnimFrameInterval, AnimLoop>());

        var interval_filter = new QueryFilter();
        interval_filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
        interval_filter.WithoutAnyComponents(ComponentTypes.Get<AnimLoop>());
        interval_query = world.Query<AnimData, AnimFrameInterval, AnimController>(interval_filter);

        var loop_filter = new QueryFilter();
        loop_filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
        loop_filter.WithoutAnyComponents(ComponentTypes.Get<AnimFrameInterval>());
        loop_query = world.Query<AnimData, AnimLoop, AnimController>(loop_filter);

        var interval_loop_filter = new QueryFilter();
        interval_loop_filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
        interval_loop_query = world.Query<AnimData, AnimFrameInterval, AnimLoop>(interval_loop_filter);
    }

    protected override void OnUpdate()
    {
        var deltaTime = Tick.deltaTime;
        Query.ForEachEntity((ref AnimData anim_data, ref AnimController controller, Entity entity) =>
        {
            AnimControllerMeta meta = controller.meta;
            AdvanceAnim(ref anim_data, Mathf.Max(0.01f, meta.frame_interval), meta.loop, deltaTime, entity);
        });
        interval_query.ForEachEntity((ref AnimData anim_data, ref AnimFrameInterval interval,
            ref AnimController controller, Entity entity) =>
        {
            AdvanceAnim(ref anim_data, Mathf.Max(0.01f, interval.value), controller.meta.loop, deltaTime, entity);
        });
        loop_query.ForEachEntity((ref AnimData anim_data, ref AnimLoop loop, ref AnimController controller,
            Entity entity) =>
        {
            AdvanceAnim(ref anim_data, Mathf.Max(0.01f, controller.meta.frame_interval), loop.value, deltaTime,
                entity);
        });
        interval_loop_query.ForEachEntity((ref AnimData anim_data, ref AnimFrameInterval interval,
            ref AnimLoop loop, Entity entity) =>
        {
            AdvanceAnim(ref anim_data, Mathf.Max(0.01f, interval.value), loop.value, deltaTime, entity);
        });
    }

    private static void AdvanceAnim(ref AnimData animData, float frameInterval, bool loop, float deltaTime,
        Entity entity)
    {
        if (animData.frames == null || animData.frames.Length == 0) return;
        var len = animData.frames.Length;
        if (animData.frame_idx < 0)
            animData.frame_idx = 0;
        else if (animData.frame_idx >= len)
        {
            if (loop)
                animData.frame_idx %= len;
            else
            {
                animData.frame_idx = len - 1;
                return;
            }
        }

        if (!loop && animData.frame_idx >= len - 1)
        {
            if (deltaTime <= 0f) return;
            if (animData.frame_timer < 0f) animData.frame_timer = 0f;
            animData.frame_timer = Mathf.Min(frameInterval, animData.frame_timer + deltaTime);
            return;
        }
        if (deltaTime <= 0f) return;
        if (animData.frame_timer < 0f) animData.frame_timer = 0f;

        animData.frame_timer += deltaTime;
        if (animData.frame_timer < frameInterval) return;

        float accumulatedTime = animData.frame_timer;
        var deltaFrameNr = Mathf.FloorToInt(animData.frame_timer / frameInterval);
        if (loop)
        {
            animData.frame_timer -= deltaFrameNr * frameInterval;
            animData.frame_idx += deltaFrameNr;
            animData.frame_idx %= len;
        }
        else
        {
            int framesToLast = len - 1 - animData.frame_idx;
            if (deltaFrameNr >= framesToLast)
            {
                animData.frame_idx = len - 1;
                animData.frame_timer = Mathf.Clamp(
                    accumulatedTime - framesToLast * frameInterval,
                    0f,
                    frameInterval);
            }
            else
            {
                animData.frame_idx += deltaFrameNr;
                animData.frame_timer -= deltaFrameNr * frameInterval;
            }
        }
    }

    private static bool IsTalismanAnim(ref AnimData animData)
    {
        if (animData.frames == null || animData.frames.Length == 0) return false;
        var idx = Mathf.Clamp(animData.frame_idx, 0, animData.frames.Length - 1);
        var frame = animData.frames[idx];
        return frame != null && !string.IsNullOrEmpty(frame.name) &&
               frame.name.StartsWith("talisman_", StringComparison.Ordinal);
    }
}
