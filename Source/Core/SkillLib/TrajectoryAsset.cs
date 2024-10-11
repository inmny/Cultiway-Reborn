using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLib;

public class TrajectoryAsset : Asset
{
    public delegate Vector3 Update(float         t, float dt, ref Vector3 current_pos, ref Vector3 velo,
                                   BaseSimObject target_obj,
                                   Vector3       target_pos, ref Entity skill_entity);

    public Quaternion default_rotation;
    public Vector3    default_velocity;

    public bool   rotation_required = false;
    public Update update_trajectory_action;
    public bool   velocity_required = false;
}