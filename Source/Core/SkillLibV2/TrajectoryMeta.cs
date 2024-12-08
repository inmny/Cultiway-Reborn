using Cultiway.Abstract;
using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using UnityEngine;
using Position = Cultiway.Core.SkillLibV2.Components.Position;
using Rotation = Cultiway.Core.SkillLibV2.Components.Rotation;

namespace Cultiway.Core.SkillLibV2;

public class TrajectoryMeta : ICanCopy
{
    public delegate Vector3 GetDeltaPosition(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity);

    public delegate Vector3 GetDeltaRotation(float dt, ref Rotation pos, ref Trajectory traj, Entity skill_entity);

    public delegate Vector3 GetDeltaScale(float dt, ref Scale scale, ref Trajectory traj, Entity skill_entity);

    public GetDeltaPosition get_delta_position;
    public GetDeltaRotation get_delta_rotation;
    public GetDeltaScale get_delta_scale;

    public bool towards_velocity;
}