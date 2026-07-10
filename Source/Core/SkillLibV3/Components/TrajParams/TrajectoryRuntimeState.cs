using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

public struct TrajectoryRuntimeState : IComponent
{
    public bool Initialized;
    public bool Returning;
    public Vector3 StartPosition;
    public Vector3 StartDirection;
    public float Elapsed;
    public float DistanceTravelled;
    public float Phase;
    public float Timer;
    public int StepIndex;
}
