using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.TrajectoryParams;
using Cultiway.Core.SkillLibV2.Components.Triggers;
using UnityEngine;

namespace Cultiway.Core.SkillLibV2.Extensions;

public static class SkillEntityMetaBuilderTools
{
    public static SkillEntityMeta.MetaBuilder SetTrajectory(this SkillEntityMeta.MetaBuilder builder,
                                                            TrajectoryMeta                   trajectory_meta,
                                                            float                            velocity)
    {
        builder.AddComponent(new Trajectory { meta = trajectory_meta })
            .AddComponent(new Velocity(velocity));
        if (trajectory_meta.towards_velocity) builder.AddComponent(new Rotation());

        return builder;
    }

    public static SkillEntityMeta.MetaBuilder AddSphereObjCollisionTrigger(
        this SkillEntityMeta.MetaBuilder builder, ObjCollisionTrigger trigger_config, float radius)
    {
        return builder.NewTrigger(trigger_config, out var collision_trigger_id, new ObjCollisionContext())
            .AddTriggerComponent(collision_trigger_id, new Collider
            {
                type = ColliderType.Sphere
            }).AddTriggerComponent(collision_trigger_id, new ColliderSphere
            {
                radius = radius
            });
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