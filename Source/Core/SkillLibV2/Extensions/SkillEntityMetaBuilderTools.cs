using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.TrajectoryParams;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using UnityEngine;

namespace Cultiway.Core.SkillLibV2.Extensions;

public static class SkillEntityMetaBuilderTools
{
    public static SkillEntityMeta.MetaBuilder SetTrail(this SkillEntityMeta.MetaBuilder builder, TrailMeta trail_meta)
    {
        return builder.AddComponent(new Trail
        {
            meta = trail_meta
        });
    }

    public static SkillEntityMeta.MetaBuilder SetTrajectory(this SkillEntityMeta.MetaBuilder builder,
                                                            TrajectoryMeta                   trajectory_meta,
                                                            float velocity         = 1,
                                                            float angle_per_second = 1)
    {
        builder.AddComponent(new Trajectory { meta = trajectory_meta })
            .AddComponent(new Velocity(velocity));
        if (trajectory_meta.towards_velocity) builder.AddComponent(new Rotation());
        if (trajectory_meta.get_delta_position != null) builder.AddComponent(new Velocity(velocity));

        if (trajectory_meta.get_delta_rotation != null)
            builder.AddComponent(new Rotation()).AddComponent(new AngleVelocity(angle_per_second));

        return builder;
    }

    public static SkillEntityMeta.MetaBuilder AddSphereObjCollisionTrigger(
        this SkillEntityMeta.MetaBuilder builder, ObjCollisionTrigger trigger_config, float radius)
    {
        return builder.NewTrigger(trigger_config, out var collision_trigger_id, new ObjCollisionContext())
            .AddTriggerComponent(collision_trigger_id, new ColliderComponent
            {
                type = ColliderType.Sphere
            }).AddTriggerComponent(collision_trigger_id, new ColliderSphere
            {
                radius = radius
            });
    }

    public static SkillEntityMeta.MetaBuilder AddTimeReachTrigger(this SkillEntityMeta.MetaBuilder builder, float time,
                                                                  TriggerActionMeta<TimeReachTrigger, TimeReachContext>
                                                                      on_time_reach, bool loop = false)
    {
        return builder.NewTrigger(new TimeReachTrigger
        {
            target_time = time,
            loop = loop,
            TriggerActionMeta = on_time_reach
        }, out var _, new TimeReachContext());
    }

    public static SkillEntityMeta.MetaBuilder AddAnim(this SkillEntityMeta.MetaBuilder builder, Sprite[] frames,
                                                      float base_scale = 1f, float frame_interval = 0.2f,
                                                      bool loop = true)
    {
        return builder.AddComponent(new AnimData
            {
                frames = frames
            })
            .AddComponent(new AnimController
            {
                meta = new AnimControllerMeta
                {
                    frame_interval = frame_interval,
                    loop = true
                }
            })
            .AddComponent(new AnimBindRenderer())
            .AddComponent(new Position())
            .AddComponent(new Rotation())
            .AddComponent(new Scale(base_scale));
    }
}