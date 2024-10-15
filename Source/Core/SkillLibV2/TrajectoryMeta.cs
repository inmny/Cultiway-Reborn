using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using UnityEngine;
using Position = Cultiway.Core.SkillLibV2.Components.Position;

namespace Cultiway.Core.SkillLibV2;

public class TrajectoryMeta
{
    public delegate Vector3 GetDeltaPosition(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity);

    public GetDeltaPosition calc;
    public bool             towards_velocity;
}