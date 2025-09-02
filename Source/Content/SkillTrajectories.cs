using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

public class SkillTrajectories : ExtendLibrary<TrajectoryAsset, SkillTrajectories>
{
    public static TrajectoryAsset TowardsDirection { get; private set; }
    public static TrajectoryAsset TowardsPosition { get; private set; }
    public static TrajectoryAsset TowardsTarget { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        TowardsDirection.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            pos.value += rot.value.normalized * dt * e.GetComponent<Velocity>().Value;
            rot.value = context.TargetDir;
        };
        TowardsPosition.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            var delta = context.TargetPos - pos.value;
            var dir = delta.normalized;
            var move_distance = dt * e.GetComponent<Velocity>().Value;

            var actual_to_move = move_distance * dir;
            if (Mathf.Abs(actual_to_move.x) > Mathf.Abs(delta.x))
            {
                actual_to_move.x = delta.x; 
            }

            if (Mathf.Abs(actual_to_move.y) > Mathf.Abs(delta.y))
            {
                actual_to_move.y = delta.y;
            }

            if (Mathf.Abs(actual_to_move.z) > Mathf.Abs(delta.z))
            {
                actual_to_move.z = delta.z;
            }

            pos.value += actual_to_move;
            rot.value = actual_to_move.normalized;
        };
        TowardsTarget.Action = (ref SkillContext context, ref Position pos, ref Rotation rot, Entity e, float dt) =>
        {
            var target_xy = context.TargetObj.current_position;
            var delta = new Vector3(target_xy.x, target_xy.y, context.TargetObj.getHeight()) - pos.value;
            var dir = delta.normalized;
            var move_distance = dt * e.GetComponent<Velocity>().Value;

            var actual_to_move = move_distance * dir;
            if (Mathf.Abs(actual_to_move.x) > Mathf.Abs(delta.x))
            {
                actual_to_move.x = delta.x;
            }

            if (Mathf.Abs(actual_to_move.y) > Mathf.Abs(delta.y))
            {
                actual_to_move.y = delta.y;
            }

            if (Mathf.Abs(actual_to_move.z) > Mathf.Abs(delta.z))
            {
                actual_to_move.z = delta.z;
            }

            pos.value += actual_to_move;
            rot.value = actual_to_move.normalized;
        };
    }
}