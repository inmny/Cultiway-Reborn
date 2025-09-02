using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Components;

public struct SkillContext : IComponent
{
    public float Strength;
    public BaseSimObject SourceObj;
    public BaseSimObject TargetObj;
    public Vector3 TargetPos;
    public Vector3 TargetDir;
}