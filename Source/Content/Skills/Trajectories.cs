using Cultiway.Abstract;
using Cultiway.Core.SkillLib;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;
using Rotation = Cultiway.Core.SkillLib.Components.Rotation;

namespace Cultiway.Content.Skills;

public class Trajectories : ExtendLibrary<TrajectoryAsset, Trajectories>
{
    public static TrajectoryAsset TorwardTargetPos { get; private set; }
    public static TrajectoryAsset TrackTargetObj   { get; }

    protected override void OnInit()
    {
        TorwardTargetPos = Add(new TrajectoryAsset
        {
            id = nameof(TorwardTargetPos),
            update_trajectory_action = torward_target_pos,
            velocity_required = true,
            default_velocity = Vector3.one,
            rotation_required = true,
            default_rotation = Quaternion.identity
        });
    }

    [Hotfixable]
    private static Vector3 torward_target_obj(float         t, float dt, ref Vector3 current_pos, ref Vector3 velo,
                                              BaseSimObject target_obj, Vector3 target_pos, ref Entity skill_entity)
    {
        Vector3 delta = target_obj?.curTransformPosition ?? target_pos - current_pos;
        Vector3 dir = delta.normalized;

        dir.Scale(velo);
        Vector3 move = dir * dt;

        var all_reach = true;
        for (var i = 0; i < 3; i++)
            if (Mathf.Abs(move[i]) > Mathf.Abs(delta[i]))
                move[i] = delta[i];
            else
                all_reach = false;

        if (!all_reach)
        {
            ref Rotation rot = ref skill_entity.GetComponent<Rotation>();
            var angle = Vector2.SignedAngle(Vector2.right, new Vector2(dir.x, dir.y));
            rot.value = Quaternion.Euler(0, 0, angle);
        }

        return current_pos + move;
    }

    [Hotfixable]
    private static Vector3 torward_target_pos(float         t, float dt, ref Vector3 current_pos, ref Vector3 velo,
                                              BaseSimObject target_obj, Vector3 target_pos, ref Entity skill_entity)
    {
        var delta = target_pos - current_pos;
        var dir = delta.normalized;

        dir.Scale(velo);
        var move = dir * dt;

        bool all_reach = true;
        for (int i = 0; i < 3; i++)
        {
            if (Mathf.Abs(move[i]) > Mathf.Abs(delta[i]))
            {
                move[i] = delta[i];
            }
            else
            {
                all_reach = false;
            }
        }

        if (!all_reach)
        {
            ref var rot = ref skill_entity.GetComponent<Rotation>();
            float angle = Vector2.SignedAngle(Vector2.right, new(dir.x, dir.y));
            rot.value = Quaternion.Euler(0, 0, angle);
        }

        return current_pos + move;
    }
}