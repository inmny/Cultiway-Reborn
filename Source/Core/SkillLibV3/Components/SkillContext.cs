using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Components;

public struct SkillContext : IComponent
{
    public float Strength;
    public BaseSimObject SourceObj;
    public BaseSimObject TargetObj;
    public Vector2 TargetPos;
    public Vector2 TargetDir;
}